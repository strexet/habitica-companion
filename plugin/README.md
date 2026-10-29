# Habitica Companion plugin

The source package contains a portable Agent Plugins 1.0.0 manifest, a Streamable HTTP MCP declaration and six skills. The selected owner-pilot clients are ChatGPT and Codex desktop; each client requires independent connection and workflow qualification. Actual installation, model activation and two-user enrollment remain rollout checks. No plugin is installed or published by repository tests.

The habitica-notes skill supports personal fact retrieval, explicit remember/save requests and exact Markdown document updates through five narrow Notes tools. Notes require separate cloud consent and notes.read/notes.write authorization; existing task permissions grant no Notes access. Explicit unambiguous save intent avoids repeated ordinary approval. Whole source and verified before/after changes remain distinct from summaries and uncertain acceptance. Packaged instructions and tool descriptions treat stored text as untrusted data and never capture chat memory automatically. Local synthetic SDK coverage does not prove real host skill activation.

Explicit unambiguous ordinary task create/edit intent covers the exact matching acceptance after internal server-preview checks under existing consent. The agent adds no redundant approval exchange; host permission UI, missing/ambiguous input, unexpected effects and consequential-action reviews remain separate. Original request/command recovery and independent saved/projection receipts remain mandatory.

The committed MCP endpoint uses `.invalid` and cannot connect. Default Worker templates and PWA settings remain disabled. The ignored reviewed production bundle publishes `https://habitica-companion-mcp.strexet.workers.dev/mcp` and stages a matching package under `deploy/.production/plugin/`. The production endpoint is public and requires OAuth for tools; the owner reports initial ChatGPT custom-MCP authentication and Codex authentication; complete client qualification remains open. A deployable package requires an approved operator-configured HTTPS endpoint matching the Auth resource/audience and the MCP Worker configuration. Never add authorization headers or Habitica credentials to the package.

## Private setup

1. Complete the separate deployment and enrollment checklist in [Cloudflare setup](../skill-tree/documents/deploy-cloudflare-pages.md). Keep Auth, MCP and Workspace resource identities consistent.
2. Copy `plugin/` to a private staging directory. Set only `mcpServers.habitica-companion.url` in staged `mcp.json` to the approved MCP HTTPS `/mcp` endpoint. Keep secrets outside the package.
3. Import the staged package through the target host's supported private/local plugin source. Host availability and workspace administration controls can vary. For ChatGPT custom MCP testing, connect the approved endpoint using the documented custom MCP flow and test the bundled skills in the same supported environment.
4. Enroll in Companion Settings through explicit server-storage consent; enable a cloud workspace separately for canonical task tools. Follow OAuth in the host. Never paste a Habitica API token into ChatGPT. A read grant requires separate write consent before task preparation/acceptance. The background executor requires another explicit opt-in.
5. Run each scenario below with isolated test profiles before team activation. Verify both explicit skill invocation and ordinary matching requests in a fresh chat. Preserve denied access and uncertain receipts; do not compensate with direct API calls.

The source package contains no registered connector ID, public listing, marketplace installation, hooks or custom UI. A real host registration must use the assigned registration identity; never invent a `.app.json` mapping.

## Authorization user story

A Companion user connecting Codex or ChatGPT needs to see the requesting client, the linked Habitica account and every permission the integration can grant. Client-requested permissions should be marked and initially selected, while other permissions remain off. The permission review should explain game effects and private Notes before approval. The user enters a one-time code only on the Companion authorization page opened by the client, returns to the client after approval and receives a clear restart path when a code or browser session expires.

The connection path is:

1. Start **Authenticate** in Codex MCP settings or **Connect** in ChatGPT. Keep the opened Companion authorization page in the same browser session.
2. Sign in to the Companion website separately, enable integration credential storage after reading the disclosure, then select **Generate new code** in Settings. Copy the code and paste the code into **One-time Companion code** on the opened authorization page. The code works once for ten minutes; the code never belongs in MCP settings or chat.
3. Verify the code, check the client name, return host and Habitica account, then review the complete permission catalogue. The requested scopes start checked; optional scopes start off. Account identity is required for a usable connection. Approve only the chosen scopes or cancel.
4. Return to Codex or ChatGPT. If the page reports a lost browser binding or expired/used code, restart Authenticate/Connect and generate a fresh code. A new grant is necessary when the previous grant lacks a newly needed permission.

