import type { CoverageView } from "../api/types";

// The Trust area makes coverage honest: it shows what the platform can and cannot see. Overstated
// coverage is worse than none, so "no providers connected" is displayed plainly.
export function TrustArea({ coverage }: { coverage: CoverageView }) {
  return (
    <section>
      <h2>Trust &amp; Coverage</h2>
      <div data-testid="providers-connected">
        Providers connected: {coverage.providersConnected}
      </div>
      <div data-testid="assets-known">Assets known: {coverage.assetsKnown}</div>
      <div data-testid="last-sweep">
        Last successful sweep:{" "}
        {coverage.lastSuccessfulSweep
          ? coverage.lastSuccessfulSweep.slice(0, 10)
          : "never"}
      </div>
      <p data-testid="coverage-note">
        <em>{coverage.coverageNote}</em>
      </p>
      <ul aria-label="Provider coverage">
        {coverage.surfaces.map((surface) => (
          <li key={`${surface.connectionRef}:${surface.surfaceId}`}>
            <strong>{surface.surfaceId}</strong>: {surface.state}, {surface.isFresh ? "fresh" : "stale"}
            {" — "}{surface.coveredCapabilities.join(", ") || "no capabilities evidenced"}
          </li>
        ))}
      </ul>
    </section>
  );
}
