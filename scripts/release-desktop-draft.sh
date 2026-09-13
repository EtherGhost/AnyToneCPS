#!/usr/bin/env bash
set -euo pipefail

# Uploads the desktop packages already built by build-desktop-packages.sh
# to a DRAFT GitHub release - nothing is published, no git tag is pushed,
# until a person opens the draft on github.com and clicks Publish. Requires
# the gh CLI, installed and authenticated (`gh auth login`) separately -
# this script only checks for it, never installs or authenticates it.
#
# Usage: scripts/release-desktop-draft.sh [notes-file]
# If notes-file is omitted, the draft is created with gh's own
# auto-generated commit-based notes as a starting point - edit them on
# github.com before publishing either way.

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
csproj_version="$(grep -oP '(?<=<Version>)[^<]+' "$repo_root/AnyToneCPS.Desktop/AnyToneCPS.Desktop.csproj")"
version="${VERSION:-$csproj_version}"
tag="v$version"
notes_file="${1:-}"

if ! command -v gh >/dev/null 2>&1; then
    echo "Missing gh CLI: sudo dnf install -y gh, then gh auth login." >&2
    exit 1
fi

if ! gh auth status >/dev/null 2>&1; then
    echo "gh is installed but not authenticated: run gh auth login first." >&2
    exit 1
fi

mapfile -t assets < <(find "$repo_root/artifacts/packages" -type f \( \
    -name "anytone-cps-$version-*.rpm" -o \
    -name "anytone-cps-$version.flatpak" -o \
    -name "AnyToneCPS-$version-*.AppImage" \
    \))

if [[ ${#assets[@]} -eq 0 ]]; then
    echo "No built packages found for version $version in artifacts/packages/." >&2
    echo "Run scripts/build-desktop-packages.sh first." >&2
    exit 1
fi

echo "Creating draft release $tag with:"
printf '  %s\n' "${assets[@]}"

notes_args=(--generate-notes)
if [[ -n "$notes_file" ]]; then
    notes_args=(--notes-file "$notes_file")
fi

gh release create "$tag" \
    --repo EtherGhost/AnyToneCPS \
    --title "$tag" \
    --draft \
    --prerelease \
    "${notes_args[@]}" \
    "${assets[@]}"

echo
echo "Draft created - review and edit it on github.com, then click Publish"
echo "when ready. Nothing is public and no git tag exists until you do."
