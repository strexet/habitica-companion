CREATE TABLE workspace_event_outbox (
 profile_id TEXT NOT NULL,event_id TEXT NOT NULL,subscription_id TEXT NOT NULL,task_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,access_epoch TEXT NOT NULL,grant_epoch TEXT NOT NULL,consent_epoch TEXT NOT NULL,
 cue TEXT NOT NULL,cue_revision INTEGER NOT NULL,expires_at INTEGER NOT NULL,
 state TEXT NOT NULL,attempt INTEGER NOT NULL,next_at INTEGER NOT NULL,lease_token TEXT,lease_until INTEGER,
 receipt_json TEXT,created_at INTEGER NOT NULL,
 PRIMARY KEY(profile_id,event_id),UNIQUE(profile_id,subscription_id,consent_epoch,cue,cue_revision)
);
CREATE INDEX workspace_event_outbox_ready ON workspace_event_outbox(state,next_at);
