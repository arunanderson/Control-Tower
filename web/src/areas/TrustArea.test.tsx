import { render, screen } from "@testing-library/react";
import { TrustArea } from "./TrustArea";
import type { CoverageView } from "../api/types";

test("coverage is reported honestly, including no providers and no sweep", () => {
  const coverage: CoverageView = {
    providersConnected: 0,
    assetsKnown: 3,
    lastSuccessfulSweep: null,
    coverageNote: "No provider connections yet — inventory is manual-registration only.",
    asOf: "2026-07-17T00:00:00Z",
  };
  render(<TrustArea coverage={coverage} />);
  expect(screen.getByTestId("providers-connected")).toHaveTextContent("0");
  expect(screen.getByTestId("last-sweep")).toHaveTextContent("never");
  expect(screen.getByTestId("coverage-note")).toHaveTextContent("manual-registration only");
});
