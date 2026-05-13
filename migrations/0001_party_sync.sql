CREATE TABLE IF NOT EXISTS party_state (
    party_id TEXT PRIMARY KEY,
    snapshot_json TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS party_cron_events (
    party_id TEXT NOT NULL,
    member_id TEXT NOT NULL,
    display_name TEXT NOT NULL,
    last_cron_utc TEXT NOT NULL,
    member_habitica_day_key TEXT NULL,
    observed_at_utc TEXT NOT NULL,
    confidence INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (party_id, member_id, last_cron_utc)
);

CREATE INDEX IF NOT EXISTS idx_party_cron_events_party_last_cron
    ON party_cron_events (party_id, last_cron_utc);

CREATE TABLE IF NOT EXISTS party_quest_queue (
    party_id TEXT NOT NULL,
    queue_item_id TEXT PRIMARY KEY,
    quest_key TEXT NOT NULL,
    created_by_user_id TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    sort_order INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_party_quest_queue_party_sort
    ON party_quest_queue (party_id, sort_order);

CREATE TABLE IF NOT EXISTS party_quest_votes (
    party_id TEXT NOT NULL,
    queue_item_id TEXT NOT NULL,
    voter_user_id TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    PRIMARY KEY (party_id, queue_item_id, voter_user_id)
);
