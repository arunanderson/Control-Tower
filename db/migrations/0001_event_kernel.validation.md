# Migration 0001 validation — E20 event kernel

## Scope and safety

`0001_event_kernel.sql` is the forward migration. It assumes that a non-owner, non-`BYPASSRLS`
group role named `control_tower_runtime` already exists. It creates no login, password, tenant data
or cloud resource.

`0001_event_kernel.down.sql` is deliberately restricted to a database whose generated name matches
`ct_p1_t07_<hex>` and to a session carrying
`control_tower.ephemeral_migration_guard=P1-T07-EPHEMERAL-ONLY`. It must never run against a shared,
staging or production database.

## Required CI cycle

The `ControlTower.Adapters.PostgreSql.Tests` collection owns one disposable database and distinct
migration-owner and runtime login roles on the pinned `postgres:16.14-alpine3.24` service. The
fixture refuses every non-loopback host before opening an administrative connection.

It performs this sequence:

1. refuse a pre-existing fixed runtime role, then create one generated migration-owner role, the
   run-owned fixed runtime role and an empty generated database;
2. apply the forward migration as the migration owner;
3. execute `0001_event_kernel.verify.sql`;
4. capture a canonical catalog fingerprint for the `event_store` schema;
5. set the rollback guard and execute the rollback as the migration owner;
6. prove that the schema is absent;
7. re-apply the same forward migration;
8. re-run verification and compare the catalog fingerprint byte-for-byte;
9. run RLS, least-privilege, immutability, concurrency, atomicity and hash-chain tests through the
   non-owner runtime role and the narrowly granted append functions;
10. terminate connections and drop only the database and login roles whose creation this fixture
    successfully completed.

The GitHub workflow supplies only a loopback connection to its per-job PostgreSQL service. A normal
solution test without the explicit ephemeral marker never opens a database connection.

## Expected durable objects

- schema `event_store`;
- tenant-bearing tables `stream_heads` and `domain_events`;
- a global event-ID primary key and tenant-local position uniqueness on `domain_events`;
- seven forced-RLS policies, including owner-path mutation policies whose rows remain protected by
  immutable triggers while runtime has no mutation grant;
- fixed-search-path security-definer functions that lock the tenant head and atomically insert an
  event plus advance that head without granting runtime table mutation privileges;
- immutable update/delete/truncate triggers on committed event records;
- least-privilege grants to `control_tower_runtime`, with database-default `PUBLIC` temporary-object
  creation revoked by the forward migration and restored only by the guarded disposable rollback;
- no runtime ownership, DDL, delete, truncate, superuser, role-creation, database-creation, or
  `BYPASSRLS` capability, and no upward role membership.
