# UX/UI Manifest

The [adopted navigation study](navigation-study.md) defines the local design baseline for new canonical workspace UI: Today, Inbox, Plan, Projects and More, with global title-first capture and advanced details on demand. Existing production routes described below remain the implementation truth until a corresponding unit is delivered. Owner selection on 2026-10-19 permits development implementation and exposure before E01 hands-on qualification; broader release retains the separate gate. Required local recovery/accessibility checks and real-device qualification remain distinct. No human usability result or authenticated competitor test is claimed.

Last reviewed: 2026-06-09

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

The repository rule in `AGENTS.md` requires both a readable light scheme and the dark `obsidian-glow` scheme for every changed UI route, at phone and desktop widths with normal and enlarged text. Keep primary actions and focus/disabled states visibly distinguishable in both variants; record a skipped scheme as unqualified rather than inferring parity.

Task pages must distinguish saved canonical data, stored Habitica identity and confirmed game effects. The shared task panel reports incomplete correspondence across the selected workspace, including completed and archived tasks. Missing group tags remain explicit. The coverage message performs no automatic repair or connection activation; the normal task flow still exposes the tracked integration defects.

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
- Secondary filled-button text uses the painted gradient stops when present, rather than the separate accent color. The production release matrix caught and corrected unreadable secondary labels in `obsidian-glow` on Settings and Spells; flat-color buttons retain their existing contrast correction.

Inspiration:

- Material dynamic color guidance favors semantic design tokens over assigning raw values directly to UI elements.
- Modern palette systems keep semantic colors adjusted per palette so the visual tone changes without changing what each role means.

## Habitica Image Asset Placement

Current audit:

- The app currently uses local PWA/favicon files, Gryphy artwork on sign-in, and a MudBlazor menu icon in the shell.
- Dashboard, Equipment, Party, and Spells now render image-backed Habitica entities through `HabiticaImageAssetResolver` and the shared `HabiticaImage` component.
- Diagnostics, Tasks, Sign In, and App Shell remain intentionally text/control driven unless a specific Habitica game entity is being represented.

Asset parity rule:

- Use dedicated Habitica artwork when the official app presents the same entity visually: gear, quests, quest bosses or collection items, eggs, hatching potions, food, pets, mounts, spells, achievements, and reward items.
- Do not add generic fantasy icons as stand-ins for missing Habitica assets. Missing assets should render a fixed-size fallback with the item name or key and be tracked as follow-up work.
- Keep text labels beside or below images. Images improve recognition, but they must not replace names, costs, status, disabled reasons, or action labels.
- Resolve artwork through one app-level image asset resolver. Components should pass stable Habitica keys and receive source path, alt text, fallback label, image kind, and preferred size.
- Follow `skill-tree/documents/habitica-api.md` for content key lookup and static asset sourcing. Do not guess image URLs inside Razor pages.

Layout rules:

- Use fixed image boxes for repeated records: 32px for dense inline chips, 40px for compact list rows, 48px for inventory and spell cards, and 64px only for prominent quest or companion panels.
- Set `object-fit: contain` and preserve pixel art with a shared image class. Avoid cropping item art to fill a decorative frame.
- Reserve image space before the file loads and keep the same dimensions for fallback state, loading state, and error state.
- In cards, place the image in a fixed first column and put text in a `min-width: 0` content column so long names wrap instead of pushing actions off-screen.
- In action-heavy cards, keep images in the header or identity zone. Never put thumbnails inside the primary action row when that row also contains buttons, counters, selects, or progress.
- Use `loading="lazy"` for below-the-fold repeated images. Critical current-state images, such as an active quest or current pet/mount, can load eagerly.
- At 360px width, images should shrink to the compact size or stack above text before labels, buttons, or stat pills overlap.

Current placement:

- Dashboard: Start New Day gear previews use official gear thumbnails. Companion and inventory summaries live on Equipment and Pets & Mounts, not on Dashboard. CRON damage and Health Potion recovery remain compact inside the Start New Day panel so they do not crowd stat allocation action rows or duplicate cards elsewhere on the page.
- Dashboard navigation cards use stable local routes plus stable Habitica web URLs only; do not add mobile app deep links or custom schemes. `Open Habitica` stays in the Companion links header on desktop and moves to the bottom of that section on phone widths so the top of the Dashboard remains compact.
- Tasks: keep task cards primarily text and control driven. Use Habitica art only for explicit reward/item/quest targets added by a future feature. Task type/status affordances may use simple UI icons, not game art.
- Equipment: gear thumbnails appear in battle loadout slots, best-in-category entries, expanded gear cards, accessory cards, and saved preset items. Fixed identity columns keep slot labels, class text, stat pills, and equip actions aligned. Summary cards use compact item icons with readable counts.
- Party and Quests: member cards do not invent avatar art. On Quests, active quest cards use a prominent quest art slot near the quest title. Queue, pool, and recent quest records use smaller quest scroll thumbnails while owner, vote, invite, and cancel controls stay in their own rows.
- Spells: spell cards show class skill icons in the header beside spell name, mana cost, and availability. Equipment recommendation rows show gear thumbnails without moving cast controls or competing with mana previews.
- Diagnostics: avoid gameplay thumbnails in raw payload/debug views. If endpoint lists need icons, use small technical UI icons only, and never let images hide JSON or response metadata.
- Sign In and App Shell: keep existing local Gryphy/app icons. Do not add remote Habitica gameplay art to authentication or navigation unless it represents a specific game entity.

Responsive verification for image work:

- Check 360px, 390-430px, 768px, 1024px, and 1200px widths.
- Verify long gear names, translated quest names, unknown item keys, missing image files, and disabled action states.
- Confirm no image causes horizontal page overflow except where an existing intentional table scroll already exists.
- Confirm fallback text remains readable and does not overlap adjacent labels, pills, meters, or buttons.

## Current Implementation

### Canonical task filters and saved lists

The Companion editor uses labelled search, task type, native state, urgency and active Todo stage controls above a bounded list. Filters combine through the canonical query engine. An incompatible type/state clears and disables the stage filter with an explanation. All-task reset and Previous/Next controls remain visible; results announce matching counts and the displayed range, including empty results.

Saved-list selection evaluates the retained definition against the loaded workspace and shows the source, predicates and exact fixed dates. Fixed dates never acquire a Today label. Named lists preserve dependency eligibility and do not copy tasks. Missing, archived or invalid definitions show unavailable with a reset/reload action rather than unrelated fallback results. The compact task surface reads definitions; /todo/lists supplies reviewed authoring.

Workspace changes clear filters; revision/query changes return to the first twenty records. Pending command recovery disables list controls and keeps the original preview. Labels and long names wrap in responsive grids, native keyboard controls retain visible focus and list/form targets have a minimum forty-four-pixel height. Component and local layout checks remain separate from actual phone keyboard, assistive technology and human usability qualification.

### App Shell

Files: `src/Habitica.WebApp/Layout/MainLayout.razor`, `src/Habitica.WebApp/wwwroot/css/app.css`

Current pattern:

