ALTER TABLE mcp_access_tokens
    ADD COLUMN access_level VARCHAR(10) NOT NULL DEFAULT 'read'
    AFTER token_hash;
