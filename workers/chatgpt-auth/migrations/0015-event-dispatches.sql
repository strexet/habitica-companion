CREATE TABLE integration_event_dispatches (
 profile_id TEXT NOT NULL,event_id TEXT NOT NULL,attempt INTEGER NOT NULL,subscription_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,grant_epoch TEXT NOT NULL,intent_hash TEXT NOT NULL,body_hash TEXT NOT NULL,
 request_id TEXT NOT NULL,state TEXT NOT NULL,receipt_json TEXT,created_at INTEGER NOT NULL,
 PRIMARY KEY(profile_id,event_id,attempt),UNIQUE(request_id)
);
