BEGIN;

CREATE SCHEMA trust_store;

REVOKE ALL ON SCHEMA trust_store FROM PUBLIC;

CREATE TABLE trust_store.person_key_index_authority (
    tenant_id uuid NOT NULL,
    index_reference varchar(24) NOT NULL,
    key_commitment bytea NOT NULL,
    CONSTRAINT pk_person_key_index_authority
        PRIMARY KEY (tenant_id),
    CONSTRAINT ck_person_key_index_authority_tenant
        CHECK (
            tenant_id <>
                '00000000-0000-0000-0000-000000000000'::uuid),
    CONSTRAINT ck_person_key_index_authority_reference
        CHECK (
            index_reference ~
                '^[a-z0-9][a-z0-9-]{0,23}$'),
    CONSTRAINT ck_person_key_index_authority_commitment
        CHECK (octet_length(key_commitment) = 32)
);

CREATE TABLE trust_store.person_key_map (
    person_key uuid NOT NULL,
    tenant_id uuid NOT NULL,
    version bigint NOT NULL,
    is_severed boolean NOT NULL,
    protection_format smallint,
    encryption_reference varchar(24),
    index_reference varchar(24),
    blind_index bytea,
    ciphertext bytea,
    nonce bytea,
    tag bytea,
    last_event_id uuid NOT NULL,
    CONSTRAINT pk_person_key_map PRIMARY KEY (person_key),
    CONSTRAINT uq_person_key_map_last_event UNIQUE (last_event_id),
    CONSTRAINT uq_person_key_map_lookup
        UNIQUE (tenant_id, index_reference, blind_index),
    CONSTRAINT ck_person_key_map_person_key
        CHECK (
            person_key <>
                '00000000-0000-0000-0000-000000000000'::uuid),
    CONSTRAINT ck_person_key_map_tenant
        CHECK (
            tenant_id <>
                '00000000-0000-0000-0000-000000000000'::uuid),
    CONSTRAINT ck_person_key_map_last_event
        CHECK (
            last_event_id <>
                '00000000-0000-0000-0000-000000000000'::uuid),
    CONSTRAINT ck_person_key_map_protection_format
        CHECK (
            protection_format IS NULL
            OR protection_format = 1),
    CONSTRAINT ck_person_key_map_encryption_reference
        CHECK (
            encryption_reference IS NULL
            OR encryption_reference ~
                '^[a-z0-9][a-z0-9-]{0,23}$'),
    CONSTRAINT ck_person_key_map_index_reference
        CHECK (
            index_reference IS NULL
            OR index_reference ~
                '^[a-z0-9][a-z0-9-]{0,23}$'),
    CONSTRAINT ck_person_key_map_blind_index
        CHECK (
            blind_index IS NULL
            OR octet_length(blind_index) = 32),
    CONSTRAINT ck_person_key_map_ciphertext
        CHECK (
            ciphertext IS NULL
            OR octet_length(ciphertext) BETWEEN 19 AND 1043),
    CONSTRAINT ck_person_key_map_nonce
        CHECK (
            nonce IS NULL
            OR octet_length(nonce) = 12),
    CONSTRAINT ck_person_key_map_tag
        CHECK (
            tag IS NULL
            OR octet_length(tag) = 16),
    CONSTRAINT ck_person_key_map_state
        CHECK (
            (
                NOT is_severed
                AND version = 1
                AND protection_format = 1
                AND encryption_reference IS NOT NULL
                AND index_reference IS NOT NULL
                AND blind_index IS NOT NULL
                AND ciphertext IS NOT NULL
                AND nonce IS NOT NULL
                AND tag IS NOT NULL)
            OR
            (
                is_severed
                AND version = 2
                AND protection_format IS NULL
                AND encryption_reference IS NULL
                AND index_reference IS NULL
                AND blind_index IS NULL
                AND ciphertext IS NULL
                AND nonce IS NULL
                AND tag IS NULL))
);

