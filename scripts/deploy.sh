#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -W)"
chorizite_home="${CHORIZITE_HOME:-C:/Games/Chorizite}"
output="$repo_root/src/ServerBrowser/bin/net8.0"
destination="$chorizite_home/plugins/ServerBrowser"

dotnet test "$repo_root/tests/ServerBrowser.Tests/ServerBrowser.Tests.csproj"
dotnet build "$repo_root/src/ServerBrowser/ServerBrowser.csproj" --no-restore
mkdir -p "$destination"
cp "$output/ServerBrowser.dll" "$output/ServerBrowser.pdb" "$output/ServerBrowser.deps.json" "$output/ServerBrowser.runtimeconfig.json" "$output/manifest.json" "$destination/"
rm -rf "$destination/assets"
cp -R "$output/assets" "$destination/"
printf 'Deployed Server Browser to %s\n' "$destination"
