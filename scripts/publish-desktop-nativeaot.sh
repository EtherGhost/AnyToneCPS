#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
publish_dir="${1:-$repo_root/artifacts/desktop-nativeaot}"
dotnet_root="${DOTNET_NATIVEAOT_ROOT:-/home/tobbe/Programmering/.dotnet-official}"
dotnet_cli_home="${DOTNET_CLI_HOME:-/home/tobbe/Programmering/.dotnet-cli-home}"
dotnet_bin="$dotnet_root/dotnet"

if [[ ! -x "$dotnet_bin" ]]; then
    echo "Missing dotnet: $dotnet_bin" >&2
    echo "Set DOTNET_NATIVEAOT_ROOT to an official .NET SDK installation." >&2
    exit 1
fi

export DOTNET_ROOT="$dotnet_root"
export DOTNET_CLI_HOME="$dotnet_cli_home"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export MSBUILDDISABLENODEREUSE=1

illink_targets="$dotnet_cli_home/.nuget/packages/microsoft.net.illink.tasks/10.0.8/build/Microsoft.NET.ILLink.targets"

if [[ -f "$illink_targets" ]] && grep -q 'TaskFactory="TaskHostFactory"' "$illink_targets"; then
    # Workaround for MSB4216 from ILLink's .NET TaskHost on this Linux toolchain.
    sed -i 's/ TaskFactory="TaskHostFactory"//g' "$illink_targets"
fi

cd "$repo_root"

"$dotnet_bin" publish AnyToneCPS.Desktop/AnyToneCPS.Desktop.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:EnableNativeAot=true \
    -p:PublishSingleFile=true \
    -p:PublishDir="$publish_dir/" \
    -m:1

echo "Published desktop NativeAOT app to: $publish_dir"