CREATE TABLE trust_store.role_assignments (
    assignment_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    subject_person_key uuid NOT NULL,
    role smallint NOT NULL,
    organization_scope smallint NOT NULL,
    assigned_by_kind smallint NOT NULL,
    assigned_by_person_key uuid,
    assigned_by_workload_id varchar(128),
    assigned_at timestamptz(6) NOT NULL,
    version bigint NOT NULL,
    revoked_at timestamptz(6),
    revoked_by_kind smallint,
    revoked_by_person_key uuid,
    revoked_by_workload_id varchar(128),
    active_slot smallint,
    last_event_id uuid NOT NULL,
    CONSTRAINT pk_role_assignments PRIMARY KEY (assignment_id),
    CONSTRAINT uq_role_assignments_last_event UNIQUE (last_event_id),
    CONSTRAINT uq_role_assignments_active
        UNIQUE (
            tenant_id,
            subject_person_key,
            role,
            active_slot),
    CONSTRAINT ck_role_assignments_assignment_id
        CHECK (
            assignment_id <>
                '00000000-0000-0000-0000-000000000000'::uuid),
    CONSTRAINT ck_role_assignments_tenant
        CHECK (
            tenant_id <>
                '00000000-0000-0000-0000-000000000000'::uuid),
    CONSTRAINT ck_role_assignments_subject
        CHECK (
            subject_person_key <>
                '00000000-0000-0000-0000-000000000000'::uuid),
    CONSTRAINT ck_role_assignments_role
        CHECK (role BETWEEN 0 AND 3),
    CONSTRAINT ck_role_assignments_scope
        CHECK (organization_scope = 0),
    CONSTRAINT ck_role_assignments_assigned_actor_kind
        CHECK (assigned_by_kind BETWEEN 1 AND 3),
    CONSTRAINT ck_role_assignments_assigned_actor_shape
        CHECK (
            (
                assigned_by_kind = 1
                AND assigned_by_person_key IS NOT NULL
                AND assigned_by_person_key <>
                    '00000000-0000-0000-0000-000000000000'::uuid
                AND assigned_by_workload_id IS NULL)
            OR
            (
                assigned_by_kind IN (2, 3)
                AND assigned_by_person_key IS NULL
                AND assigned_by_workload_id IS NOT NULL
                AND char_length(assigned_by_workload_id)
                    BETWEEN 1 AND 128
                AND btrim(assigned_by_workload_id)
                    = assigned_by_workload_id
                AND assigned_by_workload_id
                    !~ '[[:cntrl:]]')),
    CONSTRAINT ck_role_assignments_assigned_system_actor
        CHECK (
            assigned_by_kind <> 2
            OR (
                assigned_by_workload_id ~
                    '^[A-Za-z0-9._-]{1,128}$'
                AND assigned_by_workload_id !~*
                    '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}'
                AND assigned_by_workload_id !~*
                    '[0-9a-f]{32}')),
    CONSTRAINT ck_role_assignments_assigned_provider_actor
        CHECK (
            assigned_by_kind <> 3
            OR (
                position('@' IN assigned_by_workload_id) = 0
                AND lower(assigned_by_workload_id) !~*
                    '^(entra:|oid:|object-id:|directory-object:)'
                AND assigned_by_workload_id !~*
                    '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}'
                AND assigned_by_workload_id !~*
                    '[0-9a-f]{32}')),
    CONSTRAINT ck_role_assignments_assigned_at
        CHECK (
            assigned_at <> 'infinity'::timestamptz
            AND assigned_at <> '-infinity'::timestamptz),
    CONSTRAINT ck_role_assignments_revoked_actor_kind
        CHECK (
            revoked_by_kind IS NULL
            OR revoked_by_kind BETWEEN 1 AND 3),
    CONSTRAINT ck_role_assignments_revoked_actor_shape
        CHECK (
            (
                revoked_by_kind IS NULL
                AND revoked_by_person_key IS NULL
                AND revoked_by_workload_id IS NULL)
            OR
            (
                revoked_by_kind = 1
                AND revoked_by_person_key IS NOT NULL
                AND revoked_by_person_key <>
                    '00000000-0000-0000-0000-000000000000'::uuid
                AND revoked_by_workload_id IS NULL)
            OR
            (
                revoked_by_kind IN (2, 3)
                AND revoked_by_person_key IS NULL
                AND revoked_by_workload_id IS NOT NULL
                AND char_length(revoked_by_workload_id)
                    BETWEEN 1 AND 128
                AND btrim(revoked_by_workload_id)
                    = revoked_by_workload_id
                AND revoked_by_workload_id
                    !~ '[[:cntrl:]]')),
    CONSTRAINT ck_role_assignments_revoked_system_actor
        CHECK (
            revoked_by_kind IS DISTINCT FROM 2
            OR (
                revoked_by_workload_id ~
                    '^[A-Za-z0-9._-]{1,128}$'
                AND revoked_by_workload_id !~*
                    '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}'
                AND revoked_by_workload_id !~*
                    '[0-9a-f]{32}')),
    CONSTRAINT ck_role_assignments_revoked_provider_actor
        CHECK (
            revoked_by_kind IS DISTINCT FROM 3
            OR (
                position('@' IN revoked_by_workload_id) = 0
                AND lower(revoked_by_workload_id) !~*
                    '^(entra:|oid:|object-id:|directory-object:)'
                AND revoked_by_workload_id !~*
                    '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}'
                AND revoked_by_workload_id !~*
                    '[0-9a-f]{32}')),
    CONSTRAINT ck_role_assignments_revoked_at
        CHECK (
            revoked_at IS NULL
            OR (
                revoked_at <> 'infinity'::timestamptz
                AND revoked_at <> '-infinity'::timestamptz
                AND revoked_at >= assigned_at)),
    CONSTRAINT ck_role_assignments_active_slot
        CHECK (
            active_slot IS NULL
            OR active_slot = 1),
    CONSTRAINT ck_role_assignments_state
        CHECK (
            (
                version = 1
                AND revoked_at IS NULL
                AND revoked_by_kind IS NULL
                AND revoked_by_person_key IS NULL
                AND revoked_by_workload_id IS NULL
                AND active_slot = 1)
            OR
            (
                version = 2
                AND revoked_at IS NOT NULL
                AND revoked_by_kind IS NOT NULL
                AND active_slot IS NULL)),
    CONSTRAINT ck_role_assignments_last_event
        CHECK (
            last_event_id <>
                '00000000-0000-0000-0000-000000000000'::uuid)
);

