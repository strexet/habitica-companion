# Callback HTTPS egress decision

## Observed blocker

The selected subscription unit T-280cb859-c0f7-46e5-97db-bbd41dc6a99a requires connection-time public-address enforcement, original-host TLS verification, bounded requests and no redirects. No protected callback transport exists in the repository. The current offline Events prototype explicitly supplies fake address/connection assertions.

The installed local runtime is workerd1.20261012.1 under Miniflare5.20261012.0-beta. Matching upstream sockets.c++ lines862–871 mark expectedServerHostname unsupported, optionally reject the option and otherwise emit an unsupported-option warning. Later code forwards a hostname to the TLS starter; the declaration and forwarding do not establish a supported production certificate-identity guarantee. The current upstream production-edge issue6903 reports a difference from local behavior. No production-edge probe was performed.

Global fetch strictly public controls public-zone routing; the official contract does not supply an approved-address pin plus independent TLS hostname. DNS preflight followed by fetch leaves the connection lookup unconstrained. Fetching an IP with an HTTP Host override does not establish certificate verification for the original hostname. resolveOverride requires a proxied hostname in the incoming Cloudflare zone and cannot supply a general dynamic callback-address binding. Direct sockets also reject Cloudflare destination ranges. A local fake, a type declaration or a compatibility flag cannot close the required acceptance boundary.

## Recommended decision

Approve local implementation and synthetic qualification of a narrow Node22 HTTPS egress gateway as a required dependency. The gateway adds a backend trust boundary, so repository rules section14 requires an owner decision. The approval would cover code and local tests only. Deployment, public endpoints, keys, real callback provisioning and subscriptions remain excluded.

The Auth Worker remains the only callback URL/signing-secret vault and the only signer. Workspace retains opaque callback identity and receipts. A separately authenticated gateway operation receives only the exact HTTPS destination, bounded signed challenge/event bytes and permitted webhook headers. The gateway never receives signing keys, stores callback credentials or exposes generic browser/client network access. Access must bind Auth service identity, operation, payload digest, expiry and replay identity; unauthenticated requests refuse.

The gateway resolves every destination for each new connection, rejects non-public IPv4/IPv6 and mixed public/private answers, connects only to the selected approved address and checks the connected remote address. Node TLS verifies the original hostname and certificate chain with servername/Host preserved. Connection pooling and automatic redirects remain disabled. DNS, connect, TLS, headers and body share a hard deadline; response bytes, request bytes and header names are bounded. Secrets, URLs, bodies and signed headers stay out of logs. Supported operation results distinguish refusal, confirmed transport response and original uncertainty.

Local tests use synthetic identities and a controlled TLS fixture with a test-only CA. Tests prove matching/mismatched hostname and certificate handling, public/private/mixed/rebinding addresses, no second DNS lookup, redirects, timeout/byte limits, exact signed bytes, replay controls, unknown outcomes and secret-free diagnostics. Test CA injection never enters production configuration. Auth/Workspace tests retain default-off activation and fail closed without a qualified gateway. A separate operator-selected profile must qualify the eventual hosted runtime before activation.

## Alternative

Keep the subscription and reminder Events sequence blocked until a supported runtime or existing protected egress service can prove the same contract. Do not replace the contract with ordinary fetch, disable TLS verification, install a userland TLS workaround or claim local simulation qualifies the production edge.

## Evidence

- [Pinned workerd source](https://github.com/cloudflare/workerd/blob/v1.20261012.1/src/workerd/api/sockets.c%2B%2B#L862-L871)
- [Production-edge hostname override report](https://github.com/cloudflare/workerd/issues/6903)
- [Cloudflare socket restrictions](https://developers.cloudflare.com/workers/runtime-apis/tcp-sockets/)
- [Global fetch strictly public](https://developers.cloudflare.com/workers/configuration/compatibility-flags/#global-fetch-strictly-public)
- [Request resolveOverride contract](https://developers.cloudflare.com/workers/runtime-apis/request/#requestinitcfproperties)

The ignored pinned-source copy has SHA256 0c1f04a0ebcf47a6c2e511db2be5e100697771bd97c7658c4682f35481b96991. The assessment is source/documentation evidence, not a live production-network result. No callback URL, signing key, private note or account data was used.
