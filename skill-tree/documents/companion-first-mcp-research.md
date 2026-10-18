> Planning reference only. Active task definitions: [future.md](future.md). Architecture and competitive-product observations are supplied planning assumptions; external guidance requires fresh verification during Process Pending or implementation. Migration validates structure and selected repository facts, not MCP/OAuth compatibility or hands-on competitor usability.

# Habitica Companion — Companion-First Task Management, Reliability & ChatGPT/MCP Master Plan

**Status:** Planning reference; E01–E14 remain Pending intake and MCP contracts remain gated in future.md.
**Reviewed against repository:** `strexet/habitica-companion` (`main`)
**Reviewed against Skill-Tree task standard:** `strexet/skill-tree`
**External guidance reviewed:** 2026-10-06 (MCP/OAuth); 2026-10-12 (competitive task apps, PWA durability and queues)

## Queue Identity Map

Source contract numbers identify supplied reference sections only. Implementation selection uses exact prioritized T- IDs from [future.md](future.md). The import record preserves original contract numbering and Pending provenance without asserting new processing results.

| Source contract | Current task ID | Title |
| --- | --- | --- |
| 0 | `T-09f1fa5d-fa07-592d-9a2d-7ef12705cf5e` | Correct Habitica `x-client` Identity |
| 1 | `T-c6f6b09d-4625-5898-b91c-016c611b60df` | Define ChatGPT Integration Architecture, Privacy, And Retention Contract |
| 2 | `T-b883e33b-05a6-53d8-a9a5-d11d7008887f` | Add Dedicated Auth And MCP Worker Projects |
| 3 | `T-e33bccdc-22f1-5043-9230-3d12d9e9563d` | Implement Stable Profile Identity, Credential Vault, And Connection Lifecycle |
| 4 | `T-16a38176-f2fe-5f7d-a809-546f17357b49` | Implement One-Time Linking And OAuth 2.1 Authorization |
| 5 | `T-53352a18-54d2-57b6-accc-91f8e6dfad13` | Implement Read-Only MCP Baseline |
| 6 | `T-4a1de222-156a-52cf-8966-262a2c8216ed` | Add Safe Task Mutation Tools |
| 7 | `T-3987ba88-f69f-59f1-811e-f51cabfa5978` | Package The Plugin And Add Core Skills |
| 8 | `T-1a84d40a-f7e3-50f9-9601-daf3b101c7ec` | Add Party And Spell Capabilities Incrementally |
| 9 | `T-b92db026-a8cf-5922-8556-666cbea947ab` | Add Start New Day Only As A Dedicated High-Risk Feature |
| 10 | `T-96866c8f-9554-55d8-a638-6d8b5afaf21c` | Security Hardening, Revocation, Key Rotation, And Team Rollout |

## Purpose

Evolve Habitica Companion into a powerful, mobile-first, Companion-authoritative task manager (Categories, Projects, Goals, Tasks, Dailies, Habits, Calendar and Completed history), with Habitica as a gamification projection and an optional, multi-user ChatGPT/MCP integration. Preserve local-first privacy for users who do not explicitly enable server-side capabilities.

The integration should use:

```text
ChatGPT / Codex
    -> OpenAI Plugin
    -> OAuth 2.1
    -> Habitica Companion MCP resource Worker
    -> Habitica Companion auth / credential Worker
    -> Habitica API v3
```

Skills are a workflow layer on top of MCP tools. Skills must not own authentication, authorization, validation, secret handling, or mutation safety.

The current browser application continues to operate for non-opted-in users during migration, while the authoritative Companion domain is introduced incrementally:

```text
Habitica Companion PWA
    -> local Habitica User ID + API Token
    -> Habitica API v3
```

## Proposed ChatGPT Enrollment, Privacy And Retention Contract

Status: repository-owner approved for local implementation and tests. The current PWA has no ChatGPT enrollment UI. The Auth Worker has a disabled-by-default credential-vault implementation validated with local fixtures. Deployment, real enrollment, public registration and scheduled execution require separate applicable owner scope.

### Current boundary

`CredentialStore` persists credentials only in the browser when persistent mode is selected. Session credentials remain in application memory. `cloudflareSync.js` derives an encryption key and sync identifier locally; private sync receives ciphertext. `cloudflarePartySync.js` sends local claims or invite proofs, never Habitica credentials. `PrivacyPage.razor` describes the implemented browser boundary. The optional integration must preserve the current default for non-enrolled users.

### Proposed consent and data flow

Enrollment requires a separate explicit consent screen identifying the Companion operator, Cloudflare processing, the password-equivalent Habitica token, granted capabilities, retention limits and disconnect/delete controls. Existing sign-in, private sync, party membership and imported data never constitute integration consent. Cancellation sends no credentials to the integration backend.

After consent, the browser submits credentials directly to the Auth Worker over HTTPS. Auth validates credentials with Habitica, allocates or recovers the account's immutable opaque profile ID, encrypts the credential envelope and issues a management credential shown only to the enrolling browser. Consent version and timestamp are recorded. Credentials never enter OAuth parameters, linking URLs, MCP tool arguments, token props, logs or exported data.

ChatGPT receives only a Companion OAuth grant. The MCP Worker validates the resource audience and scopes through the Auth Service Binding; Auth rechecks profile/connection state before every credential-bearing operation. The Auth Worker executes allowlisted Habitica requests using the project-owned `x-client`. MCP receives normalized results, never the long-lived Habitica token. No generic URL, route or header proxy is exposed.

Read capabilities are separate from task, spell, party and Cron mutation capabilities. Write tools remain disabled until the authoritative Companion command API, durable result ledger, confirmation contracts and capability-specific dependencies are accepted. OAuth permission alone never bypasses mutation validation. Background execution remains a distinct opt-in capability.

### Proposed ownership and secrets

Separate Auth and MCP Workers own separate environment configurations. Auth alone binds `OAUTH_KV`, `HABITICA_INTEGRATION_DB` and credential-encryption secrets. The MCP Worker has only an Auth Service Binding and public resource configuration. Existing `HABITICA_SYNC_KV` and `HABITICA_PARTY_DB` never hold integration secrets or OAuth grants.

Application credential envelopes use authenticated encryption with a unique random nonce, explicit key version and profile-associated data. Encryption keys remain Worker secrets outside the database. Plaintext exists only for a validated enrollment or bounded request lifetime. Management credentials and linking tickets are stored as hashes; one-time tickets expire within ten minutes and are consumed atomically. Preview and production use separate databases, namespaces, keys and canonical resource URLs.

### Proposed lifecycle and retention defaults

| Record | Proposed lifetime and deletion behavior |
| --- | --- |
| Active credential envelope | Until credential replacement, integration disconnect or profile deletion; replacement removes the superseded envelope. |
| Stable profile and Habitica-account association | Retained during disconnect for reconnect stability; profile deletion removes the association and display metadata. Opaque IDs are never assigned to a different Habitica account. |
| OAuth grants and tokens | Access-token target lifetime ten minutes; refresh-grant target maximum lifetime thirty days. Disconnect immediately blocks operations in application state and revokes grants; expiry and provider cleanup remove residual records. |
| Linking ticket hash | Maximum ten minutes or successful single consume, whichever occurs first; expired ticket cleanup is required. |
| Management credential hash | Until replacement, disconnect or profile deletion; reconnect requires fresh Habitica validation and fresh explicit enrollment. |
| Operational audit metadata | Maximum thirty days; operation category, result code, timestamp and opaque trace ID only. No credentials, headers, task text, raw request/response bodies, prompts or conversation text. Profile deletion removes attributable audit metadata. |
| Prepared actions and result ledger | Capability-specific contracts must define retention before writes are enabled; unsupported capabilities remain denied. |
| Anonymous aggregate metrics | No retained aggregate analytics in the initial integration. Any later analytics requires a separate documented purpose and consent review. |

