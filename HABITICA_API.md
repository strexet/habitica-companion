# Habitica API Integration Guide

Last researched: 2026-04-24
Target API: Habitica API v3
Audience: developers building a new Habitica client or integration

## 1. Scope and source-of-truth policy

Use Habitica API v3 for third-party clients.

Do not build against API v4. Habitica's public wiki describes API v4 as incomplete, unstable, and unsuitable for third-party tools. API v3 is the supported public API surface for external integrations.

Primary references:

- Official API docs: `https://habitica.com/apidoc/`
- Official third-party API usage guidelines: `https://github.com/HabitRPG/habitica/wiki/API-Usage-Guidelines`
- Habitica API wiki: `https://habitica.fandom.com/wiki/Application_Programming_Interface`
- Habitica server implementation: `https://github.com/HabitRPG/habitica`
- Habitica Wiki webhook documentation: `https://habitica.fandom.com/wiki/Webhooks`
- Habitica User Data Display Tool: `https://tools.habitica.com/`
- Party & Guild Data Tool: `https://oldgods.net/habitica/cTheDragons/group.html`

Treat the official API docs and current server implementation as authoritative. Treat wiki pages, tools, Reddit posts, and old GitHub issues as implementation notes and operational evidence, not contract-level documentation.

## 2. Base URL and transport

```text
https://habitica.com/api/v3
```

Use HTTPS only. Send and receive JSON.

Recommended request headers:

```http
Content-Type: application/json
Accept: application/json
x-api-user: <authenticated-user-id>
x-api-key: <authenticated-user-api-token>
x-client: <tool-author-user-id>-<application-name>
```

Header names are usually shown in lowercase in examples, but HTTP header names are case-insensitive. Use the lowercase form consistently.

## 3. Authentication

Most API routes require two user credentials:

```http
x-api-user: <User ID>
x-api-key: <API Token>
```

The API token is equivalent to a password for API access. Never log it, store it in analytics, send it to third-party telemetry, include it in URLs, or expose it to client-side plugins that do not require it.

Users can retrieve credentials from Habitica settings:

- Web: user icon -> Settings -> API
- Android: Settings -> API
- iOS: Settings -> Account Details

For a public client, store credentials in the platform's secure credential storage:

- iOS: Keychain
- Android: EncryptedSharedPreferences / Keystore-backed storage
- Desktop: OS credential vault where available
- Server-side integration: secret manager or encrypted database column

Do not use query parameters for credentials.

## 4. `x-client` header

Every third-party API request must include `x-client`.

Format:

```text
<tool-author-user-id>-<application-name>
```

Example:

```http
x-client: 12345678-90ab-416b-cdef-1234567890ab-NewHabiticaClient
```

Important rules:

- Use the tool author's Habitica User ID, not the end user's User ID.
- Keep the application name stable and specific.
- Include this header on all requests, including GET requests.
- For temporary local experiments, use a clearly identifiable suffix such as `Testing`.

Habitica uses this value to identify third-party tools that cause server problems. Tools that generate problematic traffic may be rate-limited or blocked.

## 5. Rate limiting and traffic policy

Habitica rate-limits third-party API traffic. Every API response can include:

```http
X-RateLimit-Limit: <request-count-for-window>
X-RateLimit-Remaining: <requests-left-in-window>
X-RateLimit-Reset: <reset-time>
```

When the limit is exceeded, Habitica returns:

```http
429 Too Many Requests
Retry-After: <seconds>
```

Client requirements:

- Respect `Retry-After` exactly.
- Use exponential backoff with jitter for transient network failures and 5xx responses.
- Do not retry non-idempotent requests blindly.
- For automated background scripts, keep at least 30 seconds between API calls unless the user explicitly initiated the action.
- Add termination conditions for automation loops. Example: stop auto-casting skills when the user no longer has enough mana.
- Do not poll aggressively for state that can be refreshed manually or obtained through webhooks.

