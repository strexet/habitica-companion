# UX/UI Manifest

Last reviewed: 2026-06-08

This manifest records the current UI implementation, what is working, where readability or responsiveness has drifted, and which outside patterns are worth copying. Treat it as product guidance for future UI work, not as a pixel spec.

## Product Posture

Habitica Tool is a dense companion app for power users. The UI should feel like an operational dashboard with clear game-state affordances, not like a marketing site or a generic CRUD admin panel.

Core principles:

- Keep mutation controls explicit and close to the state they change.
- Prefer readable summaries first, deeper detail second.
- Keep resource state visible when the user is making resource-spending decisions.
- Use cards for repeated records and bounded tools, not for every page section.
- Use responsive grids with `minmax(0, 1fr)` or `minmax(min(100%, ...), 1fr)` where content can contain long task names, gear names, quest names, or translated text.
- Never let a fixed-width action row own the layout when user data is variable.
- When official Habitica UI presents a game entity as artwork, the companion UI should reserve a real image/icon slot for that entity instead of substituting generic decoration.

## Agent Usage Rules

Before changing UI or UX:

1. Read the relevant page section in this manifest.
2. Inspect the affected Razor and CSS before choosing a design.
3. Preserve established shared primitives unless they are the source of the problem.
4. Check whether the change affects readability, interaction safety, responsiveness, or user-facing copy.
5. If the change creates or changes a reusable pattern, update this manifest in the same change set.

When reviewing UI changes:

- Look for overlap, clipping, hidden overflow, cramped action rows, text that cannot wrap, and controls that move unpredictably across breakpoints.
- Check long real-world Habitica data: translated task names, long gear names, long party names, unknown quest names, large numeric values, and disabled states.
- Prefer evidence from current code and known product workflows over generic UI taste.
- Use outside examples only to clarify a pattern. Do not copy another app if its workflow optimizes for a different job.

Responsive review targets:

- Narrow phone: 360px wide.
- Large phone: 390-430px wide.
- Tablet: 768px wide.
- Small desktop or split view: 1024px wide.
- Wide desktop: 1200px and above.

UI change completion checklist:

- Primary action remains visible or easy to reach.
- Disabled actions explain their cause when the cause is not obvious.
- Data freshness remains visible when stale data can make an action unsafe.
- Any progress indicator is determinate when the app knows total work.
- Touch targets and form controls are usable on phone widths.
- The page still works with long user-generated strings.
- The color palette does not rely on color alone to communicate state.
- Game images have reserved dimensions, alt text, readable fallbacks, and do not cause layout shift or overlap while loading.

## Color Schemes

Current pattern:

- Built-in schemes live in `ColorSchemeCatalog`, carry explicit light/dark metadata, and render as grouped picker sections: Default, Built-in Light, Built-in Dark, Custom, Generated.
- `Gryphy Light` and `Gryphy Dark` are the defaults. `Forest Legacy` preserves the original app palette. The other presets cover low-contrast, dark, bright, colorful, and intentionally chaotic moods with names that fit the Habitica companion context.
- The app applies semantic CSS variables such as background, card, surface, text, muted text, primary, accent, danger, success, focus, chart, task-value min/base/max, app header, navigation drawer, input, button text, and disabled-state tokens.
- Settings exposes a scheme picker and a custom scheme builder. Fully custom palettes are built with "Copy preset"/"Paste preset" — copy the readable preset, edit colors, optional gradients, text shadows, and the dark-theme flag in any text editor, paste it back — plus collapsible advanced gradient controls for direct edits. Avoid "JSON" in user-facing copy even though the clipboard format is JSON. Custom schemes are user data, not developer config.
- The picker is a shared `ColorSchemePanel` component reused by Settings and Sign-in (full) and the Dashboard (`Compact`). Compact mode is a single dense bar — select, small swatch strip, Random Preset, Random Theme — with create/copy/preset-editor/random-save controls collapsed behind a "Customize" disclosure that auto-expands when a save or edit flow is active; the disclosure toggle reads "Done" while a save/edit flow is open and closes the surface without reverting the current draft or generated theme. The Dashboard appearance section sits near the top of the page, collapsed by default behind a "Customize theme"/"Done" fold toggle. The Settings appearance section uses the same collapsed-by-default fold toggle but reveals the full panel. This follows the collapsible-disclosure and compact-swatch-row patterns and keeps the Dashboard section a small operational band rather than a second settings page.
- Random controls: Random Preset selects an existing scheme; Random Theme generates a transient in-memory theme applied without persisting, saveable into custom schemes with a name. A chaos slider (Calm to Madness) scales hue/saturation divergence before each reroll and relaxes readability guards as it approaches Madness. Calm and moderate output must keep body/card text plus primary/secondary filled-button labels readable across generated gradients; high chaos can be wilder, but button labels should not collapse into their backgrounds until the extreme end.
- `colorSchemes.js` keeps a localStorage active-scheme and preference fallback so mobile browsers can apply the scheme before Blazor and recover if IndexedDB is delayed during navigation. Optional multi-corner gradients are painted into tiny canvas images once per scheme switch; solid schemes keep the normal token fallback.

