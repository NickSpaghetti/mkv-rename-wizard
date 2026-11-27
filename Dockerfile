# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0
ARG ZIG_VERSION=0.15.2
ARG OPENTUI_VERSION=0.9.7

# ---------------------------------------------------------------------------
# development: full polyglot dev container (.NET 10 + Rust + Zig + OpenTUI + dotnet-trace/gcdump)
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS development

ARG ZIG_VERSION
ARG OPENTUI_VERSION
ARG TARGETARCH

RUN apt-get update && apt-get install -y --no-install-recommends \
        build-essential \
        ca-certificates \
        clang \
        curl \
        git \
        libssl-dev \
        lld \
        pkg-config \
        unzip \
        xz-utils \
        zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

# Install Rust (stable) for OpenTUI frontend
ENV RUSTUP_HOME=/usr/local/rustup \
    CARGO_HOME=/usr/local/cargo \
    PATH="/usr/local/cargo/bin:/root/.dotnet/tools:${PATH}"

RUN curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs \
    | sh -s -- -y --default-toolchain stable --profile minimal \
    && rustup component add rustfmt clippy

# Install Zig (required by OpenTUI native core)
RUN ZIG_ARCH="$(case "${TARGETARCH:-amd64}" in amd64) echo x86_64;; arm64) echo aarch64;; *) echo x86_64;; esac)" \
    && curl -fsSL "https://ziglang.org/download/${ZIG_VERSION}/zig-${ZIG_ARCH}-linux-${ZIG_VERSION}.tar.xz" -o /tmp/zig.tar.xz \
    && mkdir -p /usr/local/zig \
    && tar -xf /tmp/zig.tar.xz -C /usr/local/zig --strip-components=1 \
    && ln -s /usr/local/zig/zig /usr/local/bin/zig \
    && rm -f /tmp/zig.tar.xz

# Install OpenTUI native archive (static libopentui.a + opentui.h)
ENV OPENTUI_LIB_DIR=/usr/local/opentui-native
RUN OPENTUI_ARCH="$(case "${TARGETARCH:-amd64}" in amd64) echo linux-x64;; arm64) echo linux-arm64;; *) echo linux-x64;; esac)" \
    && mkdir -p "${OPENTUI_LIB_DIR}" \
    && curl -fsSL "https://github.com/anomalyco/opentui/releases/download/v${OPENTUI_VERSION}/opentui-native-v${OPENTUI_VERSION}-${OPENTUI_ARCH}.zip" -o /tmp/opentui-native.zip \
    && unzip -q /tmp/opentui-native.zip -d "${OPENTUI_LIB_DIR}" \
    && rm -f /tmp/opentui-native.zip

# Install .NET performance & memory profiling tools
RUN dotnet tool install --global dotnet-trace \
    && dotnet tool install --global dotnet-gcdump \
    && dotnet tool install --global dotnet-counters

WORKDIR /workspace

CMD ["/bin/bash"]

# ---------------------------------------------------------------------------
# build-cli: publishes NativeAOT mkv-rename-cli binary
# ---------------------------------------------------------------------------
FROM development AS build-cli

COPY MkvRenameWizard/ /workspace/MkvRenameWizard/

RUN dotnet publish /workspace/MkvRenameWizard/MkvRenameWizard.Cli/MkvRenameWizard.Cli.csproj \
    -c Release \
    -r linux-x64 \
    -o /out

# ---------------------------------------------------------------------------
# production: minimal runtime-deps image containing NativeAOT mkv-rename-cli
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime-deps:${DOTNET_VERSION} AS production

WORKDIR /app
COPY --from=build-cli /out/mkv-rename-cli /usr/local/bin/mkv-rename-cli

ENTRYPOINT ["/usr/local/bin/mkv-rename-cli"]
CMD ["--help"]
