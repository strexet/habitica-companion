CREATE TABLE workspace_reminder_commands (
 profile_id TEXT NOT NULL REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 request_id TEXT NOT NULL,
 command_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,
 access_epoch TEXT NOT NULL,
 grant_epoch TEXT NOT NULL,
 input_hash TEXT NOT NULL,
 intent_json TEXT NOT NULL,
 expected_workspace_revision INTEGER NOT NULL,
 token_hash TEXT,
 expires_at_utc TEXT NOT NULL,
 receipt_json TEXT,
 created_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,request_id),
 UNIQUE(profile_id,command_id)
);
CREATE TABLE workspace_reminder_schedules (
 profile_id TEXT NOT NULL REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 schedule_id TEXT NOT NULL,
 revision INTEGER NOT NULL CHECK(revision>0 AND revision<=9007199254740991),
 connection_id TEXT NOT NULL,
 access_epoch TEXT NOT NULL,
 task_id TEXT NOT NULL,
 cue_utc TEXT NOT NULL,
 cue_revision INTEGER NOT NULL,
 consent_epoch TEXT NOT NULL,
 state TEXT NOT NULL CHECK(state IN ('active','cancelled','paused','finished')),
 reason TEXT NOT NULL,
 commit_tag TEXT NOT NULL,
 intent_json TEXT NOT NULL,
 next_at_utc TEXT,
 next_civil_date TEXT,
 lease_token TEXT,
 lease_until_ms INTEGER,
 updated_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,schedule_id)
);
CREATE UNIQUE INDEX workspace_reminder_active_task ON workspace_reminder_schedules(profile_id,task_id) WHERE state='active';
CREATE INDEX workspace_reminder_due ON workspace_reminder_schedules(next_at_utc) WHERE state='active';
CREATE TABLE workspace_reminder_deliveries (
 profile_id TEXT NOT NULL REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 occurrence_id TEXT NOT NULL,
 schedule_id TEXT NOT NULL,
 schedule_revision INTEGER NOT NULL,
 civil_date TEXT NOT NULL,
 at_utc TEXT NOT NULL,
 expires_at_ms INTEGER NOT NULL,
 device_id TEXT NOT NULL,
 device_revision INTEGER NOT NULL,
 subscription_id TEXT NOT NULL,
 state TEXT NOT NULL CHECK(state IN ('scheduled','dispatched','accepted','refused','unknown','expired','cancelled','retryable')),
 code TEXT NOT NULL,
 attempt INTEGER NOT NULL,
 dispatch_id TEXT NOT NULL,
 retry_after_ms INTEGER,
 http_status INTEGER,
 attempts_json TEXT NOT NULL DEFAULT '[]',
 lease_token TEXT,
 lease_until_ms INTEGER,
 updated_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,occurrence_id,device_id),
 UNIQUE(profile_id,schedule_id,schedule_revision,at_utc,device_id)
);
CREATE INDEX workspace_reminder_delivery_due ON workspace_reminder_deliveries(state,retry_after_ms);
CREATE TABLE workspace_reminder_runtime (
 profile_id TEXT PRIMARY KEY REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 access_epoch TEXT NOT NULL,
 lease_token TEXT NOT NULL,
 lease_until_ms INTEGER NOT NULL,
 last_wake_ms INTEGER NOT NULL DEFAULT 0
);
