CREATE TABLE integration_projection_state (
 profile_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,
 lease_token TEXT,
 lease_until_utc TEXT,
 next_automated_at_utc TEXT,
 PRIMARY KEY(profile_id,connection_id)
);
CREATE TABLE integration_projection_checks (
 token_hash TEXT PRIMARY KEY,
 profile_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,
 selector TEXT NOT NULL,
 task_id TEXT,
 fingerprint TEXT,
 kind TEXT,
 completed INTEGER,
 up INTEGER,
 down INTEGER,
 is_due INTEGER,
 account_json TEXT,
 checked_at_utc TEXT NOT NULL,
 expires_at_utc TEXT NOT NULL
);
CREATE TABLE integration_projection_attempts (
 profile_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,
 attempt_id TEXT NOT NULL,
 payload_hash TEXT NOT NULL,
 operation TEXT NOT NULL,
 state TEXT NOT NULL,
 outcome_json TEXT,
 created_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,connection_id,attempt_id)
);
