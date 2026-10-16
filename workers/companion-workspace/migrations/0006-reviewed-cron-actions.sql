CREATE TABLE prepared_cron_actions (
 profile_id TEXT NOT NULL REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 action_id TEXT NOT NULL,
 request_id TEXT NOT NULL,
 input_hash TEXT NOT NULL,
 connection_id TEXT NOT NULL,
 access_epoch TEXT NOT NULL,
 token_hash TEXT,
 status TEXT NOT NULL CHECK(status IN ('prepared','accepted','dispatching','confirmed','stopped','unknownOutcome')),
 version INTEGER NOT NULL DEFAULT 1,
 payload_json TEXT NOT NULL,
 entry_json TEXT NOT NULL,
 attempt_json TEXT,
 created_at_utc TEXT NOT NULL,
 expires_at_utc TEXT NOT NULL,
 consumed_at_utc TEXT,
 PRIMARY KEY(profile_id,action_id),
 UNIQUE(profile_id,request_id)
);
CREATE INDEX prepared_cron_actions_fence ON prepared_cron_actions(profile_id,status);
