# Habitica Companion Client

Status: current product vision, not an implementation source of truth. Use `FEATURES.md` for current implemented behavior and `FUTURE.md` for active planned work.

## 1. Project Summary

Habitica Companion Client is a third-party companion application for Habitica users who want deeper planning, analysis, automation, and visibility over their Habitica data.

The project is not intended to replace the official Habitica client. The official client remains the primary place for everyday task management, social interaction, and normal gameplay. This project focuses on advanced helper workflows that are difficult to perform manually inside the official application.

The application reads Habitica data through the public Habitica API, stores relevant data locally on the user's device, and uses that data to provide recommendations, calculations, presets, and assisted actions.

## 2. Product Positioning

Habitica is a habit-building and productivity application that turns real-life tasks into a role-playing game experience. Users manage habits, dailies, to-dos, rewards, characters, equipment, parties, quests, pets, mounts, and other game systems.

Habitica Companion Client is designed for users who enjoy the game mechanics and want more control over optimization-heavy actions, such as equipment selection, skill usage, party coordination, and inventory cleanup.

The project should be treated as a power-user tool.

It should be useful for regular users, but its main audience is users who already understand Habitica mechanics and want better support for planning and decision-making.

## 3. Core Idea

The application helps users answer questions such as:

- When is the best time to cast a party buff?
- Which equipment should I wear before casting a specific skill?
- Which task is the best target for a given skill?
- Can I switch to a prepared battle setup with one action?
- Can I execute a safe sequence of skill casts without manually changing equipment between every step?
- Which items can probably be sold without blocking pet or mount progress?
- How much damage, gold, or other effect should I approximately expect from an action?
- How much damage can a boss or player receive under the current conditions?

The project should make these answers visible, understandable, and actionable.

## 4. Main User Value

The application should reduce repetitive manual work in Habitica.

Instead of repeatedly checking character stats, equipment bonuses, task values, party activity, inventory state, and skill effects, the user should be able to open the companion client and see clear recommendations.

The application should not blindly perform actions without explanation. Whenever it recommends an action, it should explain why the recommendation was made and which data was used.

Examples:

- A gear recommendation should show which stats improved and why those stats matter.
- A sell recommendation should show why the item appears safe to sell.
- A party buff recommendation should show which member activity data was used.
- A macro preview should show the planned steps before anything is executed.

## 5. Product Feature Areas

This section describes product direction. Some areas are already implemented partially or fully; check `FEATURES.md` and `FUTURE.md` for current status.

## 5.1 Party Buff Timing

The application should help calculate a good time to cast party-wide buffs.

The initial idea is to estimate the median login or activity time of party members and suggest a time when the buff is likely to benefit the largest number of users.

The feature should be conservative when data is incomplete. If the API does not provide enough reliable activity data, the application should clearly show that the recommendation is approximate.

## 5.2 Gear Sets

Users should be able to create named equipment presets.

Examples:

- Maximum Perception
- Maximum Strength
- Boss Damage
- Pickpocket Setup
- Backstab Setup
- General Daily Setup

A gear set should be easy to preview, edit, compare, and apply.

## 5.3 Skill Macros

Users should be able to create predefined skill sequences.

A macro may include:

- switching equipment;
- selecting the best target;
- casting one or more skills;
- switching equipment again;
- stopping if validation fails.

The macro system should be safe by default. Before execution, the application should show a dry-run preview with expected actions, costs, warnings, and possible API requests.

## 5.4 Skill Target Recommendations

The application should help find the best task target for a skill.

Examples:

- Best task for Pickpocket.
- Best task for Backstab.
- Best habit, daily, or to-do for a specific effect.
- Best target under current equipment and stats.

The recommendation should include a short explanation.

## 5.5 Equipment Optimization

The application should recommend equipment for specific goals.

Examples:

- maximize Perception for Pickpocket;
- maximize Strength for Backstab;
- balance stats for general use;
- prepare an outfit for boss damage;
- prepare an outfit for survival or reduced damage.

