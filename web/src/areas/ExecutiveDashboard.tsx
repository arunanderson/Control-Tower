import type { ExecutiveEconomicsView } from "../api/types";
import { ConfidenceMix, EvidenceBadge } from "../components/EvidenceBadge";

export function ExecutiveDashboard({ view }: { view: ExecutiveEconomicsView }) {
  return (
    <section>
      <h2>Executive Economics</h2>
      <div data-testid="total-spend">
        Total spend: <EvidenceBadge amount={view.totalSpend} />
      </div>
      <div data-testid="declared-value">
        Declared value: <EvidenceBadge amount={view.declaredValue} />
      </div>
      <div data-testid="validated-value">
        Validated value: <EvidenceBadge amount={view.validatedValue} />
      </div>
      <div data-testid="validated-to-declared">
        Validated-to-Declared ratio: {view.validatedToDeclaredRatio.toFixed(2)}
      </div>
      <div data-testid="unattributed">
        Unattributed cost: <EvidenceBadge amount={view.unattributedCost} /> ({view.unattributedPercent.toFixed(2)})
      </div>
      <ConfidenceMix mix={view.confidenceMix} />
    </section>
  );
}
