#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

dotnet_root="${DOTNET_NATIVEAOT_ROOT:-/home/tobbe/Programmering/.dotnet-official}"
dotnet_cli_home="${DOTNET_CLI_HOME:-/home/tobbe/Programmering/.dotnet-cli-home}"
ndk_root="${ANDROID_NDK_ROOT:-/home/tobbe/Android/Sdk/ndk/27.2.12479018}"

dotnet_bin="$dotnet_root/dotnet"
ndk_bin="$ndk_root/toolchains/llvm/prebuilt/linux-x86_64/bin"

if [[ ! -x "$dotnet_bin" ]]; then
    echo "Missing dotnet: $dotnet_bin" >&2
    echo "Set DOTNET_NATIVEAOT_ROOT to an official .NET SDK installation." >&2
    exit 1
fi

if [[ ! -d "$ndk_bin" ]]; then
    echo "Missing Android NDK toolchain: $ndk_bin" >&2
    echo "Install NDK 27.2.12479018 or set ANDROID_NDK_ROOT." >&2
    exit 1
fi

export DOTNET_ROOT="$dotnet_root"
export DOTNET_CLI_HOME="$dotnet_cli_home"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export MSBUILDDISABLENODEREUSE=1
export PATH="$ndk_bin:$dotnet_root:$PATH"

illink_targets="$dotnet_cli_home/.nuget/packages/microsoft.net.illink.tasks/10.0.8/build/Microsoft.NET.ILLink.targets"

if [[ -f "$illink_targets" ]] && grep -q 'TaskFactory="TaskHostFactory"' "$illink_targets"; then
    # Workaround for MSB4216 from ILLink's .NET TaskHost on this Linux toolchain.
    sed -i 's/ TaskFactory="TaskHostFactory"//g' "$illink_targets"
fi

cd "$repo_root"

"$dotnet_bin" restore AnyToneCPS.Android/AnyToneCPS.Android.csproj \
    -r android-arm64 \
    -p:PublishAot=true

"$dotnet_bin" publish AnyToneCPS.Android/AnyToneCPS.Android.csproj \
    -c Release \
    -p:PublishAot=true \
    -p:AndroidPackageFormat=apk \
    -p:AndroidNdkDirectory="$ndk_root/" \
    --no-restore \
    -m:1 \
    -nr:false
