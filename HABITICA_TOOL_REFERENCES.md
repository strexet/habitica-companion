# HABITICA_TOOL_REFERENCES.md

Last updated: 2026-04-27
Primary audience: AI agents and developers implementing Habitica data features

This document records practical data-fetching patterns from stable third-party Habitica tools. Use it as supporting evidence after `HABITICA_API.md`; do not treat it as higher priority than official API behavior or current verified responses.

## Sources

- Habitica User Data Display Tool: `https://tools.habitica.com/habitica_user_data_display.html`
- Source repository: `https://github.com/HabitRPG/tools-for-habitica`
- Party & Guild Data Tool: `https://oldgods.net/habitica/cTheDragons/group.html`
- Party & Guild Data Tool source repository: `https://github.com/cTheDragons/Habitica-Party-Guild-Data-Tool`
- Party & Guild rate-limit helper: `https://oldgods.net/habitica/cTheDragons/js/api-limit.js`

## Habitica User Data Display Tool

Observed workflow:

```text
GET /user
GET /tasks/user
GET /tasks/user?type=_allCompletedTodos
GET /content?language=en
GET /groups/habitrpg
GET /groups/party
```

Request behavior:

- Uses Habitica API v3 at `https://habitica.com/api/v3`.
- Sends `x-client`, `x-api-user`, and `x-api-key` for authenticated user, task, tavern, and party calls.
- Sends `x-client` for content calls.
- Fetches full `/user` for the main account snapshot instead of relying on narrow `userFields`.
- Keeps the tool read-only. Its source explicitly warns against adding account-mutating features.
- Handles `TooManyRequests` and `NotAuthorized` with user-facing messages, including a retry-after-wait recommendation for rate limits.

Quest progress behavior:

- Uses `user.party.quest.progress.up` as the authenticated user's pending boss damage.
- Uses `user.party.quest.progress.collectedItems` as the authenticated user's pending collection progress.
- Uses `party.quest.progress.hp` for current boss HP remaining.
- Uses `content.quests[party.quest.key].boss` for quest boss metadata and total HP.
- Rounds displayed pending boss damage down so users do not overestimate progress.
- Rounds displayed boss HP remaining up so users do not underestimate remaining HP.
- Explains that boss HP from the party page does not include damage caused since the last party member CRON applied it.

Useful design takeaways:

- Prefer full `/user` when computed user stats or full account state are needed.
- Keep read-only data pages obviously non-mutating.
- Treat quest progress as eventually consistent around CRON application.
- Show conservative rounding for quest progress.

## Party & Guild Data Tool

Observed workflow for group or party fetch:

```text
GET /user
GET /content?language=en
GET /groups/party
GET /groups/:groupId
GET /challenges/groups/:groupId
GET /groups/:groupId/members?limit=60&includeAllPublicFields=true
GET /groups/:groupId/members?limit=60&includeAllPublicFields=true&lastId=:lastMemberId
GET /members/:memberId
```

Request behavior:

- Uses Habitica API v3 at `https://habitica.com/api/v3`.
- Uses `/groups/party` when the selected group is the authenticated user's party.
- Uses `/groups/:groupId` for guild-like group reads.
- Reads member lists with `limit=60` and `includeAllPublicFields=true`.
- Paginates member lists by passing the last returned member ID as `lastId`.
- Adds leader and quest leader IDs to the member fetch list if they were not present in the paged member list.
- Falls back to `GET /members/:memberId` for specific missing members.
- Warns that large member fetches take longer and should be limited by the user.

Rate-limit behavior:

- Tracks `X-RateLimit-Remaining` and `X-RateLimit-Reset`.
- When remaining calls are low and a reset is close, pauses before continuing.
- On HTTP 429, reads `Retry-After`, adds jitter, and retries after the delay.
- Queues batches and spreads calls when there are more calls than safe remaining capacity.

Quest progress behavior:

- Reads active quest metadata from the group/party object.
- Uses `content.quests[group.quest.key]` for boss and collection quest metadata.
- Uses `group.quest.progress.hp` for active boss HP remaining, falling back to content boss HP when current HP is missing.
- Uses `content.quests[group.quest.key].boss.hp` as the boss total HP.
- Uses each member's `party.quest.progress.up` as that member's pending boss damage.
- Uses each member's `party.quest.progress.collectedItems` as that member's pending collection quest item count.
- For active quests, counts pending member damage only for members marked true in `group.quest.members`.
- For pending quests, counts pending progress from all fetched members with returned quest progress.
- Sums member pending boss damage or collection item counts into a party total and displays it against current quest progress.
- Sorts member pending damage by member last CRON to estimate which member's next CRON could complete a quest.
- Marks users as ignored in completion estimates when they are resting in the inn or have not checked in for more than 24 hours.
- Notes that chat-derived quest progress can be inaccurate if a player changed their display name recently.

Useful design takeaways:

- `includeAllPublicFields=true` can expose useful member-side party quest progress, including `party.quest.progress.up`, when Habitica returns it.
- Total party pending boss damage/items should be computed from member progress values, not guessed from the party group's top-level progress.
- Member list pagination must handle inaccurate group `memberCount` values.
- Avoid unbounded member detail fetches; prefer the paged public member list and fetch individual member profiles only when a needed member is missing.
- Rate-limit handling should use both remaining/reset headers and `Retry-After`.

## Applicability To This App

Rules for this repository:

- Keep read-only pages read-only unless a feature explicitly requires mutation and follows `RULES.md` mutation constraints.
- Prefer the API client layer for every Habitica call.
- When implementing party quest progress, first try `GET /groups/party` plus `GET /groups/party/members?includeAllPublicFields=true`.
- Compute total party pending boss damage from public member `party.quest.progress.up` values when returned.
- Compute total party pending collection items from public member `party.quest.progress.collectedItems` values when returned.
- Resolve boss total HP from `GET /content?language=en`.
- Treat all third-party tool observations as optional API fields, because Habitica can omit or reshape public member data.
- Keep rate-limit-sensitive workflows explicit and user-triggered.
