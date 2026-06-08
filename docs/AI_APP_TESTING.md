# AI App Testing Guide

This guide is for future AI agents testing Habitica Tool in a browser. Use it when reviewing deployed or local UI/UX, especially after layout, sign-in, navigation, theme, or foldable-section changes.

## Targets

- Deployed app: `https://habitica-companion.pages.dev`
- Local app, when user asks to test local build: usually `http://localhost:5081`

Do not start a local server, run build, or run tests unless the user explicitly asks. For deployed checks, use the deployed URL directly.

## Credential Handling

- Ask the user for a Habitica User ID and API token if they have not already provided them in the current task.
- Never write the real User ID or API token into repo files, screenshots, final summaries, `FUTURE.md`, or test notes.
- Use credentials only in the browser sign-in form.
- Do not enable `Remember credentials on this device` unless the user explicitly asks for persistent browser sign-in.
- Do not transmit credentials to any non-Habitica destination. Cloud sync and party sync must never receive the API token.
- If browser snapshots display the password value, do not paste those snapshots into final output or repo docs.

## Login Flow

1. Open `/sign-in`.
2. Fill `Habitica User ID`.
3. Fill `Habitica API Token`.
4. Leave `Remember credentials` unchecked unless user says otherwise.
5. Submit with `Sign In`.
6. Wait for redirect to `/dashboard` and for staged refresh indicators to settle.
7. If routed to Dashboard but data is missing, use `Refresh` only when the user asked for live-data testing. Refresh is read-only but still calls Habitica APIs.

Known blocker to recognize:

- If both fields visibly contain values but submit shows `Habitica User ID and API Token are required.`, record this as a sign-in binding bug and stop authenticated testing. Do not keep retrying with altered credentials.

Expected success signals:

- URL changes from `/sign-in` to `/dashboard`.
- Drawer/navigation appears with Dashboard, Tasks, Equipment, Pets & Mounts, Party, Quests, Spells, Settings, and Diagnostics.
- Topbar identity changes from `Local-first Habitica companion` to the signed-in character/account identity when user data loads.
- Refresh button is enabled when the app is idle and authenticated.
- Page data may load in stages; wait for `Refreshing N` and `Cloud syncing` status chips to settle before judging layout.

## Browser Automation Login Recipe

Use placeholders in notes and code. Never write real credential values into files.

Preferred locator sequence:

```js
const baseUrl = "https://habitica-companion.pages.dev";
const userId = "<provided-user-id>";
const apiToken = "<provided-api-token>";

await tab.goto(`${baseUrl}/sign-in`);
await tab.playwright.waitForLoadState({ state: "load", timeoutMs: 30000 });
await tab.playwright.waitForTimeout(3000);

const userInput = tab.playwright.locator('input[name="user-id"]');
const tokenInput = tab.playwright.locator('input[name="api-token"]');
const signInButton = tab.playwright.getByRole("button", { name: "Sign In", exact: true });

if (await userInput.count() !== 1 || await tokenInput.count() !== 1 || await signInButton.count() !== 1) {
  throw new Error("Sign-in controls not uniquely available.");
}

await userInput.fill(userId, { timeoutMs: 5000 });
await tokenInput.fill(apiToken, { timeoutMs: 5000 });
await signInButton.click({ timeoutMs: 5000 });

await tab.playwright.waitForTimeout(45000);
```

If `fill` or `type` fails with a browser clipboard/runtime error, use keypress entry:

```js
async function pressText(locator, text) {
  for (const ch of text) {
    await locator.press(ch, { timeoutMs: 5000 });
    await tab.playwright.waitForTimeout(8);
  }
}

await userInput.click({ timeoutMs: 5000 });
await userInput.press("ControlOrMeta+A", { timeoutMs: 5000 });
await userInput.press("Backspace", { timeoutMs: 5000 });
await pressText(userInput, userId);
await userInput.press("Tab", { timeoutMs: 5000 });

await tokenInput.click({ timeoutMs: 5000 });
await tokenInput.press("ControlOrMeta+A", { timeoutMs: 5000 });
await tokenInput.press("Backspace", { timeoutMs: 5000 });
await pressText(tokenInput, apiToken);
await tokenInput.press("Tab", { timeoutMs: 5000 });

await signInButton.click({ timeoutMs: 5000 });
await tab.playwright.waitForTimeout(45000);
```

Verify field state without leaking secrets:

```js
const loginState = await tab.playwright.evaluate(() => ({
  userLength: document.querySelector('input[name="user-id"]')?.value?.length ?? 0,
  tokenLength: document.querySelector('input[name="api-token"]')?.value?.length ?? 0,
  path: location.pathname
}), undefined, { timeoutMs: 5000 });
```

Only log lengths and path. Do not log real values.

Stop after these outcomes:

- Success: URL is `/dashboard`; continue authenticated review.
- Blocker: fields have non-zero lengths, but app still says `Habitica User ID and API Token are required.`; record sign-in binding bug and do only signed-out review.
- Auth failure: app shows a Habitica/authentication error; report it without printing credentials and ask user whether to retry.
- Network/runtime failure: record browser/runtime error class, retry once after reload, then stop if same failure repeats.

Browser automation tips:

- Prefer accessible locators: labels, roles, and `data-testid`.
- If direct fill/type fails in the in-app browser, try keypress-based entry and blur the field before submit.
- Confirm each locator count is exactly one before clicking or typing.
- After login, avoid dumping full-page snapshots that may include sensitive values.
- Do not take screenshots while credentials are visible in the sign-in fields.
- If using DOM snapshots after login, avoid full-page dumps of Settings or diagnostics sections that could include local private data.

## No-Mutation Rule

Unless the user explicitly asks for a mutation test, do not click controls that change Habitica or shared app state.

