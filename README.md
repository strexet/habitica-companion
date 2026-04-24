# Habitica Companion Client

Third-party companion client for Habitica power users. The project is focused on local data analysis, explainable recommendations, and safe assisted actions rather than replacing the official Habitica app.

Current status: early-stage project setup. The repository currently defines product direction, technical baseline, Habitica API constraints, and planned feature behavior before the main implementation work starts.

## Planned feature areas

- Party buff timing recommendations
- Gear sets and gear optimization
- Skill macros with dry-run previews
- Best task selection for skill usage
- Bulk sell planning
- Skill and action result estimates

## Technical baseline

- Blazor WebAssembly PWA
- .NET 8 LTS (`net8.0`)
- MudBlazor UI components
- Local-first storage with IndexedDB behind a Dexie.js interop boundary
- Habitica API v3
- No backend in the MVP architecture

## Project documents

- `PROJECT.md` - product context and goals
- `TECHNICAL.md` - stack, architecture, storage, sync, and deployment rules
- `FEATURES.md` - planned feature behavior and constraints
- `HABITICA_API.md` - Habitica API integration rules
- `RULES.md` - repository and AI-agent workflow rules

## Notes

- Credentials are treated as password-equivalent and stay local to the user's device.
- Mutating actions are intended to be validated, previewed, and executed conservatively.