CREATE INDEX ix_role_assignments_subject_history
ON trust_store.role_assignments (
    tenant_id,
    subject_person_key,
    assigned_at,
    assignment_id);

ALTER TABLE trust_store.person_key_map
    ENABLE ROW LEVEL SECURITY;
ALTER TABLE trust_store.person_key_map
    FORCE ROW LEVEL SECURITY;
ALTER TABLE trust_store.person_key_index_authority
    ENABLE ROW LEVEL SECURITY;
ALTER TABLE trust_store.person_key_index_authority
    FORCE ROW LEVEL SECURITY;
ALTER TABLE trust_store.role_assignments
    ENABLE ROW LEVEL SECURITY;
ALTER TABLE trust_store.role_assignments
    FORCE ROW LEVEL SECURITY;

CREATE POLICY person_key_map_select_tenant
ON trust_store.person_key_map
FOR SELECT
USING (
    tenant_id = event_store.current_tenant_id());

CREATE POLICY person_key_map_insert_tenant
ON trust_store.person_key_map
FOR INSERT
WITH CHECK (
    tenant_id = event_store.current_tenant_id());

CREATE POLICY person_key_map_update_tenant
ON trust_store.person_key_map
FOR UPDATE
USING (
    tenant_id = event_store.current_tenant_id())
WITH CHECK (
    tenant_id = event_store.current_tenant_id());

CREATE POLICY person_key_index_authority_select_tenant
ON trust_store.person_key_index_authority
FOR SELECT
USING (
    tenant_id = event_store.current_tenant_id());

CREATE POLICY person_key_index_authority_insert_tenant
ON trust_store.person_key_index_authority
FOR INSERT
WITH CHECK (
    tenant_id = event_store.current_tenant_id());

CREATE POLICY role_assignments_select_tenant
ON trust_store.role_assignments
FOR SELECT
USING (
    tenant_id = event_store.current_tenant_id());

CREATE POLICY role_assignments_insert_tenant
ON trust_store.role_assignments
FOR INSERT
WITH CHECK (
    tenant_id = event_store.current_tenant_id());

CREATE POLICY role_assignments_update_tenant
ON trust_store.role_assignments
FOR UPDATE
USING (
    tenant_id = event_store.current_tenant_id())
WITH CHECK (
    tenant_id = event_store.current_tenant_id());

CREATE FUNCTION trust_store.guard_tenant(
    requested_tenant uuid)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, trust_store, event_store
