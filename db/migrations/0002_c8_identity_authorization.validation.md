# Migration 0002 validation — C8 identity and authorization foundations

## Scope and safety

`0002_c8_identity_authorization.sql` applies only after the immutable migration 0001 event kernel.
It assumes that two non-owner, non-`BYPASSRLS` group roles already exist:

- `control_tower_runtime` for bounded E18 role-assignment operations;
- `control_tower_privileged_runtime` for bounded E19 point operations and E20 append functions.

The migration creates no login, password, tenant row, key, secret or cloud resource. Raw directory
identity and display snapshots never enter the SQL boundary. The E19 adapter supplies only
application-protected ciphertext, nonce, authentication tag, a tenant-keyed blind index and bounded
non-secret key references.

`0002_c8_identity_authorization.down.sql` is deliberately restricted to a database whose generated
name matches `ct_p1_t08_<hex>` and to a session carrying
`control_tower.ephemeral_migration_guard=P1-T08-EPHEMERAL-ONLY`. It must never run against a shared,
staging or production database.

## Required CI cycle

The P1-T08 PostgreSQL fixture owns a second disposable database and generated migration-owner login
on the pinned `postgres:16.14-alpine3.24` service. The fixture refuses every non-loopback host before
opening an administrative connection and runs non-parallel with the immutable P1-T07 fixture.

It performs this sequence:

1. refuse pre-existing fixed normal and privileged runtime roles, then create both roles, one
   generated migration-owner role and an empty `ct_p1_t08_<hex>` database;
2. apply migration 0001 and execute `0001_event_kernel.verify.sql` as the migration owner without
   invoking migration 0001's P1-T07-only rollback;
3. capture the exact post-0001 catalog fingerprint;
4. apply migration 0002 and execute `0002_c8_identity_authorization.verify.sql`;
5. capture the combined event/trust catalog fingerprint;
6. set the P1-T08 rollback guard and execute only migration 0002's rollback;
7. prove `trust_store` is absent, the privileged role's added event grants are absent, and the
   catalog is byte-for-byte identical to the post-0001 baseline;
8. re-apply migration 0002, re-run both migration verification scripts and prove the combined
   catalog fingerprint is byte-for-byte identical to the first application;
9. run role-separation, RLS, direct-SQL denial, field-protection, audit ordering, concurrency,
   optimistic-conflict, atomic rollback, cancellation, tamper and tenant-context tests only through
   the non-owner runtime roles;
10. terminate connections and drop only the database and roles whose creation this fixture
    successfully completed.

The GitHub workflow already supplies the approved loopback PostgreSQL service. A normal solution
test without the explicit ephemeral marker never opens a database connection.

## Expected durable objects

- schema `trust_store`;
- forced-RLS `person_key_map`, `person_key_index_authority` and `role_assignments` tables owned only
  by the migration owner;
- globally opaque PersonKey and role-assignment primary keys;
- one immutable, tenant-keyed lookup-key reference and cryptographic commitment, allowing point
  operations to reject changed key material without scanning protected identities;
- one active `(tenant, PersonKey, role)` slot while revoked E18 history remains appendable;
- indexed deterministic role history and keyed E19 point lookup;
- active E19 rows containing only authenticated protection fields and severed rows retaining only
  tenant, opaque PersonKey, version/severed state and last event ID;
- no E18-to-E19 database foreign key across the privileged-zone perimeter;
- bounded fixed-search-path security-definer functions, with no table grant or bulk E19 function;
- deferred state-event guards that reject an E18/E19 commit without the matching privileged E20
  event ID, tenant, event type and aggregate reference;
- normal runtime access only to E18 functions;
- privileged runtime access only to E19 point functions and the three E20 append functions, with no
  E18 authority or event-table read;
- no runtime ownership, direct table DML, event enumeration, DDL, temporary objects, superuser,
  role-creation, database-creation, `BYPASSRLS` capability or upward role membership;
- no `pgcrypto`, raw identity column, plaintext field or database-held protection key.

The normal and privileged logins are trusted workload identities, not end-user or tenant
credentials. As fixed by Stage 8 §3 and Stage 9 §4.2, request/job tenancy is authenticated and made
unforgeable in the application tier; transaction-local PostgreSQL context and forced RLS are the
defence-in-depth persistence boundary. A compromised workload credential is therefore a workload
identity compromise, not a supported tenant-authentication path. Neither runtime login is exposed
to users, providers or customer administrators.

## Field-protection proof

The adapter uses BCL AES-256-GCM and HMAC-SHA-256 with tenant-bound secrets obtained through the
existing `ISecretProvider`. Authenticated data binds the protection format, tenant, PersonKey,
encryption reference, index reference and blind index. Tests prove wrong-tenant, wrong-row,
wrong-reference and tampered values fail closed; a blind-index match is accepted only after
authenticated decryption and constant-time raw-directory-ID comparison.

AES key rotation changes the current non-secret encryption reference for new writes while the
separate tenant lookup key keeps existing rows point-findable. Each row retains its non-secret AES
reference so old ciphertext remains decryptable until a later pointwise re-protection operation.
An attempted lookup-reference or lookup-key-material change fails closed once the tenant authority
is established; lookup-key rotation therefore requires the later controlled dual-read/reindex
migration and cannot silently create a second active PersonKey for the same directory identity.
Neither key material nor raw identity enters PostgreSQL, events, privileged-read evidence,
exceptions or logs.

The E19 state adapter and its privileged evidence appender use independent connection pools for the
same least-privilege database identity. This preserves audit-before-release when the state pool is
at capacity and prevents a state transaction from waiting on an audit connection held behind
itself.
