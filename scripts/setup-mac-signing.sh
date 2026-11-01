#!/bin/zsh
# Один раз создаёт постоянный ключ подписи для сборок macOS. Ключ хранится вне репозитория в отдельной связке ключей:
# пока он один и тот же, разрешения macOS сохраняются при обновлении приложения.
set -euo pipefail

signing_dir="$HOME/Library/Application Support/RobloxAntiAFK-Signing"
keychain="$signing_dir/signing.keychain-db"
password_file="$signing_dir/keychain-password"
identity_file="$signing_dir/identity"

if [[ -e "$keychain" ]]; then
  echo "Ключ подписи уже есть: $signing_dir"
  exit 0
fi

umask 077
mkdir -p "$signing_dir"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

cat > "$work/cert.conf" <<'EOF'
[req]
distinguished_name = dn
x509_extensions = ext
prompt = no
[dn]
CN = Roblox AntiAFK Self-Signed
[ext]
basicConstraints = critical, CA:false
keyUsage = critical, digitalSignature
extendedKeyUsage = critical, codeSigning
EOF

openssl req -x509 -newkey rsa:2048 -sha256 -nodes -days 7300 -config "$work/cert.conf" \
  -keyout "$work/key.pem" -out "$work/cert.pem" 2>/dev/null
import_password=$(openssl rand -hex 16)
openssl pkcs12 -export -inkey "$work/key.pem" -in "$work/cert.pem" -out "$work/identity.p12" -passout "pass:$import_password"

openssl rand -hex 32 > "$password_file"
password=$(<"$password_file")

# Создание связки добавляет её в список пользователя. Она нужна там только на время подписи, поэтому список возвращается как был.
search_list=("${(@f)$(security list-keychains -d user | sed -e 's/^ *"//' -e 's/"$//')}")
security create-keychain -p "$password" "$keychain"
security list-keychains -d user -s "${search_list[@]}"
security set-keychain-settings "$keychain"
security unlock-keychain -p "$password" "$keychain"
security import "$work/identity.p12" -k "$keychain" -P "$import_password" -T /usr/bin/codesign >/dev/null
security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "$password" "$keychain" >/dev/null

openssl x509 -in "$work/cert.pem" -noout -fingerprint -sha1 | sed -e 's/.*=//' -e 's/://g' > "$identity_file"
echo "Ключ подписи создан: $signing_dir"
