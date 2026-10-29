# Push reminder setup and recovery

## User flow

Todo Sync exposes a separate account push consent, device enrollment, per-device remote revocation and matching local-browser subscription removal. Status loading requests no notification permission. Enrollment invokes prepared browser interop directly from the explicit user gesture. The installed-app prerequisite, denied permission, unsupported platform, disabled service, local-storage failure and uncertain original request remain visible.

`/todo/reminders` exposes the selected workspace, exact task, existing voluntary-review editor and independent cloud push schedule. Browser workspaces retain foreground cues; cloud schedules require the selected cloud document revision. One-time schedules use the exact saved UTC review cue. Daily, weekly and monthly schedules require an explicit civil timezone, start, interval, local time, end date, gap skip and overlap policy. Device targets bind exact device/subscription IDs and revisions. Preview acceptance saves only schedule configuration. Cancellation retains tasks and review cues. Paused, cancelled and finished schedules remain inspectable, including separate delivery observations.

Remote device revocation removes Auth-held endpoint/auth material. Local browser unsubscribe removes only the matching browser subscription. Account push consent, browser permission, task schedules and foreground cue preferences remain separate controls. Browser data removal clears local bindings and original-command references; browser removal does not revoke remote consent or system permission. Todo Sync provides remote revocation before browser removal.

## Identity and recovery

`NotificationLocalStore` persists per-account/profile/connection bindings and per-workspace original request/command metadata through IndexedDB revision CAS. Subscription endpoints, authentication keys, management tokens and schedule acceptance tokens never enter the local notification namespace. Prepared subscription material exists only for immediate Auth upload. An explicit retry obtains fresh browser material, verifies the original fingerprint and resends the original IDs and arguments. Receipt recovery never treats absence as a failed effect.

`TaskReminderController` records the original preparation before preview and records acceptance-attempted before sending acceptance. Unknown preparation retries the original input; unknown acceptance checks the original command. Unsubmitted previews can be explicitly discarded through CAS. Submitted uncertain requests remain retained until an exact receipt establishes the result. Acceptance tokens remain memory-only; re-preparation requests a fresh token for the same command. Current account, cloud context, selected source and document revision fence late callbacks and acceptance. Configuration receipts distinguish saved/scheduled from dispatched/provider-accepted; shown/read remain unknown.

## Notifications and private task access

The published service worker imports `js/pushNotifications.js`. Push packets require version/type, valid opaque identities, bounded expiry, fixed public title/body and the fixed source route. Notification display always uses generic public text. The worker retains only routing IDs in notification data. Notification clicks build a constant same-origin `/todo/reminders/open` destination. The worker reuses only an exact guarded destination, preserving unrelated open editors. No click fetches task content or performs a task/game mutation.

The open route requires sign-in and an explicit fresh authenticated read of the exact original cloud workspace before displaying task content. Offline/unavailable reads reveal no cached task content. Missing or archived tasks remain unavailable. Sign-in preserves only the strict notification target, including repeated authentication observers; arbitrary return destinations are refused. The open route performs no implicit connection, source switch, review action or permission grant.

## Platform and qualification boundary

[WebKit documents Home Screen web-app push and direct-user-interaction permission requests](https://webkit.org/blog/13878/web-push-for-web-apps-on-ios-and-ipados/). The implementation uses capability detection and an installed-app prerequisite. The documentation reference establishes platform prerequisites; synthetic fixtures do not establish actual iPhone delivery.

Component controls cover explicit enrollment, denied/unsupported/install prerequisites, original receipt recovery, one-time/repeat previews, cancellation, source/account changes, offline open and sign-in return. Production-helper IndexedDB controls exercise independent connection CAS and secret-field refusal. Rendered fixtures exercise phone/desktop widths, normal/enlarged text, light/dark schemes including Obsidian Glow, primary controls, disabled states, geometry and contrast. Physical iPhone permission, closed-app delivery, hosted login, real push providers and assistive technologies remain separate release qualification.

## Sources

- `src/Habitica.Application/Notifications/` defines typed identity, transport, browser and local-storage contracts.
- `src/Habitica.WebApp/State/PushDeviceController.cs` and `TaskReminderController.cs` own explicit actions and original-result recovery.
- `src/Habitica.WebApp/Integration/WorkspaceCloudClient.Reminders.cs` validates exact previews and configuration-only receipts.
- `src/Habitica.WebApp/Components/PushDevicePanel.razor`, `TaskPushReminderEditor.razor` and the two reminder pages expose the flow.
- `tests/Habitica.WebApp.Tests/Workspace/PushReminder*`, reminder HTTP tests and Node notification/storage controls provide synthetic evidence.
