# RULES.md

Last updated: 2026-06-09
Primary audience: AI agents working in this repository

## 1. Purpose

This file defines how AI agents must interact with the repository, project documentation, technical stack, and Habitica integration.

Agents must read this file before making non-trivial changes.

## 2. Documentation responsibility map

Use these files as the current documentation source map:

```text
RULES.md                         repository workflow, AI-agent rules, required research
TECHNICAL.md                     architecture, stack, layering, storage, sync, deployment decisions
FEATURES.md                      implemented feature behavior, partial limits, code/data-flow ownership
FUTURE.md                        single active implementation queue and validated backlog
docs/UX_UI_MANIFEST.md           current UI behavior, interaction rules, responsive guidance
HABITICA_API.md                  Habitica endpoints, payloads, semantics, mutation safety
HABITICA_TOOL_REFERENCES.md      stable third-party Habitica tool observations
CRON.md                          Habitica Cron model and Cron-related project rules
docs/DEPLOY_CLOUDFLARE_PAGES.md  Cloudflare Pages, KV, D1, and operational deployment steps
docs/AI_APP_TESTING.md           browser/UI validation guidance for AI agents
```

Current code and tests are the final verification source for what is actually implemented. When current docs and code disagree, identify whether the document is an intended rule or stale description before changing code.

## 3. Required reading order

Before implementing changes, read the relevant documents in this order:

1. `RULES.md`
2. `HABITICA_API.md`
3. `HABITICA_TOOL_REFERENCES.md` when the change touches Habitica data fetching, sync, task/account/party parsing, quest progress, or rate-limit handling
4. `TECHNICAL.md`
5. `FEATURES.md`
6. `docs/UX_UI_MANIFEST.md` when the change touches UI, UX, layout, visual design, interaction flow, responsive behavior, or user-facing copy
7. `CRON.md` when the change touches Start New Day, Daily due-state handling, party buff timing, boss damage at Cron, or member Cron state
8. `docs/DEPLOY_CLOUDFLARE_PAGES.md` when the change touches Cloudflare Pages, KV, D1, migrations, Functions, or operational deployment
9. Existing code and tests in the affected area

For any code that interacts with Habitica, `HABITICA_API.md` is mandatory reading.
For new or changed Habitica data features, read `HABITICA_TOOL_REFERENCES.md` after `HABITICA_API.md` and before implementation so stable third-party tool workflows are considered before adding new API assumptions.
For UI or UX work, `docs/UX_UI_MANIFEST.md` is mandatory reading before editing UI code or styles.
For explicit documentation snapshot archive requests, `docs/DOCUMENTS_SNAPSHOT.md` is mandatory reading before creating the archive.

## 4. Habitica API rule

When interacting with Habitica API, always follow `HABITICA_API.md` first.

Mandatory constraints:

- use Habitica API v3;
- do not build against API v4;
- include required authentication headers for authenticated requests;
- include `x-client` on third-party API requests;
- respect rate limits;
- respect `Retry-After`;
- do not aggressively poll;
- do not blindly retry mutating requests;
- never log Habitica API tokens;
- never include credentials in debug exports, telemetry, or exception reports.

If implementation details in code conflict with `HABITICA_API.md`, prefer `HABITICA_API.md` and update code or documentation as needed.

## 5. Technical stack rule

The current baseline stack is documented in `TECHNICAL.md`.

Agents should avoid changing the technical stack unless there is a strong technical reason.

Do not replace or bypass the selected stack casually. Do not introduce alternative frameworks, storage systems, backend services, or architectural patterns just because they are convenient for one task.

If a problem suggests the current stack is insufficient, the agent may propose a change, but should not silently implement a foundational stack change.

Examples of changes that require updating `TECHNICAL.md`:

- replacing Blazor WebAssembly PWA;
- replacing MudBlazor;
- adding, removing, or materially changing the application/use-case layer;
- replacing IndexedDB/Dexie storage strategy;
- adding a backend;
- adding a native shell as a supported target;
- changing the Habitica API client architecture;
- changing credential storage policy;
- changing sync strategy;
- changing repository structure;
- changing testing baseline;
- changing deployment model.

