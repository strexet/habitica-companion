# Future Work

Last validated: 2026-05-27.

This is the single implementation queue. Historical source plans were merged here and removed after implemented items were filtered out. Entries higher in the file are higher priority; finish them first.

Implemented behavior belongs in `FEATURES.md`, foundational architecture notes in `TECHNICAL.md`, Habitica endpoint rules in `HABITICA_API.md`, and UI guidance in `docs/UX_UI_MANIFEST.md`. Delete an entry from this file when it ships.

## Implementor Rules

1. Read the relevant source-of-truth docs before editing:
   - UI/UX: `docs/UX_UI_MANIFEST.md`
   - Architecture, sync, storage: `TECHNICAL.md`
   - Habitica API rules: `HABITICA_API.md`
   - Habitica party/quest link behavior: `docs/HABITICA_DEEPLINKS.md`
   - Cloudflare deployment and D1/KV: `docs/DEPLOY_CLOUDFLARE_PAGES.md`
2. Implement one entry only. Do not bundle unrelated cleanup, renames, or opportunistic refactors.
3. If a task lists `Touch:`, edit only those paths and direct tests unless the task explicitly permits more.
4. Add or update tests next to affected code. UI behavior changes need Razor component tests where similar tests exist.
5. User-facing behavior changes must update `FEATURES.md`; sync architecture or backend behavior changes must update `TECHNICAL.md`.
6. Schema changes need the next numbered migration under `migrations/` and a deployment-doc update.
7. Never send Habitica API tokens to Cloudflare party-sync or app-data sync endpoints.
8. Keep labels short and plain. If UI copy is ambiguous, choose the smallest clear label and proceed.
9. If a needed Habitica endpoint is not documented in `HABITICA_API.md`, stop and add a follow-up entry instead of guessing.
10. For Habitica party/quest links, use stable web URLs only. Do not add `habitica://`, Android `intent://`, app-opening probes, or mobile-app-specific party/quest links unless `docs/HABITICA_DEEPLINKS.md` is updated with new official support.
11. When interacting with this file, process `Pending to be added to Prioritized Next Changes` before starting implementation work. Move one pending item at a time into `Prioritized Next Changes`, either as a new self-contained entry or merged into an existing matching entry, then remove the moved item from pending. Keep `Top` additions before current prioritized entries, `Middle` additions after all `Top` additions and before current prioritized entries, and `Bottom` additions at the bottom of `Prioritized Next Changes`.

## Validated Implemented And Removed From Backlog

- Web-app MVP shell, sign-in, staged refresh, cached dashboard/task/party/inventory snapshots, diagnostics, and local/cloud data controls.
- Inventory preset layout, stat highlighting, equipment explorer, and preset persistence.
- Task browsing, type/status filters, guarded task scoring controls, expandable details, and task mutation freshness gates.
- Spell page, target recommendations, resource checks, and not-CRONed buff warning flow.
- Dashboard Start New Day action with explicit CRON confirmation, result feedback, and post-CRON refresh.
- Party page active quest metadata/rewards, CRON summary, member CRON graph, shared quest pool, queue, voting, recent completions, owner/admin/Officer controls, and quest start action.
- Dashboard pending damage estimate, knockout warning, and manual health-potion purchase action.
- Split-key encrypted Cloudflare app-data sync, legacy single-blob restore fallback, per-section payload guard, partial-success sync behavior, and refresh coordinator deduplication.
- Refresh-domain invalidation basics after implemented mutations.
- Staged sign-in refresh UX, scoped refresh indicators, per-section cloud sync status, sync exclusions, and explicit cloud-sync conflict choices.
- Two-handed weapon awareness in spell equipment recommendations: weapon/shield selected as a `twoHanded`-aware pair; shield omitted when the two-handed weapon outscores the best one-handed + shield combination.
- Task history statistics, aggregate history charts, expanded-card per-task charts, manual task ordering, drag handles, keyboard reordering, and move buttons.
- Inventory equipment optimizer with goal selector, before/after stat deltas, recommendation equip/save actions, and two-handed weapon handling.
- Bulk sell planner for eggs, food, and hatching potions with safe surplus preview, explicit confirmation, sequential sell execution, diagnostics, and post-sell refresh.
- Dashboard Start New Day optional gear optimization: INT for post-CRON mana, CON/survival for lower damage risk, previewed stat deltas, already-equipped state, and sequential equip-before-CRON execution.
- Spells sticky current-mana bar with available MP, max MP, class, and persistent scroll visibility above spell cards.
- App color scheme system with centralized semantic tokens, Alpha/Habitica/Gryphy built-in schemes, Settings picker, custom editable schemes, shell/button/disabled/input theming, mobile localStorage fallback, fast local reload persistence, and portable sync storage.
- Random color scheme controls: shared color-scheme panel on Settings, Dashboard, and Sign-in, random-preset pick from built-in plus custom schemes, fully-random theme generation with a chaos slider (Calm to Madness) scaling hue/saturation divergence, held as a session-only pending theme (selectable via a "Generated" dropdown entry, applied without persisting), naming/saving the last random theme into custom schemes, and copy/paste of presets for building fully custom palettes.

## Pending Queue

### Queued items to be added to `Prioritized Next Changes`

Work top to bottom. This is an intake list for rough notes that must become self-contained `Prioritized Next Changes` entries before implementation. Preserve the `Priority Instructions` and `Entries` structure.

### Priority Instructions

- Top – add to the top of the `Prioritized Next Changes` list (max priority).
- Middle – right after the `Top` entries and before current `Prioritized Next Changes` list items.
- Bottom – (default) the lowest priority entries, add to the bottom of the `Prioritized Next Changes` list.

### Entries:

(empty)

## Prioritized Next Changes

Work top to bottom. Each entry is self-contained.

### Color Scheme Catalog Overhaul And Light/Dark Default Restore

Goal: rebuild the built-in color-scheme catalog with explicit light/dark categorization and grouped UI sections, fix custom-preset deletion not persisting across reload, and make scheme deletion fall back to the default of the matching light/dark variant instead of jumping to Alpha.

Touch:
- `src/Habitica.WebApp/Theme/ColorSchemeCatalog.cs`
- `src/Habitica.WebApp/Theme/ColorSchemeService.cs`
- new `src/Habitica.WebApp/Theme/ReadableSchemeClipboardModel.cs` and `src/Habitica.WebApp/Theme/ReadableSchemeParser.cs` (v2 readable copy/paste shape + bidirectional parser; v1 legacy parser stays for backwards-compatible paste)
- `src/Habitica.WebApp/Components/ColorSchemePanel.razor`
- `src/Habitica.WebApp/wwwroot/js/colorSchemes.js` (CSS variable emission for new gradient/text-shadow tokens; also the delete-persistence bug if it originates here)
- `src/Habitica.WebApp/wwwroot/css/app.css` (consume new gradient/text-shadow CSS variables; global `accent-color` rule for native checkboxes/radios)
- `src/Habitica.Storage/StorageKeys.cs` (no key changes unless a preferences schema bump is required to carry the variant flag and gradient fields)
- direct tests in `tests/Habitica.WebApp.Tests/Theme/ColorSchemeCatalogTests.cs` and `tests/Habitica.WebApp.Tests/Components/ColorSchemePanelTests.cs`
- `FEATURES.md`

Bug fix — delete persistence:
- Reproduce: delete a custom preset, refresh, the preset comes back. Trace the round-trip across `DeleteCustomAsync`, `SavePreferencesAsync`, the localStorage fast cache (`HabiticaColorScheme.applyAndStore`) and the portable `IKeyValueStorage` value. Make sure the delete writes the reduced custom-list to both stores so neither path can resurrect the deleted scheme on next `LoadPreferencesAsync`.

Variant model:
- Add an `IsDark` boolean (or `SchemeVariant { Light, Dark }` enum) to `ColorSchemeDefinition` covering both built-in and custom schemes.
- For custom schemes, the editor exposes a labeled toggle ("Dark theme") next to the Save action. Default to a luminance-derived guess from the Background token; user can override. The variant must survive reload and portable sync.
- Add `DefaultLightSchemeId = "gryphy-light"` and `DefaultDarkSchemeId = "gryphy-dark"` constants. Remove or retain `AlphaId` only as a legacy migration target.

Deletion fallback:
- When the active scheme is deleted, `ColorSchemeService.DeleteCustomAsync` selects `DefaultLightSchemeId` or `DefaultDarkSchemeId` based on the deleted scheme's variant. No silent revert to Alpha.

Built-in catalog rewrite (ordering matters — first two are defaults):
1. `gryphy-light` "Gryphy (Light)" — Light. Replace tokens with the JSON below.
2. `gryphy-dark` "Gryphy (Dark)" — Dark. Replace tokens with the JSON below.
3. `forest-legacy` "Forest Legacy" — Light. Rename from `alpha` ("Alpha (Light)"); keep its current tokens. Add a stored-preferences migration `alpha` → `forest-legacy`.
4. Keep `frosted-cake`.
5. Replace `neon-rogue` with new id `arcane-wraith` "Arcane Wraith" — Dark. Tokens below.
6. Replace `neon-abyss-carnival` with new id `phantom-fair` "Phantom Fair" — Dark. Tokens below.
7. Add new built-ins (tokens below): `toxic-swamp` "Toxic Swamp" (Dark), `green-menace` "Green Menace" (Dark), `abyssal-blackwater` "Abyssal Blackwater" (Dark), `obsidian-glow` "Obsidian Glow" (Dark), `blessed-skyhaven` "Blessed Skyhaven" (Light), `infernal-covenant` "Infernal Covenant" (Dark).
8. Remove built-ins: `habitica`, `mana-mirage`, `mushroom-meadow`, `mushroom-trip`, `sugar-crash`.
9. Retain existing other built-ins (`midnight-tavern`, `dragonfire-keep`, `frost-healer`, `sunlit-stable`, `mosswood-quest`, `potion-shop`, `boss-battle`, `quiet-ledger`, `celestial-inn`) — verify each is tagged with the correct light/dark variant.
10. Author additional Habitica-flavored low-contrast built-ins until light count == dark count. Suggested concepts (name to taste, the implementor picks final names within the spirit): a gold/treasure light theme, an arcane/mana light or dark theme, a stone/brute-force dark theme. Playful shadow tints are encouraged; avoid hard high-contrast palettes.

Panel grouping:
- `ColorSchemePanel.razor` renders sections in this exact order:
  1. Default — Gryphy Light, Gryphy Dark
  2. Built-in Light
  3. Built-in Dark
  4. Custom (hidden when no custom schemes exist)
  5. Generated (hidden unless a pending random theme exists)
- Sections render as `<optgroup>` labels in the dropdown and as visible group labels in expanded UI. The order is invariant.

Stored-preferences migration:
- On `LoadPreferencesAsync`, remap legacy ids:
  - `alpha` → `forest-legacy`
  - `neon-rogue` → `arcane-wraith`
  - `neon-abyss-carnival` → `phantom-fair`
  - `habitica`, `mana-mirage`, `mushroom-meadow`, `mushroom-trip`, `sugar-crash` → `DefaultLightSchemeId` for known-light originals; `DefaultDarkSchemeId` for known-dark originals.
- Migration runs once and persists the remapped selection back to storage.

Built-in token JSON (verbatim — copy into `ColorSchemeCatalog.cs` without modification; field order in `ColorSchemeTokens` is positional, map carefully):

