# Production release 2026-10-29

The owner requested all recent implemented features in production and authorized main publication. The existing website remains https://habitica-companion.pages.dev. No new website, database or account was created. Planned future tasks remain unfinished.

## Published implementation

- Task review cues, foreground reminders, Web Push device enrollment and reviewed schedules, with separate permission/consent and original receipts.
- Task activity evidence, retained recovery navigation, linked recovery and exact existing-record adoption, with unknown-outcome fences and no implicit replay.
- Notes project associations, exact project filters and retained version history, with matching application/cloud/MCP contracts.
- Shared diagnostic consent, sanitized preview/upload/recovery, task correspondence controls, inventory presentation and updated MCP discovery guidance.
- PWA installation/relaunch, current task navigation and the MCP 2.4 transport migration. MCP Events and originating-chat callbacks remain unavailable.
- A release-found contrast correction for gradient secondary buttons in Obsidian Glow, and a nullable-flow correction in activity journal validation.

## Production identities

| Component | Published identity |
| --- | --- |
| Auth | 7fd550da-895b-4615-8eb5-93378ab0a4bc |
| Workspace | 1ed64582-45eb-4004-9424-5f312705ca62 |
| MCP | 0c6fdccc-2886-44f3-be61-785921c6340d |
| Shared backend build | 57c8b08d7c601719daf458fc242cd31b00d8dbb18504821420123c3b07b1ae77 |
| Initial Pages artifact | faf92ea3-e151-4bd2-9937-872bff4daa79 |
| Successful Git Pages deployment | 1222d704-660d-4d31-bbaa-d303e8518870 |
| Published feature commit | 02a5953dc768e8a534856300eb3631b3f7014d9b |

The Pages artifact was built from the reviewed dirty develop snapshot based on 117363f. Main publication contains the matching source plus release evidence. The automatic Git build of 02a5953d completed successfully with the checked-in public frontend override. Local develop history is retained; the connected GitHub account publishes the consolidated release tree without rewriting existing main history.

## Migrations and configuration

Applied Auth 0010 and 0011, and Workspace 0013 through 0017. Auth 0012 already existed. Postflight finds no pending migrations. Existing browser KV/party D1, integration resources, credential/profile secrets, runtime compatibility, cron and logging/tracing settings are retained. Dedicated Web Push vault/VAPID keys were provisioned through a protected ignored file. Push availability requires explicit account consent and browser/device permission; no device was enrolled by deployment. Callback egress remains disabled.

Pages production builds use ./build.sh, output/wwwroot and HABITICA_FRONTEND_SETTINGS_FILE=../../deploy/production.frontend.json. Both environments retain existing browser storage bindings and failure behavior. Source defaults still keep optional integration disabled outside the selected production override.

## Validation and limits

Passing checks: 1441 .NET tests, 215 Auth tests, 713 Workspace tests, 94 JavaScript checks, 17 gateway tests and 5 checkpoint tests. Worker type checks, selected production dry runs, the release publish, build-identity and repository consistency checks pass. The historical audit remains stale. Parallel Miniflare runs encountered timeouts/transport failures; serial reruns pass without suppressed assertions.

The release diff also includes previously unpublished historical logs, rendered fixtures and a vendored fixture library. A whitespace check against old main reports trailing spaces or extra final blank lines in 64 of those evidence files. Application source has no such finding. Retain the original evidence/library bytes rather than silently rewriting archival measurements or vendor code to clear this formatting-only check.

The fresh UI matrix passes 2488 cases: 311 captured states at 390/1200px, Gryphy Light/Obsidian Glow, 100/200-percent text. Seeded overflow, overlap and contrast failures are detected. Raw measurements are retained in browser-results.json.gz. Physical devices, assistive technologies, all other width/theme combinations and real notification display are not established by these synthetic checks.

Authenticated live MCP compatibility reports the same new build across all three Workers; existing OAuth and workspace status reads work. All 42 requested task effects remain confirmed, with no pending/unknown task effects. Existing tag work requiring consent/review remains untouched. An ordinary browser loads production Settings with the new PWA/privacy controls and verifies the production Auth/Workspace endpoints. Terminal HTTP probes encounter provider browser filtering and are not counted as application failures.

No live spell reproduction, new Habitica task/game effect, notification subscription or callback dispatch was selected. The iPhone mass-spell incident remains Pending, as do the callback/event workflow and broader client/mobile release gates. Deployment does not install a new client plugin package or refresh host tool caches.

The structured deployment record and backend compatibility observation accompany this report. Previous Worker/Pages identities and pre-migration Time Travel bookmarks remain in the operator evidence. Rolling back code does not undo database state or confirmed external effects; preserve additive schemas and original receipt fences.
