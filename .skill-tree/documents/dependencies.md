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

`src/Habitica.WebApp/scripts/copy-dexie.mjs` prepares vendored Dexie for browser storage. Preserve package pins and committed lockfiles. Installed Worker package manifests and lockfiles own current MCP/OAuth pins; research references do not override installed versions.

## Planning toolchain

Eight web-flavor Skill Tree packages are copied into `.agents/skills/`, including Add Pending, Work Through Next and Implement All. Installed packages include standalone references and helpers; execution requires no source-repository checkout. Python 3 executes workflow helpers. `.skill-tree/installation/profile.json` owns installed identity, source/output digests, capabilities and recipes. The target-local manager owns explicit refresh; [skill-tree-installation.md](skill-tree-installation.md) provides installation context. Generated package resources require a verified managed update.

## Dedicated integration Workers

Worker-local package manifests and lockfiles pin Wrangler `4.151.0`, TypeScript `7.0.2`, Workers types `5.20261014.1` and OAuth Provider `1.2.3`. The MCP runtime pins `@modelcontextprotocol/server` and `@modelcontextprotocol/core` `2.4.0`; modern fixture clients pin `@modelcontextprotocol/client` `2.4.0`. `@modelcontextprotocol/sdk` `1.32.1` remains a development dependency for genuine legacy-client regression checks. [mcp-protocol-compatibility.md](mcp-protocol-compatibility.md) owns wire negotiation and qualification limits. Auth pins `@types/node` `22.20.5` for the constant-time comparison API. Auth fixture tests pin Miniflare `5.20261012.0-beta`, matching the selected Wrangler runtime dependency. Checked-in Worker configurations select compatibility date `2026-10-12`. Package declarations and resolved lock entries establish pins; fresh runtime acceptance requires the corresponding build/test lane.

The callback gateway under `deploy/callback-egress/` uses Node.js `22.x`, native Node modules and the existing Worker toolchain for bundling. [callback-egress.md](callback-egress.md) owns hosted qualification; no gateway publication follows from dependency inspection.
