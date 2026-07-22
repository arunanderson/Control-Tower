import type { CoverageView, PrivilegedAccessView } from "../api/types";

// The Trust area makes coverage honest: it shows what the platform can and cannot see. Overstated
// coverage is worse than none, so "no providers connected" is displayed plainly.
export function TrustArea({
  coverage,
  privilegedAccess = [],
}: {
  coverage: CoverageView;
  privilegedAccess?: PrivilegedAccessView[];
}) {
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
            <strong>{surface.surfaceId}</strong>: {surface.state},{" "}
            {surface.isFresh ? "fresh" : "stale"}
            {" — "}
            {surface.coveredCapabilities.join(", ") ||
              "no capabilities evidenced"}
          </li>
        ))}
      </ul>
      <h3>Privileged access</h3>
      {privilegedAccess.length === 0 ? (
        <p data-testid="no-privileged-access">
          No prior privileged reads recorded.
        </p>
      ) : (
        <ul aria-label="Privileged access log">
          {privilegedAccess.map((entry) => (
            <li key={entry.accessId}>
              <strong>{entry.actor}</strong> read {entry.resource} for{" "}
              {entry.purpose} at {entry.occurredAt.slice(0, 10)}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
