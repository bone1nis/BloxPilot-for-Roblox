#!/bin/zsh
# Прогоняет тесты и собирает релизы для Windows и macOS в папку dist. Запускать на Mac.
set -euo pipefail
cd "$(dirname "$0")/.."

project=src/RobloxAntiAfk.App/RobloxAntiAfk.App.csproj
app_name="Roblox AntiAFK"

dotnet test --project tests/RobloxAntiAfk.Core.Tests

rm -rf dist artifacts

dotnet publish "$project" -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none \
  -o artifacts/win-x64
mkdir -p dist
cp artifacts/win-x64/RobloxAntiAFK.exe "dist/$app_name.exe"

publish_mac() {
  local rid=$1 folder=$2
  local bundle="dist/$folder/$app_name.app"
  dotnet publish "$project" -c Release -r "$rid" --self-contained -p:DebugType=none -o "$bundle/Contents/MacOS"
  mkdir -p "$bundle/Contents/Resources"
  cp packaging/macos/Info.plist "$bundle/Contents/Info.plist"
  cp packaging/macos/icon.icns "$bundle/Contents/Resources/icon.icns"
  codesign --force --deep --sign - "$bundle"
  ditto -c -k --keepParent "$bundle" "dist/Roblox-AntiAFK-$folder.zip"
}

publish_mac osx-arm64 mac-apple-silicon
publish_mac osx-x64 mac-intel

rm -rf artifacts
du -sh dist/*
