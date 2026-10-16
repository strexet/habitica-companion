ALTER TABLE integration_profiles ADD COLUMN grant_epoch TEXT NOT NULL DEFAULT 'legacy';
ALTER TABLE consent_links ADD COLUMN grant_epoch TEXT NOT NULL DEFAULT 'legacy';
ALTER TABLE linking_tickets ADD COLUMN grant_epoch TEXT NOT NULL DEFAULT 'legacy';
