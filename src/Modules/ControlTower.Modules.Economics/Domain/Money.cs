namespace ControlTower.Modules.Economics.Domain;

/// <summary>Raised on an economics invariant violation (e.g. a figure without evidence).</summary>
public sealed class EconomicsException(string message) : Exception(message);

/// <summary>
/// A monetary amount in its native currency. FX conversion happens only at reporting time with dated,
/// sourced rates (Stage 5 FX rule) — amounts are never silently converted or combined across currencies.
/// </summary>
public readonly record struct Money
{
    public Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new EconomicsException("Currency is required (amounts are stored in native currency).");
        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public decimal Amount { get; }
    public string Currency { get; }

    public static Money Zero(string currency) => new(0m, currency);

    public Money Add(Money other)
    {
        if (other.Currency != Currency)
            throw new EconomicsException($"Cannot combine {Currency} and {other.Currency} without reporting-time FX conversion.");
        return new Money(Amount + other.Amount, Currency);
    }

    public override string ToString() => $"{Amount:0.##} {Currency}";
}
