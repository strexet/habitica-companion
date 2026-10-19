# Recovered Companion task processing

Seven developer-selected checklist items were captured before checklist acknowledgement. Ten new Ready contracts and one reused project interface contract preserve all seven Pending origins. Priority 100 retains priority-101 defect precedence.

## Research evidence

- Task writes: workers/chatgpt-mcp/src/task-tools.ts and workers/companion-workspace/src/task-api.ts retain internal previews, identity and revision checks. Platform safeguards remain distinct from redundant agent conversation approval.
- Hierarchy: WorkspaceTaskRelationships.cs, WorkspaceRules.cs and task-relationships.test.mjs already support independent Todo parent links and iterative cycle validation. Missing work concerns hierarchy traversal, tree UI and usable deep editing.
- Projects: workspace-organization.ts, organization-commands.ts and organization-tools.ts already implement independent projects and scoped create/update/archive. Additional work concerns safe logical removal and coherent client exposure. The existing browser project contract is reused.
- PWA: manifest.webmanifest already defines standalone launch and icons; service-worker.published.js caches application assets. Product naming and visible installation guidance require work. Persisted-credential relaunch exists; session-only storage must retain the selected policy.
- Reminders: the existing foreground cue contract excludes background delivery. Opt-in subscriptions, durable dispatch and device UX are separate new units.

## Official platform references

