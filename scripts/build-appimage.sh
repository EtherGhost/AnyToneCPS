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

install -m 0644 "$repo_root/AnyToneCPS.Desktop/Linux/se.tobbe.AnyToneCPS.desktop" \
    "$appdir/usr/share/applications/se.tobbe.AnyToneCPS.desktop"
install -m 0644 "$repo_root/AnyToneCPS.Desktop/Linux/se.tobbe.AnyToneCPS.desktop" \
    "$appdir/se.tobbe.AnyToneCPS.desktop"

install -m 0644 "$repo_root/AnyToneCPS/Assets/Icon.png" \
    "$appdir/usr/share/icons/hicolor/256x256/apps/se.tobbe.AnyToneCPS.png"
install -m 0644 "$repo_root/AnyToneCPS/Assets/Icon.png" "$appdir/se.tobbe.AnyToneCPS.png"

cat > "$appdir/AppRun" <<'APPRUN'
#!/usr/bin/env bash
here="$(dirname "$(readlink -f "${0}")")"
exec "$here/usr/bin/AnyToneCPS.Desktop" "$@"
APPRUN
chmod 0755 "$appdir/AppRun"

appimage_out="$out_dir/AnyToneCPS-$version-x86_64.AppImage"
ARCH=x86_64 "$appimagetool" "$appdir" "$appimage_out"

echo "AppImage: $appimage_out"
