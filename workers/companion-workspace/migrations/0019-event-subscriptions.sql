CREATE TABLE workspace_event_consents (
 profile_id TEXT NOT NULL,task_id TEXT NOT NULL,connection_id TEXT NOT NULL,access_epoch TEXT NOT NULL,grant_epoch TEXT NOT NULL,
 consent_epoch TEXT NOT NULL,enabled INTEGER NOT NULL,cue TEXT,cue_revision INTEGER NOT NULL,
 PRIMARY KEY(profile_id,task_id)
);
CREATE TABLE workspace_event_commands (
 profile_id TEXT NOT NULL,request_id TEXT NOT NULL,command_id TEXT NOT NULL,task_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,access_epoch TEXT NOT NULL,grant_epoch TEXT NOT NULL,
 input_hash TEXT NOT NULL,input_json TEXT NOT NULL,token_hash TEXT NOT NULL,expires_at INTEGER NOT NULL,receipt_json TEXT,
 PRIMARY KEY(profile_id,request_id),UNIQUE(profile_id,command_id)
);
CREATE TABLE workspace_event_subscriptions (
 profile_id TEXT NOT NULL,subscription_id TEXT NOT NULL,callback_id TEXT NOT NULL,task_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,access_epoch TEXT NOT NULL,grant_epoch TEXT NOT NULL,consent_epoch TEXT NOT NULL,
 revision INTEGER NOT NULL,state TEXT NOT NULL,expires_at INTEGER NOT NULL,operation_id TEXT NOT NULL,
 PRIMARY KEY(profile_id,subscription_id)
);
CREATE INDEX workspace_events_due ON workspace_event_subscriptions(state,expires_at);
