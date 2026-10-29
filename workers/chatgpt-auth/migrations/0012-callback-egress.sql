-- Transient one-use transport fences only. No callback URL, body, signature or profile data.
CREATE TABLE integration_callback_egress_claims (
  request_id TEXT PRIMARY KEY NOT NULL,
  grant_hash TEXT NOT NULL,
  expires_at_ms INTEGER NOT NULL,
  claimed_at_ms INTEGER NOT NULL
);
CREATE INDEX integration_callback_egress_expiry ON integration_callback_egress_claims(expires_at_ms);
