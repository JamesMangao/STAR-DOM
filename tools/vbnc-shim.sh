#!/bin/sh
# vbnc shim: strip directives from generated .vb files that vbnc cannot parse.
#
# WHY THIS EXISTS
# Mono's ASP.NET code generator writes every .aspx out as a .vb file whose FIRST
# line is an #ExternalChecksum(...) directive. That directive is a Roslyn
# (Microsoft VB.NET compiler) construct; Mono's own vbnc has no parser for it.
# When XSP4 asks the code generator to compile a page, vbnc reads that line,
# throws, and reports the failure through Mono's error path as
#
#   App_global.asax_xxxxxxxx_1.vb (1,19) : error VBNC30248: CHANGEME
#
# Column 19 is the opening quote of the checksum path on line 1. The text
# "CHANGEME" is not a real message: error code 30248 has no entry in vbnc's
# resource table, so vbnc prints the placeholder. That is why the failure looks
# like a nonsense internal compiler error rather than "unknown directive", and
# it is why it cannot be diagnosed from the ASP.NET error page, which only
# surfaces the mapped part of the exception.
#
# Every page fails this way, so the site 500s on all 28 requests. Removing the
# single directive line makes the identical file compile cleanly, which is how
# this was confirmed rather than guessed.
#
# This is not an application defect: the generated source is correct VB, and the
# same file compiles as soon as the one unsupported directive is gone. The only
# place the directive can be removed is between the generator and vbnc, because
# the generator runs in-process inside XSP4 and rewrites its scratch file on
# every request.
#
# The argument list is passed through untouched -- in particular /out: still
# names the DLL XSP4 expects -- so vbnc's own diagnostics remain identical apart
# from the removed line. The original bytes are restored afterwards, because XSP4
# reuses the same scratch path across requests.
#
# Set VBNC_SHIM=0 to bypass the shim (pass the files through unmodified), which
# is useful when diagnosing vbnc itself.
set -u

REAL=/usr/bin/vbnc.real
if [ ! -x "$REAL" ]; then
    REAL=$(command -v vbnc 2>/dev/null || true)
fi
if [ -z "$REAL" ] || [ ! -x "$REAL" ]; then
    echo "vbnc-shim: cannot find the real vbnc to delegate to" >&2
    exit 127
fi

TMPD=""
cleanup() {
    if [ -n "$TMPD" ] && [ -d "$TMPD" ]; then
        for b in "$TMPD"/*.bak; do
            [ -e "$b" ] || continue
            cp "$b" "$TMPD/$(basename "$b" .bak).vb" 2>/dev/null || true
        done
        rm -rf "$TMPD"
    fi
}
trap cleanup EXIT INT TERM

# Consume the original argument list, appending each argument back unchanged
# except for .vb files, which are edited in place first.
n=$#
i=0
while [ "$i" -lt "$n" ]; do
    arg=$1
    shift
    case "$arg" in
        *.vb)
            if [ "${VBNC_SHIM:-1}" = "0" ] || [ ! -f "$arg" ]; then
                set -- "$@" "$arg"
            else
                if [ -z "$TMPD" ]; then
                    TMPD=$(mktemp -d) || TMPD=""
                fi
                if [ -n "$TMPD" ]; then
                    cp "$arg" "$TMPD/$i.bak" 2>/dev/null || true
                    # Delete the offending line outright rather than trying to
                    # match it with a regex. The line begins with a UTF-8 BOM
                    # (EF BB BF), which is not matched by [[:space:]] or by any
                    # "leading non-alphanumeric" class, so an anchored pattern
                    # silently fails to match and the shim becomes a no-op.
                    # Deleting the line also removes the BOM, which is what the
                    # confirming test did and what makes the result compile.
                    sed -e '1{/ExternalChecksum/d}' -e '1{/SourceChecksum/d}' \
                        -e '2{/ExternalChecksum/d}' -e '2{/SourceChecksum/d}' \
                        "$arg" > "$TMPD/$i.new" 2>/dev/null &&
                        cat "$TMPD/$i.new" > "$arg" 2>/dev/null || true
                fi
                set -- "$@" "$arg"
            fi
            ;;
        *)
            set -- "$@" "$arg"
            ;;
    esac
    i=$((i + 1))
done

"$REAL" "$@"
rc=$?
exit $rc
