# Database migration rules

Scope: every file under `/db`. The frozen blueprint and root `CLAUDE.md` remain authoritative.

- Migrations are ordered, versioned and immutable once merged. Never edit, rename or reuse an
  existing migration number after merge; add a later migration instead.
- Every forward migration must have a paired rollback script, executable verification script and
  validation note describing apply → verify → rollback → re-apply plus schema-drift evidence.
- Migration tests may run only against a disposable loopback database created by the current test
  run. Never execute a migration or rollback against a shared, staging or production environment.
- Forward SQL contains no credentials or environment-specific tenant data. Runtime roles are
  provisioned outside migrations; migrations may grant only the least privileges named by their
  contract.
- RLS is enabled and forced on every tenant-bearing table. Runtime roles must not own tables and
  must never have `SUPERUSER` or `BYPASSRLS`.
- Forward and rollback scripts are transaction-wrapped. Rollback scripts must fail closed unless
  their explicit ephemeral-environment guard is present.
- Validation must prove least privilege, tenant isolation, immutability, rollback completeness,
  deterministic re-application and catalog equivalence. A successful SQL exit alone is not proof.
