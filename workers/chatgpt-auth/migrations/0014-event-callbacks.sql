CREATE TABLE integration_event_callbacks (
 profile_id TEXT NOT NULL,task_id TEXT NOT NULL,subscription_id TEXT NOT NULL,revision INTEGER NOT NULL,callback_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,grant_epoch TEXT NOT NULL,consent_epoch TEXT NOT NULL,operation_id TEXT NOT NULL,
 ciphertext TEXT NOT NULL,iv TEXT NOT NULL,key_version TEXT NOT NULL,expires_at INTEGER NOT NULL,
 request_id TEXT NOT NULL,state TEXT NOT NULL,receipt_json TEXT,
 PRIMARY KEY(profile_id,subscription_id,revision),UNIQUE(request_id)
);