- MudBlazor app bar with drawer navigation for authenticated sessions.
- The signed-out header exposes a visible `Sign in` link to `/sign-in`, including Settings and cached-data pages. The sign-in action occupies the existing header action slot.
- The authenticated top bar keeps identity, compact sync freshness, and Refresh/status action in one flex row; active page refresh replaces Refresh with a same-size `Syncing ...` chip.
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
- Full-width expandable iPhone installation guidance below the sign-in panels, with explicit separate-storage and session-only caveats. Installation requests no notification permission.
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
- Start New Day panel appears only when the current-user snapshot says the Habitica day has not been processed. The action includes an optional gear recommendation preview, compact CRON damage summary, contextual Health Potion recovery, confirmed due unfinished-Dailies list, collapsed estimate details, and inline confirmation instead of an immediate mutation.
- Stats allocation table with horizontal overflow.
- Explicit armoire and gem-for-gold actions plus companion navigation links. The external `Open Habitica` action stays in the Companion links header on desktop and moves below the local companion cards on phone widths.

What works:

- Stat cards are scannable and stable.
- Resource/progress meters make HP, MP, and XP easier to compare at a glance.
- Start New Day explains missed Dailies, quest progress, buff expiry, estimated incoming damage, and optional gear auto-equip before calling Habitica Cron, matching the app's explicit-mutation posture.
- The stats allocation table preserves comparison columns, which is better than collapsing stat math into disconnected mobile cards.
- Pending stat allocation has clear apply/clear actions.

Drift:

- The table is readable on desktop but becomes a scroll task on phones.

Improvement:

- For mobile stats, keep horizontal scroll but add a sticky first column or repeated stat label so context does not disappear.
- Keep Start New Day as a small operational panel, not a hero or persistent global warning, because it is important only when Cron is due.
- Keep CRON gear optimization inside the Start New Day panel, with compact current/recommended/delta stat chips and recommended item rows rather than a separate inventory-style workspace.
- Keep CRON damage and recovery inside the Start New Day panel. The default damage view should show total estimated damage, HP after CRON, risk or confidence, and compact Dailies/Boss breakdown, using the active theme's surface, border, text, warning, danger, and muted tokens. Formulas, missing due-state Dailies, unavailable checklist/stat inputs, and unavailable sources belong in collapsed details. Health Potion recovery should stay secondary and row-like, showing current HP, HP after CRON, 15 HP healing, 25 GP cost, gold, concise recommendation text, and a single-purchase action only when recovery is useful.

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

### Equipment

Files: `src/Habitica.WebApp/Pages/InventoryPage.razor`

Current pattern:

- Summary cards, battle loadout, preset save/restore, best-in-category strip, collapsible other items, stat-bearing accessory groups, and folded cosmetic/no-stat groups.
- Equipment optimizer with goal selector, before/after stat deltas, and recommendation equip/save actions. Intelligence is the initial goal across classes and the invalid-input fallback. Explicit alternate goals survive page refresh notifications while mounted; equipment changes require the separate equip action.
- Responsive `auto-fit` grids bound minimum tracks by the available width. Inventory headers wrap, gear identity text uses shrinkable tracks and stat pills remain within the identity column. Mobile preset inputs use an automatic flex basis to preserve input height in stacked rows. Long gear names wrap within recommendation cards.

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
- Member status includes local display-name search near class and sort controls; no-match search states stay clearable without leaving the section.
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
- Spell cards with stable summary, cost/availability pills, count/target input zone, mana spent/available/after-cast preview, auto-equip toggle, Spend All Mana, cast button, card-local cancel/progress, card-local quest/stat context, effect preview, and equipment recommendations.
- Cron-sensitive stat buffs show an inline warning inside the spell card when the user has not started the current Habitica day. The warning offers Cancel, Cast anyway, and Start New Day and Cast, plus local per-day suppression and a collapsed due-Daily mini-list disclosure.
- Blessing previews keep raw per-member healing as the primary line, show multi-cast totals per member instead of party-wide aggregate HP, and place limited-value or low-need healing warnings close to Cast without disabling normal casting.
- Responsive two-zone layout: variable user inputs on the left, mana/action status on the right; stacks at narrower widths. Spell card/status tracks use zero minimum sizing, narrow spell tracks remain shrinkable, and mana rows wrap when text expands. Cast, Spend All Mana and recommendation controls remain inside visible cards at200-percent text.

What works:

- Available mana is visible on each spell card while evaluating a cast.
- The sticky mana bar keeps current MP visible while comparing far-apart spell cards.
- Mana spent and after-cast value provide before/after feedback before the user commits.
- Spend All Mana updates the existing count and previews without casting, keeping max-spend planning reversible.
- Boss quest progress and party pending damage stay inside spell cards that can affect boss damage instead of a top-page quest summary.
- Unspent stat points appear only on stat-sensitive spell cards when allocation is unlocked.
- Unaffordable spell counts show a local reason in the mana preview instead of relying only on a disabled Cast button.
- Low-value Blessing warnings help avoid waste while preserving the user's ability to cast.
- Determinate progress bars match the known cast/equip counts.
- The one-second Preparing state gives a narrow cancellation window before the first Habitica request.
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

### Diagnostics

Files: `src/Habitica.WebApp/Pages/LiveTestsPage.razor`

Current pattern:

- Safe checks, guarded reversible gear check, quick account reads and compact filterable recent app messages with per-entry Details and copy/download feedback.
- Diagnostic timestamps use local short date and time formatting inside the Razor expression.

What works:

- Risky checks require explicit acknowledgement.
- The diagnostics console is sticky on wider screens and filterable.
- Request counts make network cost visible.

Drift:

- The button cluster in the diagnostics console can become visually busy.
- Quick account previews retain technical payloads for diagnostic use.

Improvement:

- Keep detailed app-message JSON collapsed behind native Details controls.
- Split app-message actions into an overflow menu or secondary row.

### Settings

Files: `src/Habitica.WebApp/Pages/SettingsPage.razor`

Current pattern:

- Action cards for sign out, checks, backup, restore, private sync, and clear local data.
- Expandable iPhone installation guidance remains available without sign-in. Open Checks uses outlined secondary navigation for readable labels in Obsidian and light schemes; the existing /live-tests route remains unchanged.
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
- Card-level refresh indicators belong inside the card whose data is stale or being refreshed, such as the Dashboard Start New Day CRON summary when tasks or party data are refreshing.
- Field-level status belongs beside the specific value or control when a mutation affects one item, such as equipment slot progress, spell count progress, cloud sync section status, or import-conflict choices.
- Global busy states are reserved for blocking mutations and first-load surfaces with no usable cached data.
- Use loading skeletons only when delayed content has a stable final structure and no cached data to show. Background refreshes should use compact status chips rather than skeleton flashes.
- If a visible value changes after a background update, a subtle changed-value animation may be used only with a reduced-motion fallback.

### Resource-Spending Actions

Habitica skills require MP and target either a task, the player, or the party. Official Habitica flows show skill cost, grey out unaffordable skills, apply immediately after target selection, and confirm the result.

Application rule:

- Keep MP visible on every spell decision surface.
- Show cost, availability, and after-cast state before mutation.
- Keep spend-all controls non-mutating; they may change only local count/preview state.
- For sequential spell runs, show a local preparation/progress surface and put Cancel on the active card only.
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
- Give each reordered task group a compact `Reset to Habitica` action only when that group has live custom order data. Reset must sit near reorder controls, affect only that group, and require no confirmation because the user can create a fresh order by rearranging again.

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
- Summary stat cards work well for dashboard, party, Equipment, and spells.
- Dashboard resource meters and party member HP/MP chips improve scanability without adding large new panels.
- Responsive grid primitives are already strong in Equipment and party areas.
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
- Equipment can equip, but spells now show a stronger before/after preview and disabled reason.
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

## Optional ChatGPT Linking

Settings displays a separate optional connection card with server credential-storage disclosure, code expiry, audit metadata limits and a Privacy link. Disabled configuration displays an unavailable message. Available configuration requires sign-in and a consent checkbox before enrollment. Active connection controls generate a linking code or open explicit disconnect/delete confirmation. The connected card uses a short Codex/ChatGPT handoff list and a selectable one-time-code card with a copy action, local expiry and manual-copy fallback. The guide distinguishes the browser authorization form from MCP settings and chat and explains fresh-code recovery after expiry or lost browser binding. Busy state disables duplicate requests; logout removes tab management state.

The standalone Auth page shares Companion light/dark visual language through self-contained CSS, identifies the requesting client, return host and Habitica account, and displays every supported permission in grouped rows. Code verification sits next to the code field; the full read-only permission preview follows the code form. After verification, app-requested permissions start checked, other permissions start unchecked and required profile identity stays selected. Concise permission titles keep consequence text visible beside each checkbox; selected-row shading follows the actual checked state while App requested and Available badges show the client's original request. Lost browser binding, invalid/expired code and temporary failure render a branded restart path without exposing untrusted request data. The Auth page follows browser color preference because browser-local Companion palettes do not cross origins. CSS adapts at narrow widths and uses visible keyboard focus. Approval or denial returns to the validated client callback; the form policy includes the validated callback origin without bypassing single-use cookie checks. Existing sign-in remains browser-only. The [integration contract](companion-first-mcp-research.md#proposed-chatgpt-enrollment-privacy-and-retention-contract) owns consent and lifecycle requirements.

## Workspace privacy and portable data controls

Settings separates cloud workspace consent from ChatGPT linking and encrypted private sync. The disclosure names server-readable task notes and history, encrypted server credential storage, retained data after disconnect and independently enabled background execution. An unavailable deployment shows an explicit unavailable state with no connection action. Cloud export, disconnect and deletion require an authenticated tab session; deletion has a confirmation step.

Local canonical workspace backups have separate listing, export and confirmed restore controls. Restore preserves existing data on conflicts and does not upload to the cloud. Global browser clearing includes local canonical records. Workspace and legacy backup copy identifies the distinct formats and readable content. Privacy repeats the separate workspace, credential, browser and Habitica deletion boundaries.

## Companion task editor and command review

The Tasks page exposes the canonical editor through an explicit disclosure button. Browser and cloud sources remain visible, with independent workspace selection, a read-only explanation for frozen or stale copies and separate paused-execution status for restored copies. The existing Habitica task dashboard remains separate. Account changes clear the editor's pending content and discard late data.

The editor creates Todos, explicitly scheduled Dailies and Habits. The workspace header separates listed tasks from archived records; completed unarchived tasks remain listed. Selected unlinked tasks can be archived through a reviewed command; the review states that history remains in exports and Habitica is unchanged. New Daily/Habit creation carries required game intent; unsupported projection controls remain absent for archiving.

Every save opens a field-level review before confirmation. Native urgency and game difficulty have separate labels. Checklist replacement requires an explicit checkbox; empty replacement clears the list. Completion and Habit-score previews show the affected occurrence or bounded event count and explain possible game effects. Unknown acceptance preserves the original command ID and replaces ordinary cancellation with receipt checking or an original-command retry. Successful canonical saving and projection progress use separate text labels and a bounded recent-history disclosure.

Existing Habitica links require a separate comparison of canonical and external records, explicit identity entry and fresh approval. Linking retains native task fields. Forms use shared input/button primitives, flexible action rows and wrapping task/history text. The fieldset, task list and preview permit narrow layouts without fixed-width user content.

The authenticated workspace restore control uses the shared file-input styling and a shrinking field wrapper. Native file controls must not impose a page-wide grid minimum. The real-Razor synthetic Settings fixture fits the 360-pixel phone viewport after the field repair; the fixture distinguishes layout measurement from component interaction checks.

## Authenticated planning settings

Settings separates authenticated shared planning from the legacy queue and from cloud task storage. The disclosure names readable quest selections, contributor labels, votes and receipts, plus retention until removal. Consent is enabled only after loading a verified party and selecting the explicit acknowledgement; the request binds the displayed party ID. Leader permission controls and whole-namespace deletion appear only for a verified leader view. Server checks remain authoritative.

Own contribution export/removal remains available after party departure through management authentication. Destructive actions show concrete effects before confirmation, including deletion of votes on removed owned quests. Failed or changed-state actions clear cached management state and require a reload. Account changes clear displayed party facts and discard late responses. The synthetic real-Razor settings fixture qualifies narrow wrapping separately from component interaction tests.

## Explicit integration management controls

The connected Settings card separates status, ChatGPT grant revocation, tab management-credential replacement, credential revalidation and full disconnect/deletion. Status names the account, permissions, last verification, repair state and last ChatGPT authorization. Missing usage is explicit; a truncated permission list is marked incomplete. Confirmations describe retained credentials and separate Workspace access. Grant revocation clears displayed linking codes and permission status. Lost management rotation requires revalidation; forgotten tabs discard late results. Controls wrap in the existing action rows on narrow screens. Existing default disabled integration behavior remains explicit.

## Saved workspace work status

Connected workspace Settings provides a manual saved-work status button. The timestamped result distinguishes pending, waiting over fifteen minutes, uncertain, blocked and expired execution leases; the oldest pending task-command time is optional. The result explains paused uncertainty and absence of retries. No polling, task text or command identifiers appear. Busy state prevents duplicate checks. Failed or throttled reads show unavailable status rather than zero counts. Account/workspace changes hide stored results and discard late responses. Existing source-disabled configuration retains the unavailable state. Component interaction checks and a real-Razor narrow browser fixture qualify local controls independently of hosted/device validation.

## Narrow and enlarged cloud controls

Settings shells/cards use shrinking grid tracks, and nested Settings grids cap minimum tracks at the available width. Card copy and button labels wrap long text without hiding overflow. Canonical command fields cap intrinsic widths; native date controls use narrow padding and a shrinking fieldset. Real Razor fixtures cover 320/390/1024-pixel widths and 200-percent root text at 320 pixels across command review, integration management, workspace renewal/status and party controls. Browser measurements distinguish fitting controls from actual mobile-device and assistive-technology acceptance.


## Ordinary user and agent test controls

Normal users see existing spell, party and manual Start New Day controls without an agent-test banner or test-only disable reason. Class/unlock, mana, selected target, fresh data, permission and confirmation checks remain visible. All supported spell target kinds remain available in an explicit agent profile. Agent test mode displays the separate party-management/Start New Day restriction; spell controls do not inherit that restriction. Equipment controls remain available for owned gear. Party departure is prohibited in every profile, independently of UI state.