```json
// id: gryphy-light  name: "Gryphy (Light)"  variant: Light
{ "Background": "#f7f1ff", "CardBackground": "rgba(255, 252, 255, 0.94)", "CardBorder": "rgba(103, 49, 184, 0.13)", "Ink": "#2d2040", "Muted": "#756881", "Primary": "#7334bd", "Accent": "#d99416", "Danger": "#c84a67", "Success": "#2a9277", "Focus": "#438fd0", "Shadow": "0 18px 44px rgba(32, 17, 54, 0.09)", "Surface": "rgba(255, 255, 255, 0.72)", "SurfaceStrong": "rgba(255, 252, 255, 0.92)", "ChartPrimary": "#7334bd", "ChartSecondary": "#438fd0", "TaskNegative": "#e4d8f6", "TaskNeutral": "#f0e9fb", "TaskPositive": "#faf7ff", "AppBarBackground": "#684095", "AppBarText": "#fff8ff", "DrawerBackground": "#3b2356", "DrawerText": "#f8f0ff", "ButtonText": "#ffffff", "DisabledBackground": "rgba(103, 49, 184, 0.06)", "DisabledText": "rgba(117, 104, 129, 0.58)", "DisabledBorder": "rgba(117, 104, 129, 0.22)", "InputBackground": "rgba(255, 252, 255, 0.92)", "InputBorder": "rgba(103, 49, 184, 0.13)" }
```

```json
// id: gryphy-dark  name: "Gryphy (Dark)"  variant: Dark
{ "Background": "#12091e", "CardBackground": "rgba(30, 16, 47, 0.94)", "CardBorder": "rgba(178, 93, 255, 0.18)", "Ink": "#f3eaf6", "Muted": "#b5a6c5", "Primary": "#a765e2", "Accent": "#d9b33a", "Danger": "#e46380", "Success": "#58bfa9", "Focus": "#5aa9df", "Shadow": "0 22px 52px rgba(0, 0, 0, 0.34)", "Surface": "rgba(45, 24, 70, 0.76)", "SurfaceStrong": "rgba(38, 21, 61, 0.94)", "ChartPrimary": "#a765e2", "ChartSecondary": "#5aa9df", "TaskNegative": "#21172d", "TaskNeutral": "#2d2040", "TaskPositive": "#3c2a55", "AppBarBackground": "#241436", "AppBarText": "#f3eaf6", "DrawerBackground": "#211331", "DrawerText": "#f3eaf6", "ButtonText": "#140a20", "DisabledBackground": "rgba(190, 140, 230, 0.085)", "DisabledText": "rgba(181, 166, 197, 0.52)", "DisabledBorder": "rgba(190, 140, 230, 0.22)", "InputBackground": "#21142e", "InputBorder": "rgba(190, 140, 230, 0.24)" }
```

```json
// id: arcane-wraith  name: "Arcane Wraith"  variant: Dark  (replaces neon-rogue)
{ "Background": "#0b0920", "CardBackground": "rgba(20, 17, 44, 0.94)", "CardBorder": "rgba(68, 190, 210, 0.20)", "Ink": "#ececf7", "Muted": "#aaa7cb", "Primary": "#42bfd2", "Accent": "#d75ad2", "Danger": "#df5d7d", "Success": "#55c894", "Focus": "#907ee0", "Shadow": "0 22px 54px rgba(0, 0, 0, 0.42)", "Surface": "rgba(25, 22, 56, 0.78)", "SurfaceStrong": "rgba(31, 27, 70, 0.92)", "ChartPrimary": "#42bfd2", "ChartSecondary": "#d75ad2", "TaskNegative": "#102631", "TaskNeutral": "#183745", "TaskPositive": "#1b4c59", "AppBarBackground": "#151037", "AppBarText": "#ececf7", "DrawerBackground": "#100d2b", "DrawerText": "#ececf7", "ButtonText": "#0b0920", "DisabledBackground": "rgba(236, 236, 247, 0.08)", "DisabledText": "rgba(170, 167, 203, 0.52)", "DisabledBorder": "rgba(68, 190, 210, 0.22)", "InputBackground": "#19163a", "InputBorder": "rgba(68, 190, 210, 0.26)" }
```

```json
// id: phantom-fair  name: "Phantom Fair"  variant: Dark  (replaces neon-abyss-carnival)
{ "Background": "#0b0820", "CardBackground": "rgba(23, 13, 51, 0.94)", "CardBorder": "rgba(220, 86, 200, 0.28)", "Ink": "#eee8f2", "Muted": "#aea2ce", "Primary": "#43bfd2", "Accent": "#d6bd42", "Danger": "#df5578", "Success": "#5fc991", "Focus": "#9b61d6", "Shadow": "0 24px 70px rgba(220, 55, 95, 0.20)", "Surface": "rgba(34, 21, 75, 0.78)", "SurfaceStrong": "rgba(43, 27, 95, 0.92)", "ChartPrimary": "#43bfd2", "ChartSecondary": "#d6bd42", "TaskNegative": "#2a1738", "TaskNeutral": "#24385f", "TaskPositive": "#17584a", "AppBarBackground": "#1a1038", "AppBarText": "#eee8f2", "DrawerBackground": "#0d0924", "DrawerText": "#eee8f2", "ButtonText": "#0b0820", "DisabledBackground": "rgba(238, 232, 242, 0.08)", "DisabledText": "rgba(174, 162, 206, 0.52)", "DisabledBorder": "rgba(220, 86, 200, 0.22)", "InputBackground": "#18113a", "InputBorder": "rgba(67, 191, 210, 0.28)" }
```

```json
// id: toxic-swamp  name: "Toxic Swamp"  variant: Dark
{ "Background": "#10190f", "CardBackground": "#1d2b1b", "CardBorder": "#496a35", "Ink": "#e8edd6", "Muted": "#99aa83", "Primary": "#9bdc2f", "Accent": "#5b4fd6", "Danger": "#c83a32", "Success": "#5fc94a", "Focus": "#b6ef42", "Shadow": "0 22px 56px rgba(100, 180, 38, 0.16)", "Surface": "#22331f", "SurfaceStrong": "#2c4725", "ChartPrimary": "#9bdc2f", "ChartSecondary": "#5b4fd6", "TaskNegative": "#3a1715", "TaskNeutral": "#2b3827", "TaskPositive": "#173717", "AppBarBackground": "#213f19", "AppBarText": "#e8edd6", "DrawerBackground": "#172814", "DrawerText": "#e8edd6", "ButtonText": "#10190f", "DisabledBackground": "#2e3c2b", "DisabledText": "#7f8d71", "DisabledBorder": "#405234", "InputBackground": "#1a2519", "InputBorder": "#58723c" }
```

```json
// id: green-menace  name: "Green Menace"  variant: Dark
{ "Background": "#120f12", "CardBackground": "#332031", "CardBorder": "#654461", "Ink": "#e8dfd2", "Muted": "#aeb9b4", "Primary": "#c72ab7", "Accent": "#55c964", "Danger": "#b92828", "Success": "#3bae5d", "Focus": "#5ac568", "Shadow": "0 18px 40px rgba(8, 12, 16, 0.31)", "Surface": "#3a2a38", "SurfaceStrong": "#4a3848", "ChartPrimary": "#c72ab7", "ChartSecondary": "#55c964", "TaskNegative": "#b92828", "TaskNeutral": "#aeb9b4", "TaskPositive": "#3bae5d", "AppBarBackground": "#3d1839", "AppBarText": "#e8dfd2", "DrawerBackground": "#351f33", "DrawerText": "#e8dfd2", "ButtonText": "#f0e8dc", "DisabledBackground": "#3d313c", "DisabledText": "#8d9993", "DisabledBorder": "#5a4057", "InputBackground": "#2f2630", "InputBorder": "#654461" }
```

```json
// id: abyssal-blackwater  name: "Abyssal Blackwater"  variant: Dark
{ "Background": "#000405", "CardBackground": "rgba(1, 6, 7, 0.99)", "CardBorder": "rgba(38, 150, 156, 0.38)", "Ink": "#c9e4e2", "Muted": "#7f9f9e", "Primary": "#35b8be", "Accent": "#2d8289", "Danger": "#aa3b4e", "Success": "#339f87", "Focus": "#43c7cd", "Shadow": "0 34px 104px rgba(8, 68, 74, 0.16)", "Surface": "rgba(1, 7, 8, 0.97)", "SurfaceStrong": "rgba(2, 10, 12, 0.99)", "ChartPrimary": "#35b8be", "ChartSecondary": "#2d8289", "TaskNegative": "#100305", "TaskNeutral": "#031113", "TaskPositive": "#031310", "AppBarBackground": "#000607", "AppBarText": "#c9e4e2", "DrawerBackground": "#000202", "DrawerText": "#c9e4e2", "ButtonText": "#000405", "DisabledBackground": "rgba(201, 228, 226, 0.055)", "DisabledText": "rgba(127, 159, 158, 0.58)", "DisabledBorder": "rgba(38, 150, 156, 0.24)", "InputBackground": "#010809", "InputBorder": "rgba(53, 184, 190, 0.36)" }
```

```json
// id: obsidian-glow  name: "Obsidian Glow"  variant: Dark
{ "Background": "#05060a", "CardBackground": "rgba(12, 14, 22, 0.96)", "CardBorder": "rgba(155, 190, 255, 0.20)", "Ink": "#e7ecf6", "Muted": "#98a2b8", "Primary": "#7fa8ff", "Accent": "#b78cff", "Danger": "#d85f78", "Success": "#58c99b", "Focus": "#9fc0ff", "Shadow": "0 24px 72px rgba(150, 185, 255, 0.22)", "Surface": "rgba(15, 18, 30, 0.82)", "SurfaceStrong": "rgba(20, 24, 40, 0.96)", "ChartPrimary": "#7fa8ff", "ChartSecondary": "#b78cff", "TaskNegative": "#21151f", "TaskNeutral": "#171d2d", "TaskPositive": "#13251f", "AppBarBackground": "#080a12", "AppBarText": "#e7ecf6", "DrawerBackground": "#07080f", "DrawerText": "#e7ecf6", "ButtonText": "#05060a", "DisabledBackground": "rgba(231, 236, 246, 0.07)", "DisabledText": "rgba(152, 162, 184, 0.54)", "DisabledBorder": "rgba(155, 190, 255, 0.16)", "InputBackground": "#0e111c", "InputBorder": "rgba(155, 190, 255, 0.24)" }
```

```json
// id: blessed-skyhaven  name: "Blessed Skyhaven"  variant: Light
{ "Background": "#edf8ff", "CardBackground": "rgba(255, 255, 255, 0.97)", "CardBorder": "rgba(255, 213, 92, 0.62)", "Ink": "#1b3148", "Muted": "#6f879d", "Primary": "#6fbfff", "Accent": "#f0bd3f", "Danger": "#d75d75", "Success": "#56b8f0", "Focus": "#9bdcff", "Shadow": "0 24px 78px rgba(255, 224, 128, 0.38)", "Surface": "rgba(245, 252, 255, 0.92)", "SurfaceStrong": "rgba(255, 255, 255, 0.99)", "ChartPrimary": "#6fbfff", "ChartSecondary": "#f0bd3f", "TaskNegative": "#ffe4ec", "TaskNeutral": "#f5fcff", "TaskPositive": "#e2f5ff", "AppBarBackground": "#fff4c5", "AppBarText": "#1b3148", "DrawerBackground": "#fafdff", "DrawerText": "#1b3148", "ButtonText": "#10283d", "DisabledBackground": "rgba(27, 49, 72, 0.08)", "DisabledText": "rgba(111, 135, 157, 0.62)", "DisabledBorder": "rgba(255, 213, 92, 0.36)", "InputBackground": "#fbfeff", "InputBorder": "rgba(111, 191, 255, 0.52)" }
```

