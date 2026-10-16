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

## Current owner action: replace the profile key

Only `PROFILE_ID_KEY` was added during setup. The value appeared in chat and must not be reused. The active secret binding has been removed from the setup-only Auth Worker; no real account enrollment occurred. The corrected Auth Worker is `habitica-companion-auth`. Fresh protected key entry is required before application publication.

Only commands inside `sh` blocks belong in Terminal. Account descriptions, version information and success messages belong in chat. A generated key belongs in a password-manager secret field and the waiting Wrangler secret prompt. Never paste a key at the ordinary shell prompt or into a conversation.

1. Open Terminal using Command+Space, type `Terminal`, and press Return.
2. Enter the project folder:

```sh
cd /Users/petr/Projects/habitica-tool
```

3. Generate one fresh random profile key directly onto the Mac clipboard:

```sh
node -e "process.stdout.write(require('node:crypto').randomBytes(32).toString('base64url'))" | pbcopy
```

The command prints nothing. The clipboard contains the new key.

4. Open a password manager, such as the existing password manager used for account passwords. Create or update an entry with title `Habitica Companion — PROFILE_ID_KEY`. Paste the clipboard value into the password/secret field and save. Replace the exposed value rather than preserving the exposed value as the active key. Copy the saved value again if the password manager clears the clipboard.
5. Run:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler secret put PROFILE_ID_KEY --config deploy/.production/chatgpt-auth.json
```

6. Wait for **Enter a secret value**. Paste the saved fresh value at that waiting prompt and press Return. Hidden input is expected. The reported target must be `habitica-companion-auth`.
7. Wait for the success message and reply in chat: `Fresh profile key added.` No value, screenshot of a value or copied credential-bearing command belongs in the reply.

No other command is needed for the current owner step. The two credential-encryption keys are separate later prompts. [Cloudflare secrets](https://developers.cloudflare.com/workers/configuration/secrets/) explains encrypted bindings and the fact that secret changes deploy Worker versions immediately.

## Remaining owner key prompts

After the fresh profile key succeeds, create a different random value for `CREDENTIAL_KEY_V1`, save the value as `Habitica Companion — CREDENTIAL_KEY_V1` in the password manager, and enter the value only when the following command asks:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler secret put CREDENTIAL_KEY_V1 --config deploy/.production/chatgpt-auth.json
```

Repeat with another different value for `CREDENTIAL_KEY_V2`, saved under the matching password-manager title:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler secret put CREDENTIAL_KEY_V2 --config deploy/.production/chatgpt-auth.json
```

Generate each value with the clipboard command in the preceding section. The three keys must differ. No key belongs in public JSON, Git, frontend settings or chat. Back up the password-manager entries securely. Profile-key stability matters after real accounts enroll; the current pre-enrollment replacement does not require profile migration.

The agent can verify secret names without reading values:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler secret list --config deploy/.production/chatgpt-auth.json
```

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

Pages uses `CLOUDFLARE_ACCOUNT_ID` for account selection; its configuration does not support `account_id`. Worker configuration retains the account field. The staged directory supplies retained Pages bindings and Functions. No source upload or dashboard editor paste is needed. Record the deployment result and actual source commit. The pre-update deployment identity is `774c03ca-039f-439c-93b2-ac3d26db11d3`. Asset rollback cannot reverse database changes or confirmed Habitica effects.

## Hosting and privacy checks before enrollment

Verify the stable website, `/settings` refresh and `/privacy`. Check `/health` on each backend origin. MCP health must report Auth reachability; the current authorization label alone is not an end-to-end OAuth verdict. Opening `/mcp` as a normal webpage does not test the authenticated protocol.

Review deployed bindings, request/log redaction and provider retention before real credential enrollment. Invocation logging remains disabled; no body/header logging or conversation-text storage is selected. Auth maintenance removes bounded operational metadata under the app’s 30-day policy. Provider logs/backups have separate plan-based retention, so profile deletion is not a claim of immediate provider backup erasure. [Workers Logs](https://developers.cloudflare.com/workers/observability/logs/workers-logs/) and [D1 Time Travel](https://developers.cloudflare.com/d1/reference/time-travel/) describe provider behavior.

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

Direct MCP connectivity and packaged skill installation are separate checks. The staged package is `deploy/.production/plugin/`; use the target client’s supported private/local marketplace route. The [plugin client matrix](../plugin/README.md#client-qualification-matrix) owns the exact read, canonical task, receipt, reconnect, revocation and skill scenarios. Missing host installation features remain explicit unrun gates. No public plugin submission is selected.

Start with “Show my Habitica account status. Change nothing.” Then request a Companion-only test Todo with no Habitica projection, review the preview and explicitly approve the save. A lost response requires lookup of the original receipt, not a replacement command. A second consenting account and real iOS/Android checks follow the owner pilot; no owner can consent for another account.

## Stop and recover

Stop new cloud access by disabling enrollment/OAuth and Workspace flags in the matching generated production configs and redeploying the affected Workers. Keep all game-effect/executor/Cron flags false. Per-account disconnect/deletion controls remain separate. Retain accepted histories and unknown-effect fences. Never delete established browser data or retry unknown gameplay effects to repair a connection. Restore only verified backups with compatible schemas.
