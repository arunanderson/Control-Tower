DO $$
DECLARE
    normal_role text := 'control_tower_runtime';
    privileged_role text :=
        'control_tower_privileged_runtime';
    tenant_table text;
BEGIN
    IF to_regnamespace('event_store') IS NULL
       OR to_regclass(
            'event_store.domain_events') IS NULL
    THEN
        RAISE EXCEPTION
            'migration 0001 event kernel is missing';
    END IF;

    IF to_regnamespace('trust_store') IS NULL
       OR to_regclass(
            'trust_store.role_assignments') IS NULL
       OR to_regclass(
            'trust_store.person_key_map') IS NULL
       OR to_regclass(
            'trust_store.person_key_index_authority') IS NULL
    THEN
        RAISE EXCEPTION
            'one or more C8 durable objects are missing';
    END IF;

    FOREACH tenant_table IN ARRAY ARRAY[
        'role_assignments',
        'person_key_map',
        'person_key_index_authority'
    ]
    LOOP
        IF NOT EXISTS (
            SELECT 1
            FROM pg_class AS relation
            INNER JOIN pg_namespace AS namespace
                ON namespace.oid =
                    relation.relnamespace
            INNER JOIN pg_roles AS owner
                ON owner.oid = relation.relowner
            WHERE namespace.nspname = 'trust_store'
              AND relation.relname = tenant_table
              AND relation.relkind = 'r'
              AND relation.relrowsecurity
              AND relation.relforcerowsecurity
              AND owner.rolname = current_user)
        THEN
            RAISE EXCEPTION
                'ownership or forced RLS is invalid on trust_store.%',
                tenant_table;
        END IF;
    END LOOP;

    IF (
        SELECT count(*)
        FROM pg_policies
        WHERE schemaname = 'trust_store'
          AND tablename IN (
              'role_assignments',
              'person_key_map',
              'person_key_index_authority')) <> 8
       OR EXISTS (
            SELECT expected.name
            FROM (
                VALUES
                    ('role_assignments_select_tenant'),
                    ('role_assignments_insert_tenant'),
                    ('role_assignments_update_tenant'),
                    ('person_key_map_select_tenant'),
                    ('person_key_map_insert_tenant'),
                    ('person_key_map_update_tenant'),
                    ('person_key_index_authority_select_tenant'),
                    ('person_key_index_authority_insert_tenant')
            ) AS expected(name)
            EXCEPT
            SELECT policyname
            FROM pg_policies
            WHERE schemaname = 'trust_store')
       OR EXISTS (
            SELECT 1
            FROM pg_policies
            WHERE schemaname = 'trust_store'
              AND (
                  permissive <> 'PERMISSIVE'
                  OR roles <> ARRAY['public']::name[]))
    THEN
        RAISE EXCEPTION
            'the C8 RLS policy set is incomplete';
    END IF;

    IF (
        SELECT count(*)
        FROM pg_constraint AS constraint_record
        INNER JOIN pg_class AS relation
            ON relation.oid =
                constraint_record.conrelid
        INNER JOIN pg_namespace AS namespace
            ON namespace.oid =
                relation.relnamespace
        WHERE namespace.nspname = 'trust_store'
          AND relation.relname =
                'person_key_index_authority') <> 4
       OR EXISTS (
            SELECT expected.name
            FROM (
                VALUES
                    ('pk_person_key_index_authority'),
                    ('ck_person_key_index_authority_tenant'),
                    ('ck_person_key_index_authority_reference'),
                    ('ck_person_key_index_authority_commitment')
            ) AS expected(name)
            EXCEPT
            SELECT constraint_record.conname::text
            FROM pg_constraint AS constraint_record
            INNER JOIN pg_class AS relation
                ON relation.oid =
                    constraint_record.conrelid
            INNER JOIN pg_namespace AS namespace
                ON namespace.oid =
                    relation.relnamespace
            WHERE namespace.nspname = 'trust_store'
              AND relation.relname =
                    'person_key_index_authority')
    THEN
        RAISE EXCEPTION
            'person-key index authority constraints are incomplete';
    END IF;

    IF (
        SELECT count(*)
        FROM pg_constraint AS constraint_record
        INNER JOIN pg_class AS relation
            ON relation.oid =
                constraint_record.conrelid
        INNER JOIN pg_namespace AS namespace
            ON namespace.oid =
                relation.relnamespace
        WHERE namespace.nspname = 'trust_store'
          AND relation.relname =
                'person_key_map'
          AND constraint_record.contype <> 't') <> 14
       OR EXISTS (
            SELECT expected.name
            FROM (
                VALUES
                    ('pk_person_key_map'),
                    ('uq_person_key_map_last_event'),
                    ('uq_person_key_map_lookup'),
                    ('ck_person_key_map_person_key'),
                    ('ck_person_key_map_tenant'),
                    ('ck_person_key_map_last_event'),
                    ('ck_person_key_map_protection_format'),
                    ('ck_person_key_map_encryption_reference'),
                    ('ck_person_key_map_index_reference'),
                    ('ck_person_key_map_blind_index'),
                    ('ck_person_key_map_ciphertext'),
                    ('ck_person_key_map_nonce'),
                    ('ck_person_key_map_tag'),
                    ('ck_person_key_map_state')
            ) AS expected(name)
            EXCEPT
            SELECT constraint_record.conname::text
            FROM pg_constraint AS constraint_record
            INNER JOIN pg_class AS relation
                ON relation.oid =
                    constraint_record.conrelid
            INNER JOIN pg_namespace AS namespace
                ON namespace.oid =
                    relation.relnamespace
            WHERE namespace.nspname = 'trust_store'
              AND relation.relname =
                    'person_key_map')
    THEN
        RAISE EXCEPTION
            'person-key constraints are incomplete';
    END IF;

    IF (
        SELECT count(*)
        FROM pg_constraint AS constraint_record
        INNER JOIN pg_class AS relation
            ON relation.oid =
                constraint_record.conrelid
        INNER JOIN pg_namespace AS namespace
            ON namespace.oid =
                relation.relnamespace
        WHERE namespace.nspname = 'trust_store'
          AND relation.relname =
                'role_assignments'
          AND constraint_record.contype <> 't') <> 21
       OR EXISTS (
            SELECT expected.name
            FROM (
                VALUES
                    ('pk_role_assignments'),
                    ('uq_role_assignments_last_event'),
                    ('uq_role_assignments_active'),
                    ('ck_role_assignments_assignment_id'),
                    ('ck_role_assignments_tenant'),
                    ('ck_role_assignments_subject'),
                    ('ck_role_assignments_role'),
                    ('ck_role_assignments_scope'),
                    ('ck_role_assignments_assigned_actor_kind'),
                    ('ck_role_assignments_assigned_actor_shape'),
                    ('ck_role_assignments_assigned_system_actor'),
                    ('ck_role_assignments_assigned_provider_actor'),
                    ('ck_role_assignments_assigned_at'),
                    ('ck_role_assignments_revoked_actor_kind'),
                    ('ck_role_assignments_revoked_actor_shape'),
                    ('ck_role_assignments_revoked_system_actor'),
                    ('ck_role_assignments_revoked_provider_actor'),
                    ('ck_role_assignments_revoked_at'),
                    ('ck_role_assignments_active_slot'),
                    ('ck_role_assignments_state'),
                    ('ck_role_assignments_last_event')
            ) AS expected(name)
            EXCEPT
            SELECT constraint_record.conname::text
            FROM pg_constraint AS constraint_record
            INNER JOIN pg_class AS relation
                ON relation.oid =
                    constraint_record.conrelid
            INNER JOIN pg_namespace AS namespace
                ON namespace.oid =
                    relation.relnamespace
            WHERE namespace.nspname = 'trust_store'
              AND relation.relname =
                    'role_assignments')
    THEN
        RAISE EXCEPTION
            'role-assignment constraints are incomplete';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_class AS index_relation
        INNER JOIN pg_namespace AS namespace
            ON namespace.oid =
                index_relation.relnamespace
        INNER JOIN pg_index AS index_record
            ON index_record.indexrelid =
                index_relation.oid
        WHERE namespace.nspname = 'trust_store'
          AND index_relation.relname =
                'ix_role_assignments_subject_history'
          AND index_record.indisvalid
          AND index_record.indisready
          AND NOT index_record.indisunique
          AND NOT index_record.indisprimary
          AND pg_get_indexdef(index_relation.oid) =
                'CREATE INDEX ix_role_assignments_subject_history ON trust_store.role_assignments USING btree (tenant_id, subject_person_key, assigned_at, assignment_id)')
    THEN
        RAISE EXCEPTION
            'role-assignment history index is missing';
    END IF;

    IF (
        SELECT count(*)
        FROM pg_trigger AS trigger
        INNER JOIN pg_class AS relation
            ON relation.oid = trigger.tgrelid
        INNER JOIN pg_namespace AS namespace
            ON namespace.oid =
                relation.relnamespace
        WHERE namespace.nspname = 'trust_store'
          AND relation.relname IN (
              'role_assignments',
              'person_key_map')
          AND NOT trigger.tgisinternal
          AND trigger.tgenabled = 'O'
          AND trigger.tgdeferrable
          AND trigger.tginitdeferred) <> 2
    THEN
        RAISE EXCEPTION
            'deferred C8 event guards are incomplete';
    END IF;

    IF (
        SELECT count(*)
        FROM pg_proc AS procedure
        INNER JOIN pg_namespace AS namespace
            ON namespace.oid =
                procedure.pronamespace
        INNER JOIN pg_roles AS owner
            ON owner.oid = procedure.proowner
        WHERE namespace.nspname = 'trust_store'
          AND procedure.proname IN (
              'guard_tenant',
              'read_role_assignment',
              'lock_role_assignment',
              'list_role_assignments',
              'lock_active_role_assignment',
              'insert_role_assignment',
              'revoke_role_assignment',
              'lock_person_key_creation',
              'find_person_key',
              'read_person_key',
              'insert_person_key',
              'sever_person_key',
              'require_role_assignment_event',
              'require_person_key_event')
          AND procedure.prosecdef
          AND procedure.proconfig @>
              ARRAY[
                  'search_path=pg_catalog, trust_store, event_store']
          AND owner.rolname = current_user) <> 14
    THEN
        RAISE EXCEPTION
            'C8 security-definer functions are incomplete';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_proc AS procedure
        INNER JOIN pg_namespace AS namespace
            ON namespace.oid =
                procedure.pronamespace
        CROSS JOIN LATERAL aclexplode(
            COALESCE(
                procedure.proacl,
                acldefault(
                    'f',
                    procedure.proowner))) AS access
        WHERE namespace.nspname = 'trust_store'
          AND access.grantee = 0
          AND access.privilege_type = 'EXECUTE')
    THEN
        RAISE EXCEPTION
            'PUBLIC can execute C8 functions';
    END IF;

    IF NOT has_schema_privilege(
            normal_role,
            'trust_store',
            'USAGE')
       OR has_schema_privilege(
            normal_role,
            'trust_store',
            'CREATE')
       OR NOT has_schema_privilege(
            privileged_role,
            'trust_store',
            'USAGE')
       OR has_schema_privilege(
            privileged_role,
            'trust_store',
            'CREATE')
    THEN
        RAISE EXCEPTION
            'C8 schema privileges are invalid';
    END IF;

    IF has_table_privilege(
            normal_role,
            'trust_store.role_assignments',
            'SELECT,INSERT,UPDATE,DELETE,TRUNCATE')
       OR has_table_privilege(
            normal_role,
            'trust_store.person_key_map',
            'SELECT,INSERT,UPDATE,DELETE,TRUNCATE')
       OR has_table_privilege(
            normal_role,
            'trust_store.person_key_index_authority',
            'SELECT,INSERT,UPDATE,DELETE,TRUNCATE')
       OR has_table_privilege(
            privileged_role,
            'trust_store.role_assignments',
            'SELECT,INSERT,UPDATE,DELETE,TRUNCATE')
       OR has_table_privilege(
            privileged_role,
            'trust_store.person_key_map',
            'SELECT,INSERT,UPDATE,DELETE,TRUNCATE')
       OR has_table_privilege(
            privileged_role,
            'trust_store.person_key_index_authority',
            'SELECT,INSERT,UPDATE,DELETE,TRUNCATE')
       OR has_table_privilege(
            privileged_role,
            'event_store.domain_events',
            'SELECT,INSERT,UPDATE,DELETE,TRUNCATE')
    THEN
        RAISE EXCEPTION
            'runtime table privileges are too broad';
    END IF;

    IF NOT has_function_privilege(
            normal_role,
            'trust_store.read_role_assignment(uuid,uuid)',
            'EXECUTE')
       OR NOT has_function_privilege(
            normal_role,
            'trust_store.lock_role_assignment(uuid,uuid)',
            'EXECUTE')
       OR NOT has_function_privilege(
            normal_role,
            'trust_store.list_role_assignments(uuid,uuid)',
            'EXECUTE')
       OR NOT has_function_privilege(
            normal_role,
            'trust_store.lock_active_role_assignment(uuid,uuid,smallint)',
            'EXECUTE')
       OR NOT has_function_privilege(
            normal_role,
            'trust_store.insert_role_assignment(uuid,uuid,uuid,smallint,smallint,smallint,uuid,character varying,timestamp with time zone,uuid)',
            'EXECUTE')
       OR NOT has_function_privilege(
            normal_role,
            'trust_store.revoke_role_assignment(uuid,uuid,bigint,timestamp with time zone,smallint,uuid,character varying,uuid)',
            'EXECUTE')
    THEN
        RAISE EXCEPTION
            'normal runtime E18 privileges are incomplete';
    END IF;

    IF has_function_privilege(
            normal_role,
            'trust_store.lock_person_key_creation(uuid,character varying,bytea)',
            'EXECUTE')
       OR has_function_privilege(
            normal_role,
            'trust_store.find_person_key(uuid,character varying,bytea,bytea)',
            'EXECUTE')
       OR has_function_privilege(
            normal_role,
            'trust_store.read_person_key(uuid,uuid)',
            'EXECUTE')
       OR has_function_privilege(
            normal_role,
            'trust_store.insert_person_key(uuid,uuid,smallint,character varying,character varying,bytea,bytea,bytea,bytea,bytea,uuid)',
            'EXECUTE')
       OR has_function_privilege(
            normal_role,
            'trust_store.sever_person_key(uuid,uuid,bigint,uuid)',
            'EXECUTE')
    THEN
        RAISE EXCEPTION
            'normal runtime can enter the E19 perimeter';
    END IF;

    IF NOT has_function_privilege(
            privileged_role,
            'trust_store.lock_person_key_creation(uuid,character varying,bytea)',
            'EXECUTE')
       OR NOT has_function_privilege(
            privileged_role,
            'trust_store.find_person_key(uuid,character varying,bytea,bytea)',
            'EXECUTE')
       OR NOT has_function_privilege(
            privileged_role,
            'trust_store.read_person_key(uuid,uuid)',
            'EXECUTE')
       OR NOT has_function_privilege(
            privileged_role,
            'trust_store.insert_person_key(uuid,uuid,smallint,character varying,character varying,bytea,bytea,bytea,bytea,bytea,uuid)',
            'EXECUTE')
       OR NOT has_function_privilege(
            privileged_role,
            'trust_store.sever_person_key(uuid,uuid,bigint,uuid)',
            'EXECUTE')
    THEN
        RAISE EXCEPTION
            'privileged runtime E19 privileges are incomplete';
    END IF;

    IF has_function_privilege(
            privileged_role,
            'trust_store.read_role_assignment(uuid,uuid)',
            'EXECUTE')
       OR has_function_privilege(
            privileged_role,
            'trust_store.lock_role_assignment(uuid,uuid)',
            'EXECUTE')
       OR has_function_privilege(
            privileged_role,
            'trust_store.list_role_assignments(uuid,uuid)',
            'EXECUTE')
       OR has_function_privilege(
            privileged_role,
            'trust_store.lock_active_role_assignment(uuid,uuid,smallint)',
            'EXECUTE')
       OR has_function_privilege(
            privileged_role,
            'trust_store.insert_role_assignment(uuid,uuid,uuid,smallint,smallint,smallint,uuid,character varying,timestamp with time zone,uuid)',
            'EXECUTE')
       OR has_function_privilege(
            privileged_role,
            'trust_store.revoke_role_assignment(uuid,uuid,bigint,timestamp with time zone,smallint,uuid,character varying,uuid)',
            'EXECUTE')
    THEN
        RAISE EXCEPTION
            'privileged runtime has E18 authority';
    END IF;

    IF NOT has_schema_privilege(
            privileged_role,
            'event_store',
            'USAGE')
       OR has_schema_privilege(
            privileged_role,
            'event_store',
            'CREATE')
       OR NOT has_function_privilege(
            privileged_role,
            'event_store.current_tenant_id()',
            'EXECUTE')
       OR NOT has_function_privilege(
            privileged_role,
            'event_store.lock_stream_head(uuid)',
            'EXECUTE')
       OR NOT has_function_privilege(
            privileged_role,
            'event_store.append_event(integer,uuid,bigint,uuid,character varying,character varying,character varying,smallint,character varying,timestamp with time zone,timestamp with time zone,character varying,character varying,character varying,smallint,character varying,character varying,bytea)',
            'EXECUTE')
    THEN
        RAISE EXCEPTION
            'privileged event-append privileges are invalid';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_roles
        WHERE rolname IN (
            normal_role,
            privileged_role)
          AND (
              rolsuper
              OR rolbypassrls
              OR rolcreaterole
              OR rolcreatedb
              OR rolreplication))
    THEN
        RAISE EXCEPTION
            'a runtime role has forbidden cluster privileges';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_auth_members AS membership
        INNER JOIN pg_roles AS member_role
            ON member_role.oid = membership.member
        WHERE member_role.rolname IN (
            normal_role,
            privileged_role))
    THEN
        RAISE EXCEPTION
            'a runtime role has forbidden role memberships';
    END IF;

    IF NOT has_database_privilege(
            normal_role,
            current_database(),
            'CONNECT')
       OR has_database_privilege(
            normal_role,
            current_database(),
            'CREATE')
       OR has_database_privilege(
            normal_role,
            current_database(),
            'TEMPORARY')
       OR NOT has_database_privilege(
            privileged_role,
            current_database(),
            'CONNECT')
       OR has_database_privilege(
            privileged_role,
            current_database(),
            'CREATE')
       OR has_database_privilege(
            privileged_role,
            current_database(),
            'TEMPORARY')
    THEN
        RAISE EXCEPTION
            'runtime database privileges are invalid';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_extension
        WHERE extname = 'pgcrypto')
    THEN
        RAISE EXCEPTION
            'database-native identity crypto is forbidden';
    END IF;
END;
$$;
