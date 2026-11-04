# Pending processing result

Recorded planning snapshot. Source task definitions remain in skill-tree/documents/future.md.

## Processing receipt

Processing run: `PR-662bd939-3fbc-401b-ba37-204067e17989`. Twelve Pending entries promoted into 26 created tasks and three compatible reused tasks. No retained, resolved-as-already-done or unanswered Pending entries. Current prioritized count: 51; Backlog count: 12.

The stopped implementation controller remains stopped. New main tasks remain deferred outside the previous frozen snapshot. Standalone processing supplies no implementation, live testing, deployment, package installation or publication instruction.

## Prioritized queue

Ready describes the researched contract. Unfinished dependency numbers below refer to rows in the same snapshot; required dependencies must finish before execution. Live/client/device qualification still requires the relevant account/device setup. Created and reused provenance is marked explicitly.

| # | Task ID | Priority | Intended result | State | Unfinished dependencies | Provenance |
| --- | --- | ---: | --- | --- | --- | --- |
| 1 | [T-210471d2-f432-43b3-8fc7-bbfd74f93e29](../../documents/future.md:79) | 103 | Require Habitica correspondence in authoritative Todo commands | Ready | None | Created |
| 2 | [T-5d6fb7f0-132b-4bf7-8a5a-8b6d825beb54](../../documents/future.md:118) | 103 | Add reviewed projection commands for existing unchanged tasks | Ready | 1 | Created |
| 3 | [T-0e97833e-efaf-4cbc-ad22-8a03af170c30](../../documents/future.md:157) | 103 | Create and verify corresponding Habitica Habits | Ready | 1 | Created |
| 4 | [T-07d27408-3019-423f-a5de-4cda1f9e2818](../../documents/future.md:196) | 103 | Create mapped Habitica Dailies with lossless schedules | Ready | 1 | Created |
| 5 | [T-c43d3aff-9bfa-4684-a248-a83af2193492](../../documents/future.md:235) | 103 | Expose task linking and rejected-preview recovery in Todo UI | Ready | 2 | Created |
| 6 | [T-7bb19b25-83e8-470d-9e94-94a91d744440](../../documents/future.md:274) | 103 | Align browser and agent task defaults with required Habitica links | Ready | 1, 5, 3, 4 | Created |
| 7 | [T-63ee6e49-6452-4e7c-a707-1dd54b41c94c](../../documents/future.md:313) | 103 | Persist stable Habitica tag mappings and projection intents | Ready | None | Created |
| 8 | [T-ed1df00d-7d90-4aac-a3d6-ca2579ec05c0](../../documents/future.md:352) | 103 | Add scoped native Habitica tag and task-tag gateway operations | Ready | 7 | Created |
| 9 | [T-a9dd2e5e-1e51-4537-a3e0-68e3ae730b94](../../documents/future.md:391) | 103 | Reconcile complete organization tag membership without scoring | Ready | 7, 8, 2 | Created |
| 10 | [T-80782d5c-2d9e-4897-ab4c-df3f72b59774](../../documents/future.md:430) | 103 | Expose group tag consent coverage and original receipt recovery | Ready | 9 | Created |
| 11 | [T-1cb1e7eb-8599-428b-9c23-52509b926113](../../documents/future.md:469) | 103 | Convert checklist projects through a resumable identity ledger | Ready | 2, 9 | Created |
| 12 | [T-7f1f3588-abf9-4b7a-ad6c-ab87be50f0c2](../../documents/future.md:508) | 103 | Expose checklist conversion preview and resumable project progress | Ready | 11 | Created |
| 13 | [T-bf9aa399-2724-4f21-868e-dc6e4028004c](../../documents/future.md:547) | 103 | Retire verified converted source tasks through supported reviewed commands | Ready | 12 | Created |
| 14 | [T-b89fac23-3ac2-4c64-8560-be7a5d796a81](../../documents/future.md:586) | 101 | Distinguish bulk cast outcomes from cooldown cleanup and refresh failures | Ready | None | Created |
| 15 | [T-e7d542d4-4a47-4155-afc9-ab0dfb457fa0](../../documents/future.md:625) | 101 | Recover original spell gear after cancellation and interrupted cleanup | Ready | 14 | Created |
| 16 | [T-fed75824-e410-4bca-8664-5039742424da](../../documents/future.md:664) | 100 | Add stable unique project aliases to canonical commands and reads | Ready | None | Created |
| 17 | [T-4b2b6c3b-0c31-4e65-89ab-9e9a63f8bf11](../../documents/future.md:703) | 100 | Expose project aliases in browser pickers and agent planning | Ready | 16 | Created |
| 18 | [T-d46e457f-7139-416e-875a-840f9ff6ca89](../../documents/future.md:742) | 100 | Resolve deadline shorthand to explicit civil dates in agent workflows | Ready | None | Created |
| 19 | [T-c8e0c53c-c9f3-430a-ae54-76da27fdf9c9](../../documents/future.md:781) | 100 | Expose backend build and tool compatibility discovery | Ready | None | Created |
| 20 | [T-6706aad6-b870-484b-80c3-e56c29431556](../../documents/future.md:820) | 100 | Package traceable skill releases and truthful host update guidance | Ready | 19 | Created |
| 21 | [T-6b956777-ed98-4783-a353-5e490de79444](../../documents/future.md:859) | 100 | Add independent account-bound Markdown note records and local storage | Ready | None | Created |
| 22 | [T-385399c5-981c-4710-ae5e-4e0f4860dbdd](../../documents/future.md:898) | 100 | Add separately consented cloud note commands and private reads | Ready | 21 | Created |
| 23 | [T-ea6e2e26-15f9-4889-9505-5da1d96a6c1d](../../documents/future.md:937) | 100 | Add agent note discovery capture and verified document updates | Ready | 22 | Created |
| 24 | [T-3ef1c9c0-dd43-487f-a8b5-22c1771ce1d3](../../documents/future.md:976) | 100 | Build the Notes section with safe Markdown editing and recovery | Ready | 21, 22 | Created |
| 25 | [T-f362faf0-4d48-4ed3-b586-843c47345510](../../documents/future.md:1015) | 100 | Default inventory equipment recommendations to Intelligence | Ready | None | Created |
| 26 | [T-9dce9211-4c9d-478a-952c-12adba3e013f](../../documents/future.md:1054) | 100 | Qualify automatic Companion discovery and invocation | Ready | None | Existing |
| 27 | [T-1f1eb5fd-febd-44fc-b475-69121e11400a](../../documents/future.md:1094) | 100 | Expose iPhone PWA reminder setup and recovery | Ready | 28 | Existing |
| 28 | [T-ce099b7b-f9cc-49b8-aadd-04b5623b1094](../../documents/future.md:1134) | 100 | Dispatch durable scheduled Web Push reminders | Ready | 29, 49 | Existing |
| 29 | [T-0e166dfc-1eb0-42f7-ba4d-5949f476d31b](../../documents/future.md:1174) | 100 | Persist explicit account-bound Web Push subscriptions | Ready | 31 | Existing |
| 30 | [T-6a8db6be-b721-4b88-b62b-c6566d759080](../../documents/future.md:1214) | 100 | Determine supported originating-chat reminder delivery | Ready | None | Existing |
| 31 | [T-51ce8719-0131-46d3-882a-4219d1bef929](../../documents/future.md:1254) | 100 | Complete iPhone PWA installation and relaunch support | Ready | None | Existing |
| 32 | [T-a7c06616-38a3-4cfb-8f2d-4ee41eab6b4c](../../documents/future.md:1294) | 100 | Execute explicitly requested task edits without redundant agent approval | Ready | None | Existing |
| 33 | [T-b92d23ba-8586-4869-a6b8-f8eb180f1b02](../../documents/future.md:1334) | 100 | Move task analytics to a dedicated statistics page | Ready | None | Reused |
| 34 | [T-c0bf3f10-141f-464c-bc1e-53040d67e61c](../../documents/future.md:1374) | 100 | Create dedicated Todo sync and task settings | Ready | None | Existing |
| 35 | [T-3b76d03e-c380-4c4f-9e2b-a982c1c9850b](../../documents/future.md:1414) | 100 | Compose the always-open Todo navigation and task pages | Ready | 33, 34 | Existing |
| 36 | [T-d76d2a9a-1318-4e63-9bb4-73f620b108cc](../../documents/future.md:1454) | 100 | Qualify ChatGPT, Codex, multi-user and mobile release gates | Blocked pending answers | None | Existing |
| 37 | [T-89b296c0-fb5e-4cf3-9b1b-7462acb2da25](../../documents/future.md:1497) | 99 | Diagnose browser qualification startup timing | Ready | None | Existing |
| 38 | [T-13c008e0-2062-4846-ab4b-0f331a280f0b](../../documents/future.md:1537) | 99 | Serialize scoped diagnostic append and clear operations | Ready | None | Existing |
| 39 | [T-06336249-6901-4d10-8df1-229ca9bcad6b](../../documents/future.md:1577) | 99 | Prepare reproducible task UX and device qualification evidence | Ready | 35 | Existing |
| 40 | [T-2355607b-ba77-46c0-9495-aae61e16a8f0](../../documents/future.md:1618) | 99 | Restore archived tasks and correct native Daily occurrences | Ready | None | Existing |
| 41 | [T-0a2f12ac-6df6-498f-b090-f69c2a526b2a](../../documents/future.md:1659) | 99 | Add reviewed own-task Habitica unchecking and reconciliation | Ready | 40 | Existing |
| 42 | [T-8545fa1a-f9e1-4c2a-8501-30bfbae4c985](../../documents/future.md:1700) | 99 | Expose extended recovery actions in task history | Ready | 40, 41 | Reused |
| 43 | [T-aa82feee-97b1-4f14-8b5a-d642cca014e3](../../documents/future.md:1741) | 99 | Retain trustworthy task activity and schedule coverage | Ready | None | Existing |
| 44 | [T-3c84f362-0f7b-4f0c-9e23-75d9a1e0725f](../../documents/future.md:1782) | 99 | Show coverage-aware Daily and Habit activity streaks | Ready | 43, 33 | Existing |
| 45 | [T-e77f6c7c-cf2f-487d-bb82-831e6f6167f9](../../documents/future.md:1823) | 99 | Edit supported linked Daily schedules with explicit reconciliation | Ready | 43, 4 | Reused |
| 46 | [T-70251ccf-7d6a-4e89-b4d4-98cd15c9d528](../../documents/future.md:1864) | 99 | Convert task types through reviewed linked copies | Ready | 40 | Existing |
| 47 | [T-f5079dc2-61e9-4889-bbb0-9c2edb43eadd](../../documents/future.md:1905) | 99 | Add voluntary review scheduling and retained review history | Ready | None | Existing |
| 48 | [T-916deee7-8be7-44aa-9cd2-b9d86e90e56d](../../documents/future.md:1946) | 99 | Evaluate saved lists with explicit relative-date predicates | Ready | None | Existing |
| 49 | [T-b05e848e-9bb0-461c-aee0-8c4293fcbeb2](../../documents/future.md:1987) | 99 | Deliver explicit in-app and foreground browser reminder cues | Ready | 47, 34 | Existing |
| 50 | [T-96866c8f-9554-55d8-a638-6d8b5afaf21c](../../documents/future.md:2028) | 97 | Security Hardening, Revocation, Key Rotation, And Team Rollout | Ready | 36 | Existing |
| 51 | [T-16e1b491-18c3-498e-82da-81a97ff1294a](../../documents/future.md:2179) | 25 | Add project-filtered retained task history navigation | Ready | None | Created |

