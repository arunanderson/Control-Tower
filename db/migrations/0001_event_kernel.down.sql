BEGIN;

DO $$
BEGIN
    IF current_database() !~ '^ct_p1_t07_[0-9a-f]+$'
       OR current_setting(
            'control_tower.ephemeral_migration_guard',
            true) IS DISTINCT FROM 'P1-T07-EPHEMERAL-ONLY'
    THEN
        RAISE EXCEPTION
            '0001 rollback is restricted to a guarded P1-T07 ephemeral database'
            USING ERRCODE = '42501';
    END IF;
END;
$$;

DROP SCHEMA event_store CASCADE;

DO $$
BEGIN
    EXECUTE format(
        'GRANT TEMPORARY ON DATABASE %I TO PUBLIC',
        current_database());
END;
$$;

COMMIT;
