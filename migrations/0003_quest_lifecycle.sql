-- Add auto-reconciliation metadata to recently completed quests

ALTER TABLE party_recently_completed_quests ADD COLUMN completed_by_user_id TEXT NULL;
ALTER TABLE party_recently_completed_quests ADD COLUMN completed_by_display_name TEXT NULL;
ALTER TABLE party_recently_completed_quests ADD COLUMN completion_source TEXT NOT NULL DEFAULT 'manual';

-- Prevent duplicate completion entries from concurrent auto-detection by multiple users.
-- source_queue_item_id is set when a queue entry is completed; only one completion per queue item.
CREATE UNIQUE INDEX IF NOT EXISTS idx_party_recently_completed_source_queue
    ON party_recently_completed_quests (party_id, source_queue_item_id)
    WHERE source_queue_item_id IS NOT NULL;