```json
// id: infernal-covenant  name: "Infernal Covenant"  variant: Dark
{ "Background": "#030000", "CardBackground": "rgba(9, 1, 1, 0.99)", "CardBorder": "rgba(255, 34, 54, 0.54)", "Ink": "#e8caca", "Muted": "#a46d70", "Primary": "#ff1f36", "Accent": "#9b0c18", "Danger": "#ff3048", "Success": "#9a4a34", "Focus": "#ff4058", "Shadow": "0 30px 90px rgba(255, 20, 45, 0.32)", "Surface": "rgba(10, 2, 2, 0.97)", "SurfaceStrong": "rgba(18, 3, 4, 0.99)", "ChartPrimary": "#ff1f36", "ChartSecondary": "#9b0c18", "TaskNegative": "#210305", "TaskNeutral": "#140203", "TaskPositive": "#1a0704", "AppBarBackground": "#070000", "AppBarText": "#e8caca", "DrawerBackground": "#010000", "DrawerText": "#e8caca", "ButtonText": "#030000", "DisabledBackground": "rgba(232, 202, 202, 0.06)", "DisabledText": "rgba(164, 109, 112, 0.62)", "DisabledBorder": "rgba(255, 34, 54, 0.28)", "InputBackground": "#090101", "InputBorder": "rgba(255, 31, 54, 0.48)" }
```

Token model expansion — gradients and text shadows:

Big surfaces gain optional multi-stop gradient definitions. All new fields are nullable on `ColorSchemeTokens`; when null, the renderer falls back to the existing solid token. Backwards compatibility: every retained built-in (the 11 not rewritten) must keep working with all gradient fields null.

- Add nullable token records (one per gradient-capable surface). Reuse a shared shape:

```csharp
public sealed record GradientStops9(
    string TopLeft, string Top, string TopRight,
    string MiddleLeft, string Middle, string MiddleRight,
    string BottomLeft, string Bottom, string BottomRight);

public sealed record GradientStops8(
    string TopLeft, string Top, string TopRight,
    string MiddleLeft, string MiddleRight,
    string BottomLeft, string Bottom, string BottomRight);

public sealed record GradientStops6(
    string TopLeft, string Top, string TopRight,
    string BottomLeft, string Bottom, string BottomRight);

public sealed record GradientStops4(
    string TopLeft, string TopRight,
    string BottomLeft, string BottomRight);

public sealed record GradientStops2(
    string Start, string End);
```

- Extend `ColorSchemeTokens` with nullable fields (all default `null`):
  - `GradientStops9? BackgroundGradient` — full-page main background. 9 stops because the surface fills the viewport; the center stop is meaningful.
  - `GradientStops8? CardGradient` — content-bearing panels (`CardBackground`/`Surface`/`SurfaceStrong`). Center stop omitted because text rendered atop the card would compete with a center color flare; mid-edges preserve directional shading without distracting the content.
  - `GradientStops6? AppBarGradient` — top app header. Six stops: TL/T/TR + BL/B/BR. App-bar height is small, no need for mid-row stops.
  - `GradientStops6? DrawerGradient` — side menu. Six stops as above; the drawer is narrow and tall so the horizontal triplets read as gradient bands rather than a square grid (acceptable, vertical gradient still dominates visually).
  - `GradientStops4? PrimaryButtonGradient` — filled primary buttons (Cast, Start New Day, Confirm). 4 corner stops; small surface, 2 may suffice but 4 lets the implementor produce a subtle diagonal sheen if desired.
  - `GradientStops2? SecondaryButtonGradient` — outlined/text-secondary buttons. 2 stops only (start → end, vertical by default). Cheap and unobtrusive.
  - `GradientStops2? AccentChipGradient` — accent/badge surfaces (e.g. quest reward chips, status pills). 2 stops.
- Buttons opt in: when the gradient field is null, the button keeps its solid background (today's behavior). When set, the rendered button uses the gradient. Same null-fallback rule applies to chips.
- Icons (raw SVG/inline) stay solid — they are filled with `currentColor` and a gradient would require `<linearGradient>` defs out of scope here.
- Disabled-state guard: when a button is `:disabled`, force `background: var(--disabled-bg)` regardless of gradient — keep contrast with `--disabled-text` and avoid the gradient making disabled buttons look active.
- Focus ring: ensure `:focus-visible` outline (`var(--focus)`) renders above the gradient.
- Add nullable text-shadow tokens (per surface):
  - `string? HeadingTextShadow` — applied to `h1`/`h2`/`h3` and `.section-label` rendered on cards/panels.
  - `string? AppBarTextShadow` — applied to `AppBarText`.
  - `string? DrawerTextShadow` — applied to `DrawerText`.
  Each is a raw CSS `text-shadow` value (e.g. `"0 1px 2px rgba(0,0,0,0.35)"`). Null = no shadow.

Gradient render recipe (frozen — do NOT pick a different approach without raising it):

- Multi-corner stops (9/8/6/4) are NOT rendered as stacked CSS `radial-gradient` overlays. Stacking eight radials blooms, miscolors the midpoints, and burns GPU at full viewport. Conic gradients rotate, do not interpolate bilinearly. Houdini Paint Worklets lack Safari/Firefox-stable support.
- Instead: in `colorSchemes.js`, paint a tiny `<canvas>` once per gradient surface per scheme load. One pixel per stop. Export as a data URL via `canvas.toDataURL("image/png")`. CSS scales the image up via `background-size: 100% 100%`; the browser's native bilinear interpolation produces a smooth multi-corner gradient that genuinely honors every stop.
- Canvas sizes per surface:
  - 9-stop `PageBackground`: 3×3 px (rows top/middle/bottom; columns left/center/right).
  - 8-stop `Card`: 3×3 px. The middle pixel is computed as the average of the eight stops (so the user-perceived "center" still blends smoothly without requiring an explicit input field).
  - 6-stop `AppBar`, `Drawer`: 3×2 px (top row TL/T/TR, bottom row BL/B/BR).
  - 4-stop `PrimaryButton`: 2×2 px.
  - 2-stop `SecondaryButton`, `AccentChip`: NO canvas — use plain `linear-gradient(180deg, var(--start), var(--end))`. 2-stop is degenerate and CSS handles it well.
- Painter pseudocode (lives in `colorSchemes.js`):

```javascript
function paintStopsToDataUrl(width, height, stops) {
  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;
  const ctx = canvas.getContext("2d");
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      ctx.fillStyle = stops[y * width + x];
      ctx.fillRect(x, y, 1, 1);
    }
  }
  return canvas.toDataURL("image/png");
}

// 9-stop background:
const bgUrl = paintStopsToDataUrl(3, 3, [
  g.topLeft,    g.top,    g.topRight,
  g.middleLeft, g.middle, g.middleRight,
  g.bottomLeft, g.bottom, g.bottomRight,
]);
root.style.setProperty("--bg-gradient", `url("${bgUrl}") center/100% 100% no-repeat, var(--bg)`);
```

- Stops accept any CSS color string `ctx.fillStyle` accepts: `#rgb`, `#rrggbb`, `rgba(...)`, `hsl(...)`, `color-mix(...)` (where supported). Translucent stops (rgba) honor alpha; the `var(--bg)` fallback layer underneath the image keeps the composition valid.
- Composed variable shape: `url("data:image/png;base64,...") center/100% 100% no-repeat, <solid-fallback>`. The trailing solid fallback is the matching solid token (`var(--bg)`, `var(--card-bg)`, etc.) so any opacity in stops resolves against the right base.
- `background-size: 100% 100%` is implicit in the shorthand. Do NOT use `background-repeat` other than `no-repeat`. Bilinear scaling is the browser default for raster images via `image-rendering: auto` — do NOT set `image-rendering: pixelated` on these surfaces.
- Performance: each gradient repaint is one canvas + one `toDataURL` call. The canvases are tiny (max 9 pixels), conversion is sub-millisecond. Data URLs total to a few hundred bytes across all surfaces. Painting happens once per scheme switch, not per frame. No further runtime cost.
- Memory budget: store the generated data URLs on the active scheme's applied state; clear them when the scheme changes. No need to cache across schemes.
- Accessibility: the data-URL image is purely decorative; do NOT add `role="img"` or `alt` text. Reduced-motion preference is irrelevant (static).
- Print stylesheets: skip the data URL on `@media print` — fall back to the solid token. The image-URL CSS variable still works in print but adds ink cost.

Feasibility sanity check:
- 9/8/6/4-stop gradients via tiny rasterized images render reliably in Chrome, Edge, Safari (incl. iOS), Firefox.
- 2-stop CSS linear gradients are universally supported.
- Text shadows are universally supported.
- Native `accent-color` is supported in all evergreen browsers; older Safari falls back to default tick. Acceptable.
- Per-corner button gradients on `:hover` / `:active` keep working because we override `background:` and let `transform`/`box-shadow` handle the interaction states.

JS interop and CSS variables:
- `colorSchemes.js` emits per-stop variables under predictable names. For the 9-stop background:
  `--bg-grad-tl`, `--bg-grad-t`, `--bg-grad-tr`, `--bg-grad-ml`, `--bg-grad-m`, `--bg-grad-mr`, `--bg-grad-bl`, `--bg-grad-b`, `--bg-grad-br`.
  Similar pattern for `--card-grad-*` (8 stops, no `-m`), `--appbar-grad-*` (6), `--drawer-grad-*` (6).
- In addition, emit a precomposed `--bg-gradient` (composed `radial-gradient(...)` or layered `conic-gradient` — implementer chooses the recipe that best approximates a 3×3 corner-mapped surface) and similar `--card-gradient`, `--appbar-gradient`, `--drawer-gradient`, `--primary-btn-gradient`, `--secondary-btn-gradient`, `--accent-chip-gradient`. CSS rules in `app.css` reference the composed variables; the per-stop variables are exposed for ad-hoc consumers (e.g. badge or chart code).
- When a scheme has no gradient for a surface, emit `--<surface>-gradient: <solid-token-fallback>` (so `background: var(--bg-gradient)` still works), and clear the per-stop `--*-grad-*` variables to `initial`. Solid fallbacks per surface: `--bg`, `--card-bg`, `--appbar-bg`, `--drawer-bg`, `--primary`, transparent/`--card-bg`, `--accent`.
- Emit text-shadow variables: `--heading-text-shadow`, `--appbar-text-shadow`, `--drawer-text-shadow`. Default `none` when null.

CSS in `app.css`:
- `body` / main app shell background: `background: var(--bg-gradient);` instead of `background: var(--bg);`.
- `.card-surface`, `.dashboard-panel`: `background: var(--card-gradient);`.
- App bar / drawer: `background: var(--appbar-gradient);` / `background: var(--drawer-gradient);`.
- MudButton primary (`Variant.Filled` + `Color.Primary`): `background: var(--primary-btn-gradient);`. Secondary/outlined buttons: `background: var(--secondary-btn-gradient);`. Accent chips/badges: `background: var(--accent-chip-gradient);`. `:disabled` overrides to `var(--disabled-bg)`. `:focus-visible` outline uses `var(--focus)`.
- Headings receive `text-shadow: var(--heading-text-shadow, none);`. AppBar/Drawer text use their respective shadow variables.
- Native control accent fix (see below): a single global rule for `input[type="checkbox"], input[type="radio"] { accent-color: var(--primary); }` placed where it cannot be overridden by browser defaults. The existing scoped `.app-input { accent-color: var(--primary); }` does not cover bare checkboxes (e.g. PARTY SYNC SETTINGS `Shared queue controls` checkboxes), which is why those render as system blue today.

Custom-scheme editor:
- When the user saves a custom scheme, expose collapsible "Advanced" panels per gradient-capable surface. Default state: collapsed and disabled (solid token used). On expand + enable, render the 9/8/6 swatch pickers prefilled from the current solid token so the user can tweak. On save, only persist gradient fields that are populated; otherwise persist null.
- Add a "Text shadows" subgroup with three optional inputs (heading/appbar/drawer). Empty value = null.

Native control accent fix (also addresses PARTY SYNC SETTINGS — Shared queue controls):
- Today `src/Habitica.WebApp/wwwroot/css/app.css:566` and `:1735` and `:2902` set `accent-color: var(--primary)` only on `.app-input` and a couple of scoped contexts. Bare `<input type="checkbox">` inside `.checkbox-row` (PartyPage.razor lines 160–195) inherits the browser's system color (Windows/Chrome blue, Safari blue, etc.).
- Add a global rule `input[type="checkbox"], input[type="radio"] { accent-color: var(--primary); }` near the top of `app.css` and verify no scoped overrides exist downstream. Verify the resulting tick color updates across all themes including the dark ones.

Preferences schema:
- Bump the persisted `ColorSchemePreferences` schema version. Old payloads load with all new fields null. New payloads round-trip through localStorage and portable `IKeyValueStorage` without loss.

Token JSON for new gradients/text-shadows on rewritten built-ins:
- Not required. Existing token JSON for Gryphy/Forest Legacy/Arcane Wraith/etc. keeps gradient fields null and looks identical to today.
- Blessed Skyhaven and Infernal Covenant ship without gradient definitions in this entry; the implementor may optionally author tasteful gradient stops for these two if the resulting render reads well.

Readable Copy/Paste Preset JSON schema:

Goal: the JSON emitted by the **Copy preset** button must be self-explanatory enough that a user can read it, hand-edit it, and produce a working custom theme without diving into source. **Paste preset** must consume that same readable schema and also still accept the legacy flat-tokens shape so old shared snippets keep working.

Top-level shape (every field except `Name` and `Colors` is optional — paste tolerates omissions):

```json
{
  "$schema": "habitica-tool.color-scheme.v2",
  "Name": "My Theme",
  "Variant": "Light",
  "Description": "Optional free-text note about this theme.",
  "Colors": {
    "PageBackground":       "#f7f1ff",
    "CardBackground":       "rgba(255, 252, 255, 0.94)",
    "CardBorder":           "rgba(103, 49, 184, 0.13)",
    "BodyText":             "#2d2040",
    "SecondaryText":        "#756881",
    "Primary":              "#7334bd",
    "Accent":               "#d99416",
    "Danger":               "#c84a67",
    "Success":              "#2a9277",
    "FocusOutline":         "#438fd0",
    "CardShadow":           "0 18px 44px rgba(32, 17, 54, 0.09)",
    "SurfaceTint":          "rgba(255, 255, 255, 0.72)",
    "SurfaceStrongTint":    "rgba(255, 252, 255, 0.92)",
    "ChartPrimary":         "#7334bd",
    "ChartSecondary":       "#438fd0",
    "TaskNegativeTint":     "#e4d8f6",
    "TaskNeutralTint":      "#f0e9fb",
    "TaskPositiveTint":     "#faf7ff",
    "AppBarBackground":     "#684095",
    "AppBarText":           "#fff8ff",
    "DrawerBackground":     "#3b2356",
    "DrawerText":           "#f8f0ff",
    "ButtonText":           "#ffffff",
    "DisabledBackground":   "rgba(103, 49, 184, 0.06)",
    "DisabledText":         "rgba(117, 104, 129, 0.58)",
    "DisabledBorder":       "rgba(117, 104, 129, 0.22)",
    "InputBackground":      "rgba(255, 252, 255, 0.92)",
    "InputBorder":          "rgba(103, 49, 184, 0.13)"
  },
  "Gradients": {
    "PageBackground": {
      "TopLeft":     "#f7f1ff", "Top":         "#f4ecff", "TopRight":    "#efe4ff",
      "MiddleLeft":  "#f6efff", "Middle":      "#f3eaff", "MiddleRight": "#eee2ff",
      "BottomLeft":  "#f5edff", "Bottom":      "#f1e6ff", "BottomRight": "#ebdcff"
    },
    "Card": {
      "TopLeft":     "...", "Top":         "...", "TopRight":    "...",
      "MiddleLeft":  "...",                       "MiddleRight": "...",
      "BottomLeft":  "...", "Bottom":      "...", "BottomRight": "..."
    },
    "AppBar": {
      "TopLeft":    "...", "Top":    "...", "TopRight":    "...",
      "BottomLeft": "...", "Bottom": "...", "BottomRight": "..."
    },
    "Drawer": {
      "TopLeft":    "...", "Top":    "...", "TopRight":    "...",
      "BottomLeft": "...", "Bottom": "...", "BottomRight": "..."
    },
    "PrimaryButton": {
      "TopLeft":    "...", "TopRight":    "...",
      "BottomLeft": "...", "BottomRight": "..."
    },
    "SecondaryButton": { "Start": "...", "End": "..." },
    "AccentChip":      { "Start": "...", "End": "..." }
  },
  "TextShadows": {
    "Headings": "0 1px 2px rgba(0,0,0,0.35)",
    "AppBar":   null,
    "Drawer":   null
  }
}
```

Field-name principles:
- Self-describing where the role isn't obvious. `Background` → `PageBackground` (clarifies "page, not card"). `Ink` → `BodyText`. `Muted` → `SecondaryText`. `Focus` → `FocusOutline`. `Shadow` → `CardShadow`. `Surface`/`SurfaceStrong` → `SurfaceTint`/`SurfaceStrongTint` (signals they're translucent overlays). `TaskNegative`/`TaskNeutral`/`TaskPositive` → `TaskNegativeTint`/`TaskNeutralTint`/`TaskPositiveTint`.
- Names that are already clear stay: `CardBackground`, `CardBorder`, `Primary`, `Accent`, `Danger`, `Success`, `ChartPrimary`, `ChartSecondary`, `AppBarBackground`, `AppBarText`, `DrawerBackground`, `DrawerText`, `ButtonText`, `DisabledBackground`/`Text`/`Border`, `InputBackground`/`Border`.
- Gradient stop names match cardinal positions used in the C# records (`TopLeft`, `Top`, `TopRight`, `MiddleLeft`, `Middle`, `MiddleRight`, `BottomLeft`, `Bottom`, `BottomRight` for 9-stop; subsets per surface). 2-stop uses `Start`/`End`.
- `Variant` is `"Light"` or `"Dark"` — lower-case acceptable on paste; emit Pascal-case on copy.
- `$schema` is a constant `"habitica-tool.color-scheme.v2"` — the parser uses it to choose the v2 (readable) decoder path; absence means try v1 (legacy flat-tokens) fallback. Do NOT fetch the URL.

