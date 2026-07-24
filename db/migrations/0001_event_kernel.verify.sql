DO $$
DECLARE
    runtime_role text := 'control_tower_runtime';
    tenant_table text;
BEGIN
    IF to_regnamespace('event_store') IS NULL THEN
        RAISE EXCEPTION 'event_store schema is missing';
    END IF;

    IF to_regclass('event_store.stream_heads') IS NULL
       OR to_regclass('event_store.domain_events') IS NULL
    THEN
        RAISE EXCEPTION 'one or more event-kernel tables are missing';
    END IF;

    FOREACH tenant_table IN ARRAY ARRAY[
        'stream_heads',
        'domain_events'
    ]
    LOOP
        IF NOT EXISTS (
            SELECT 1
            FROM pg_class AS relation
            INNER JOIN pg_namespace AS namespace
                ON namespace.oid = relation.relnamespace
            INNER JOIN pg_roles AS owner
                ON owner.oid = relation.relowner
            WHERE namespace.nspname = 'event_store'
              AND relation.relname = tenant_table
              AND relation.relkind = 'r'
              AND relation.relrowsecurity
              AND relation.relforcerowsecurity
              AND owner.rolname = current_user)
        THEN
            RAISE EXCEPTION
                'ownership or forced RLS is invalid on event_store.%',
                tenant_table;
        END IF;
    END LOOP;

    IF (
        SELECT count(*)
        FROM pg_policies
        WHERE schemaname = 'event_store'
          AND tablename IN (
              'stream_heads',
              'domain_events')) <> 7
    THEN
        RAISE EXCEPTION 'the event-kernel RLS policy set is incomplete';
    END IF;

    IF (
        SELECT count(*)
        FROM pg_trigger AS trigger
        INNER JOIN pg_class AS relation
            ON relation.oid = trigger.tgrelid
        INNER JOIN pg_namespace AS namespace
            ON namespace.oid = relation.relnamespace
        WHERE namespace.nspname = 'event_store'
          AND relation.relname = 'domain_events'
          AND NOT trigger.tgisinternal
          AND trigger.tgenabled = 'O') <> 2
    THEN
        RAISE EXCEPTION 'immutable event triggers are incomplete';
    END IF;

    IF (
        SELECT count(*)
        FROM pg_proc AS procedure
        INNER JOIN pg_namespace AS namespace
            ON namespace.oid = procedure.pronamespace
        INNER JOIN pg_roles AS owner
            ON owner.oid = procedure.proowner
        WHERE namespace.nspname = 'event_store'
          AND procedure.proname IN (
              'lock_stream_head',
              'append_event')
          AND procedure.prosecdef
          AND procedure.proconfig @>
              ARRAY['search_path=pg_catalog, event_store']
          AND owner.rolname = current_user) <> 2
    THEN
        RAISE EXCEPTION
            'security-definer append functions are not fixed-path owner functions';
    END IF;

    IF NOT has_schema_privilege(
            runtime_role,
            'event_store',
            'USAGE')
       OR has_schema_privilege(
            runtime_role,
            'event_store',
            'CREATE')
    THEN
        RAISE EXCEPTION 'runtime schema privileges are invalid';
    END IF;

    IF NOT has_table_privilege(
            runtime_role,
            'event_store.domain_events',
            'SELECT')
       OR has_table_privilege(
            runtime_role,
            'event_store.domain_events',
            'INSERT,UPDATE,DELETE,TRUNCATE')
       OR has_table_privilege(
            runtime_role,
            'event_store.stream_heads',
            'SELECT,INSERT,UPDATE,DELETE,TRUNCATE')
    THEN
        RAISE EXCEPTION 'runtime table privileges are invalid';
    END IF;

    IF NOT has_function_privilege(
            runtime_role,
            'event_store.current_tenant_id()',
            'EXECUTE')
       OR NOT has_function_privilege(
            runtime_role,
            'event_store.lock_stream_head(uuid)',
            'EXECUTE')
       OR NOT has_function_privilege(
            runtime_role,
            'event_store.append_event(integer,uuid,bigint,uuid,character varying,character varying,character varying,smallint,character varying,timestamp with time zone,timestamp with time zone,character varying,character varying,character varying,smallint,character varying,character varying,bytea)',
            'EXECUTE')
    THEN
        RAISE EXCEPTION 'runtime function privileges are incomplete';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_proc AS procedure
        INNER JOIN pg_namespace AS namespace
            ON namespace.oid = procedure.pronamespace
        CROSS JOIN LATERAL aclexplode(
            COALESCE(
                procedure.proacl,
                acldefault('f', procedure.proowner))) AS access
        WHERE namespace.nspname = 'event_store'
          AND procedure.proname IN (
              'current_tenant_id',
              'lock_stream_head',
              'append_event')
          AND access.grantee = 0
          AND access.privilege_type = 'EXECUTE')
    THEN
        RAISE EXCEPTION 'PUBLIC can execute event-kernel functions';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_roles
        WHERE rolname = runtime_role
          AND (
              rolsuper
              OR rolbypassrls
              OR rolcreaterole
              OR rolcreatedb
              OR rolreplication))
    THEN
        RAISE EXCEPTION 'runtime role has forbidden cluster privileges';
    END IF;
END;
$$;
