# Optional Companion Workspace Worker

The package owns canonical cloud task data and command history. The Auth Worker owns encrypted Habitica credentials and exposes a private identity service binding. All shipped access flags remain disabled; remote URLs and database IDs are placeholders.

Install the Auth, MCP and Workspace package dependencies before local qualification. Build Auth and MCP first, then build Workspace. Run the Auth tests and `npm --prefix workers/companion-workspace test`. The workspace tests reuse the pinned Auth Miniflare fixture with synthetic accounts, mocked Habitica validation and isolated local databases.

Public routes require an independently consented workspace session:

| Route | Behavior |
| --- | --- |
| `POST /connect` | Exchange an Auth management bearer plus `workspace-v1` consent for a tab-local workspace session. |
| `GET /workspace` | Read canonical state and current store version. |
| `POST /commands` | Accept a typed command atomically with revision checks and command-ID deduplication. |
| `GET /export` | Export the complete validated journal format without credentials. |
| `POST /disconnect` | Revoke workspace sessions and executor eligibility while retaining canonical history. |
| `DELETE /workspace` | Delete canonical data and journal after workspace-ID confirmation. |

Requests cannot select an account. The private Auth broker derives profile identity and verifies credential epochs. Workspace access epochs prevent stale requests from reaching a replacement workspace. The service currently accepts canonical commands without issuing Habitica mutations. Projection reconciliation and hosted execution are subsequent dependencies.

Canonical documents are limited to one MiB; accepted archives are limited to eight MiB. Atomic accounting rejects additional writes before retained history exceeds portable export capacity. The service never truncates accepted history. Larger workspaces require an explicit storage extension.

No command in local qualification deploys resources or applies remote migrations. Live enrollment, production activation and real Habitica effects require separate authorization scopes.
