# Local callback egress review

Scope: gateway entry/transport/package, Auth service envelope and one-use claim route/migration, explicit rollout flag mapping and matching tests/documentation. Existing unrelated dirty changes remain outside the review.

No actionable issue remains in the inspected task changes. TLS payload writes follow trusted-chain, original-host and connected-address checks. The public classifier precedes the internal transport primitive; the deployed function supplies no test CA, resolver or destination override. D1 reserves an original identity before gateway dispatch; acknowledgement loss never authorizes another send. Raw callback URL/body/webhook credentials remain transient and absent from claim rows and reduced error output.

Corrections verified during local validation: Origin refusal accepts the existing early HTTP403 gate while asserting zero reservations; the Node-only envelope resides under Auth rather than shared Worker type scope; source packaging refuses existing archives rather than retaining stale ZIP members; Auth ingress and TLS handshake have explicit cancellation controls.

No changed application UI route or C# source requires a new Blazor build/rendered matrix. Auth type/dry build, matching MCP/Workspace bundles, actual local TLS, synthetic Worker/D1 and source/archive checks cover the local candidate. Provider deployment, account/logging policy, public DNS changes, real callbacks, subscriptions and rendered chat delivery remain unqualified.
