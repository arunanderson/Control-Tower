import { render, screen } from "@testing-library/react";
import { EvidenceBadge } from "./EvidenceBadge";
import type { EconomicAmount } from "../api/types";

const amount: EconomicAmount = {
  amount: 1000,
  currency: "EUR",
  evidenceClass: "Measured",
  source: "Azure Cost Management",
  methodology: "billing meter",
  asOf: "2026-07-17T00:00:00Z",
  validationState: "SystemObserved",
};

test("an amount is never shown without its evidence fields", () => {
  render(<EvidenceBadge amount={amount} />);
  expect(screen.getByTestId("evidence-class")).toHaveTextContent("Measured");
  expect(screen.getByTestId("validation-state")).toHaveTextContent("SystemObserved");
  expect(screen.getByTestId("methodology")).toHaveTextContent("billing meter");
  expect(screen.getByTestId("source")).toHaveTextContent("Azure Cost Management");
  expect(screen.getByTestId("as-of")).toHaveTextContent("2026-07-17");
});
