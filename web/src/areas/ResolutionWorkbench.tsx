import type { MergeCaseView } from "../api/types";

// The Resolution & Merge Workbench. It displays the manual merge queue (identifier collisions and
// ambiguous matches that were NOT auto-linked) with honest confidence labels, and offers the operator
// a resolve action. It holds no business logic: it renders read models and invokes the resolution API,
// which emits the audit events. Low/ambiguous matches reach here precisely because they never auto-link.
export function ResolutionWorkbench({
  mergeCases,
  onResolve,
}: {
  mergeCases: MergeCaseView[];
  onResolve: (id: string, outcome: string) => void;
}) {
  return (
    <section>
      <h2>Resolution &amp; Merge Workbench</h2>
      {mergeCases.length === 0 ? (
        <p data-testid="queue-empty">
          No open merge cases — nothing awaiting a human decision.
        </p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Identifier</th>
              <th>Reason</th>
              <th>Confidence</th>
              <th>Candidates</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {mergeCases.map((c) => (
              <tr key={c.mergeCaseId} data-testid="merge-case">
                <td>
                  {c.identifiers
                    .map((i) => `${i.system}:${i.identifierType}:${i.value}`)
                    .join(", ")}
                </td>
                <td>{c.reason}</td>
                <td data-testid="case-confidence">{c.confidence}</td>
                <td>{c.candidateAssetIds.length}</td>
                <td>
                  <button onClick={() => onResolve(c.mergeCaseId, "reviewed")}>
                    Resolve
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
