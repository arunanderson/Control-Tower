import type { GovernanceCaseView, GovernanceDebtView } from "../api/types";

export function GovernanceArea({
  cases,
  debt,
}: {
  cases: GovernanceCaseView[];
  debt: GovernanceDebtView[];
}) {
  return (
    <section>
      <h2>Governance</h2>

      <h3>Cases</h3>
      <table>
        <thead>
          <tr>
            <th>Type</th>
            <th>Risk</th>
            <th>Status</th>
            <th>Reviewers</th>
            <th>SLA</th>
            <th>Decision</th>
          </tr>
        </thead>
        <tbody>
          {cases.map((c) => (
            <tr key={c.caseId} data-testid="case-row">
              <td>{c.type}</td>
              <td>{c.riskTier}</td>
              <td>{c.status}</td>
              <td>{c.requiredReviewers.join(", ") || "auto"}</td>
              <td>
                {c.slaBreached ? (
                  <strong data-testid="sla-breach">SLA breached</strong>
                ) : (
                  "on time"
                )}
              </td>
              <td data-testid="case-outcome">
                {c.reuseAction ? `reuse: ${c.reuseAction}` : (c.outcome ?? "—")}
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <h3>Governance debt</h3>
      {debt.length === 0 ? (
        <p data-testid="no-debt">No open governance debt.</p>
      ) : (
        <ul>
          {debt.map((d, i) => (
            <li key={`${d.assetId}-${i}`} data-testid="debt-row">
              {d.debtType} — {d.isOpen ? "open" : "resolved"}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