The user should be able to save the recommendation as a gear set.

## 5.6 Bulk Sell Helper

The application should help identify items that are likely safe to sell.

The feature should account for the user's current pet and mount progress where possible.

The application should not silently sell items only because they appear unused. Selling should require a preview and explicit confirmation.

## 5.7 Action Result Estimates

The application should show approximate results for selected actions.

Examples:

- expected damage;
- expected gold;
- expected benefit from a skill;
- expected boss progress;
- expected player damage risk.

The application should clearly distinguish between exact values, API-returned values, and local estimates.

## 6. Product Principles

## 6.1 Companion, Not Replacement

The project should not try to duplicate every official Habitica feature.

It should focus on advanced planning, calculations, automation helpers, and local data analysis.

## 6.2 Explain Recommendations

Recommendations must be explainable.

A user should be able to understand why the application suggests a specific item, task, time, skill, or macro step.

## 6.3 Prefer Safe Defaults

The application should avoid destructive or irreversible actions by default.

Potentially risky actions should use:

- preview;
- validation;
- warnings;
- explicit confirmation;
- execution logs.

## 6.4 Respect Habitica API Rules

The project must follow Habitica API rules and third-party tool expectations.

The application should use the supported public API, include required headers, respect rate limits, and avoid aggressive polling.

For technical API details, developers and agents must use `HABITICA_API.md`.

## 6.5 Local-First Data

The application should prefer local-first behavior.

Habitica data should be synchronized into a local cache or snapshot, and the UI should primarily work from local data. This improves responsiveness and reduces unnecessary API calls.

## 6.6 User-Controlled Credentials

The user must remain in control of their Habitica credentials.

The application should not send the Habitica API token anywhere except the official Habitica API. The application should provide clear controls for clearing local data and credentials.

## 7. Expected User Flow

A typical user flow:

1. The user opens the companion client.
2. The user enters their Habitica User ID and API Token.
3. The application validates access to Habitica.
4. The application synchronizes the user's profile, tasks, party data, equipment, inventory, and other relevant data.
5. The data is stored locally.
6. The user opens a feature area, such as gear optimization or party buff timing.
7. The application calculates recommendations from local data.
8. The user reviews the explanation.
9. The user may apply a gear set, run a macro, sell selected items, or perform another assisted action.
10. The application updates local state after each successful API operation.

## 8. Non-Goals

The project should not initially focus on:

- replacing the official Habitica task UI;
- rebuilding social features;
- chat replacement;
- full quest management replacement;
- real-time multiplayer interaction;
- complex game-like 3D or 2D presentation;
- server-side storage of user API tokens;
- automatic background automation without user visibility.

These areas may be reconsidered later, but they are not part of the initial product direction.

## 9. Target Audience

Primary audience:

- experienced Habitica users;
- party members who coordinate quests and buffs;
- users who optimize gear and skills;
- users who want better visibility into character and task data;
- users who are comfortable using a third-party tool with their own API credentials.

Secondary audience:

- developers building Habitica-related tools;
- users who want to inspect their Habitica data in a clearer format;
- party leaders who want better coordination support.

## 10. Tone and UX Direction

The product should feel helpful, calm, and trustworthy.

It should not feel like a bot that secretly plays the game for the user. It should feel like a planner, dashboard, and assistant that helps the user make better decisions.

Recommended UX direction:

- clear dashboards;
- readable tables;
- compact explanations;
- dry-run previews;
- visible warnings;
- local sync status;
- reversible configuration;
- no hidden destructive behavior.

## 11. Relationship to Technical Documentation

This document describes the project from a user-friendly product perspective.

More technical details are stored in:

- `HABITICA_API.md` — Habitica API behavior and usage rules.
- `TECHNICAL.md` — technical stack and architecture.
- `FEATURES.md` — detailed feature behavior.
- `FUTURE.md` — active implementation queue and validated backlog.
- `RULES.md` — repository and AI-agent collaboration rules.

When this project vision changes, this file should be updated together with the relevant technical documents.
