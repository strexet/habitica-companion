#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WEBAPP_DIR="$ROOT_DIR/src/Habitica.WebApp"

cd "$WEBAPP_DIR"
npm install
npm run sync:vendor

cd "$ROOT_DIR"
dotnet run --project src/Habitica.WebApp --urls http://localhost:5081

