#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
publish_dir="${PUBLISH_DIR:-$repo_root/artifacts/desktop-nativeaot}"
manifest="$repo_root/flatpak/se.tobbe.AnyToneCPS.yml"
build_dir="$repo_root/artifacts/flatpak-build"
repo_dir="$repo_root/artifacts/flatpak-repo"
bundle_out="$repo_root/artifacts/packages"
csproj_version="$(grep -oP '(?<=<Version>)[^<]+' "$repo_root/AnyToneCPS.Desktop/AnyToneCPS.Desktop.csproj")"

if [[ ! -x "$publish_dir/AnyToneCPS.Desktop" ]]; then
    echo "Missing published desktop app: $publish_dir/AnyToneCPS.Desktop" >&2
    echo "Run scripts/publish-desktop-nativeaot.sh first." >&2
    exit 1
fi

if ! command -v flatpak-builder >/dev/null 2>&1; then
    echo "flatpak-builder is not installed (sudo dnf install flatpak-builder)." >&2
    exit 1
fi

if ! flatpak info org.freedesktop.Sdk//25.08 >/dev/null 2>&1; then
    echo "org.freedesktop.Sdk//25.08 is not installed (flatpak install flathub org.freedesktop.Sdk//25.08)." >&2
    exit 1
fi

mkdir -p "$bundle_out"

flatpak-builder --force-clean --user --repo="$repo_dir" "$build_dir" "$manifest"

bundle_path="$bundle_out/anytone-cps-$csproj_version.flatpak"
flatpak build-bundle "$repo_dir" "$bundle_path" se.tobbe.AnyToneCPS

echo "Flatpak bundle: $bundle_path"
