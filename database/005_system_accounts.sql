ALTER TABLE nexusauth.users
    ADD COLUMN IF NOT EXISTS is_system_account boolean NOT NULL DEFAULT false;

UPDATE nexusauth.users
SET is_system_account = true
WHERE username = 'admin';