Disconnect marks the connection revoked before cleanup, prevents new operations, deletes the connection envelope and management credentials, invalidates tickets and revokes grants. Stable profile identity remains for a later authorized reconnect. Delete profile performs the same access block and removes the profile association, labels and attributable audit records. Cleanup failures return an explicit pending-cleanup state and permit idempotent retry; revoked state continues to deny access. An in-flight Habitica request cannot be recalled and must be reported separately.

Application deletion does not clear browser snapshots, delete Habitica data, remove legacy private/party sync records or erase ChatGPT conversations. Hosting-provider request logs and backups have separate operator-controlled lifecycles. Production activation requires verification and disclosure of provider retention and restored-backup deletion handling; no immediate physical erasure guarantee is claimed. Habitica token rotation is an independent account-level recovery control.

### Proposed implementation and release gates

The repository owner approved the optional server-side trust boundary and retention defaults for local implementation and tests. Per-user enrollment still requires consent. Before enrollment exists, the PWA privacy page continues to describe only current browser behavior. Enrollment implementation must update the privacy page, sign-in/settings notices and disconnect/delete UI together.

Required implementation evidence includes isolation between two profiles, expired/replayed ticket rejection, audience/scope enforcement, credential redaction, disconnect denial despite residual provider records, deletion retry after cleanup faults and reconnect identity stability. Local fixtures must use synthetic credentials. Real credential enrollment, deployment and destructive lifecycle tests require named target and action scope.

### Official compatibility sources