Rules:

- Add new app colors as semantic tokens first; avoid hard-coded one-off colors in Razor or CSS.
- Header, drawer, MudBlazor button variants, inputs, progress bars, disabled buttons, and nested panels must consume scheme variables. A palette is not done if only `body` and top-level cards change.
- Keep enough contrast between `background`, `card background`, `surface`, `strong surface`, `text`, and `muted text` in every built-in scheme. Dark schemes must not use pale surfaces with pale text.
- Sign-in hero content uses the app-bar background/text token pair even though the element also carries `card-surface`; the generic card-gradient override must not turn the hero into pale card text on light themes. Hero feature chips should stay shell chips, not accent-filled chips with inherited shell text.
- Preserve meaning across schemes: danger remains danger, success remains success, stale/conflict states keep text labels, and task-value colors must retain min/base/max progression without switching to unrelated semantic state colors.
- Task value backgrounds use `Task min`, `Task base`, and `Task max` as one same-hue ramp. Values near zero stay close to base; negative values move toward min; positive values move toward max. Do not implement this as a visible CSS gradient background or as separate red/green/orange status colors.
- Do not rely on color alone. Pair important state colors with copy, labels, icons, layout, or disabled reasons.
- Disabled controls must keep visible text and borders after a scheme switch; do not express disabled state only through opacity.
- Built-in scheme changes belong in `ColorSchemeCatalog`; user-created schemes belong in `preferences/colorSchemes`.
- Keep custom token names stable because saved user schemes and cloud sync depend on them. When adding tokens, backfill old custom schemes from `Alpha` before validating.

Inspiration:

- Material dynamic color guidance favors semantic design tokens over assigning raw values directly to UI elements.
- Modern palette systems keep semantic colors adjusted per palette so the visual tone changes without changing what each role means.

## Habitica Image Asset Placement

Current audit:

- The app currently uses local PWA/favicon files, Gryphy artwork on sign-in, and a MudBlazor menu icon in the shell.
- Dashboard, Inventory, Party, and Spells now render image-backed Habitica entities through `HabiticaImageAssetResolver` and the shared `HabiticaImage` component.
- Diagnostics, Live Tests, Tasks, Sign In, and App Shell remain intentionally text/control driven unless a specific Habitica game entity is being represented.

Asset parity rule:

- Use dedicated Habitica artwork when the official app presents the same entity visually: gear, quests, quest bosses or collection items, eggs, hatching potions, food, pets, mounts, spells, achievements, and reward items.
- Do not add generic fantasy icons as stand-ins for missing Habitica assets. Missing assets should render a fixed-size fallback with the item name or key and be tracked as follow-up work.
- Keep text labels beside or below images. Images improve recognition, but they must not replace names, costs, status, disabled reasons, or action labels.
- Resolve artwork through one app-level image asset resolver. Components should pass stable Habitica keys and receive source path, alt text, fallback label, image kind, and preferred size.
- Follow `HABITICA_API.md` for content key lookup and static asset sourcing. Do not guess image URLs inside Razor pages.

Layout rules:

- Use fixed image boxes for repeated records: 32px for dense inline chips, 40px for compact list rows, 48px for inventory and spell cards, and 64px only for prominent quest or companion panels.
- Set `object-fit: contain` and preserve pixel art with a shared image class. Avoid cropping item art to fill a decorative frame.
- Reserve image space before the file loads and keep the same dimensions for fallback state, loading state, and error state.
- In cards, place the image in a fixed first column and put text in a `min-width: 0` content column so long names wrap instead of pushing actions off-screen.
- In action-heavy cards, keep images in the header or identity zone. Never put thumbnails inside the primary action row when that row also contains buttons, counters, selects, or progress.
- Use `loading="lazy"` for below-the-fold repeated images. Critical current-state images, such as an active quest or current pet/mount, can load eagerly.
- At 360px width, images should shrink to the compact size or stack above text before labels, buttons, or stat pills overlap.

Current placement:

