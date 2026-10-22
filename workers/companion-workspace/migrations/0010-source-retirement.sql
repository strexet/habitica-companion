CREATE TABLE workspace_source_retirements (
 profile_id TEXT NOT NULL REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 command_id TEXT NOT NULL,
 request_id TEXT NOT NULL,
 input_hash TEXT NOT NULL,
 connection_id TEXT NOT NULL,
 access_epoch TEXT NOT NULL,
 grant_epoch TEXT NOT NULL,
 authority_json TEXT NOT NULL,
 plan_json TEXT NOT NULL,
 verification_json TEXT NOT NULL,
 state TEXT NOT NULL,
 prepared_request_json TEXT,
 native_outcome_json TEXT,
 receipt_json TEXT,
 error_code TEXT,
 created_at_utc TEXT NOT NULL,
 expires_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,command_id),
 UNIQUE(profile_id,request_id)
);
ALTER TABLE workspaces ADD COLUMN retirement_fence TEXT;
CREATE TRIGGER workspace_retirement_fence BEFORE UPDATE OF document_json ON workspaces
 WHEN OLD.retirement_fence IS NOT NULL AND NEW.document_json<>OLD.document_json AND NEW.commit_tag<>OLD.retirement_fence
 BEGIN SELECT RAISE(ABORT,'source_retirement_pending'); END;