AS $$
BEGIN
    IF requested_tenant IS NULL
       OR requested_tenant =
            '00000000-0000-0000-0000-000000000000'::uuid
       OR requested_tenant IS DISTINCT FROM
            event_store.current_tenant_id()
    THEN
        RAISE EXCEPTION 'trust operation rejected'
            USING ERRCODE = '42501';
    END IF;
END;
$$;

CREATE FUNCTION trust_store.read_role_assignment(
    p_tenant_id uuid,
    p_assignment_id uuid)
RETURNS SETOF trust_store.role_assignments
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, trust_store, event_store
AS $$
BEGIN
    PERFORM trust_store.guard_tenant(p_tenant_id);
    IF p_assignment_id IS NULL
       OR p_assignment_id =
            '00000000-0000-0000-0000-000000000000'::uuid
    THEN
        RETURN;
    END IF;

    RETURN QUERY
    SELECT assignment.*
    FROM trust_store.role_assignments AS assignment
    WHERE assignment.tenant_id = p_tenant_id
      AND assignment.assignment_id = p_assignment_id;
END;
$$;

CREATE FUNCTION trust_store.lock_role_assignment(
    p_tenant_id uuid,
    p_assignment_id uuid)
RETURNS SETOF trust_store.role_assignments
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, trust_store, event_store
AS $$
BEGIN
    PERFORM trust_store.guard_tenant(p_tenant_id);
    IF p_assignment_id IS NULL
       OR p_assignment_id =
            '00000000-0000-0000-0000-000000000000'::uuid
    THEN
        RETURN;
    END IF;

    RETURN QUERY
    SELECT assignment.*
    FROM trust_store.role_assignments AS assignment
    WHERE assignment.tenant_id = p_tenant_id
      AND assignment.assignment_id = p_assignment_id
    FOR UPDATE;
END;
$$;

CREATE FUNCTION trust_store.list_role_assignments(
    p_tenant_id uuid,
    p_subject_person_key uuid)
RETURNS SETOF trust_store.role_assignments
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, trust_store, event_store
AS $$
BEGIN
    PERFORM trust_store.guard_tenant(p_tenant_id);
    IF p_subject_person_key IS NULL
       OR p_subject_person_key =
            '00000000-0000-0000-0000-000000000000'::uuid
    THEN
        RETURN;
    END IF;

    RETURN QUERY
    SELECT assignment.*
    FROM trust_store.role_assignments AS assignment
    WHERE assignment.tenant_id = p_tenant_id
      AND assignment.subject_person_key =
            p_subject_person_key
    ORDER BY
        assignment.assigned_at,
        assignment.assignment_id;
END;
$$;

CREATE FUNCTION trust_store.lock_active_role_assignment(
    p_tenant_id uuid,
    p_subject_person_key uuid,
    p_role smallint)
RETURNS SETOF trust_store.role_assignments
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, trust_store, event_store
AS $$
BEGIN
    PERFORM trust_store.guard_tenant(p_tenant_id);
    IF p_subject_person_key IS NULL
       OR p_subject_person_key =
            '00000000-0000-0000-0000-000000000000'::uuid
       OR p_role IS NULL
       OR p_role NOT BETWEEN 0 AND 3
    THEN
        RAISE EXCEPTION 'trust operation rejected'
            USING ERRCODE = '22023';
    END IF;

    PERFORM pg_catalog.pg_advisory_xact_lock(
        pg_catalog.hashtextextended(
            p_tenant_id::text || ':' ||
            p_subject_person_key::text || ':' ||
            p_role::text,
            81241017));

    RETURN QUERY
    SELECT assignment.*
    FROM trust_store.role_assignments AS assignment
    WHERE assignment.tenant_id = p_tenant_id
      AND assignment.subject_person_key =
            p_subject_person_key
      AND assignment.role = p_role
      AND assignment.active_slot = 1
    FOR UPDATE;
END;
$$;

CREATE FUNCTION trust_store.insert_role_assignment(
    p_tenant_id uuid,
    p_assignment_id uuid,
    p_subject_person_key uuid,
    p_role smallint,
    p_organization_scope smallint,
    p_assigned_by_kind smallint,
    p_assigned_by_person_key uuid,
    p_assigned_by_workload_id varchar(128),
    p_assigned_at timestamptz,
    p_event_id uuid)
