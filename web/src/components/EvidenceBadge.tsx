import type { EconomicAmount } from "../api/types";

// Renders a monetary amount together with the evidence the blueprint requires everywhere (ADR-025):
// evidence class, source, methodology, validation state, as-of. No number is shown without them.
export function EvidenceBadge({ amount }: { amount: EconomicAmount }) {
  return (
    <span className="evidence">
      <strong>
        {amount.amount.toLocaleString()} {amount.currency}
      </strong>{" "}
      <small data-testid="evidence-class">[{amount.evidenceClass}]</small>{" "}
      <small data-testid="validation-state">{amount.validationState}</small>{" "}
      <small data-testid="methodology" title={amount.methodology}>
        method: {amount.methodology}
      </small>{" "}
      <small data-testid="source">source: {amount.source}</small>{" "}
      <small data-testid="as-of">as of {amount.asOf.slice(0, 10)}</small>
    </span>
  );
}

export function ConfidenceMix({ mix }: { mix: Record<string, number> }) {
  const entries = Object.entries(mix);
  return (
    <div className="confidence-mix" data-testid="confidence-mix">
      {entries.length === 0 ? (
        <em>no value figures</em>
      ) : (
        entries.map(([cls, count]) => (
          <span key={cls}>
            {cls}: {count}{" "}
          </span>
        ))
      )}
    </div>
  );
}