- Dashboard: Start New Day gear previews use official gear thumbnails. Companion and inventory summaries live on Inventory and Pets & Mounts, not on Dashboard. Pending quest damage remains text-only so it does not crowd Start New Day and stat allocation action rows.
- Dashboard navigation cards use stable local routes plus stable Habitica web URLs only; do not add mobile app deep links or custom schemes.
- Tasks: keep task cards primarily text and control driven. Use Habitica art only for explicit reward/item/quest targets added by a future feature. Task type/status affordances may use simple UI icons, not game art.
- Inventory: gear thumbnails appear in battle loadout slots, best-in-category entries, expanded gear cards, accessory cards, and saved preset items. Fixed identity columns keep slot labels, class text, stat pills, and equip actions aligned. Summary cards use compact item icons with readable counts.
- Party and Quests: member cards do not invent avatar art. On Quests, active quest cards use a prominent quest art slot near the quest title. Queue, pool, and recent quest records use smaller quest scroll thumbnails while owner, vote, invite, and cancel controls stay in their own rows.
- Spells: spell cards show class skill icons in the header beside spell name, mana cost, and availability. Equipment recommendation rows show gear thumbnails without moving cast controls or competing with mana previews.
- Diagnostics and Live Tests: avoid gameplay thumbnails in raw payload/debug views. If endpoint lists need icons, use small technical UI icons only, and never let images hide JSON or response metadata.
- Sign In and App Shell: keep existing local Gryphy/app icons. Do not add remote Habitica gameplay art to authentication or navigation unless it represents a specific game entity.

Responsive verification for image work:

- Check 360px, 390-430px, 768px, 1024px, and 1200px widths.
- Verify long gear names, translated quest names, unknown item keys, missing image files, and disabled action states.
- Confirm no image causes horizontal page overflow except where an existing intentional table scroll already exists.
- Confirm fallback text remains readable and does not overlap adjacent labels, pills, meters, or buttons.

## Current Implementation

### App Shell

Files: `src/Habitica.WebApp/Layout/MainLayout.razor`, `src/Habitica.WebApp/wwwroot/css/app.css`

Current pattern:

- MudBlazor app bar with drawer navigation for authenticated sessions.
- Top bar keeps identity, compact sync freshness, and Refresh/status action in one flex row; active page refresh replaces Refresh with a same-size `Syncing ...` chip.
- On narrow phones, the top bar remains a single compact row: the identity subtitle hides, the sync chip ellipsizes inside a capped slot, and Refresh/status stays inside the app-bar chrome instead of wrapping below it.
- Centered `.shell-content` with a `1200px` max width.
- Reusable `card-surface`, `ui-pill`, `section-label`, `panel-copy`, `field-row`, `checkbox-row`, `app-input`, and responsive grid classes.

What works:

- Navigation is predictable and app-like.
- Freshness/error status appears near the page content and in compact topbar form rather than hidden in settings.
- Shared typography and pill styles make status labels recognizable.

Drift:

- The visual language leans heavily on beige/teal/gold. It is coherent, but the app can become one-note across long pages.
- Many sections use card-like surfaces. Repeated records are appropriate as cards; top-level sections should remain plain bands or single surfaces.
- The top app bar consumes vertical space on small screens; sticky page controls must account for it.

### Sign In

Files: `src/Habitica.WebApp/Pages/SignIn.razor`

Current pattern:

- Two-column landing/sign-in layout.
- Trust strip, explicit token handling notes, optional saved credentials, and direct Habitica API settings link.

What works:

- The risk model is unusually clear for a third-party API-token app.
- Help text is close to the credential fields.
- Session-only sign-in is the default, which matches the privacy posture.

Drift:

- The hero copy is useful on first run, but returning users mostly need the form and saved-data path.
- On small devices, the help sections can push the primary form far down the page.

Improvement:

- If authenticated cached data exists, move "Open Saved Data" higher and reduce introductory copy.

### Dashboard

Files: `src/Habitica.WebApp/Pages/DashboardPage.razor`

Current pattern:

- Summary stat cards for account, HP, MP, XP, gold, and open tasks.
- HP, MP, and XP cards include compact meters so the current ratio has a readable shape, not only text.
- Start New Day panel appears only when the current-user snapshot says the Habitica day has not been processed. The action includes an optional gear recommendation preview, an expanded compact list for due unfinished Dailies, and inline confirmation instead of an immediate mutation.
- Stats allocation table with horizontal overflow.
- Explicit armoire and gem-for-gold actions plus companion navigation links.

What works:

