namespace ControlTower.Platform.Tenancy;

/// <summary>Strongly-typed tenant identifier. Empty is rejected (no ambient/global tenant).</summary>
public readonly record struct TenantId
{
    public Guid Value { get; }

    public TenantId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value.ToString();
}
