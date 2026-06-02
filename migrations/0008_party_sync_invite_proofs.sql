-- 0008_party_sync_invite_proofs.sql

-- Adds optional hashed invite proofs for shared party-sync access.

ALTER TABLE party_sync_settings ADD COLUMN tokenized_invite_proof_mode_enabled INTEGER NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS party_sync_invite_proofs (
    party_id TEXT NOT NULL,
    proof_id TEXT NOT NULL,
    token_hash TEXT NOT NULL,
    display_label TEXT NOT NULL,
    issued_by_user_id TEXT NOT NULL,
    issued_by_display_name TEXT NULL,
    issued_at_utc TEXT NOT NULL,
    expires_at_utc TEXT NULL,
    revoked_by_user_id TEXT NULL,
    revoked_by_display_name TEXT NULL,
    revoked_at_utc TEXT NULL,
    removed_by_user_id TEXT NULL,
    removed_by_display_name TEXT NULL,
    removed_at_utc TEXT NULL,
    PRIMARY KEY (party_id, proof_id)
);

CREATE INDEX IF NOT EXISTS idx_party_sync_invite_proofs_active
    ON party_sync_invite_proofs (party_id, revoked_at_utc, removed_at_utc, expires_at_utc);