Copy preset output:
- Emit pretty-printed (indented) JSON, 2-space indent, ordered exactly as above so a user scanning top-to-bottom learns the layout.
- Include every populated field. Null-valued gradient surfaces and null text-shadows are omitted from the output (cleaner), but the parser must accept explicit nulls on paste.
- `Name` is the editor's current name; `Variant` matches the editor's dark-theme toggle; `Description` defaults to an empty string when the editor has no description field (a description input may be added in a follow-up; for now the field is reserved and ignored on paste).

Paste preset parsing rules:
- v2 detection: `$schema == "habitica-tool.color-scheme.v2"` OR a top-level `Colors` object is present → v2 decoder.
- v1 fallback: when neither marker is present, treat the top-level object as the legacy flat tokens (matches the existing `ColorSchemeClipboardModel` payload and lets old shared JSON snippets keep working).
- Aliases on paste: each readable name accepts the legacy short name too. `PageBackground` ↔ `Background`, `BodyText` ↔ `Ink`, `SecondaryText` ↔ `Muted`, `FocusOutline` ↔ `Focus`, `CardShadow` ↔ `Shadow`, `SurfaceTint` ↔ `Surface`, `SurfaceStrongTint` ↔ `SurfaceStrong`, `TaskNegativeTint`/etc. ↔ `TaskNegative`/etc. Either-or — both present and disagreeing → reject with a clear error.
- Required minimum for paste: `Name` + at least one recognised color in `Colors` (or, for v1 fallback, in the root object). Missing optional groups (`Gradients`, `TextShadows`, `Variant`, `Description`) are treated as null/defaults.
- Partial gradients: a `Gradients.<Surface>` object must contain ALL stops for its surface count, or be omitted entirely. A half-filled gradient is rejected with a specific error naming the surface and the missing stops.
- Unknown fields are ignored silently so future-version JSON pasted into an older build does not hard-fail (the unknown fields are dropped; recognized fields apply).
- Invalid color values surface a precise error pointing at the field path (e.g. `Colors.Primary is not a valid CSS color`).

Help affordance:
- Above the Copy/Paste buttons, add a small `What is this?` disclosure (link or info icon) that, when expanded, shows a 1-screen explainer with:
  - "Each color controls one part of the app — page background, cards, buttons, text."
  - A labelled mini-mock of the app with each field name pointing to where it applies (text + lightweight schematic; no need for pixel-perfect screenshots).
  - A "Tips" line: "Hex (`#aabbcc`), `rgba(...)`, or `color-mix(...)` all work. `CardShadow` is a full CSS `box-shadow` value. `TextShadows.*` is a CSS `text-shadow` value or null."
- Place the explainer above the existing "Copy the preset, tweak the colors, then paste it back…" copy at `ColorSchemePanel.razor:92`.

Implementation notes:
- New types in `Habitica.WebApp/Theme/`:
  - `ReadableSchemeClipboardModel` (v2) with the shape above.
  - `ReadableSchemeParser` — bidirectional: serialize a `ColorSchemeDefinition` → v2 JSON, and parse v2 JSON → `ColorSchemeDefinition`. Wraps the existing legacy parser as the v1 fallback.
- Existing `ColorSchemeClipboardModel` (the v1 record at `ColorSchemePanel.razor:672`) stays for legacy paste only — the Copy button switches to emit v2.
- Extend the existing `SchemeParseResult` enum with `PartialGradient` and `ConflictingAliases` cases; extend the user-facing message map in `DescribeParseFailure`.
- All gradient and text-shadow nulls are preserved exactly through copy → paste → save → reload → copy.

Acceptance (added to the existing Acceptance block — see below):
- Copy preset emits indented JSON with `$schema`, `Name`, `Variant`, optional `Description`, `Colors`, optional `Gradients`, optional `TextShadows` exactly as documented.
- Field names in the emitted JSON match the readable schema (e.g. `PageBackground`, `BodyText`, `SecondaryText`, `FocusOutline`, `CardShadow`, `SurfaceTint`).
- Paste accepts the v2 readable schema, the v1 legacy flat-tokens shape, and the readable schema with legacy aliases mixed in (when not conflicting).
- Paste rejects partial gradients with a message naming the surface and missing stops.
- Paste rejects conflicting alias pairs (e.g. both `PageBackground` and `Background` set to different values) with a clear error.
- Round-trip: copy → paste yields a byte-identical `ColorSchemeDefinition` (including all gradient and text-shadow fields).
- The `What is this?` explainer renders above Copy/Paste and references every field in the schema by its readable name.

