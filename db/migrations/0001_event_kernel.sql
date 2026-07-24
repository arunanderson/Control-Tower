BEGIN;

CREATE SCHEMA event_store;

REVOKE ALL ON SCHEMA event_store FROM PUBLIC;

CREATE FUNCTION event_store.current_tenant_id()
RETURNS uuid
LANGUAGE sql
STABLE
PARALLEL SAFE
AS $$
    SELECT NULLIF(
        current_setting('control_tower.tenant_id', true),
        '')::uuid
$$;

CREATE FUNCTION event_store.reject_immutable_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'event records are immutable'
        USING ERRCODE = '55000';
END;
$$;

CREATE TABLE event_store.stream_heads (
    tenant_id uuid NOT NULL,
    next_position bigint NOT NULL,
    last_hash varchar(64) NOT NULL,
    CONSTRAINT pk_stream_heads PRIMARY KEY (tenant_id),
    CONSTRAINT ck_stream_heads_tenant_nonempty
        CHECK (tenant_id <> '00000000-0000-0000-0000-000000000000'::uuid),
    CONSTRAINT ck_stream_heads_position CHECK (next_position > 0),
    CONSTRAINT ck_stream_heads_hash
        CHECK (
            (next_position = 1 AND last_hash = '')
            OR
            (next_position > 1 AND last_hash ~ '^[0-9A-F]{64}$'))
);

CREATE TABLE event_store.domain_events (
    integrity_format_version integer NOT NULL,
    tenant_id uuid NOT NULL,
    position bigint NOT NULL,
    event_id uuid NOT NULL,
    event_type varchar(160) NOT NULL,
    aggregate_kind varchar(64) NOT NULL,
    aggregate_value varchar(256) NOT NULL,
    actor_kind smallint NOT NULL,
    actor_opaque_id varchar(128) NOT NULL,
    occurred_at timestamptz(6) NOT NULL,
    recorded_at timestamptz(6) NOT NULL,
    reason varchar(2048),
    correlation_kind varchar(64),
    correlation_value varchar(256),
    privilege smallint NOT NULL,
    previous_hash varchar(64) NOT NULL,
    hash varchar(64) NOT NULL,
    payload bytea NOT NULL,
    CONSTRAINT pk_domain_events PRIMARY KEY (event_id),
    CONSTRAINT uq_domain_events_tenant_position
        UNIQUE (tenant_id, position),
    CONSTRAINT ck_domain_events_integrity_format
        CHECK (integrity_format_version = 2),
    CONSTRAINT ck_domain_events_tenant_nonempty
        CHECK (tenant_id <> '00000000-0000-0000-0000-000000000000'::uuid),
    CONSTRAINT ck_domain_events_position CHECK (position > 0),
    CONSTRAINT ck_domain_events_event_id_nonempty
        CHECK (event_id <> '00000000-0000-0000-0000-000000000000'::uuid),
    CONSTRAINT ck_domain_events_event_type
        CHECK (event_type ~ '^[A-Za-z][A-Za-z0-9._-]{0,159}$'),
    CONSTRAINT ck_domain_events_aggregate_kind
        CHECK (aggregate_kind ~ '^[a-z][a-z0-9-]{0,63}$'),
    CONSTRAINT ck_domain_events_aggregate_value
        CHECK (
            char_length(aggregate_value) BETWEEN 1 AND 256
            AND btrim(aggregate_value) = aggregate_value
            AND aggregate_value !~ '[[:cntrl:]]'),
    CONSTRAINT ck_domain_events_actor_kind
        CHECK (actor_kind BETWEEN 1 AND 3),
    CONSTRAINT ck_domain_events_actor_opaque_id
        CHECK (
            char_length(actor_opaque_id) BETWEEN 1 AND 128
            AND btrim(actor_opaque_id) = actor_opaque_id
            AND actor_opaque_id !~ '[[:cntrl:]]'),
    CONSTRAINT ck_domain_events_human_actor
        CHECK (
            actor_kind <> 1
            OR (
                actor_opaque_id ~
                    '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                AND actor_opaque_id <>
                    '00000000-0000-0000-0000-000000000000')),
    CONSTRAINT ck_domain_events_system_actor
        CHECK (
            actor_kind <> 2
            OR (
                actor_opaque_id ~ '^[A-Za-z0-9._-]{1,128}$'
                AND actor_opaque_id !~*
                    '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}'
                AND actor_opaque_id !~*
                    '[0-9a-f]{32}')),
    CONSTRAINT ck_domain_events_provider_actor
        CHECK (
            actor_kind <> 3
            OR (
                position('@' IN actor_opaque_id) = 0
                AND lower(actor_opaque_id) !~*
                    '^(entra:|oid:|object-id:|directory-object:)'
                AND actor_opaque_id !~*
                    '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}'
                AND actor_opaque_id !~*
                    '[0-9a-f]{32}')),
    CONSTRAINT ck_domain_events_occurred_at_finite
        CHECK (
            occurred_at <> 'infinity'::timestamptz
            AND occurred_at <> '-infinity'::timestamptz),
    CONSTRAINT ck_domain_events_recorded_at_finite
        CHECK (
            recorded_at <> 'infinity'::timestamptz
            AND recorded_at <> '-infinity'::timestamptz),
    CONSTRAINT ck_domain_events_reason
        CHECK (
            reason IS NULL
            OR (
                char_length(reason) BETWEEN 1 AND 2048
                AND btrim(reason) = reason
                AND reason !~ '[[:cntrl:]]')),
    CONSTRAINT ck_domain_events_correlation_pair
        CHECK (
            (correlation_kind IS NULL AND correlation_value IS NULL)
            OR
            (correlation_kind IS NOT NULL AND correlation_value IS NOT NULL)),
    CONSTRAINT ck_domain_events_correlation_kind
        CHECK (
            correlation_kind IS NULL
            OR correlation_kind ~ '^[a-z][a-z0-9-]{0,63}$'),
    CONSTRAINT ck_domain_events_correlation_value
        CHECK (
            correlation_value IS NULL
            OR (
                char_length(correlation_value) BETWEEN 1 AND 256
                AND btrim(correlation_value) = correlation_value
                AND correlation_value !~ '[[:cntrl:]]')),
    CONSTRAINT ck_domain_events_privilege
        CHECK (privilege IN (0, 1)),
    CONSTRAINT ck_domain_events_previous_hash
        CHECK (
            (position = 1 AND previous_hash = '')
            OR
            (position > 1 AND previous_hash ~ '^[0-9A-F]{64}$')),
    CONSTRAINT ck_domain_events_hash
        CHECK (hash ~ '^[0-9A-F]{64}$')
);