Safe interactions for UI review:

- navigation
- search fields
- sort/select filters
- local fold/details toggles
- theme selection and preview
- local appearance panel expand/collapse
- read-only links and panels
- viewport changes

Avoid these mutation controls:

- task `Complete`, `Uncomplete`, `Score +`, `Score -`
- spell cast buttons
- stat allocation or Start New Day / CRON actions
- equip gear, equip pet, equip mount
- hatch, feed, sell, buy potion, buy gems, armoire
- quest accept/reject/start/invite/vote/queue/remove/expire/skip/complete actions
- party role, officer, kick, invite-proof, and shared-queue management buttons
- Settings `Clear Local Data`, import, upload/download sync, export unless the user asks to test those flows
- Diagnostics optional gear check or any reversible/live check unless the user explicitly asks

## Pages To Review

Check these routes after login:

- `/dashboard`
- `/tasks`
- `/inventory`
- `/pets-mounts`
- `/party`
- `/quests`
- `/spells`
- `/settings`
- `/diagnostics`
- `/privacy`

Use direct route navigation for reliable coverage:

```js
const routes = [
  "/dashboard",
  "/tasks",
  "/inventory",
  "/pets-mounts",
  "/party",
  "/quests",
  "/spells",
  "/settings",
  "/diagnostics",
  "/privacy"
];

for (const route of routes) {
  await tab.goto(`${baseUrl}${route}`);
  await tab.playwright.waitForLoadState({ state: "load", timeoutMs: 30000 });
  await tab.playwright.waitForTimeout(3000);
  // Inspect layout, then expand safe foldables on this page.
}
```

After each navigation:

- confirm current URL/path
- wait for transient refresh/sync chips to settle when practical
- inspect top visible section first
- scroll through whole page
- expand safe foldables
- repeat quick overflow/control checks after expansion

Also check direct signed-out deep links when sign-in, empty states, or navigation changed:

- `/dashboard`
- `/tasks`
- `/inventory`
- `/pets-mounts`
- `/party`
- `/quests`
- `/spells`

## Foldable And Expandable UI

Open every non-mutating foldable area available in the current state.

High-priority foldables:

- Tasks: task group folds, completed visibility, task `Details`, task charts, rearrange mode only if no order mutation is performed.
- Party: member `Details`, active quest details/rewards, active quest participants, member filters/sort, CRON graph context.
- Quests: active quest details/participants, quest pool show/hide, queue sections, recently completed sections.
- Pets & Mounts: companion groups, bulk-sell planner panels, hatching/feed planner folds, collection filters/search.
- Equipment: preset panels, optimizer panels, equipment groups, details/action rows.
- Spells: CRON warning details, recommendation/details areas, target selectors.
- Settings: Appearance, Custom scheme, Advanced, cloud-sync section list.
- Diagnostics: filters, result groups, recent app messages, safe read-only sections.

Do not use foldables that only appear after clicking a mutation confirmation unless user explicitly asks.

Agent foldable discovery recipe:

- Take a fresh DOM snapshot after each page navigation.
- Search visible controls for labels containing `Details`, `Show`, `Hide`, `Expand`, `Advanced`, `Customize`, `Open`, or `Filters`.
- Click only controls that clearly reveal local/read-only content.
- Do not click controls whose labels imply Habitica/shared-state mutation: `Complete`, `Score`, `Cast`, `Equip`, `Buy`, `Sell`, `Feed`, `Hatch`, `Invite`, `Vote`, `Start`, `Accept`, `Reject`, `Remove`, `Expire`, `Skip`, `Assign`, `Kick`, `Upload`, `Download`, `Import`, `Clear`.
- When many repeated `Details` controls exist, click a small representative set from each card type or group, not every single card if the list is huge.
- Re-snapshot after expansion before checking alignment.

## Responsive Checks

Use at least these viewport sizes:

- Desktop: `1280x720`
- Tablet: `820x900`
- Mobile: `390x844`

For each key page and expanded section, check:

- no horizontal overflow
- controls do not overlap
- button/input heights align within rows
- card grids collapse cleanly
- action rows wrap without stair-step drift
- labels and values start at consistent x-positions
- long titles/descriptions wrap without pushing footer buttons into uneven positions
- sticky bars and topbar do not hide content

Reset the viewport when finished unless the user asks to keep a size.

## Theme Checks

Theme changes are local UI state. Use them for visual review when the user allows appearance testing.

Minimum built-in schemes to check:

- `Gryphy (Light)`
- `Gryphy (Dark)`
- one high-saturation dark scheme such as `Toxic Swamp`
- one warm/danger scheme such as `Boss Battle`

Check:

- sign-in hero text and feature chips
- app bar and disabled Refresh button
- card headings/body text
- primary/secondary buttons
- inputs/selects
- badges/chips/pills
- custom scheme editor and swatches

Do not save a generated custom scheme unless the user explicitly asks. Prefer selecting built-in presets or temporary preview states for review.

## Finding Notes

For each issue, capture:

- page/route
- viewport
- theme
- section or control
- actual behavior
- expected behavior
- reproduction steps
- whether it blocks further testing
- suggested implementation plan

When asked to add findings to `FUTURE.md`, add prioritized, self-contained entries under `Prioritized Next Changes`. Each entry should include:

- `Goal`
- `Source finding`
- `Touch`
- `Out of scope`
- `Implementation plan`
- `Acceptance`
- `Need to run build`
- `Need to run test(s)`

Do not add raw credentials, screenshots with tokens, or private user data to `FUTURE.md`.

## Final Report Shape

Keep final report short:

- what was checked
- what was blocked
- where findings were written
- whether build/tests were run
- exact build/test commands for user to run
- suggested commit message when repo files changed
