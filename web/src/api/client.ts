import type {
  AssetLedgerView,
  CoverageView,
  ExecutiveEconomicsView,
  GovernanceCaseView,
  GovernanceDebtView,
  RoiView,
} from "./types";

// Thin read-only client over the Experience API. It only fetches read models; it performs no
// calculation and holds no business logic. Tenant is carried as a header (dev; Entra token in prod).
export interface ControlTowerApi {
  getAssets(): Promise<AssetLedgerView[]>;
  getAssetRecord(id: string): Promise<AssetLedgerView | null>;
  getExecutive(): Promise<ExecutiveEconomicsView>;
  getPortfolioRoi(): Promise<RoiView>;
  getDepartmentRoi(): Promise<RoiView[]>;
  getAgentRoi(): Promise<RoiView>;
  getGovernanceCases(): Promise<GovernanceCaseView[]>;
  getGovernanceDebt(): Promise<GovernanceDebtView[]>;
  getCoverage(): Promise<CoverageView>;
}

export class HttpControlTowerApi implements ControlTowerApi {
  constructor(
    private readonly baseUrl: string,
    private readonly tenantId: string,
  ) {}

  private async get<T>(path: string): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      headers: { "X-Tenant-Id": this.tenantId },
    });
    if (!response.ok) throw new Error(`GET ${path} failed: ${response.status}`);
    return (await response.json()) as T;
  }

  getAssets = () => this.get<AssetLedgerView[]>("/api/portfolio/assets");
  getAssetRecord = async (id: string) => {
    const response = await fetch(`${this.baseUrl}/api/portfolio/assets/${id}`, {
      headers: { "X-Tenant-Id": this.tenantId },
    });
    if (response.status === 404) return null;
    if (!response.ok) throw new Error(`asset record failed: ${response.status}`);
    return (await response.json()) as AssetLedgerView;
  };
  getExecutive = () => this.get<ExecutiveEconomicsView>("/api/economics/executive");
  getPortfolioRoi = () => this.get<RoiView>("/api/economics/portfolio");
  getDepartmentRoi = () => this.get<RoiView[]>("/api/economics/departments");
  getAgentRoi = () => this.get<RoiView>("/api/economics/agents");
  getGovernanceCases = () => this.get<GovernanceCaseView[]>("/api/governance/cases");
  getGovernanceDebt = () => this.get<GovernanceDebtView[]>("/api/governance/debt");
  getCoverage = () => this.get<CoverageView>("/api/trust/coverage");
}
