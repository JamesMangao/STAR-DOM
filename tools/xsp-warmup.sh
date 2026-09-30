#!/bin/sh
# Compile-check every .aspx under the web root by starting XSP4 and requesting
# each page, then fail the Docker build if any page failed to COMPILE.
#
# Why this exists: XSP4 compiles .aspx on first request by shelling out to vbnc.
# If vbnc cannot be launched, every page returns HTTP 500 and the site looks
# deployed but is completely dead. A warm-up that only curls a fixed list of
# pages, discards the status code, and ends in `|| true` cannot detect that --
# which is exactly how a broken image reached production.
#
# IMPORTANT: at build time there are no Supabase env vars, so Db.ConnString()
# falls back to an unreachable local PostgreSQL. Pages that query the database
# will legitimately 500 here. So this script does NOT fail on 5xx -- it fails on
# the *compilation* failure signature, and on the absence of any compiled
# assembly in XSP4's cache. A database error and a compile error are different
# failures and only one of them should break the build.

set -u

ROOT="${1:-/app/STAR-DOM-Web}"
PORT="${2:-19876}"
TMPDIR="${TMPDIR:-/app/.xsp-cache}"
COMPILE_SIGNATURES="Error running vbnc|Error running mcs|VBCodeGenerator|CodeDomProvider|Could not load file or assembly .*STAR_DOM_Web"

export TMPDIR
mkdir -p "$TMPDIR"

echo ">> web root : $ROOT"
echo ">> cache    : $TMPDIR"
echo ">> port     : $PORT"

# ---------------------------------------------------------------------------
# 1. The precompiled Web Application assembly must exist. Without it XSP4 has
#    no Inherits base class to reference and falls back to vbnc for everything.
# ---------------------------------------------------------------------------
APP_DLL="$ROOT/bin/STAR_DOM_Web.dll"
if [ ! -f "$APP_DLL" ]; then
    echo "FATAL: $APP_DLL is missing."
    echo "       msbuild did not produce the Web Application assembly, so XSP4"
    echo "       would try to compile code-behind at request time. Check the"
    echo "       msbuild step above and that bin/ is not in .dockerignore."
    exit 1
fi
echo ">> app assembly present: $(ls -l "$APP_DLL" | awk '{print $5" bytes"}')"

# ---------------------------------------------------------------------------
# 2. Start XSP4 and wait until it actually answers.
# ---------------------------------------------------------------------------
echo ">> starting xsp4..."
mono /usr/lib/mono/4.5/xsp4.exe --nonstop --port="$PORT" --address=127.0.0.1 \
     --root="$ROOT" >"$TMPDIR/xsp4.log" 2>&1 &
XSP_PID=$!

cleanup() {
    kill "$XSP_PID" 2>/dev/null || true
    wait "$XSP_PID" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

i=0
READY=0
while [ "$i" -lt 60 ]; do
    if ! kill -0 "$XSP_PID" 2>/dev/null; then
        echo "FATAL: xsp4 exited during startup. Log:"
        cat "$TMPDIR/xsp4.log"
        exit 1
    fi
    code=$(curl -s -o /dev/null -w '%{http_code}' \
           "http://127.0.0.1:$PORT/Login.aspx" 2>/dev/null || echo 000)
    if [ "$code" != "000" ]; then
        READY=1
        break
    fi
    i=$((i + 1))
    sleep 1
done

if [ "$READY" -ne 1 ]; then
    echo "FATAL: xsp4 never answered on port $PORT within 60s. Log:"
    cat "$TMPDIR/xsp4.log"
    exit 1
fi
echo ">> xsp4 is up (pid $XSP_PID)"

# ---------------------------------------------------------------------------
# 3. Request EVERY .aspx, discovered from disk so a new page can never be
#    silently left out of this check again.
# ---------------------------------------------------------------------------
TOTAL=0
FAILED=0
find "$ROOT" -name '*.aspx' -not -path '*/bin/*' -not -path '*/obj/*' \
     -not -path '*/packages/*' | sort | while :; do
    read -r f || break
    TOTAL=$((TOTAL + 1))

    page="/${f#"$ROOT"}"
    body="$TMPDIR/body.tmp"
    code=$(curl -s -o "$body" -w '%{http_code}' \
           --max-time 120 "http://127.0.0.1:$PORT$page" 2>/dev/null || echo 000)

    if grep -Eq "$COMPILE_SIGNATURES" "$body" 2>/dev/null; then
        FAILED=$((FAILED + 1))
        echo "   COMPILE-FAIL $page (HTTP $code)"
        sed -n '1,12p' "$body" | sed 's/^/        /'
    else
        echo "   ok          $page (HTTP $code)"
    fi
    # counters live in this subshell; publish them for the summary below
    echo "$TOTAL $FAILED" > "$TMPDIR/counters"
done

TOTAL=0
FAILED=0
if [ -f "$TMPDIR/counters" ]; then
    read -r TOTAL FAILED < "$TMPDIR/counters"
fi
echo ">> requested $TOTAL page(s), $FAILED compile failure(s)"

# ---------------------------------------------------------------------------
# 4. Independent proof that compilation actually happened: XSP4 caches the
#    assemblies it builds, so an empty cache means nothing was ever compiled.
# ---------------------------------------------------------------------------
CACHED=$(find "$TMPDIR" -name '*.dll' 2>/dev/null | wc -l | tr -d ' ')
echo ">> compiled assemblies in cache: $CACHED"

cleanup
trap - EXIT INT TERM

if [ "$FAILED" -gt 0 ]; then
    echo ""
    echo "FATAL: $FAILED page(s) failed to compile. The runtime compiler (vbnc)"
    echo "       cannot build this application, so the deployed site would 500 on"
    echo "       every request. Aborting the build."
    echo "       xsp4 log: $TMPDIR/xsp4.log"
    exit 1
fi

if [ "$CACHED" -eq 0 ]; then
    echo ""
    echo "FATAL: no compiled assemblies in $TMPDIR. Nothing was actually"
    echo "       compiled, so the warm-up proved nothing."
    exit 1
fi

echo ">> compile gate PASSED - every page builds, cache is populated"