Random theme generator + chaos slider — gradient extension:

The existing generator emits solid tokens only. After this entry ships, random themes would look flat next to gradient-rich built-ins — visually inconsistent in the catalog UX. Extend the generator so random themes ship with gradient stops too. **Calm should feel premium and subtle; Madness should genuinely look chaotic and clashy.** Do not flatten Madness in the name of safety; the user opts into chaos by dragging the slider.

- `GenerateRandomTheme(seed, chaos)` derives each gradient stop deterministically from the seed and chaos value. Per-corner offsets perturb hue and lightness around the matching solid token. Seed determinism preserved: same `(seed, chaos)` reproduces every stop exactly.
- Generator emits gradient definitions for `PageBackground` (9 stops), `Card` (8 stops), `AppBar` (6 stops), `Drawer` (6 stops), `PrimaryButton` (4 stops), `SecondaryButton` (2 stops), `AccentChip` (2 stops). All seven gradient-capable surfaces.
- Generator does NOT emit text-shadow tokens. Random text shadows hurt readability too easily; leave `HeadingTextShadow`/`AppBarTextShadow`/`DrawerTextShadow` null. Authors can add shadows by hand in the custom editor.

Per-surface chaos scaling (chaos `c` ∈ [0, 1], piecewise — gentle Calm side, unhinged Madness side):

| Surface | Calm (c ≤ 0.6) | Madness ramp (0.6 < c ≤ 1.0) | Rationale |
|---|---|---|---|
| `PageBackground` | `c` (full) | `c * 1.4` at top | Decorative. Goes nuclear at Madness. |
| `Card` | `c * 0.5` (tame) | linear ramp to `c * 1.2` at c=1.0 | Calm side stays readable for daily use; Madness side becomes deliberately wild — text may swim. The feature, not a bug. |
| `AppBar`, `Drawer` | `c` | `c * 1.3` at top | Chrome bands lean into clash at high end. |
| `PrimaryButton` | `c * 0.8` | `c * 1.2` at top | Calm preserves ButtonText contrast; Madness lets buttons scream. |
| `SecondaryButton`, `AccentChip` | `c` | `c * 1.5` at top | Small surfaces. Pure carnival at Madness. |

Easing curve on the base chaos value BEFORE per-surface scaling: `c_eff = c^0.55`. Aggressive top-end push so the last quarter of the slider feels like falling off a cliff. Calm-side movement stays subtle for premium-looking low-chaos themes.

High-chaos hue divergence (no joke):
- At `c > 0.6`, target a minimum hue gap of 90° between `Primary` and `Accent`.
- At `c > 0.85`, push the gap to 150°–180° (deliberate clash). `Danger` and `Success` similarly allowed to abandon the red/green convention — Danger may go cyan, Success may go magenta if the seed says so.
- At `c > 0.9`, saturation boost: solid token saturations driven to 0.85–1.0 in HSL space. Themes look neon, not pastel.
- At `c > 0.95`, allow `BodyText` and `AppBarText`/`DrawerText` to drift toward saturated colors instead of safe near-neutral. Combined with wild backgrounds this is genuinely hard to read — intentional.

Per-corner gradient hue range:
- Calm (`c ≤ 0.5`): per-corner hue rotation capped at ±25°. Subtle directional shading.
- Mid (`0.5 < c ≤ 0.85`): cap at ±90°. Visible color variation per corner.
- Madness (`c > 0.85`): cap at ±180°. Opposite-hue corners ARE allowed. Bilinear midpoints may interpolate through grey, and that grey wash IS part of the Madness look. Embrace the mud.

High-chaos text shadows (NEW exception to "generator emits no text shadows"):
- At `c > 0.85` ONLY, the generator emits randomized `HeadingTextShadow` with a strong colored blur (e.g. `0 0 18px rgba(<saturated-hue>, 0.85)`). Makes headings glow / smear at Madness. Below 0.85, shadows stay null.
- `AppBarTextShadow` / `DrawerTextShadow` stay null even at Madness — those bars are small and shadows there break too obviously.

Contrast guard — tiered, with Madness usability floor:

- For `c ≤ 0.6` (Calm/Lively): enforce on Card stop average vs `BodyText` ≥ WCAG 3.0. Nudge uniformly toward `CardBackground` if the average fails.
- For `0.6 < c ≤ 0.85` (Wild): relaxed minimum (WCAG 2.0 large-text contrast). Stops can drift further.
- For `c > 0.85` (Madness): the per-surface chaos guards drop, BUT the **Madness usability floor** below stays on. The point is "visually chaotic, still usable" — not "broken."

Madness usability floor — "usable enough, not pretty" (always on, even at `c = 1.0`):

The point: at Madness the app looks insane but the user can still find the Settings page to pick a sane theme. Not a full WCAG guarantee — just enough handholds to navigate out.

1. **AppBarText vs the average AppBar gradient** — minimum contrast 3.0:1. Top nav stays readable. (AppBar is small enough that even the worst stop usually averages OK; we only floor the mean.)
2. **DrawerText vs the average Drawer gradient** — minimum contrast 3.0:1. Side menu stays navigable so users can reach Settings.
3. **ButtonText vs PrimaryButton gradient average** — minimum contrast 3.0:1. Primary actions findable. SecondaryButton not floored — secondary actions may be invisible at Madness; that is fine.
4. **FocusOutline vs both PageBackground average AND CardBackground** — minimum contrast 2.5:1. Keyboard nav never fully invisible.
5. **Danger distinguishable from Primary** — either hue separation ≥ 30° OR luminance separation ≥ 0.15. Destructive actions identifiable. Loose floor; lets Danger go cyan if Primary is red.

What breaks loose at Madness (by design):
- PageBackground per-corner stops including opposite-hue corners. Mud midpoints OK.
- Card gradient AND Card text legibility — Card surface IS allowed to swim. Per-corner stops can clash hard. BodyText may be hard to read on cards. User will scroll, squint, and laugh; the app is still navigable via top bar and drawer.
- Solid token saturation (0.85–1.0 HSL).
- Heading text shadow color and blur (cap blur ≤ 22px so glyph outlines survive somewhere on the page).
- Hue identity of Primary/Accent/Danger/Success — convention abandoned.
- Input borders, disabled-state distinction, card shadows, surface tints — all free to clash.

Enforcement:
1. Roll all stops from `(seed, c)` with the per-surface chaos curves.
2. Check the 5 usability floors. For each failing floor, nudge ONLY that single token (e.g. just `AppBarText`) along the minimum perceptual path until it passes. Do not cascade nudges; chaos on every other channel survives untouched.
3. No re-rolls. The seed must remain deterministic, and the nudges above are bounded — they always succeed in one pass for the 5 listed floors.

Net result: at Madness the dashboard background pulses, cards clash, buttons scream, headings glow — but the app bar stays readable, the drawer is navigable, primary buttons are findable, focus rings are visible, and Danger never blurs into Primary. Five handholds. Everything else is chaos.

Slider label thresholds — keep the existing 10-bucket ladder at `ColorSchemePanel.razor:177-189`. Do NOT collapse to 4. The ladder reads as escalating intensity:

| Threshold | Label | Vibe |
|---|---|---|
| `c ≤ 0.01` | CALM | Solid-only feel; gradients barely perceivable |
| `c < 0.12` | MELLOW | Subtle directional shading |
| `c < 0.24` | BREEZY | Visible but tasteful |
| `c < 0.36` | LIVELY | Premium-magazine palette |
| `c < 0.50` | WILD | Bold corners, still readable |
| `c < 0.62` | FERAL | Saturation climbing, hues drift |
| `c < 0.74` | FRENZIED | Convention loosening, Card stops clash |
| `c < 0.86` | CHAOTIC | Heading shadows arrive, hues clash hard |
| `c < 0.99` | UNHINGED | Saturation maxed, hue identity abandoned |
| else | MADNESS | Floors only — handholds + chaos |

Chaos-tier breakpoints used in this entry map onto the existing ladder, not new bands:
- "Calm" chaos curve range = CALM through LIVELY (`c ≤ 0.36`).
- "Wild" range = WILD through FRENZIED (`0.36 < c ≤ 0.74`).
- "Madness" floor activates at CHAOTIC and above (`c > 0.85`), matching where heading text-shadows turn on.
- The 5-handhold usability floor is enforced at MADNESS specifically (`c > 0.99`); UNHINGED and CHAOTIC still apply the tiered guards. Adjust the spec phrasing to read against these ladder bucket names, not the 4-tier shorthand.

Existing test `Same_seed_reproduces_the_same_theme_so_chaos_slider_is_reversible` must still pass. Add tests: gradient stops emitted at every chaos level; two seeds produce distinct gradient sets; piecewise per-surface chaos curve respected (snapshot specific multipliers at c=0.3, 0.6, 0.85, 1.0); high-chaos hue divergence between Primary/Accent at c=0.95 ≥ 150°; text-shadow emitted only at c > 0.85; contrast guard fires at c=0.5, relaxes at c=0.7, skipped at c=0.95; saturation boost at c > 0.9 measurable in HSL.

Live chaos slider performance:
- `AdjustRandomChaosAsync` re-runs the generator on every slider tick, then `applyColorScheme` re-paints the canvases. Cost per tick: 5 × (canvas paint + `toDataURL`) ≈ 2.5 ms desktop, ~8 ms low-end mobile. Comfortably under one 60 Hz frame (16.67 ms).
- No throttling/debouncing on the slider — the cost is small enough that live-morph stays smooth.
- If profiling on low-end mobile shows dropped frames, fall back to `requestAnimationFrame` coalescing in the slider handler. Do NOT add `setTimeout` debouncing — that makes the slider feel laggy.

Out of scope:
- renaming or removing existing token fields in `ColorSchemeTokens` (additive only);
- gradients on inline icons (SVG `currentColor` fill);
- random-generator emission of text-shadow tokens (left null; user can author in editor);
- changing the chaos slider's seed-replay semantics or its Calm-to-Madness labels;
- changing portable-sync storage keys (only the value schema is bumped to carry the variant flag and the new optional fields);
- changing DI registration or the JS interop function signature.

