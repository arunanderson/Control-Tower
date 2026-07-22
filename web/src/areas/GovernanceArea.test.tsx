import { render, screen } from "@testing-library/react";
import { GovernanceArea } from "./GovernanceArea";
import type { GovernanceCaseView, GovernanceDebtView } from "../api/types";

const cases: GovernanceCaseView[] = [
  {
    caseId: "c1",
    assetId: "a1",
    type: "ReuseDecision",
    riskTier: "Low",
    status: "Open",
    requiredReviewers: [],
    decisionCount: 0,
    outcome: "reuse:Reuse",
    dueBy: "2026-07-18T00:00:00Z",
    reuseAction: "Reuse",
    slaBreached: true,
  },
];

const debt: GovernanceDebtView[] = [
  {
    assetId: "a2",
    debtType: "Ownerless",
    raisedAt: "2026-07-17T00:00:00Z",
    isOpen: true,
  },
];

test("the workbench shows recommendation outcome, SLA breach and governance debt", () => {
  render(<GovernanceArea cases={cases} debt={debt} />);
  expect(screen.getByTestId("case-outcome")).toHaveTextContent("reuse: Reuse");
  expect(screen.getByTestId("sla-breach")).toBeInTheDocument();
  expect(screen.getByTestId("debt-row")).toHaveTextContent("Ownerless");
});
