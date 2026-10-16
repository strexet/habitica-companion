CREATE TABLE credential_key_jobs (
 job_id TEXT PRIMARY KEY,
 from_version TEXT NOT NULL,
 to_version TEXT NOT NULL,
 last_profile_id TEXT,
 scanned INTEGER NOT NULL DEFAULT 0,
 migrated INTEGER NOT NULL DEFAULT 0,
 skipped INTEGER NOT NULL DEFAULT 0,
 failures INTEGER NOT NULL DEFAULT 0,
 status TEXT NOT NULL CHECK(status IN ('running','blocked','complete')),
 failure_code TEXT,
 lease_token TEXT,
 lease_until_utc TEXT,
 created_at_utc TEXT NOT NULL,
 updated_at_utc TEXT NOT NULL
);
CREATE INDEX credential_key_jobs_retention ON credential_key_jobs(updated_at_utc);
