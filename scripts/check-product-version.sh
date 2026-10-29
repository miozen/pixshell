#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
win_version="$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' win/PixShell.csproj)"
mac_version="$(sed -n 's/^VERSION="${VERSION:-\([^}]*\)}"$/\1/p' mac/scripts/package-mac.sh)"
[[ "$win_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ && "$mac_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo 'Invalid project version' >&2; exit 1; }
version="$win_version"
check() { grep -Fq -- "$2" "$1" || { echo "Version mismatch: $1" >&2; exit 1; }; }
check win/PixShell.csproj "<AssemblyVersion>$version.0</AssemblyVersion>"
check win/PixShell.csproj "<FileVersion>$version.0</FileVersion>"
check win/MainWindow.xaml.cs "AppVersion = \"$version\""
check win/App.xaml.cs "$version [headless]"
check win/Bridge/AgentCLI.cs "'version': '$version'"
check win/installer/PixShell.iss "#define MyAppVersion \"$version\""
check win/installer/package-win.ps1 "[string]\$Version = '$version'"
check .github/workflows/release-windows.yml "default: '$version'"
check win/MainWindow.xaml.cs 'const string repo = "miozen/pixshell"'
version="$mac_version"
check mac/Sources/PixShell/main.swift "Log.banner(\"$version\")"
check mac/Sources/PixShell/App/AppDelegate+Menu.swift "AppUpdate.check(current: \"$version\")"
check mac/Sources/PixShell/App/AppDelegate+Layout.swift "v$version"
check mac/Sources/PixShell/Bridge/AgentMCP.swift "'version': '$version'"
check mac/scripts/package-mac.sh "VERSION:-$version"
check mac/scripts/make-dmg.sh "VERSION:-$version"
for workflow in .github/workflows/release-macos-{arm64,x64}.yml; do
  check "$workflow" "default: '$version'"
done
check mac/Sources/PixShell/Store/AppUpdate.swift 'repoSlug = "miozen/pixshell"'
echo "Windows $win_version and macOS $mac_version version checks passed."
