CREATE TABLE linking_tickets (
  ticket_id TEXT PRIMARY KEY,
  profile_id TEXT NOT NULL,
  connection_id TEXT NOT NULL,
  code_hash TEXT NOT NULL UNIQUE,
  created_at_utc TEXT NOT NULL,
  expires_at_utc TEXT NOT NULL,
  consumed_at_utc TEXT,
  failed_attempts INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX linking_expiry ON linking_tickets(expires_at_utc);
CREATE TABLE consent_links (
  handle_hash TEXT PRIMARY KEY,
  profile_id TEXT NOT NULL,
  connection_id TEXT NOT NULL,
  expires_at_utc TEXT NOT NULL,
  consumed_at_utc TEXT
);
CREATE TABLE authorization_attempts (
  bucket TEXT PRIMARY KEY,
  attempts INTEGER NOT NULL,
  expires_at_utc TEXT NOT NULL
);
