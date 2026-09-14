-- Compatibility entry point for the isolated WebAuthn deployment.
-- WebAuthn tables now belong to the canonical fresh-database schema.
\ir production-init.sql