## Canonical lifecycle preview labels

The existing command preview names scheduled occurrence skip, Companion pause and Companion resume explicitly. Occurrence dates and separate zero game-effect state remain visible. Visible recurring row actions open the local native schedule/occurrence/lifecycle editor; hosted and physical-device qualification remain separate. Application controller and cloud adapters retain original command identity and skipped-record counts without presenting local pause as a paused Habitica schedule.

## Native placement API availability

The command layer and optional MCP expose reviewed native planned-date and category/project/section/label placement. The basic canonical editor still has no placement controls. A future UI must label planned date separately from deadline, expose explicit clearing and full label replacement, and review exact assignments before acceptance. Native placement leaves game effects and lifecycle state unchanged. Existing E01 and production UI qualification requirements remain active.


## Native organization authorization disclosure

The authorization page presents native category/project/section/label/goal edits as a separate selectable permission. The disclosure states that archival retains related tasks and history and produces no game effects. Existing task-write permission does not authorize organization edits. Local Projects and Saved lists controls require exact reviewed intent and preserve original receipt recovery after uncertain responses. Hosted and device qualification remains separate.

Cloud workspace storage disclosure includes saved task-view definitions. Saved-view read errors report unavailable data without claiming a task change. The dedicated local Saved lists page presents complete fixed filters and exact review; broader Todo navigation qualification remains separate.

## Appearance sync feedback and draft recovery

Saved theme imports update the mounted picker, semantic CSS and reload cache. Unsaved editor fields and generated previews remain local. A sync conflict presents explicit actions to apply the synced appearance or retain the draft for continued editing. Saving a retained draft requires the conflict choice first. Cancelling an editor restores the latest saved appearance without uploading a preview or changing selection time.

Appearance saves report local persistence independently from encrypted-cloud availability. Cloud failures remain visible near the theme controls and in existing sync diagnostics. Task filters, page folds and disclosure state remain device-local; missing tab management access does not establish server disconnection.


## Unknown connection state and explicit recovery

A fresh authenticated Settings tab displays unknown server connection state until explicit verification. Missing tab management access never labels server enrollment disconnected or background execution disabled. Integration recovery and workspace recovery retain separate disclosures and unchecked consent. Copy identifies encrypted server storage and possible profile creation. Intentional linking-code generation remains distinct from restoring management access.

Workspace recovery and manual status refresh reread actual executor availability and opt-in. Unknown status hides execution mutations; verified paused state explains retained pending commands and separate enablement. Verification failures clear earlier success displays. Account replacement clears consent, status and pending actions; responses owned by a previous account cannot repopulate the page. Existing wrapping action rows, shared controls and theme tokens remain in use. Synthetic Razor/browser frames qualify narrow and desktop layout separately from real device and hosted authorization checks.


## Settings session action rows

Related session cards use heading, flexible description and action grid rows. Bottom actions align within a wider shared row; descriptions have no fixed height. Narrow cards stack through the existing adaptive Settings grid. Session actions retain native button/link keyboard semantics, shared focus/theme tokens and a 44-pixel minimum target. Scope remains limited to settings-session-actions rather than every action-card.

## Changed-page qualification contract

Every new or materially changed page requires synthetic interaction assertions and actual rendered evidence. Route/state coverage names forms, navigation, inline confirmations or modal dialogs, tables, charts, logs, long content and disabled/error/loading states as applicable. Missing categories receive explicit explanations. The shared browser matrix checks the documented six widths, enlarged text, normal themes, deterministic calm/moderate output, primary target dimensions and sampled composited text contrast. Keyboard reachability and visible focus require an observed journey. Seeded overlap/overflow and unreadable-text examples qualify the detector. Extreme chaos and real-device/assistive-technology results remain separate.


## Shared readable foregrounds and primary geometry

Colored labels, statuses, links, outlined actions and muted copy consume derived on-surface foreground tokens. Qualifying authored colors remain intact; insufficient colors move toward readable ink across common card/body/input surfaces and sampled theme stops. Semantic fills and borders retain original colors. Trust messages on sign-in use card surfaces and ink rather than shell foregrounds. Default-size filled actions and navigation links have the shared 44-pixel minimum; long labels wrap. Tasks root tracks and stat columns shrink for enlarged narrow layouts without hiding overflow.


## Diagnostic message freshness and private detail

Recent app messages read bounded local history on page entry. An unavailable local read retains the previous view and states the refresh limitation. Opening Diagnostics creates no Habitica refresh or gameplay request. Message summaries use fixed operation/result text; copied and downloaded details preserve safe context and counters while excluding task text, account identifiers and credentials. Command receipt lookup remains the source for accepted tasks and game effects; diagnostics cannot imply success from missing logs.


### Compact diagnostic rows

Recent app messages use a bounded scrolling list with single row separators, no per-message frame and no movement on hover. Each row shows local timestamp, textual severity, operation, safe summary and feature/source. Click, tap, Enter or Space copies the sanitized entry; visible selection and a polite status message report the result. Details remains a separate native disclosure with selectable wrapped metadata and JSON for inspection or manual copy after clipboard refusal. The copy target retains the shared primary minimum height, and inset focus remains visible inside the scroller. Filters and batch copy/download remain above the list.


### Explicit agent diagnostic sharing

The collapsed Share diagnostics with Codex or ChatGPT disclosure keeps optional export guidance outside the normal log-reading path. Preview diagnostic bundle creates a bounded filtered snapshot in a selectable wrapping preview. Copy reviewed bundle and Download reviewed bundle appear only after preview. Guidance states the explicit attachment step, local-browser scope and separate server receipts. Filter or session-state changes remove the preview and export actions. Copy/download status uses the existing polite message region and fixed recoverable errors.

## Direct task capture and primary rows

TodoTaskPanel places title and Add together before task rows; Enter in the title submits the native form for review. Primary row Edit, Complete and Habit score actions stay outside disclosure sections. Long notes remain behind a native Notes disclosure; rows wrap and stack at phone widths. More details contains advanced fields, separate planned-date review and explicit archival. A loaded workspace keeps source controls collapsed; unavailable/unselected sources expose the source chooser and reason. Reviews and receipts appear above the editor and preserve a wrapping original-command reference. Review focus moves to the review heading; confirmation/cancellation requests focus back on the title. New Dailies require an explicit supported schedule; completion requires an explicit occurrence date. Shared semantic colors, focus treatment and 44-pixel primary targets apply. The complete Todo sidebar and Today/category navigation remain separate planned composition work.

## Today day groups

Today places the civil-day input, a device-date shortcut and Add task before a compact workspace/review panel. Ordinary scheduled and planned rows stay outside disclosures. Each row labels planned date, deadline, schedule zone and retained occurrence state as applicable; duplicate date reasons share one row. Scheduled Dailies, already recorded occurrences and inactive work have separate headings. Empty data and unavailable evaluation remain distinct. Revision-bound pagination uses visible Previous/Next day rows controls. Native date input and the shared review heading retain keyboard focus; a closed successful/cancelled review returns focus to the day input. All rows use the shared responsive grid and semantic theme tokens.


