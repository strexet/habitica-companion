# CRON.md

# Habitica Cron Concept

This document describes Habitica's **Cron** concept for third-party client development and AI agents.

The scope is intentionally limited to Cron-related behavior: daily reset timing, server-side effects, API implications, and party-member state detection for features such as better group buff timing.

## 1. Short Definition

In Habitica, **Cron** is the beginning-of-day processing step for a user account.

Cron is **not** a global scheduled job that runs for every user at midnight. Instead, it is user-specific and request-triggered:

- each user has their own Habitica day boundary;
- the boundary is controlled by the user's Custom Day Start setting;
- Cron runs when the user's account interacts with Habitica for the first time after that boundary;
- if the user does not interact with Habitica, their Cron does not run at that time.

For client development, the practical meaning is:

> A user has "started today's Habitica day" only after their Cron has run for the current Habitica day.

This matters for party buffing because buffs expire for each user when that user's next Cron runs.

## 2. Core Mental Model

Habitica Cron behaves like a per-user daily latch:

1. The user's Custom Day Start passes.
2. The user's account becomes eligible for Cron.
3. The next qualifying interaction triggers Cron.
4. Cron processes the previous Habitica day.
5. The user's `lastCron` is updated.
6. The user is now considered to be in the new Habitica day.

This means two party members can be in different Cron states at the same real-world time:

- Member A may have already triggered today's Cron.
- Member B may still be in "yesterday" from Habitica's perspective.
- Buffing Member B before their Cron is usually wasteful, because the buff can disappear as soon as they next open/sync Habitica.

## 3. Triggering Cron

Cron can be triggered by ordinary user interactions after the user's Custom Day Start.

Examples of interactions that can trigger Cron include:

- logging in;
- reloading the web app;
- syncing the mobile app;
- ticking off a task;
- buying a reward;
- changing equipment;
- casting a skill.

Cron does **not** necessarily run exactly at the Custom Day Start time. If the account is inactive at that moment, Cron waits until the next interaction.

## 4. Custom Day Start

The **Custom Day Start** setting defines when the user's Habitica day ends and the next one becomes eligible to start.

Important details:

- The default Custom Day Start is midnight in the user's timezone.
- Habitica treats Custom Day Start as an offset from midnight.
- A Custom Day Start after midnight delays the reset; it does not make the day reset earlier.
- Example: if Custom Day Start is `2`, then the user's previous Habitica day continues until 02:00 local time.
- Example: if Custom Day Start is `23`, the user's previous Habitica day continues almost the entire next real-world day and only resets at 23:00.

For a third-party client, never assume that calendar midnight equals Habitica day reset.

## 5. Record Yesterday's Activity

Habitica has a "Record Yesterday's Activity" flow, also called the "Check off any Dailies you did yesterday" screen.

When a user interacts with Habitica after Custom Day Start:

- if there are incomplete Dailies that require confirmation before damage, Habitica may show this screen first;
- Cron should run only after the user confirms yesterday's activity;
- once the user chooses to start the new day, Cron applies the relevant missed-Daily consequences.

A third-party client that runs Cron directly can bypass this UX and immediately apply missed-Daily damage. This is dangerous and should not be done silently.

## 6. API Endpoint: Manual Cron

Habitica exposes:

```http
POST /api/v3/cron
```

Server-side API documentation in the Habitica source describes this route as "Run cron" and explicitly says it assumes the user has already been shown the Record Yesterday's Activity screen. It immediately applies damage for incomplete due Dailies.

Development guidance:

- Do **not** call this endpoint automatically just to check state.
- Do **not** call it as a harmless "sync" operation.
- Only call it when the client intentionally starts the user's new Habitica day and has handled any required "yesterday" confirmation UX.
- Prefer ordinary authenticated data-fetching/sync flows for reading state.

## 7. Main Cron Effects

When Cron runs normally, it performs beginning-of-day processing.

Important effects include:

- resets Dailies and Daily checklists;
- applies damage for incomplete due Dailies;
- applies boss quest damage caused by missed Dailies;
- applies the user's accumulated quest progress to the active quest;
- resets the user's personal quest progress accumulator;
- resets daily drop count;
- decays one-sided Habit values toward neutral;
- makes incomplete To-Dos redder;
- removes active buffs;
- calculates Perfect Day state;
- regenerates Mana based on Daily completion;
- updates login/check-in incentives;
- increments internal Cron counters;
- updates `lastCron`.

For client development, the most important consequence is that Cron is a destructive state transition. It is not just a timestamp update.

## 8. Resting in the Inn

If the user is resting in the Inn, Cron still runs, but some negative and quest-related effects are skipped.

When resting:

- incomplete Dailies do not damage the user;
- incomplete Dailies do not damage party members through a boss quest;
- missed-Daily quest progress consequences are skipped;
- Mana gain is handled differently;
- other Cron actions can still occur.

