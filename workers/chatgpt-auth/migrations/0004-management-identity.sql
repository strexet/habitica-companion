CREATE UNIQUE INDEX integration_management_token ON integration_profiles(management_token_hash) WHERE management_token_hash IS NOT NULL;
