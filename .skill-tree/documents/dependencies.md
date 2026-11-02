# Dependencies

## Pinned sources

| Owner | Dependency | Pin |
| --- | --- | --- |
| `global.json` | .NET SDK | `8.0.125` |
| `.node-version`, web package | Node.js | `22.x` |
| `Directory.Packages.props` | ASP.NET WebAssembly and DevServer | `8.0.25` |
| `Directory.Packages.props` | Microsoft.JSInterop | `8.0.25` |
| `Directory.Packages.props` | MudBlazor | `9.3.0` |
| `Directory.Packages.props` | xUnit | `2.9.3` |
| `Directory.Packages.props` | bUnit | `2.7.2` |
| `Directory.Packages.props` | Microsoft.NET.Test.Sdk | `18.4.0` |
| `Directory.Packages.props` | xunit.runner.visualstudio | `3.1.5` |
| `Directory.Packages.props` | coverlet.collector | `6.0.4` |
| `src/Habitica.WebApp/package.json` | Dexie | `4.2.0` |

`src/Habitica.WebApp/scripts/copy-dexie.mjs` prepares vendored Dexie for browser storage. Preserve package pins and committed lockfiles. MCP/OAuth SDK versions in research are candidates requiring fresh official verification before installation.

## Planning toolchain

Eight generic Skill Tree packages are copied into `.agents/skills/`, including Add Pending, Work Through Next and Implement All. Installed packages include standalone references and helpers; execution requires no source-repository checkout. Python 3 executes workflow helpers. Skill Tree source revision: `e8448615cc853ec58f8b1fd7df05732b60e650a0`; generated package resources are owned by Skill Tree and require synchronized upstream updates.

## Dedicated integration Workers

Worker-local package manifests and lockfiles pin Wrangler `4.151.0`, TypeScript `7.0.2`, Workers types `5.20261014.1` and OAuth Provider `1.2.3`. MCP pins the MCP TypeScript SDK `1.32.1`. Auth pins Node.js 22 type definitions for the constant-time comparison API. Auth fixture tests pin Miniflare `5.20261012.0-beta`, matching the selected Wrangler runtime dependency. Worker compatibility date is `2026-10-12`, supported by the locked local runtime; the runtime rejects a later `2026-10-15` date. Existing PWA package pins remain unchanged.
