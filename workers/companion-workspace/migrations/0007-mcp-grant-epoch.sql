ALTER TABLE workspaces ADD COLUMN mcp_grant_epoch TEXT;
ALTER TABLE workspace_preparations ADD COLUMN grant_epoch TEXT NOT NULL DEFAULT 'legacy';
ALTER TABLE prepared_spell_actions ADD COLUMN grant_epoch TEXT NOT NULL DEFAULT 'legacy';
ALTER TABLE prepared_cron_actions ADD COLUMN grant_epoch TEXT NOT NULL DEFAULT 'legacy';
