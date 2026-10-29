# Repository Rules

Primary audience: AI agents working in this repository

## 1. Purpose

This file defines how AI agents must interact with the repository, project documentation, technical stack, and Habitica integration.

Agents must read this file before making non-trivial changes.

## 2. Documentation responsibility map

Use these files as the current documentation source map:

```text
skill-tree/documents/rules.md                         repository workflow, AI-agent rules, required research
skill-tree/documents/technical.md                     architecture, stack, layering, storage, sync, deployment decisions
skill-tree/documents/features.md                      implemented feature behavior, partial limits, code/data-flow ownership
skill-tree/documents/future.md                        single active implementation queue and validated backlog
skill-tree/documents/ux-ui-manifest.md           current UI behavior, interaction rules, responsive guidance
skill-tree/documents/habitica-api.md                  Habitica endpoints, payloads, semantics, mutation safety
skill-tree/documents/habitica-tool-references.md      stable third-party Habitica tool observations
skill-tree/documents/cron.md                          Habitica Cron model and Cron-related project rules
skill-tree/documents/deploy-cloudflare-pages.md  Cloudflare Pages, KV, D1, and operational deployment steps
skill-tree/documents/ai-app-testing.md           browser/UI validation guidance for AI agents
```

Current code and tests are the final verification source for what is actually implemented. When current docs and code disagree, identify whether the document is an intended rule or stale description before changing code.

## 3. Required reading order

Before implementing changes, read the relevant documents in this order:

1. `skill-tree/documents/rules.md`
2. `skill-tree/documents/habitica-api.md`
3. `skill-tree/documents/habitica-tool-references.md` when the change touches Habitica data fetching, sync, task/account/party parsing, quest progress, or rate-limit handling
4. `skill-tree/documents/technical.md`
5. `skill-tree/documents/features.md`
6. `skill-tree/documents/ux-ui-manifest.md` when the change touches UI, UX, layout, visual design, interaction flow, responsive behavior, or user-facing copy
7. `skill-tree/documents/cron.md` when the change touches Start New Day, Daily due-state handling, party buff timing, boss damage at Cron, or member Cron state
8. `skill-tree/documents/deploy-cloudflare-pages.md` when the change touches Cloudflare Pages, KV, D1, migrations, Functions, or operational deployment
9. Existing code and tests in the affected area

For any code that interacts with Habitica, `skill-tree/documents/habitica-api.md` is mandatory reading.
For new or changed Habitica data features, read `skill-tree/documents/habitica-tool-references.md` after `skill-tree/documents/habitica-api.md` and before implementation so stable third-party tool workflows are considered before adding new API assumptions.
For UI or UX work, `skill-tree/documents/ux-ui-manifest.md` is mandatory reading before editing UI code or styles.
For explicit documentation snapshot archive requests, `skill-tree/documents/documents-snapshot.md` is mandatory reading before creating the archive.

## 4. Habitica API rule

When interacting with Habitica API, always follow `skill-tree/documents/habitica-api.md` first.

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

If implementation details in code conflict with `skill-tree/documents/habitica-api.md`, prefer `skill-tree/documents/habitica-api.md` and update code or documentation as needed.

## 5. Technical stack rule

The current baseline stack is documented in `skill-tree/documents/technical.md`.

Agents should avoid changing the technical stack unless there is a strong technical reason.

Do not replace or bypass the selected stack casually. Do not introduce alternative frameworks, storage systems, backend services, or architectural patterns just because they are convenient for one task.

If a problem suggests the current stack is insufficient, the agent may propose a change, but should not silently implement a foundational stack change.

Examples of changes that require updating `skill-tree/documents/technical.md`:

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

When such a change is implemented, update `skill-tree/documents/technical.md` in the same change set.

## 6. Feature documentation rule

When adding a new feature, update `skill-tree/documents/features.md`.

When materially changing how an existing feature works, update the corresponding section in `skill-tree/documents/features.md`.

