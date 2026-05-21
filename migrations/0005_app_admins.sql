-- 0005_app_admins.sql

CREATE TABLE IF NOT EXISTS app_admins (
    user_id TEXT PRIMARY KEY,
    granted_by_user_id TEXT NULL,
    granted_at_utc TEXT NOT NULL,
    revoked_by_user_id TEXT NULL,
    revoked_at_utc TEXT NULL,
    note TEXT NULL
);

CREATE INDEX IF NOT EXISTS idx_app_admins_active
    ON app_admins (revoked_at_utc);
