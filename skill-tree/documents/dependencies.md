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

Eight generic Skill Tree packages are copied into `.agents/skills/`, including Add Pending, Work Through Next and Implement All. Installed packages include standalone references and helpers; execution requires no source-repository checkout. Python 3 executes workflow helpers. Skill Tree source revision: `0f91954`; generated package resources are owned by Skill Tree and require synchronized upstream updates.
