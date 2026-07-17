import { useEffect, useMemo, useState } from "react";
import { HttpControlTowerApi } from "./api/client";
import type {
  AssetLedgerView,
  CoverageView,
  ExecutiveEconomicsView,
  GovernanceCaseView,
  GovernanceDebtView,
  RoiView,
} from "./api/types";
import { ExecutiveDashboard } from "./areas/ExecutiveDashboard";
import { PortfolioArea } from "./areas/PortfolioArea";
import { EconomicsArea } from "./areas/EconomicsArea";
import { GovernanceArea } from "./areas/GovernanceArea";
import { TrustArea } from "./areas/TrustArea";
import { AdministrationArea } from "./areas/AdministrationArea";

type Area = "Portfolio" | "Economics" | "Governance" | "Trust" | "Administration";
const AREAS: Area[] = ["Portfolio", "Economics", "Governance", "Trust", "Administration"];

function devTenantId(): string {
  const key = "ct-dev-tenant";
  let id = localStorage.getItem(key);
  if (!id) {
    id = crypto.randomUUID();
    localStorage.setItem(key, id);
  }
  return id;
}

interface Loaded {
  assets: AssetLedgerView[];
  executive: ExecutiveEconomicsView;
  portfolio: RoiView;
  departments: RoiView[];
  cases: GovernanceCaseView[];
  debt: GovernanceDebtView[];
  coverage: CoverageView;
}

export function App() {
  const api = useMemo(() => new HttpControlTowerApi("", devTenantId()), []);
  const [area, setArea] = useState<Area>("Portfolio");
  const [selectedAsset, setSelectedAsset] = useState<string | undefined>();
  const [data, setData] = useState<Loaded | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([
      api.getAssets(),
      api.getExecutive(),
      api.getPortfolioRoi(),
      api.getDepartmentRoi(),
      api.getGovernanceCases(),
      api.getGovernanceDebt(),
      api.getCoverage(),
    ])
      .then(([assets, executive, portfolio, departments, cases, debt, coverage]) =>
        setData({ assets, executive, portfolio, departments, cases, debt, coverage }),
      )
      .catch((e) => setError(String(e)));
  }, [api]);

  return (
    <main>
      <header>
        <h1>Enterprise AI Control Tower</h1>
        <nav>
          {AREAS.map((a) => (
            <button key={a} onClick={() => setArea(a)} aria-current={area === a}>
              {a}
            </button>
          ))}
        </nav>
      </header>

      {error && <p role="alert">{error}</p>}
      {!data && !error && <p>Loading…</p>}

      {data && area === "Portfolio" && (
        <>
          <ExecutiveDashboard view={data.executive} />
          <PortfolioArea assets={data.assets} selectedId={selectedAsset} onSelect={setSelectedAsset} />
        </>
      )}
      {data && area === "Economics" && <EconomicsArea portfolio={data.portfolio} departments={data.departments} />}
      {data && area === "Governance" && <GovernanceArea cases={data.cases} debt={data.debt} />}
      {data && area === "Trust" && <TrustArea coverage={data.coverage} />}
      {data && area === "Administration" && (
        <AdministrationArea summary={{ tenant: devTenantId(), areas: AREAS, readModelOnly: true }} />
      )}
    </main>
  );
}
