CREATE TABLE upstream_pacing (
 bucket TEXT PRIMARY KEY,
 profile_id TEXT REFERENCES integration_profiles(profile_id) ON DELETE CASCADE,
 version INTEGER NOT NULL DEFAULT 1,
 capacity REAL NOT NULL CHECK(capacity > 0),
 tokens REAL NOT NULL CHECK(tokens >= 0),
 updated_ms REAL NOT NULL,
 next_ms REAL NOT NULL DEFAULT 0,
 reset_ms REAL NOT NULL DEFAULT 0,
 cooldown_ms REAL NOT NULL DEFAULT 0,
 recovery REAL NOT NULL DEFAULT 1 CHECK(recovery >= 1 AND recovery <= 16),
 reservation TEXT,
 expires_ms REAL NOT NULL
);
CREATE INDEX upstream_pacing_expiry ON upstream_pacing(expires_ms);
