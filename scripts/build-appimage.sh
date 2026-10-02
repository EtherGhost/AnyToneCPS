#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
publish_dir="${PUBLISH_DIR:-$repo_root/artifacts/desktop-nativeaot}"
csproj_version="$(grep -oP '(?<=<Version>)[^<]+' "$repo_root/AnyToneCPS.Desktop/AnyToneCPS.Desktop.csproj")"
version="${VERSION:-$csproj_version}"
appdir="$repo_root/artifacts/AnyToneCPS.AppDir"
out_dir="$repo_root/artifacts/packages"
tools_dir="$repo_root/artifacts/tools"
appimagetool="$tools_dir/appimagetool-x86_64.AppImage"

if [[ ! -x "$publish_dir/AnyToneCPS.Desktop" ]]; then
    echo "Missing published desktop app: $publish_dir/AnyToneCPS.Desktop" >&2
    echo "Run scripts/publish-desktop-nativeaot.sh first." >&2
    exit 1
fi

mkdir -p "$tools_dir" "$out_dir"

if [[ ! -x "$appimagetool" ]]; then
    echo "Downloading appimagetool..."
    curl -fL -o "$appimagetool" \
        https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage
    chmod +x "$appimagetool"
fi

rm -rf "$appdir"
mkdir -p "$appdir/usr/bin" "$appdir/usr/share/applications" "$appdir/usr/share/icons/hicolor/256x256/apps"

install -m 0755 "$publish_dir/AnyToneCPS.Desktop" "$appdir/usr/bin/AnyToneCPS.Desktop"
install -m 0755 "$publish_dir/libHarfBuzzSharp.so" "$appdir/usr/bin/libHarfBuzzSharp.so"
install -m 0755 "$publish_dir/libSkiaSharp.so" "$appdir/usr/bin/libSkiaSharp.so"
install -m 0755 "$publish_dir/libSystem.IO.Ports.Native.so" "$appdir/usr/bin/libSystem.IO.Ports.Native.so"

# libicu and libfontconfig are NOT NuGet-shipped like the 3 above - they're
# plain system libraries, so unlike those 3 they don't land in $publish_dir
# on their own. Found missing live 2026-10-02: a container with neither
# installed crashed on startup (.NET's own ICU init, then a transitive
# SkiaSharp->fontconfig dependency once ICU was fixed) - the AppImage's
# whole point is running without relying on the host having these, so both
# get bundled from the build machine's own copies. Globbed rather than a
# single hardcoded version (e.g. libicuuc.so.77) so a Fedora version bump
# on the build host doesn't silently stop matching - picks up both the
# dlopen'd SONAME symlink and the real versioned file together.
for lib_glob in /usr/lib64/libicuuc.so.* /usr/lib64/libicudata.so.* /usr/lib64/libicui18n.so.* /usr/lib64/libfontconfig.so.*; do
    cp -P "$lib_glob" "$appdir/usr/bin/"
done

install -m 0644 "$repo_root/AnyToneCPS.Desktop/Linux/se.cloudsite.AnyToneCPS.desktop" \
    "$appdir/usr/share/applications/se.cloudsite.AnyToneCPS.desktop"
install -m 0644 "$repo_root/AnyToneCPS.Desktop/Linux/se.cloudsite.AnyToneCPS.desktop" \
    "$appdir/se.cloudsite.AnyToneCPS.desktop"

install -m 0644 "$repo_root/AnyToneCPS/Assets/Icon.png" \
    "$appdir/usr/share/icons/hicolor/256x256/apps/se.cloudsite.AnyToneCPS.png"
install -m 0644 "$repo_root/AnyToneCPS/Assets/Icon.png" "$appdir/se.cloudsite.AnyToneCPS.png"

cat > "$appdir/AppRun" <<'APPRUN'
#!/usr/bin/env bash
here="$(dirname "$(readlink -f "${0}")")"
# Covers both how the bundled libs get found: a direct dlopen by SONAME
# (.NET's own ICU probing) and a transitive .so-to-.so dependency (SkiaSharp
# pulling in fontconfig) - both honor LD_LIBRARY_PATH, neither is guaranteed
# to check the executable's own directory on its own.
export LD_LIBRARY_PATH="$here/usr/bin:${LD_LIBRARY_PATH:-}"
exec "$here/usr/bin/AnyToneCPS.Desktop" "$@"
APPRUN
chmod 0755 "$appdir/AppRun"

appimage_out="$out_dir/AnyToneCPS-$version-x86_64.AppImage"
ARCH=x86_64 "$appimagetool" "$appdir" "$appimage_out"

echo "AppImage: $appimage_out"