Operational note: public tools that enumerate party or guild members can generate many requests. Use pagination, cache immutable content, and expose progress/cancellation in the UI.

## 6. Response envelope

Successful responses generally use this shape:

```json
{
  "success": true,
  "data": {},
  "notifications": []
}
```

Some endpoints include a `message` field:

```json
{
  "success": true,
  "data": {},
  "message": "Bought Short Sword",
  "notifications": []
}
```

Errors generally use this shape:

```json
{
  "success": false,
  "error": "BadRequest",
  "message": "Task type must be one of \"habit\", \"daily\", \"todo\", \"reward\"."
}
```

Validation errors may include an `errors` array:

```json
{
  "success": false,
  "error": "BadRequest",
  "message": "todo validation failed",
  "errors": [
    {
      "message": "Path `text` is required.",
      "path": "text"
    }
  ]
}
```

Handle errors by HTTP status and by `error`/`message`. Do not depend on exact localized message text for control flow.

Common statuses:

| Status | Meaning | Client behavior |
| --- | --- | --- |
| 200 | Success | Parse `data`. |
| 201 | Created | Parse created resource in `data`. |
| 202 | Accepted / approval requested | Treat as non-final state; refresh affected entity. |
| 400 | Bad request / validation | Do not retry without changing input. |
| 401 | Invalid credentials or unauthorized operation | Ask user to re-authenticate or explain permissions. |
| 404 | Resource not found | Refresh local cache; entity may have been deleted. |
| 429 | Rate limited | Wait `Retry-After`; throttle future calls. |
| 5xx | Server/transient | Retry with backoff if request is safe or deduplicated. |

## 7. Versioning and synchronization

Habitica responses often include full or large resource objects. Some API calls return more data than the immediate operation appears to require. Design the client with selective parsing and defensive schema handling.

Guidelines:

- Ignore unknown fields.
- Treat missing optional fields as normal.
- Preserve server IDs exactly.
- Use `id` as the public resource identifier where present; `_id` may also appear.
- Use task aliases only as optional user-defined shortcuts, not as a replacement for stable server IDs.
- Refresh user state after operations that affect stats, inventory, tasks, party quest state, or notifications.
- Avoid assuming that write responses contain every field needed to update all local projections.

## 8. User API

### 8.1 Get authenticated user

```http
GET /user
```

Returns the authenticated user document. The user document can include achievements, auth metadata, challenge memberships, flags, guild memberships, history, inbox, invitations, inventory, notifications, party state, preferences, profile, subscription/purchase data, stats, tags, and task order.

Use `userFields` to limit the response:

```http
GET /user?userFields=stats,items.gear.equipped,tasksOrder
```

`notifications` are always returned.

Recommended client usage:

- Use narrow `userFields` for frequent refreshes.
- Fetch the full user document only on initial sync, debug export, or explicit full refresh.
- Use `tasksOrder` together with `/tasks/user` for client-side ordering.

### 8.2 Update authenticated user

```http
PUT /user
Content-Type: application/json

{
  "preferences.timezoneOffset": -120,
  "profile.name": "Display Name"
}
```

Some paths are protected and cannot be updated through this endpoint. Handle `NotAuthorized` responses for protected paths.

Recommended usage:

- Send only fields being modified.
- Do not send a full cached user object back to the server.
- Keep updates explicit and auditable.

### 8.3 Toggle resting in the Inn

```http
POST /user/sleep
```

Toggles `user.preferences.sleep`.

The response data is a boolean representing the resulting sleep state.

### 8.4 Inventory and avatar actions

