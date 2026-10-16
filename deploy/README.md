# Deploy Habitica Companion to the existing website

## Selected target

The selected environment is the existing production website: **https://habitica-companion.pages.dev**. No separate dev website or backend environment is selected. Local source remains on `develop`; the deployed artifact targets the existing Pages production slot. A Git branch name and a hosting environment are separate concepts.

| Component | Name or address |
| --- | --- |
| Pages project / website | `habitica-companion` / `https://habitica-companion.pages.dev` |
| Authentication backend | `habitica-companion-auth` / `https://habitica-companion-auth.strexet.workers.dev` |
| Workspace backend | `habitica-companion-workspace` / `https://habitica-companion-workspace.strexet.workers.dev` |
| MCP endpoint | `https://habitica-companion-mcp.strexet.workers.dev/mcp` |
| OAuth storage | `habitica-companion-oauth` |
| Integration database | `habitica-companion-auth` |
| Workspace database | `habitica-companion-workspace` |
| Public operator input | `deploy/production.local.json` |
| Generated deployment bundle | `deploy/.production/` |

Cloudflare Pages serves the website. Workers run backend code. KV namespaces store key/value records; D1 databases store structured records. Separate backend services and databases protect different data boundaries within the same production environment. The existing browser-sync KV and party database remain retained. No new website or purchased domain is needed.

Wrangler is the project-installed Cloudflare deployment tool. Cloudflare login and account/subdomain lookup are complete. Resource preparation, database migrations, public configuration, builds and publication belong to the agent/operator; the owner does not need to repeat account setup or manually edit JSON.

## Current owner action: sign in and connect

The production website and all three backends are published. Protected uploads of `PROFILE_ID_KEY`, `CREDENTIAL_KEY_V1` and `CREDENTIAL_KEY_V2` are complete; the agent verified only the secret names. No further Terminal commands or key generation are required. Keep the saved keys in the password manager. Do not replace the profile key after real enrollment without a reviewed recovery procedure.