## Inbox and accessible planning agenda

Inbox and Plan expose a labelled native view selector, reference-day input and visible Add task link. Upcoming, Week and Month use chronological agenda rows and Previous/Next period buttons, with explicit inclusive range labels and Monday week boundaries. A manual date input provides keyboard navigation without dragging or a timed calendar. Inbox, Anytime and Overdue explain membership; overdue rows distinguish earlier planning from missed deadlines. Plan / move stays visible beside each row.

The placement editor opens above results and receives heading focus. Planned day remains separate from deadline and recurrence. Existing category/project/section choices use bounded native selectors with visible page controls; selected off-page values remain visible. Labels use an optional disclosure with explicit add/remove controls. Review moves focus to the shared command heading; confirmation/cancellation returns focus to the placement heading. A stale editor retains the draft and offers explicit reload. Close returns focus to the view selector. Rows and placement fields share semantic colors, wrapping layouts and primary touch targets across themes.


## Retained task activity

Task history keeps activity type and retained-text search visible at the top. More filters contains labelled UTC-day bounds and the Habit direction filter; active filters keep the disclosure open. Rows show event kind, recorded UTC time, optional Daily civil date, historical/current detail provenance and archival flags in text. Notes and original event/task/command references use native disclosures with wrapping content. A supported Restore Todo action stays visible beside the event; unsupported rows explain the specific reason.

Restoration opens the shared exact-command review and moves focus to the review heading. Confirmation or cancellation returns focus to retained-text search. Original uncertain acceptance remains visible with receipt recovery and blocks another restoration. Shared semantic tokens, responsive rows and primary touch targets apply across themes.

## Removed project presentation

Removed projects stay out of active placement choices. A retained assignment displays Unassigned with a removed-project reference notice; selected metadata retains the exact ID until explicit reassignment. Inbox includes otherwise eligible unscheduled Todos whose former project is removed. Logical removal never completes, moves or hides descendants through a cascading task change. The Projects manager exposes the reviewed native lifecycle with paged reference acknowledgment for removal.

## Bounded descendant task navigation

Tasks and subtasks presents roots or direct children with explicit Expand subtasks buttons. A focused branch exposes Collapse to parent and Expand entire branch; the complete branch remains paged. Rows show parent context, independent project assignment, status and optional notes/dates. Exact task and parent IDs remain in a native References disclosure; move choices and concrete reviews retain visible IDs. Deep levels use a level label without shrinking the content area. Branch actions wrap below task content so enlarged text cannot consume the text column. Archived parents remain navigable anchors with disabled write controls. Project task lists select one project, then preserve clearly labelled cross-project branch context.

Edit, Add subtask, Move and Detach remain visible row actions. Move uses a native paged parent selector with IDs for repeated names and retained off-page selection. Create subtask first reviews an independent Todo and then an exact parent attachment; saved and attached states remain visibly separate. Failed attachment retains the saved Todo and offers explicit retention as independent work. Navigating away before attachment keeps the task independent. No drag gesture is required. Native forms, focusable headings, wrapping text, semantic theme colors and shared primary target sizes apply. Typing does not move focus to a heading.

## Optional project and task organization

Projects places optional metadata management before project task navigation. The kind selector switches flat categories, projects, sections, labels and goals. Create/edit fields remain focused on the selected kind; project category is optional, section project is immutable after creation, and goal membership uses paged Todo choices with the full selected set. Goal progress reports eligible completion counts and explicit unavailable progress for an empty set. Duplicate names show references when selection needs disambiguation. Exact IDs remain visible in concrete native reviews; ordinary row references stay in native disclosures.

Project removal lists twenty affected references per page and requires acknowledgment of every page before fresh confirmation. Archive/removal consequences distinguish metadata visibility from retained tasks/history. Native reviews, original uncertainty/recovery and the selected source remain visible. Pending native work disables fresh task writes; failed refresh requires reloading. Task rows keep Plan / assign project and Organize visible. Workflow state, active stage and dependency replacement use separate reviews. Dormant stage copy explains inactive-state precedence. Native forms and list/keyboard controls supply the complete workflow without dragging or a board. Actions stack below content across readable themes and enlarged text.

## Daily schedule and recurring lifecycle editor

Daily rows expose Schedule / occurrences; Habit rows expose Pause / resume without Daily recurrence fields. Opening the editor moves keyboard focus to the heading. Typing preserves focus. Native labeled fields present only applicable weekly or monthly choices. Full schedule replacement, clearing, exact occurrence completion/skip and pause/resume have separate explicit reviews. New Dailies stay unscheduled until a saved-task schedule is reviewed.

Loaded schedule and task state remain visible. Exact date readiness uses the loaded rule, shows the named rule timezone and distinguishes retained records, unscheduled work, unavailable timezone and inactive state. The schedule-zone current-day shortcut does not use the device timezone; unscheduled work has an explicitly labeled device-date shortcut. Retained history is paged and disclaims historical timezone provenance. Stale context disables fresh changes and offers explicit reload. Pending and uncertain operations retain original recovery controls above the editor. A retained game mapping explains the native-editor limit and points to the separate main completion/effect review. Companion pause copy explicitly disclaims pausing Habitica penalties. Shared theme colors, wrapping content, native keyboard controls and primary target dimensions apply.


## Saved-list editor and current results

The dedicated /todo/lists page has one primary heading, a compact explicit workspace/source selector and labelled full-definition fields. Direct task capture links to Saved lists. Results remain a selection of existing tasks, with separate definition and matching-task pagination. Empty matches, unavailable reads and archived definitions have distinct copy. Mapping state never establishes successful game effects. Fixed dates retain explicit fixed labels and never become relative Today filters.

Changing to Unblocked active Todos clears and disables the redundant type/state fields with an explanation. Stage compatibility and exact organization references validate before review. The complete resulting query appears in the saved-list review and in cross-page Projects recovery. Name-only updates preserve prior filters; clearing filters is explicit. Unknown outcomes retain original recovery controls and prevent replacement edits. Confirmed saves retain the original receipt when refresh fails.

Source/account changes clear private editing and selection; revised data requires explicit editor reload before replacement. Opening a definition, editing and reviewing request focus at the relevant heading. Long names, exact references and action rows wrap using shared theme tokens and forty-four-pixel primary targets. Expanded workspace controls use wrapping grid tracks so enlarged text cannot force the source picker across adjacent buttons. Synthetic responsive and keyboard observations remain separate from physical-device and assistive-technology qualification.

## Todo correspondence review

Normal Todo preparations show required game intent even when an older optional projection control supplied false. The concrete review and receipt disclose pending effects, explicit cloud execution prerequisites and confirmed-effect counts. Browser-only capture can save queued intent offline without credentials being uploaded. A saved/pending Todo must not be described as already present or rewarded in Habitica. Capture control simplification and full linking workflows remain separate Ready units; the correspondence boundary does not qualify the deferred UI changes.

## Habit game-intent review

Habit creation review shows Positive (+) and Negative (−) as Enabled/Disabled alongside independent native urgency and game details. Required pending game intent and executor prerequisites remain visible before confirmation. Known disabled scoring directions render disabled primary row actions; backend checks also refuse the corresponding score. Legacy native archive fixtures remain distinct from newly projected Habit capture. No creation-time score or automatic execution follows from the review.