Common endpoints:

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/user/inventory/buy` | Get gear/equipment available for purchase. |
| GET | `/user/in-app-rewards` | Get in-app reward column items. |
| POST | `/user/buy/:key` | Buy gear, armoire, potion, quest, or special item. |
| POST | `/user/buy-gear/:key` | Buy a specific gear item. |
| POST | `/user/purchase/:type/:key` | Purchase gem or gem-purchasable item. |
| POST | `/user/hatch/:egg/:hatchingPotion` | Hatch a pet. |
| POST | `/user/equip/:type/:key` | Equip or unequip mount, pet, costume item, or battle gear. |
| POST | `/user/feed/:pet/:food` | Feed a pet. Supports `?amount=<number>`. |
| POST | `/user/sell/:type/:key` | Sell eggs, hatching potions, or food. |
| POST | `/user/release-pets` | Release pets. |
| POST | `/user/release-mounts` | Release mounts. |
| POST | `/user/release-both` | Release pets and mounts. |
| POST | `/user/revive` | Revive from death. |
| POST | `/user/rebirth` | Use Orb of Rebirth. |

For `/user/purchase/:type/:key`, the wiki notes that a `quantity` body parameter can be supplied for supported purchases, even where this is not fully reflected in the official API docs:

```json
{
  "quantity": 3
}
```

Do not expose destructive or premium-currency actions without explicit confirmation.

### 8.5 Class and skills

Common endpoints include:

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/user/change-class` | Change class. Usually requires level and/or currency conditions. |
| POST | `/user/disable-classes` | Disable class system. |
| POST | `/user/class/cast/:skill` | Cast a class skill. |
| POST | `/user/allocate` | Allocate one stat point. |
| POST | `/user/allocate-bulk` | Allocate multiple stat points. |

Automation rule: before casting skills automatically, verify mana and target validity. Stop the loop when the action can no longer be completed.

### 8.6 Blocking and private messages

Common endpoints:

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/user/block/:uuid` | Block or unblock a user from sending PMs. |
| POST | `/members/send-private-message` | Send a private message. |

Treat PM features as abuse-sensitive. Add local validation, rate limiting, and clear UX around recipient identity.

## 9. Task API

Task types:

```text
habit | daily | todo | reward
```

Task difficulty values:

```text
0.1 = Trivial
1   = Easy
1.5 = Medium
2   = Hard
```

Task attributes:

```text
str | int | per | con
```

### 9.1 Create user task

```http
POST /tasks/user
Content-Type: application/json

{
  "text": "Write API client sync tests",
  "type": "todo",
  "alias": "api-client-sync-tests",
  "notes": "Cover create/update/score/delete flows.",
  "tags": ["ed427623-9a69-4aac-9852-13deb9c190c3"],
  "checklist": [
    { "text": "Create fixtures", "completed": true },
    { "text": "Add retry tests", "completed": false }
  ],
  "priority": 2
}
```

The endpoint accepts either a single task object or an array of task objects. It returns a task object for single creation and an array for batch creation.

Common fields:

| Field | Type | Applies to | Notes |
| --- | --- | --- | --- |
| `text` | string | all | Required. |
| `type` | string | all | Required: `habit`, `daily`, `todo`, `reward`. |
| `alias` | string | all | Alphanumeric, underscores, and dashes. Must be unique for the user. |
| `notes` | string | all | Markdown-supported text. |
| `tags` | string[] | all | Tag IDs. |
| `priority` | number | habit/daily/todo | Difficulty. |
| `attribute` | string | habit/daily/todo | `str`, `int`, `per`, `con`. |
| `checklist` | array | daily/todo | Checklist items. |
| `collapseChecklist` | boolean | daily/todo | UI state. |
| `date` | date | todo | Due date. |
| `frequency` | string | daily | `daily`, `weekly`, `monthly`, `yearly`. |
| `repeat` | object | daily | Weekly repeat map: `su`, `m`, `t`, `w`, `th`, `f`, `s`. |
| `everyX` | number | daily | Repeat interval. |
| `daysOfMonth` | int[] | daily | Monthly repeats. |
| `weeksOfMonth` | int[] | daily | Monthly repeats. |
| `startDate` | date | daily | First available date. |
| `up` | boolean | habit | Enables positive scoring. |
| `down` | boolean | habit | Enables negative scoring. |
| `value` | number | reward | Gold cost. |

Daily creation examples:

```json
{
  "text": "Workout",
  "type": "daily",
  "frequency": "weekly",
  "repeat": {
    "m": true,
    "t": false,
    "w": true,
    "th": false,
    "f": true,
    "s": false,
    "su": false
  },
  "startDate": "2026-04-24",
  "priority": 1.5
}
```

Habit creation example:

```json
{
  "text": "Drink water",
  "type": "habit",
  "up": true,
  "down": false,
  "priority": 0.1
}
```

Reward creation example:

```json
{
  "text": "Play 30 minutes",
  "type": "reward",
  "value": 10
}
```

### 9.2 Get user tasks

```http
GET /tasks/user
```

By default, returns all active habits, dailies, todos, and rewards. Completed todos are excluded unless explicitly requested.

Query parameters:

| Parameter | Values | Notes |
| --- | --- | --- |
| `type` | `habits`, `dailys`, `todos`, `rewards`, `completedTodos` | `completedTodos` returns only the 30 most recently completed todos. |
| `dueDate` | date | Used to compute `nextDue` for returned tasks. |

Examples:

```http
GET /tasks/user?type=dailys
GET /tasks/user?type=completedTodos
GET /tasks/user?type=todos&dueDate=2026-04-24
```

Implementation note: server code contains `_allCompletedTodos` as a beta/internal option likely to be removed. Do not use it in production clients.

### 9.3 Update task

```http
PUT /tasks/:taskId
Content-Type: application/json