Open [Habitica Companion](https://habitica-companion.pages.dev) in a separate browser profile. Follow [Owner account and client connection after publication](#owner-account-and-client-connection-after-publication) below for the exact Habitica credential source, sign-in fields, separate consent controls and ChatGPT/Codex setup. Provide credentials only to the intended sign-in form. The initial pilot covers account/task reads and Companion-only task saves; leave background execution off and avoid existing gameplay buttons during the first check.

The recorded website source is `f94b768fc4e0431ce7459a3f3fc25f7c50751e2e` from local `develop`, published to the existing production slot. [Sanitized publication evidence](../skill-tree/history/validation/production-publication.json) records version identities, retained bindings and hosted checks. Real account enrollment, OAuth completion in ChatGPT and Codex desktop, plugin activation, multi-user isolation and device behavior still require observed results.

## Agent/operator preparation and publication

The ignored `deploy/production.local.json` contains public account/resource IDs, exact origins and the reviewed existing Pages selection. No credentials belong in the file. The generator remains `deploy/prepare-dev.mjs` for compatibility with the repository’s deployment tooling; explicit existing Pages selection produces production names, runtime metadata and `deploy/.production/` output. Dedicated-dev generation is an unselected alternative.

```sh
node deploy/prepare-dev.mjs deploy/production.local.json
```

An existing output directory is refused. Preserve prior bundles before regeneration. Existing Pages reuse requires exact reviewed project, origin, browser bindings, party database name, production branch and original deployment identity. Default isolation guards continue to reject accidental production reuse without that explicit selection.

The existing Pages project’s stable address maps to the `main` slot. Publication uses an artifact built from local `develop` and records the actual commit hash; no Git merge, branch switch or push is required. Existing automatic Git deployment remains enabled and can replace a manual upload later. [Pages branch controls](https://developers.cloudflare.com/pages/configuration/branch-build-controls/) describes the project setting.

Before publication, confirm the complete existing browser bindings are retained, the three secrets exist, Auth/Workspace migrations 0001–0007 are applied, published frontend endpoints match the table above, and all cloud game-effect flags remain false. The existing party database already has the required migrations. Existing browser scoring, equipment and spell controls can still change Habitica; only the cloud game-effect lanes are disabled.

The agent/operator publishes in dependency order:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler deploy --config deploy/.production/chatgpt-auth.json
./workers/chatgpt-auth/node_modules/.bin/wrangler deploy --config deploy/.production/companion-workspace.json
./workers/chatgpt-auth/node_modules/.bin/wrangler deploy --config deploy/.production/chatgpt-mcp.json
HABITICA_FRONTEND_SETTINGS_FILE="$PWD/deploy/.production/appsettings.json" ./build.sh
```

Inspect `output/wwwroot/appsettings.json` before website upload. The build merges complete public settings and restores checked-in source defaults after success/failure. Then upload to the exact selected website:

```sh
CLOUDFLARE_ACCOUNT_ID=8c962c94f28f5bdd298024365e0dea40 ./workers/chatgpt-auth/node_modules/.bin/wrangler --cwd deploy/.production/pages pages deploy "$PWD/output/wwwroot" --project-name habitica-companion --branch main --commit-hash "$(git rev-parse HEAD)"
```

Pages uses `CLOUDFLARE_ACCOUNT_ID` for account selection; Pages configuration does not support `account_id`. Worker configuration retains the account field. The staged directory supplies retained Pages bindings and Functions. No source upload or dashboard editor paste is needed. Record the deployment result and actual source commit. The pre-update deployment identity is `774c03ca-039f-439c-93b2-ac3d26db11d3`. Asset rollback cannot reverse database changes or confirmed Habitica effects.

## Hosting and privacy checks before enrollment

Verify the stable website, `/settings` refresh and `/privacy`. Check `/health` on each backend origin. MCP health must report Auth reachability; the current authorization label alone is not an end-to-end OAuth verdict. Opening `/mcp` as a normal webpage does not test the authenticated protocol.

Review deployed bindings, request/log redaction and provider retention before real credential enrollment. Invocation logging remains disabled; no body/header logging or conversation-text storage is selected. Auth maintenance removes bounded operational metadata under the app’s 30-day policy. Deployed settings confirm invocation logs and traces are off, with no Logpush destination or tail consumer. Application source contains no console logging. Workers Logs can retain errors/custom logs for three days on Free or seven days on Paid; D1 Time Travel retains backups for seven days on Free or thirty days on Paid. Account-plan lookup was denied by the available OAuth scope, so the exact tier remains unverified; the conservative disclosed upper bounds are seven and thirty days. Profile deletion is not a claim of immediate provider backup erasure. [Workers Logs](https://developers.cloudflare.com/workers/observability/logs/workers-logs/) and [D1 Time Travel](https://developers.cloudflare.com/d1/reference/time-travel/) describe provider behavior.

Keep `CRON_ENABLED`, projection, spells, authenticated party planning, executor and key-operation flags false. Cloudflare maintenance schedules are separate from Habitica’s irreversible Start New Day/Cron feature. No live Cron test is selected.

## Owner account and client connection after publication

1. Open `https://habitica-companion.pages.dev` in a separate browser profile for the first test.
2. In Habitica, open **Settings → API**, or [Habitica API settings](https://habitica.com/user/settings/api). Copy the intended account’s User ID and API Token.
3. Paste User ID into **Habitica User ID** and API Token into **Habitica API Token** on the Companion sign-in form. Leave `Remember credentials on this device` unchecked for the first test. Never paste either credential into client chat or Cloudflare public settings.
4. In Companion Settings, read the storage disclosure under **ChatGPT linking**, select the checkbox and click **Enable ChatGPT linking**. The current label serves ChatGPT and Codex.
5. Read the separate workspace disclosure, select the checkbox and click **Connect cloud workspace**. Leave background execution off and existing data-transfer controls alone during the first pilot.
6. Use **Check saved work status**. Preserve a failed response as a failure rather than assuming zero saved work. Keep the Settings tab open because management access is tab-local.

ChatGPT uses the current **Plugins → plus → Add custom MCP server** route, subject to account/workspace availability. Enter name `Habitica Companion`, OAuth authentication and server URL `https://habitica-companion-mcp.strexet.workers.dev/mcp`. No manual Habitica token/header or invented client secret belongs in the connection. [Official connection guidance](https://developers.openai.com/plugins/deploy/connect-chatgpt) defines the route.

Codex desktop uses **Settings → MCP servers → Add server**, name `habitica-companion`, transport **Streamable HTTP**, and the same `/mcp` URL. Save/restart and choose **Authenticate** when prompted. Local CLI setup is a supplementary alternative:

```sh
codex mcp add habitica-companion --url https://habitica-companion-mcp.strexet.workers.dev/mcp
codex mcp login habitica-companion --scopes profile:read,account:read,tasks:read
```

Each client needs a different fresh **Generate new code** result from Companion Settings. Paste the code only into **One-time Companion code** on the authorization page at `habitica-companion-auth.strexet.workers.dev`, click **Verify code**, inspect the displayed account/scopes and click **Allow selected access**. Codes are single-use and expire within ten minutes. [Official MCP configuration](https://learn.chatgpt.com/docs/extend/mcp?surface=app) documents desktop/CLI configuration and OAuth.

If an authorization page is expired or already used, close the old authorization tab and start **Authenticate** again in the client. Generate a fresh code in the existing Companion Settings tab, verify the code on the newly opened page, review the account/scopes and approve once. The enrolled profile and stored credentials remain available.

Direct MCP connectivity and packaged skill installation are separate checks. The staged package is `deploy/.production/plugin/`; use the target client’s supported private/local marketplace route. The [plugin client matrix](../plugin/README.md#client-qualification-matrix) owns the exact read, canonical task, receipt, reconnect, revocation and skill scenarios. Missing host installation features remain explicit unrun gates. No public plugin submission is selected.

Start with “Show my Habitica account status. Change nothing.” Then request a Companion-only test Todo with no Habitica projection, review the preview and explicitly approve the save. A lost response requires lookup of the original receipt, not a replacement command. A second consenting account and real iOS/Android checks follow the owner pilot; no owner can consent for another account.

## ChatGPT reconnect regression check

1. Open [ChatGPT Plugins](https://chatgpt.com/plugins), select **habitica-companion**, open the connection details and select **Refresh**. Metadata refresh is required after server authentication/tool metadata changes; [official refresh instructions](https://developers.openai.com/plugins/deploy/connect-chatgpt#refresh-metadata) describe the supported custom-MCP flow.
2. Complete **Reconnect** once if ChatGPT requests authorization. Generate a fresh linking code in [Companion Settings](https://habitica-companion.pages.dev/settings) and enter the code only on the Companion authorization page opened by ChatGPT. Review the account and requested scopes, then approve. Keep the integration profile and existing credentials.
3. Start a new ChatGPT conversation, type `@` and select **habitica-companion**. Send three separate messages: `Show my Habitica account status. Change nothing.`, `Show my Habitica tasks. Change nothing.`, then `Show my Habitica account status again. Change nothing.` Expected: successful reads without another reconnect dialog. A prompt for additional permissions on an operation outside the approved scopes requires separate consent.
4. After more than ten minutes, send the account-status request again. Expected: successful read through automatic token refresh. Run a read in Codex, then repeat a ChatGPT read to check client independence. Report only the failing request, elapsed time and error label; exclude tokens, linking codes and account data.

The owner reports initial authentication in both clients and two successful ChatGPT reads, followed by immediate repeated reconnection. Local continuity checks pass; the live regression remains open until the owner retest succeeds.

## Stop and recover

Stop new cloud access by disabling enrollment/OAuth and Workspace flags in the matching generated production configs and redeploying the affected Workers. Keep all game-effect/executor/Cron flags false. Per-account disconnect/deletion controls remain separate. Retain accepted histories and unknown-effect fences. Never delete established browser data or retry unknown gameplay effects to repair a connection. Restore only verified backups with compatible schemas.
