# Build and Release

## Toolchain

`global.json` selects .NET SDK `8.0.125`; `.node-version` and the web package select Node.js `22.x`. NuGet versions reside in `Directory.Packages.props`; npm dependencies and lockfile reside in `src/Habitica.WebApp/`.

## Local prerequisites and build

```sh
cd src/Habitica.WebApp
npm ci
npm run sync:vendor
cd ../..
dotnet restore Habitica.sln
dotnet build Habitica.sln -m:1 -nodeReuse:false
```

`run-app.sh` prepares web dependencies and starts `Habitica.WebApp` on `http://localhost:5081`. Restore/install/build commands remain separate from documentation validation.

## Published output

`build.sh` installs the selected SDK into the local `dotnet/` directory, prepares vendored Dexie, optionally writes `HABITICA_X_CLIENT_HEADER` into web configuration, and publishes the web application into `output/`. `wrangler.toml` identifies Pages output as `output/wwwroot`.

[deploy-cloudflare-pages.md](deploy-cloudflare-pages.md) owns Pages, KV, D1, migration and operational instructions. Current bindings are `HABITICA_SYNC_KV` and `HABITICA_PARTY_DB`. Dedicated MCP/Auth Workers are planned and absent. Deployment, real-service checks and publication require an applicable developer request.