{
  "notes": "Replace notes; unspecified fields remain unchanged."
}
```

`taskId` may be the task ID or alias. Prefer the server ID in stored client state.

Send partial updates only. Do not PUT a full stale task object unless the user explicitly edited every field.

### 9.4 Delete task

```http
DELETE /tasks/:taskId
```

Use confirmation for destructive UI actions.

### 9.5 Score task

```http
POST /tasks/:taskId/score/:direction
```

`direction`:

```text
up | down
```

Examples:

```http
POST /tasks/829d435b-edc4-498c-a30e-e52361a0f35a/score/up
POST /tasks/api-client-sync-tests/score/down
```

The response contains updated user stats plus scoring metadata:

```json
{
  "success": true,
  "data": {
    "delta": 0.9746999906450404,
    "_tmp": {},
    "hp": 49.0,
    "mp": 37.2,
    "exp": 101.9,
    "gp": 77.0,
    "lvl": 19,
    "class": "rogue",
    "points": 0,
    "str": 5,
    "con": 3,
    "int": 3,
    "per": 8,
    "buffs": {}
  },
  "notifications": []
}
```

Scoring can also return drop and quest progress data under `_tmp`.

Client behavior:

- Refresh the affected task list after scoring a daily or todo.
- Refresh user stats after any score operation.
- Do not assume task completion toggling semantics are identical for habits, dailies, todos, and rewards.
- For group tasks requiring approval, a score request can return an approval-request state rather than immediate completion.

### 9.6 Move task

```http
POST /tasks/:taskId/move/to/:position
```

`position` is numeric. Completed todos are not sortable and do not appear in `user.tasksOrder.todos`.

After moving, refresh the relevant `tasksOrder` and task list.

### 9.7 Checklist operations

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/tasks/:taskId/checklist` | Add checklist item. |
| POST | `/tasks/:taskId/checklist/:itemId/score` | Toggle checklist item completion. |
| PUT | `/tasks/:taskId/checklist/:itemId` | Update checklist item. |
| DELETE | `/tasks/:taskId/checklist/:itemId` | Delete checklist item. |

Checklist items are valid only for dailies and todos.

Add item example:

```json
{
  "text": "Do this subtask"
}
```

## 10. Tags API

