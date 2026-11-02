# Concrete Project Profile

Flavor: web. Static compatibility only; execution remains unqualified.

```json
{
  "facts": {
    "capabilities": {
      "playwright-test": {
        "package": "@playwright/test",
        "state": "absent",
        "version": null
      }
    },
    "node": "22",
    "package_manager": "npm",
    "packages": {},
    "test_assemblies": [],
    "ui": []
  },
  "gaps": [],
  "instruction_profile_id": "bb326012f921b283552737895372f53475376596a98322fdf551cdd009713122",
  "recipes": {
    "dotnet-focused-tests": {
      "argv": [
        "dotnet",
        "test",
        "{test_project}",
        "-m:1",
        "-nodeReuse:false",
        "--filter",
        "{test_filter}",
        "--logger",
        "trx;LogFileName={report_name}",
        "--results-directory",
        "{result_directory}"
      ],
      "execution_notes": "Use a meaningful task-specific test filter; require nonzero discovery and preserve TRX evidence. Existing project build prerequisites remain binding.",
      "input_paths": [
        ".skill-tree/validation-recipes.json",
        "global.json",
        "Directory.Build.props",
        "Directory.Packages.props",
        "tests/Habitica.Api.Tests/Habitica.Api.Tests.csproj",
        "tests/Habitica.Application.Tests/Habitica.Application.Tests.csproj",
        "tests/Habitica.BrowserChecks/Habitica.BrowserChecks.csproj",
        "tests/Habitica.Domain.Tests/Habitica.Domain.Tests.csproj",
        "tests/Habitica.Rules.Tests/Habitica.Rules.Tests.csproj",
        "tests/Habitica.Storage.Tests/Habitica.Storage.Tests.csproj",
        "tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj"
      ],
      "lane": "unit",
      "qualification": "owner-declared static recipe; runner unqualified",
      "toolchain": {
        "csharp": "12.0",
        "evidence": "Repository declarations and existing project.assets.json inspected as data; no runner execution.",
        "framework": "Blazor WebAssembly net8.0",
        "sdk_declaration": "8.0.125",
        "test_frameworks": {
          "Microsoft.NET.Test.Sdk": "18.4.0",
          "bunit": "2.7.2",
          "xunit": "2.9.3"
        }
      }
    },
    "netlify-hosted-controls": {
      "argv": [
        "node",
        "--experimental-strip-types",
        "tests/hosted/normal-delivery.mjs",
        "{session_file}",
        "{report_file}"
      ],
      "execution_notes": "The owner-approved controller snapshots and enables the exact provider lanes before delivery. The command persists original authority before sending, verifies exact signed bytes and replay, then resumes the same report after controller shutdown to verify refusals and an unchanged receiver count. Complete only with deployed-identity/configuration/logging evidence and a final pass report. No real subscription or user payload is permitted.",
      "input_paths": [
        ".skill-tree/validation-recipes.json",
        "tests/hosted/normal-delivery.mjs",
        "tests/hosted/callback-receiver.mjs",
        "deploy/callback-egress/src/gateway.mjs",
        "deploy/callback-egress/src/transport.mjs",
        "workers/chatgpt-auth/src/callback-egress-protocol.ts"
      ],
      "lane": "server",
      "qualification": "owner-declared static recipe; runner unqualified",
      "toolchain": {
        "node_declaration": "22.x",
        "qualification": "Exact deployed gateway/Auth and ephemeral owned receiver; original request ID survives interruptions."
      }
    },
    "node-focused-tests": {
      "argv": [
        "node",
        "--experimental-strip-types",
        "--test",
        "--test-concurrency=1",
        "--test-timeout=15000",
        "{test_file}"
      ],
      "execution_notes": "Explicit test-file selection; no wildcard shell evaluation.",
      "input_paths": [
        ".skill-tree/validation-recipes.json",
        "deploy/callback-egress/package.json"
      ],
      "lane": "unit",
      "qualification": "owner-declared static recipe; runner unqualified",
      "toolchain": {
        "node_declaration": "22.x"
      }
    },
    "pwa-shell-browser": {
      "argv": [
        "node",
        "tests/browser/pwa-shell.mjs"
      ],
      "execution_notes": "Requires documented current Release fixture, pinned PLAYWRIGHT_MODULE, exact owned loopback session, isolated persistent browser profiles and cleanup. No browser execution during installation.",
      "input_paths": [
        ".skill-tree/validation-recipes.json",
        "tests/browser/pwa-shell.mjs",
        "src/Habitica.WebApp/wwwroot/service-worker.published.js",
        "src/Habitica.WebApp/package.json",
        "src/Habitica.WebApp/package-lock.json"
      ],
      "lane": "browser",
      "qualification": "owner-declared static recipe; runner unqualified"
    },
    "subscription-contracts-execution-profile": {
      "argv": [
        "python3",
        "tests/Functions/subscription-contracts.py",
        "{report_file}"
      ],
      "execution_notes": "Serial dry builds then nonzero synthetic Worker/MCP/D1, independent receiver and native local TLS tests. No hosted callbacks or user subscription.",
      "input_paths": [
        ".skill-tree/validation-recipes.json",
        "tests/Functions/subscription-contracts.py",
        "workers/chatgpt-auth/src/events.ts",
        "workers/companion-workspace/src/events.ts",
        "workers/shared/events.ts",
        "workers/chatgpt-auth/test/events.test.mjs",
        "workers/chatgpt-auth/test/fixtures/oauth.mjs",
        "workers/chatgpt-auth/wrangler.jsonc",
        "workers/companion-workspace/wrangler.jsonc",
        "workers/chatgpt-mcp/wrangler.jsonc",
        "deploy/callback-egress/src/gateway.mjs",
        "workers/chatgpt-auth/test/fixtures/events.mjs",
        "workers/chatgpt-auth/src/event-dispatch.ts",
        "workers/companion-workspace/src/event-scheduler.ts"
      ],
      "lane": "integration",
      "qualification": "owner-declared static recipe; runner unqualified",
      "toolchain": {
        "node": "22.x",
        "python": "3.x",
        "workers": "pinned Wrangler4.151.0/Miniflare5; actual local D1"
      }
    },
    "webapp-release-build": {
      "argv": [
        "dotnet",
        "publish",
        "src/Habitica.WebApp/Habitica.WebApp.csproj",
        "-c",
        "Release",
        "-o",
        "{output_directory}",
        "-m:1",
        "-nodeReuse:false"
      ],
      "execution_notes": "Local build only; no deployment. Build authorization and isolated output remain task-specific.",
      "input_paths": [
        ".skill-tree/validation-recipes.json",
        "global.json",
        "Directory.Build.props",
        "Directory.Packages.props",
        "src/Habitica.WebApp/Habitica.WebApp.csproj"
      ],
      "lane": "build",
      "qualification": "owner-declared static recipe; runner unqualified",
      "toolchain": {
        "csharp": "12.0",
        "evidence": "Repository declarations and existing project.assets.json inspected as data; no runner execution.",
        "framework": "Blazor WebAssembly net8.0",
        "sdk_declaration": "8.0.125",
        "test_frameworks": {
          "Microsoft.NET.Test.Sdk": "18.4.0",
          "bunit": "2.7.2",
          "xunit": "2.9.3"
        }
      }
    },
    "worker-focused-tests": {
      "argv": [
        "node",
        "--experimental-strip-types",
        "--test",
        "--test-concurrency=1",
        "--test-timeout=30000",
        "{test_file}"
      ],
      "execution_notes": "Follow documented affected Worker dry-build prerequisites. Run Worker suites separately from .NET and other Worker suites. No real credentials or provider effects.",
      "input_paths": [
        ".skill-tree/validation-recipes.json",
        "workers/chatgpt-auth/package.json",
        "workers/chatgpt-auth/package-lock.json",
        "workers/chatgpt-mcp/package.json",
        "workers/chatgpt-mcp/package-lock.json",
        "workers/companion-workspace/package.json",
        "workers/companion-workspace/package-lock.json"
      ],
      "lane": "integration",
      "qualification": "owner-declared static recipe; runner unqualified",
      "toolchain": {
        "fixtures": "Synthetic providers and isolated Miniflare/D1 only",
        "node_declaration": "22.x"
      }
    }
  }
}
```
