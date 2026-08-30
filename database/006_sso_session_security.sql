-- ============================================================
-- SSO session security migration
-- 1) token_invalid_before invalidates tokens issued before a user security event.
-- 2) sso_sessions supports server-side SSO session revocation and expiry.
-- ============================================================

BEGIN;

ALTER TABLE nexusauth.users
    ADD COLUMN IF NOT EXISTS token_invalid_before timestamptz;

CREATE TABLE IF NOT EXISTS nexusauth.sso_sessions (
    id              uuid            NOT NULL,
    user_id         uuid            NOT NULL,
    created_at      timestamptz     NOT NULL,
    expires_at      timestamptz     NOT NULL,
    revoked_at      timestamptz,
    CONSTRAINT pk_sso_sessions PRIMARY KEY (id),
    CONSTRAINT ck_sso_sessions_expiry_after_creation
        CHECK (expires_at > created_at)
);

CREATE INDEX IF NOT EXISTS ix_sso_sessions_active_user_expiry
    ON nexusauth.sso_sessions (user_id, expires_at DESC)
    WHERE revoked_at IS NULL;

COMMIT;