Tags are stored on the user and referenced by task IDs.

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/tags` | Create tag. |
| GET | `/tags` | List user tags. |
| GET | `/tags/:tagId` | Get one tag. |
| PUT | `/tags/:tagId` | Rename/update tag. |
| DELETE | `/tags/:tagId` | Delete tag. |
| POST | `/reorder-tags` | Reorder tags. |

Create tag:

```http
POST /tags
Content-Type: application/json

{
  "name": "Work"
}
```

Reorder tag:

```http
POST /reorder-tags
Content-Type: application/json

{
  "tagId": "c6855fae-ca15-48af-a88b-86d0c65ead47",
  "to": 0
}
```

Deleting a tag removes the tag reference from all user tasks.

## 11. Content API

```http
GET /content
```

Returns available content objects such as eggs, hatching potions, food, quests, gear, and other game content.

Use this endpoint to resolve valid keys for item-related routes such as:

```http
POST /user/hatch/:egg/:hatchingPotion
POST /user/feed/:pet/:food
POST /user/purchase/:type/:key
```

Client recommendations:

- Cache content data aggressively.
- Refresh content on app startup, app version change, or when server content version changes if exposed by the response.
- Do not hardcode item keys unless a feature is intentionally pinned to a known item.

## 12. Members API

### 12.1 Get member by ID

```http
GET /members/:memberId
```

Returns public member profile data including stats, profile, preferences, party, inventory summary, achievements, and auth timestamp metadata.

### 12.2 Get member by username

```http
GET /members/username/:username
```

The username may be passed with or without `@`.

### 12.3 Get member achievements

```http
GET /members/:memberId/achievements
```

Returns achievements grouped into categories such as basic, seasonal, and special.

### 12.4 Send private message

```http
POST /members/send-private-message
Content-Type: application/json

{
  "toUserId": "99999999-9999-9999-9999-8f14c101aeff",
  "message": "Message text"
}
```

Add local abuse prevention for bulk messaging. Do not automate PMs without explicit user intent.

## 13. Groups, parties, and guilds

Special group IDs:

```text
party     = authenticated user's party
habitrpg  = Tavern
<uuid>    = specific guild or group
```

Common endpoints:

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/groups/:groupId` | Get group. |
| PUT | `/groups/:groupId` | Update group; leader/moderator permissions required depending on group. |
| POST | `/groups/:groupId/join` | Join group. |
| POST | `/groups/:groupId/reject-invite` | Reject invite. |
| POST | `/groups/:groupId/leave` | Leave group. |
| POST | `/groups/:groupId/removeMember/:memberId` | Remove member; leader/admin restrictions apply. |
| GET | `/groups/:groupId/members` | Get members for group. |
| GET | `/groups/:groupId/invites` | Get pending invites for group. |
| POST | `/groups/:groupId/chat` | Post chat message. |
| GET | `/groups/:groupId/chat` | Get chat. |
| DELETE | `/groups/:groupId/chat/:chatId` | Delete chat message where permitted. |

### 13.1 Group members pagination

```http
GET /groups/:groupId/members
```

Default limit is 30. Maximum documented limit is 60.

Query parameters:

| Parameter | Notes |
| --- | --- |
| `lastId` | Last returned member ID from previous page. Used for pagination. |
| `limit` | Optional. Max 60. |
| `includeAllPublicFields` | If `true`, returns public member fields similar to single-member fetch. |
| `search` | Search profile name / username. |

Pagination pattern:

1. Request `/groups/:groupId/members?limit=60`.
2. Sort is by `_id` ascending on the server.
3. Store the last returned member ID.
4. Request `/groups/:groupId/members?limit=60&lastId=<last-id>`.
5. Stop when the response contains fewer than requested limit.

For large guilds, expose cancellation and progress. Do not run multiple full member scans concurrently for the same account.

### 13.2 Party and quest operations