- Stat cards are scannable and stable.
- Resource/progress meters make HP, MP, and XP easier to compare at a glance.
- Start New Day explains missed Dailies, quest progress, buff expiry, and optional gear auto-equip before calling Habitica Cron, matching the app's explicit-mutation posture.
- The stats allocation table preserves comparison columns, which is better than collapsing stat math into disconnected mobile cards.
- Pending stat allocation has clear apply/clear actions.

Drift:

- The table is readable on desktop but becomes a scroll task on phones.

Improvement:

- For mobile stats, keep horizontal scroll but add a sticky first column or repeated stat label so context does not disappear.
- Keep Start New Day as a small operational panel, not a hero or persistent global warning, because it is important only when Cron is due.
- Keep CRON gear optimization inside the Start New Day panel, with compact current/recommended/delta stat chips and recommended item rows rather than a separate inventory-style workspace.

### Tasks

Files: `src/Habitica.WebApp/Pages/TasksPage.razor`

Current pattern:

- Cached task groups with search, type filters, sort control, collapse controls, completed toggle, and compact task cards. Cards keep title, description, scoring/checkoff controls, disabled reasons, progress, and a Details toggle visible; status, metadata, and charts expand in place.
- Week/month/year task statistics with aggregate history and month-activity charts.
- Due dates render as readable local date labels such as Today, Tomorrow, Yesterday, or a local calendar date instead of UTC-style timestamps.
- Task mutation controls stay inline with the affected card without requiring detail expansion, show disabled reasons from freshness/auth state, and use visible progress for repeated Habit scoring. Their task-scoped action row wraps compact controls within the card at phone widths instead of stretching each button across the full card.
- Task details expand inside the card and show cached metadata without navigating away from the current scan position.

What works:

- Cached browsing state is clear.
- Group controls preserve context and reduce page length.
- Type filters and sorting are in the header toolbar, matching Todoist/Linear-style fast list refinement without introducing a separate filter page.
- Cards handle notes and metadata better than a narrow table would.
- Inline scoring follows the Spells page's safer multi-action pattern: count, explicit action, determinate progress, then refresh.
- Due dates are now easier to scan on task cards.

Drift:

- Completed/open state is clear, but task type and Habitica color/value meaning rely on surrounding group context.
- Task history now has aggregate and expanded-card charts, but advanced filters such as due-window and value-polarity filters are still future work.

Comparable apps:

- Todoist exposes task layouts such as list, board, and calendar, and treats filtering as a first-class task-view feature.
- Linear makes filters accessible from list and board views and lets users refine by issue properties.

Assessment:

- Our task cards are better for showing Habitica-specific value and notes in a local data browser.
- Todoist/Linear are better at fast filter composition and view switching.

Improvement:

- Add status, due-window, and value-polarity filters once the type/sort row proves stable.
- Add exact due timestamps as secondary detail only where precision matters.
- Keep task history charts compact, aggregate by default, and render per-task charts only inside expanded details.
- For drag-and-drop task ordering, make reordering available only where manual order is the active ordering model, or clearly explain why a visible sorted/filtered view cannot accept a drop at that position.
- Use an explicit drag handle, lift state, insertion marker, and invalid-drop feedback so task cards do not feel like they can be accidentally dragged while selecting text, expanding details, or pressing score controls.
- Keep keyboard and single-pointer reorder alternatives available; the current Tasks page drag handle supports arrow-key reordering, and each task card exposes compact move-to-top, move-up, move-down, and move-to-bottom buttons.
- Keep task-card reorder affordances hidden until the user enables the section-level Rearrange mode. Keep all four move buttons in one horizontal row.
- Preserve hidden/completed items when reordering the visible subset, and keep the dropped task in view with a brief inline confirmation.

### Inventory

Files: `src/Habitica.WebApp/Pages/InventoryPage.razor`

Current pattern:

- Summary cards, battle loadout, preset save/restore, best-in-category strip, collapsible other items, stat-bearing accessory groups, and folded cosmetic/no-stat groups.
- Equipment optimizer with goal selector, before/after stat deltas, and recommendation equip/save actions.
- Responsive `auto-fit` grids and `overflow-wrap` for long gear names.

What works:

- "Best in Category" gives users a sensible default path before exposing the full gear list.
- Presets map well to the power-user workflow.
- Responsive card grids and wrap rules are stronger here than on earlier spell-card layouts.

Drift:

- Repeated gear info appears in some cards, increasing visual noise.
- Preset saving uses a text input plus action button, which is clear but can feel detached from the current loadout state.
- Gear stat pills are dense; users may need stronger highlighting of what changes if they equip an item.