- [OpenAI tool contracts](https://developers.openai.com/plugins/plan/tools) distinguish tool annotations from server authorization and consequential-action confirmation.
- [OpenAI MCP Events](https://developers.openai.com/plugins/build/mcp-events) documents authenticated subscription discovery and signed webhook delivery. Host event acceptance does not independently establish originating-chat rendering.
- [Codex app-server](https://learn.chatgpt.com/docs/app-server) documents thread resume and turn start in an authenticated client context. Existing desktop-thread ownership and remote delivery need explicit qualification.
- [OpenAI metadata evaluation](https://developers.openai.com/plugins/guides/optimize-metadata) uses direct, indirect and negative prompt scenarios; discovery metadata cannot guarantee selection in every chat.
- [WebKit Home Screen Web Push](https://webkit.org/blog/13878/web-push-for-web-apps-on-ios-and-ipados/) documents installed-PWA support and gesture-based permission on supported iOS/iPadOS. Actual installation and delivery remain physical-device checks.

## Prioritized queue

| Priority | Task | Status | Required unfinished dependencies |
| --- | --- | --- | --- |
| 101 | T-4b82019b-e1fa-4238-8629-05111e9dcc88 — Repair shared readable text and enlarged layouts | Ready | None |
| 100 | T-264e3162-adc3-45f0-943e-a55feccc9d8e — Complete safe project retirement and agent project lifecycle | Ready | None |
| 100 | T-9dce9211-4c9d-478a-952c-12adba3e013f — Qualify automatic Companion discovery and invocation | Ready | None |
| 100 | T-1f1eb5fd-febd-44fc-b475-69121e11400a — Expose iPhone PWA reminder setup and recovery | Ready | T-ce099b7b-f9cc-49b8-aadd-04b5623b1094, T-ea3c2df0-7c13-4a77-a920-1451801b9946, T-6122dded-900d-4d92-8739-bbc344e15ea0 |
| 100 | T-ce099b7b-f9cc-49b8-aadd-04b5623b1094 — Dispatch durable scheduled Web Push reminders | Ready | T-0e166dfc-1eb0-42f7-ba4d-5949f476d31b, T-b05e848e-9bb0-461c-aee0-8c4293fcbeb2 |
| 100 | T-0e166dfc-1eb0-42f7-ba4d-5949f476d31b — Persist explicit account-bound Web Push subscriptions | Ready | T-51ce8719-0131-46d3-882a-4219d1bef929 |
| 100 | T-6a8db6be-b721-4b88-b62b-c6566d759080 — Determine supported originating-chat reminder delivery | Ready | None |
| 100 | T-51ce8719-0131-46d3-882a-4219d1bef929 — Complete iPhone PWA installation and relaunch support | Ready | T-6122dded-900d-4d92-8739-bbc344e15ea0 |
| 100 | T-eb745ca4-9da7-4fef-8c5f-13f115b4c22a — Build accessible nested Todo task editing | Ready | T-8d5444d6-8dec-4395-ae9d-5ea6fa6d8856, T-ea3c2df0-7c13-4a77-a920-1451801b9946, T-6122dded-900d-4d92-8739-bbc344e15ea0 |
| 100 | T-8d5444d6-8dec-4395-ae9d-5ea6fa6d8856 — Expose stable arbitrary-depth Todo hierarchy queries | Ready | None |
| 100 | T-a7c06616-38a3-4cfb-8f2d-4ee41eab6b4c — Execute explicitly requested task edits without redundant agent approval | Ready | None |
| 100 | T-6122dded-900d-4d92-8739-bbc344e15ea0 — Add reusable page layout and theme qualification | Ready | T-4b82019b-e1fa-4238-8629-05111e9dcc88 |
| 100 | T-d007ac44-f38c-4069-9b70-e66adbc64bf0 — Redact and cover new feature diagnostics | Ready | None |
| 100 | T-3f71047d-22a7-4e85-a356-d41f1c99f82e — Render compact copyable diagnostic rows | Ready | T-d007ac44-f38c-4069-9b70-e66adbc64bf0 |
| 100 | T-7aed32a0-e0b2-4f25-bdca-ebbed8e2a92b — Expose on-demand diagnostic bundles to agents | Ready | T-d007ac44-f38c-4069-9b70-e66adbc64bf0 |
| 100 | T-9f465413-725a-440e-a464-cb5c555cb87d — Unify account-bound Todo workspace presentation | Ready | None |
| 100 | T-ea3c2df0-7c13-4a77-a920-1451801b9946 — Build direct title-first task capture and editing | Ready | T-9f465413-725a-440e-a464-cb5c555cb87d, T-6122dded-900d-4d92-8739-bbc344e15ea0 |
| 100 | T-8e39f2a0-a0fe-4d1a-82af-b3472da5dcae — Build schedule-aware Today task groups | Ready | T-9f465413-725a-440e-a464-cb5c555cb87d, T-ea3c2df0-7c13-4a77-a920-1451801b9946, T-6122dded-900d-4d92-8739-bbc344e15ea0 |
| 100 | T-fb41407b-58d3-4dca-b389-e80bbe517831 — Expose Inbox and calendar planning views | Ready | T-8e39f2a0-a0fe-4d1a-82af-b3472da5dcae, T-ea3c2df0-7c13-4a77-a920-1451801b9946, T-6122dded-900d-4d92-8739-bbc344e15ea0 |
| 100 | T-54623cea-9c8f-4819-b33f-b6552c82df55 — Expose retained task history and safe Todo restore | Ready | T-9f465413-725a-440e-a464-cb5c555cb87d, T-ea3c2df0-7c13-4a77-a920-1451801b9946, T-6122dded-900d-4d92-8739-bbc344e15ea0 |
| 100 | T-574a6ba2-741e-4d13-b798-b909c24de93d — Add reviewed browser organization workflows | Ready | T-9f465413-725a-440e-a464-cb5c555cb87d |
| 100 | T-fd2c633c-7b39-4776-a364-dacb83abaff7 — Expose projects goals and optional task organization | Ready | T-574a6ba2-741e-4d13-b798-b909c24de93d, T-ea3c2df0-7c13-4a77-a920-1451801b9946, T-6122dded-900d-4d92-8739-bbc344e15ea0, T-264e3162-adc3-45f0-943e-a55feccc9d8e, T-eb745ca4-9da7-4fef-8c5f-13f115b4c22a |
| 100 | T-68cb7da7-62e9-4136-b5c5-07da20f97c30 — Expose recurring schedules and occurrence lifecycle | Ready | T-9f465413-725a-440e-a464-cb5c555cb87d, T-ea3c2df0-7c13-4a77-a920-1451801b9946, T-6122dded-900d-4d92-8739-bbc344e15ea0 |
| 100 | T-66aec83d-1d40-4c42-87ec-6566e1ac3769 — Expose reviewed saved-list authoring | Ready | T-574a6ba2-741e-4d13-b798-b909c24de93d, T-6122dded-900d-4d92-8739-bbc344e15ea0 |
| 100 | T-b92d23ba-8586-4869-a6b8-f8eb180f1b02 — Move task analytics to a dedicated statistics page | Ready | T-9f465413-725a-440e-a464-cb5c555cb87d, T-54623cea-9c8f-4819-b33f-b6552c82df55, T-6122dded-900d-4d92-8739-bbc344e15ea0 |
| 100 | T-c0bf3f10-141f-464c-bc1e-53040d67e61c — Create dedicated Todo sync and task settings | Ready | T-9f465413-725a-440e-a464-cb5c555cb87d, T-d007ac44-f38c-4069-9b70-e66adbc64bf0, T-6122dded-900d-4d92-8739-bbc344e15ea0 |
| 100 | T-3b76d03e-c380-4c4f-9e2b-a982c1c9850b — Compose the always-open Todo navigation and task pages | Ready | T-ea3c2df0-7c13-4a77-a920-1451801b9946, T-8e39f2a0-a0fe-4d1a-82af-b3472da5dcae, T-fb41407b-58d3-4dca-b389-e80bbe517831, T-54623cea-9c8f-4819-b33f-b6552c82df55, T-fd2c633c-7b39-4776-a364-dacb83abaff7, T-68cb7da7-62e9-4136-b5c5-07da20f97c30, T-66aec83d-1d40-4c42-87ec-6566e1ac3769, T-b92d23ba-8586-4869-a6b8-f8eb180f1b02, T-c0bf3f10-141f-464c-bc1e-53040d67e61c, T-6122dded-900d-4d92-8739-bbc344e15ea0 |
| 100 | T-d76d2a9a-1318-4e63-9bb4-73f620b108cc — Qualify ChatGPT, Codex, multi-user and mobile release gates | Blocked pending answers | None |
| 99 | T-06336249-6901-4d10-8df1-229ca9bcad6b — Prepare reproducible task UX and device qualification evidence | Ready | T-3b76d03e-c380-4c4f-9e2b-a982c1c9850b, T-6122dded-900d-4d92-8739-bbc344e15ea0 |
| 99 | T-2355607b-ba77-46c0-9495-aae61e16a8f0 — Restore archived tasks and correct native Daily occurrences | Ready | None |
| 99 | T-0a2f12ac-6df6-498f-b090-f69c2a526b2a — Add reviewed own-task Habitica unchecking and reconciliation | Ready | T-2355607b-ba77-46c0-9495-aae61e16a8f0 |
| 99 | T-8545fa1a-f9e1-4c2a-8501-30bfbae4c985 — Expose extended recovery actions in task history | Ready | T-2355607b-ba77-46c0-9495-aae61e16a8f0, T-0a2f12ac-6df6-498f-b090-f69c2a526b2a, T-54623cea-9c8f-4819-b33f-b6552c82df55, T-6122dded-900d-4d92-8739-bbc344e15ea0 |
| 99 | T-aa82feee-97b1-4f14-8b5a-d642cca014e3 — Retain trustworthy task activity and schedule coverage | Ready | None |
| 99 | T-3c84f362-0f7b-4f0c-9e23-75d9a1e0725f — Show coverage-aware Daily and Habit activity streaks | Ready | T-aa82feee-97b1-4f14-8b5a-d642cca014e3, T-b92d23ba-8586-4869-a6b8-f8eb180f1b02 |
| 99 | T-e77f6c7c-cf2f-487d-bb82-831e6f6167f9 — Edit supported linked Daily schedules with explicit reconciliation | Ready | T-aa82feee-97b1-4f14-8b5a-d642cca014e3, T-68cb7da7-62e9-4136-b5c5-07da20f97c30 |
| 99 | T-70251ccf-7d6a-4e89-b4d4-98cd15c9d528 — Convert task types through reviewed linked copies | Ready | T-2355607b-ba77-46c0-9495-aae61e16a8f0, T-ea3c2df0-7c13-4a77-a920-1451801b9946 |
| 99 | T-f5079dc2-61e9-4889-bbb0-9c2edb43eadd — Add voluntary review scheduling and retained review history | Ready | T-9f465413-725a-440e-a464-cb5c555cb87d, T-ea3c2df0-7c13-4a77-a920-1451801b9946 |
| 99 | T-916deee7-8be7-44aa-9cd2-b9d86e90e56d — Evaluate saved lists with explicit relative-date predicates | Ready | T-66aec83d-1d40-4c42-87ec-6566e1ac3769 |
| 99 | T-b05e848e-9bb0-461c-aee0-8c4293fcbeb2 — Deliver explicit in-app and foreground browser reminder cues | Ready | T-f5079dc2-61e9-4889-bbb0-9c2edb43eadd, T-8e39f2a0-a0fe-4d1a-82af-b3472da5dcae, T-c0bf3f10-141f-464c-bc1e-53040d67e61c |
| 97 | T-96866c8f-9554-55d8-a638-6d8b5afaf21c — Security Hardening, Revocation, Key Rotation, And Team Rollout | Ready | T-d76d2a9a-1318-4e63-9bb4-73f620b108cc |

## Controller handoff

The active Implement All checkpoint resumes T-4b82019b-e1fa-4238-8629-05111e9dcc88 before new main work. The original frozen snapshot remains authoritative; new main identities remain deferred unless required through an existing snapshot dependency. The developer request authorizes a subsequent refreshed Implement All snapshot after the active run reaches a valid handoff. No run state or partial implementation is discarded.

Physical iPhone, independent-account and live ChatGPT qualification remain unverified release gates, not synthetic implementation blockers.
