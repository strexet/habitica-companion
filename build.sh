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

if [ -n "${HABITICA_X_CLIENT_HEADER:-}" ]; then
    node -e 'const fs = require("fs"); const header = process.env.HABITICA_X_CLIENT_HEADER || ""; fs.writeFileSync("wwwroot/appsettings.json", JSON.stringify({ Habitica: { XClientHeader: header } }, null, 2) + "\n");'
fi

cd ../..
"$DOTNET_INSTALL_DIR/dotnet" publish src/Habitica.WebApp/Habitica.WebApp.csproj -c Release -o output
