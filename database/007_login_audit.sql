BEGIN;

CREATE TABLE IF NOT EXISTS nexusauth.login_audit_logs (
    id              uuid            NOT NULL,
    user_id         uuid,
    username        varchar(100)    NOT NULL,
    client_id       varchar(128),
    is_successful   boolean         NOT NULL,
    failure_reason  varchar(64),
    ip_address      varchar(45),
    user_agent      varchar(1024),
    occurred_at     timestamptz     NOT NULL,
    CONSTRAINT pk_login_audit_logs PRIMARY KEY (id),
    CONSTRAINT ck_login_audit_logs_failure_reason
        CHECK ((is_successful AND failure_reason IS NULL) OR (NOT is_successful AND failure_reason IS NOT NULL))
);

CREATE INDEX IF NOT EXISTS ix_login_audit_logs_occurred_at
    ON nexusauth.login_audit_logs (occurred_at DESC);
CREATE INDEX IF NOT EXISTS ix_login_audit_logs_user_occurred_at
    ON nexusauth.login_audit_logs (user_id, occurred_at DESC);
CREATE INDEX IF NOT EXISTS ix_login_audit_logs_client_occurred_at
    ON nexusauth.login_audit_logs (client_id, occurred_at DESC);

COMMIT;
