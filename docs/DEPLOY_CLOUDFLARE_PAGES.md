# Deploy to Cloudflare Pages

This app is a Blazor WebAssembly PWA with no backend. Cloudflare Pages can build the app from GitHub and serve the published static files from the `*.pages.dev` domain.

## Repository files

- `build.sh` - installs the pinned .NET SDK, installs npm dependencies, syncs vendored Dexie, and publishes the Blazor WebAssembly app.
- `.node-version` - asks Cloudflare Pages to use Node.js 22.
- `src/Habitica.WebApp/wwwroot/_redirects` - sends app routes such as `/tasks` and `/settings` to `index.html` so refreshes work.

## Cloudflare Pages settings

Use these settings when creating the Pages project:

```text
Framework preset: None
Build command: ./build.sh
Build output directory: output/wwwroot
Root directory: repository root
Production branch: main
Deploy command: leave empty
```

The build script installs .NET SDK `8.0.125` by default to match `global.json`. Override it only if `global.json` is updated in the same change.

Do not set `npx wrangler deploy` as the deploy command for the Git-connected Pages project. That command deploys a Worker and does not know which static output directory to publish. Cloudflare Pages should publish the configured `output/wwwroot` directory after `build.sh` finishes.

## First deployment

1. Push this repository to GitHub.
2. In Cloudflare, open `Workers & Pages`.
3. Create a Pages application and import the GitHub repository.
4. Use the build settings listed above.
5. Add this environment variable if you want a production Habitica `x-client` value baked into `appsettings.json`:

   ```text
   HABITICA_X_CLIENT_HEADER=your-habitica-user-id-habitica-tool
   ```

   This value is public in the deployed frontend. Do not put Habitica API tokens, passwords, or private keys in Cloudflare Pages environment variables for this static app.

6. Save and deploy.
7. Open the generated `https://<project>.pages.dev` URL.
8. Check a routed page refresh, for example `https://<project>.pages.dev/tasks`.

## Updates

Push to the production branch and Cloudflare Pages will rebuild automatically.

## Operational notes

- A custom domain is optional. The generated `*.pages.dev` domain works for HTTPS and PWA testing.
- The app must be served from the site root with `base href="/"`. Subpath hosting is not the baseline deployment target.
- Habitica user credentials remain in the user's browser storage. Cloudflare Pages only serves static assets.
- PWA offline behavior should be validated from the published HTTPS site, not from `dotnet run`.
