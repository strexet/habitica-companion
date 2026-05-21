-- 0004_party_sync_management.sql

CREATE TABLE IF NOT EXISTS party_sync_roles (
    party_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    display_name TEXT NOT NULL,
    role TEXT NOT NULL,
    assigned_by_user_id TEXT NOT NULL,
    assigned_by_display_name TEXT NULL,
    assigned_at_utc TEXT NOT NULL,
    revoked_by_user_id TEXT NULL,
    revoked_by_display_name TEXT NULL,
    revoked_at_utc TEXT NULL,
    PRIMARY KEY (party_id, user_id, role, assigned_at_utc)
);

CREATE INDEX IF NOT EXISTS idx_party_sync_roles_active
    ON party_sync_roles (party_id, role, revoked_at_utc);

CREATE TABLE IF NOT EXISTS party_sync_kicks (
    party_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    display_name TEXT NOT NULL,
    kicked_by_user_id TEXT NOT NULL,
    kicked_by_display_name TEXT NULL,
    kicked_at_utc TEXT NOT NULL,
    reason TEXT NULL,
    revoked_by_user_id TEXT NULL,
    revoked_by_display_name TEXT NULL,
    revoked_at_utc TEXT NULL,
    PRIMARY KEY (party_id, user_id, kicked_at_utc)
);

CREATE INDEX IF NOT EXISTS idx_party_sync_kicks_active
    ON party_sync_kicks (party_id, user_id, revoked_at_utc);

CREATE TABLE IF NOT EXISTS party_sync_settings (
    party_id TEXT PRIMARY KEY,
    officer_can_manage_queue INTEGER NOT NULL DEFAULT 1,
    officer_can_moderate_members INTEGER NOT NULL DEFAULT 1,
    officer_only_queue_edits INTEGER NOT NULL DEFAULT 0,
    member_auto_reconcile_enabled INTEGER NOT NULL DEFAULT 1,
    updated_by_user_id TEXT NULL,
    updated_by_display_name TEXT NULL,
    updated_at_utc TEXT NOT NULL
);