RETURNS boolean
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, trust_store, event_store
AS $$
DECLARE
    affected_rows integer;
BEGIN
    PERFORM trust_store.guard_tenant(p_tenant_id);
    INSERT INTO trust_store.role_assignments (
        assignment_id,
        tenant_id,
        subject_person_key,
        role,
        organization_scope,
        assigned_by_kind,
        assigned_by_person_key,
        assigned_by_workload_id,
        assigned_at,
        version,
        revoked_at,
        revoked_by_kind,
        revoked_by_person_key,
        revoked_by_workload_id,
        active_slot,
        last_event_id)
    VALUES (
        p_assignment_id,
        p_tenant_id,
        p_subject_person_key,
        p_role,
        p_organization_scope,
        p_assigned_by_kind,
        p_assigned_by_person_key,
        p_assigned_by_workload_id,
        p_assigned_at,
        1,
        NULL,
        NULL,
        NULL,
        NULL,
        1,
        p_event_id)
    ON CONFLICT (assignment_id) DO NOTHING;

    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    RETURN affected_rows = 1;
END;
$$;

CREATE FUNCTION trust_store.revoke_role_assignment(
    p_tenant_id uuid,
    p_assignment_id uuid,
    p_expected_version bigint,
    p_revoked_at timestamptz,
    p_revoked_by_kind smallint,
    p_revoked_by_person_key uuid,
    p_revoked_by_workload_id varchar(128),
    p_event_id uuid)
RETURNS boolean
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, trust_store, event_store
AS $$
DECLARE
    affected_rows integer;
BEGIN
    PERFORM trust_store.guard_tenant(p_tenant_id);
    UPDATE trust_store.role_assignments AS assignment
    SET version = 2,
        revoked_at = p_revoked_at,
        revoked_by_kind = p_revoked_by_kind,
        revoked_by_person_key = p_revoked_by_person_key,
        revoked_by_workload_id = p_revoked_by_workload_id,
        active_slot = NULL,
        last_event_id = p_event_id
    WHERE assignment.tenant_id = p_tenant_id
      AND assignment.assignment_id = p_assignment_id
      AND assignment.version = p_expected_version
      AND p_expected_version = 1
      AND assignment.active_slot = 1;

    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    RETURN affected_rows = 1;
END;
$$;

CREATE FUNCTION trust_store.lock_person_key_creation(
    p_tenant_id uuid,
    p_index_reference varchar(24),
    p_key_commitment bytea)
RETURNS boolean
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, trust_store, event_store
AS $$
BEGIN
    PERFORM trust_store.guard_tenant(p_tenant_id);
    IF p_index_reference IS NULL
       OR p_index_reference !~
            '^[a-z0-9][a-z0-9-]{0,23}$'
       OR p_key_commitment IS NULL
       OR octet_length(p_key_commitment) <> 32
    THEN
        RAISE EXCEPTION 'trust operation rejected'
            USING ERRCODE = '22023';
    END IF;

    PERFORM pg_catalog.pg_advisory_xact_lock(
        pg_catalog.hashtextextended(
            'person-key-map:' || p_tenant_id::text,
            81241017));

    IF EXISTS (
        SELECT 1
        FROM trust_store.person_key_index_authority
            AS authority
        WHERE authority.tenant_id = p_tenant_id
          AND (
              authority.index_reference
                    IS DISTINCT FROM p_index_reference
              OR authority.key_commitment
                    IS DISTINCT FROM p_key_commitment))
    THEN
        RAISE EXCEPTION 'trust operation rejected'
            USING ERRCODE = '55000';
    END IF;

    RETURN true;
END;
$$;

CREATE FUNCTION trust_store.find_person_key(
    p_tenant_id uuid,
    p_index_reference varchar(24),
    p_key_commitment bytea,
    p_blind_index bytea)
RETURNS SETOF trust_store.person_key_map
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, trust_store, event_store
AS $$
DECLARE
    established_reference varchar(24);
    established_commitment bytea;
