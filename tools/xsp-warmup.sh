#!/bin/sh
# Compile-check every .aspx under the web root by starting XSP4 and requesting
# each page, then fail the Docker build if the runtime could not build them.
#
# Why this exists: XSP4 compiles .aspx on first request by shelling out to vbnc.
# If vbnc cannot be launched, every page returns HTTP 500 and the site looks
# deployed but is completely dead. A warm-up that only curls a fixed list of
# pages, discards the status code, and ends in `|| true` cannot detect that --
# which is exactly how a broken image reached production.
#
# IMPORTANT: at build time there are no Supabase env vars, so Db.ConnString()
# falls back to Host=localhost;Port=5432 where nothing is listening. Pages that
# query the database therefore legitimately 500 here, and that must not fail the
# build. But "tolerate every 5xx" is exactly how a completely broken image once
# shipped: this gate passed an image whose every request died with
# "VBNC99999: Failed to resolve assembly". So a 5xx is tolerated only when the
# body is demonstrably a *connection* failure. Any other 5xx -- a compile error,
# a missing assembly, a type-load failure -- fails the build.
set -u

ROOT="${1:-/app/STAR-DOM-Web}"
PORT="${2:-19876}"
TMPDIR="${TMPDIR:-/app/.xsp-cache}"
COMPILE_SIGNATURES="Error running vbnc|Error running mcs|VBNC99999|VBCodeGenerator|CodeDomProvider|Failed to resolve assembly|Could not load file or assembly|CompilationException|Error compiling|Error origin: Compiler|BC[0-9]{5}"
DB_ERROR_SIGNATURES="Npgsql|SocketException|Connection refused|ECONNREFUSED|No such host|Failed to connect|Unable to connect|Connection timed out|Name or service not known"

export TMPDIR
mkdir -p "$TMPDIR"

# Read an HTTP status as exactly three digits, or 000 when the request never
# completed. Never write `curl ... || echo 000`: curl already emits 000 through
# -w on failure, so that idiom yields "000000", which silently defeats every
# comparison against "000" and lets a dead server look healthy.
http_code() {
    _c=$(curl -s -o "$2" -w '%{http_code}' --max-time "$3" "$1" 2>/dev/null)
    _c=$(printf '%s' "$_c" | tr -cd '0-9')
    if [ "${#_c}" -ne 3 ]; then
        _c=000
    fi
    printf '%s' "$_c"
}

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
if command -v xsp4 >/dev/null 2>&1; then
    XSP_CMD=xsp4
elif [ -f /usr/lib/mono/4.5/xsp4.exe ]; then
    XSP_CMD="mono /usr/lib/mono/4.5/xsp4.exe"
else
    echo "FATAL: xsp4 is not installed (no xsp4 on PATH, no /usr/lib/mono/4.5/xsp4.exe)."
    exit 1
fi

echo ">> starting xsp4 via: $XSP_CMD"
# shellcheck disable=SC2086
$XSP_CMD --nonstop --port="$PORT" --address=127.0.0.1 --root="$ROOT" \
    >"$TMPDIR/xsp4.log" 2>&1 &
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
    code=$(http_code "http://127.0.0.1:$PORT/Login.aspx" /dev/null 10)
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
echo ">> xsp4 is up (pid $XSP_PID, first status $code)"

# ---------------------------------------------------------------------------
# 3. Request EVERY .aspx, discovered from disk so a new page can never be
#    silently left out of this check again.
# ---------------------------------------------------------------------------
find "$ROOT" -name '*.aspx' -not -path '*/bin/*' -not -path '*/obj/*' \
     -not -path '*/packages/*' | sort > "$TMPDIR/pages"
echo ">> discovered $(wc -l < "$TMPDIR/pages" | tr -d ' ') page(s) on disk"

TOTAL=0
FAILED=0
NORESP=0
UNEXPLAINED=0
while IFS= read -r f; do
    [ -n "$f" ] || continue
    TOTAL=$((TOTAL + 1))

    page="/${f#"$ROOT"}"
    body="$TMPDIR/body.tmp"
    code=$(http_code "http://127.0.0.1:$PORT$page" "$body" 120)

    if [ "$code" = "000" ]; then
        # No HTTP response at all: the server dropped the connection or never
        # answered. Reporting this as "ok" is how a completely dead site once
        # passed this gate, so it is a hard failure.
        NORESP=$((NORESP + 1))
        echo "   NO-RESPONSE $page"
    elif grep -Eq "$COMPILE_SIGNATURES" "$body" 2>/dev/null; then
        FAILED=$((FAILED + 1))
        echo "   COMPILE-FAIL $page (HTTP $code)"
        sed -n '1,12p' "$body" | sed 's/^/        /'
    elif [ "$code" -ge 500 ] 2>/dev/null && ! grep -Eq "$DB_ERROR_SIGNATURES" "$body" 2>/dev/null; then
        # A 5xx that is not the expected "no database at build time" failure.
        # Something else is wrong and must not reach production.
        UNEXPLAINED=$((UNEXPLAINED + 1))
        echo "   UNEXPLAINED-5XX $page (HTTP $code)"
        sed -n '1,12p' "$body" | sed 's/^/        /'
    else
        echo "   ok          $page (HTTP $code)"
    fi
done < "$TMPDIR/pages"

echo ">> requested $TOTAL page(s), $FAILED compile failure(s), $NORESP no-response(s), $UNEXPLAINED unexplained 5xx"

# ---------------------------------------------------------------------------
# 4. Independent proof that compilation actually happened: XSP4 caches the
#    assemblies it builds, so an empty cache means nothing was ever compiled.
# ---------------------------------------------------------------------------
CACHED=$(find "$TMPDIR" -name '*.dll' 2>/dev/null | wc -l | tr -d ' ')
echo ">> compiled assemblies in cache: $CACHED"

if [ "$NORESP" -gt 0 ]; then
    echo ">> xsp4 log:"
    sed 's/^/       /' "$TMPDIR/xsp4.log"
fi

cleanup
trap - EXIT INT TERM

if [ "$NORESP" -gt 0 ]; then
    echo ""
    echo "FATAL: $NORESP page(s) returned no HTTP response (status 000)."
    echo "       The warm-up proves nothing when the server is not answering,"
    echo "       so it is treated as a failure rather than a pass. xsp4 log above."
    exit 1
fi

if [ "$FAILED" -gt 0 ]; then
    echo ""
    echo "FATAL: $FAILED page(s) failed to compile. The runtime compiler (vbnc)"
    echo "       cannot build this application, so the deployed site would 500 on"
    echo "       every request. Aborting the build."
    echo "       xsp4 log: $TMPDIR/xsp4.log"
    exit 1
fi

if [ "$UNEXPLAINED" -gt 0 ]; then
    echo ""
    echo "FATAL: $UNEXPLAINED page(s) returned 5xx for a reason other than the"
    echo "       expected 'no database configured at build time'. A 5xx is only"
    echo "       tolerated when the body shows a connection failure, so these are"
    echo "       real defects and must not be deployed. Bodies are above."
    exit 1
fi

if [ "$CACHED" -eq 0 ]; then
    echo ""
    echo "FATAL: no compiled assemblies in $TMPDIR. Nothing was actually"
    echo "       compiled, so the warm-up proved nothing."
    echo "       xsp4 log:"
    sed 's/^/       /' "$TMPDIR/xsp4.log"
    exit 1
fi

echo ">> compile gate PASSED - every page builds, cache is populated"
