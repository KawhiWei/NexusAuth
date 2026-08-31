-- Lightweight configurable login flow: TOTP credential storage.
-- Secrets are protected by the Provider's ASP.NET Data Protection key ring
-- before being persisted. Apply this migration once to an existing database.

ALTER TABLE nexusauth.users
    ADD COLUMN IF NOT EXISTS totp_secret_protected text,
    ADD COLUMN IF NOT EXISTS totp_pending_secret_protected text,
    ADD COLUMN IF NOT EXISTS totp_pending_expires_at timestamptz,
    ADD COLUMN IF NOT EXISTS totp_enabled boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS totp_last_used_counter bigint;

ALTER TABLE nexusauth.users
    DROP CONSTRAINT IF EXISTS ck_users_totp_state;

ALTER TABLE nexusauth.users
    ADD CONSTRAINT ck_users_totp_state CHECK (
        (totp_enabled = false)
        OR (totp_secret_protected IS NOT NULL)
    );
