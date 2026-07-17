namespace ControlTower.Modules.Ledger.Domain;

/// <summary>
/// Temporal ownership record. Ownership history is never overwritten (Stage 4 §2.3): lapsing sets
/// <see cref="ValidTo"/> and marks the assignment Lapsed; reassignment lapses the old and adds a new
/// Active one. An asset with no current Owner-role assignment is Ownerless — a first-class condition.
/// </summary>
public sealed class OwnershipAssignment
{
    public OwnershipAssignment(PersonRef person, OwnershipRole role, DateTimeOffset validFrom)
    {
        Id = Guid.NewGuid();
        Person = person;
        Role = role;
        ValidFrom = validFrom;
        Status = OwnershipStatus.Active;
    }

    public Guid Id { get; }

    public PersonRef Person { get; }

    public OwnershipRole Role { get; }

    public DateTimeOffset ValidFrom { get; }

    public DateTimeOffset? ValidTo { get; private set; }

    public OwnershipStatus Status { get; private set; }

    public bool IsCurrent => Status == OwnershipStatus.Active && ValidTo is null;

    internal void Lapse(DateTimeOffset at)
    {
        if (!IsCurrent) throw new DomainException("Only a current assignment can lapse.");
        ValidTo = at;
        Status = OwnershipStatus.Lapsed;
    }
}