CREATE TRIGGER domain_events_immutable_rows
BEFORE UPDATE OR DELETE ON event_store.domain_events
FOR EACH ROW EXECUTE FUNCTION event_store.reject_immutable_mutation();

CREATE TRIGGER domain_events_immutable_truncate
BEFORE TRUNCATE ON event_store.domain_events
FOR EACH STATEMENT EXECUTE FUNCTION event_store.reject_immutable_mutation();

ALTER TABLE event_store.stream_heads ENABLE ROW LEVEL SECURITY;
ALTER TABLE event_store.stream_heads FORCE ROW LEVEL SECURITY;
ALTER TABLE event_store.domain_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE event_store.domain_events FORCE ROW LEVEL SECURITY;

CREATE POLICY stream_heads_select_tenant
ON event_store.stream_heads
FOR SELECT
USING (tenant_id = event_store.current_tenant_id());

CREATE POLICY stream_heads_insert_tenant
ON event_store.stream_heads
FOR INSERT
WITH CHECK (tenant_id = event_store.current_tenant_id());

CREATE POLICY stream_heads_update_tenant
ON event_store.stream_heads
FOR UPDATE
USING (tenant_id = event_store.current_tenant_id())
WITH CHECK (tenant_id = event_store.current_tenant_id());

CREATE POLICY domain_events_select_tenant
ON event_store.domain_events
FOR SELECT
USING (tenant_id = event_store.current_tenant_id());

CREATE POLICY domain_events_insert_tenant
ON event_store.domain_events
FOR INSERT
WITH CHECK (tenant_id = event_store.current_tenant_id());

CREATE POLICY domain_events_update_tenant
ON event_store.domain_events
FOR UPDATE
USING (tenant_id = event_store.current_tenant_id())
WITH CHECK (tenant_id = event_store.current_tenant_id());

CREATE POLICY domain_events_delete_tenant
ON event_store.domain_events
FOR DELETE
USING (tenant_id = event_store.current_tenant_id());

