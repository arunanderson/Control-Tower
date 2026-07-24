BEGIN;

DO $$
BEGIN
    IF current_database() !~ '^ct_p1_t08_[0-9a-f]+$'
       OR current_setting(
            'control_tower.ephemeral_migration_guard',
            true) IS DISTINCT FROM
                'P1-T08-EPHEMERAL-ONLY'
    THEN
        RAISE EXCEPTION
            '0002 rollback is restricted to a guarded P1-T08 ephemeral database'
            USING ERRCODE = '42501';
    END IF;
END;
$$;

REVOKE EXECUTE ON FUNCTION event_store.append_event(
    integer,
    uuid,
    bigint,
    uuid,
    varchar,
    varchar,
    varchar,
    smallint,
    varchar,
    timestamptz,
    timestamptz,
    varchar,
    varchar,
    varchar,
    smallint,
    varchar,
    varchar,
    bytea)
    FROM control_tower_privileged_runtime;
REVOKE EXECUTE ON FUNCTION
    event_store.lock_stream_head(uuid)
    FROM control_tower_privileged_runtime;
REVOKE EXECUTE ON FUNCTION
    event_store.current_tenant_id()
    FROM control_tower_privileged_runtime;
REVOKE USAGE ON SCHEMA event_store
    FROM control_tower_privileged_runtime;

DROP SCHEMA trust_store CASCADE;

COMMIT;
