#!/usr/bin/env bash
# Publishes self-contained single-file binaries for all supported RIDs.
# Usage:  scripts/publish.sh [version]
# Output: artifacts/<rid>/shelly[.exe]  and  artifacts/shelly-<version>-<rid>.{tar.gz,zip}

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

version="${1:-0.1.0}"
project="src/JDMallen.Shelly/JDMallen.Shelly.csproj"
out_root="dist"

rids=(
	linux-x64
	linux-arm64
	win-x64
	win-arm64
	osx-x64
	osx-arm64
)

rm -rf "$out_root"
mkdir -p "$out_root"

for rid in "${rids[@]}"; do
	out_dir="$out_root/$rid"
	echo ">>> Publishing $rid"
	dotnet publish "$project" \
		--configuration Release \
		--runtime "$rid" \
		--output "$out_dir" \
		-p:Version="$version" \
		--nologo \
		--verbosity minimal

	# Stage just the executable for the archive.
	stage="$out_root/stage-$rid"
	mkdir -p "$stage"
	if [[ "$rid" == win-* ]]; then
		cp "$out_dir/shelly.exe" "$stage/"
		archive="$out_root/shelly-$version-$rid.zip"
		(cd "$stage" && zip -q "$repo_root/$archive" shelly.exe)
	else
		cp "$out_dir/shelly" "$stage/"
		archive="$out_root/shelly-$version-$rid.tar.gz"
		tar -C "$stage" -czf "$archive" shelly
	fi
	rm -rf "$stage"
	echo "    -> $archive ($(du -h "$archive" | cut -f1))"
done

echo
echo "Done. Artifacts in $out_root/"