The proposed OAuth flow uses authorization code with S256 PKCE and an authenticated immutable profile response, following [OpenAI plugin authentication guidance](https://developers.openai.com/plugins/build/auth). Profile output contains identity fields only; connection health remains a separate result.

Resource discovery and audience-bound access follow [MCP authorization](https://modelcontextprotocol.io/specification/2025-11-25/basic/authorization). Companion OAuth tokens are not passed through to Habitica.

The split server candidate follows Cloudflare's [authorization server reference](https://github.com/cloudflare/workers-oauth-provider/blob/main/docs/authorization-server.md) and [resource server reference](https://github.com/cloudflare/workers-oauth-provider/blob/main/docs/resource-servers.md). Package versions and local binding compatibility require verification during scaffolding. The proposed operation proxy and retention defaults are Companion design choices, not provider guarantees.

Habitica traffic uses the creator identity and rate controls from [Habitica API Usage Guidelines](https://github.com/HabitRPG/habitica/wiki/API-Usage-Guidelines). Background traffic retains the thirty-second minimum gap. Shared-IP traffic budgets must be tested before hosted execution.

---

# Planning Constraints

The supplied architecture reference defines candidate integration constraints. Fresh official verification remains required before implementation.

## 1. Fix `x-client` compliance before building MCP

The repository documentation states the Habitica requirement for `x-client` to identify the **tool creator**, not the end user.

`HabiticaApiClient` requires an explicitly configured project-owned `x-client` and rejects missing or blank values. The PWA configuration contains the owner-supplied public creator identity with the stable `habitica-tool` suffix. Future MCP execution must use the same logical application identity.

The server-side MCP architecture must never generate `x-client` from the connected end user's Habitica User ID.

## 2. Split OpenAI profile identity from Habitica account status

OpenAI's current multi-account profile-tool contract is strict.

The tool marked with:

```text
_meta["openai/profile"] = true
```

must return only the profile identity schema:

```json
{
  "id": "...",
  "name": "...",
  "nickname": "..."
}
```

with no extra Habitica stats.

Therefore the earlier proposed `get_profile` tool must be split into:

```text
get_profile
get_account_status
```

`get_profile` exists only for stable connection identity.

`get_account_status` returns level, class, HP, MP, GP, XP, `needsCron`, party state, and similar live Habitica data.

## 3. Profile identity must survive reconnects

A random per-connection ID is not sufficient for the OpenAI profile tool because the profile identity should remain stable across token refresh and reconnection.

Recommended profile identity:

```text
profileId =
    "prf_" +
    base64url(
        HMAC-SHA256(
            PROFILE_ID_KEY,
            canonicalHabiticaUserId
        )
    )
```

Properties:

- opaque to ChatGPT;
- stable for the same Habitica account;
- does not expose the Habitica User ID;
- does not require retaining a plaintext identity lookup table;
- can be regenerated after a reconnect.

`PROFILE_ID_KEY` must be a separate Worker secret from credential-encryption keys.

## 4. Use Cloudflare OAuth Provider v1 candidate split architecture

The reviewed plan now targets the current Cloudflare split architecture:

```text
Authorization Worker
    @cloudflare/workers-oauth-provider
    OAuthAuthorizationServer
    OAUTH_KV

             |
             | Service Binding
             v

MCP Resource Worker
    OAuthResourceServer
    createMcpHandler()
```

The MCP Worker does **not** need a separate OAuth KV namespace.

The authorization Worker does.

The canonical MCP resource must be registered exactly, for example:

```text
https://habitica-companion-mcp.<account>.workers.dev/mcp
```

Tokens issued for another resource must not be accepted.

## 5. Do not overload OAuth KV with Companion application data

`OAUTH_KV` belongs to the OAuth provider library.

Habitica credential storage, linking tickets, connection lifecycle state, and management credentials should live in a separate application database.

Recommended:

```text
OAUTH_KV
    -> OAuth clients
    -> grants
    -> access / refresh token state

HABITICA_INTEGRATION_DB
    -> integration profiles
    -> encrypted Habitica credential envelopes
    -> linking tickets
    -> management-token hashes
    -> revocation metadata
    -> audit metadata
```

Do not use:

```text
HABITICA_SYNC_KV
HABITICA_PARTY_DB
```

for ChatGPT credential-bearing state.

## 6. Improve the pairing model

A short pairing code is acceptable only when treated as an authentication credential.

Use:

- cryptographically random code;
- at least ~60 bits of entropy;
- one-time use;
- short expiration;
- only a hash stored server-side;
- atomic consume;
- strict attempt limits;
- endpoint-level rate limiting.

Recommended human code:

```text
12 Crockford Base32 characters
```

rather than the previously sketched 8-character example.

The PWA can additionally display a high-entropy one-click linking URL / QR code later, but a linking URL is not required for MVP.

## 7. OAuth subject should be the stable profile, not a temporary connection

Use:

```text
OAuth userId / subject -> profileId
```

not:

```text
OAuth userId -> connectionId
```

Stable OAuth subjects allow credential repair/rotation without changing the represented OpenAI account identity.

OAuth token props should contain only minimal application identity, for example:

```json
{
  "profileId": "prf_..."
}
```

OAuth tokens and props must never contain the Habitica API token.

## 8. Direct write tools need stronger retry semantics

MCP JSON-RPC request IDs exist, but request IDs must not be treated as durable business idempotency keys.

A client may legitimately issue a new request with a new request ID.

Therefore:

- never blindly replay non-idempotent Habitica mutations;
- use server-side mutation records where replay would be harmful;
- for high-risk operations use `prepare -> execute` with a server-issued short-lived action token;
- do not rely on MCP request ID as the sole deduplication boundary.

Task mutation tools use the authoritative Companion command API and durable journal after E02/E03/E04/E05. No direct MCP-to-Habitica task-write shortcut is permitted.

## 9. Safety annotations were too weak

Some operations appearing as ordinary task writes also modify game state.

For example, completing/scoring a task can change XP, GP, HP, MP, drops, and quest progress.

Recommended annotations:

```text
create_todo     destructiveHint=false
update_task     destructiveHint=false
complete_task   destructiveHint=true
score_habit     destructiveHint=true
cast_spell      destructiveHint=true
start_new_day   destructiveHint=true
```

All Habitica account tools remain:

```text
openWorldHint=false
```

because tools operate on a bounded authenticated account rather than arbitrary internet resources.

Annotations remain hints only. Server-side authorization and validation are mandatory.

## 10. Advanced actions need a two-phase pattern

`Start New Day`, spell sequences, equipment-changing spell plans, purchases, and similar actions must not be exposed as simple fire-and-forget calls.

Preferred structure:

```text
prepare_start_new_day
execute_start_new_day
prepare_spell_cast
execute_spell_cast
```

The preparation record captures profile, requested operation, relevant fresh state, warnings, expected requests, expiration, and an opaque action token.

Execution revalidates state and consumes the token exactly once.

## 11. Plugin packaging format changed since the earlier sketch

For the portable Agent Plugins format, prefer:

```text
plugin/
  plugin.json
  mcp.json
  skills/
    ...
```

with the current Agent Plugins schemas.

`.codex-plugin/plugin.json` and `.mcp.json` remain compatibility/scaffolding formats, not the preferred portable layout.

For early MCP testing, packaging is not required: ChatGPT can create a personal plugin directly from a custom MCP server.

Package Skills only after the MCP contract is stable.

## 12. Server-side proxying changes Habitica rate-limit topology

Habitica documents rate limits by User ID **or IP address**.

Moving API traffic from many users' browsers to shared Cloudflare infrastructure may concentrate traffic through server-side network origins.

The rollout must therefore monitor:

- `X-RateLimit-Limit`;
- `X-RateLimit-Remaining`;
- `X-RateLimit-Reset`;
- `Retry-After`;
- unexpected cross-user 429 patterns.

Do not add background polling.

Any future background automation must honor Habitica's documented minimum 30-second interval for automated background calls.

---

# Target Architecture

```text
                                      ┌───────────────────────┐
                                      │   ChatGPT / Codex     │
                                      │                       │
                                      │ Habitica Companion    │
                                      │ Plugin / MCP client   │
                                      └───────────┬───────────┘
                                                  │
                                             OAuth + MCP
                                                  │
                    ┌─────────────────────────────┴─────────────────────────────┐
                    │                                                           │
                    ▼                                                           ▼
        ┌────────────────────────┐                                  ┌────────────────────────┐
        │ ChatGPT Auth Worker    │<────── Service Binding ─────────│ MCP Resource Worker    │
        │                        │                                  │                        │
        │ OAuthAuthorization-    │                                  │ OAuthResourceServer    │
        │ Server                 │                                  │ createMcpHandler()     │
        │                        │                                  │ MCP tools              │
        │ OAUTH_KV               │                                  │ Habitica API client    │
        │ Integration D1         │                                  └───────────┬────────────┘
        │ credential vault       │                                              │
        │ linking / revocation   │                                              ▼
        └────────────┬───────────┘                                     Habitica API v3
                     │
                     │ explicit enrollment
                     │
                     ▼
        ┌────────────────────────┐
        │ Habitica Companion PWA │
        │                        │
        │ existing local auth    │
        │ existing IndexedDB     │
        │ Settings integration   │
        └────────────────────────┘
```

Existing non-opted-in users retain:

```text
PWA -> Habitica API v3
```

with no ChatGPT server credential storage.

---

# Security Invariants

1. Habitica API tokens are password-equivalent.
2. Existing users remain local-only until explicit opt-in.
3. ChatGPT enrollment is a new and clearly disclosed trust boundary.
4. `x-client` always identifies the project/tool creator, never the end user.
5. No Habitica API token may appear in URL, query string, logs, analytics, diagnostics, exports, OAuth claims, MCP results, or Skills.
6. Habitica credentials are encrypted at rest with authenticated encryption.
7. Credential-encryption keys and profile-ID keys are separate secrets.
8. OAuth identity resolves to an opaque stable `profileId`.
9. OAuth tokens are audience-bound to the exact MCP resource.
10. Every MCP tool enforces the required scope server-side.
11. A model instruction is never an authorization mechanism.
12. Generic Habitica HTTP passthrough tools are forbidden.
13. Cross-user isolation is an explicit test category.
14. Non-idempotent mutations are never blindly retried.
15. Sensitive mutations are bounded and sequential.
16. High-risk actions use server-issued prepare/execute tokens.
17. Habitica `Retry-After` is respected.
18. No background polling is part of the initial integration.
19. OAuth grant revocation and credential deletion are both supported.
20. The public repository continues to contain the integration implementation, satisfying Habitica's public-tool reviewability requirement.

---

# Proposed OAuth Scopes

```text
habitica.profile.read
habitica.tasks.read
habitica.tasks.write
habitica.party.read
habitica.party.write
habitica.spells.cast
habitica.cron.execute
```

Do not request every scope at first authorization.

Use per-tool security schemes and step-up authorization.

---

# Tool Contract

| Tool | Phase | Scope | Read-only | Destructive |
|---|---|---|---:|---:|
| `get_profile` | MVP | `habitica.profile.read` | yes | no |
| `get_account_status` | MVP | `habitica.profile.read` | yes | no |
| `list_tasks` | MVP | `habitica.tasks.read` | yes | no |
| `get_task` | MVP | `habitica.tasks.read` | yes | no |
| `create_todo` | MVP write | `habitica.tasks.write` | no | no |
| `update_task` | MVP write | `habitica.tasks.write` | no | no |
| `complete_task` | MVP write | `habitica.tasks.write` | no | yes |
| `score_habit` | MVP write | `habitica.tasks.write` | no | yes |
| `get_party_context` | later | `habitica.party.read` | yes | no |
| `queue_quest` | later | `habitica.party.write` | no | no |
| `vote_quest` | later | `habitica.party.write` | no | no |
| `get_spell_context` | later | `habitica.profile.read` | yes | no |
| `prepare_spell_cast` | later | `habitica.spells.cast` | yes* | no |
| `execute_spell_cast` | later | `habitica.spells.cast` | no | yes |
| `get_cron_status` | later | `habitica.profile.read` | yes | no |
| `prepare_start_new_day` | later | `habitica.cron.execute` | yes* | no |
| `execute_start_new_day` | later | `habitica.cron.execute` | no | yes |

`prepare_*` does not mutate Habitica. Preparation may persist a short-lived internal action record.

Do not implement:

```text
habitica_request(method, path, body)
execute_arbitrary_action(...)
raw_habitica_api(...)
```

---


# Product Expansion: Companion-First Tasks, Projects, Goals, and Reliability

**Added:** 2026-10-12
**Scope:** The document is an integrated amendment to the reviewed ChatGPT/MCP plan, not an implementation or a completed product spec.
**Documentation language:** English, consistent with the repository's `skill-tree/documents/rules.md`.
**Evidence policy:** Product comparisons below describe documented capabilities, not hands-on usability-test findings. Recommendations are proposed design decisions and need prototype/user validation.

## Executive Decisions

1. **Companion becomes the source of truth for Companion-managed work.** Companion owns durable IDs, the full task/project/goal/category model, local completion history, recurrence, scheduling and lifecycle. Habitica is a gamification projection/integration, not the canonical datastore for Companion records. Companion ownership does not retroactively make all imported Habitica tasks Companion-owned without user-visible migration.
2. **Import existing Habitica work without data loss.** The initial importer creates local Companion IDs and a mapping to external Habitica task IDs/aliases. Existing Habitica edits are treated according to an explicit import-vs-managed ownership policy. Do not erase user data or blindly recreate a task intentionally removed outside Companion.
3. **Tags are labels, not a substitute for projects.** Reuse Habitica tags via a managed mapping where possible; keep categories, projects, goals, sections, dependencies, review state, calendar scheduling, and extra task metadata in Companion. Do not serialize hidden project metadata into Habitica task text or notes as an authoritative workaround.
4. **A write is accepted locally before Habitica confirms the projection.** Persist the domain change and a corresponding outbox command atomically. Show separate `Saved in Companion` and `Synced to Habitica` statuses. In-game rewards are pending until confirmed upstream.
5. **Do not claim exactly-once upstream execution.** Durable intent and reconciliation are realistic; unconditional execution of every command, in all failure conditions, is not. Unknown outcomes for non-idempotent scores must be reconciled or explicitly resolved rather than automatically replayed.
6. **Offline/manual mode and always-on mode differ.** Local-only PWA mode has a durable browser outbox processed while the app is running, but cannot guarantee closed-browser delivery or survive explicit clearing/eviction of browser storage. An optional server-managed workspace + credential authorization is required for reliable unattended execution and remote MCP access.
7. **Use progressive disclosure and mobile-first flows.** Default to a minimal Today/Inbox interface with quick capture, not to a Jira-like configuration screen. Advanced project structure, saved views, custom recurrence, goals, and automation remain optional and discoverable.
8. **ChatGPT and manual UX share the same Companion application commands.** Do not keep a separate MCP-to-Habitica write path after the Companion authoritative model ships. MCP calls create/update Companion entities and enqueue projections, using the same validation, permission and conflict rules as the PWA.
9. **Roll out by vertical slices.** Do not replace the current Tasks UI or move all existing data in one irreversible migration. Import in read-only preview, enable opt-in adoption, then migrate views and commands by feature flag.
10. **Trust boundaries stay explicit.** The existing encrypted per-section KV sync is opaque to the server, and must not be quietly repurposed as a server-readable multi-device/MCP workspace. Companion cloud mode requires a deliberate identity, privacy, storage, export, deletion and encryption decision.

## Evidence From The Current Companion Repository

Checked against `main` (review date 2026-10-12; verify again before implementation):

- `src/Habitica.Domain/Tasks/TaskSnapshot.cs` currently holds basic Habitica task data (`Id`, `Text`, `Type`, completion, difficulty, notes, due date, history and due-state), but no native projects/categories/goals, durable Companion-owned entity ID, projection mapping, command log, or dedicated recurrence model.
- `src/Habitica.Application/Tasks/TaskListViewModelFactory.cs` groups records by Todo/Daily/Habit/Reward and supports text search and simple sorting. The factory is not a cross-project planning/query engine.
- `src/Habitica.Storage/TaskSnapshotStore.cs` stores one latest task snapshot behind a key-value abstraction; `StorageKeys.cs` defines the existing portable sections. The snapshot store is a cache, not a durable domain store.
- `src/Habitica.WebApp/wwwroot/js/storage/indexedDbStorage.js` currently has a single Dexie `kv` table (`&key, updatedAtUtc`). The schema requires explicit migration to structured stores for authoritative entities and an atomic outbox.
- `src/Habitica.Api/HabiticaApiClient.cs` has an in-memory, per-client adaptive token-bucket implementation with an initial 30 requests/60 seconds estimate and `350 ms` floor. The API client is not a durable, coordinated scheduler across tabs, workers, browsers and MCP; after a timeout the API client cannot prove whether a write reached Habitica.
- `functions/api/party-sync/[partyId].js` is a separate shared-party state API with a trust-based local claim path. **Do not treat shared-party state as authoritative personal-account authorization for a new workspace/MCP write API.**
- `src/Habitica.WebApp/Pages/TasksPage.razor` is the existing Tasks workspace and must be reviewed against the new information architecture rather than simply patched with more fields.
- Current `PrivacyPage.razor` promises local Habitica credentials. Any optional server-mode enrollment requires a deliberate new privacy explanation.
- Existing `skill-tree/documents/technical.md`, `skill-tree/documents/rules.md`, `skill-tree/documents/habitica-api.md`, `skill-tree/documents/features.md`, `skill-tree/documents/ux-ui-manifest.md`, and `skill-tree/documents/cron.md` remain mandatory source references before implementing the relevant work.

## Competitor Research Matrix — What To Borrow And What To Reject

The goal is to combine **personal task-manager speed** with **selected project-management depth**, not reproduce an enterprise tracking system. The official references in the last section should be rechecked when individual epics are decomposed.

| Product | Documented patterns worth studying | Companion recommendation | Explicitly avoid |
|---|---|---|---|
| **Todoist** | Inbox; quick capture with natural-language dates; projects/sections/subtasks; labels/filters; Today/Upcoming; list/board/calendar; clear distinction between planned date and deadline; completed-task views | Primary benchmark for navigation, quick capture, filter/saved view model, project/task editing, due/deadline language | Paid-tier cloning, social productivity scores, corporate teamwork |
| **Things 3** | Areas -> Projects -> To-Dos; Today / Upcoming / Anytime / Someday / Logbook; planned start and deadline are different; dormant tasks are visually quiet | Primary benchmark for *focus* and task staging; separate task organization from time-based view; clear archive | Apple-only implementation assumptions; over-hidden records inaccessible to search |
| **TickTick** | Integrated calendar/list; Agenda/Week/Month; habit tracker separate from ordinary To Dos; flexible recurrence, tags, filters, reminders | Benchmark for mobile calendar-to-list interaction and Habits vs Dailies separation | Default-on Pomodoro/matrix/constant reminders/location tracking |
| **Amazing Marvin** | Categories as areas, multi-step projects, Inbox, Today; opt-in feature modules; configurable staleness warning; missing-next-step reviews | Benchmark for categories, project depth, aging/review hygiene, modular advanced mode | Huge mandatory settings surface; guilt-inducing procrastination prompts and intrusive gamification |
| **Microsoft To Do** | `My Day`, Planned, Completed, lists, steps, simple focused entry | Baseline for no-configuration onboarding and minimal daily workflow | Office/Outlook coupling |
| **Asana** | `My Tasks` with cross-project views; flexible list/board/calendar; custom sections and fields; scheduling dates and subtasks | Borrow only personal cross-project views, optionally project stages and simple views | Assignments/performance/capacity metrics, labor tracking, approvals, enterprise rules |
| **Linear** | Triage/Inbox, Backlog, Active, Done, archive; custom views; clean issue states | Borrow clear state transitions, backlog vs actionable separation, triage/review | Cycles/sprints/team metrics as compulsory concepts |
| **Jira** | Separate backlog and work-in-progress; filters; configurable board status mapping | Borrow backlog vs current work and a lightweight optional board | JQL complexity, epics/sprints/estimates/velocity/burnout tracking |

### Research Syntheses (Proposed, Not Current Product Behavior)

**A. One task, several orthogonal axes:**

```text
Organization: Category / Project / Section / Goal / Labels
Time:         plannedFor / startDate / dueAt / deadline / reminder / recurrence
Execution:    Inbox / Next / InProgress / Waiting / Done / Canceled / Archived
Game:         Habitica Todo / Daily / Habit / Reward + projection status
Review:       age / lastMeaningfulActivity / lastReviewed / blockedReason
```

The organization axes are not interchangeable. A task can belong to a project, be tagged `@errands`, be scheduled for Saturday, have a Monday deadline, be Waiting, and independently map to a Habitica Todo.

**B. Simple default; optional power:** basic task capture requires text only. Project/category, details, recurrence, dependencies, goals and labels reveal only on demand. A user should never need to create a project, choose a priority, or configure a field to add a task.

**C. Planned date is not a deadline:** `plannedFor` schedules attention; `dueAt`/`deadline` is a commitment. A task without a date remains visible in Inbox/Anytime/the assigned project. Do not infer a time zone or convert all-day dates to midnight UTC without explicitly preserving calendar-date semantics.

**D. Staleness is not lateness:** staleness reflects age since meaningful progress/review, with per-context thresholds and exclusions for waiting/recurring/someday tasks. A not-due task may be stale; an overdue task may have recent activity. Do not automatically delete or punish stale tasks.

**E. Completion history is a Companion event stream:** completion history must outlive Habitica's limited completed Todo retrieval and preserve repeated Habit/Daily occurrences, including missed/completed counts and projection status.

### Recommended Mobile-First Information Architecture

Default bottom navigation, subject to prototype tests (up to five destinations):

```text
Today   Inbox   Plan   Projects   More
                  |
              Upcoming / Calendar

Global quick-add (+) from primary views
```

- **Today:** today's selected tasks, due Dailies, optional short habit strip, overdue/at-risk summary, and clear per-task sync state.
- **Inbox:** immediate capture, review/organize later; no mandatory form.
- **Plan:** Upcoming agenda and compact Week/Month calendar with day agenda beneath the calendar; completed items are optionally shown, not default clutter.
- **Projects:** categories / areas with nested projects; progress and optional goal links; quick switcher rather than nested navigation traps.
- **More:** Habits, Dailies, Completed/Activity Log, Labels/Filters, Settings, and existing Companion gameplay sections unless dedicated navigation is justified by usability research.

Important: existing Dashboard/Spells/Equipment/Party/Quests must remain discoverable. Run a navigation prototype to decide which sections belong in primary navigation versus a `More` hub; don't hide frequently used game features without testing.

**Compact task row:** completion affordance, two-line title max, one secondary line for due/plan/project/label, optional critical badge for overdue/sync conflict, large touch target. Never show six metadata pills on every row.

**Task detail editor:** bottom sheet on mobile, full-screen at small heights/when keyboard is open; grouped advanced fields, explicit Save/Undo on destructive changes, quick natural language capture optionally but a manual date/time picker must always work.

**Calendar:** calendar grid + selected-day agenda; agenda remains usable for screen readers and narrow screens; touch drag is optional and has an accessible date-picker fallback. Ensure recurring Dailies/Companion occurrences do not appear as duplicate Todo cards.

**Sync UX:** only confirmed upstream rewards get 'Synced'; locally committed changes show an unobtrusive pending state, with drill-down `Waiting`, `Retrying`, `Checking result`, `Needs attention`. Avoid raw API exception popups.

### UX Study Protocol Before Final Screens

- Gather current screenshots and publicly documented mobile flows from Todoist, Things, TickTick, Amazing Marvin and at least one minimalist app; record evidence date and platform.
- Create side-by-side journey maps for: quick capture; reschedule; complete; recurring Daily; habit score; create project; move task; see Upcoming; review Completed; recover pending sync; connection setup.
- Evaluate each journey by taps, time to first usable action, readability, discoverability, state clarity, accessibility and error recovery. Do not invent lab test scores.
- Prototype on 360–390 px, 430 px, tablet and desktop widths. Test virtual keyboard, safe areas, one-handed reach, large text, screen readers and low-quality network.
- Conduct lightweight usability checks with willing current users without assuming permission to track or identify unknown teammates. Capture qualitative feedback only with consent.
- Produce a 'copy / adapt / reject' matrix with screenshots or permitted links, rationale, mobile-specific trade-offs and implementation priority.

## Canonical Data And Habitica Projection Boundaries

### Companion entity model (candidate, not finalized schema)

```text
Workspace / Profile
  Category (area of responsibility; optional nesting)
  Goal (desired outcome with progress model; may link projects)
  Project (finite deliverable; category, goalId?, optional sections)
  Label (cross-cutting multi-tag)
  Task (Companion UUID; one project?; labels[]; parentTaskId?;
        type, lifecycleStatus, priority, plannedFor, dueAt, deadline,
        recurrenceRule?, createdAt, updatedAt, lastActivityAt, completedAt)
  TaskOccurrence / HabitLog (repeated actions and evidence)
  TaskEvent (append-only completion, edit, restore, etc.)
  HabiticaProjection (companionTaskId -> Habitica taskId/alias, mapping type,
                      lastObservedVersion, syncState, lastReconciledAt)
  OutboxCommand (durable intent, prerequisites, retry/reconcile state)
```

Avoid using the `Habitica task id` as the Companion entity's primary ID. Goals do not have to project to Habitica. A project may include Todo/Daily/Habit tasks; the project need not become a Habitica Todo. Keep `Category` and `Project` as first-class Companion concepts; use Habitica tags as optional mirrored labels with explicit `companionLabelId <-> habiticaTagId` mapping. Verify tag and alias endpoints/uniqueness upstream before depending on tag or alias contracts.

### Source-of-truth policy

- **Companion-managed task:** Companion fields/state win for managed metadata; Habitica is projection. Missing mapped Habitica Todo may be recreated, then completed if Companion had completed the Todo. Restoration needs confirmed absence, stable correlation and safe retry state.
- **Imported Habitica task:** initially external-owned/read-through. Show review before conversion to Companion-managed. External-only changes should never be silently discarded at import.
- **External edits after conversion:** surface conflict policy and optional re-import; don't oscillate between systems or erase user intent.
- **Game-state properties:** XP/HP/MP/GP, gear, drops, Habitica quest effects and actual Cron results remain **Habitica-authoritative**. Companion cannot fabricate an upstream reward.
- **Missing task on Habitica:** recreate only for a previously Companion-managed task when policy permits restoration, and absence was actually verified. A user intentionally deleting a Habitica task outside Companion is a conflict, not always a transient error; define a user preference/review behavior.
- **Completed Companion Todo with no Habitica task:** enqueue `ensure projection exists -> score complete` as dependent operations. If create succeeded but response timed out, find/reconcile the created projection before attempting another create; if scoring outcome is uncertain, don't blindly score twice.
- **Habits and Dailies:** repeated scoring and Daily occurrence semantics differ from one-time Todos; do not project repeated instances as new Todos by default. Completion events and game-score events must be modeled separately.

### Companion authority vs reliable cloud synchronization

Workspace authority and synchronization are distinct questions:

1. **Domain authority:** Companion records define user tasks/projects/goals and completion, regardless of whether saved locally or in cloud.
2. **Durability:** local-only IndexedDB is best effort and vulnerable to manual clearing/eviction; durable multi-device, closed-browser guarantees require an optional hosted canonical store.
3. **Confidentiality:** the current browser-encrypted KV sync is not directly readable by a remote MCP worker. Adding a server-readable workspace requires a separate explicit privacy and encryption contract.
4. **Remote MCP:** ChatGPT can only observe current Companion-native projects/goals/commands if the data is published to an authorized backend, not just stored in one browser's IndexedDB.

**Proposed path:** maintain local-first storage for every user; add an optional authenticated cloud workspace for advanced cross-device/server/MCP use. The server workspace is authoritative for committed cloud mutations, while each client maintains a dedicated durable local journal and merges on reconnection. Define conflict resolution/versioning before enabling multi-device writes. If cloud mode is not enabled, the app remains fully manually usable but cannot provide remote, always-on MCP operations over purely local data.

**Trust warning:** a server performing MCP queries over Companion-native records cannot simultaneously promise universal opacity when records remain opaque end-to-end ciphertext to the server, unless a separately designed selective disclosure/secure computation approach is built. The current encrypted KV sync must not be misrepresented as the new server-readable workspace.

## Reliability Contract: What Can Be Guaranteed

**Do not promise:** 'Every submitted Habitica command always completes exactly once.' The service may be permanently offline, tokens may be revoked, tasks may become invalid, the account may be banned/deleted, or a non-idempotent score may succeed upstream even though the response is lost. Cloudflare Queues are at-least-once and do not magically make Habitica writes idempotent. Browser Background Sync is not universally available.

**Promise by mode:**

| Mode | Guaranteed by Companion | Not guaranteed |
|---|---|---|
| Local-only PWA | A locally committed command is recorded durably in IndexedDB while browser storage remains available; eligible commands resume when the app runs and connectivity permits; no silent command drop | Execution after browser/app closed; survival of local storage deletion; eventual success if Habitica permanently rejects or stays unavailable |
| Opt-in hosted workspace/executor | Accepted commands persist in a durable server ledger; worker keeps scheduling retries/reconciliation until confirmed applied or permanently blocked; survives browser closure and device reboot within platform/service durability assumptions | Exactly-once effects for non-idempotent Habitica mutations lacking upstream idempotency; eventual completion despite permanent refusal/outage |

**Client UX:** the Companion local state updates immediately after a successful local transaction. Each projection has a separate game-sync state. Manual interaction and MCP use the same command ledger where server mode is enabled.

### Transactional outbox / saga pipeline

```text
User/UI/MCP action
   -> validate Companion invariant / permission
   -> atomic local transaction:
        mutate Companion entity + add TaskEvent + append OutboxCommand
   -> UI: 'Saved in Companion' + pending game state
   -> scheduler acquires per-profile/per-task lease
   -> obey backpressure/rate budget
   -> invoke narrowly defined Habitica operation
   -> persist attempt status and response metadata
   -> verify/reconcile when necessary
   -> mark projection confirmed or BlockedNeedsAttention
   -> UI/ChatGPT structured result and progress
```

Suggested state machine:

```text
Pending -> Ready -> Leased -> Sending -> Confirmed
                        |           |
                        |           +-> UnknownOutcome -> Reconciling
                        |                               |-> Confirmed
                        |                               |-> BlockedNeedsAttention
                        |                               +-> Ready (only if safe)
                        +-> RetryScheduled
                                |-> Ready
                                +-> BlockedNeedsAttention

Any pending state -> Canceled (only before irreversible send)
```

Persist attempts, `nextAttemptAt`, `lastHttpStatus`, bounded error category, stable `commandId`, `taskId`, profile ID, dependencies and external projection identifiers. Never record secret request headers or unredacted request bodies.

### Failure classification

| Failure | Scheduler behavior | User experience |
|---|---|---|
| Offline, DNS failure, TCP reset, 502/503/504, transient server errors | Retry with exponential backoff, jitter, max interval and eventual probe/resume | `Waiting for Habitica`, no exception modal |
| `429` or upstream rate headers exhausted | Respect `Retry-After`/reset, halt targeted sending until allowed | `Rate-limited; retry scheduled` |
| Timeout after request could have been sent | Mark `UnknownOutcome`, reconcile; **never blindly replay score/cast/Cron** | `Checking whether Habitica applied it` |
| Invalid credentials / 401 | Pause the affected profile, require relink/credential repair | `Reconnect Habitica` |
| Validation/authorization/unsupported task / 400/403, permanent 404 | Mark blocked or reconcile under managed-missing projection policy | `Needs attention`, explicit reason and action |
| Conflicting external edit, duplicate alias, mismatched mapping | Conflict resolution; no overwrite loop | `Review conflict` |
| Upstream permanent outage | Retain command, bounded probes, no busy loop, operator/user notification if supported | Visible pending duration; honest no guarantee |

Do not expose raw Habitica exceptions to the Blazor UI; failures become typed application results and meaningful activity items. Provide a persistent queue/activity view with retry status, last attempt, next planned attempt, cancel/resolve (when safe), and an audit trail.

### Adaptive traffic controller (replace inadequate current behavior)

- Enforce per-user **and shared-egress** budgets. Habitica may rate-limit by User ID **or IP**, so even separate connected profiles might share an upstream limiter.
- Use actual `X-RateLimit-*` and `Retry-After` response headers as the strongest observed inputs; keep fallback estimates conservative if headers are absent or ambiguous.
- Begin slowing **before** theoretical exhaustion: configurable early-warning threshold, safety reserve, projected in-flight requests and burst controls. Do not ramp only when almost empty.
- Add adaptive/AIMD-style control: decrease allowed concurrency/rate sharply on 429 and repeated transient failures; recover *gradually* on a window of successful responses rather than instant full-speed after one success.
- Network timeout is not necessarily a server rate-limit signal: separate connectivity circuit breaker from rate-budget controller; recovery probes should not flood.
- Support fairness among users, ordering/dependency within one task, backpressure, bounded concurrency and no starvation; use single active executor/lease per task/profile to avoid tabs/workers duplicating sends.
- Browser client throttle state currently resets per `HabiticaApiClient` instance: the new queue/controller must persist or coordinate rate state sufficiently for cross-tab/multi-executor correctness.
- Instrument rate tokens/remaining, queues/latency, retries, 429s, timeout category, backoff duration, recovery and aging. Avoid inferring exact API limits from hardcoded constants when headers are available.
- Test controlled synthetic schedules (burst, early slowdown, 429, 503, packet loss, recovery, multi-profile shared egress) without live Habitica traffic.

### Cloudflare Queues are optional transport, not the ledger

When adding hosted execution, D1's durable command ledger is the recoverable source of truth. Cloudflare Queues can wake processors or distribute jobs, but delivery is **at least once**; exhausted retries can reach a DLQ and eventually expire. Use a persisted command state machine, leases, reconciliation and periodic bounded sweeps to avoid losing work when a queue message expires. Do not promise an indefinite durable queue solely because Cloudflare Queues is enabled.

---

---

# Revised Implementation Sequencing / Cross-Cutting Gates

Keep detailed OAuth/MCP contracts in [future.md](future.md), but apply the listed gates before claiming an integration task complete:

| Original task | Status / new dependency interpretation |
|---|---|
| **0 — Correct `x-client`** | API-client initialization requires a public project-owned creator/app value; no end-user fallback exists. |
| **1 — Privacy architecture** | Must explicitly cover Companion-owned data, hosted workspace visibility, local-only mode, durability guarantees and command history. |
| **2 — Auth/MCP Workers** | Infrastructure can be built independently, but cloud workspace and outbox execution ownership must be designed before production writes. |
| **3 — Profile/vault** | Must distinguish Habitica identity, Companion workspace identity and stable OAuth profile identity; no implicit connection to existing party-sync local claims. |
| **4 — OAuth/linking** | Consent must cover access to Companion-managed projects/tasks as well as optional Habitica credential vault; no remote data without an authorized workspace. |
| **5 — Read-only MCP** | Existing `get_profile`/`get_account_status` can ship as Habitica-based bootstrap; `list_tasks`/`get_task` must be documented as **legacy Habitica read-only** until E02/E03/E04 are done. Add canonical Companion read tools only when authoritative workspace exists. |
| **6 — Task mutation MCP** | **BLOCKED for Companion-managed writes** until E02/E03/E04/E05 are decomposed and shipped, and E07 is resolved for off-browser execution. Use Companion command API, not direct Habitica writes. |
| **7 — Plugin + Skills** | Update instructions/tools to categories/projects/goals/planning/history; read-only Skills may begin earlier, but advanced workflows depend on E13. |
| **8 — Party/spells** | Retain separate game-state safety logic; use shared command/reliability engine for eligible mutations. Party roles/claims require actual authorization proof. |
| **9 — Cron** | Retain prepare/execute, no blind retry and explicit success-unknown handling; must use the same reliable-command ledger. |
| **10 — Hardening/rollout** | Add mobile UX adoption, persistent queue visibility, privacy lifecycle, data migration and multi-device/conflict testing. |

**Critical path for Companion-first remote task management:** E01 (research) -> E02 (canonical model) -> E03 (storage/workspace) + E05 (outbox) + E06 (limiter) -> E04 (Habitica projection) -> E07 (hosted execution) -> E08–E12 (manual mobile UX) and E13 (MCP parity). OAuth tasks 0–4 can progress in parallel where OAuth trust contracts are not contradicted.

**Milestone definitions:**

1. **Foundation:** `x-client`, explicit source-of-truth/privacy decision, task/projection model; UI/mobile competitive research.
2. **Local reliable Companion:** durable canonical entities, local transaction/outbox, reconciliation, early slowdown and intuitive pending state.
3. **Manual task-manager MVP:** mobile quick capture, Today/Inbox, Upcoming/Calendar, project/category essentials, completed Logbook.
4. **Power user:** goals, advanced project structure, Habits/Dailies recurrence, saved views, task hygiene.
5. **Cloud/MCP:** optional secure workspace, hosted executor, OAuth integration, Companion CRUD for full task/project/goal model, same reliable ledger.
6. **Game power features:** spell/party/Cron with explicit prepare/execute and reliable but honest outcome handling.
7. **Team rollout:** privacy/security, migration, performance/accessibility, real mobile testing and operational fault drills.

**Owner decisions to collect during decomposition (not silently choose):** Is the hosted Companion workspace intended for all synced users or only ChatGPT opt-ins? Which data must be readable by the backend? Should intentional deletion in Habitica restore automatically or trigger review? Can categories nest? One primary project or multi-home? What is a goal's progress metric? Which Habitica task types are projections vs independent entities? How long are completed events retained? Does policy allow high-risk game actions while a prior command is `UnknownOutcome`? Who can run a background executor and how is the executor disabled?

# External Primary Sources And Further UX Review Checklist

- Todoist: https://www.todoist.com/help/todoist/get-started/get-started-with-todoist-OgNNJR ; https://www.todoist.com/help/todoist/features/introduction-to-filters-V98wIH ; https://www.todoist.com/help/todoist/get-started/todoist-glossary-cA60laWMH ; https://lp-regional-test.todoist.com/help/todoist/features/view-completed-tasks-in-todoist-J19h2s ; https://www.todoist.com/help/todoist/features
- Things 3: https://culturedcode.com/things/support/articles/4001304/ ; https://culturedcode.com/things/support/articles/9596775/
- TickTick: https://ticktick.com/features
- Amazing Marvin: https://help.amazingmarvin.com/amazing-marvin/how-marvin-works ; https://help.amazingmarvin.com/amazing-marvin/categories-projects-and-tasks ; https://help.amazingmarvin.com/amazing-marvin/staleness-warning ; https://help.amazingmarvin.com/amazing-marvin/categories/strategies
- Microsoft To Do: https://support.microsoft.com/en-us/todo/welcome-to-microsoft-to-do
- Asana: https://help.asana.com/s/article/maximize-productivity-with-my-tasks ; https://help.asana.com/s/article/planning-with-asana-calendar
- Linear: https://linear.app/docs/default-team-pages ; https://linear.app/docs/configuring-workflows
- Jira: https://support.atlassian.com/jira-software-cloud/docs/use-your-kanban-backlog/
- Habitica rate/API: https://github.com/HabitRPG/habitica/wiki/API-Usage-Guidelines ; https://habitica.com/apidoc/
- IndexedDB storage limits: https://developer.mozilla.org/en-US/docs/Web/API/Storage_API/Storage_quotas_and_eviction_criteria
- Browser background limitations: https://developer.mozilla.org/en-US/docs/Web/API/Background_Synchronization_API ; https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps/Guides/Offline_and_background_operation
- Cloudflare Queues delivery/DLQ: https://developers.cloudflare.com/queues/reference/delivery-guarantees/ ; https://developers.cloudflare.com/queues/configuration/dead-letter-queues/
- OpenAI MCP/plugin: https://developers.openai.com/plugins/build/mcp-server ; https://developers.openai.com/plugins/build/auth ; https://developers.openai.com/plugins/concepts/skills

**Research completion gap:** Source documentation and a few publicly indexed mobile screenshots were reviewed; real hands-on tests on current app versions, interaction counts, user interviews, visual component benchmarking and accessibility audits have **not** yet been performed. Hands-on tests are explicit deliverables of E01 and E14, not fabricated evidence.

---

---

# Recommended Delivery Milestones

The high-level Companion product roadmap and detailed OAuth/MCP tasks form **one program**, with distinct product/domain and remote-access tracks. **Do not interpret completion of original Tasks 0–6 as a fully Companion-authoritative MVP.**

| Milestone | Scope | Release gate |
|---|---|---|
| **M0 — Research + API compliance** | E01, original Task 0; explicit owner decisions | Approved mobile IA and stable tool-owner `x-client` |
| **M1 — Domain and durability** | E02, E03, E05, E06; original Task 1 | Canonical task IDs; atomic journal; typed errors; earlier slowdown |
| **M2 — Habitica projection** | E04 plus integration tests | Existing imports preserved; missing-managed-task repair and safe create-then-complete reconciled |
| **M3 — Manual mobile task-manager MVP** | E08, E09, essential E10/E11, E14 | Quick Add, Today, Inbox, Plan/Calendar, Projects and Logbook usable on phones |
| **M4 — Optional hosted account / MCP** | E07, E13; original Tasks 2–7 with revised gates | OAuth per-user identity; server-readable authorized workspace; MCP uses Companion command API |
| **M5 — Power user** | Advanced E10/E11/E12; original Task 8 as appropriate | Goals, saved views, review/staleness and party/spell interoperability |
| **M6 — High-risk gameplay** | Original Task 9 | Prepared single-use Cron, no blind replay |
| **M7 — Hardened rollout** | Original Task 10 + E14 | Migration, accessibility, revocation, fault injection and real multi-user isolation |

Implementation may run independent design and infrastructure streams concurrently, but never bypass task-level dependency or security gates. The **first manual task-manager release** should not wait for OAuth; **remote MCP support for Companion-native projects** must wait for the authoritative workspace and command API.

# Validation Matrix For MVP

## Identity

- same Habitica account -> same opaque profile ID;
- different Habitica accounts -> different profile IDs;
- reconnect -> stable profile ID;
- OAuth token for profile A cannot access profile B.

## Secrets

- API token absent from D1 plaintext columns, OAUTH_KV application props, logs, traces, URLs, MCP structured content, and errors.
- wrong encryption key fails closed.
- wrong AAD fails closed.

## OAuth

- PKCE S256.
- exact resource/audience.
- scope enforcement per tool.
- step-up write scope.
- consumed linking code cannot be reused.
- OAuth denial creates no grant.

## MCP

- Streamable HTTP initializes.
- `get_profile` passes strict OpenAI schema.
- tool annotations match actual behavior.
- output schemas validate.
- no authorization data comes from model-supplied IDs.

## Habitica

- stable tool-owner `x-client`.
- `Retry-After` honored.
- no blind write retry.
- Daily due state follows server evaluation.
- no background polling.
- cross-user 429 behavior monitored during rollout.


## Companion Authority / Local-First / Recovery

- Companion UUID remains stable through Habitica task deletion/recreation and account reconnection.
- Category/Goal/Project/Label metadata survives export, import, offline edits and cloud synchronization.
- Atomic local domain-event + outbox transaction survives restart.
- Confirmed Companion Todo completion with missing game projection creates the projection once and scores the projection once **when outcome can be verified**; otherwise enters `UnknownOutcome` instead of guessing.
- Existing Habitica tasks remain available on migration; external delete/edit conflict policy is visible.
- Persistent completed history includes recurring occurrences beyond Habitica's recent completed-Todo API window.
- Manual mobile UI and MCP use the same business command API, scopes, and failure states.
- Browser-only mode never claims execution will continue after the app closes.
- Hosted mode survives Worker restart and queue redelivery without an untracked command disappearing.
- Early-slowdown synthetic tests, 429/backoff and gradual recovery tests pass.
- Progress UI distinguishes local save from verified Habitica reward.

## Mutations

- fuzzy match ambiguity never writes.
- partial multi-score reports completed count.
- success + refresh failure is not retried.
- ambiguous network outcome never triggers automatic replay.

---

# Supplied Source References

Repository sources:

- https://github.com/strexet/habitica-companion
- https://github.com/strexet/skill-tree

Habitica:

- https://github.com/HabitRPG/habitica/wiki/API-Usage-Guidelines
- https://habitica.com/apidoc/

OpenAI:

- https://developers.openai.com/api/docs/guides/custom-mcp-server
- https://developers.openai.com/plugins/build/auth
- https://developers.openai.com/plugins/build/mcp-server
- https://developers.openai.com/plugins/build/skills
- https://developers.openai.com/plugins/build/plugins
- https://developers.openai.com/plugins/guides/security-privacy

Cloudflare:

- https://developers.cloudflare.com/agents/model-context-protocol/guides/remote-mcp-server/
- https://developers.cloudflare.com/agents/model-context-protocol/protocol/authorization/
- https://developers.cloudflare.com/changelog/post/2026-10-01-workers-oauth-provider-1x/
- https://github.com/cloudflare/workers-oauth-provider

---

# Candidate Architecture Decision

Use:

```text
Existing Blazor PWA
    + explicit ChatGPT enrollment

Dedicated Cloudflare Authorization Worker
    + OAuthAuthorizationServer
    + OAUTH_KV
    + integration D1
    + encrypted Habitica credentials

Dedicated Cloudflare MCP Resource Worker
    + OAuthResourceServer
    + Service Binding to Auth Worker
    + stateless MCP
    + semantic allowlisted Habitica tools

OpenAI Plugin
    + MCP
    + focused Skills after MCP stabilizes
```

Do not add an extra agent harness.

ChatGPT/Codex already provides the orchestration layer. The server should remain a narrowly scoped capability and security boundary, while Skills provide repeatable workflows around the exposed tools.


## Companion-First Final Amendment (2026-10-12)

OAuth/Worker/plugin architecture supplies candidate identity and transport boundaries. Task writes require Companion commands, a durable journal and projection reconciliation.

```text
PWA UI / ChatGPT MCP
       -> Companion application commands (same semantics and auth checks)
       -> Companion-owned task/project/goal/event records (local / opt-in cloud)
       -> durable outbox / scheduler / reconciliation
       -> Habitica game projection (validated task tags/aliases + scores)
```

**Operational invariant:** `Companion completion != Habitica award confirmed` until the upstream operation is verified. Permanent errors and unknown non-idempotent outcomes remain persisted and visible; never silently discard failed commands or unknown outcomes. Background completion is available only with a correctly authorized optional hosted executor.

**Next planning action:** Conduct E01, define critical owner decisions for E02/E03/E04/E05/E07, then use `skill-tree-process-future-pending` to split one high-level epic at a time. Only implementation-ready subtasks go into `Prioritized Next Changes`; preserve original OAuth task order but apply revised gates.

## Navigation prototype evidence boundary

The local research prototype resides in `skill-tree/prototypes/mobile-navigation.html`; `navigation-review.html` supplies phone/tablet/desktop frame widths, enlarged text and a short-screen approximation. All records are synthetic and in memory. The prototype has no network or credential integration and supplies no application acceptance evidence. The [navigation study](navigation-study.md) adopts the checked baseline under the standing local contract direction and records default/advanced behavior and future production acceptance. Documented competitor workflows remain distinct from hands-on competitor testing, local prototype checks and owner usability feedback. Detailed production UI promotion still requires the remaining hands-on E01 gate in future.md; local design adoption does not claim that qualification.