BEGIN
    PERFORM trust_store.guard_tenant(p_tenant_id);
    IF p_index_reference IS NULL
       OR p_index_reference !~
            '^[a-z0-9][a-z0-9-]{0,23}$'
       OR p_key_commitment IS NULL
       OR octet_length(p_key_commitment) <> 32
       OR p_blind_index IS NULL
       OR octet_length(p_blind_index) <> 32
    THEN
        RAISE EXCEPTION 'trust operation rejected'
            USING ERRCODE = '22023';
    END IF;

    SELECT
        authority.index_reference,
        authority.key_commitment
    INTO
        established_reference,
        established_commitment
    FROM trust_store.person_key_index_authority AS authority
    WHERE authority.tenant_id = p_tenant_id;

    IF NOT FOUND
    THEN
        RETURN;
    END IF;
    IF established_reference
            IS DISTINCT FROM p_index_reference
       OR established_commitment
            IS DISTINCT FROM p_key_commitment
    THEN
        RAISE EXCEPTION 'trust operation rejected'
            USING ERRCODE = '55000';
    END IF;

    RETURN QUERY
    SELECT mapping.*
    FROM trust_store.person_key_map AS mapping
    WHERE mapping.tenant_id = p_tenant_id
      AND mapping.index_reference = p_index_reference
      AND mapping.blind_index = p_blind_index
      AND NOT mapping.is_severed
    FOR UPDATE;
END;
$$;

CREATE FUNCTION trust_store.read_person_key(
    p_tenant_id uuid,
    p_person_key uuid)
RETURNS SETOF trust_store.person_key_map
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, trust_store, event_store
AS $$
BEGIN
    PERFORM trust_store.guard_tenant(p_tenant_id);
    IF p_person_key IS NULL
       OR p_person_key =
            '00000000-0000-0000-0000-000000000000'::uuid
    THEN
        RETURN;
    END IF;

    RETURN QUERY
    SELECT mapping.*
    FROM trust_store.person_key_map AS mapping
    WHERE mapping.tenant_id = p_tenant_id
      AND mapping.person_key = p_person_key
    FOR UPDATE;
END;
$$;

CREATE FUNCTION trust_store.insert_person_key(
    p_tenant_id uuid,
    p_person_key uuid,
    p_protection_format smallint,
    p_encryption_reference varchar(24),
    p_index_reference varchar(24),
    p_key_commitment bytea,
    p_blind_index bytea,
    p_ciphertext bytea,
    p_nonce bytea,
    p_tag bytea,
    p_event_id uuid)
RETURNS boolean
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, trust_store, event_store
AS $$
BEGIN
    PERFORM trust_store.guard_tenant(p_tenant_id);
    PERFORM trust_store.lock_person_key_creation(
        p_tenant_id,
        p_index_reference,
        p_key_commitment);

    INSERT INTO trust_store.person_key_index_authority (
        tenant_id,
        index_reference,
        key_commitment)
    VALUES (
        p_tenant_id,
        p_index_reference,
        p_key_commitment)
    ON CONFLICT (tenant_id) DO NOTHING;

    INSERT INTO trust_store.person_key_map (
        person_key,
        tenant_id,
        version,
        is_severed,
        protection_format,
        encryption_reference,
        index_reference,
        blind_index,
        ciphertext,
        nonce,
        tag,
        last_event_id)
    VALUES (
        p_person_key,
        p_tenant_id,
        1,
        false,
        p_protection_format,
        p_encryption_reference,
        p_index_reference,
        p_blind_index,
        p_ciphertext,
        p_nonce,
        p_tag,
        p_event_id);

    RETURN true;
END;
$$;

CREATE FUNCTION trust_store.sever_person_key(
    p_tenant_id uuid,
    p_person_key uuid,
    p_expected_version bigint,
    p_event_id uuid)
RETURNS boolean
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, trust_store, event_store
AS $$
DECLARE
    affected_rows integer;
BEGIN
    PERFORM trust_store.guard_tenant(p_tenant_id);
    UPDATE trust_store.person_key_map AS mapping
    SET version = 2,
        is_severed = true,
        protection_format = NULL,
        encryption_reference = NULL,
        index_reference = NULL,
        blind_index = NULL,
        ciphertext = NULL,
        nonce = NULL,
        tag = NULL,
        last_event_id = p_event_id
    WHERE mapping.tenant_id = p_tenant_id
      AND mapping.person_key = p_person_key
      AND mapping.version = p_expected_version
      AND p_expected_version = 1
      AND NOT mapping.is_severed;

    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    RETURN affected_rows = 1;
END;
$$;

