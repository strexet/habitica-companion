# Dev deployment operator checklist

Status: local dev prerequisites and synthetic recovery qualification pass; isolated dev configuration is prepared. Remote resources, hosted enrollment and client/device qualification remain unverified. The source defaults remain disabled. ChatGPT and Codex desktop are required test clients; live Cron stays off. [Deployment readiness](../skill-tree/documents/deploy-cloudflare-pages.md#intermediate-dev-launch-readiness) owns launch gates.

## 1. Provide Cloudflare access and choose addresses

The operator needs access to a Cloudflare account with permission to create Workers, Pages, two KV namespaces, three D1 databases and Worker secrets. Use an existing eligible account; confirm account limits/billing in the dashboard. Local CLI authentication is interactive:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler login
./workers/chatgpt-auth/node_modules/.bin/wrangler whoami
```

Run commands from the repository root after dependencies are installed. Do not paste authentication output or tokens into chat. Report only the selected account ID and non-secret resource identities. The account/project configuration must refer to `develop` code from `strexet/habitica-tool`.

Choose an available dedicated direct-upload Pages project ending in `-dev`, separate from the existing production project. Commands below use `habitica-companion-dev` as the example; replace the project and derived names consistently if unavailable. The matching stable PWA origin is `https://habitica-companion-dev.pages.dev`. Worker names generated from the example project are `habitica-companion-dev-auth`, `habitica-companion-dev-mcp` and `habitica-companion-dev-workspace`.

For each Worker, select either a custom hostname in an account-owned Cloudflare zone or the matching stable `https://<worker-name>.<account-subdomain>.workers.dev` address. Custom domains require the corresponding zone/DNS access. Every service needs a distinct HTTPS origin. Ephemeral deployment preview URLs cannot serve as stable OAuth identities.

## 2. Create empty dev resources

Create resources through the Cloudflare dashboard or the following commands. Each command changes only the named dev resource; verify the selected account before execution. Existing production resources must remain untouched.

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler pages project create habitica-companion-dev --production-branch develop
./workers/chatgpt-auth/node_modules/.bin/wrangler kv namespace create habitica-companion-dev-sync
./workers/chatgpt-auth/node_modules/.bin/wrangler kv namespace create habitica-companion-dev-oauth
./workers/chatgpt-auth/node_modules/.bin/wrangler d1 create habitica-companion-dev-party
./workers/chatgpt-auth/node_modules/.bin/wrangler d1 create habitica-companion-dev-auth
./workers/chatgpt-auth/node_modules/.bin/wrangler d1 create habitica-companion-dev-workspace
```

Record the two namespace IDs and three database IDs. Confirm each resource is newly created and empty, and each ID differs from the production resource IDs. Existing Pages configuration points normal/preview bindings at matching IDs; the generated dedicated dev configuration replaces the dependency on the existing project.

## 3. Prepare and review public configuration

Copy `deploy/dev.example.json` to ignored `deploy/dev.local.json`. Replace every placeholder with the chosen account ID, origins, resource IDs and the public creator/app header. No API token, password, encryption key or OAuth token belongs in the input file.

```sh
node deploy/prepare-dev.mjs deploy/dev.local.json
```

The generator rejects missing fields, reused committed resources/origins, invalid addresses, non-dev project names and unknown fields. Output resides in ignored `deploy/.dev/`; an existing output directory is refused. After input corrections, preserve any needed generated material and choose a fresh output directory explicitly.

Review `deployment-plan.json`, the three Worker configs, `pages/wrangler.json`, `appsettings.json` and the staged plugin. Confirm account, resource and origin identities match. Initial flags enable enrollment, OAuth and Workspace only. Projection, party planning, spells, background execution, private key operations and live Cron remain disabled. The staged Pages directory includes the existing Functions and isolated dev bindings.

## 4. Provision encryption secrets

Generate three distinct random 256-bit base64url secrets with a trusted local secret manager. Retain a secure backup; loss of the stable profile key or required credential read keys prevents recovery. Set the secrets through interactive CLI prompts; do not include values in command arguments, files tracked by Git or chat:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler secret put PROFILE_ID_KEY --config deploy/.dev/chatgpt-auth.json
./workers/chatgpt-auth/node_modules/.bin/wrangler secret put CREDENTIAL_KEY_V1 --config deploy/.dev/chatgpt-auth.json
./workers/chatgpt-auth/node_modules/.bin/wrangler secret put CREDENTIAL_KEY_V2 --config deploy/.dev/chatgpt-auth.json
```

Inspect provider log and backup retention before real enrollment. Require secret redaction, no conversation logging and the documented 30-day operational audit cleanup. Keep key operations disabled; initial deployment requires no key migration.

## 5. Apply dev schemas and publish

Only after local launch prerequisites pass and the generated plan is reviewed, apply migrations to the exact dev bindings:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler d1 migrations apply HABITICA_INTEGRATION_DB --remote --config deploy/.dev/chatgpt-auth.json
./workers/chatgpt-auth/node_modules/.bin/wrangler d1 migrations apply WORKSPACE_DB --remote --config deploy/.dev/companion-workspace.json
./workers/chatgpt-auth/node_modules/.bin/wrangler d1 migrations apply HABITICA_PARTY_DB --remote --config deploy/.dev/pages/wrangler.json
```

Publish Auth, Workspace and MCP in dependency order. The Worker names, routes and bindings come from reviewed dev configuration:

```sh
./workers/chatgpt-auth/node_modules/.bin/wrangler deploy --config deploy/.dev/chatgpt-auth.json
./workers/chatgpt-auth/node_modules/.bin/wrangler deploy --config deploy/.dev/companion-workspace.json
./workers/chatgpt-auth/node_modules/.bin/wrangler deploy --config deploy/.dev/chatgpt-mcp.json
HABITICA_FRONTEND_SETTINGS_FILE="$PWD/deploy/.dev/appsettings.json" ./build.sh
./workers/chatgpt-auth/node_modules/.bin/wrangler --cwd deploy/.dev/pages pages deploy "$PWD/output/wwwroot" --project-name habitica-companion-dev --branch develop
```

Inspect published `output/wwwroot/appsettings.json` before the Pages upload. The build preserves complete merged settings and restores the source settings on success/failure. Record the deployed commit and resource/migration results. Dev migrations must never target production storage. Asset rollback cannot undo schema changes or real Habitica effects.

## 6. Participate in live client qualification

1. Open dev Settings and complete explicit integration storage consent for the test account. Enable cloud workspace access separately; adoption of existing browser data remains explicit. Use a test browser profile to preserve ordinary local data.
2. Generate a fresh one-time linking code in dev Settings for each client. The current card label is “ChatGPT linking”; linking supports both clients and each code is consumed once. Connect the dev MCP address in ChatGPT using the supported custom MCP flow. Generate another code, connect the same address in Codex desktop MCP settings and complete separate OAuth consent. Use the exact callback selected by each client; no redirect or Origin wildcard is required.
3. Install the staged `deploy/.dev/plugin/` through a supported private/local plugin source and check all three bundled skills in each client. Record unavailable host features as failed/unrun gates. Direct MCP connectivity does not qualify plugin activation.
4. Run the [client matrix](../plugin/README.md#client-qualification-matrix): reads, denied scope, reviewed canonical task save, original receipt recovery, reconnect and revocation. Initial tests save Companion data only. Projection, spells and background execution remain off; live Cron remains off throughout dev preparation. The flags constrain cloud integration only; existing browser Habitica controls can still change the real account. Keep initial tests within the canonical Companion task lane and avoid legacy scoring/equipment/spell controls.
5. Invite one consenting teammate for the separate account-isolation/revocation stage. The operator cannot consent on behalf of another account owner. Real iOS Safari/PWA and Android qualification requires selected devices and owners; record versions and redacted results.

## 7. Stop and recover

Disable enrollment/OAuth and Workspace access in the generated configs and redeploy the affected Workers to stop new access. Disable the executor before any later effectful testing. Retain command history and unknown outcomes; never retry uncertain game effects. Revoke host access through account controls as needed. Restore only from verified dev backups and record schema compatibility. Keep production separate and leave live Cron disabled.

## Owner participation and automation handoff

Interactive Cloudflare account login/selection, address choices, secure secret entry and each account owner’s consent require owner participation. Resource creation, public configuration, migrations and publication commands are prepared for an operator or agent with the selected account access and explicit dev deployment scope. No production publication is part of the dev plan.

The first owner pilot requires only the dedicated dev environment and the owner’s available clients. A second consenting tester and physical devices qualify broader release gates; absence of the second-user/device lanes does not block the initial owner pilot.

## Information required from the operator

- Selected Cloudflare account ID and confirmed account permissions.
- Dedicated dev Pages project and four stable service origins.
- Two new dev KV namespace IDs and three new dev D1 database IDs.
- Confirmation of secure secret provisioning and provider retention settings, without secret values.
- Available ChatGPT custom-MCP access, Codex desktop version and private plugin installation route.
- A consenting second tester and available iOS/Android devices for the broader release qualification stage.

[Official Cloudflare Pages configuration](https://developers.cloudflare.com/pages/functions/wrangler-configuration/), [D1 migrations](https://developers.cloudflare.com/d1/reference/migrations/) and [Codex MCP setup](https://learn.chatgpt.com/docs/extend/mcp?surface=app) define the platform setup details.

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
