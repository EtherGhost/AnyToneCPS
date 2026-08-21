#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "Usage: $0 /absolute/path/to/AnyToneCPS.Desktop" >&2
    exit 2
fi

exec_path="$1"
if [[ "${exec_path}" != /* ]]; then
    echo "Exec path must be absolute: ${exec_path}" >&2
    exit 2
fi

if [[ ! -x "${exec_path}" ]]; then
    echo "Exec path is not executable: ${exec_path}" >&2
    exit 2
fi

app_id="se.tobbe.AnyToneCPS"
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_dir="$(cd -- "${script_dir}/../.." && pwd)"
data_home="${XDG_DATA_HOME:-${HOME}/.local/share}"
desktop_dir="${data_home}/applications"
icon_dir="${data_home}/icons/hicolor/256x256/apps"

mkdir -p "${desktop_dir}" "${icon_dir}"
install -m 0644 "${repo_dir}/AnyToneCPS/Assets/Icon.png" "${icon_dir}/${app_id}.png"
sed "s|^Exec=.*|Exec=${exec_path}|" \
    "${script_dir}/${app_id}.desktop" > "${desktop_dir}/${app_id}.desktop"
chmod 0644 "${desktop_dir}/${app_id}.desktop"

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "${desktop_dir}" >/dev/null 2>&1 || true
fi

if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q "${data_home}/icons/hicolor" >/dev/null 2>&1 || true
fi

echo "Installed ${desktop_dir}/${app_id}.desktop"