## Explicit Daily creation schedule

New Daily capture shows frequency, interval, required start date, fixed timezone and applicable weekday/month-date inputs directly in the form. The initial start date is blank so no schedule is silently selected. Weekly and monthly controls retain draft values when switching frequency. Missing or unsupported rules show an error and prepare no command. The advanced Companion task panel uses the same fields.

Review displays the complete recurrence and requested Habitica creation. The review states that fresh account timezone/day-start verification and separate executor access are required, that creation runs no Cron or historical scoring, and that future missed Dailies may have normal penalties. Fixed zones use UTC or Etc/GMT identifiers; UTC+7 is Etc/GMT-7. Account mismatch blocks projection rather than converting dates. Native historical Daily editing remains separate from linked schedule authoring.


## Task linking and rejected preparation

Tasks and retained History events expose a Habitica link action outside disclosure sections. The link editor names the exact current Companion task and separates counterpart creation/verification from existing-record adoption. Historical/archived tasks and existing Dailies do not offer automatic new-copy creation. An explicit cloud prerequisite replaces unavailable browser adoption controls. Review compares identities and states; optional details expose both notes and schedule/date differences without claiming synchronization.

Definitively rejected preparation offers Return to editing and Cancel rejected request while retaining the draft. Uncertain preparation offers original-preview recovery and no cancellation. Uncertain command acceptance retains original receipt controls. Uncertain adoption keeps the original comparison, enables Recover original link receipt and disables cancellation and source/type switching. The advanced task panel keeps recovery outside disabled editing fields. A confirmed link with failed refresh requires reload before another edit. Link editor headings receive focus; known-preview cancellation returns to normal task controls.


## Required game intent and visible readiness

Normal capture and the advanced task panel omit optional Habitica projection checkboxes. Creation, field edits and activity request required correspondence by default. Readiness text appears after primary capture, preserving title/Add placement; compact task review surfaces also display source/execution prerequisites. Browser capture waits for separate cloud consent and execution. Unverified status, unavailable execution, paused execution and a verified enabled observation remain distinct. No observation alone confirms a game effect.

New Habit capture offers positive/negative direction checkboxes in advanced game details; both default enabled and the review prints exact values. Native urgency and game difficulty retain independent fields. Incompatible Todo-only deadlines and Habit checklists are not offered for unrelated kinds. Activity review identifies the exact affected task and warns when counterpart creation/recovery precedes scoring. Explicit native schedule-only/lifecycle controls retain separate wording and never imply pausing Habitica penalties. Pending and unknown receipts remain unconfirmed; accepted canonical completion is not displayed as confirmed rewards.


## Tag coverage data boundary

Todo coverage now evaluates stored tag identities only against the signed-in account and keeps recorded membership confirmation separate. A missing/mismatched expected account, stale desired metadata or unknown result cannot display verified tag coverage. Existing missing-group counts include pending/review identities. The model milestone changes no visual layout or navigation. Dedicated tag review/recovery controls and full membership derivation remain the following tasks; stored identity never proves fresh upstream state or game rewards.


## Tag permission disclosure and observation recovery

The standalone Auth approval page presents tag access separately from native organization/task permissions. The disclosure explains the additional Companion consent, reviewed native tag metadata, preserved unrelated tags and absence of automatic tag deletion or gameplay permission. Workspace identity reports validated scope, but the following tag UI milestone owns account consent controls and canonical tag review/recovery entrypoints.

A recovered metadata observation must be described as an original send with uncertain outcome and a matching current state. Approval records the reviewed observed state without repeating the write; missing identity, changed names, lost unrelated membership or expired/revoked review cannot be presented as confirmed original execution. The secure standalone Auth page retains browser-native colors rather than reading PWA theme preferences across origins. Font sizing follows relative units so increased root text size affects the disclosure and controls. Actual host/device authorization remains separate from synthetic page qualification.

## Tag reconciliation review boundary

Native organization and task previews can return tagProjection with the full changed group selection, removedTaskIds, archive retention and separate consent/scope readiness. Agent instructions require presenting the tag metadata with the original canonical preview. Native saved state and tag metadata confirmation remain separate from gameplay rewards. Missing authority or a changed review context preserves native capture with an explicit tag review gap.

The backend milestone adds no browser controls or visual pages. The next tag UI milestone must expose separate consent, complete review, adoption and original unknown-result recovery before the backend is published. Existing portable coverage remains an audit of stored evidence; an older source revision can require review without scheduling another automatic write.


## Group tag consent and recovery

Settings and Todo Sync expose an unverified-per-tab consent state with explicit Check tag consent and Check tag coverage actions. Enabling tag metadata requires a separate disclosure checkbox; agent permission and background execution remain independent. Group rows show identity, membership and missing/retained task coverage. Exact native IDs distinguish duplicate-name candidates; candidate selection opens a review without acceptance.

Membership previews expose complete twenty-reference pages and require every page before confirmation. Existing task/organization previews use the shared tag metadata review component. Review headings receive keyboard focus. Unknown acceptance retains the original ID and blocks replacement while offering original receipt lookup and explicit original acceptance retry. A proven terminal-unaccepted original displays the closed identity and permits a fresh complete review. Missing receipts, prepared records, generic refusals and unavailable evidence keep the uncertainty fence. Original native unknown work offers a separate observation review. Buttons and long references use shared theme/layout primitives; normal schemes require readable contrast and primary targets under the common matrix.


## Checklist conversion review contract

Conversion backend previews expose full source descriptions, explicit active/completedArchived mapping for every item, exact selected target snapshots, new capture count and complete tag metadata intent. Duplicate names require exact selection. Deferred historical matches remain visible gaps. Reviewed canonical capture, stored correspondence and actual native verification use separate labels. Missing receipt never means rejection; repeat only the original request/command identity. Projects and checklist Todos now open /todo/convert. The browser uses explicit per-item lifecycle/action choices, paginated exact target candidates and full source/target descriptions. Each ten-item review page requires acknowledgement; separate tag changes retain separate complete-page review. Review headings receive focus. Source status pages distinguish placement, stored counterpart coverage and original pending commands, including historical gaps. Navigation preserves the original review, and unknown acceptance offers original receipt lookup or explicit same-command retry. Current/frozen source descriptions remain visible after edits; accepted conversions refuse unsupported amendments. Source retirement opens a separate exact-impact page; conversion approval alone never deletes the source. Shared input/button/layout/theme primitives retain long-content wrapping and normal-scheme contrast.


## Converted source cleanup review

The conversion status links to /todo/retire/{ConversionId:guid}. The cleanup page shows exact source/native IDs, full source description, project, verified replacement progress and complete ten-item review pages. Every page requires acknowledgement, followed by a separate irreversible-deletion checkbox. The primary destructive button remains disabled until ready, unexpired and fully reviewed. Already-absent source archival uses a distinct label. Review headings receive focus; shared controls and surfaces inherit the active scheme.

