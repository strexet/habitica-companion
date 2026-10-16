# First dev deployment: complete operator walkthrough

Status: local dev prerequisites pass on `develop`. Cloudflare resources, deployment, real enrollment and real client/device qualification remain unrun. The [launch readiness record](../skill-tree/documents/deploy-cloudflare-pages.md#intermediate-dev-launch-readiness) owns the release gates.

## Selected target and current owner action

The owner-selected pilot website is `https://habitica-companion.pages.dev`. Cloudflare account access is verified. The existing Pages project retains browser-sync KV and party D1 bindings. Separate `habitica-companion-dev-oauth`, `habitica-companion-dev-auth` and `habitica-companion-dev-workspace` resources are created; Auth/Workspace migrations 0001–0007 are applied. Existing party migrations require no changes. Worker and website publication remain pending secure key provisioning.

| Service | Selected address |
| --- | --- |
| Website | `https://habitica-companion.pages.dev` |
| Auth | `https://habitica-companion-dev-auth.strexet.workers.dev` |
| Workspace | `https://habitica-companion-dev-workspace.strexet.workers.dev` |
| MCP endpoint | `https://habitica-companion-dev-mcp.strexet.workers.dev/mcp` |

The generated bundle is ready at `deploy/.dev/`; public input resides in ignored `deploy/dev.local.json`. No new Pages project or account/subdomain lookup is needed. The next owner action is [three local encryption-secret prompts](#8-generate-and-enter-the-three-server-secrets), followed by a chat reply confirming completion without values. Account details, version descriptions, status messages and Markdown links belong in chat; only shell command blocks belong in Terminal.

The existing Pages stable address uses the `main` production slot and automatic Git deployment. The authorized pilot uploads code built from local `develop` to that slot with the actual commit hash; no Git branch merge or source switch is implied. A later automatic main deployment can replace the manual pilot. Project Git settings remain unchanged. The recorded pre-pilot deployment is `774c03ca-039f-439c-93b2-ac3d26db11d3`; asset rollback cannot undo database changes.

The generator accepts an optional `existingPages` selection containing exact project, origin, retained KV/party IDs, party database name, production branch and previous deployment ID. The selection is an operator record, not automatic proof of remote ownership. Authenticated inventory must establish the match before reuse. Default dedicated-dev generation retains the suffix/isolation restrictions. New backend names retain the `-dev` prefix and integration resources remain distinct.

For the selected existing project, the eventual Pages upload uses the prepared bundle and observed slot:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler --cwd deploy/.dev/pages pages deploy "$PWD/output/wwwroot" --project-name habitica-companion --branch main --commit-hash "$(git rev-parse HEAD)"
```

Publication remains an agent/operator step after the key prompts; the owner does not need to execute the upload command. The walkthrough below also documents the alternate new dedicated Pages project route; skip the alternate project/storage creation steps for the selected existing target.

## What gets created

Cloudflare hosts the app and backend. The repository is the folder containing the source and deployment scripts on the Mac: `/Users/petr/Projects/habitica-tool`. A terminal command runs locally until a Wrangler command explicitly creates or publishes a Cloudflare resource. Wrangler is Cloudflare’s command-line tool, already pinned in the repository.

| Component | Plain-language purpose | Example dev name |
| --- | --- | --- |
| Pages project | Serves the website opened in a browser, including existing sync Functions | `habitica-companion-dev` |
| Auth Worker | Checks consent, stores encrypted Habitica credentials and issues limited client access | `habitica-companion-dev-auth` |
| Workspace Worker | Stores Companion tasks, notes and command history | `habitica-companion-dev-workspace` |
| MCP Worker | Provides the tools called by ChatGPT and Codex | `habitica-companion-dev-mcp` |
| Sync KV namespace | Stores encrypted browser-sync data | `habitica-companion-dev-sync` |
| OAuth KV namespace | Stores integration authorization data | `habitica-companion-dev-oauth` |
| Party D1 database | Stores existing browser party-sync records | `habitica-companion-dev-party` |
| Auth D1 database | Stores integration profiles, encrypted credentials and operational records | `habitica-companion-dev-auth` |
| Workspace D1 database | Stores canonical tasks and durable command history | `habitica-companion-dev-workspace` |

KV means named key/value storage; a namespace is one isolated storage container. D1 is Cloudflare’s SQL database. A binding connects a Worker or Pages Function to one of the storage resources. The generator writes the bindings; no manual code editing is needed.

An origin is a website’s `https://hostname`, without a trailing slash or path. The MCP endpoint adds `/mcp` to the MCP origin. The Auth origin is the issuer: the service responsible for sign-in and permissions.

The walkthrough uses Cloudflare-provided `pages.dev` and `workers.dev` addresses. No purchased domain, DNS changes, rented server, OpenAI API key or Global Cloudflare API Key is required for the route. Cloudflare documents the supplied Worker addresses in [workers.dev setup](https://developers.cloudflare.com/workers/configuration/routing/workers-dev/).

The first pilot enables enrollment, OAuth and Companion workspace storage. Projection to Habitica, authenticated party planning, spells, background execution, key operations and live Cron stay disabled. Existing browser gameplay controls retain real Habitica effects; initial tests use reads and Companion-only task saves.

## Choose the amount of manual work

Recommended handoff: complete sections 1–3, then send the non-secret handoff in section 4. An agent can perform resource creation, configuration, builds and the explicitly requested dev deployment after access is available. Owner participation is still needed for secret entry and per-account consent. Sections 5–14 give the complete manual route as well.

A second tester and physical mobile devices are needed for broader release qualification. The first owner pilot can start before the second-user/device stage.

## 1. Open the correct folder in Terminal

On the Mac, press Command+Space, type `Terminal`, and press Return. Paste each command block into Terminal, then press Return. Copy only the command text, without the surrounding Markdown fences. Run blocks in order and wait for the prompt to return. Stop at an error instead of proceeding to publication.

```sh
cd /Users/petr/Projects/habitica-tool
pwd
git branch --show-current
git status --short
```

Expected: the path ends in `habitica-tool`, the branch is `develop`, and status has no unexpected changes. The checkout already exists; no clone, GitHub connection, branch creation or push is needed. Do not reset or delete unexpected edits.

Check the local tools:

```sh
node --version
npm --version
./workers/chatgpt-auth/node_modules/.bin/wrangler --version
```

The Worker packages require Node 22.x. The prepared Mac has Node 22.12.0. If Node is missing, install a Node 22.x macOS distribution from the [official Node downloads](https://nodejs.org/en/download), reopen Terminal and repeat the folder command. Avoid upgrading the repository’s packages. If only the local Wrangler path is missing, restore the locked dependencies:

```sh
npm --prefix workers/chatgpt-auth ci
npm --prefix workers/chatgpt-mcp ci
npm --prefix workers/companion-workspace ci
```

`npm ci` installs the package versions recorded in the repository. The later website build installs the pinned .NET SDK and frontend dependencies automatically. No separate .NET installation or global Wrangler installation is required for that build path.

## 2. Create or select the Cloudflare account

1. Open [Cloudflare Dashboard](https://dash.cloudflare.com/) in a browser. Sign in, or choose Sign up and verify the email address.
2. Select the account intended for dev hosting. A Cloudflare account owns the resources; an email login can have access to several accounts. Use an account with permission to create Workers, Pages, KV, D1 and Worker secrets.
3. If onboarding asks for a domain, navigate to **Workers & Pages** using the sidebar or dashboard search. A custom domain is unnecessary for the walkthrough.
4. Review the selected account’s Workers plan, usage and billing before creating resources. Do not purchase a plan merely to follow an example. Actual plan limits and usage must be checked in the account; the local tests do not establish free-tier capacity.
5. Copy the **Account ID**. One documented route is dashboard search, Command+K, then `Copy account ID`. Another is **Workers & Pages → Account Details → Account ID**. The value is 32 hexadecimal characters, for example `0123456789abcdef0123456789abcdef`.

Paste the real Account ID into a private setup note for later use in `accountId`. An Account ID identifies an account and grants no access. A Zone ID identifies a domain and is not needed. [Official Account ID instructions](https://developers.cloudflare.com/fundamentals/account/find-account-and-zone-ids/) describe both dashboard routes.

In **Workers & Pages**, find `Your subdomain` and the adjacent setup/change control. Select or retain an available account subdomain. For example, a chosen subdomain `petr-habitica-dev` produces `petr-habitica-dev.workers.dev`. Record the actual value. Changing an existing account subdomain affects that account’s Worker addresses; retain an existing value when other Workers use the account subdomain. [Official workers.dev instructions](https://developers.cloudflare.com/workers/configuration/routing/workers-dev/) show the control.

Use the Pages project name `habitica-companion-dev` if available. A different name must end in `-dev`, and every derived name below must change consistently. Example addresses with the example subdomain are:

| Service | Address | Destination |
| --- | --- | --- |
| Website | `https://habitica-companion-dev.pages.dev` | `pwaOrigin` |
| Auth | `https://habitica-companion-dev-auth.petr-habitica-dev.workers.dev` | `authOrigin` |
| MCP origin | `https://habitica-companion-dev-mcp.petr-habitica-dev.workers.dev` | `mcpOrigin` |
| Workspace | `https://habitica-companion-dev-workspace.petr-habitica-dev.workers.dev` | `workspaceOrigin` |
| MCP endpoint | `https://habitica-companion-dev-mcp.petr-habitica-dev.workers.dev/mcp` | ChatGPT/Codex server URL, not `mcpOrigin` |

The example subdomain is illustrative, not an allocated address. Substitute the actual account subdomain. Addresses become available after deployment; an error before deployment is expected. Keep the final addresses stable after enrollment because OAuth uses exact service identities.

## 3. Authorize the local deployment tool

In Terminal, still inside the repository:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler login
```

A browser opens a Cloudflare authorization page. Sign into the selected account and approve Wrangler access. Return to Terminal and wait for the success message. Approval authorizes the local tool; no Cloudflare token needs to be copied into the chat or any repository file.

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler whoami
```

Check the selected account name and ID in the result. Keep authentication details private. For resource commands before generated configs exist, select the account explicitly in the Terminal session:

```sh
export CLOUDFLARE_ACCOUNT_ID="PASTE_REAL_ACCOUNT_ID_HERE"
```

Replace the quoted placeholder with the Account ID from section 2. The environment variable applies to the current Terminal session; set the variable again after opening a new Terminal. Generated configurations also contain `account_id`, so the later publish commands target the reviewed account.

## 4. Recommended handoff point

After sections 1–3, send the following non-secret information in the chat, filling the placeholders:

```text
Cloudflare login completed on the Mac.
Account ID: <32-character account ID>
workers.dev account subdomain: <actual subdomain>.workers.dev
Dev Pages project name: habitica-companion-dev
Use the Cloudflare-provided addresses derived from those names.
ChatGPT custom-plugin access: available / unavailable / not checked
Codex desktop version: <version from the app About screen>
Please create isolated dev resources and deploy the prepared develop code.
I will enter encryption secrets locally when prompted and perform account consent.
```

The handoff supplies the missing account/address choices and explicit dev publication scope. Resource IDs do not need to exist before the handoff. An agent can obtain the IDs during creation. No Habitica token, Cloudflare token, encryption secret or linking code belongs in the handoff.

The remainder documents the full manual route. Resource creation changes the Cloudflare account; secret commands also create or update Worker versions. Guide preparation executes no remote command below.

## 5. Create the dedicated dev resources

Run the following commands one at a time. Keep the `-dev` names. If an existing resource has the same name, verify ownership and emptiness before reuse; never select a production resource to resolve a naming conflict.

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler pages project create habitica-companion-dev --production-branch develop
```

The command creates the website container, without uploading the app. `production-branch develop` means the main address of the dedicated dev project follows `develop`; the setting does not deploy to the existing production application. Check the project in **Workers & Pages → habitica-companion-dev** and record the assigned `pages.dev` domain. If Cloudflare allocates a different domain because the requested name is unavailable, stop and align the project/name configuration before proceeding. The current generator requires the exact project-name.pages.dev form.

Direct Upload builds the app on the Mac and uploads the resulting files explicitly. Do not connect GitHub or drag source files into the dashboard. Cloudflare notes that Direct Upload projects cannot later switch to Git integration in place; automatic Git builds would use another project. [Direct Upload documentation](https://developers.cloudflare.com/pages/get-started/direct-upload/) and [Pages commands](https://developers.cloudflare.com/workers/wrangler/commands/pages/) describe the route.

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler kv namespace create habitica-companion-dev-sync
./workers/chatgpt-auth/node_modules/.bin/wrangler kv namespace create habitica-companion-dev-oauth
```

Each result contains a namespace `id`: a 32-character hexadecimal string. Copy only the ID, not the surrounding binding block. Record the sync result as `pagesKvId`; record the OAuth result as `authKvId`. If Wrangler offers to modify a source config automatically, decline; the generator will create the isolated config. IDs are also available under **Storage & Databases → Workers KV**, selecting the named namespace. [KV setup](https://developers.cloudflare.com/kv/get-started/) documents namespace creation and binding.

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler d1 create habitica-companion-dev-party
./workers/chatgpt-auth/node_modules/.bin/wrangler d1 create habitica-companion-dev-auth
./workers/chatgpt-auth/node_modules/.bin/wrangler d1 create habitica-companion-dev-workspace
```

Each result contains `database_id`, a UUID with hyphens, for example `11111111-2222-4333-8444-555555555555`. Copy the party result into `partyDatabaseId`, Auth into `authDatabaseId`, and Workspace into `workspaceDatabaseId`. Decline automatic source-config edits. The databases also appear under **Storage & Databases → D1 SQL Database**. [D1 setup](https://developers.cloudflare.com/d1/get-started/) describes the generated identity.

Recover missing output without creating duplicate resources:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler kv namespace list
./workers/chatgpt-auth/node_modules/.bin/wrangler d1 list
```

The three Workers are published from repository code later; no dashboard Hello World Worker creation is needed. The first Auth secret command can create the Auth temporary placeholder automatically.

## 6. Paste the public values into one local JSON file

Create the operator file once:

```sh
cp -n deploy/dev.example.json deploy/dev.local.json
nano deploy/dev.local.json
```

`cp -n` preserves an existing operator file. `nano` is a text editor inside Terminal. Replace values inside the quotes. Save with Control+O, press Return to confirm the filename, then exit with Control+X. Keep straight double quotes, commas between properties and no comma after the final property. Save the JSON file rather than executing the file contents in Terminal.

| Field in `deploy/dev.local.json` | Exact source |
| --- | --- |
| `accountId` | Cloudflare Account ID from section 2; not Zone ID |
| `pagesProject` | Exact dedicated Pages project name from section 5 |
| `pwaOrigin` | Assigned stable Pages domain, prefixed with `https://` |
| `authOrigin` | `<pagesProject>-auth.<account-subdomain>.workers.dev`, prefixed with `https://` |
| `mcpOrigin` | `<pagesProject>-mcp.<account-subdomain>.workers.dev`, prefixed with `https://`; omit `/mcp` here |
| `workspaceOrigin` | `<pagesProject>-workspace.<account-subdomain>.workers.dev`, prefixed with `https://` |
| `pagesKvId` | `id` from creating the sync KV namespace |
| `authKvId` | `id` from creating the OAuth KV namespace |
| `partyDatabaseId` | `database_id` from creating the party D1 database |
| `authDatabaseId` | `database_id` from creating the Auth D1 database |
| `workspaceDatabaseId` | `database_id` from creating the Workspace D1 database |
| `creatorHeader` | Existing project-owned public app identity; retrieve with the command below |

```sh
node -p "require('./src/Habitica.WebApp/wwwroot/appsettings.json').Habitica.XClientHeader"
```

Copy the output into `creatorHeader`. The output is the app’s public Habitica `x-client` identifier, not a signed-in user’s API token. Retain the committed project-owned identity; no new registration is required for the walkthrough.

Illustrative complete file, with fake IDs and an example subdomain; replace the account/resource IDs and subdomain with actual results:

```json
{
  "accountId": "0123456789abcdef0123456789abcdef",
  "pagesProject": "habitica-companion-dev",
  "pwaOrigin": "https://habitica-companion-dev.pages.dev",
  "authOrigin": "https://habitica-companion-dev-auth.petr-habitica-dev.workers.dev",
  "mcpOrigin": "https://habitica-companion-dev-mcp.petr-habitica-dev.workers.dev",
  "workspaceOrigin": "https://habitica-companion-dev-workspace.petr-habitica-dev.workers.dev",
  "pagesKvId": "11111111111111111111111111111111",
  "partyDatabaseId": "11111111-2222-4333-8444-555555555555",
  "authKvId": "22222222222222222222222222222222",
  "authDatabaseId": "22222222-3333-4444-8555-666666666666",
  "workspaceDatabaseId": "33333333-4444-4555-8666-777777777777",
  "creatorHeader": "COPY_EXISTING_XCLIENTHEADER_OUTPUT_HERE"
}
```

Every field is public configuration. No credentials belong in the file. Git ignores `deploy/dev.local.json` and generated `deploy/.dev/`; neither needs to be committed or pushed.

## 7. Generate the deployment files

```sh
node deploy/prepare-dev.mjs deploy/dev.local.json
```

Expected: `Dev configuration prepared... No resources were changed.` The generator validates format and separation from committed production IDs; actual account ownership still requires the preceding checks.

| Generated path | Purpose; manual changes normally unnecessary |
| --- | --- |
| `deploy/.dev/deployment-plan.json` | Review names, origins, storage IDs, required secrets and disabled game-effect flags |
| `deploy/.dev/chatgpt-auth.json` | Auth Worker code path, account, database/KV and public settings |
| `deploy/.dev/companion-workspace.json` | Workspace Worker database and private service connections |
| `deploy/.dev/chatgpt-mcp.json` | MCP Worker connections to Auth/Workspace |
| `deploy/.dev/pages/wrangler.json` | Website’s isolated dev sync bindings |
| `deploy/.dev/pages/functions/` | Staged existing Pages backend functions |
| `deploy/.dev/appsettings.json` | Public frontend endpoints and enabled integration controls |
| `deploy/.dev/plugin/` | Staged plugin with the dev MCP endpoint |

Open the plan with `nano deploy/.dev/deployment-plan.json` and verify the account-derived addresses and resource identities. Confirm `cloudGameEffectsEnabled` and `liveCronEnabled` are both `false`. Do not change source `wrangler.toml`, source Worker configs or source `appsettings.json` to deploy dev.

If the generator reports an existing output directory, have the agent preserve the earlier bundle outside the target directory and regenerate from corrected public input. Do not delete output containing needed deployment records. All subsequent commands in the walkthrough require the final current bundle to reside at `deploy/.dev`; an alternate output path requires adjusting every following path.

## 8. Generate and enter the three server secrets

Create three new random encryption keys for the dev environment. The keys serve credential encryption/profile derivation rather than Cloudflare login, Habitica sign-in or OpenAI access. Store three different values in a password manager with the exact name/value association:

| Secret name | Purpose | Paste destination |
| --- | --- | --- |
| `PROFILE_ID_KEY` | Stable private derivation of integration profile identities; retain unchanged | First protected Wrangler prompt below |
| `CREDENTIAL_KEY_V1` | Current credential encryption key | Second protected prompt |
| `CREDENTIAL_KEY_V2` | Separate credential encryption key required by the configuration | Third protected prompt |

On macOS, the following command creates one 32-byte random base64url value directly on the clipboard without printing the value in Terminal:

```sh
node -e "process.stdout.write(require('node:crypto').randomBytes(32).toString('base64url'))" | pbcopy
```

Immediately paste the clipboard into a new password-manager item named `Habitica Companion DEV — PROFILE_ID_KEY`. Save the password-manager item. Then run:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler secret put PROFILE_ID_KEY --config deploy/.dev/chatgpt-auth.json
```

At the `Enter a secret value` prompt, paste the saved value and press Return. Hidden input is expected. On first setup, Wrangler may ask to create `habitica-companion-dev-auth`; answer yes only when the displayed dev name matches. The pinned Wrangler creates a placeholder Worker for the case. The actual code is published in section 10.

Repeat the random-generation command for a new value, save a password-manager item named `Habitica Companion DEV — CREDENTIAL_KEY_V1`, then paste the saved value into:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler secret put CREDENTIAL_KEY_V1 --config deploy/.dev/chatgpt-auth.json
```

Generate a third value, save `Habitica Companion DEV — CREDENTIAL_KEY_V2`, then paste the saved value into:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler secret put CREDENTIAL_KEY_V2 --config deploy/.dev/chatgpt-auth.json
```

Check names without printing values:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler secret list --config deploy/.dev/chatgpt-auth.json
```

Expected: all three names. Clear the clipboard by copying ordinary non-secret text. Keep secret values outside chat, source files, JSON public configuration and screenshots. Secret changes can deploy Worker versions immediately, as described in [Cloudflare secrets](https://developers.cloudflare.com/workers/configuration/secrets/); secret provisioning changes the remote account.

## 9. Prepare empty database tables

A migration is a checked-in instruction that creates or changes database tables. Apply the existing migrations to the three new dev databases; no SQL authoring is needed:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler d1 migrations apply HABITICA_INTEGRATION_DB --remote --config deploy/.dev/chatgpt-auth.json
./workers/chatgpt-auth/node_modules/.bin/wrangler d1 migrations apply WORKSPACE_DB --remote --config deploy/.dev/companion-workspace.json
./workers/chatgpt-auth/node_modules/.bin/wrangler d1 migrations apply HABITICA_PARTY_DB --remote --config deploy/.dev/pages/wrangler.json
```

Review each confirmation target before accepting. The binding names such as `WORKSPACE_DB` are intentional: generated configs resolve the names to actual dev database IDs. `--remote` means Cloudflare storage rather than a local simulation. Expected migrations: Auth 0001–0007, Workspace 0001–0007 and party 0001–0008. Stop on failure. Never substitute the root production config or a production database ID.

## 10. Publish backend code, build the website and upload the app

Run from `/Users/petr/Projects/habitica-tool`, in the following order:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler deploy --config deploy/.dev/chatgpt-auth.json
./workers/chatgpt-auth/node_modules/.bin/wrangler deploy --config deploy/.dev/companion-workspace.json
./workers/chatgpt-auth/node_modules/.bin/wrangler deploy --config deploy/.dev/chatgpt-mcp.json
```

Auth goes first because Workspace depends on Auth private services. MCP goes last because MCP depends on both services. Compare every reported Worker name and stable URL with section 6. The generator supplies all bindings and public runtime settings. No source code needs to be pasted into a dashboard editor.

```sh
HABITICA_FRONTEND_SETTINGS_FILE="$PWD/deploy/.dev/appsettings.json" ./build.sh
```

The build command downloads the pinned .NET SDK, restores dependencies and builds website files under `output/wwwroot`. The build receives generated dev settings and restores the checked-in defaults afterward. Wait for successful publication, then inspect:

```sh
nano output/wwwroot/appsettings.json
```

Verify the Auth URL and Workspace URL point to the selected dev services, linking is enabled, and authenticated party planning is false. No secret or API token should appear. Exit the editor, then upload:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler --cwd deploy/.dev/pages pages deploy "$PWD/output/wwwroot" --project-name habitica-companion-dev --branch develop
```

The staged directory supplies the isolated bindings and Functions. Do not upload the source repository, use the root production configuration, or replace the step with dashboard drag-and-drop. No Git push is required. Record the commit with `git rev-parse --short HEAD`, the deployment URL and the successful migration results.

## 11. Check hosting and retention before entering Habitica credentials

Open the stable website address, then `/settings` and `/privacy`. Refresh `/settings` to check routing. Cloud controls should be available after sign-in rather than displaying source-default unavailable messages.

Open the following URLs in a browser, substituting actual hosts:

```text
https://habitica-companion-dev-auth.<account-subdomain>.workers.dev/health
https://habitica-companion-dev-workspace.<account-subdomain>.workers.dev/health
https://habitica-companion-dev-mcp.<account-subdomain>.workers.dev/health
```

Expected: HTTP success with small JSON responses identifying the services; MCP reports Auth reachability. The current MCP health response’s authorization label is not an OAuth end-to-end verdict. Opening `/mcp` as an ordinary browser page is not a tool test: MCP uses authenticated protocol requests rather than an HTML homepage.

In Cloudflare, open **Workers & Pages → each dev Worker → Settings** and inspect deployed bindings/variables. Keep game-effect flags false. Check the Auth maintenance trigger `17 * * * *` and Workspace trigger `* * * * *`; Cloudflare’s scheduled trigger is distinct from the disabled Habitica Start New Day/Cron capability. The Auth maintenance job cleans operational metadata; the Workspace executor remains disabled.

Before enrollment, verify the following provider settings with the operator:

- **Worker logs:** review the Worker’s Observability/Logs area and deployed `observability.logs.invocation_logs: false`. Do not add body/header/credential logging or export logs to another service. The configured app policy does not store conversation text. Inspect failures using redacted information only.
- **D1 backups:** open **Storage & Databases → D1 SQL Database → each dev database → Time Travel** and record the available recovery window. Provider backup retention is separate from deleting an active integration profile.
- **Retention:** app operational metadata cleanup uses the documented 30-day limit. Cloudflare logs and backups have separate plan-based retention; do not claim that every provider copy is deleted immediately or retained for exactly thirty days.

Cloudflare currently documents Workers Logs retention of 3 days on Free and 7 days on Paid, with a scheduled pricing change; check the account’s actual offering at setup. D1 Time Travel documents 7 days on Free and 30 days on Paid. [Workers Logs](https://developers.cloudflare.com/workers/observability/logs/workers-logs/) and [D1 Time Travel](https://developers.cloudflare.com/d1/reference/time-travel/) describe the independent policies. No purchase or retention expansion is necessary merely to match an example number.

## 12. Connect the owner’s Habitica account to the dev app

1. Open the stable dev website in a separate browser profile, preserving ordinary app data.
2. In Habitica, open **Settings → API**, or [Habitica API settings](https://habitica.com/user/settings/api). Copy **User ID** and **API Token** from the intended test account.
3. In the dev website’s **Sign in with Habitica** form, paste User ID into **Habitica User ID** and API Token into **Habitica API Token**. Leave `Remember credentials on this device` unchecked for a tab-only pilot. Submit the sign-in form. Habitica sign-in values belong outside Cloudflare configuration and ChatGPT/Codex messages.
4. Open `/settings`. Under **ChatGPT linking**, read the storage disclosure, select the consent checkbox and click **Enable ChatGPT linking**. The label serves both ChatGPT and Codex. The linking opt-in sends credentials to the dev integration server for encrypted storage.
5. Under **Companion workspace / Cloud tasks and workspace backups**, read the separate readable-data disclosure, select the consent checkbox and click **Connect cloud workspace**. Cloud workspace access is required for canonical task tools. Existing browser task adoption remains separate; leave transfer/import controls alone for the first pilot.
6. Leave background execution off. Use **Check saved work status** to confirm that the saved-work status request succeeds. A new empty workspace should have zero counts; a failure must not be treated as zero work.
7. Keep the Settings tab open. Management access is tab-local; closing/signing out forgets local management credentials. Use revalidation/renewal controls rather than assuming a fresh tab retained authority.

## 13. Connect ChatGPT and Codex independently

### ChatGPT custom MCP connection

The current official connection route is **ChatGPT Plugins → plus → Add custom MCP server**, subject to account/workspace policy. Enter:

| Field | Value |
| --- | --- |
| Name | `Habitica Companion DEV` |
| Description | `Dev-only Habitica reads and reviewed Companion task saves` |
| Connection | Public HTTPS endpoint |
| Server URL | Actual `mcpOrigin` followed by `/mcp` |
| Authentication | OAuth, using server discovery |

Review and create the connection as a plugin. No manually invented OAuth client ID/secret, Habitica token or Cloudflare token is needed. In the browser OAuth page opened by ChatGPT, verify the address belongs to the chosen dev Auth origin. Generate a fresh code using **Generate new code** in dev Settings, paste the code into **One-time Companion code** on the authorization page and click **Verify code**. Check the displayed account and selected permissions, then click **Allow selected access**. Codes expire within ten minutes and are single-use.

Start a fresh ChatGPT conversation, type `@`, select the dev plugin and ask: “Show my Habitica account status. Change nothing.” Verify the tool result belongs to the intended account. Missing custom-MCP controls are an account/workspace availability issue to record, not a reason to invent another menu or publish publicly. [Official custom-server connection steps](https://developers.openai.com/plugins/deploy/connect-chatgpt) define the current flow.

### Codex desktop direct MCP connection

Open the Codex desktop app’s **Settings → MCP servers → Add server**. Use name `habitica-companion-dev`, transport **Streamable HTTP**, and the same exact HTTPS `/mcp` endpoint. Leave static bearer token/header fields empty. Save, restart the MCP connection when requested, and choose **Authenticate**.

Use a different fresh linking code from Settings for the Codex OAuth page. Verify the dev Auth hostname, account label and scopes. The redirect back to a local loopback address is selected by Codex; do not replace the redirect with a ChatGPT callback or add wildcard origins. Start a fresh Codex chat and request the same read-only account check. A successful desktop tool call supplies separate desktop evidence.

If the desktop menu differs, the installed CLI offers the following explicit alternative. Substitute the actual hostname; the commands update local Codex MCP settings and may open OAuth immediately:

```sh
codex mcp add habitica-companion-dev --url https://habitica-companion-dev-mcp.ACTUAL_ACCOUNT_SUBDOMAIN.workers.dev/mcp
codex mcp login habitica-companion-dev --scopes profile:read,account:read,tasks:read
```

Run login only if authentication did not already complete during add. CLI success supplements, but does not replace, a desktop tool call. [Official MCP setup](https://learn.chatgpt.com/docs/extend/mcp?surface=app) documents shared local configuration and OAuth. The installed CLI’s `codex mcp add --help` and `codex mcp login --help` confirm the command options.

### Packaged skills: separate local desktop installation

A custom-server connection exposes MCP tools without establishing installation of the three skill files. The staged package is `/Users/petr/Projects/habitica-tool/deploy/.dev/plugin/`. The package needs a local marketplace entry in a supported desktop client.

For a new separate local marketplace root, run the following block once from the repository. If the target already exists, preserve the existing package and let the agent update the staging directory instead of copying another nested directory.

```sh
mkdir -p deploy/.dev/local-marketplace/plugins
mkdir -p deploy/.dev/local-marketplace/.agents/plugins
cp -R deploy/.dev/plugin deploy/.dev/local-marketplace/plugins/habitica-companion
nano deploy/.dev/local-marketplace/.agents/plugins/marketplace.json
```

Paste the JSON below into the empty editor, save with Control+O and Return, then exit with Control+X:

```json
{
  "name": "habitica-dev-local",
  "plugins": [
    {
      "name": "habitica-companion",
      "source": { "source": "local", "path": "./plugins/habitica-companion" },
      "policy": { "installation": "AVAILABLE", "authentication": "ON_INSTALL" },
      "category": "Productivity"
    }
  ]
}
```

An agent can create the local staging file without modifying the global personal marketplace. Register the root with:

```sh
codex plugin marketplace add /Users/petr/Projects/habitica-tool/deploy/.dev/local-marketplace
```

Restart the desktop app, locate the local marketplace/plugin in Plugins, install/connect the package and test `$habitica-account-review`, `$habitica-daily-review` and `$habitica-task-management`. Retain only the intended active connection while testing to avoid confusing duplicate server identities. Do not overwrite an existing personal marketplace or submit the plugin publicly. The recipe follows [official local marketplace packaging](https://developers.openai.com/plugins/build/plugins); actual desktop installation and model activation remain unrun gates until observed. A hosted web client does not load a local folder automatically; hosted packaged-skill availability requires a separately supported workspace/plugin route.

## 14. First live test and later release checks

Run the first pilot in each client independently:

1. Request account status and Dailies, explicitly changing nothing.
2. Ask to create a Companion-only Todo named `DEV TEST — read chapter 2`, with no Habitica projection. Require a preview before approval. The accepted receipt must distinguish saved Companion data from a Habitica effect.
3. Use the original command receipt to inspect the saved result. Do not request a replacement command because a response was delayed or lost.
4. Reconnect/refresh the client and check the original saved identity again.
5. Test grant revocation last: Settings **Revoke ChatGPT access**, then confirm. The current label revokes OAuth grants for both clients; separate Workspace access remains. Old client access/previews must be denied. Link again with fresh consent/code if continuing.

Record the dev commit, client/app version, test request, expected result, actual result and any redacted error. Do not retain tokens, codes, credential-bearing headers or private task text. Use the [client matrix](../plugin/README.md#client-qualification-matrix) for the full set. Existing browser scoring/equipment/spell buttons remain outside the first pilot.

Later, a second account owner repeats enrollment independently to check isolation and revocation. Mobile owners check the same dev origin on real iOS Safari/PWA and Android, recording device/browser versions. Party/game-effect lanes require selected test scope and deliberate activation; live Cron stays off. Broader release completion cannot be inferred from one owner’s successful pilot.

## Stop, troubleshoot and recover

| Symptom | Next action |
| --- | --- |
| `no such file` for repository/Wrangler | Repeat section 1; verify path and install locked dependencies only if missing |
| Cloudflare authentication/account error | Repeat login and whoami; verify selected account and permissions |
| Missing/invalid ID or origin in generator | Correct the exact public JSON field; no trailing slash, path or fake ID |
| Resource/project name unavailable | Select another consistent `-dev` project prefix and regenerate; do not reuse production |
| Worker binding/service missing | Verify Auth → Workspace → MCP deploy order and generated dev configs |
| D1 table missing | Apply the matching dev migration set before use |
| Linking/workspace unavailable in website | Inspect generated/published appsettings and Worker flags; no source-default URL should be published |
| Expired/used linking code | Generate a new code and restart that authorization attempt |
| Quota or Retry-After | Wait the stated period; do not repeatedly click or replay a mutation |
| Lost command acknowledgement | Look up the original receipt; preserve unknown outcomes |
| Missing custom plugin/marketplace controls | Record client/version/policy limitation and preserve the unrun gate |

To stop cloud access, set `ENROLLMENT_ENABLED`, `OAUTH_ENABLED` and `WORKSPACE_ENABLED` false in the corresponding generated Auth/MCP/Workspace configs, then redeploy affected Workers. Keep every game-effect/executor/Cron flag false. Per-account disconnect/deletion controls are separate from stopping the environment. Do not delete databases or accepted command history to resolve an uncertain effect. A static asset rollback cannot undo database schema or confirmed Habitica effects. Restore only a verified dev backup with a recorded compatible schema.

## Anonymous quota diagnostics

The Auth database retains fixed anonymous daily counts for local quota denials and actual upstream 429 responses. An operator can inspect only the metric rows in the dev D1 console:

```sql
SELECT CASE WHEN bucket LIKE 'metric:local_denied:%' THEN 'local_quota_denials' ELSE 'upstream_429' END AS metric,
       SUM(attempts) AS count
FROM authorization_attempts
WHERE (bucket LIKE 'metric:local_denied:%' OR bucket LIKE 'metric:upstream_429:%')
  AND expires_at_utc > strftime('%Y-%m-%dT%H:%M:%fZ','now')
GROUP BY metric;
```

User Settings exposes only the active workspace's saved-work counts. Status checks neither poll Habitica nor retry commands. Local quota errors carry Retry-After; upstream cooldown and credential-repair state remain authoritative. Original receipt recovery has a separate bounded allowance. Raw counter buckets, profile identifiers, credentials and request bodies remain excluded from shared diagnostics.
