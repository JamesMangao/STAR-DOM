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

# ── vbnc discovery ───────────────────────────────────────────────────────────
# XSP4 compiles .aspx on first request and VBCodeGenerator.FromFileBatch shells
# out to vbnc. Mono's package layout has moved the binary between releases, so
# make sure a vbnc is reachable on PATH and point MONO_VBNC at it.
#
# This block only guarantees vbnc is *present*. It deliberately does NOT claim
# to validate compilation -- `vbnc --help` proves nothing about whether this
# application actually builds. The real check is the compile gate further down.
RUN set -e; \
    if ! command -v vbnc >/dev/null 2>&1; then \
        echo ">> vbnc not on PATH, searching for vbnc.exe..."; \
        VBNC_EXE=$(find /usr/lib/mono -name 'vbnc.exe' 2>/dev/null | head -1); \
        if [ -z "$VBNC_EXE" ]; then \
            echo "FATAL: vbnc.exe not found anywhere under /usr/lib/mono"; exit 1; \
        fi; \
        echo ">> Found $VBNC_EXE - creating /usr/bin/vbnc wrapper"; \
        printf '#!/bin/sh\nexec mono "%s" "$@"\n' "$VBNC_EXE" > /usr/bin/vbnc; \
        chmod +x /usr/bin/vbnc; \
    fi; \
    echo ">> vbnc at: $(command -v vbnc)"; \
    echo ">> presence confirmed. Do NOT probe it here: vbnc has no --help, so a" ; \
    echo ">> version probe exits non-zero on a perfectly good install. Whether it" ; \
    echo ">> can actually compile this app is proven by the gate further down."

WORKDIR /app

# Copy project files
COPY . /app

# The website project root is /app/STAR-DOM-Web (the repo-root/STAR-DOM-Web folder
# holds the .vbproj, Site.master, App/, css/, packages/ and bin/).
WORKDIR /app/STAR-DOM-Web

# Build the VB.NET Web application. Do NOT swallow failures: if this breaks, the
# deploy should fail here with a real compiler error instead of booting XSP4
# against a half-built output and 500ing on the first request.
#
# TargetFrameworkVersion is overridden to 4.8 on the command line on purpose.
# The project file says 4.8.1 because that is the targeting pack installed on the
# development machine, but Mono 6.12 ships a 4.8 reference-assembly set and has no
# 4.8.1 one -- so an unoverridden build inside the image fails with MSB3644.
# 4.8 is also what web.config declares (<compilation targetFramework="4.8">), so
# the container compiles and runs against a consistent framework.
RUN msbuild /nologo /v:minimal /p:Configuration=Release /p:Platform="AnyCPU" \
        /p:TargetFrameworkVersion=v4.8 STAR-DOM-Web.vbproj

# ── Compile gate ─────────────────────────────────────────────────────────────
# XSP4 compiles .aspx at request time via vbnc. This step requests EVERY page so
# that (a) the assemblies get built once here instead of on the first live hit,
# and (b) a page that cannot compile fails the deploy here, with the real error
# in the log, instead of shipping an image that 500s on every request.
#
# The previous version of this step warmed a hardcoded 11 of the 29 pages and
# ended in `curl ... || true`, so it could not fail even when every page 500'd.
# tools/xsp-warmup.sh discovers pages from disk and exits non-zero on failure.
ENV TMPDIR=/app/.xsp-cache
RUN mkdir -p "$TMPDIR" && \
    sh /app/tools/xsp-warmup.sh /app/STAR-DOM-Web 19876

WORKDIR /app/STAR-DOM-Web

# Render dynamic port binding (defaults to 10000)
ENV PORT=10000
EXPOSE 10000

# Start Mono XSP4 web server
CMD ["sh", "-c", "xsp4 --nonstop --port=${PORT:-10000} --address=0.0.0.0 --root=/app/STAR-DOM-Web"]