Unknown dispatch preserves the original command across navigation, disables another deletion and exposes original receipt lookup. Explicit observed-absence reconciliation requires a separate uncertainty acknowledgement. Confirmed outcomes show originalReceipt, alreadyAbsent or observedAbsentAfterUnknown and retain the source/history audit. Canonical refresh follows confirmed cleanup without losing the receipt. An original-ID recovery field supports page reload. Account/workspace changes discard private drafts and late responses. Source retirement history is not presented as an ordinary source edit; unrelated source amendments retain the existing refusal. No hosted or physical-device qualification follows from synthetic rendering.


## Bulk-spell result presentation

Spells renders the last spell operation in a persistent status surface above the cards. The result shows acknowledged/requested counts, readable failed stage, independent gear cleanup and snapshot refresh state, uncertainty and observed cooldown. An unknown dispatched cast explicitly prohibits replaying the batch. Completed casts with failed followups use a warning, preserving cast acknowledgement. The result surface supplies no retry control. Active progress names preparation, equipment, casting and refresh stages; cancellation remains on the active card. Shared theme surfaces and typography apply to the outcome, including enlarged text and long operation IDs. Private sync warnings cannot replace the displayed cast counts.

### Spell recovery surface

The Spells equipment recovery section exposes persisted work through an authenticated check. Exact original/temporary slots and unresolved status remain visible together. Restore original gear and Keep current gear have separate meanings; later selections require explicit supersession. Controls disable during active work. Recovery never offers a spell replay. Long equipment identifiers wrap within existing theme-aware card surfaces.


## Canonical alias presentation boundary

Projects shows optional alias input during creation. Existing-project alias authoring uses a separate form with the exact selected name/ID, a normalized preview and explicit blank-to-clear review. Name/category drafts never join an alias-only change. Name-marker cleanup requires a separate explicit name review after alias acceptance; a marker such as `<codev>` supplies no alias semantics.

Project options show escaped names, alias labels and disambiguating IDs. Selected off-page or archived references retain visible alias and lifecycle context. Shared placement, project navigation, saved-view filters and conversion destinations keep exact GUID option values. Continuous alias text wraps inside the picker. Agent shorthand resolves to an exact canonical project before reviewed commands; ambiguity requires explicit selection. Clearing a retained alias changes no archive/removal lifecycle or task placement.


## Agent deadline wording

An agent deadline review states the exact ISO date and a readable local interpretation. Relative dates also state the observed local anchor date and timezone. Missing material date/zone context, ambiguous `dd`/`дд` usage, numeric order and uncertain leap dates remain explicit questions before preparation. Existing-task identity, ordinary preview approval and original receipt recovery remain visible. The stored deadline never implies a planned date, reminder or originating-chat notification. Browser date inputs remain exact-date controls; packaged guidance owns natural-language interpretation.

### Connector version reporting

Agent-facing compatibility results distinguish backend identity from a client-reported package. Unknown installation remains explicit. Mixed or unavailable Workers request operator review without reconnecting, changing consent or replaying account actions. This local preparation adds no browser page or production publication.

### Package update guidance

Version answers identify the evidence layer: loaded package descriptor, backend compatibility response or directly observed host metadata. Missing descriptors remain unknown. Package guidance distinguishes MCP metadata refresh, imported-package replacement and local marketplace cache updates. A fresh conversation qualifies discovery rather than proving installation. No version check changes consent, credentials, tasks or installation state.

### Notes foundation boundaries

The Notes engine exposes explicit accepted, duplicate, stale, quota and unknown-acceptance outcomes. Editor controls must preserve the distinction and original command ID. Trash remains recoverable until explicit purge. The dedicated Notes page now composes the service; existing task-note controls remain separate. OAuth consent identifies personal Notes reads and writes separately from task access, with explicit server-readable content and retained-version disclosure. Cloud Notes connection requires separate notes-v1 consent; ordinary task consent grants no Notes access.

### Notes project context

The Notes editor shows exact current project identity and lifecycle, with a canonical project picker and an explicit Unassigned action. Project/source/Workspace references wrap within the panel. Save reviews association changes before acceptance; the review preserves original before/after identities and disables content edits until cancellation or acceptance. Stale links remain visible and detachable. Project choices require the matching loaded Workspace; source changes remain explicit.

The library combines text, trash and all/project/unassigned filters before twenty-row pagination. Retained filters preserve exact source and Workspace. /todo/projects exposes a separate Notes section for the exact project selected in Project tasks. Opening the section reads only separately authorized Notes; mismatched sources and missing Notes consent expose no private title, snippet or editor. New note selects the exact eligible project; All notes permits explicit attachment through the same reviewed editor. Hiding the section preserves the private scoped draft. Dirty-navigation, import/export, history, conflict and original-receipt controls remain available. No task or Habitica effect follows project Notes navigation.

### Agent Notes interactions

Explicit unambiguous Notes capture/update intent permits acceptance of a matching server preview without another ordinary permission question. Ambiguous note selection, unresolved conflicting facts and unrequested permanent deletion require clarification. The response identifies actual saved content or changes after exact-version/hash verification. Receipt acceptance, later current content and unavailable retained versions remain distinct. Notes text never acts as authorization. No automatic chat memory capture, task creation or Habitica scoring follows a Notes read/save.


### Notes page interaction

Notes remains a top-level destination, separate from Todo descriptions. Source, New note, search and the title/Markdown editor are direct controls. On phones the library and editor stack; New note and selection focus the editor. Browser-only and cloud/agent-readable content are labelled explicitly. Cloud consent names server readability, encrypted credential storage, retained versions and separate deletion. No source switch uploads local data.

Unsaved changes use an inline keep/export/discard choice. A conflicting stored revision keeps the private draft and pauses replacement until explicit reload. Safe preview uses shared surfaces and readable ink; tables and fenced code can scroll locally. Raw HTML stays text and remote images never load. Trash/restore reviews name the exact record; purge additionally acknowledges irreversible deletion of all retained versions. Original command uncertainty pauses fresh writes and exposes receipt lookup. Navigation away retains the scoped draft; account/session clearing removes the draft. Source and cleanup controls preserve account/notebook ownership. Synthetic rendered coverage and physical-device limits are recorded in testing.md.

### Dedicated statistics presentation

Statistics moves aggregate charts off the initial task action screen. The dedicated destination exposes period, UTC reference day, kind and current-title search directly; Companion project filtering names current membership. Sections distinguish canonical activity, currently confirmed game effects and independent cached Habitica data. Current project/goal progress does not claim historical as-of denominators. Graphs expose visible numeric values, labelled dates, focusable horizontal scrolling and descriptive source/coverage text. Missing data has an unavailable state; empty retained history does not imply no activity. Per-task detail charts remain optional within the existing task card. Expanded metadata grids shrink under enlarged text, task-value surface labels use readable ink and primary navigation targets retain forty-four-pixel height.
### Todo Sync management

Todo Sync presents source selection, retained command state and exact original receipt lookup before server settings. Pending, blocked and unknown command counts stay distinct from executor enabled, paused or unverified status. The source panel and history use the same scoped selection as task lists; browsing the route never opts into a cloud connection or sends a task effect. Long command IDs wrap, and history rows stack on narrow phones. General Settings links to Todo Sync while retaining private device sync and theme preferences. Browser-clear review names separate Notes and workspace exports; integration profile deletion names retained cloud Notes. Receipt lookup remains a read, and absence never claims an action failed.