When such a change is implemented, update `TECHNICAL.md` in the same change set.

## 6. Feature documentation rule

When adding a new feature, update `FEATURES.md`.

When materially changing how an existing feature works, update the corresponding section in `FEATURES.md`.

When removing or deprecating a feature, update its status in `FEATURES.md` and explain the reason.

Feature documentation must include:

- status;
- owner module;
- application entry point when UI/application orchestration is involved;
- primary Habitica data;
- whether it mutates Habitica state;
- offline behavior;
- API interaction;
- algorithm or rules;
- validation;
- error handling;
- security/privacy notes;
- tests;
- open questions when behavior is not fully verified.

Do not add vague product descriptions. Keep feature documentation technical.

## 7. Documentation update policy

Update documentation when the repository behavior changes.

Do not update documentation only to rephrase existing content unless the user explicitly asks for documentation cleanup.

Documentation changes should be minimal, accurate, and placed in the correct file:

```text
Habitica API behavior -> HABITICA_API.md
Project stack/architecture -> TECHNICAL.md
Feature behavior -> FEATURES.md
UI/UX patterns, review findings, and responsive-design guidance -> docs/UX_UI_MANIFEST.md
Documentation snapshot workflow -> docs/DOCUMENTS_SNAPSHOT.md
AI-agent workflow rules -> RULES.md
```

If a change touches multiple categories, update all relevant documents.

## 8. Architecture boundaries

Respect the architecture in `TECHNICAL.md`.

Rules:

- UI components must not contain Habitica formulas or optimizer logic.
- UI components must call application/use-case services or read-model facades, not raw API or storage code.
- Domain models must not depend on UI, storage, or HTTP.
- Application layer code must own orchestration across API, storage, rules, freshness checks, and user-facing workflows.
- Rules/calculation code must be deterministic where possible.
- API code must handle rate limits and credential redaction.
- Storage code must not leak credentials into snapshots or debug exports.
- Mutating operations must be validated and logged.

## 9. Mutating Habitica actions

Any action that changes Habitica state must be implemented conservatively.

Required behavior:

1. Validate against local snapshot.
2. Show dry-run preview for multi-step, destructive, or batch operations.
3. Require explicit user confirmation when needed.
4. Execute sequentially by default.
5. Stop on unexpected state changes or API failures.
6. Respect `Retry-After` and rate-limit headers.
7. Persist an execution log.
8. Refresh or update local state after success.
9. Report partial success clearly.

Do not implement fire-and-forget macro execution, bulk selling, gear switching, or skill casting.

## 10. Security rules

Treat Habitica API token as password-equivalent.

Never:

- log API tokens;
- include API tokens in URLs;
- include API tokens in telemetry;
- include API tokens in exported debug files;
- hardcode user credentials;
- commit credentials;
- expose raw request headers in UI;
- store credentials in calculation snapshots or execution logs.

Always redact secrets in errors and logs.

## 11. Testing rules

For non-trivial logic, add or update tests.

Required test areas:

- Habitica formula calculations;
- optimizer logic;
- macro validation;
- mutating action planners;
- snapshot freshness policy;
- rate-limit handling;
- API error normalization;
- storage migrations;
- storage JS interop/error mapping when browser storage is involved;
- credential redaction;
- snapshot serialization.

Default tests must not require live Habitica credentials.

Live Habitica API tests must be opt-in and clearly separated from normal CI.

## 12. External-source research rule

Gather current external context before changing behavior where project code and docs are insufficient.

Required external checks:

- Habitica API or data-shape changes: official Habitica API docs and current Habitica server repository when formulas, fields, or endpoint behavior are not fully documented here.
- Cron, damage, Daily due-state, buff expiry, or Start New Day changes: `CRON.md`, current Cron-related project code/tests, and official Habitica Cron implementation when local docs do not fully answer the behavior.
- Habitica mobile/web link behavior: `docs/HABITICA_DEEPLINKS.md`, official app source or association files, and stable web URLs.
- Cloudflare, Wrangler, D1, KV, Pages Functions, or deployment changes: current official Cloudflare documentation when command syntax, bindings, or platform behavior is not obvious from current repo docs.
- Security-sensitive or credential-handling changes: current official platform/API guidance plus existing project tests and docs.

