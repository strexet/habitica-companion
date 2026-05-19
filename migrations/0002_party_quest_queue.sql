ALTER TABLE party_quest_queue ADD COLUMN quest_name TEXT NULL;
ALTER TABLE party_quest_queue ADD COLUMN owner_user_id TEXT NULL;
ALTER TABLE party_quest_queue ADD COLUMN owner_display_name TEXT NULL;
ALTER TABLE party_quest_queue ADD COLUMN status TEXT NOT NULL DEFAULT 'Queued';
ALTER TABLE party_quest_queue ADD COLUMN updated_at_utc TEXT NULL;
ALTER TABLE party_quest_queue ADD COLUMN selected_at_utc TEXT NULL;
ALTER TABLE party_quest_queue ADD COLUMN started_at_utc TEXT NULL;
ALTER TABLE party_quest_queue ADD COLUMN completed_at_utc TEXT NULL;
ALTER TABLE party_quest_queue ADD COLUMN manual_pin_rank INTEGER NULL;
ALTER TABLE party_quest_queue ADD COLUMN owner_ready INTEGER NOT NULL DEFAULT 0;
ALTER TABLE party_quest_queue ADD COLUMN version INTEGER NOT NULL DEFAULT 1;
ALTER TABLE party_quest_queue ADD COLUMN reward_summary_json TEXT NULL;

UPDATE party_quest_queue
SET owner_user_id = created_by_user_id
WHERE owner_user_id IS NULL;

UPDATE party_quest_queue
SET updated_at_utc = created_at_utc
WHERE updated_at_utc IS NULL;

CREATE INDEX IF NOT EXISTS idx_party_quest_queue_party_status
    ON party_quest_queue (party_id, status, sort_order);

ALTER TABLE party_quest_votes ADD COLUMN voter_display_name TEXT NULL;
ALTER TABLE party_quest_votes ADD COLUMN vote_weight INTEGER NOT NULL DEFAULT 1;
ALTER TABLE party_quest_votes ADD COLUMN updated_at_utc TEXT NULL;

CREATE TABLE IF NOT EXISTS party_recently_completed_quests (
    party_id TEXT NOT NULL,
    quest_key TEXT NOT NULL,
    quest_name TEXT NULL,
    completed_at_utc TEXT NOT NULL,
    started_at_utc TEXT NULL,
    owner_user_id TEXT NULL,
    owner_display_name TEXT NULL,
    participants_count INTEGER NULL,
    reward_summary_json TEXT NULL,
    source_queue_item_id TEXT NULL,
    PRIMARY KEY (party_id, quest_key, completed_at_utc)
);

CREATE INDEX IF NOT EXISTS idx_party_recently_completed_party_completed
    ON party_recently_completed_quests (party_id, completed_at_utc DESC);

CREATE TABLE IF NOT EXISTS party_quest_pool_entries (
    party_id TEXT NOT NULL,
    quest_key TEXT NOT NULL,
    quest_name TEXT NULL,
    owner_user_id TEXT NOT NULL,
    owner_display_name TEXT NULL,
    quest_type TEXT NULL,
    reward_summary_json TEXT NULL,
    available_count INTEGER NOT NULL DEFAULT 1,
    last_seen_at_utc TEXT NOT NULL,
    PRIMARY KEY (party_id, quest_key, owner_user_id)
);

CREATE INDEX IF NOT EXISTS idx_party_quest_pool_party_quest
    ON party_quest_pool_entries (party_id, quest_key);