Do not treat "resting in the Inn" as "Cron disabled". It is better understood as a modified Cron mode.

## 9. Multiple Missed Days

If a user is inactive for several days, Cron does not run on each missed day in real time.

Instead:

- the next interaction after the user returns triggers Cron;
- Habitica calculates missed time relative to the user's last Cron;
- Habitica intentionally prevents the same Daily from damaging the user repeatedly for every missed day;
- many Cron effects still behave as a single Cron event rather than a full replay of every missed day.

For third-party clients and AI agents:

- do not infer that one inactive user has had multiple separate Cron events;
- do not assume quest progress or boss damage was applied daily while they were away;
- use actual user fields and server state, not a local "days passed" simulation.

## 10. Timezones and Daylight Saving Time

Cron depends on the user's timezone and Custom Day Start.

Known edge cases:

- timezone changes can make Cron happen at unexpected times;
- daylight saving time changes can cause surprising Cron behavior;
- using multiple devices with different timezone settings can cause Cron timing problems;
- changing Custom Day Start can cause unexpected results, including a second Cron-like transition in a real-world day in some cases.

Client guidance:

- preserve and display Habitica's server-provided user state whenever possible;
- avoid local-only Cron calculations as a source of truth;
- if showing "expected next Cron", clearly label it as an estimate;
- expect edge cases around timezone travel and DST.

## 11. Party Effects

Cron is especially important in parties.

### 11.1 Boss Quest Damage

When a user misses Dailies during a boss quest:

- Cron applies damage to that user from their missed Dailies;
- the boss can also damage the user;
- the boss can also damage other quest participants.

This happens when the missing user's Cron runs, not necessarily when the real-world day changes.

### 11.2 Quest Progress

Quest progress accumulated by a user is applied during that user's Cron.

This means:

- group quest state can lag behind actions performed during the current Habitica day;
- the party may not see final progress from a member until that member's next Cron;
- API data for group quest progress may reflect progress processed at member Cron boundaries rather than every immediate task action.

### 11.3 Party Buffs

Party buffs expire for each member when that member's next Cron runs.

Therefore:

- casting buffs before a member has started their current Habitica day may waste the buff on that member;
- the member may lose the buff almost immediately when they next trigger Cron;
- the best buff timing is usually after all relevant party members have triggered Cron for the current Habitica day, but before they complete many tasks.

This is the key reason a third-party client may need Cron information for party members.

## 12. Determining Whether a Party Member Has Logged In / Croned Today

For group buffing, the useful question is not literally "has the user logged in today?"

The better question is:

> Has this party member triggered Cron for their current Habitica day?

A member can open Habitica before their Custom Day Start and still remain in the previous Habitica day. Conversely, a member can trigger Cron through many interactions, not only login.

### 12.1 Preferred State Label

Use labels that describe Cron state directly:

- `Croned today`
- `Not croned yet`
- `Unknown`
- `Possibly stale`

Avoid labels like `Online today` unless the app has actual presence/activity data.

### 12.2 Data Needed

To determine state accurately, a client needs at least:

- member identifier;
- member `lastCron`;
- member Custom Day Start;
- member timezone or UTC offset information used by Habitica;
- current server time or a trusted current timestamp.

Depending on API permissions and returned public fields, some of this data may not be available for other party members. If `lastCron`, Custom Day Start, or timezone data is missing, mark the result as `Unknown` rather than guessing.

### 12.3 Practical Algorithm

For each party member:

1. Fetch member data from Habitica.
2. Read `lastCron`.
3. Read Custom Day Start / timezone-related fields if available.
4. Compute the start of the member's current Habitica day.
5. If `lastCron >= currentHabiticaDayStart`, classify as `Croned today`.
6. If `lastCron < currentHabiticaDayStart`, classify as `Not croned yet`.
7. If required fields are missing, classify as `Unknown`.

Pseudo-code:

```ts
type CronState = "CRONED_TODAY" | "NOT_CRONED_YET" | "UNKNOWN";

function getCronState(member: HabiticaMember, now: Date): CronState {
  if (!member.lastCron) return "UNKNOWN";
  if (!member.preferences?.dayStart) return "UNKNOWN";
  if (!hasUsableTimezoneData(member)) return "UNKNOWN";

  const habiticaDayStart = computeHabiticaDayStart(
    now,
    member.preferences.dayStart,
    member.preferences.timezoneOffset
  );

  return new Date(member.lastCron) >= habiticaDayStart
    ? "CRONED_TODAY"
    : "NOT_CRONED_YET";
}
```

### 12.4 Important Caveat

The algorithm above is conceptually correct, but production code should be careful:

- Habitica timezone fields can be counterintuitive because JavaScript timezone offsets use inverted signs compared with common UTC notation.
- Habitica also stores timezone-related fields around Cron, such as offset-at-last-Cron values.
- Official server state should win over local estimates.
- Always test against real accounts in different timezones and with non-midnight Custom Day Start values.