Do not rely on memory or old implementation plans for third-party service behavior. Record uncertainty as an assumption or follow-up instead of presenting it as verified behavior.

## 13. AI-agent change workflow

For a requested change:

1. Read `RULES.md`.
2. Read `HABITICA_API.md` if the change touches Habitica data or API behavior.
3. Read `HABITICA_TOOL_REFERENCES.md` if the change touches Habitica data fetching, parsing, sync, quest progress, or rate-limit behavior.
4. Read `TECHNICAL.md` if the change touches architecture, stack, storage, deployment, sync, credentials, tests, or logging.
5. Read `FEATURES.md` if the change adds or modifies feature behavior.
6. Read `docs/UX_UI_MANIFEST.md` if the change touches UI, UX, layout, visual design, interaction flow, responsive behavior, or user-facing copy.
7. Read `CRON.md` if the change touches Cron behavior.
8. Inspect affected code and tests.
9. Gather required external-source context under section 12 when local project sources are insufficient.
10. Make the smallest technically correct change.
11. Update documentation when required by these rules.
12. Add or update tests when logic changes.
13. Review changed files for real issues.
14. Report changed files and any remaining risks.

Do not invent issues. If no issues are found during final review, state that the changed files look correct.

## 14. When to propose instead of implement

Propose a change instead of directly implementing it when:

- it changes the baseline technical stack;
- it adds a backend;
- it changes credential storage policy;
- it changes Habitica API version;
- it requires unverified Habitica formulas for mutating actions;
- it creates background automation that may hit Habitica API repeatedly;
- it changes security assumptions;
- it creates irreversible or destructive user actions.

For small, reversible, well-scoped implementation work, proceed without unnecessary discussion.

## 15. Style rules for project documents

Use English for project documentation.

Keep documentation:

- technical;
- dry;
- explicit;
- concise but complete;
- useful for future agents;
- free of marketing language;
- free of unsupported claims.

When behavior is unknown, write `Open questions` or `Assumption` instead of pretending certainty.

## 16. AGENTS.md handoff command

Add the following instruction to `AGENTS.md`:

```markdown
Before making non-trivial changes, read and follow `RULES.md`. For any Habitica API work, read `HABITICA_API.md` before editing code. Gather current official external context when local docs/code do not fully establish third-party behavior. Keep `TECHNICAL.md` updated when foundational technical decisions change, and keep `FEATURES.md` updated when features are added, changed, deprecated, or removed.
```

## 17. Documentation snapshot requests

When the user explicitly asks for a document, documentation, markdown, or `.md` snapshot archive, follow `docs/DOCUMENTS_SNAPSHOT.md`.

This rule is conditional. Do not create or update snapshot archives during ordinary documentation or code changes unless the user asks for a snapshot.

Default behavior for snapshot requests:

- use tracked project markdown files as the source set unless the user chooses a narrower source scope;
- allow explicit user scope such as one markdown file, several markdown files, one documentation directory, or the newly edited markdown file from the same request;
- for a single-file snapshot request, produce a zip archive containing only that file's renamed snapshot copy;
- exclude ignored dependency, SDK, build-output, and vendor markdown files unless explicitly requested;
- confirm each included document contains repository-specific information;
- do not edit source documents while preparing snapshot copies;
- place the zip archive at repository root, named with the current snapshot creation date and time unless the user provides a different name;
- keep archive members at the zip root, with no preserved folder structure;
- add `.snapshot-YYYY-MM-DD-HH-MM.md` to every archive member filename, using the current snapshot creation date and time;
- prepend a snapshot notice to every copied document explaining source path, snapshot date, relevance, and possible drift;
- verify archive contents before reporting completion.
