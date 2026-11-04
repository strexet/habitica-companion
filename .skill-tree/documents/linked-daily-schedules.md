# Reviewed linked Daily schedule edits

## Boundary

The explicit `editLinkedDailySchedule` operation replaces the native recurrence and queues one metadata update for an exact already-linked active Daily. Fresh provider observation requires the connected cloud Workspace. Browser-only, pending, unknown and conflicting mappings keep schedule authoring unavailable. Ordinary `updateTask` recurrence replacement remains restricted to unlinked Dailies. Native skip, pause and resume retain the existing unlinked-only boundary.

Local source implementation and isolated synthetic qualification grant no production publication, provider task mutation, executor enrollment or device permission. Real notifications in chat/on a device remain a separate owner-deferred qualification.

## Lossless subset and provider semantics

Source review on 2026-11-03 checked the official [task update controller](https://github.com/HabitRPG/habitica/blob/develop/website/server/controllers/api-v3/tasks.js#L571), [start-date normalization](https://github.com/HabitRPG/habitica/blob/develop/website/server/libs/tasks/utils.js#L56), [task model](https://github.com/HabitRPG/habitica/blob/develop/website/server/models/task.js#L351) and [schedule evaluator](https://github.com/HabitRPG/habitica/blob/develop/website/common/script/cron.js#L111).

Every Daily PUT can replace nonempty monthly days with the account-local start-date day. The update-supported monthly subset therefore contains one month date matching the local start date. Multi-date monthly schedules remain retained and read-only, including during an ordinary title-only update. The provider schedule evaluator uses rolling start-based week intervals; multiweek native rules require a Monday anchor for representable parity. Calendar preview skips absent month dates; preview dates never assert provider completion, computed `isDue` or provider `nextDue` output.

Supported daily, weekly and monthly day-of-month rules use UTC start years2–9998, intervals1–366, exact UTC or fixed Etc/GMT zones matching the observed account offset, midnight account day start and an account-local midnight provider start. Yearly schedules, zero/large retained intervals, month-week rules, mixed inactive month fields, monthly day/start mismatches, non-midnight starts, named DST zones and mismatched/custom account days refuse without truncating fields. Daily/monthly edits preserve inactive raw weekday defaults. Weekly edits explicitly replace all seven weekday flags. Native start dates retain lower bounds; retained history remains unchanged.

## Review, acceptance and dispatch

`prepare_task_command` accepts operation `editLinkedDailySchedule`, exact Companion task ID/revision and only `patch.recurrence`. The server allocates the command ID, refuses caller-supplied linked context, checks unresolved game/tag effects and reads the exact Daily plus account settings through Auth. The durable preview stores complete raw before/expected-after frequency, interval, start, weekday, month-date and month-week fields, exact Habitica ID/fingerprint, account offset/dayStart and observation time. The preview expires after90seconds. Repeated identical request IDs return the original preview rather than refreshing observations implicitly.

`edit_linked_daily_schedule` accepts the exact server-issued command ID. Acceptance rechecks the canonical revision, mapping, complete policy, observation age and unresolved task effects. Command hash v12 binds every linked context field over the existing recurrence command hash. Commands without linked context retain prior hash versions and optional JSON keys remain omitted. The accepted immutable task snapshot, activity witness and acceptance time retain the effective native schedule epoch. Acceptance does not claim a successful provider update.

The opted-in executor obtains fresh account and exact-task checks, compares the original complete fingerprint/schedule/account and sends only the six reviewed schedule fields. Auth verifies both check tokens and the reviewed rule, re-reads account/task immediately before reserving one PUT, and applies existing pacing, credential, connection and quota fences. No score, Cron, resting setting or historical backfill belongs to the operation. Habitica offers no conditional task PUT; a final external edit between observation and PUT remains an explicit upstream race.

Even ordinary Daily field updates undergo a fresh account/task read before PUT. A retained monthly or unsupported rule cannot be silently rewritten by a title-only change. A refusal leaves the original provider fields unchanged.

## Original outcomes and schedule epochs

The Auth attempt journal stores the actual normalized provider schedule and observation time with the original outcome. Workspace records the original attempt ID and compares the actual schedule with the reviewed expectation. Matching normalization confirms one effect. Different normalization retains the actual fields, marks the command blocked and mapping externally conflicting, and requires explicit reconciliation. Native intent remains distinct from provider response facts.

`get_task_command` and repeated acceptance return `scheduleEpoch`: native acceptance time, complete native rule, reviewed linked context and optional original normalized result. Missing results remain pending/unknown rather than successful. Typed clients validate the epoch, requested rule, exact command/task IDs and normalization flag. Provider state equality cannot substitute for an original schedule-write receipt. An uncertain dispatch retains the prepared original request; lookup of a durable Auth receipt can recover a lost Auth-to-Workspace acknowledgement without a second PUT. An unknown Auth outcome remains fenced, even when current provider fields equal the desired rule.

## Interface and agent parity

The recurring editor enables linked replacement only for eligible active cloud-linked Dailies. Clear, native occurrence and lifecycle controls remain disabled for linked work. Review displays the saved native rule, complete raw before/expected-after provider fields, account settings, current account day and up to six dates in a bounded366-day window. Confirmation names the metadata effect and independent provider penalties. Todo Sync retains pending/original epoch wording. Account changes hide private previews and receipts before a replacement Workspace can load.

The reviewed MCP prepare/accept/original-receipt contract covers the same action. Discovery metadata never grants a write scope; acceptance requires `habitica.tasks.write` and existing account consent. Agent workflow uses MCP rather than browser automation. Browser automation belongs only to owned synthetic UI qualification.

## Verification sources and limits

- `tests/fixtures/workspace/linked-daily-schedule-v1.json`: twenty independently asserted support/refusal examples shared by C# and TypeScript.
- `tests/fixtures/workspace/linked-daily-command-v12.json`: independent length-prefixed hash fixture, including full account/provider context.
- Application schedule controls: policy, exact epoch acceptance, immutable original receipt, stale refusal and journal compatibility.
- Auth gateway controls: fresh external/account refusals, one update, original lookup, unknown fencing and destructive ordinary monthly PUT refusal.
- Local SDK/Worker/D1 controls: reviewed parity, read-scope refusal, metadata effects only, original lost-ack recovery and retained normalization mismatch.
- Component controls: linked cloud authoring, browser/conflict refusal, exact field/date disclosure, account cleanup, original unknown acceptance and strict typed transport.
- `linked-daily-rendered-browser`: current Release app, light/obsidian schemes, phone/desktop widths, normal/enlarged text, keyboard/focus/geometry/contrast, plus actual hydrated Daily/Sync routes with isolated existing-account recovery, real local Auth/Workspace Workers/D1 and native IndexedDB. The synthetic executor stays disabled and provider writes are prohibited.

Current check receipts and original failures reside under `.skill-tree/history/validation/linked-daily-schedules-2026-11-03/`. Service workers are blocked in the changed-route harness. Hosted OAuth/CORS/deployment, installed PWA activation, physical devices and assistive technologies remain separate qualification boundaries.

Linked capture and linked recurrence authoring use readable UTC offset choices backed by the exact supported UTC/Etc-GMT IDs. Choices are derived through DailyProjectionSchedule normalization; UTC+07:00 retains Etc/GMT-7 and UTC−05:00 retains Etc/GMT+5. Existing Etc/GMT aliases remain intact until an explicit choice. Fresh observed account settings constrain choices; fractional offsets and nonmidnight starts show an unsupported-settings reason, and mismatches require an explicit correction. Observation never rewrites a draft and never replaces fresh server validation. Unverified account settings remain labelled. Native unlinked recurrence retains a separate free-text UTC/IANA input and DST guidance.
