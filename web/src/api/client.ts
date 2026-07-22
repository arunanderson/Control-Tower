import type {
  AssetLedgerView,
  AssetResolutionView,
  CoverageView,
  ExecutiveEconomicsView,
  GovernanceCaseView,
  GovernanceDebtView,
  MergeCaseView,
  PrivilegedAccessView,
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
  getPrivilegedAccess(): Promise<PrivilegedAccessView[]>;
  getMergeCases(): Promise<MergeCaseView[]>;
  getAssetResolution(id: string): Promise<AssetResolutionView | null>;
  // Operator actions — commands routed to the C1 resolution service (event-driven, auditable).
  resolveMergeCase(id: string, outcome: string): Promise<void>;
  mergeAssets(targetId: string, sourceId: string): Promise<void>;
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
    if (!response.ok)
      throw new Error(`asset record failed: ${response.status}`);
    return (await response.json()) as AssetLedgerView;
  };
  getExecutive = () =>
    this.get<ExecutiveEconomicsView>("/api/economics/executive");
  getPortfolioRoi = () => this.get<RoiView>("/api/economics/portfolio");
  getDepartmentRoi = () => this.get<RoiView[]>("/api/economics/departments");
  getAgentRoi = () => this.get<RoiView>("/api/economics/agents");
  getGovernanceCases = () =>
    this.get<GovernanceCaseView[]>("/api/governance/cases");
  getGovernanceDebt = () =>
    this.get<GovernanceDebtView[]>("/api/governance/debt");
  getCoverage = () => this.get<CoverageView>("/api/trust/coverage");
  getPrivilegedAccess = async () => {
    const response = await fetch(
      `${this.baseUrl}/api/trust/privileged-access`,
      {
        headers: {
          "X-Tenant-Id": this.tenantId,
          "X-Actor": "development-user",
          "X-Purpose": "Review privileged access history",
        },
      },
    );
    if (!response.ok)
      throw new Error(`privileged access failed: ${response.status}`);
    return (await response.json()) as PrivilegedAccessView[];
  };
  getMergeCases = () =>
    this.get<MergeCaseView[]>("/api/resolution/merge-cases");
  getAssetResolution = async (id: string) => {
    const response = await fetch(
      `${this.baseUrl}/api/resolution/assets/${id}`,
      {
        headers: { "X-Tenant-Id": this.tenantId },
      },
    );
    if (response.status === 404) return null;
    if (!response.ok)
      throw new Error(`asset resolution failed: ${response.status}`);
    return (await response.json()) as AssetResolutionView;
  };

  private async post(path: string, body: unknown): Promise<void> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: "POST",
      headers: {
        "X-Tenant-Id": this.tenantId,
        "Content-Type": "application/json",
      },
      body: JSON.stringify(body),
    });
    if (!response.ok)
      throw new Error(`POST ${path} failed: ${response.status}`);
  }

  resolveMergeCase = (id: string, outcome: string) =>
    this.post(`/api/resolution/merge-cases/${id}/resolve`, { outcome });
  mergeAssets = (targetId: string, sourceId: string) =>
    this.post("/api/resolution/merge", { targetId, sourceId });
}
