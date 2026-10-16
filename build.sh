#!/bin/sh
set -eu

DOTNET_VERSION="${DOTNET_VERSION:-8.0.125}"
DOTNET_INSTALL_DIR="${DOTNET_INSTALL_DIR:-./dotnet}"

curl -fsSL https://dot.net/v1/dotnet-install.sh > dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh -Version "$DOTNET_VERSION" -InstallDir "$DOTNET_INSTALL_DIR"
"$DOTNET_INSTALL_DIR/dotnet" --version

cd src/Habitica.WebApp
npm ci
npm run sync:vendor

# Restore checked-in defaults after success or failure; publish receives the full merged settings.
FRONTEND_SETTINGS_PATH="$PWD/wwwroot/appsettings.json"
FRONTEND_SETTINGS_BACKUP="$(mktemp)"
cp "$FRONTEND_SETTINGS_PATH" "$FRONTEND_SETTINGS_BACKUP"
trap 'cp "$FRONTEND_SETTINGS_BACKUP" "$FRONTEND_SETTINGS_PATH"; rm -f "$FRONTEND_SETTINGS_BACKUP"' EXIT
node ../../deploy/frontend-settings.mjs "$FRONTEND_SETTINGS_PATH"

cd ../..
"$DOTNET_INSTALL_DIR/dotnet" publish src/Habitica.WebApp/Habitica.WebApp.csproj -c Release -o output
