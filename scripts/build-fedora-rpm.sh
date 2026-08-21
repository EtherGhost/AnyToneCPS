#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
publish_dir="${PUBLISH_DIR:-$repo_root/artifacts/desktop-nativeaot}"
# Default follows AnyToneCPS.Desktop.csproj's <Version> so a normal release
# only needs that one value bumped, not this script too. Override with
# VERSION= for a one-off build.
csproj_version="$(grep -oP '(?<=<Version>)[^<]+' "$repo_root/AnyToneCPS.Desktop/AnyToneCPS.Desktop.csproj")"
version="${VERSION:-$csproj_version}"
release="${RELEASE:-1}"
rpm_root="$repo_root/artifacts/rpm"
spec_file="$rpm_root/SPECS/anytone-cps.spec"
rpm_out="$repo_root/artifacts/packages"

if [[ ! -x "$publish_dir/AnyToneCPS.Desktop" ]]; then
    echo "Missing published desktop app: $publish_dir/AnyToneCPS.Desktop" >&2
    echo "Run scripts/publish-desktop-nativeaot.sh first." >&2
    exit 1
fi

mkdir -p "$rpm_root/BUILD" "$rpm_root/BUILDROOT" "$rpm_root/RPMS" "$rpm_root/SOURCES" "$rpm_root/SPECS" "$rpm_root/SRPMS" "$rpm_root/TMP" "$rpm_out"

cat > "$spec_file" <<SPEC
Name: anytone-cps
Version: $version
Release: $release%{?dist}
Summary: AnyTone CPS codeplug editor
License: MIT
URL: https://github.com/EtherGhost/AnyToneCPS
BuildArch: x86_64

%global _publish_dir $publish_dir
%global _repo_dir $repo_root

%description
AnyTone CPS is an Avalonia-based codeplug editor and CSV exporter for AnyTone radios.

%prep

%build

%install
rm -rf "%{buildroot}"
install -d "%{buildroot}/opt/anytone-cps"
install -m 0755 "%{_publish_dir}/AnyToneCPS.Desktop" "%{buildroot}/opt/anytone-cps/AnyToneCPS.Desktop"
install -m 0755 "%{_publish_dir}/libHarfBuzzSharp.so" "%{buildroot}/opt/anytone-cps/libHarfBuzzSharp.so"
install -m 0755 "%{_publish_dir}/libSkiaSharp.so" "%{buildroot}/opt/anytone-cps/libSkiaSharp.so"
install -m 0755 "%{_publish_dir}/libSystem.IO.Ports.Native.so" "%{buildroot}/opt/anytone-cps/libSystem.IO.Ports.Native.so"

install -d "%{buildroot}%{_bindir}"
cat > "%{buildroot}%{_bindir}/anytone-cps" <<'WRAPPER'
#!/usr/bin/env bash
cd /opt/anytone-cps
exec ./AnyToneCPS.Desktop "$@"
WRAPPER
chmod 0755 "%{buildroot}%{_bindir}/anytone-cps"

install -d "%{buildroot}%{_datadir}/applications"
sed 's|^Exec=.*|Exec=anytone-cps|' "%{_repo_dir}/AnyToneCPS.Desktop/Linux/se.tobbe.AnyToneCPS.desktop" > "%{buildroot}%{_datadir}/applications/se.tobbe.AnyToneCPS.desktop"
chmod 0644 "%{buildroot}%{_datadir}/applications/se.tobbe.AnyToneCPS.desktop"

install -d "%{buildroot}%{_datadir}/icons/hicolor/256x256/apps"
install -m 0644 "%{_repo_dir}/AnyToneCPS/Assets/Icon.png" "%{buildroot}%{_datadir}/icons/hicolor/256x256/apps/se.tobbe.AnyToneCPS.png"

%post
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database %{_datadir}/applications >/dev/null 2>&1 || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q %{_datadir}/icons/hicolor >/dev/null 2>&1 || true
fi

%postun
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database %{_datadir}/applications >/dev/null 2>&1 || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q %{_datadir}/icons/hicolor >/dev/null 2>&1 || true
fi

%files
%dir /opt/anytone-cps
/opt/anytone-cps/AnyToneCPS.Desktop
/opt/anytone-cps/libHarfBuzzSharp.so
/opt/anytone-cps/libSkiaSharp.so
/opt/anytone-cps/libSystem.IO.Ports.Native.so
%{_bindir}/anytone-cps
%{_datadir}/applications/se.tobbe.AnyToneCPS.desktop
%{_datadir}/icons/hicolor/256x256/apps/se.tobbe.AnyToneCPS.png

%changelog
* Thu Jun 04 2026 AnyToneCPS <local@localhost> - $version-$release
- Local NativeAOT Fedora package
SPEC

rpmbuild \
    --define "_topdir $rpm_root" \
    --define "_tmppath $rpm_root/TMP" \
    -bb "$spec_file"

find "$rpm_root/RPMS" -type f -name '*.rpm' -exec cp -f {} "$rpm_out/" \;

echo "RPM package(s):"
find "$rpm_out" -type f -name '*.rpm' -print
