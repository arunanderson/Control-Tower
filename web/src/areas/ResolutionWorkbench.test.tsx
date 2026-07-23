import { render, screen, fireEvent } from "@testing-library/react";
import { ResolutionWorkbench } from "./ResolutionWorkbench";
import type { MergeCaseView } from "../api/types";

const collision: MergeCaseView = {
  mergeCaseId: "case-1",
  reason: "Identifier sys:t maps to 2 assets — collision.",
  confidence: "Low",
  identifiers: [{ system: "sys", identifierType: "t", value: "X" }],
  candidateAssetIds: ["a", "b"],
  observationRef: null,
  openedAt: "2026-07-22T00:00:00Z",
};

test("shows the merge-case queue with its honest confidence label", () => {
  render(
    <ResolutionWorkbench
      mergeCases={[collision]}
      canResolve={false}
      onResolve={() => {}}
    />,
  );
  expect(screen.getByTestId("merge-case")).toBeInTheDocument();
  expect(screen.getByTestId("case-confidence")).toHaveTextContent("Low");
  expect(screen.queryByText("Resolve")).not.toBeInTheDocument();
});

test("an empty queue is stated plainly", () => {
  render(
    <ResolutionWorkbench
      mergeCases={[]}
      canResolve={false}
      onResolve={() => {}}
    />,
  );
  expect(screen.getByTestId("queue-empty")).toBeInTheDocument();
});

test("the resolve action invokes the operator callback with the case id", () => {
  let resolved = "";
  render(
    <ResolutionWorkbench
      mergeCases={[collision]}
      canResolve
      onResolve={(id) => (resolved = id)}
    />,
  );
  fireEvent.click(screen.getByText("Resolve"));
  expect(resolved).toBe("case-1");
});