Acceptance:
- Built-in catalog matches the new list and order. Light and dark count are equal.
- Gryphy Light/Dark tokens match the provided JSON exactly.
- `alpha` is renamed to `forest-legacy` and stored preferences referencing `alpha` continue to resolve through the migration.
- `habitica`, `mana-mirage`, `mushroom-meadow`, `mushroom-trip`, `sugar-crash`, `neon-rogue`, `neon-abyss-carnival` are absent from `BuiltInSchemes`.
- New schemes `arcane-wraith`, `phantom-fair`, `toxic-swamp`, `green-menace`, `abyssal-blackwater`, `obsidian-glow`, `blessed-skyhaven`, `infernal-covenant` are present with the provided tokens.
- Deleting a custom preset persists across page reload; the deleted scheme does not reappear.
- Deleting the active scheme falls back to `gryphy-light` if the deleted scheme was Light, `gryphy-dark` if Dark.
- Custom-scheme editor exposes a Dark-theme toggle; saved variant survives reload and portable sync.
- `ColorSchemePanel` renders sections in order Default → Built-in Light → Built-in Dark → Custom → Generated. Empty Custom and Generated sections are hidden; Default and Built-in Light/Dark are always shown.
- `ColorSchemeTokens` exposes the new nullable gradient fields (`BackgroundGradient`, `CardGradient`, `AppBarGradient`, `DrawerGradient`, `PrimaryButtonGradient`, `SecondaryButtonGradient`, `AccentChipGradient`) and text-shadow fields (`HeadingTextShadow`, `AppBarTextShadow`, `DrawerTextShadow`). All retained built-ins still load with these fields null and render identically to before.
- `colorSchemes.js` emits per-stop CSS variables AND composed `--bg-gradient` / `--card-gradient` / `--appbar-gradient` / `--drawer-gradient` / `--primary-btn-gradient` / `--secondary-btn-gradient` / `--accent-chip-gradient` variables. Multi-corner surfaces (9/8/6/4) compose as `url("data:image/png;base64,...") center/100% 100% no-repeat, <solid-fallback>` using the canvas-painted data-URL recipe. 2-stop surfaces compose as `linear-gradient(180deg, <start>, <end>)`. When a scheme defines no gradient for a surface, the composed variable resolves to the solid token fallback so existing CSS keeps working.
- Canvas-painted data URLs are recomputed only on scheme switch, not per frame. Total data-URL footprint stays under 2 KB across all surfaces.
- `app.css` consumes the composed gradient variables on body, `.card-surface`/`.dashboard-panel`, app bar, drawer, primary/secondary MudButton backgrounds, and accent chips. Disabled buttons keep `var(--disabled-bg)`; `:focus-visible` outline remains visible above any gradient. Headings, app-bar text, and drawer text consume their respective text-shadow variables.
- Custom-scheme editor exposes collapsible advanced sub-panels for each gradient-capable surface (background, card, app bar, drawer, primary button, secondary button, accent chip) and a text-shadow input subgroup. Stop count matches the surface type (9/8/6/4/2). Disabled/empty inputs persist as null and the composed variables fall back to the solid token.
- Native checkboxes and radios use `accent-color: var(--primary)` globally. Verify in PARTY SYNC SETTINGS → Shared queue controls, Spells "Do not warn again", and Dashboard "Auto equip" — all four/all visible checkboxes pick up the theme primary color instead of system blue.
- Persisted preferences schema bump; old payloads load with nullable fields null; new payloads round-trip through localStorage and portable `IKeyValueStorage` without loss.
- Tests cover built-in membership, ordering, removed-schemes absence, light/dark count parity, custom variant persistence (including portable sync round-trip), delete-persistence regression, deletion-fallback per variant, legacy-id migration on load, gradient/text-shadow nullability (load with all-null and reject malformed partial gradients), JS interop emission of composed variables with and without gradient stops, and a CSS regression test that bare `<input type="checkbox">` resolves `accent-color` to the active scheme's primary.
- Copy preset emits the v2 readable JSON (`$schema`, `Name`, `Variant`, optional `Description`, `Colors`, optional `Gradients`, optional `TextShadows`) with readable field names (`PageBackground`, `BodyText`, `SecondaryText`, `FocusOutline`, `CardShadow`, `SurfaceTint`, etc.). All token fields exposed by the post-overhaul model — including the new gradient and text-shadow fields — appear in the readable schema.
- Paste preset accepts both v2 readable JSON and the v1 legacy flat-tokens shape. Legacy aliases (`Background`, `Ink`, `Muted`, `Focus`, `Shadow`, `Surface`, `SurfaceStrong`, `TaskNegative`, `TaskNeutral`, `TaskPositive`) are accepted on paste only.
- Paste rejects partial gradients (missing stops) with a message naming the surface and missing stop list; rejects conflicting alias pairs (both modern and legacy name set to different values) with a clear error.
- Round-trip copy → paste yields a byte-identical `ColorSchemeDefinition`, including all gradient and text-shadow fields. Nulls survive the round-trip.
- The `What is this?` explainer renders above Copy/Paste and references every field in the readable schema by its readable name.
- Tests cover Copy emission shape, v2 paste, v1 legacy paste, alias paste, conflicting-alias rejection, partial-gradient rejection, unknown-field tolerance, and round-trip equivalence.

### Dashboard Navigation Card Title/Description Spacing

Goal: fix navigation link cards (Companion and Habitica link sections) rendering title and body with no separation — e.g. "TasksScore and inspect cached tasks." should read as a title line plus a description line.

Touch:
- `src/Habitica.WebApp/Pages/DashboardPage.razor` (`RenderDashboardLink`, ~line 852 and link cards ~268-271)
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/`

Out of scope:
- changing nav targets, labels, or descriptions;
- redesigning the cards beyond title/body separation.

Acceptance:
- Every navigation link card renders title and description as distinct lines/elements with visible spacing.
- Applies to all affected nav menus (Companion and Habitica link cards).
- Test asserts title and description are separate nodes (not concatenated text).

### Compact Task Cards

Goal: shrink task cards to ease working through the task list. Collapsed card shows only task title and description plus move buttons and a Details toggle; all other current task info hides behind Details.

Touch:
- `src/Habitica.WebApp/Pages/TasksPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/`
- `FEATURES.md`
- reference `docs/UX_UI_MANIFEST.md` (do not violate its density/affordance rules)

Out of scope:
- removing any existing task detail data — only hide it behind Details;
- changing scoring/checkoff/reorder logic or freshness gates;
- changing task filters.

Acceptance:
- Collapsed task card shows title, description, move-card buttons (repositioned for the smaller card), and a Details toggle only.
- Details reveals all previously-visible per-task information; nothing is lost.
- Move buttons and keyboard reordering still function with the new layout.
- Layout follows `docs/UX_UI_MANIFEST.md`.
- Tests cover collapsed vs expanded rendering and move buttons still present.

### Spells Auto-Equip Best Option Default With Dropdown

Goal: when a spell's auto-equip feature has multiple equipment options, default-select the most profitable option and offer the rest via a dropdown sorted most→least profitable.

Touch:
- `src/Habitica.WebApp/Pages/SpellsPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- spell equipment-recommendation logic under `src/Habitica.Rules` / `src/Habitica.Application` (only the selection/ordering surface; do not change scoring formulas)
- direct tests under `tests/Habitica.WebApp.Tests/Pages/SpellsPageTests.cs` and rule tests under `tests/`
- `FEATURES.md`

Out of scope:
- changing how profitability/stat deltas are computed;
- changing cast execution order or CRON-warning semantics;
- changing two-handed weapon pairing logic.

Acceptance:
- With multiple options, the most profitable option is preselected.
- A dropdown lists remaining options sorted most→least profitable.
- Selecting a non-default option updates the equip plan; single-option case shows no dropdown.
- Tests cover default selection, dropdown ordering, and selection change.

### Split Party Page Into Party And Quests Pages

Goal: relieve Party-page overload by separating quest-heavy blocks onto a dedicated Quests page, reusing existing data/logic.

Touch:
- `src/Habitica.WebApp/Pages/PartyPage.razor`
- new `src/Habitica.WebApp/Pages/QuestsPage.razor`
- `src/Habitica.WebApp/Components/Navigation/AppNavMenu.razor` and Dashboard nav cards in `DashboardPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/`
- `FEATURES.md`
- `docs/UX_UI_MANIFEST.md` if navigation guidance changes

Layout split:
- `Party` page keeps: party description/info, PARTY SYNC ROLES, PARTY SYNC SETTINGS, a small quest card linking to the Quests page, members list, buff info, CRON graph.
- `Quests` page holds: all quest and quest-queue-related blocks (active quest, shared pool, queue, voting, recent completions, quest controls). Update intra-quest-card links to point at the Quests page.

Out of scope:
- new database fields or party-sync data-contract changes;
- changing quest/queue/sync logic, permissions, or stale-data guards;
- changing Habitica party/quest links.

Acceptance:
- Quest blocks move to a new Quests page; Party page retains the listed sections plus a quest summary card linking to Quests.
- All existing actions keep current authorization and freshness guards; no schema change.
- Quest-related links updated to the Quests page.
- Navigation exposes the Quests page; related docs updated.
- Tests cover both pages rendering their sections and at least one guarded quest action still working on the Quests page.

### Party Sync Tokenized Invite Proofs

Goal: add an optional manager-issued party-sync proof mode. Parties continue to work with browser-only `local-claim-v1` by default, but an owner/app admin can enable tokenized invite proofs so shared party queue access no longer depends only on client-supplied local claim headers.

Touch:
- `functions/api/party-sync/[partyId].js`
- `src/Habitica.WebApp/wwwroot/js/sync/cloudflarePartySync.js`
- `src/Habitica.WebApp/State`
- `migrations/`
- direct tests under `tests/Functions/` and `tests/Habitica.WebApp.Tests/`
- `TECHNICAL.md`
- `FEATURES.md`
- `docs/DEPLOY_CLOUDFLARE_PAGES.md`

Implementation shape:
- Add a D1 migration for invite-proof state. Store party id, proof id or token hash, display label, issued/revoked/expires timestamps, issuer metadata, and an enabled/disabled party setting. Do not store raw reusable proof tokens if a hash is enough.
- Keep `local-claim-v1` as the default and as the recovery path. If tokenized proof mode is disabled or no active proof exists, existing party-sync behavior must remain unchanged.
- Add owner/app-admin management actions to create, list, revoke, rotate, remove, enable, and disable tokenized proofs. Existing Officer permissions should not automatically grant proof-management powers unless the code explicitly already treats the caller as owner/app admin.
- Extend `readAccessProof()` to parse both `local-claim-v1` and the new proof version. Extend `resolvePartySyncAccess()` so tokenized proof identity still passes through the same owner/admin/Officer/kick checks used by local claims.
- Update the browser sync bridge to send the new proof headers only when local state has an active tokenized proof. Do not send Habitica API tokens, raw credentials, or authorization headers to Cloudflare.
- Surface concise UI/state feedback for proof mode: disabled, enabled, active proof, revoked/expired proof, and fallback to local claim.

Out of scope:
- sending Habitica API tokens to Cloudflare;
- changing role names (`app admin`, `party owner`, `Officer`);
- removing the existing `local-claim-v1` reader;
- replacing party-sync roles, queue permissions, or kick semantics;
- requiring tokenized proofs for existing parties by default.

Acceptance:
- With no invite proof created, and with tokenized mode disabled, all existing party-sync reads/writes still work through `local-claim-v1`.
- Owner/app admin can enable and disable tokenized proof mode.
- Owner/app admin can create, list, revoke, rotate, and remove invite proofs without exposing Habitica credentials. Removing the active proof invalidates the old proof; the party can issue a new proof later and falls back to browser-only `local-claim-v1` while no active proof exists.
- `readAccessProof()` accepts both the new proof version and `local-claim-v1`; unsupported proof versions still fail with a clear 401.
- `resolvePartySyncAccess()` rejects malformed, expired, revoked, wrong-party, and kicked-user tokenized proofs.
- Owner/app-admin recovery remains possible when tokenized proofs are missing, expired, revoked, or misconfigured.
- Frontend bridge sends tokenized proof headers only when an active proof is available, and otherwise keeps the existing local-claim headers.
- Worker tests cover: local-claim fallback, valid proof, malformed proof, expired proof, revoked proof, removed proof, wrong-party proof, kicked-user rejection, owner/admin bypass/recovery, enable/disable mode behavior, and rotate invalidating the old proof.
- WebApp tests cover proof-mode state mapping and header selection without sending Habitica API tokens to Cloudflare.

### Active Quest Metadata And Detail Affordances

Goal: fill remaining active quest card metadata and drill-ins when Habitica or cached shared state exposes the data.

