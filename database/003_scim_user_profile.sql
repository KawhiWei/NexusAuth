-- SCIM 2.0 core User profile attributes.
-- Safe to run against an existing NexusAuth database.

BEGIN;

ALTER TABLE nexusauth.users
    ADD COLUMN IF NOT EXISTS external_id varchar(256),
    ADD COLUMN IF NOT EXISTS given_name varchar(100),
    ADD COLUMN IF NOT EXISTS family_name varchar(100),
    ADD COLUMN IF NOT EXISTS middle_name varchar(100),
    ADD COLUMN IF NOT EXISTS honorific_prefix varchar(50),
    ADD COLUMN IF NOT EXISTS honorific_suffix varchar(50),
    ADD COLUMN IF NOT EXISTS profile_url varchar(2048),
    ADD COLUMN IF NOT EXISTS title varchar(256),
    ADD COLUMN IF NOT EXISTS user_type varchar(100),
    ADD COLUMN IF NOT EXISTS preferred_language varchar(35),
    ADD COLUMN IF NOT EXISTS locale varchar(35),
    ADD COLUMN IF NOT EXISTS timezone varchar(100);

CREATE UNIQUE INDEX IF NOT EXISTS ix_users_external_id
    ON nexusauth.users (external_id)
    WHERE external_id IS NOT NULL;

COMMIT;
