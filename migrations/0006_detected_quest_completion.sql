-- Add idempotency keys for automatic recently completed quest detection.

ALTER TABLE party_recently_completed_quests ADD COLUMN detection_key TEXT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS idx_party_recently_completed_detection
    ON party_recently_completed_quests (party_id, detection_key)
    WHERE detection_key IS NOT NULL;
