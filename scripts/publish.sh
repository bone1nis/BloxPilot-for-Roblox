#!/bin/zsh
# Прогоняет тесты и собирает релизы для Windows и macOS в папку dist. Запускать на Mac.
set -euo pipefail
cd "$(dirname "$0")/.."

project=src/RobloxAntiAfk.App/RobloxAntiAfk.App.csproj
app_name="Roblox AntiAFK"
signing_dir="$HOME/Library/Application Support/RobloxAntiAFK-Signing"
signing_keychain="$signing_dir/signing.keychain-db"

# Без постоянного ключа каждая сборка выглядела бы для macOS новым приложением и теряла разрешения.
if [[ ! -e "$signing_keychain" ]]; then
  echo "Нет ключа подписи. Создай его один раз: ./scripts/setup-mac-signing.sh" >&2
  exit 1
fi
signing_identity=$(<"$signing_dir/identity")

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
  codesign --force --deep --timestamp=none --keychain "$signing_keychain" --sign "$signing_identity" "$bundle"
  ditto -c -k --keepParent "$bundle" "dist/Roblox-AntiAFK-$folder.zip"
}

# codesign ищет ключ только в связках из списка пользователя, поэтому она попадает туда на время подписи.
search_list=("${(@f)$(security list-keychains -d user | sed -e 's/^ *"//' -e 's/"$//')}")
trap 'security list-keychains -d user -s "${search_list[@]}"' EXIT
security list-keychains -d user -s "${search_list[@]}" "$signing_keychain"
security unlock-keychain -p "$(<"$signing_dir/keychain-password")" "$signing_keychain"

publish_mac osx-arm64 mac-apple-silicon
publish_mac osx-x64 mac-intel

rm -rf artifacts
du -sh dist/*
