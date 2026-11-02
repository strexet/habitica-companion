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
  "instruction_profile_id": "5f4d751258f7b082643e12b2d2f4b764366ff64a302ae3aa5d40bb053ad063e4",
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
