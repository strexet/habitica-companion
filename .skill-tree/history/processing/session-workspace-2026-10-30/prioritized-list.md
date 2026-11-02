# Prioritized queue after session/Workspace processing

All rows retain stored status Ready. Dependency and material execution blockers remain explicit; readiness grants no execution authority.

| Rank | Task ID | Priority | Intended result | Unmet dependencies or material blocker |
| --- | --- | --- | --- | --- |
| 1 | T-1481b473-244b-4918-b498-775fd1efcaf4 | 107 | Restore existing integration, Workspace and Notes sessions without enrollment | None in task dependency graph |
| 2 | T-be29adc1-276c-417d-8f3d-434e34d17a0d | 107 | Persist account settings and restore one active Workspace across sessions | T-1481b473-244b-4918-b498-775fd1efcaf4 |
| 3 | T-b56ed4eb-6a2a-4d48-8d2f-c1ee5729b723 | 107 | Move browser tasks and Notes into one cloud Workspace with recoverable adoption | T-be29adc1-276c-417d-8f3d-434e34d17a0d |
| 4 | T-fcb3bef0-1d46-433f-bf15-d90b7f359048 | 107 | Expose reviewed Workspace adoption and original-transfer recovery through MCP | T-b56ed4eb-6a2a-4d48-8d2f-c1ee5729b723 |
| 5 | T-726f81ba-4130-4b59-b793-686298251e6c | 107 | Deliver and qualify one Workspace across Settings, tasks and Notes | T-be29adc1-276c-417d-8f3d-434e34d17a0d, T-b56ed4eb-6a2a-4d48-8d2f-c1ee5729b723, T-fcb3bef0-1d46-433f-bf15-d90b7f359048 |
| 6 | T-ec421773-5864-4bc5-b17f-ce04e0d818c2 | 101 | Qualify the selected Netlify callback gateway deployment | None in task dependency graph; owner-controlled hosted fixtures, account/site and runtime execution scope required |
| 7 | T-23342106-269e-4cd6-992e-4ff95be3e62d | 100 | Browse and manage standalone and project Notes folders | T-94b18897-2c2f-44ab-b89b-34e58b3903c5 |
| 8 | T-94b18897-2c2f-44ab-b89b-34e58b3903c5 | 100 | Expose reviewed Notes folder workflows through MCP | T-7aa20175-78af-40c9-86a3-e1ea37109c34 |
| 9 | T-7aa20175-78af-40c9-86a3-e1ea37109c34 | 100 | Persist Notes folders through the cloud owner API | T-5111060c-76ae-4a0e-a48b-260ecbb8885f |
| 10 | T-5111060c-76ae-4a0e-a48b-260ecbb8885f | 100 | Add versioned Notes folders and local persistence | None in task dependency graph |
| 11 | T-569ff3a0-8765-49b6-9b37-a62e091617ae | 99 | Deliver due task reminder events with durable original receipts | T-280cb859-c0f7-46e5-97db-bbd41dc6a99a |
| 12 | T-280cb859-c0f7-46e5-97db-bbd41dc6a99a | 99 | Implement Auth-owned callback secrets and reviewed event subscriptions | T-ec421773-5864-4bc5-b17f-ce04e0d818c2 |
| 13 | T-3c84f362-0f7b-4f0c-9e23-75d9a1e0725f | 99 | Show coverage-aware Daily and Habit activity streaks | None in task dependency graph |
| 14 | T-e77f6c7c-cf2f-487d-bb82-831e6f6167f9 | 99 | Edit supported linked Daily schedules with explicit reconciliation | None in task dependency graph |
| 15 | T-70251ccf-7d6a-4e89-b4d4-98cd15c9d528 | 99 | Convert task types through reviewed linked copies | None in task dependency graph |
| 16 | T-916deee7-8be7-44aa-9cd2-b9d86e90e56d | 99 | Evaluate saved lists with explicit relative-date predicates | None in task dependency graph |
| 17 | T-16e1b491-18c3-498e-82da-81a97ff1294a | 25 | Add project-filtered retained task history navigation | None in task dependency graph |
| 18 | T-d76d2a9a-1318-4e63-9bb4-73f620b108cc | 24 | Qualify ChatGPT, Codex, multi-user and mobile release gates | None in task dependency graph; selected competitor sessions, consenting accounts and human/device observations required |
| 19 | T-96866c8f-9554-55d8-a638-6d8b5afaf21c | 24 | Security Hardening, Revocation, Key Rotation, And Team Rollout | T-d76d2a9a-1318-4e63-9bb4-73f620b108cc |

The retained Pending is P-8e7323df-2f47-4e19-864d-821ed108e7f2 at priority106; fresh affected-client build/controller identity and sanitized iPhone evidence remain missing.

No task was reused or resolved without promotion. The five new tasks preserve P-66d0b256-307b-4a9d-babd-13d6e7edd9f6 provenance.

The saved Implement All checkpoint remains unchanged and excludes the five new tasks. An explicit controller-scope resolution is required before a new Work Through Next run can start. The existing hosted execution stop is not lifted by processing.