CREATE FUNCTION event_store.lock_stream_head(requested_tenant uuid)
RETURNS TABLE (
    stream_position bigint,
    previous_hash varchar(64))
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, event_store
AS $$
BEGIN
    IF requested_tenant IS NULL
       OR requested_tenant =
            '00000000-0000-0000-0000-000000000000'::uuid
       OR requested_tenant IS DISTINCT FROM
            event_store.current_tenant_id()
    THEN
        RAISE EXCEPTION 'event append rejected'
            USING ERRCODE = '42501';
    END IF;

    INSERT INTO event_store.stream_heads (
        tenant_id,
        next_position,
        last_hash)
    VALUES (requested_tenant, 1, '')
    ON CONFLICT (tenant_id) DO NOTHING;

    RETURN QUERY
    SELECT head.next_position, head.last_hash
    FROM event_store.stream_heads AS head
    WHERE head.tenant_id = requested_tenant
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'event append rejected'
            USING ERRCODE = '55000';
    END IF;
END;
$$;

CREATE FUNCTION event_store.append_event(
    p_integrity_format_version integer,
    p_tenant_id uuid,
    p_position bigint,
    p_event_id uuid,
    p_event_type varchar(160),
    p_aggregate_kind varchar(64),
    p_aggregate_value varchar(256),
    p_actor_kind smallint,
    p_actor_opaque_id varchar(128),
    p_occurred_at timestamptz,
    p_recorded_at timestamptz,
    p_reason varchar(2048),
    p_correlation_kind varchar(64),
    p_correlation_value varchar(256),
    p_privilege smallint,
    p_previous_hash varchar(64),
    p_hash varchar(64),
    p_payload bytea)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, event_store
AS $$
DECLARE
    authoritative_position bigint;
    authoritative_previous_hash varchar(64);
    affected_rows integer;
BEGIN
    IF p_tenant_id IS NULL
       OR p_tenant_id IS DISTINCT FROM
            event_store.current_tenant_id()
    THEN
        RAISE EXCEPTION 'event append rejected'
            USING ERRCODE = '42501';
    END IF;

    SELECT head.next_position, head.last_hash
    INTO authoritative_position, authoritative_previous_hash
    FROM event_store.stream_heads AS head
    WHERE head.tenant_id = p_tenant_id
    FOR UPDATE;

    IF NOT FOUND
       OR authoritative_position IS DISTINCT FROM p_position
       OR authoritative_previous_hash IS DISTINCT FROM p_previous_hash
    THEN
        RAISE EXCEPTION 'event append rejected'
            USING ERRCODE = '55000';
    END IF;

    INSERT INTO event_store.domain_events (
        integrity_format_version,
        tenant_id,
        position,
        event_id,
        event_type,
        aggregate_kind,
        aggregate_value,
        actor_kind,
        actor_opaque_id,
        occurred_at,
        recorded_at,
        reason,
        correlation_kind,
        correlation_value,
        privilege,
        previous_hash,
        hash,
        payload)
    VALUES (
        p_integrity_format_version,
        p_tenant_id,
        p_position,
        p_event_id,
        p_event_type,
        p_aggregate_kind,
        p_aggregate_value,
        p_actor_kind,
        p_actor_opaque_id,
        p_occurred_at,
        p_recorded_at,
        p_reason,
        p_correlation_kind,
        p_correlation_value,
        p_privilege,
        p_previous_hash,
        p_hash,
        p_payload);

    UPDATE event_store.stream_heads
    SET next_position = p_position + 1,
        last_hash = p_hash
    WHERE tenant_id = p_tenant_id
      AND next_position = p_position
      AND last_hash = p_previous_hash;

    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    IF affected_rows <> 1 THEN
        RAISE EXCEPTION 'event append rejected'
            USING ERRCODE = '55000';
    END IF;
END;
$$;

REVOKE ALL ON ALL TABLES IN SCHEMA event_store FROM PUBLIC;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA event_store FROM PUBLIC;

DO $$
BEGIN
    EXECUTE format(
        'REVOKE TEMPORARY ON DATABASE %I FROM PUBLIC',
        current_database());
END;
$$;

GRANT USAGE ON SCHEMA event_store TO control_tower_runtime;
GRANT EXECUTE ON FUNCTION event_store.current_tenant_id()
    TO control_tower_runtime;
GRANT EXECUTE ON FUNCTION event_store.lock_stream_head(uuid)
    TO control_tower_runtime;
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
    TO control_tower_runtime;
GRANT SELECT ON event_store.domain_events
    TO control_tower_runtime;

COMMIT;