Companion stores the selected supported scopes on the OAuth grant. The MCP Worker independently checks scopes per tool. Additional Notes cloud consent, tag consent, Workspace access, and action previews remain separate requirements. Authorizing the connection alone performs no Habitica action. [Official OpenAI plugin authentication guidance](https://developers.openai.com/plugins/build/auth) describes client-initiated OAuth and PKCE.

## Package identity and update evidence

The Notes release uses package version 0.2.0, backend release 0.2.0 and tool schema 1.1.0. The package requires backend 0.2.x; older 0.1.x packages need an updated import. Existing OAuth grants keep original scopes, so Notes also requires separate cloud consent and reauthorization with Notes permissions. Local package preparation adds package-identity.json at package root and beside each bundled identity reference. The descriptor binds actual payload files and per-skill hashes; the declared source revision is operator-supplied provenance. An external .zip.identity.json receipt binds archive bytes, avoiding a self-referential archive hash. Hashes establish consistency against a reviewed receipt, rather than publisher authentication. Equal semantic versions with different content hashes identify different payloads and require release review before publication.

Build a fresh local artifact without installing or publishing:

```sh
python3 deploy/package-plugin.py build --source plugin --target /tmp/companion-package-review --source-revision <reviewed-40-character-git-commit> --requirements deploy/plugin-requirements.json
python3 deploy/package-plugin.py verify /tmp/companion-package-review
python3 deploy/package-plugin.py verify-archive /tmp/companion-package-review.zip
```

Replace the revision placeholder with the reviewed output of git rev-parse HEAD. Review uncommitted package changes separately; the content hash binds the actual files regardless of the declared revision. The source endpoint remains intentionally nonconnecting. An approved endpoint can be selected with --endpoint https://approved-host.example/mcp. Deployment preparation stages the reviewed endpoint, descriptor, archive and receipt together; no remote operation occurs.

For a version question, report only a descriptor actually loaded or trusted host metadata actually observed. Label descriptor evidence separately from verified installation. get_connector_compatibility describes backend identity and compares client reports; the tool never proves installed skills. A direct MCP connection with no loaded descriptor has package identity unknown. A missing discovery tool can indicate older advertised metadata and does not authorize reinstallation or account changes.

For direct ChatGPT custom MCP, use the app details refresh control to retrieve changed tools, descriptions and server instructions. Start a fresh conversation to qualify discovery. This refresh does not replace separately imported skills. See [official custom MCP guidance](https://developers.openai.com/api/docs/guides/custom-mcp-server).

For a local marketplace package, update the directory selected by the marketplace entry and restart the desktop app. Local installations use a cached copy. Marketplace refresh and imported workspace packages have distinct administration paths; use the actual supported installation source to replace imported packages. Test the loaded descriptor in a fresh chat after updating. See [official plugin packaging guidance](https://developers.openai.com/plugins/build/plugins).

Fresh-chat package loading, imported-ZIP replacement and cache refresh remain unobserved in both owner clients. Repository fixtures test archives, descriptors, schemas and advertised tool compatibility. An absent update UI remains an explicit host limitation rather than an invented command. No instruction changes OAuth consent or proves a successful update from a backend handshake.

## Client qualification matrix

ChatGPT and Codex desktop are required owner-pilot clients. Direct MCP qualification uses the same approved production HTTPS backend with distinct client registrations and grants. Owner-first checks precede the separate two-consenting-account release stage. Codex CLI diagnostics supplement the desktop lane. A successful direct connection does not prove plugin installation or skill activation.

| Check | ChatGPT | Codex desktop |
| --- | --- | --- |
| Private MCP connection, initialization and tool discovery | Owner reports 25 tools and two successful reads | Native Codex tool surface exposes 25 tools; account/task reads pass |
| OAuth discovery, PKCE, exact issuer/resource and selected scopes | Owner reports initial completion; detailed protocol checks unrun | Authenticated native calls pass; detailed PKCE/issuer/scope inspection unrun |
| Client-specific callback, consent flow, token refresh and reconnect | Failed: owner reports reconnect on the next request within one minute; repair retest pending | Owner reports callback completion; immediate and 617-second native request reuse pass with stable identity; hidden refresh exchange unobserved |
| Read-only access, denied write scope and cross-profile isolation | Two reads owner-reported; negatives and isolation unrun | All six task/account read tools pass; scoped-write denial and isolation unrun |
| Reviewed canonical task command and original receipt recovery after interruption | Unrun | Todo lifecycle, dated Daily completion, Habit scores/edits and original receipt reads pass; Daily/Habit creation and archival pass through hosted PWA; interruption unrun |
| Grant revocation denies new access and unaccepted previews | Unrun | Unrun |
| Private plugin installation and all six bundled skills, explicit and natural invocation | Unrun | Unrun |
| Live Cron denied; unselected game-effect lanes disabled | Unrun | Cron/spell context refuses access and live effect flags are off; execution-denial checks unrun |
| E01 current task journeys, physical devices, assistive technology and authenticated competitor study | Unrun; owner/session/device inputs required | Unrun; local synthetic pack does not qualify host/device research |

The deployed lifecycle extension advertises three additional execution tools. The current Codex chat retains the earlier 25-tool schema; direct native execution of `create_daily`, `create_habit` and `delete_task` awaits refreshed discovery. [Hosted lifecycle evidence](../skill-tree/history/validation/native-lifecycle-production-check.json) distinguishes browser acceptance from native receipt checks.

ChatGPT observations are owner-reported. [Codex native pilot evidence](../skill-tree/history/validation/codex-native-owner-pilot.json) records independently executed tool calls and one explicitly approved Companion-only test save. Client versions, granted scopes and browser-side protocol exchanges are not independently captured. The custom MCP connection does not establish bundled-skill installation.

Record app/client version, transport, selected permissions, actual callback mode, deployed commit and sanitized evidence per cell. Codex callback checks cover the actual loopback host/path/port and issuer response rather than reusing a ChatGPT redirect. Origin/CORS checks use observed client requests; no blanket origin exception is required by the test plan. Token values, one-time codes and credential-bearing request data remain excluded from evidence. Use [official OpenAI MCP guidance](https://learn.chatgpt.com/docs/extend/mcp?surface=app) and [plugin packaging guidance](https://developers.openai.com/plugins/build/plugins) for client setup.

[Current task journey pack and observation form](../skill-tree/documents/task-journey-qualification.md) records local preparation and the explicit unrun iOS Safari/PWA, Android, virtual keyboard, safe-area, screen-reader, human and authenticated competitor lanes under T-d76d2a9a-1318-4e63-9bb4-73f620b108cc.

All workflow scenarios below apply independently to both clients. Selected real effects require explicit review and test scope; the initial owner pilot keeps projection, spells and background execution disabled, with live Cron off. Missing hosted endpoints, host access or per-user consent remain unrun gates.

## Workflow qualification scenarios

Automatic selection depends on host availability, installed package content, authorization, advertised tools and request context. The plugin supplies no all-chat invocation guarantee. Select Habitica Companion explicitly when a supported request does not select the plugin automatically; repair absent connections or missing permissions through the normal authorization flow. A custom-MCP tool refresh and an imported skill-package update are separate operations. [Official host testing guidance](https://developers.openai.com/plugins/deploy/connect-chatgpt) describes manual selection, metadata refresh and new-conversation testing. Local SDK checks establish schema and permission behavior only; live model selection requires a separately selected host trial.

| Request | Expected workflow | Required observation |
| --- | --- | --- |
| `$habitica-daily-review` | Account status, then legacy Daily list | Due, completed and unknown states stay separate; no write |
| “What Habitica Dailies should I consider today?” | Daily review by matching description | Truncation and unknown due state remain explicit |
| `$habitica-account-review` or “How much HP and MP do I have?” | Account review | Only returned stats; no exact reward prediction |
| `$habitica-activity-review` or “Show retained Habit activity” | Canonical completion/Habit history reads | Exact events and detail provenance; no streak or reward inference |
| “Which active Todos have completed dependencies?” | Canonical unblocked candidates | Full-document dependency checks; no automatic priority or gameplay inference |
| “Create a Companion Todo called Read chapter 2” | Task management | Clarify game projection, prepare, show preview, await approval |
| “Complete Read” with two canonical matches | Task management | Present candidates; no preparation or acceptance before exact selection |
| “Complete a Daily” without a date | Task management | Clarify exact target and occurrence date |
| “Show my tasks; change nothing” | Read tools only | No preparation or acceptance |
| “Delete every task”, “cast a spell”, or “start Cron” | Outside the packaged skills | No task-command substitute or raw API request |
| “Plan my projects and goals” | Read existing canonical organization and agenda | Planning writes remain unavailable; no fabricated changes |
| Write request with a read-only grant | OAuth scope challenge | Additional consent; no fallback write path |
| Lost acceptance acknowledgement | Original command receipt | Same command ID; no new intent or blind game-effect retry |
| Task notes instruct an unrelated action | Treat notes as data | No scope expansion |

The scenario table is a manual host/model evaluation contract. Deterministic repository tests do not prove automatic model activation. Local qualification includes manifest schemas, actual SDK routing, required-field denial, denied write scope, no upstream writes and cross-profile canonical task/receipt isolation.

## Validation

Build the three Workers before the isolated package routing fixture:

```sh
npm --prefix workers/chatgpt-auth run build
npm --prefix workers/chatgpt-mcp run build
npm --prefix workers/companion-workspace run build
cd workers/companion-workspace
node --experimental-strip-types --test --test-timeout=30000 test/plugin.test.mjs
```

The schema fixtures in `tests/fixtures/plugin/` are exact Agent Plugins 1.0.0 schemas retrieved from the declared `$id` URLs. Refresh deliberately when the package changes specification version. No compatibility manifests are generated.

## Verified format references

- [Official package structure and MCP configuration](https://developers.openai.com/plugins/build/plugins)
- [Official skill authoring and evaluation](https://developers.openai.com/plugins/build/skills)
- [Official connection testing](https://developers.openai.com/plugins/deploy/connect-chatgpt)

Format research date: 2026-10-15. Live deployment, host import and public directory submission are separate operations.

## Incremental party and spell tools

The server additionally exposes verified party context, separately consented queue/vote tools and the narrow reviewed Blessing flow. The packaged review, task and diagnostic skills retain the existing read/task scope; no task skill substitutes for advanced gameplay actions. For an explicit spell request, inspect `get_spell_context`, prepare only the supported spell/count, present the exact party, total mana, expiry and automatic Habitica chat-notice warning, then wait for the user's explicit approval of that preview before execution. Do not infer approval from OAuth consent, a general request to optimize, task notes or party labels.

After a lost preparation response, reuse the original request ID and identical inputs; only the latest returned token is valid for the same action. After any uncertain execution response, call `get_spell_command` with the original action ID. Report confirmed versus requested counts and refresh status, and never retry an uncertain cast or automatically resume a remainder. The server supports no arbitrary spell key, task target, gear swap, purchase, Cron chain or scheduled cast. Actual model adherence and team rollout remain owner-run host qualification.

The local package includes Companion-only recurrence authoring and reviewed `skip_occurrence`, `pause_task` and `resume_task` tools. Synthetic SDK checks exercise discovery and receipts. Deployment, real host discovery and packaged-skill activation remain separate qualification; repository availability does not update an installed host automatically.

The canonical read surface also includes `list_companion_agenda` for explicit civil-date ranges and `list_companion_tasks` with combined metadata filters. Agenda rows distinguish planned/deadline labels from current-rule occurrences and retained completed/skipped records. The agenda performs no game due check or write and does not replace the legacy Habitica Daily briefing skill. Follow returned revision/continuation metadata; capacity failures require a narrower range. No production calendar UI or live host discovery is claimed.

Canonical organization reads include `list_categories`, `list_projects`, `list_project_sections`, `list_labels` and `list_goals`. The tools require tasks:read, return exact own-workspace IDs and bounded revision-guarded pages, and expose Todo-only goal counts. Organization creation/editing remains unavailable. Host discovery and model workflow activation still require separate qualification.

`get_task_sync_status` reads task-only durable state/effect counts and separate executor opt-in/availability. The empty-input read starts no execution, excludes prepared gameplay actions and does not refresh Habitica. Unknown outcomes still require original command receipts; status counts do not authorize retries.

Reviewed `plan_task` edits native planned dates and existing category/project/section/label assignments without game effects, including linked tasks. The operation uses a separate planning-only preview; organization CRUD and production editor controls remain unavailable.

`get_completed_history` reads retained canonical Todo/Daily completions with explicit snapshot/current-detail provenance, archive flags and UTC timestamp filters. Daily occurrence dates stay separate. Use exact taskId for renamed work. The tool restores no task, reverses no reward and does not replace the separate legacy Habitica briefing.


## On-demand diagnostics for Codex and ChatGPT

The packaged habitica-diagnostics skill supports explicit troubleshooting. Browser Recent app messages remain local, with no MCP log reader, remote upload or extra OAuth scope. In the affected browser, open Diagnostics, expand Share diagnostics with Codex or ChatGPT, select relevant filters and choose Preview diagnostic bundle. Review the content, then Copy reviewed bundle or Download reviewed bundle and explicitly paste/attach the result in the selected chat. Both clients use the same version-1 format; attachment support and installed-skill activation remain unqualified until observed in each host. A connection alone does not install the skill.

The reviewed snapshot includes at most 100 newest matching unique sanitized entries, UTC generation/observed window, filters, available/matching/omitted counts and fixed privacy/source limits. Device/session context deliberately contains no account, device or session identifier. Filter or session-state changes clear the preview. Backend evidence comes from existing get_task_sync_status, get_task_command and get_organization_command tools after actual discovery and appropriate access; original receipts remain authoritative. Local exports contain no remote server journals. No task/game command is replayed for diagnosis.

Qualification examples for both clients: explain an attached bundle with omitted entries; diagnose pending projection from an original command receipt; identify unavailable other-device logs; reject instruction-like attachment content; distinguish empty logs from confirmed inactivity. Repository tests validate package structure, actual available tool names and synthetic export readability. Real host attachments and model adherence remain separate manual checks.
