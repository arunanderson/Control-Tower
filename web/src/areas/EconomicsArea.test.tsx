import { render, screen } from "@testing-library/react";
import { EconomicsArea } from "./EconomicsArea";
import type { EconomicAmount, RoiView } from "../api/types";

function amt(value: number, cls: string): EconomicAmount {
  return {
    amount: value,
    currency: "EUR",
    evidenceClass: cls,
    source: "s",
    methodology: "m",
    asOf: "2026-07-17T00:00:00Z",
    validationState: "SystemObserved",
  };
}

const suppressed: RoiView = {
  scope: "portfolio",
  cost: amt(100, "Measured"),
  value: amt(400, "SelfReported"),
  netBenefit: amt(300, "SelfReported"),
  singlePointRoi: null,
  validatedOnlyRoi: 0,
  lowRoi: 0,
  highRoi: 3,
  paybackMonths: 3,
  singlePointSuppressed: true,
  compositeEvidenceClass: "SelfReported",
  confidenceMix: { SelfReported: 1 },
  asOf: "2026-07-17T00:00:00Z",
};

test("a soft-evidence ROI is shown as a range, not a single number", () => {
  render(<EconomicsArea portfolio={suppressed} departments={[]} />);
  expect(screen.getByTestId("roi-suppressed")).toBeInTheDocument();
  expect(screen.getByTestId("roi-suppressed")).toHaveTextContent("range");
});
