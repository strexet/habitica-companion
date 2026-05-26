-- 0007_queue_selection_controls.sql

-- Adds deterministic selection expiry metadata for shared quest queue control.

ALTER TABLE party_quest_queue ADD COLUMN selected_expires_at_utc TEXT NULL;
ALTER TABLE party_quest_queue ADD COLUMN expired_at_utc TEXT NULL;

UPDATE party_quest_queue
SET selected_expires_at_utc = datetime(selected_at_utc, '+72 hours')
WHERE selected_expires_at_utc IS NULL
  AND selected_at_utc IS NOT NULL
  AND status IN ('Selected', 'InviteSent');

UPDATE party_quest_queue
SET expired_at_utc = COALESCE(updated_at_utc, selected_expires_at_utc, created_at_utc)
WHERE expired_at_utc IS NULL
  AND status = 'Expired';

CREATE INDEX IF NOT EXISTS idx_party_quest_queue_selection_expiry
    ON party_quest_queue (party_id, status, selected_expires_at_utc);