Touch:
- `src/Habitica.Api`
- `src/Habitica.Domain/Party`
- `src/Habitica.WebApp/Pages/PartyPage.razor`
- direct tests under `tests/`
- `FEATURES.md`

Out of scope:
- mobile app deep links; keep web fallback from `docs/HABITICA_DEEPLINKS.md`;
- fake values when Habitica data is missing.

Acceptance:
- Active quest snapshot preserves nullable owner/starter and started-at fields when the API or shared queue state exposes them.
- Active quest card shows owner or starter, started date, details view, participants view, and rewards/details affordances when cached data exists.
- Missing owner/starter/started-at fields render concise unavailable states without inventing values.
- Participant names use the same member-detail focus behavior as the party member list.

### Pets And Mounts Page With Bulk Sell Planner Relocation

Goal: build a dedicated Pets & Mounts page that surfaces per-pet/per-mount ownership, fast equip, search, missing-collection gaps, market-status hints, and feed-with-best-food. Move the BULK SELL PLANNER from the Inventory page onto this new page.

Touch:
- new `src/Habitica.WebApp/Pages/PetsMountsPage.razor`
- `src/Habitica.WebApp/Pages/InventoryPage.razor` (remove bulk-sell UI block and its helpers; preserve all other inventory behavior)
- `src/Habitica.WebApp/Pages/DashboardPage.razor` (add a `RenderDashboardLink("Pets & Mounts", ...)` nav card around line 286-289)
- `src/Habitica.WebApp/Components/Navigation/AppNavMenu.razor` (add Pets & Mounts entry between Inventory and Party)
- `src/Habitica.Api/HabiticaApiClient.cs` and `src/Habitica.Api/IHabiticaSyncClient.cs` (add `FeedPetAsync`, `EquipPetAsync`, `EquipMountAsync`, `HatchPetAsync`; surface per-key pet/mount ownership maps and food/hatching-potion ownership in the user snapshot mapper)
- `src/Habitica.Domain/User/UserSnapshot.cs` (extend `InventorySnapshot` with `OwnedPets`, `OwnedMounts`, food/egg/hatching-potion per-key maps if not already present; small additive change, additive nullable defaults to preserve existing call sites)
- `src/Habitica.Domain` catalog: pets/mounts/food catalog records (egg group, potion group, favorite-food mapping). Static data, derive from a checked-in catalog file rather than a live API call.
- `src/Habitica.WebApp/State/AppSessionController.cs` (`FeedPetAsync`, `EquipPetAsync`, `EquipMountAsync` orchestration mirroring `BuyHealthPotionAsync` — fresh-state guard, sequential execution with stop-on-failure, post-action `GetUserSnapshotAsync` refresh, diagnostics logging)
- `src/Habitica.Storage/StorageKeys.cs` (new local-only key `PetsMountsViewPreferences`; NOT added to `PortableDataKeys`)
- direct tests under `tests/Habitica.WebApp.Tests/Pages/PetsMountsPageTests.cs`, `tests/Habitica.WebApp.Tests/Pages/InventoryPageTests.cs` (assert bulk-sell removal), rule tests for feed-recommendation ordering
- `HABITICA_API.md` if new contract details are pinned down during implementation
- `FEATURES.md`
- `docs/UX_UI_MANIFEST.md` if a new page-level guidance is added

Habitica endpoints (already documented in `HABITICA_API.md:270-275`):
- `POST /user/feed/:pet/:food` — supports `?amount=<n>`
- `POST /user/equip/pet/:key` and `POST /user/equip/mount/:key` (via `/user/equip/:type/:key`)
- `POST /user/hatch/:egg/:hatchingPotion`
- Pets/mounts/food/eggs/hatchingPotions data already flows through the user snapshot endpoint; the API mapper currently keeps only counts. Extend it to capture per-key ownership maps. If a needed endpoint is not documented, stop and add a follow-up entry rather than guessing.

