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

`build.sh` installs the selected SDK into the local `dotnet/` directory, prepares vendored Dexie, merges public frontend overrides through `deploy/frontend-settings.mjs`, preserves unrelated settings and restores source configuration after publish success/failure, and publishes the web application into `output/`. `wrangler.toml` identifies Pages output as `output/wwwroot`.

[deploy-cloudflare-pages.md](deploy-cloudflare-pages.md) owns Pages, KV, D1, migration and operational instructions. Current bindings are `HABITICA_SYNC_KV` and `HABITICA_PARTY_DB`. Dedicated Auth, MCP and Workspace Workers reside under `workers/`; [deploy-cloudflare-pages.md](deploy-cloudflare-pages.md#dedicated-worker-local-validation) owns local dry-run and binding fixture commands. Deployment, real-service checks and publication require an applicable developer request.

## Configuration selection

Source PWA settings disable optional integration and leave backend URLs empty. `HABITICA_FRONTEND_SETTINGS_FILE` selects a public override merged during `build.sh`; `deploy/production.frontend.json` supplies the selected production endpoints. `HABITICA_X_CLIENT_HEADER` overrides the public creator header. Neither public configuration file contains credentials. Worker defaults remain disabled independently of frontend settings; [deployment preparation](../../deploy/README.md) owns reviewed backend configuration.

## Worker build identity and local dry bundles

`workers/release-inputs.json` and bounded Worker source/package/config inputs feed `workers/scripts/generate-build-identity.py`. After a reviewed change to the bounded inputs, regenerate the shared identity, then build matching bundles before composed service fixtures:

```sh
python3 workers/scripts/generate-build-identity.py
npm --prefix workers/chatgpt-auth run build
npm --prefix workers/chatgpt-mcp run build
npm --prefix workers/companion-workspace run build
```

Each build checks the generated identity, types and TypeScript before the default-environment Wrangler dry run. Source documentation changes outside the bounded inputs require no identity regeneration. Dry bundles establish no publication, remote migration, host discovery or live-account behavior. Exact release identities and check results belong in structured release evidence under `.skill-tree/history/releases/`.

## Complete main publication

Production Git builds require `HABITICA_PLUGIN_MCP_ENDPOINT` in addition to the public frontend override. `build.sh` creates and verifies the current plugin archive with the exact `CF_PAGES_COMMIT_SHA`; an enabled frontend fails closed when the reviewed public endpoint is absent. Previously published archives remain under `src/Habitica.WebApp/wwwroot/downloads/`.

`tests/release/qualification.py` owns bounded full .NET/Functions, serial Worker, rendered browser/PWA and hosted identity controls. `tests/release/hosted-smoke.mjs` checks fresh signed-out startup in Chromium/WebKit. Physical devices, assistive technology and real account interactions require separate evidence.

`deploy/coordinated-release.py` publishes only explicit committed source to the existing production resources. Run preflight before changes; retain exact predecessor versions and D1 recovery bookmarks privately. Configure Pages public build settings, qualify the integration, publish matching compatible Workers before main triggers a Git build, then build and publish the exact merged main state. The controller preserves live bindings, secret references and runtime flags. Pending migrations stop publication for review. Postflight compares served artifact hashes, Worker source bytes, migration coverage and OAuth discovery. Recovery requires the recorded original versions/bookmarks and a fresh ownership check; the controller never blindly retries an upload with an unknown outcome.
