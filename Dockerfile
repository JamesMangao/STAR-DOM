# Ubuntu 20.04 LTS (focal) to MATCH the Mono repository suite below.
# The Mono project repo only publishes stable-focal/stable-bionic packages; installing
# those on jammy (22.04) leaves the install partially completed and vbnc (the VB.NET
# code generator XSP4 needs to compile .aspx at request time) unregistered, which
# surfaces as: "System.SystemException: Error running vbnc: Cannot find the specified file"
FROM ubuntu:20.04

ENV DEBIAN_FRONTEND=noninteractive
ENV TZ=UTC

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        gnupg \
        dirmngr \
        curl && \
    gpg --homedir /tmp --no-default-keyring --keyring /usr/share/keyrings/mono-official-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF && \
    echo "deb [signed-by=/usr/share/keyrings/mono-official-archive-keyring.gpg] https://download.mono-project.com/repo/ubuntu stable-focal main" | tee /etc/apt/sources.list.d/mono-official-stable.list && \
    apt-get update && \
    apt-get install -y --no-install-recommends \
        mono-devel \
        mono-vbnc \
        mono-xsp4 \
        nuget \
        msbuild && \
    rm -rf /var/lib/apt/lists/*

# ── vbnc discovery hardening ───────────────────────────────────────────────────
# Mono's VBCodeProvider looks for vbnc in several locations depending on build:
#   1. $PATH  2. /usr/bin/vbnc  3. GAC-relative paths
# Some Mono package versions install the binary to /usr/lib/mono/*/vbnc.exe but
# do NOT create the /usr/bin/vbnc wrapper, causing the "Cannot find the specified
# file" error at runtime even though the package IS installed.
# Fix: locate the real vbnc / vbnc.exe and ensure /usr/bin/vbnc exists.
ENV MONO_VBNC=/usr/bin/vbnc
RUN set -e; \
    if ! command -v vbnc >/dev/null 2>&1; then \
        echo ">> vbnc not on PATH, searching for vbnc.exe..."; \
        VBNC_EXE=$(find /usr/lib/mono -name 'vbnc.exe' 2>/dev/null | head -1); \
        if [ -z "$VBNC_EXE" ]; then \
            echo "FATAL: vbnc.exe not found anywhere under /usr/lib/mono"; exit 1; \
        fi; \
        echo ">> Found $VBNC_EXE — creating /usr/bin/vbnc wrapper"; \
        printf '#!/bin/sh\nexec mono "%s" "$@"\n' "$VBNC_EXE" > /usr/bin/vbnc; \
        chmod +x /usr/bin/vbnc; \
    fi && \
    echo ">> vbnc verification:" && vbnc --help >/dev/null 2>&1 && echo "OK" || \
    (echo "FATAL: vbnc exists but cannot run" && exit 1)

# Also wire up the CodeDom provider path that XSP4/Mono looks for at runtime.
# VBCodeGenerator.FromFileBatch shells out to the path stored in the Mono config;
# ensure it resolves even if the config points to an alternate location.
RUN VBNC_REAL=$(command -v vbnc) && \
    for d in /usr/lib/mono/4.5 /usr/lib/mono/4.0; do \
        [ -d "$d" ] && [ ! -e "$d/vbnc.exe" ] && \
        VBNC_EXE=$(find /usr/lib/mono -name 'vbnc.exe' 2>/dev/null | head -1) && \
        [ -n "$VBNC_EXE" ] && ln -sf "$VBNC_EXE" "$d/vbnc.exe" || true; \
    done && \
    echo ">> vbnc symlinks OK"

WORKDIR /app

# Copy project files
COPY . /app

WORKDIR /app/STAR-DOM-Web/STAR-DOM-Web

# Build the VB.NET Web application. Do NOT swallow failures: if this breaks, the
# deploy should fail here with a real compiler error instead of booting XSP4
# against a half-built output and 500ing on the first request.
RUN msbuild /p:Configuration=Release /p:Platform="AnyCPU" STAR-DOM-Web.vbproj

# ── Precompile ASPX pages ─────────────────────────────────────────────────────
# XSP4 normally compiles .aspx/.master pages on first request using vbnc.
# Precompiling them here means the runtime never needs to invoke vbnc, which
# eliminates the "Cannot find the specified file" error class entirely.
# The -fixednames flag keeps the assembly names predictable.
RUN mono /usr/lib/mono/4.5/xsp4.exe --nonstop --port=19876 --address=127.0.0.1 \
        --root=/app/STAR-DOM-Web/STAR-DOM-Web & \
    XSP_PID=$!; \
    sleep 3; \
    echo ">> Warming up pages to trigger precompilation..."; \
    for page in \
        /Login.aspx \
        /Register.aspx \
        /App/Marketplace.aspx \
        /App/Catalog.aspx \
        /App/Cart.aspx \
        /App/Checkout.aspx \
        /App/Orders.aspx \
        /App/CommissionHub.aspx \
        /App/Notifications.aspx \
        /App/Profile.aspx \
        /App/PopupLocations.aspx \
    ; do \
        echo "   warming $page"; \
        curl -s -o /dev/null "http://127.0.0.1:19876$page" || true; \
    done; \
    kill $XSP_PID 2>/dev/null || true; \
    wait $XSP_PID 2>/dev/null || true; \
    echo ">> Precompilation warm-up done"

WORKDIR /app/STAR-DOM-Web/STAR-DOM-Web

# Render dynamic port binding (defaults to 10000)
ENV PORT=10000
EXPOSE 10000

# Start Mono XSP4 web server
CMD ["sh", "-c", "xsp4 --nonstop --port=${PORT:-10000} --address=0.0.0.0 --root=/app/STAR-DOM-Web/STAR-DOM-Web"]