## 13. Suggested Party Buffing UX

A third-party client can use Cron state to improve party buff timing.

Suggested member states:

| State | Meaning | Buffing Advice |
| --- | --- | --- |
| `Croned today` | Member has already crossed today's Cron boundary and triggered Cron. | Safe to buff, assuming they have not already completed most tasks. |
| `Not croned yet` | Member's current Habitica day has started by time, but their Cron has not run yet. | Buff may be wasted; wait if possible. |
| `Unknown` | Client lacks required fields or confidence. | Do not block buffing, but show uncertainty. |
| `Possibly stale` | Data is old or fetched before a likely Cron boundary. | Refresh before recommending. |

Useful group-level messages:

- `3/5 members have croned today. Buffs may be wasted on 2 members.`
- `All visible members have croned today. This is a good buff window.`
- `Cron state unknown for 1 member because required profile fields are not available.`

## 14. API Notes for Party Member Fetching

Habitica's public API includes group/member endpoints, including a route for getting members of a group:

```http
GET /api/v3/groups/:groupId/members
```

For the authenticated user's party, clients commonly use `party` as the group id:

```http
GET /api/v3/groups/party/members
```

Some tools use query parameters such as:

```http
includeAllPublicFields=true
includeTasks=true
```

Implementation guidance:

- request the minimum data needed;
- avoid fetching tasks unless a feature truly needs them;
- treat API tokens as passwords;
- do not expose or log another user's private task data;
- gracefully handle missing fields for party members.

## 15. Do Not Simulate Cron Locally

A third-party client should not try to reproduce Habitica Cron effects locally.

Reasons:

- Cron changes many different parts of the user model;
- task scoring formulas are non-trivial;
- missed-day behavior has intentional special cases;
- quest progress is processed through server logic;
- safe mode / semi-safe mode and server-side implementation details may change;
- local simulation can easily disagree with the server.

Allowed local logic:

- estimate whether a member has likely croned today;
- estimate next possible Cron time;
- display warnings and recommendations;
- decide whether buff timing looks good.

Forbidden / risky local logic:

- applying task damage locally;
- estimating exact HP/MP/quest changes as authoritative;
- assuming quest progress before server Cron processing;
- triggering Cron without explicit UX.

## 16. AI Agent Rules

When an AI agent works with Habitica Cron:

1. Treat Cron as a server-side state transition, not a simple daily timer.
2. Never assume midnight reset.
3. Always account for Custom Day Start.
4. Prefer `lastCron` and server-provided state over local guesses.
5. For party buffing, ask: "Has each member already croned for the current Habitica day?"
6. If required member fields are missing, return `Unknown`, not a confident answer.
7. Do not recommend calling `POST /api/v3/cron` unless the user explicitly wants to start their new day and understands the consequences.
8. Do not trigger Cron as a background automation without a clear user opt-in.
9. Be careful with Resting in the Inn; Cron still runs but has modified effects.
10. Keep party UX non-punitive: use Cron state to improve timing, not to shame users.

## 17. Developer Checklist

Before implementing Cron-aware party buffing:

- [ ] Fetch current authenticated user.
- [ ] Fetch party members.
- [ ] Confirm whether member `lastCron` is available.
- [ ] Confirm whether member Custom Day Start is available.
- [ ] Confirm whether timezone/offset data is available and reliable.
- [ ] Implement `Croned today` / `Not croned yet` / `Unknown`.
- [ ] Add stale-data detection.
- [ ] Add clear UI copy explaining that buff timing depends on each member's Cron.
- [ ] Avoid calling `POST /api/v3/cron` unless explicitly intended.
- [ ] Test with:
  - midnight Custom Day Start;
  - non-midnight Custom Day Start;
  - users in different timezones;
  - users who have not opened Habitica today;
  - users resting in the Inn;
  - active boss quest;
  - collection quest;
  - recently changed timezone / DST if possible.

## 18. Source Notes

Primary references used for this document:

- Habitica API documentation: https://habitica.com/apidoc/
- Habitica Wiki — Cron: https://habitica.fandom.com/wiki/Cron
- Habitica Wiki — Custom Day Start: https://habitica.fandom.com/wiki/Custom_Day_Start
- Habitica source — `website/server/libs/cron.js`: https://raw.githubusercontent.com/HabitRPG/habitica/develop/website/server/libs/cron.js
- Habitica source — `website/server/controllers/api-v3/cron.js`: https://raw.githubusercontent.com/HabitRPG/habitica/develop/website/server/controllers/api-v3/cron.js
- Habitica API usage notes: https://habitica.fandom.com/wiki/Application_Programming_Interface

The implementation details in Habitica source may change. Treat this document as a development guide and re-check official API/source behavior before building destructive Cron actions.