Feature shape:
- Groups: pets and mounts grouped by egg family (e.g. base, magic-potion, quest, premium) plus a separate hatching-potion section. Group names come from a static catalog. Empty groups still render with an empty-state hint.
- Each group is foldable. Folded state persists to local browser storage via `PetsMountsViewPreferences` (NOT portable sync). Survives reload; not synced across devices.
- Search box filters across all pets/mounts/potions by key and display name.
- Missing-collection view per group: list not-yet-owned pets/mounts and indicate hatching ingredients still needed (egg + potion missing from inventory). Display "ready to hatch" when both ingredients are owned.
- Market-status hints derived only from current inventory + catalog: "can hatch X with current inventory", "need egg Y" or "need potion Z to complete this group". Do NOT scrape live prices or invent gem costs.
- Feed UI: select a pet → food dropdown pre-sorted by recommendation (favorite food for the pet's egg group first, then generic food, then non-matching). Allow a multi-food queue with pre-feed preview. Execute sequentially with stop-on-failure. Refresh user snapshot after the queue finishes.
- Fast equip buttons on every owned pet/mount card; refresh snapshot after equip.
- Bulk sell planner is moved verbatim — same plan computation, sell execution, diagnostics, and refresh hooks. Inventory page must no longer render it. Diagnostics area stays `Inventory` (renaming the area is out of scope; a follow-up entry may rename it).

Out of scope:
- syncing per-pet/per-mount ownership maps to Cloudflare app-data sync;
- release-pets / release-mounts actions;
- gem-currency purchases (covered by the Dashboard buy-gems entry);
- changing existing sell execution, confirmation copy, or sell-result UX;
- redesigning the Inventory page beyond removing the bulk-sell block and any helpers that become dead code.

Acceptance:
- Pets & Mounts page is reachable from the Dashboard nav card and the side nav.
- Pet, mount, and hatching-potion groupings render with foldable state. Folded state survives reload from local storage; the value does NOT appear in any portable-sync payload.
- Search filter narrows visible entries across all groups by display name and key.
- Missing-collection view enumerates not-owned pets/mounts and the hatching ingredients still needed.
- Market-status hints only reference items derivable from current inventory + catalog.
- Feed action shows a pre-feed preview, supports a multi-food queue, runs sequentially with stop-on-failure, and refreshes the snapshot afterwards.
- Fast-equip changes the current pet/mount and refreshes the snapshot.
- Bulk sell planner appears on Pets & Mounts with identical behavior; Inventory page no longer renders it.
- No Habitica API tokens are forwarded to Cloudflare. No portable-sync entry is added for folded-group state.
- Tests cover: empty pets/mounts rendering, group rendering, fold persistence (with mocked storage), search filter, missing-collection enumeration, feed dry-run preview, multi-food queue failure handling, fast-equip success, bulk-sell relocation rendering, and Inventory-page bulk-sell removal.

### Dashboard Spend Gold Buy Gems Action

Goal: add a "Buy gems with gold" action inside the Dashboard SPEND GOLD section. Visible only when the Habitica user is eligible to convert gold to gems (subscription-gated and respecting Habitica's monthly cap).

Touch:
- `src/Habitica.Api/HabiticaApiClient.cs` and `src/Habitica.Api/IHabiticaSyncClient.cs` (add `PurchaseGemsForGoldAsync(int quantity)` and extend the user snapshot mapper to expose subscription status and remaining gem-for-gold cap when the API provides them)
- `src/Habitica.Domain/User/UserSnapshot.cs` (add nullable `bool? CanBuyGemsForGold` and `int? RemainingGemPurchases`, OR a small `SubscriptionSnapshot` record referenced from `UserSnapshot`; additive nullable fields to preserve existing call sites)
- `src/Habitica.WebApp/Pages/DashboardPage.razor` (SPEND GOLD section around line 483-507; add the gems card and `BuyGemsForGoldAsync` UI handler with `CanBuyGems()` validation, mirroring `BuyArmoireAsync`/`CanBuyArmoire`)
- `src/Habitica.WebApp/State/AppSessionController.cs` (`BuyGemsForGoldAsync` orchestration mirroring `BuyHealthPotionAsync` and `BuyArmoireAsync`: fresh-state guard, sequential per-gem execution if multi, refresh snapshot, diagnostics)
- direct tests under `tests/Habitica.WebApp.Tests/Pages/DashboardPageTests.cs` and `tests/` for the controller orchestration
- `HABITICA_API.md` (pin endpoint shape and quantity behavior if the implementation confirms the bulk-quantity body parameter works)
- `FEATURES.md`

Habitica endpoint:
- `POST /user/purchase/gems/gem` with optional `quantity` body parameter — already documented at `HABITICA_API.md:269` and `:282-288`.
- Cost: 20 GP per gem (Habitica default). Monthly cap depends on subscription tier; if the cap is not present on the user snapshot, rely on Habitica's error response and degrade gracefully.

UI shape:
- New card titled "Buy gems with gold" inside the Spend Gold block. Hidden entirely when `Snapshot.CanBuyGemsForGold != true`.
- Quantity input clamped to `min(floor(Gold / 20), RemainingGemPurchases ?? floor(Gold / 20))`.
- Explicit confirmation modal/inline confirm required before purchase (per `HABITICA_API.md:290`: "Do not expose destructive or premium-currency actions without explicit confirmation").
- If the bulk `quantity` body parameter is verified to work in one call, send a single request. Otherwise loop sequentially per gem with stop-on-failure. Document the chosen path in `HABITICA_API.md` during implementation.
- Snackbar result + refresh of user snapshot on success; surface remaining cap and updated gem balance.
- Diagnostics logging under `DiagnosticsFeatureArea.Inventory` (rename to `Currency` is out of scope; follow-up acceptable).

Out of scope:
- selling gems back for gold;
- mystery hourglass purchase;
- subscription management UI beyond the gem-for-gold eligibility gate;
- exposing other subscription perks anywhere else in the UI;
- adding a dedicated currency page.

Acceptance:
- Buy-gems card appears only when the user is eligible to buy gems for gold; otherwise the SPEND GOLD section renders unchanged.
- Quantity input clamps to the affordable maximum and the remaining monthly cap when the cap is known.
- Action requires explicit confirmation. Cancel keeps state untouched.
- Successful purchase refreshes the user snapshot and updates the displayed gold and gem totals.
- Failed purchase (e.g. cap reached, API error) surfaces a concise error and stops further per-gem requests when looping.
- No Habitica credentials are forwarded to Cloudflare.
- Tests cover: card hidden when ineligible, visible when eligible, quantity clamp, confirmation gate, success refresh, partial failure during multi-gem sequence, and snapshot mapping for the new subscription fields.

### Inline Unfinished Dailies Mini List In CRON Blocks

Goal: surface unfinished dailies inside the CRON-action panel on the Dashboard and the CRON buff-warning card on Spells, so the user can knock dailies out in place before CRON without leaving context.

Touch:
- `src/Habitica.WebApp/Components/` — new `CronUnfinishedDailiesMiniList.razor` shared component
- `src/Habitica.WebApp/Pages/DashboardPage.razor` (`cron-action-panel` at line ~112; insert mini list before the confirmation panel at ~188)
- `src/Habitica.WebApp/Pages/SpellsPage.razor` (`spell-cron-warning` at line ~198; insert collapsed disclosure with mini list)
- `src/Habitica.WebApp/wwwroot/css/app.css`
- `src/Habitica.WebApp/State/AppSessionController.cs` (reuse `ScoreTaskAsync` at line ~1674; no new orchestration method unless a multi-tick helper is required)
- direct tests under `tests/Habitica.WebApp.Tests/Pages/DashboardPageTests.cs`, `tests/Habitica.WebApp.Tests/Pages/SpellsPageTests.cs`, and `tests/Habitica.WebApp.Tests/Components/CronUnfinishedDailiesMiniListTests.cs`
- `FEATURES.md`
- `docs/UX_UI_MANIFEST.md` if a new shared compact-task pattern is documented

UX decision (the "think on this hard" call):
- Dashboard: render the mini list inline inside `cron-action-panel`, expanded by default. The Start-New-Day flow already invites the user to act before CRON; an inline list reinforces "finish what you can before CRON" and is reachable without scrolling away from the gear optimizer.
- Spells: keep the CRON warning card visually compact. Render the mini list inside the warning, but collapsed behind a disclosure labeled `"N dailies due — show"` (singular `"1 daily due — show"`). Expand reveals the same compact list. Reasoning: the spell card flow's primary action is "Start New Day and Cast"; cluttering it with full daily controls every time risks pushing the cast actions below the fold. Disclosure preserves option without overwhelming the default state.
- Do NOT redirect the Spells flow to the Dashboard. The redirect breaks the cast intent and forces re-context.
- Hide the mini list (do not render the disclosure at all) when there are zero unfinished dailies due today.

Shared component shape (`CronUnfinishedDailiesMiniList.razor`):
- Parameter: `IReadOnlyList<TaskSnapshot> Dailies` (already-filtered to due-and-unfinished today by the parent).
- Parameter: `EventCallback<TaskSnapshot> OnComplete` — parent wires it to `SessionController.ScoreTaskAsync(task, TaskScoreDirection.Up)`.
- Parameter: `bool StartCollapsed` — default false on Dashboard, true on Spells.
- Renders: section label `"Unfinished dailies"`, a count badge, and a compact row per daily with:
  - daily title (truncated with title attribute)
  - difficulty indicator (small)
  - check button (`✓` icon) that calls `OnComplete`
- Row layout: single line, no notes, no checklist sub-items, no history chart — strictly lightweight. The full Tasks-page card remains the place for detail.
- Busy/disabled state: while `SessionController.State.IsBusy`, disable check buttons. After a successful score, the row visually crosses out or removes itself; final list reflects the live snapshot on next refresh.
- Stale-data guard: if `SessionController.State.TaskFreshness != SnapshotFreshnessState.Fresh`, replace the check buttons with a small "Refresh tasks to check off" hint linking to the existing refresh control. Do not silently submit against stale tasks.

Filtering logic:
- "Unfinished daily due today" = `TaskSnapshot.Type == TaskType.Daily && !IsCompleted && due-today`. Reuse the existing `incompleteDailies` selection from `src/Habitica.Application/Dashboard/PendingDamageEstimateFactory.cs:44` if it already encodes the "due today" rule; otherwise extend that selection in the factory and have both the damage estimate and the mini list source from it. Do NOT invent a parallel "due today" filter that could drift from the damage estimate.

Out of scope:
- Habits and To-Dos (only Dailies);
- checklist sub-item handling (a daily with a checklist still ticks as a single score-up);
- bulk "check all" action;
- changing the Start-New-Day confirmation copy beyond the mini-list addition;
- changing the buff-timing warning copy beyond adding the disclosure;
- changing `ScoreTaskAsync` semantics or freshness guards;
- adding the mini list to any other page (Party, Inventory, Settings, etc.).

Acceptance:
- When `snapshot.NeedsCron == true` and at least one due daily is incomplete, the Dashboard `cron-action-panel` renders the mini list expanded between the gear optimizer and the confirmation panel.
- When `snapshot.NeedsCron == true` triggers a `spell-cron-warning` and at least one due daily is incomplete, the warning card renders a collapsed disclosure showing `"N daily/dailies due — show"`. Expanding reveals the same compact list.
- Mini list is hidden entirely when zero due dailies are incomplete.
- Each row's check button calls `ScoreTaskAsync(task, TaskScoreDirection.Up)`; on success, the row disappears or visibly resolves and the count badge decrements.
- Stale task freshness disables the per-row check buttons and surfaces a refresh hint instead of submitting against stale state.
- No bulk-check button. No checklist sub-item UI.
- No redirect from Spells to Dashboard introduced by this entry.
- The same data source feeds both the Dashboard mini list and `PendingDamageEstimateFactory.incompleteDailies`. Tests assert they cannot drift.
- Tests cover: Dashboard renders mini list when needs-CRON+unfinished, hidden when zero unfinished, single-row check calls the controller, stale state disables checks, Spells disclosure is collapsed by default and expands on click, both mini lists hidden when not needing CRON, and the count badge updates after a successful score.

## Backlog

These entries are lower priority. Each entry is self-contained and should be promoted into `Prioritized Next Changes` before implementation.

### Party Quest Workspace Modes

Goal: separate the Party page's current quest, shared queue planning, quest pool, recent completions, and member/CRON sections into clearer scan modes.

Touch:
- `src/Habitica.WebApp/Pages/PartyPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/PartyPageTests.cs`
- `FEATURES.md`
- `docs/UX_UI_MANIFEST.md` if shared navigation or mode guidance changes

Out of scope:
- changing party-sync data contracts;
- adding new quest analytics;
- changing Habitica party or quest links.

Acceptance:
- Party page provides a tab or segmented mode switch for current quest, planning, history, and members/CRON.
- Existing actions keep their current authorization and stale-data guards.
- Empty and offline cached states remain visible in the relevant mode.
- Component tests cover mode switching and at least one guarded action still rendering in its mode.

### Party Quest History Analytics

Goal: summarize stored shared quest completion history beyond the current recent-completion cards and queue penalty labels.

Touch:
- `src/Habitica.Domain/Party`
- `src/Habitica.WebApp/Pages/PartyPage.razor`
- `functions/api/party-sync/[partyId].js` only if the existing recent-completion payload is not enough
- direct tests under `tests/`
- `FEATURES.md`

Out of scope:
- collecting Habitica data that is not already available through current snapshots, chat completion signals, or shared queue records;
- optional vote budgets;
- changing quest queue ordering.

Acceptance:
- History view shows aggregate completions by quest and by owner from available shared history.
- Analytics clearly state the covered time window and when data is unavailable or sparse.
- Existing recent-completion removal permissions remain unchanged.
- Tests cover aggregate calculations and sparse/no-history rendering.

### Skill Macro Collection MVP

Goal: implement the planned local macro collection for predefined equipment and skill sequences.

Touch:
- `src/Habitica.Rules/Skills`
- `src/Habitica.Application`
- `src/Habitica.WebApp`
- direct tests under `tests/`
- `FEATURES.md`
- `TECHNICAL.md` if storage or execution architecture changes

Out of scope:
- arbitrary user code execution;
- loops or unbounded repeat-until macros;
- storing credentials in exported macros;
- server-side macro execution.

Acceptance:
- Users can create, edit, delete, and run local declarative macros using initial step types from `FEATURES.md`.
- Dry-run preview shows planned equipment changes, selected targets, mana cost, expected requests, warnings, and stop conditions.
- Execution runs sequentially, persists progress, refreshes or updates local state after mutating steps, and stops on validation failures, API errors, stale state, or unexpected state changes.
- Macro steps can reference existing inventory preset ids and dynamic gear strategies without copying transient recommendation data.
- Tests cover parsing/validation, missing gear, insufficient mana, stale data, restore-original-gear behavior, and partial execution failure.

### Task Mutation Dry-Run Summaries

Goal: add stronger pre-action summaries for existing task scoring/checkoff controls where local data can make the mutation clearer.

Touch:
- `src/Habitica.Api`
- `src/Habitica.Application`
- `src/Habitica.WebApp/Pages/TasksPage.razor`
- direct tests under `tests/`
- `HABITICA_API.md` if endpoint response assumptions are added or corrected
- `FEATURES.md`

Out of scope:
- duplicating spell estimates, Dashboard pending-damage/health-potion helpers, Inventory equip deltas, or bulk-sell previews;
- adding undocumented Habitica mutation endpoints;
- claiming exact GP/XP/HP deltas unless the value comes from a live API response or a documented formula.

Acceptance:
- Task cards show a concise dry-run summary for supported scoring/checkoff actions before multi-score or ambiguous mutations execute.
- Summaries distinguish exact API-returned values, local estimates, and unavailable values.
- Multi-score habit actions still run sequentially and stop on failure.
- Tests cover summary rendering, stale-data blocking, and unavailable-estimate copy.

### Settings Danger Zone Confirmation

Goal: require an explicit confirmation step before clearing local browser data from Settings.

Touch:
- `src/Habitica.WebApp/Pages/SettingsPage.razor`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/`
- `FEATURES.md`

Out of scope:
- changing the data actually cleared by `ClearLocalDataAsync()`;
- adding new import/export behavior.

Acceptance:
- Clear Local Data opens or reveals a confirmation that names credentials, cached Habitica data, party history, diagnostics, and setup data.
- The destructive action does not call `ClearLocalDataAsync()` until the confirmation control is activated.
- Cancel/close keeps local data untouched.
- Tests cover initial click, cancellation, and confirmed clearing.

### Returning User Copy Compression

Goal: reduce repeated hero/help copy for authenticated returning users while keeping first-run empty states understandable.

Touch:
- `src/Habitica.WebApp/Pages`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct page tests under `tests/Habitica.WebApp.Tests/Pages/`
- `docs/UX_UI_MANIFEST.md` if shared copy rules change

Out of scope:
- redesigning navigation;
- removing first-run or unauthenticated guidance;
- changing data loading behavior.

Acceptance:
- Returning authenticated users see denser top sections on Dashboard, Tasks, Party, Inventory, and Spells.
- First-run, signed-out, stale-data, and empty-cache states still explain the next action.
- Tests cover at least one authenticated returning state and one unauthenticated/empty state.

### Mobile Stat Table Context

Goal: keep row labels visible or repeated when wide stat tables scroll horizontally on small screens.

Touch:
- `src/Habitica.WebApp/wwwroot/css/app.css`
- pages using `.stats-table`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/` if markup changes
- `docs/UX_UI_MANIFEST.md` if table guidance changes

Out of scope:
- replacing table data with cards across desktop;
- changing stat calculations.

Acceptance:
- Mobile-width stat tables keep row identity visible through a sticky first column or repeated row-label context.
- Horizontal scrolling remains available for dense table values.
- Desktop table layout remains unchanged except for harmless label-context support.

### Compact Spell Card Density Pass

Goal: make spell cards easier to scan after the current full-card layout, recommendations, and CRON warning flow have been exercised.

Touch:
- `src/Habitica.WebApp/Pages/SpellsPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/SpellsPageTests.cs`
- `FEATURES.md`

Out of scope:
- changing spell estimate formulas;
- changing dynamic gear recommendation selection;
- changing cast execution order or CRON-warning semantics.

Acceptance:
- Spell cards keep target selection, count, mana preview, cast action, estimate text, CRON warning, and equipment recommendations available.
- Repeated low-priority explanatory copy is collapsed, summarized, or moved behind local detail affordances.
- Active casting progress and errors remain prominent.
- Tests cover key controls still rendering after the density change.



## [6.1.0] — 2026-05-27

### Добавлено

- Добавлен метод `GDPRWindowController.Close()` для закрытия GDPR-окна из кода.
- Добавлен метод `UserConsentManager.CloseGdprUnityUI()` для закрытия активного GDPR-окна из меню настроек игры напрямую (без необходимости иметь ссылку на `GDPRWindowController`).

### Исправлено

- При уничтожении GDPR-окна через `Destroy` теперь корректно завершается `Task`, возвращаемый из `ShowGdprUnityUI`.
