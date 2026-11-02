# Hosted gateway operator handoff

Task: `T-ec421773-5864-4bc5-b17f-ce04e0d818c2`.
Run: `WR-305a9958-f4d2-4b9c-98ac-bd6663206c66`.

## Scope

The repository owner authorizes temporary gateway/claim activation, synthetic controls against owned fixtures and subsequent disable/rollback. The authorization receipt resides in `owner-scope.json` beside the operator handoff. The selected normal-delivery boundary resides in `normal-delivery-profile.json`. The exact HTTPS receiver and executable hosted recipe remain prerequisites. Authorization requires no repeated confirmation for the accepted operations.

The existing gateway is `https://kaleidoscopic-rolypoly-416a42.netlify.app/callback-egress`. The selected Auth Worker is `habitica-companion-auth`. Historical deployment records identify candidates; current deployment, runtime, configuration and rollback identities require observation before activation.

## Netlify access

Owner sign-in is complete. The authenticated project configuration identifies **Flying Monolith**, team slug `strexet`, and project ID `04629462-d0c6-4a72-bf02-6170cb1c39f8`. The team billing view identifies the Free plan and 258 remaining credits from 300. Provider observations reside in `provider-inspection.json` beside the operator handoff. Credentials remain outside chat and repository evidence.

Project identity is visible under **Project configuration > General > Project details > Project information**. Plan and credits are visible under team **Usage & billing**. The agent can inspect the authenticated project without further owner sign-in while the browser session remains valid.

## Normal-delivery fixture

The owner identifies the existing `habitica-companion` project as the available public domain. Repository deployment documentation identifies the frontend as `habitica-companion.pages.dev`. The selected stage needs an isolated HTTP receiver on existing owned HTTPS hosting, exact deployed source/runtime identities, signed-byte observations, original claim/replay assertions and disable/rollback evidence. Separate DNS zones and raw TLS servers are unnecessary for the selected stage. Receiver deployment and normal signed delivery remain unexecuted.

The agent prepares the receiver, reduced synthetic counters and concrete validation recipe inside the accepted scope. No extra manual domain/server setup is required for the selected normal-delivery stage. Existing destination/certificate guards and local negative tests remain enforced. A successful request establishes only the exercised flow; unexecuted hosted failure paths remain explicit limitations.

## Deferred DNS/TLS fixtures

Pending `P-b1c8f9b2-fe66-406a-aa04-70aab3a38f9b`, priority100, owns dedicated hosted DNS/TLS failure injection. The deferred coverage is nonblocking for normal-delivery qualification. Project hostnames do not establish control over an authoritative DNS zone or the provider TLS listener. An independently controlled zone and raw TLS server remain unconfirmed.

An existing disposable server or VPS with a public IP and owner-controlled DNS names can supply the remaining fixtures. Resource purchases are not part of the handoff. Public IP addresses and hostnames identify proposed fixtures and do not establish ownership alone. The fixture binding requires the owner-controlled test allocation and permitted cleanup scope.

The deferred fixture requires authoritative DNS control and direct TCP443/TLS control. Healthy HTTPS, redirects and bounded HTTP delays alone do not cover invalid certificate or stalled TLS controls. A generic public echo endpoint or an unrelated production application is unsuitable.

Dedicated fixture research supplies exact DNS records, certificate commands and receiver configuration when an owned DNS provider and server platform are available. The owner need not implement webhook verification, HTTP handlers or the hosted runner manually. Deferred fixture capabilities are:

- An isolated hostname with a publicly trusted certificate, synthetic payload observations and reduced per-request counters.
- Owned IPv4 and IPv6 listeners for the required address-family controls.
- Separate controlled wrong-host and untrusted-certificate listeners; refused TLS must produce no HTTP payload observation.
- An owned redirect source and sink with independent counters.
- Stalled handshake, delayed response, oversized response and interrupted acknowledgement controls on isolated listeners.
- Dedicated DNS names for public, private-only, mixed-answer and changed-answer controls. Private targets remain inside the approved fixture boundary.

Ordinary Netlify HTTPS delivery cannot substitute for independent raw TLS behavior. Physical devices, real subscriptions and chat rendering remain outside the hosted transport acceptance.

## Existing project coverage

An isolated HTTP receiver implemented as a Pages Function can support synthetic signed-byte observations, response codes, redirects, delayed responses and bounded-body controls. The receiver still requires task-scoped source changes, controlled reduced counters, exact deployment identity and cleanup before use. No receiver is deployed by the access preflight.

Cloudflare terminates inbound TLS before passing HTTP requests to the Worker. A Pages Function cannot select a wrong certificate or stall the raw inbound TLS handshake. Provider-managed DNS for `pages.dev` and `netlify.app` cannot supply controlled private/mixed/changing DNS answers for the project hostname. HTTP-only coverage leaves the deferred hosted DNS/TLS scenarios unverified without blocking the owner-selected normal-delivery stage.

## Agent continuation

1. Re-admit the same run; retain the original snapshot, task identity and failure evidence.
2. Verify selected Netlify owner/team/project, actual Free plan/quota, Node22 runtime, deployed source identity, Auth/D1 bindings, logging and current rollback identity.
3. Bind the exact owned HTTPS receiver and permitted synthetic operations to the normal-delivery profile. Prepare the concrete `netlify-hosted-controls` recipe and refresh/admit the installation before check preparation.
4. Execute approved controls with the existing per-operation deadlines and cleanup reserve. Retain original request identities and unknown-outcome fences.
5. Disable the approved lanes and verify rollback/shutdown. Preserve the additive claim table and original fences.
6. Record normal-delivery acceptance only after every selected control passes. List the deferred hosted DNS/TLS limitations explicitly. Resume the frozen Implement All snapshot through the controller; deferred fixture work remains outside the snapshot.

## Sources

- [Netlify project ID and team slug locations](https://docs.netlify.com/api-and-cli-guides/api-guides/get-started-with-api/).
- [Netlify plan and Usage & billing information](https://www.netlify.com/pricing/).
- [Netlify function logs and retention](https://docs.netlify.com/build/functions/logs/).
- [Cloudflare inbound protocols](https://developers.cloudflare.com/workers/reference/protocols/).
- [Cloudflare TLS termination boundary](https://developers.cloudflare.com/workers/reference/security-model/).
- [Netlify managed HTTPS](https://docs.netlify.com/manage/domains/secure-domains-with-https/https-ssl/).
- Repository `deploy/callback-egress/src/transport.mjs` and the retained hosted profile define the required transport behavior.