Common party quest endpoints include:

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/groups/party/quests/invite/:questKey` | Invite party to quest. |
| POST | `/groups/party/quests/accept` | Accept quest invitation. |
| POST | `/groups/party/quests/reject` | Reject quest invitation. |
| POST | `/groups/party/quests/abort` | Abort active quest where permitted. |
| POST | `/groups/party/quests/force-start` | Force-start quest where permitted. |
| POST | `/groups/party/quests/leave` | Leave quest where permitted. |

Quest state is stored primarily on the party/group and user party fields. Refresh party/group state after quest actions.

Known nuance: historical GitHub discussion notes that group quest data may reflect progress from each user's last cron and may not show all in-day progress in every context. Treat quest progress displays as eventually consistent unless verified by a fresh party state response.

### 13.3 Party & Guild Data Tool observations

The Party & Guild Data Tool demonstrates several practical concerns for group clients:

- `groupId` accepts `party`, a guild UUID, or sometimes a guild URL that the tool parses.
- Large member lists must be paginated and throttled.
- Fetching full member details is expensive; use it only when required.
- Member activity is context-dependent. The tool describes "last active" as a derived value from cron, chat, drop, buff, or transformation activity depending on group context.
- Only one instance of a heavy third-party tool should be used at a time to avoid duplicate or excessive calls.

## 14. Challenges

Challenges are group-linked task templates/competitions. Common operations include creating challenges, listing user/group challenges, joining/leaving challenges, getting challenge tasks, and managing challenge tasks.

Task endpoints include:

```http
POST /tasks/challenge/:challengeId
GET /tasks/challenge/:challengeId
```

Creating challenge tasks is restricted to the challenge leader. Challenge task creation accepts mostly the same task fields as user task creation.

Member listing:

```http
GET /challenges/:challengeId/members
```

Use the same pagination mindset as group member listing.

Client considerations:

- Challenge-owned tasks can have different modification rules from user-owned tasks.
- Preserve `challenge` metadata on tasks.
- Do not assume a user can edit a task just because it appears in their task list.
- If a task belongs to a challenge or group, verify permissions before exposing edit/delete controls.

## 15. Webhooks

Habitica can POST event data to user-configured webhook URLs.

Webhook management is available through the API and the website's API settings. Third-party tools such as Habitica Webhook Editor and Habitica Api-Helper Tool expose options that may not be visible in the main website UI.

Common webhook management endpoints:

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/user/webhook` | List webhooks. |
| POST | `/user/webhook` | Create webhook. |
| PUT | `/user/webhook/:id` | Update webhook. |
| DELETE | `/user/webhook/:id` | Delete webhook. |

Typical creation payload:

```json
{
  "url": "https://example.com/habitica/webhook",
  "label": "NewHabiticaClient task webhook",
  "type": "taskActivity",
  "enabled": true,
  "options": {
    "created": true,
    "updated": true,
    "deleted": true,
    "scored": true,
    "checklistScored": true
  }
}
```

Webhook categories described by Habitica community documentation:

| Webhook type | Events |
| --- | --- |
| `taskActivity` | task created, updated, deleted, scored, checklist item scored |
| `questActivity` | quest started, finished, invited |
| `userActivity` | pet hatched, mount raised, level-up |
| `groupChatReceived` | chat received from a selected party/guild |

Webhook receiver requirements:

- Habitica sends a POST request to the configured URL.
- Respond quickly with a 2xx status.
- The webhook timeout is documented by the wiki as 30 seconds.
- Best practice is to acknowledge immediately with a small non-empty response and process asynchronously on your side.
- Validate payload shape defensively.
- Add idempotency where possible; webhook delivery can be retried or duplicated in distributed systems.
- Do not perform long external calls before responding to Habitica.

Example receiver response:

```json
{ "ok": true }
```

Example task webhook fields commonly observed:

```json
{
  "webhookType": "taskActivity",
  "type": "scored",
  "task": {
    "id": "829d435b-edc4-498c-a30e-e52361a0f35a",
    "alias": "api-client-sync-tests",
    "type": "todo",
    "text": "Write API client sync tests"
  },
  "user": {
    "_id": "12345678-90ab-416b-cdef-1234567890ab"
  }
}
```