Improvement:

- Keep before/after stat deltas close to equip actions and optimizer recommendations.
- Avoid duplicated slot/class text inside the same gear card.
- Consider a compact comparison drawer for selected gear rather than expanding every item.

### Pets And Mounts

Files: `src/Habitica.WebApp/Pages/PetsMountsPage.razor`

Current pattern:

- Dedicated companion workspace with current pet/mount summary, collection search, foldable base/magic-potion/quest/premium/wacky/special groups, and a separate hatching-potion group.
- Large collection groups are folded by default except the base collection and hatching potions. Fold choices persist only in local browser storage; search temporarily expands groups to show matches.
- Owned pet and mount cards keep official Habitica art in fixed identity slots and place fast-equip controls beside the affected companion.
- Missing pet cards show cached-inventory hatch readiness or the specific missing egg and/or potion. Group hints summarize cached hatch availability without live price or gem-cost claims.
- Hatch planning uses per-pet queue cards before any mutation. Each row shows pet identity, ownership state, egg and hatching potion keys, available/reserved counts, planned consumption, local warnings, and a remove action; execution requires inline confirmation, runs queued hatches in order, and shows bottom block-local progress while active.
- Feed planning uses per-pet queue cards. Each card shows growth progress, selected normal food, available/reserved counts, expected progress after feeding, warnings, remove action, Transform to Mount, and separate Use Saddle confirmation. Bulk feed execution shows bottom block-local progress while active.
- Missing mount cards can add their corresponding owned growable pet to the same feed queue with Plan to grow. Unavailable missing mounts show a short cached-data reason instead of a dead action.
- Companion group headers keep bulk hatch and feed planning beside the fold toggle. `Add All to Hatching Queue` appends only valid missing pets with cached eggs and hatching potions, shows `No hatchable pets` when disabled, and leaves mutation execution inside the Hatch Planner controls. `Add All to Feeding Queue` appends only valid growable missing mounts from the currently visible group/filter set, shows `No growable mounts` when disabled, and leaves mutation execution inside the Feed Planner controls.
- Queue-add interactions preserve the clicked card or group header position when planner blocks above the collection expand. Scroll correction is immediate, local to add-to-queue actions, and not used for remove, clear, equip, fold, filter, or execution actions.
- Pet and mount sections have creature type filters that compose with search and expand matching folded groups. Filter labels use readable catalog names and keep the existing group organization.
- The bulk sell planner lives at the bottom with its existing keep-count preview and confirmation flow.

What works:

- Companion collection management no longer competes with gear optimization for space.
- Default folds keep large quest and premium catalogs usable on phone widths.
- Missing-state copy remains local and auditable because it comes only from the snapshot and checked-in catalog.

Improvement:

- Keep special-event companions visible through the fallback group when the checked-in catalog does not describe their hatch path.
- Keep release-pet and release-mount actions out of this page until a separately reviewed destructive flow exists.
- Keep saddles separate from normal food selectors. Use the saddle count/info block for unavailable saddle guidance until a purchase flow is separately reviewed.

### Party And Quests

Files: `src/Habitica.WebApp/Pages/PartyPage.razor`, `src/Habitica.WebApp/Pages/QuestsPage.razor`

Current pattern:

- Party owns one combined party-name-and-notes summary, a compact quest-summary link, member summaries, CRON rhythm/timeline visualization, and bottom-grouped roles, settings, and moderation.
- Quests owns active quest details, progress metrics, queue, pool search, voting, recent completions, and quest refresh actions. The quest pool starts expanded on this dedicated workspace and keeps an in-memory hide/show control for queue-focused scanning.
- Party-sync role strip and owner/app-admin settings stay near the bottom directly before moderation so administration does not interrupt member review.
- Owner/app-admin party-sync settings are a compact operational panel, and kick records stay last so moderation history does not interrupt member review.
- Officer assignment and kick/unkick controls live in expanded member details, close to the affected member identity.
- Member cards show subtle HP/MP chips near display name/class.
- Member sorting includes Low HP and Low MP modes; those sort current values ascending so the lowest member appears first, with unknown values last.

What works:

- The Quests page keeps quest progress, queue decisions, and scroll availability together without crowding Party member review.
- Quest progress distinguishes current, user pending, party pending, and estimated post-CRON state.
- Active quests show one participant count and keep participant names behind a compact in-memory drill-in; detailed accepted/pending/rejected response groups remain limited to invitations. Owner or starter and started date render when cached, with concise unavailable states otherwise. Description and rewards stay behind a compact details control. Finish estimates hide unknown finishing-member and timing-confidence fields when timing data is unavailable.
- The CRON rhythm visualization is a good domain-specific UI and should be preserved.
- Member rows use responsive grid fallback and word breaking for long names.
- HP/MP chips add useful party context without turning the member list into a stats table.
- Low HP/Low MP sorting supports quick support-target review.