CREATE FUNCTION trust_store.require_role_assignment_event()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, trust_store, event_store
AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM event_store.domain_events AS event
        WHERE event.event_id = NEW.last_event_id
          AND event.tenant_id = NEW.tenant_id
          AND event.event_type = 'RoleAssignmentChanged'
          AND event.aggregate_kind = 'role-assignment'
          AND event.aggregate_value =
                NEW.assignment_id::text
          AND event.privilege = 1)
    THEN
        RAISE EXCEPTION 'trust state event is missing'
            USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$;

CREATE FUNCTION trust_store.require_person_key_event()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, trust_store, event_store
AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM event_store.domain_events AS event
        WHERE event.event_id = NEW.last_event_id
          AND event.tenant_id = NEW.tenant_id
          AND event.event_type = 'PersonKeyMapChanged'
          AND event.aggregate_kind = 'person-key'
          AND event.aggregate_value =
                NEW.person_key::text
          AND event.privilege = 1)
    THEN
        RAISE EXCEPTION 'trust state event is missing'
            USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$;

CREATE CONSTRAINT TRIGGER role_assignment_event_guard
AFTER INSERT OR UPDATE ON trust_store.role_assignments
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW
EXECUTE FUNCTION trust_store.require_role_assignment_event();

CREATE CONSTRAINT TRIGGER person_key_event_guard
AFTER INSERT OR UPDATE ON trust_store.person_key_map
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW
EXECUTE FUNCTION trust_store.require_person_key_event();

REVOKE ALL ON ALL TABLES IN SCHEMA trust_store
    FROM PUBLIC;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA trust_store
    FROM PUBLIC;

GRANT USAGE ON SCHEMA trust_store
    TO control_tower_runtime;
GRANT EXECUTE ON FUNCTION
    trust_store.read_role_assignment(uuid, uuid)
    TO control_tower_runtime;
GRANT EXECUTE ON FUNCTION
    trust_store.lock_role_assignment(uuid, uuid)
    TO control_tower_runtime;
GRANT EXECUTE ON FUNCTION
    trust_store.list_role_assignments(uuid, uuid)
    TO control_tower_runtime;
GRANT EXECUTE ON FUNCTION
    trust_store.lock_active_role_assignment(
        uuid,
        uuid,
        smallint)
    TO control_tower_runtime;
GRANT EXECUTE ON FUNCTION
    trust_store.insert_role_assignment(
        uuid,
        uuid,
        uuid,
        smallint,
        smallint,
        smallint,
        uuid,
        varchar,
        timestamptz,
        uuid)
    TO control_tower_runtime;
GRANT EXECUTE ON FUNCTION
    trust_store.revoke_role_assignment(
        uuid,
        uuid,
        bigint,
        timestamptz,
        smallint,
        uuid,
        varchar,
        uuid)
    TO control_tower_runtime;

GRANT USAGE ON SCHEMA trust_store
    TO control_tower_privileged_runtime;
GRANT EXECUTE ON FUNCTION
    trust_store.lock_person_key_creation(
        uuid,
        varchar,
        bytea)
    TO control_tower_privileged_runtime;
GRANT EXECUTE ON FUNCTION
    trust_store.find_person_key(
        uuid,
        varchar,
        bytea,
        bytea)
    TO control_tower_privileged_runtime;
GRANT EXECUTE ON FUNCTION
    trust_store.read_person_key(uuid, uuid)
    TO control_tower_privileged_runtime;
GRANT EXECUTE ON FUNCTION
    trust_store.insert_person_key(
        uuid,
        uuid,
        smallint,
        varchar,
        varchar,
        bytea,
        bytea,
        bytea,
        bytea,
        bytea,
        uuid)
    TO control_tower_privileged_runtime;
GRANT EXECUTE ON FUNCTION
    trust_store.sever_person_key(
        uuid,
        uuid,
        bigint,
        uuid)
    TO control_tower_privileged_runtime;

GRANT USAGE ON SCHEMA event_store
    TO control_tower_privileged_runtime;
GRANT EXECUTE ON FUNCTION
    event_store.current_tenant_id()
    TO control_tower_privileged_runtime;
GRANT EXECUTE ON FUNCTION
    event_store.lock_stream_head(uuid)
    TO control_tower_privileged_runtime;
GRANT EXECUTE ON FUNCTION event_store.append_event(
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
    TO control_tower_privileged_runtime;

COMMIT;
