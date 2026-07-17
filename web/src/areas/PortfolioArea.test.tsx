import { render, screen } from "@testing-library/react";
import { AssetRecord } from "./PortfolioArea";
import type { AssetLedgerView } from "../api/types";

test("the Asset Record shows ownerless status and match confidence for any asset type", () => {
  const asset: AssetLedgerView = {
    assetId: "a1",
    displayName: "Sales Copilot",
    assetType: "agent",
    registrationStatus: "Registered",
    operationalLifecycleState: "Production",
    matchConfidence: "High",
    isOwnerless: true,
    ownerDisplayName: null,
    businessPurpose: "Drafts sales emails",
    resolutionLinkCount: 2,
  };
  render(<AssetRecord asset={asset} />);
  expect(screen.getByTestId("asset-record")).toHaveTextContent("Sales Copilot");
  expect(screen.getByTestId("match-confidence")).toHaveTextContent("High");
  expect(screen.getByTestId("ownerless")).toBeInTheDocument();
});