Drift:

- The Quests page remains dense because active state, planning state, and history are intentionally visible together.
- Quest queue actions and read-only quest state share similar visual weight.
- Management controls add more density; keep them compact and avoid turning the page into a generic admin console.
- Some table-style member/stat areas still depend on horizontal scrolling.

Improvement:

- Separate "current quest state" and "planning queue" more strongly with tabs or a segmented switch if the dedicated Quests page still becomes difficult to scan.
- Promote only the next relevant party action; demote secondary refresh/open links.
- Keep the CRON visualization but add short labels for confidence and uncertainty near the chart.

### Spells

Files: `src/Habitica.WebApp/Pages/SpellsPage.razor`, `src/Habitica.WebApp/wwwroot/css/app.css`

Current pattern after the latest UI pass:

- Sticky current-mana bar above the spell cards, showing available MP, max MP, and class while scrolling.
- Spell cards with stable summary, cost/availability pills, count/target input zone, mana spent/available/after-cast preview, auto-equip toggle, cast button, progress bars, card-local quest/stat context, effect preview, and equipment recommendations.
- Cron-sensitive stat buffs show an inline warning inside the spell card when the user has not started the current Habitica day. The warning offers Cancel, Cast anyway, and Start New Day and Cast, plus local per-day suppression and a collapsed due-Daily mini-list disclosure.
- Responsive two-zone layout: variable user inputs on the left, mana/action status on the right; stacks at narrower widths.

What works:

- Available mana is visible on each spell card while evaluating a cast.
- The sticky mana bar keeps current MP visible while comparing far-apart spell cards.
- Mana spent and after-cast value provide before/after feedback before the user commits.
- Boss quest progress and party pending damage stay inside spell cards that can affect boss damage instead of a top-page quest summary.
- Unspent stat points appear only on stat-sensitive spell cards when allocation is unlocked.
- Unaffordable spell counts show a local reason in the mana preview instead of relying only on a disabled Cast button.
- Determinate progress bars match the known cast/equip counts.
- Auto-equip remains close to Cast without stealing space from target selection.
- The buff timing warning is close to the Cast decision and does not block unrelated spell cards.
- The layout avoids the prior overlap caused by placing count, target, total mana, auto-equip, and Cast in one fragile row.

Drift:

- Spell cards are still information-heavy. The page is good for precise bulk casting, but less close to Habitica's original fast "skill drawer" interaction.
- Target selection is explicit and safe, but less spatial than selecting a task directly from a task list.
- Equipment recommendations increase confidence, but they lengthen every spell card.

Comparable apps and games:

- Habitica's web flow puts skills in a drawer on the Tasks page; task-targeting skills are selected first, then applied to a task. Habitica mobile shows skill name, description, and mana cost, and greys out unaffordable skills.
- Habitica confirms skill use and mana deduction after applying a skill.
- RPG UIs such as World of Warcraft make the resource bar persistent and show casting/progress near the action; this makes resource state and action state hard to miss.

Assessment:

- Our implementation is better for planning because it supports count, target choice, effect estimates, auto-equip, and after-cast mana before mutation.
- Habitica's official UI is better for lightweight direct manipulation because it keeps skills close to tasks and uses task highlighting.
- RPG resource bars are better for immediate combat readability, but our denser operational layout is appropriate because this app optimizes deliberate batch actions rather than real-time play.

Improvement:

- Add a compact mode that shows one-line spell summaries with expandable details.
- Consider an optional task-picker drawer that reuses task cards for spatial target selection.
- Consider a future party-wide buff coordination surface that combines party CRON rhythm with current buff state, instead of making each spell card carry all coordination context.

### Diagnostics and Live Tests

Files: `src/Habitica.WebApp/Pages/LiveTestsPage.razor`

Current pattern:

- Safe checks, guarded reversible gear check, quick account reads, recent app messages, filters, JSON preview, copy/download actions.

What works:

- Risky checks require explicit acknowledgement.
- The diagnostics console is sticky on wider screens and filterable.
- Request counts make network cost visible.

Drift:

- The button cluster in the diagnostics console can become visually busy.
- JSON preview is useful for developers but heavy for normal users.

Improvement:

- Keep JSON available but collapse it behind "Details" by default after a successful check.
- Split app-message actions into an overflow menu or secondary row.

### Settings

Files: `src/Habitica.WebApp/Pages/SettingsPage.razor`

Current pattern:

- Action cards for sign out, checks, backup, restore, private sync, and clear local data.
- Import conflict warning with merge, keep-local, use-remote, and section-by-section choices.
- Per-section cloud sync status rows for succeeded, failed, skipped, excluded, and conflicting sections.

What works:

- Sensitive actions are grouped under local data and sync.
- Backup copy explains that credentials are excluded.
- Import conflict flow prevents silent overwrite.
- Section-level sync status keeps encrypted sync feedback next to Upload and Download instead of relying on a global page banner.

Drift:

- Clear local data is visually marked as danger, but it sits in the same grid pattern as safe actions.
- Upload/download sync actions have equal visual weight, though their risks differ by context.

Improvement:

- Give destructive actions a second confirmation step or separate danger zone.

## Cross-App Reference Patterns

### Status and Progress

Apple's Human Interface Guidelines frame feedback as a way to show current status, success/failure, warnings, and next steps. They also recommend putting status feedback near the thing it describes when possible.

Android/Material progress guidance separates determinate progress, which shows exact completion, from indeterminate progress, which only signals that work is ongoing.

Application rule:

- Use determinate progress when the app knows `completed` and `total`, as it does for spell casting, equipment slot changes, Pets & Mounts feed/hatch queues, and multi-step diagnostics.
- Use passive inline status for freshness, sync, mana, and cached data.
- Reserve interrupting warnings for destructive local data actions, credential handling, and irreversible Habitica mutations.
- Keep cached data interactive during background refresh and cloud sync. Disable only the action that would conflict with the active operation.
- Page-level refresh indicators belong in the app bar and the page's refresh strip. Use them for manual refresh, sign-in background refresh, and visible-domain loading.
- Card-level refresh indicators belong inside the card whose data is stale or being refreshed, such as Dashboard pending damage when tasks or party data are refreshing.
- Field-level status belongs beside the specific value or control when a mutation affects one item, such as equipment slot progress, spell count progress, cloud sync section status, or import-conflict choices.
- Global busy states are reserved for blocking mutations and first-load surfaces with no usable cached data.
- Use loading skeletons only when delayed content has a stable final structure and no cached data to show. Background refreshes should use compact status chips rather than skeleton flashes.
- If a visible value changes after a background update, a subtle changed-value animation may be used only with a reduced-motion fallback.

### Resource-Spending Actions

Habitica skills require MP and target either a task, the player, or the party. Official Habitica flows show skill cost, grey out unaffordable skills, apply immediately after target selection, and confirm the result.

Application rule:

- Keep MP visible on every spell decision surface.
- Show cost, availability, and after-cast state before mutation.
- Disable unaffordable actions and explain why near the disabled control.
- Preserve Habitica's terminology in user-facing text: use "skills" when describing the game concept, but `spell` remains acceptable in code.

### Dense Productivity Lists

Todoist, Linear, Notion, and Trello all converge on the same pattern for dense work data: list/board/card surfaces plus filters, sorts, visible properties, and saved/custom views.

Application rule:

- For tasks, inventory, and party queues, improve filtering before adding more visual decoration.
- Expose only the metadata that helps the current decision; move secondary metadata into details, toggles, or collapsed sections.
- Prefer saved view state for power-user workflows, as Tasks already does with folded/completed preferences.

### Drag And Drop Reordering

Best-in-class productivity apps treat drag-and-drop reordering as a manual-order affordance, not a replacement for sorting rules. Todoist allows manual drag order in projects, but sorted Today/filter views constrain reordering by due time, priority, and manual-sort mode. Linear separates display options from manual ordering, and makes manual order a deliberate view setting that updates shared order. Material list guidance supports manual tile reordering within lists, but keeps primary actions on the tile and supplemental actions consistently placed. Apple HIG recommends clear destination feedback, drop placeholders, auto-scroll in long containers, undo when possible, and visible failure feedback for invalid drops. Accessible implementations such as WAI-ARIA APG and React Aria show that drag-and-drop needs keyboard, screen reader, and single-pointer alternatives.

Application rule:

- Make manual order explicit: if a list is sorted by name, value, due date, status, or another computed field, a drag should either be disabled with clear copy or limited to positions that preserve the active ordering rule.
- Prefer a small drag handle over whole-card dragging when cards contain buttons, expandable details, selectable text, links, or score controls.
- During drag, show a lifted card preview, reserve original space or show a placeholder, and show a full-width insertion marker between valid targets.
- Invalid destinations should show "not allowed" feedback and leave order unchanged; failed drops should restore the item and show a concise inline error.
- Long task lists need edge auto-scroll during drag and must keep the focused/dropped item visible after reorder.
- Pointer/touch drag is not enough. Provide keyboard and single-pointer alternatives such as focusable reorder handles, Move up/down commands, or a compact reorder mode. Announce moved item position through a live region where practical; the Tasks page uses a focusable drag handle with arrow-key movement and a polite live region.
- Preserve filtered-out and collapsed items when reordering a visible subset. Reorder only within the current task group unless the UI explicitly supports cross-group moves and explains the property change.
- Do not persist every hover position. Persist only on committed drop; debounce storage/cloud writes if a future implementation syncs order remotely.

References:

- Todoist manual and constrained task ordering: https://www.todoist.com/help/articles/default-sorting-order-in-todoist-mqmgerY7
- Linear display options and manual ordering: https://linear.app/docs/display-options
- Linear drag between groups: https://linear.app/changelog/2023-04-27-improved-drag-and-drop
- Material list gestures and reordering: https://m1.material.io/components/lists.html
- Apple drag-and-drop feedback: https://developer.apple.com/design/human-interface-guidelines/drag-and-drop
- WAI-ARIA rearrangeable listbox keyboard pattern: https://www.w3.org/WAI/ARIA/apg/patterns/listbox/examples/listbox-rearrangeable/
- React Aria accessible drag-and-drop model: https://react-aria.adobe.com/dnd

## Review Findings

### Good Patterns to Keep

- Freshness banners are visible on data-dependent pages.
- Explicit mutation gates are consistent: buttons disable when data is stale, unauthenticated, or busy.
- Summary stat cards work well for dashboard, party, inventory, and spells.
- Dashboard resource meters and party member HP/MP chips improve scanability without adding large new panels.
- Responsive grid primitives are already strong in inventory and party areas.
- Guarded diagnostics and session-only sign-in match the app's safety model.
- Spell-page mana preview is now aligned with resource-spending UX best practice.

### Readability Drift

- Several pages use uppercase labels, muted notes, pills, cards, and panels at the same time. This can flatten hierarchy.
- Long pages often start with explanatory copy that is useful once but less useful on repeat visits.
- Technical values leak into user display in remaining developer-oriented areas, especially raw-ish diagnostic previews.
- Some controls are word-heavy where a compact segmented control, menu, or icon button would scan better.

### Responsiveness Drift

- Horizontal tables preserve data comparison, but mobile users need sticky labels or clearer scroll affordances.
- Any row that mixes fixed controls with variable user data is a future overlap risk.
- Sticky elements need explicit offsets for the top app bar.
- Button rows should stack earlier than content starts to compress.

### Interaction Drift

- The app is strong at safe mutation but sometimes weak at "what changed" feedback.
- Inventory can equip, but spells now show a stronger before/after preview and disabled reason.
- Settings and diagnostics have multiple same-weight actions where the primary next action should be clearer.

## Prioritized Improvements

1. Add disabled-action reason text for inventory and dashboard allocation.
2. Add task filters for type, status, due window, and value polarity.
3. Split party quest state and queue planning into clearer modes.
4. Add a settings danger zone with confirmation for destructive actions.
5. Reduce repeated hero/help copy for returning authenticated users.
6. Introduce a compact spell-card mode after the current stable card layout has been tested.
7. Add sticky first-column or label context for mobile stat tables.

## Source Notes

- Habitica Skills: https://habitica.fandom.com/wiki/Skills
- Habitica FAQ on mana: https://habitica.fandom.com/wiki/FAQ
- Habitica Android skills behavior: https://habitica.fandom.com/wiki/Mobile_App_for_Android%3A_Habitica
- Apple HIG Feedback: https://developer.apple.com/design/human-interface-guidelines/feedback
- Android progress indicators: https://developer.android.com/develop/ui/compose/components/progress
- MUI responsive UI summary: https://mui.com/material-ui/guides/responsive-ui/
- World of Warcraft cast-time/resource behavior: https://warcraft.wiki.gg/wiki/Cast_time
- Todoist view customization: https://www.todoist.com/en/help/articles/customize-views-in-todoist-AoHhBxFdZ
- Linear filters: https://linear.app/docs/filters
- Notion views, filters, and sorts: https://www.notion.com/help/views-filters-and-sorts
- Trello views: https://support.atlassian.com/trello/docs/trello-views/
