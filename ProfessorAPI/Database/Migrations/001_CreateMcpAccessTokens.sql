CREATE TABLE IF NOT EXISTS mcp_access_tokens (
    token_hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    professor_id VARCHAR(255) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    expires_at DATETIME NOT NULL,
    PRIMARY KEY (token_hash),
    INDEX idx_mcp_access_tokens_professor_id (professor_id),
    INDEX idx_mcp_access_tokens_expires_at (expires_at)
);
