import type { RoiView } from "../api/types";
import { ConfidenceMix, EvidenceBadge } from "../components/EvidenceBadge";

function RoiCard({ roi }: { roi: RoiView }) {
  return (
    <div className="roi-card" data-testid={`roi-${roi.scope}`}>
      <h3>{roi.scope}</h3>
      <div>
        Cost: <EvidenceBadge amount={roi.cost} />
      </div>
      <div>
        Value: <EvidenceBadge amount={roi.value} />
      </div>
      <div>
        Net benefit: <EvidenceBadge amount={roi.netBenefit} />
      </div>
      <div data-testid="roi-figure">
        {roi.singlePointSuppressed ? (
          <em data-testid="roi-suppressed">
            ROI shown as a range {roi.lowRoi.toFixed(2)}–
            {roi.highRoi.toFixed(2)} (validated-only{" "}
            {roi.validatedOnlyRoi.toFixed(2)}) — too much soft evidence for a
            single number
          </em>
        ) : (
          <span>
            ROI {roi.singlePointRoi?.toFixed(2)} (validated-only{" "}
            {roi.validatedOnlyRoi.toFixed(2)})
          </span>
        )}
      </div>
      <div>
        Payback:{" "}
        {roi.paybackMonths === null ? "n/a" : `${roi.paybackMonths} months`}
      </div>
      <ConfidenceMix mix={roi.confidenceMix} />
    </div>
  );
}

export function EconomicsArea({
  portfolio,
  departments,
}: {
  portfolio: RoiView;
  departments: RoiView[];
}) {
  return (
    <section>
      <h2>Economics</h2>
      <RoiCard roi={portfolio} />
      <h3>By department</h3>
      {departments.map((d) => (
        <RoiCard key={d.scope} roi={d} />
      ))}
    </section>
  );
}
