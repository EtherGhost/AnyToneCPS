#!/usr/bin/env bash
set -euo pipefail

# Chains the existing individual desktop build scripts into one command -
# no new build logic here, just the order they need to run in. See each
# script's own comments for what it actually does and why.

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
publish_dir="${PUBLISH_DIR:-$repo_root/artifacts/desktop-nativeaot}"
csproj_version="$(grep -oP '(?<=<Version>)[^<]+' "$repo_root/AnyToneCPS.Desktop/AnyToneCPS.Desktop.csproj")"
version="${VERSION:-$csproj_version}"

echo "Building desktop packages for version $version"

# Stale incremental NativeAOT state has caused real crashes before (see
# scripts/publish-android-nativeaot.sh's own history) - cheap enough to
# always clean here too rather than risk it.
rm -rf "$publish_dir" "$repo_root/AnyToneCPS.Desktop/obj" "$repo_root/AnyToneCPS.Desktop/bin" \
    "$repo_root/AnyToneCPS/obj" "$repo_root/AnyToneCPS/bin"

"$repo_root/scripts/publish-desktop-nativeaot.sh" "$publish_dir"

VERSION="$version" "$repo_root/scripts/build-fedora-rpm.sh"
VERSION="$version" "$repo_root/scripts/build-flatpak.sh"
VERSION="$version" "$repo_root/scripts/build-appimage.sh"

echo
echo "Desktop packages for $version:"
find "$repo_root/artifacts/packages" -type f \( \
    -name "anytone-cps-$version-*.rpm" -o \
    -name "anytone-cps-$version.flatpak" -o \
    -name "AnyToneCPS-$version-*.AppImage" \
    \) -print