## Material decisions and limits

- Every ordinary Todo and full child requires Habitica correspondence. Existing accepted receipts retain original meaning; local capture and missing consent remain explicit pending integration.
- Historical completed-task repair cannot write protected completion fields through normal create/update. Verified retained identity or a separate explicit reviewed game-effect decision is required; no automatic reward backfill.
- Organization tags require additive authority; existing zero-effect organization permission does not silently expand. Remote tag deletion is excluded.
- Existing adaptive pacing remains in use; spell work targets failure classification and interrupted equipment restoration.
- Notes have separate local records, cloud consent and note read/write permissions; task-read grants and old task exports do not expose personal documents.
- Plugin package identity differs from backend version; absent host/package evidence remains unknown. Version functionality belongs to a later release.
- Completed history, priority fields, statistics and recovery work are reused rather than duplicated.

## Processing provenance

- `P-2c423cb9-59c1-4364-9d89-a3dfdbf9655e`: promoted. Promote canonical alias identity/uniqueness and separate browser/agent journeys using an explicit workspace-local normalization policy. Produced/reused IDs: `T-4b2b6c3b-0c31-4e65-89ab-9e9a63f8bf11`, `T-fed75824-e410-4bca-8664-5039742424da`.
- `P-314450c7-4cb3-4604-a872-27d0f2d189b9`: promoted. Promote four bounded Notes units for local model/storage, separately consented cloud service, agent tools and safe browser authoring. Personal notes remain separate from task projections and old grants. Produced/reused IDs: `T-ea6e2e26-15f9-4889-9505-5da1d96a6c1d`, `T-3ef1c9c0-dd43-487f-a8b5-22c1771ce1d3`, `T-385399c5-981c-4710-ae5e-4e0f4860dbdd`, `T-6b956777-ed98-4783-a353-5e490de79444`.
- `P-3736aa26-c58b-402d-a736-ff6ce23907bd`: promoted. Promote separate Habit and lossless Daily creation units. Reuse the existing linked-Daily schedule task and make shared creation normalization a prerequisite instead of duplicating schedule authoring. Produced/reused IDs: `T-07d27408-3019-423f-a5de-4cda1f9e2818`, `T-0e97833e-efaf-4cbc-ad22-8a03af170c30`, `T-e77f6c7c-cf2f-487d-bb82-831e6f6167f9`.
- `P-4b315ee2-e2b4-4fa0-82fb-1c43e7336a8e`: promoted. Promote the targeted Intelligence preference change; current defaults remain Balanced, all alternate goals remain available and opening the optimizer must not equip gear. Produced/reused IDs: `T-f362faf0-4d48-4ed3-b586-843c47345510`.
- `P-505551fa-3ca7-4415-b19e-b64585bf8f9f`: promoted. Promote four bounded units for persistent tag identity, separately scoped gateway operations, complete membership reconciliation and browser/agent recovery; native tag deletion and score effects are excluded. Produced/reused IDs: `T-80782d5c-2d9e-4897-ab4c-df3f72b59774`, `T-ed1df00d-7d90-4aac-a3d6-ca2579ec05c0`, `T-63ee6e49-6452-4e7c-a707-1dd54b41c94c`, `T-a9dd2e5e-1e51-4537-a3e0-68e3ae730b94`.
- `P-7414291b-9448-4d98-884c-97b582cd2d67`: promoted. Promote resumable source-item identity conversion, browser/agent conversion journeys and separately reviewed linked-source retirement. Retain the legacy source until every replacement and historical-state decision is verified. Produced/reused IDs: `T-bf9aa399-2724-4f21-868e-dc6e4028004c`, `T-1cb1e7eb-8599-428b-9c23-52509b926113`, `T-7f1f3588-abf9-4b7a-ad6c-ab87be50f0c2`.
- `P-762549ef-d24d-4caa-ab3a-573a8e60eb21`: promoted. Promote only missing project-filtered retained history/navigation, and reuse existing dedicated statistics and extended-recovery tasks. Existing search/date/history behavior is not reimplemented. Produced/reused IDs: `T-16e1b491-18c3-498e-82da-81a97ff1294a`, `T-8545fa1a-f9e1-4c2a-8501-30bfbae4c985`, `T-b92d23ba-8586-4869-a6b8-f8eb180f1b02`.
- `P-9cc8f051-8a18-46e0-b1fa-959c362efa54`: promoted. Promote a typed unchanged-task repair command and a separate UI/recovery unit. Historical completion repair requires verified identity or explicit review rather than synthetic completed fields or automatic reward backfill. Produced/reused IDs: `T-5d6fb7f0-132b-4bf7-8a5a-8b6d825beb54`, `T-c43d3aff-9bfa-4684-a248-a83af2193492`.
- `P-c151f29d-1662-4e34-9316-b849c085681f`: promoted. Promote backend compatibility discovery and traceable later package releases. Installed host identity remains unknown without actual package/host evidence; no current production publication follows. Produced/reused IDs: `T-c8e0c53c-c9f3-430a-ae54-76da27fdf9c9`, `T-6706aad6-b870-484b-80c3-e56c29431556`.
- `P-e19b7c0e-af23-4983-908f-d2e0acde668e`: promoted. Promote multilingual deadline shorthand/date interpretation over existing exact fields, with explicit timezone/year rules and separate host qualification; no notification delivery or duplicate parser is implied. Produced/reused IDs: `T-d46e457f-7139-416e-875a-840f9ff6ca89`.
- `P-f0b54e18-9d9f-4528-8383-db7de5542c63`: promoted. Promote authoritative prospective Todo policy and coordinated browser/agent defaults. Existing immutable journals, local capture, consent and uncertain-outcome fences remain explicit. Produced/reused IDs: `T-7bb19b25-83e8-470d-9e94-94a91d744440`, `T-210471d2-f432-43b3-8fc7-bbfd74f93e29`.
- `P-f6c9c39b-7d9a-476c-b7e1-f2243beee5d0`: promoted. Existing adaptive pacing already covers serialized API dispatch. Promote truthful stage/outcome reporting and durable bounded equipment recovery for missing cancellation cleanup and swallowed restoration failure; do not rebuild pacing. Produced/reused IDs: `T-e7d542d4-4a47-4155-afc9-ab0dfb457fa0`, `T-b89fac23-3ac2-4c64-8560-be7a5d796a81`.

## Next-action handoff

Available choices: keep the planning result; choose another exact task or retained research topic; request another action.

Recommended action: Work Through Next for T-210471d2-f432-43b3-8fc7-bbfd74f93e29, Require Habitica correspondence in authoritative Todo commands. Priority 103 is highest, no unfinished prerequisite exists, and the authoritative command policy precedes safe ordinary creation, existing-task repair and recurring creation. Wait for an actual owner selection.
