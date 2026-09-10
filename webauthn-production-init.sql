-- This bootstrap is intentionally executed only against the isolated
-- WebAuthnNexusAuth database. It reuses the current base schema definition,
-- then adds the WebAuthn-specific storage owned by this deployment.
\ir production-init.sql

CREATE TABLE nexusauth.webauthn_credentials (
    id uuid NOT NULL,
    user_id uuid NOT NULL REFERENCES nexusauth.users(id) ON DELETE CASCADE,
    credential_id bytea NOT NULL,
    public_key_cose bytea NOT NULL,
    signature_counter bigint NOT NULL DEFAULT 0,
    aaguid uuid NOT NULL,
    transports jsonb NOT NULL DEFAULT '[]'::jsonb,
    is_backup_eligible boolean NOT NULL,
    is_backed_up boolean NOT NULL,
    display_name varchar(128) NOT NULL,
    created_at timestamptz NOT NULL,
    last_used_at timestamptz,
    disabled_at timestamptz,
    CONSTRAINT pk_webauthn_credentials PRIMARY KEY (id),
    CONSTRAINT ux_webauthn_credentials_credential_id UNIQUE (credential_id)
);

CREATE INDEX ix_webauthn_credentials_user_enabled
    ON nexusauth.webauthn_credentials (user_id, disabled_at);

CREATE TABLE nexusauth.webauthn_challenges (
    id uuid NOT NULL,
    token_hash varchar(64) NOT NULL,
    purpose varchar(32) NOT NULL,
    user_id uuid REFERENCES nexusauth.users(id) ON DELETE CASCADE,
    options_json jsonb NOT NULL,
    return_url text,
    remember_me boolean NOT NULL DEFAULT false,
    expires_at timestamptz NOT NULL,
    consumed_at timestamptz,
    created_at timestamptz NOT NULL,
    CONSTRAINT pk_webauthn_challenges PRIMARY KEY (id),
    CONSTRAINT ux_webauthn_challenges_token_hash UNIQUE (token_hash),
    CONSTRAINT ck_webauthn_challenges_purpose CHECK (purpose IN ('registration', 'authentication'))
);

CREATE INDEX ix_webauthn_challenges_purpose_expiry
    ON nexusauth.webauthn_challenges (purpose, expires_at);
