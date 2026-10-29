# Events callback hosting decision

Decision date: 2026-10-29.

Owner selection: Netlify Functions Free is the primary hosting target for the narrow Node.js HTTPS callback egress gateway. The owner prefers a free option and requests setup assistance when implementation reaches deployment. The current request is to record the decision in Future.

Planning identities: Pending P-2d43941f-f8b5-489c-9467-dd2dcc7383a4; subscription parent T-280cb859-c0f7-46e5-97db-bbd41dc6a99a; active frozen run WR-5663ac54-9d1e-4145-8792-17c83b2b8bd3.

Required deliverables:

- A deployable Netlify Node.js function, deployment configuration, guided account/Free-plan setup, secret configuration, health checks and rollback instructions.
- Auth-owned encrypted callback secrets and signatures; Workspace/D1-owned subscriptions, delivery state and original receipts; an authenticated bounded gateway without stored signing keys or generic public proxy access.
- Connection-time public-address validation, approved-address connection binding, original-host TLS verification, no redirects, bounded requests and redacted diagnostics for verification and event delivery.
- Current quota/runtime/access checks and hosted negative controls, followed by the actual ChatGPT subscription, verification, event delivery and unsubscribe flow. Local tests or an HTTP acknowledgment alone do not prove a chat response. Local Codex chat wake-up remains unverified.
- Explicit quota-exhaustion behavior preserving undelivered state. No automatic paid upgrade or provider fallback.

Execution boundary: the decision resolves the hosting choice under rules.md section14. The planning update does not resume implementation, process Pending, create accounts, configure keys, deploy services, activate subscriptions or authorize paid resources. Deployment preparation and operator assistance belong to the future implementation; live provisioning and qualification require the concrete selected account and execution scope.

Research handoff: verify the current [Netlify Free plan](https://www.netlify.com/pricing/), [credit accounting](https://docs.netlify.com/manage/accounts-and-billing/billing/billing-for-credit-based-plans/credit-based-pricing-plans/) and [Node.js runtime](https://docs.netlify.com/build/functions/configuration/#nodejs-version-for-runtime) during scoped processing. Platform documentation establishes a candidate runtime, not deployed transport acceptance.

The original local-gateway proposal and result remain historical evidence. The Future contract and owner selection supersede the unanswered hosting choice without changing the original evidence verdict.
