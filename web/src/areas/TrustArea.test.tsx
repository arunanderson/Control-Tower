import { render, screen } from "@testing-library/react";
import { TrustArea } from "./TrustArea";
import type { CoverageView } from "../api/types";

test("coverage is reported honestly, including no providers and no sweep", () => {
  const coverage: CoverageView = {
    providersConnected: 0,
    assetsKnown: 3,
    lastSuccessfulSweep: null,
    coverageNote:
      "No provider connections yet — inventory is manual-registration only.",
    asOf: "2026-07-17T00:00:00Z",
    surfaces: [],
  };
  render(<TrustArea coverage={coverage} />);
  expect(screen.getByTestId("providers-connected")).toHaveTextContent("0");
  expect(screen.getByTestId("last-sweep")).toHaveTextContent("never");
  expect(screen.getByTestId("coverage-note")).toHaveTextContent(
    "manual-registration only",
  );
});

test("provider surfaces expose state, freshness, and evidenced capabilities", () => {
  const coverage: CoverageView = {
    providersConnected: 1,
    assetsKnown: 2,
    lastSuccessfulSweep: "2026-07-22T12:00:00Z",
    coverageNote: "Coverage is evidenced by 1 provider connection(s).",
    asOf: "2026-07-22T12:01:00Z",
    surfaces: [
      {
        connectionRef: "conn-1",
        surfaceId: "manual-csv",
        coveredCapabilities: ["Inventory"],
        state: "Connected",
        isFresh: true,
        lastSuccessfulSweep: "2026-07-22T12:00:00Z",
        observed: 2,
        new: 2,
        changed: 0,
        suppressed: 0,
      },
    ],
  };
  render(<TrustArea coverage={coverage} />);
  expect(screen.getByLabelText("Provider coverage")).toHaveTextContent(
    "manual-csv",
  );
  expect(screen.getByLabelText("Provider coverage")).toHaveTextContent("fresh");
  expect(screen.getByLabelText("Provider coverage")).toHaveTextContent(
    "Inventory",
  );
});

test("privileged access history is customer-visible", () => {
  const coverage: CoverageView = {
    providersConnected: 0,
    assetsKnown: 0,
    lastSuccessfulSweep: null,
    coverageNote: "Unknown",
    asOf: "2026-07-22T00:00:00Z",
    surfaces: [],
  };
  render(
    <TrustArea
      coverage={coverage}
      privilegedAccess={[
        {
          accessId: "a1",
          actor: "alex",
          purpose: "Support investigation",
          resource: "trust.privileged-access-log",
          occurredAt: "2026-07-22T12:00:00Z",
          correlationId: "trace-1",
        },
      ]}
    />,
  );
  expect(screen.getByLabelText("Privileged access log")).toHaveTextContent(
    "alex",
  );
  expect(screen.getByLabelText("Privileged access log")).toHaveTextContent(
    "Support investigation",
  );
});