## 16. User Data Display Tool observations

The User Data Display Tool is read-only. It uses the user's API credentials to fetch data from Habitica and does not intentionally mutate the account.

Useful implementation lessons:

- Treat the API token as a password.
- Do not send credentials anywhere except Habitica.
- Avoid saving credentials in browser storage unless the user explicitly chooses persistence and understands the risk.
- Provide a clear "clear data" path for browser-based tools.
- Useful derived views can be built from the public API: task overview, task stats, untagged tasks, habit trends, habit history, daily history, quest progress, missing equipment, and drops received today.

For a production client, avoid embedding long-lived API tokens into static pages without a clear threat model. A static web client cannot fully protect a user's API token from browser extensions or injected scripts.

## 17. Request construction examples

### 17.1 cURL

```bash
curl --compressed \
  -X GET "https://habitica.com/api/v3/user?userFields=stats,tasksOrder" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "x-api-user: ${HABITICA_USER_ID}" \
  -H "x-api-key: ${HABITICA_API_TOKEN}" \
  -H "x-client: ${HABITICA_TOOL_AUTHOR_ID}-NewHabiticaClient"
```

### 17.2 Create task

```bash
curl --compressed \
  -X POST "https://habitica.com/api/v3/tasks/user" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "x-api-user: ${HABITICA_USER_ID}" \
  -H "x-api-key: ${HABITICA_API_TOKEN}" \
  -H "x-client: ${HABITICA_TOOL_AUTHOR_ID}-NewHabiticaClient" \
  --data '{
    "text": "Ship Habitica client MVP",
    "type": "todo",
    "priority": 2
  }'
```

### 17.3 Score task

```bash
curl --compressed \
  -X POST "https://habitica.com/api/v3/tasks/${TASK_ID}/score/up" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "x-api-user: ${HABITICA_USER_ID}" \
  -H "x-api-key: ${HABITICA_API_TOKEN}" \
  -H "x-client: ${HABITICA_TOOL_AUTHOR_ID}-NewHabiticaClient"
```

### 17.4 Fetch party members with pagination

```bash
curl --compressed \
  -X GET "https://habitica.com/api/v3/groups/party/members?limit=60" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "x-api-user: ${HABITICA_USER_ID}" \
  -H "x-api-key: ${HABITICA_API_TOKEN}" \
  -H "x-client: ${HABITICA_TOOL_AUTHOR_ID}-NewHabiticaClient"
```

Next page:

```bash
curl --compressed \
  -X GET "https://habitica.com/api/v3/groups/party/members?limit=60&lastId=${LAST_MEMBER_ID}" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "x-api-user: ${HABITICA_USER_ID}" \
  -H "x-api-key: ${HABITICA_API_TOKEN}" \
  -H "x-client: ${HABITICA_TOOL_AUTHOR_ID}-NewHabiticaClient"
```

## 18. Client architecture recommendations

### 18.1 API client layer

Implement a single API transport layer responsible for:

- Base URL management.
- Auth headers.
- `x-client` header.
- JSON serialization.
- Rate-limit parsing.
- Retry/backoff policy.
- Error normalization.
- Request logging with credential redaction.

Do not let feature code construct raw Habitica requests directly.

### 18.2 Local model handling

Use server IDs as primary keys. Keep raw server payloads for debugging only if they do not contain secrets.

Recommended local aggregates:

- `AuthenticatedUserSnapshot`
- `TaskEntity`
- `TaskOrder`
- `TagEntity`
- `GroupSummary`
- `MemberSummary`
- `QuestState`
- `ContentCatalog`

Keep derived UI state separate from server state.

### 18.3 Sync strategy

Minimum viable sync:

1. Fetch `/user?userFields=stats,preferences,profile,party,tasksOrder,tags,items,notifications`.
2. Fetch `/tasks/user`.
3. Fetch `/content` if catalog is missing or stale.
4. Build local projections.

