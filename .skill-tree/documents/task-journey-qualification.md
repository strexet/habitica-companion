# Current task journey qualification

## Scope and provenance

The current navigation owner is AppNavMenu. Todo stays open with Today (/todo/today), Habits (/todo/habits), Dailies (/todo/dailies) and Tasks (/todo/tasks), followed by Projects, Statistics and Sync. Inbox, Planning, Saved lists and History remain secondary destinations. /tasks also contains the shared Companion editor beside Habitica tasks and Rewards. The five-destination navigation prototype is historical design evidence.

The executable pack is [task-journeys.json](../../tests/browser-qualification/task-journeys.json). Eight journeys select sixteen fresh Razor states. Each coverage entry records the actual route, state and exporting interaction-test method. bUnit executes Blazor handlers against synthetic storage/transport. Static browser fixtures qualify rendered geometry, text contrast and native HTML controls; clicking a static Save button cannot establish application persistence or a real game effect.

| Journey | Steps and observed boundary | Interaction owner |
| --- | --- | --- |
| Navigation | Open Todo; visit Today, Habits, Dailies and Tasks; capture and category actions remain visible. | AppNavMenuTests and TodoCategoryPageTests |
| Capture | Enter synthetic title, review exact required game correspondence, accept once; lose acknowledgement and recover the original command. | TodoTaskPanelTests |
| Dates | Change planned date separately from deadline; review replacement placement; retain exact identity and refuse stale/invalid dates. | TodoPlanningPanelTests and TodoTaskPanelTests |
| Complete/restore | Complete exact native work; inspect retained completion; restore an eligible native Todo completion; recover original receipt after uncertainty. | TodoTodayPanelTests and TodoHistoryPanelTests |
| Recurrence | Inspect complete rule/timezone; review replacement; retain recorded dates and pause state. Linked controls state reconciliation limits. | TodoRecurringEditorTests |
| Projects | Navigate exact branch and optional metadata; review native changes; do not infer Habitica tags from a project receipt. | TodoOrganizationPanelTests and TodoHierarchyPanelTests |
| Sync | Inspect current source and exact original command; unknown/missing receipts never enable replacement dispatch. | TodoSyncPageTests |
| Diagnostic recovery | Preview sanitized attachment before copy/download; failed local reads retain explicit unavailability. | LiveTestsPageTests and AppSessionControllerTests |

Current native restoration supports eligible completed, nonarchived, unlinked Todos only. Archived restoration and Daily occurrence correction have separate backend/MCP contracts; History UI exposure and reviewed Habitica unchecking retain separate task contracts. A fixture restore never qualifies those future capabilities or an inverse reward.

## Reproduction

Run from the repository root with the existing .NET dependencies. These commands use synthetic data and local output only. Use a new output directory for each run. Build/publish precedes browser staging; no remote deployment is involved.

```sh
dotnet publish src/Habitica.WebApp/Habitica.WebApp.csproj -c Release -o /tmp/habitica-journey-publish -m:1 -nodeReuse:false
HABITICA_BROWSER_EVIDENCE=/tmp/habitica-journey-captured dotnet test tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj -m:1 -nodeReuse:false --logger trx --results-directory /tmp/habitica-journey-tests
python3 tests/browser-qualification/task-journeys.py --captured /tmp/habitica-journey-captured --output /tmp/habitica-journey-selected
python3 tests/browser-qualification/prepare.py --fixtures /tmp/habitica-journey-selected --published /tmp/habitica-journey-publish/wwwroot --serve-seconds 590 --port 17960
```

Give each external command an independent deadline and cleanup reserve under the repository execution rules. Open the local matrix.html URL while its bounded server is running. Run all sixteen selected fixtures: six widths, seven deterministic themes (including Gryphy Light, Gryphy Dark and Obsidian Glow), 100/200-percent text. Keep the existing five-second case and 300-second matrix limits. If a prerequisite expires, retain measured cases and restart only the missing cases with a fresh legitimate server. Never report unavailable measurements as passing.

Export Raw measurements, coverage.json and asset-fingerprints.json; preserve source hashes, TRX, command deadlines and tool wall time. Run seeded failures and confirm geometry and contrast detectors report their intended errors. Exercise native Tab/Enter/Space in separately opened fixtures and record focus/disclosure behavior independently of component event assertions. Screenshots supplement measurements. Disabled controls are geometry-checked and reviewed visibly; automatic text contrast excludes disabled text, so it supplies no disabled-contrast ratio.

## Observation form

Copy one row per journey, state and execution venue. Leave missing observations unrun; no elapsed time, human rating or device pass can be inferred from fixtures.

| Field | Required record |
| --- | --- |
| Identity | Journey ID, route, fixture/state, exact synthetic task/command identity where applicable; no account IDs or private text |
| Venue | Local synthetic / authorized hosted / physical device / authenticated competitor; operator, UTC observation time and app/browser/OS versions |
| Source | Current source/asset hashes, TRX interaction test or browser result file, screenshot path |
| Display | CSS viewport, actual device orientation/safe-area where tested, theme, normal/enlarged text |
| Interaction | Pointer/keyboard/screen reader; entry/return focus, visible ring, labels, status announcement and disabled-action reason |
| Result | pass/fail/unrun, exact expected versus observed behavior, retained original receipt after interruption |
| Limit | Missing inputs, unexplored paths and release-gate reference; do not store tokens, credential fields or private task content |

## Human and device handoff — all unrun

These rows remain requirements of T-d76d2a9a-1318-4e63-9bb4-73f620b108cc. The owner explicitly deferred that gate; preparation grants no account/device operation scope.

| Lane | Required inputs and observation | Status |
| --- | --- | --- |
| iOS Safari | Owner-selected device/OS/browser, authorized venue and operator; capture/date controls, real virtual keyboard, rotation, safe-area, scroll and interruption/return | unrun |
| iOS installed PWA | Same inputs plus actual installation and standalone launch; separate session, foreground/background recovery and visible source freshness | unrun |
| Android browser/PWA | Named physical device and browser/version; keyboard, enlarged system text, touch, back navigation and lifecycle recovery | unrun |
| VoiceOver and TalkBack | Operator/device and selected screen-reader settings; names, reading order, review entry/return focus, status announcements and keyboard alternatives | unrun |
| Human usability | Consenting participant/session, exact eight journeys, observation/time/error form and feedback; no synthetic task-time claims | unrun |
| Authenticated Todoist and Things | Authorized existing product sessions/platform/version and synthetic comparison tasks; capture, schedule versus deadline, complete/restore and recurrence observations. No new account or purchase implied | unrun |
| Two accounts and hosts | Two consenting testers and selected ChatGPT/Codex versions, existing grants/scopes, isolation/revocation/interruption checks from the release matrix | unrun |

Public documentation was rechecked on 2026-10-27: [Todoist Quick Add](https://www.todoist.com/help/todoist/features/use-task-quick-add-in-todoist-va4Lhpzz) supports title-first capture and optional details; [Things scheduling](https://culturedcode.com/things/support/articles/2803579/) distinguishes start dates and deadlines. These are documented workflow references only. They supply no authenticated competitor observation or usability comparison.

Actual local results reside in [task-journeys/result.json](../history/validation/task-journeys/result.json). The [client release matrix](../../plugin/README.md#client-qualification-matrix) retains host qualification; this pack never upgrades its status.