When removing or deprecating a feature, update its status in `skill-tree/documents/features.md` and explain the reason.

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
Habitica API behavior -> skill-tree/documents/habitica-api.md
Project stack/architecture -> skill-tree/documents/technical.md
Feature behavior -> skill-tree/documents/features.md
UI/UX patterns, review findings, and responsive-design guidance -> skill-tree/documents/ux-ui-manifest.md
Documentation snapshot workflow -> skill-tree/documents/documents-snapshot.md
AI-agent workflow rules -> skill-tree/documents/rules.md
```

If a change touches multiple categories, update all relevant documents.

## 8. Architecture boundaries

Respect the architecture in `skill-tree/documents/technical.md`.

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

## Required task correspondence

Task and organization changes must follow [required Habitica correspondence](companion-workspace-contract.md#required-habitica-correspondence). Every task and full subtask requires the correct Habitica task; task-bearing groups require native tag mappings. Missing mappings, unsupported task kinds, unavailable consent and paused execution remain explicit integration gaps. Native saves, parent links and project membership never prove Habitica presence or rewards. Agents must inspect actual command receipts and preserve unknown-outcome fences; ordinary reward-free task defaults are defects rather than a product exception.

## Agent tool coverage

When an action is part of an intended Codex or ChatGPT workflow but can only be completed in the Companion browser, record the missing MCP capability as a product bug. Add a scoped agent tool with the same identity checks, review, consent, authorization and original-receipt recovery as the application command. Audit the surrounding workflow for other missing steps and cover parity with contract tests. A one-off browser operation does not close the MCP gap or establish an acceptable permanent agent workflow. Do not expose arbitrary Habitica API passthrough or weaken existing approval boundaries to achieve parity.

## Optional integration approval boundary

The [proposed ChatGPT enrollment, privacy and retention contract](companion-first-mcp-research.md#proposed-chatgpt-enrollment-privacy-and-retention-contract) defines the candidate server-side trust boundary. Current PWA sign-in and sync grant no remote credential-storage consent. The repository owner approved the backend/vault contract and selected development capability publication plus bounded own-account Codex live tests on 2026-10-19. Real enrollment retains explicit per-user consent. Party departure remains prohibited; agent party mutations, real Start New Day tests, key operations and independent-user/device qualification remain outside the selected sequence.

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

UI/page changes require changed-route/state fixture coverage, meaningful component interaction assertions and rendered geometry/theme qualification under testing.md and ux-ui-manifest.md. Qualification includes the documented responsive widths, 200-percent text, long content, visible keyboard focus, primary target sizing and normal-theme contrast. Missing physical-device, assistive-technology or unsupported-rendering evidence remains explicit. Static markup or token tests alone do not establish rendered acceptance.

Default tests must not require live Habitica credentials.

Live Habitica API tests must be opt-in and clearly separated from normal CI.

## 12. External-source research rule

Gather current external context before changing behavior where project code and docs are insufficient.

Required external checks:

- Habitica API or data-shape changes: official Habitica API docs and current Habitica server repository when formulas, fields, or endpoint behavior are not fully documented here.
- Cron, damage, Daily due-state, buff expiry, or Start New Day changes: `skill-tree/documents/cron.md`, current Cron-related project code/tests, and official Habitica Cron implementation when local docs do not fully answer the behavior.
- Habitica mobile/web link behavior: `skill-tree/documents/habitica-deeplinks.md`, official app source or association files, and stable web URLs.
- Cloudflare, Wrangler, D1, KV, Pages Functions, or deployment changes: current official Cloudflare documentation when command syntax, bindings, or platform behavior is not obvious from current repo docs.
- Security-sensitive or credential-handling changes: current official platform/API guidance plus existing project tests and docs.

Do not rely on memory or old implementation plans for third-party service behavior. Record uncertainty as an assumption or follow-up instead of presenting it as verified behavior.

## 13. AI-agent change workflow

For a requested change:

1. Read `skill-tree/documents/rules.md`.
2. Read `skill-tree/documents/habitica-api.md` if the change touches Habitica data or API behavior.
3. Read `skill-tree/documents/habitica-tool-references.md` if the change touches Habitica data fetching, parsing, sync, quest progress, or rate-limit behavior.
4. Read `skill-tree/documents/technical.md` if the change touches architecture, stack, storage, deployment, sync, credentials, tests, or logging.
5. Read `skill-tree/documents/features.md` if the change adds or modifies feature behavior.
6. Read `skill-tree/documents/ux-ui-manifest.md` if the change touches UI, UX, layout, visual design, interaction flow, responsive behavior, or user-facing copy.
7. Read `skill-tree/documents/cron.md` if the change touches Cron behavior.
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
Before making non-trivial changes, read and follow `skill-tree/documents/rules.md`. For any Habitica API work, read `skill-tree/documents/habitica-api.md` before editing code. Gather current official external context when local docs/code do not fully establish third-party behavior. Keep `skill-tree/documents/technical.md` updated when foundational technical decisions change, and keep `skill-tree/documents/features.md` updated when features are added, changed, deprecated, or removed.
```

## 17. Documentation snapshot requests

When the user explicitly asks for a document, documentation, markdown, or `.md` snapshot archive, follow `skill-tree/documents/documents-snapshot.md`.

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

## Skill Tree planning workflow

`skill-tree/skill-tree.json` indexes canonical documents and policy references. `skill-tree/documents/future.md` owns task definitions; `future.json` owns generated navigation plus the authoritative active checkpoint. Preserve `active_run` during refresh. Structured history supplies evidence and never grants permission.

Use stable P-/T-/B- UUID identities and integer priorities. Select only Prioritized Next Changes; higher numeric priority wins and document order breaks ties. Check required dependencies and blocking questions before implementation. E01–E14 are research/decomposition intake; labels remain source references alongside stable Pending IDs.

Add Pending captures brief understood requests. Process Pending researches selected Pending IDs/names against current code, tests, docs and official context; promotion produces independently executable contracts and preserves every Pending origin. Standalone processing reports the prioritized list, presents one recommendation last, and waits for an actual developer selection. An active checkpoint alone does not authorize continuation.

Work Through Next handles one explicitly selected root and required descendants. Implement All freezes the initial prioritized snapshot plus required descendants. Implementation findings use Add Pending; nonblocking findings remain deferred after one processing pass. Record accepted validation evidence and terminal task outcomes before queue removal. Imported historical summaries do not establish completed acceptance.

Project documentation uses English and lowercase kebab-case owned paths; prescribed root discovery names and technical identifiers retain spelling. The planning migration preserves inherited product contracts; existing prose-lint gaps do not imply a full product/document audit or permission to change behavior.
