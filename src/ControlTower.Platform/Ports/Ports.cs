namespace ControlTower.Platform.Ports;

// Adapter seams (DEV-001 + ADR-023 portability). Production implementations are Azure
// (Key Vault, Service Bus, Blob/WORM, Azure Database for PostgreSQL). Development-only substitutes
// are alternate implementations of these ports, swapped by configuration — never referenced by the
// domain and never present in a production path.

/// <summary>Secret retrieval seam. Prod: Azure Key Vault. Credentials are isolated more strongly than data (ADR-021).</summary>
public interface ISecretProvider
{
    ValueTask<string> GetSecretAsync(string name, CancellationToken ct = default);
}

/// <summary>Durable background-job queue seam. Prod: Azure Service Bus.</summary>
public interface IJobQueue
{
    ValueTask EnqueueAsync(string queue, ReadOnlyMemory<byte> payload, CancellationToken ct = default);
}

/// <summary>Evidence/WORM anchor seam. Prod: Azure Blob Storage with immutability (ADR-021 evidence integrity).</summary>
public interface IEvidenceStore
{
    ValueTask AnchorAsync(string key, ReadOnlyMemory<byte> content, CancellationToken ct = default);
}

/// <summary>Relational data-access seam. Prod: Azure Database for PostgreSQL Flexible Server (DEC-001). Standard SQL only.</summary>
public interface IDataStore
{
    // Intentionally minimal at the skeleton stage; concrete query contracts arrive with each module.
}