### Todo category flow

Todo remains open in the drawer; no disclosure control hides Today, Habits, Dailies or Tasks. Direct category tabs show the current destination with `aria-current=page`. Habits, Dailies and Tasks present a title field and an immediate action before advanced details. Today places the selected date and exact due Daily actions beside a direct Add task link. Projects, Statistics, Sync, Inbox, Planning, Saved lists, History and the legacy Habitica task/Reward surface remain named destinations. Task source remains shared across routes; a compact correspondence status explains missing reward links without moving consent or executor controls onto the task form. Theme colors use the existing semantic ink, surface, primary and button-text tokens, and selected tabs retain the shared focus color.

At 360px with normal text, four category tabs stay on one row; at enlarged text they wrap into readable rows without overlapping labels. The selected workspace is a short disclosure summary; Tasks keeps detailed correspondence status in a separate disclosure, with its incomplete stored-link count visible in the summary. More details and source controls remain available without taking space from the first task action. On the first phone screen at normal text, Habits exposes both score directions and Tasks exposes title capture plus the first Complete action. Secondary project and reference links follow the task controls. The native Habitica task link and original receipt remain accessible through Todo Sync.

## Web Push interaction foundation

The registered browser adapter adds no visible control yet. Future device/reminder UI must prepare capability before enabling the direct click action, preserve independent reminder consent, show installation-required/unsupported/denied/dismissed states, and never repeat a permission prompt automatically. Reuse the same-key browser subscription; expose key-change recovery explicitly. Revoke the exact server device before local unsubscribe and keep uncertain cleanup visible. An outside-app permission change must reconcile to the exact reviewed server target. Task-neutral lock-screen copy, authenticated same-origin clicks and all light/dark/mobile/enlarged-text route qualification remain required in the dependent reminder UI task.

## Voluntary review editor

Organize on /todo/tasks includes optional review cues and manual stale flags. The editor labels exact UTC input and the chosen UTC/IANA display zone; unknown zones and non-UTC/date-only inputs refuse preparation. Separate set, snooze, clear and mark-reviewed actions never change task state, urgency, deadlines, plans or game effects. Mark reviewed consumes the review cue and retains the manual stale flag. Retained history distinguishes every action and supports revision-bound paging. The shared concrete preview states exact task ID/action/instants/zone; unknown acceptance recovers the original command, and stale account/source data disables editing. Review cues do not request notification permission or establish a mandatory review routine.

## Today reminder cues

Today exposes separate Enable in-app cues here and Enable browser notifications here controls. Browser permission never follows from loading, review scheduling or in-app opt-in. Reached cues retain visible task title, exact UTC instant and named display zone, with explicit optional-review wording and a Tasks review-editor link. Denied/unavailable permission leaves in-app cues visible. Loaded-snapshot freshness, clock changes, uncertain attempts, local receipt capacity and closed-tab/background delivery limits remain readable. Actions wrap at phone widths and enlarged text; disabled browser controls do not imply enrollment. [foreground-reminders.md](foreground-reminders.md) records ownership and qualification limits.

## Scheduled reminder review contract

Durable push reminder setup is separate from saving a review cue and enabling foreground browser notifications. Reviewed API/MCP previews expose exact task/cue, device targets, timezone, repeat/end/DST policy and expiry; unknown configuration acceptance recovers the original command. Device consent remains a direct browser/platform boundary. Disabled schedules and provider-accepted/refused/unknown observations remain distinct from shown/read delivery. The dedicated iPhone setup and safe notification-click UI remain unimplemented by the sender unit; [scheduled-push-reminders.md](scheduled-push-reminders.md) records the local backend boundary.

Push setup surfaces: `/todo/sync` exposes separate account/device consent and revocation; `/todo/reminders` exposes exact task cue, device targets, repeat/DST/end preview and original receipt recovery; `/todo/reminders/open` reveals a task only after explicit fresh authenticated exact-workspace access. Enlarged-text phone/desktop qualification includes light and dark schemes with Obsidian Glow. See [push-reminder-ui.md](push-reminder-ui.md).

Until a reviewed event channel is implemented and host-qualified, reminder UX uses the separately consented PWA channel or foreground Today cues. Companion must distinguish host receipt from actual chat rendering; no arbitrary originating-chat binding is inferred. See [originating-chat-reminders.md](originating-chat-reminders.md).

For explicit unambiguous ordinary create/edit intent, the agent checks the concrete preview internally and proceeds without an extra conversational approval exchange. Host permission UI and every consequential-action review remain separate. No Companion UI route changed; see [ordinary-task-intent.md](ordinary-task-intent.md).

## Task journey evidence boundary

The current Todo navigation and supported task flows use [task-journey-qualification.md](task-journey-qualification.md) for reproducible current-route checks. Historical prototype destinations supply design history only. Record viewport/theme/text, keyboard/focus, exact result and source/test provenance. Real keyboard/safe-area, touch, VoiceOver/TalkBack and human observations remain separate unrun release lanes.

## Native recovery exposure boundary

The native recovery backend/MCP contract distinguishes unarchive from completed-Todo restore and exact Daily occurrence correction. The current history controls name the exact archive revision or occurrence/source command, preserve original history, explain required reference repair and refuse linked/uncertain native effects. Linked completion uncheck has a separate review with unknown resource effects and original-receipt recovery. No implicit reward reversal or next occurrence follows from native correction.

## History recovery

`/todo/history` keeps native Todo restoration, archive recovery, terminal Daily correction and Habitica uncheck distinct. The retained-record browser pages archived tasks and completed/skipped Dailies without inferring missing activity. Reviews show exact source identities and native consequences; linked resource changes remain unknown. Pending linked recovery owns editing, disables replacement actions and points back to original receipts. Filters and draft text remain independent. See [history recovery controls](history-recovery.md) for supported states and current qualification boundaries.

## Durable existing-record link recovery

Tasks, retained History and the advanced task panel keep the same exact-pair comparison and controls. Typed transport now uses shared adoption preparation/acceptance/original-receipt tools. Recover original link receipt reads the immutable command before any original acceptance retry; later workspace revisions do not invalidate an accepted receipt. A proven original terminal refusal releases only the uncertainty fence and requires a fresh comparison. Generic HTTP errors preserve the original review. Accepted mapping metadata alone never confirms rewards or fresh native state. No consent, device permission or executor activation follows from linking.

Phone-width link editors remove repeated nested card insets and reduce review padding so enlarged recovery labels remain readable. Desktop spacing stays independent. Geometry/contrast measurements and manual keyboard/readability review remain separate evidence.

DiagnosticSharingPanel shares the /diagnostics and /live-tests routes. The panel separates status, unchecked diagnostic consent, local source/filter preview, metadata preparation, unchecked exact-body upload and original lookup. Pending or unknown outcomes lock replacement and repeat upload. Identity changes erase private preview state; normal navigation preserves the scoped review. Long Workspace/request/hash identifiers wrap inside the card. Cleanup uncertainty and unavailable sources remain visible. Rendered route/state evidence resides in skill-tree/history/validation/diagnostic-sharing-ui.