After task mutation:

1. Apply optimistic UI only if the operation is simple and reversible.
2. Send mutation.
3. Parse response.
4. Refresh affected task type and user stats.

After inventory, quest, class, or purchase mutation:

1. Send mutation.
2. Refresh user fields relevant to the feature.
3. Refresh content only if keys/content availability might have changed.

### 18.4 Logging

Log:

- HTTP method.
- Path without credentials.
- Status code.
- Rate-limit headers.
- Request duration.
- Habitica `error` code.
- Sanitized response message.

Do not log:

- `x-api-key`.
- Full auth headers.
- Private messages.
- Full user profile/inbox.
- Full webhook payloads unless explicitly enabled in a secure debug mode.

## 19. Security and privacy requirements

- Treat API token as a password.
- Add explicit user confirmation for purchases, destructive account actions, destructive group actions, mass task changes, PM automation, and quest actions.
- Redact credentials from logs, crash reports, analytics, screenshots, and support bundles.
- Do not send Habitica data to third-party analytics without user consent.
- For browser clients, document that API tokens are exposed to the browser runtime.
- For shared devices, provide logout and local data clearing.
- For open-source third-party tools used by others, keep code publicly reviewable as required by Habitica's guidelines.

## 20. Known pitfalls

| Pitfall | Mitigation |
| --- | --- |
| Missing `x-client` header | Always inject it in the transport layer. |
| Over-polling | Use manual refresh, webhooks, caching, and rate-limit-aware scheduling. |
| Treating API token as harmless ID | Store and redact it like a password. |
| Using API v4 | Do not use v4 for third-party clients. |
| Assuming all task IDs are UUID aliases | Store server `id`; allow alias only as a convenience. |
| Sending full stale objects in PUT | Send partial updates only. |
| Fetching full user document repeatedly | Use `userFields`. |
| Scanning large guilds in parallel | Paginate, throttle, and cancel. |
| Assuming challenge/group tasks are user-editable | Check ownership and permissions. |
| Ignoring quest eventual consistency | Refresh party state and communicate uncertainty in UI. |
| Blocking webhook responses on heavy work | Respond 2xx immediately, process asynchronously. |
| Depending on localized error messages | Use HTTP status and stable error code where possible. |

## 21. Minimal endpoint checklist for a new client

A practical first implementation should support:

| Feature | Endpoints |
| --- | --- |
| Auth check | `GET /user?userFields=profile,stats,preferences` |
| Content catalog | `GET /content` |
| Task list | `GET /tasks/user` |
| Task create | `POST /tasks/user` |
| Task edit | `PUT /tasks/:taskId` |
| Task delete | `DELETE /tasks/:taskId` |
| Task score | `POST /tasks/:taskId/score/:direction` |
| Checklist | `/tasks/:taskId/checklist...` |
| Tags | `/tags`, `/reorder-tags` |
| Party state | `GET /groups/party` |
| Party members | `GET /groups/party/members` |
| Member profile | `GET /members/:memberId`, `GET /members/username/:username` |
| Quest actions | `/groups/party/quests/...` |
| Webhooks | `/user/webhook` |

## 22. Final review checklist

Before shipping the client:

- All API requests include `x-api-user`, `x-api-key`, and `x-client` where authentication is required.
- `x-client` uses the tool author's User ID, not the current end user's User ID.
- API token is stored securely and redacted everywhere.
- `429` and `Retry-After` are handled.
- Background automation has a minimum 30-second delay between calls.
- Request loops have explicit stop conditions.
- API v4 is not used.
- Full user fetches are not used as the default refresh path.
- Large group member fetches are paginated and cancelable.
- Webhook receivers respond with 2xx quickly.
- Destructive and premium-currency operations require explicit confirmation.
- Unknown fields do not break parsing.
- Missing optional fields do not break parsing.
