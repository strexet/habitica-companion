# Prioritized task choices

Order: descending numeric priority; document order breaks ties. Ready defines researched scope, not authorization or completed dependencies.

| Row | ID | Priority | Title and intended result | State and unmet dependencies |
| --- | --- | --- | --- | --- |
| 1 | T-23342106-269e-4cd6-992e-4ff95be3e62d | 100 | Browse and manage standalone and project Notes folders | Ready; awaits row2, row5 |
| 2 | T-94b18897-2c2f-44ab-b89b-34e58b3903c5 | 100 | Expose reviewed Notes folder workflows through MCP | Ready; awaits row3 |
| 3 | T-7aa20175-78af-40c9-86a3-e1ea37109c34 | 100 | Persist Notes folders through the cloud owner API | Ready; awaits row4 |
| 4 | T-5111060c-76ae-4a0e-a48b-260ecbb8885f | 100 | Add versioned Notes folders and local persistence | Ready |
| 5 | T-a48aa46e-fd3c-43ff-b9b3-3becddb47599 | 100 | Expose project-linked Notes in Notes and project views | Ready |
| 6 | T-569ff3a0-8765-49b6-9b37-a62e091617ae | 99 | Deliver due task reminder events with durable original receipts | Ready; awaits row7 |
| 7 | T-280cb859-c0f7-46e5-97db-bbd41dc6a99a | 99 | Implement Auth-owned callback secrets and reviewed event subscriptions | Ready; awaits row8 |
| 8 | T-273899d8-6f17-43ee-b82f-41013e1021b9 | 99 | Qualify an explicit MCP2 migration while retaining legacy clients | Ready |
| 9 | T-3c84f362-0f7b-4f0c-9e23-75d9a1e0725f | 99 | Show coverage-aware Daily and Habit activity streaks | Ready |
| 10 | T-e77f6c7c-cf2f-487d-bb82-831e6f6167f9 | 99 | Edit supported linked Daily schedules with explicit reconciliation | Ready |
| 11 | T-70251ccf-7d6a-4e89-b4d4-98cd15c9d528 | 99 | Convert task types through reviewed linked copies | Ready |
| 12 | T-916deee7-8be7-44aa-9cd2-b9d86e90e56d | 99 | Evaluate saved lists with explicit relative-date predicates | Ready |
| 13 | T-16e1b491-18c3-498e-82da-81a97ff1294a | 25 | Add project-filtered retained task history navigation | Ready |
| 14 | T-d76d2a9a-1318-4e63-9bb4-73f620b108cc | 24 | Qualify ChatGPT, Codex, multi-user and mobile release gates | Blocked pending answers |
| 15 | T-96866c8f-9554-55d8-a638-6d8b5afaf21c | 24 | Security Hardening, Revocation, Key Rotation, And Team Rollout | Ready; awaits row14 |

Retained Pending P-8e7323df-2f47-4e19-864d-821ed108e7f2 at priority106 needs fresh loaded PWA identity and sanitized iPhone evidence; no duplicate implementation is planned. Release gate row14 requires separate actual-host, consenting-user and device qualification. Row15 waits for row14 despite Ready contract status.

Available choices: select an exact task, clarify retained incident/release evidence, request other planning work or stop. The existing owner-stopped checkpoint remains preserved; selecting new folder work requires an explicit controller scope handoff rather than resuming the old frozen snapshot.

Recommended next action: select Work Through Next for T-5111060c-76ae-4a0e-a48b-260ecbb8885f, the highest-ranked dependency-ready unit. Folder UI/MCP/cloud rows1-3 remain visible but await the model/storage contract. Await an actual owner selection before execution.
