import type { AssetLedgerView } from "../api/types";

// The single polymorphic Asset Record — one experience for every AI asset type (ADR-019.4).
export function AssetRecord({ asset }: { asset: AssetLedgerView }) {
  return (
    <div className="asset-record" data-testid="asset-record">
      <h3>{asset.displayName}</h3>
      <dl>
        <dt>Type</dt>
        <dd>{asset.assetType}</dd>
        <dt>Registration</dt>
        <dd>{asset.registrationStatus}</dd>
        <dt>Lifecycle</dt>
        <dd>{asset.operationalLifecycleState}</dd>
        <dt>Match confidence</dt>
        <dd data-testid="match-confidence">{asset.matchConfidence}</dd>
        <dt>Owner</dt>
        <dd data-testid="owner">
          {asset.isOwnerless ? (
            <strong data-testid="ownerless">Ownerless</strong>
          ) : (
            asset.ownerDisplayName
          )}
        </dd>
        <dt>Business purpose</dt>
        <dd>{asset.businessPurpose ?? "—"}</dd>
        <dt>Resolution links</dt>
        <dd>{asset.resolutionLinkCount}</dd>
      </dl>
    </div>
  );
}

export function PortfolioArea({
  assets,
  selectedId,
  onSelect,
}: {
  assets: AssetLedgerView[];
  selectedId?: string;
  onSelect: (id: string) => void;
}) {
  const selected = assets.find((a) => a.assetId === selectedId);
  return (
    <section>
      <h2>Portfolio</h2>
      <table>
        <thead>
          <tr>
            <th>Name</th>
            <th>Type</th>
            <th>Status</th>
            <th>Confidence</th>
            <th>Owner</th>
          </tr>
        </thead>
        <tbody>
          {assets.map((a) => (
            <tr
              key={a.assetId}
              data-testid="asset-row"
              onClick={() => onSelect(a.assetId)}
            >
              <td>{a.displayName}</td>
              <td>{a.assetType}</td>
              <td>{a.registrationStatus}</td>
              <td>{a.matchConfidence}</td>
              <td>{a.isOwnerless ? "Ownerless" : a.ownerDisplayName}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {selected && <AssetRecord asset={selected} />}
    </section>
  );
}
