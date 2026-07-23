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

export type AccessTokenProvider = () => Promise<string>;

export type ControlTowerRole =
  "Viewer" | "Operator" | "Administrator" | "Executive-scope";

export type ControlTowerCapability =
  | "portfolio.read"
  | "economics.executive.read"
  | "economics.portfolio.read"
  | "economics.detail.read"
  | "economics.reporting-periods.read"
  | "economics.reporting-periods.manage"
  | "governance.read"
  | "trust.coverage.read"
  | "trust.privileged-access.read"
  | "trust.legal-holds.read"
  | "trust.legal-holds.manage"
  | "administration.read"
  | "resolution.read"
  | "resolution.manage"
  | "ledger.manage";

export interface ControlTowerSession {
  readonly tenant: string;
  readonly directoryTenant: string;
  readonly actor: string;
  readonly roles: readonly ControlTowerRole[];
  readonly capabilities: readonly ControlTowerCapability[];
  readonly organizationScope: "TenantWide";
}

export interface AdministrationSummary {
  readonly tenant: string;
  readonly areas: readonly string[];
  readonly readModelOnly: boolean;
}

export type ControlTowerClientErrorKind =
  | "access-token"
  | "reauthentication-required"
  | "unauthenticated"
  | "forbidden"
  | "not-found"
  | "server"
  | "network"
  | "invalid-response"
  | "no-access";

export class ControlTowerClientError extends Error {
  constructor(
    readonly kind: ControlTowerClientErrorKind,
    message: string,
    readonly status?: number,
  ) {
    super(message);
    this.name = new.target.name;
  }
}

export class AccessTokenAcquisitionError extends ControlTowerClientError {
  constructor() {
    super("access-token", "Unable to acquire an access token.");
  }
}

export class ReauthenticationRequiredClientError extends ControlTowerClientError {
  constructor() {
    super(
      "reauthentication-required",
      "Interactive reauthentication is required.",
    );
  }
}

export class UnauthenticatedError extends ControlTowerClientError {
  constructor() {
    super("unauthenticated", "Authentication is required.", 401);
  }
}

export class ForbiddenError extends ControlTowerClientError {
  constructor() {
    super("forbidden", "You do not have access to this resource.", 403);
  }
}

export class NotFoundError extends ControlTowerClientError {
  constructor() {
    super("not-found", "The requested resource was not found.", 404);
  }
}

export class ServiceResponseError extends ControlTowerClientError {
  constructor(status: number) {
    super("server", "The Control Tower service request failed.", status);
  }
}

export class NetworkRequestError extends ControlTowerClientError {
  constructor() {
    super("network", "The Control Tower service could not be reached.");
  }
}

export class InvalidResponseError extends ControlTowerClientError {
  constructor() {
    super(
      "invalid-response",
      "The Control Tower service returned an invalid response.",
    );
  }
}

export class NoAccessError extends ControlTowerClientError {
  constructor() {
    super("no-access", "No Control Tower access is assigned.");
  }
}

// Thin client over the C7 Experience API. Identity and authorization are resolved by the server:
// the browser supplies only an MSAL-managed delegated bearer token and bounded business context.
export interface ControlTowerApi {
  getSession(): Promise<ControlTowerSession>;
  clearSession(): void;
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
  getAdministrationSummary(): Promise<AdministrationSummary>;
  getMergeCases(): Promise<MergeCaseView[]>;
  getAssetResolution(id: string): Promise<AssetResolutionView | null>;
  resolveMergeCase(id: string, outcome: string): Promise<void>;
  mergeAssets(targetId: string, sourceId: string): Promise<void>;
}

const KNOWN_ROLES = new Set<ControlTowerRole>([
  "Viewer",
  "Operator",
  "Administrator",
  "Executive-scope",
]);

const KNOWN_CAPABILITIES = new Set<ControlTowerCapability>([
  "portfolio.read",
  "economics.executive.read",
  "economics.portfolio.read",
  "economics.detail.read",
  "economics.reporting-periods.read",
  "economics.reporting-periods.manage",
  "governance.read",
  "trust.coverage.read",
  "trust.privileged-access.read",
  "trust.legal-holds.read",
  "trust.legal-holds.manage",
  "administration.read",
  "resolution.read",
  "resolution.manage",
  "ledger.manage",
]);

const UUID =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const ZERO_UUID = "00000000-0000-0000-0000-000000000000";
const PRIVILEGED_ACCESS_PURPOSE = "Review privileged access history";

export class HttpControlTowerApi implements ControlTowerApi {
  private sessionPromise?: Promise<ControlTowerSession>;

  constructor(private readonly accessTokenProvider: AccessTokenProvider) {}

  getSession(): Promise<ControlTowerSession> {
    if (this.sessionPromise) return this.sessionPromise;

    const pending = this.requestJson<unknown>("/whoami").then(parseSession);
    this.sessionPromise = pending;
    void pending.catch(() => {
      if (this.sessionPromise === pending) this.sessionPromise = undefined;
    });
    return pending;
  }

  clearSession(): void {
    this.sessionPromise = undefined;
  }

  getAssets = () =>
    this.getAfterSession<AssetLedgerView[]>("/api/portfolio/assets");

  getAssetRecord = (id: string) =>
    this.getAfterSessionOrNull<AssetLedgerView>(
      `/api/portfolio/assets/${encodeURIComponent(id)}`,
    );

  getExecutive = () =>
    this.getAfterSession<ExecutiveEconomicsView>("/api/economics/executive");

  getPortfolioRoi = () =>
    this.getAfterSession<RoiView>("/api/economics/portfolio");

  getDepartmentRoi = () =>
    this.getAfterSession<RoiView[]>("/api/economics/departments");

  getAgentRoi = () => this.getAfterSession<RoiView>("/api/economics/agents");

  getGovernanceCases = () =>
    this.getAfterSession<GovernanceCaseView[]>("/api/governance/cases");

  getGovernanceDebt = () =>
    this.getAfterSession<GovernanceDebtView[]>("/api/governance/debt");

  getCoverage = () => this.getAfterSession<CoverageView>("/api/trust/coverage");

  getPrivilegedAccess = async () => {
    await this.getSession();
    return this.requestJson<PrivilegedAccessView[]>(
      "/api/trust/privileged-access",
      {
        headers: { "X-Purpose": PRIVILEGED_ACCESS_PURPOSE },
      },
    );
  };

  getAdministrationSummary = async () => {
    const session = await this.getSession();
    const summary = parseAdministrationSummary(
      await this.requestJson<unknown>("/api/admin/summary"),
    );
    if (summary.tenant.toLowerCase() !== session.tenant.toLowerCase()) {
      throw new InvalidResponseError();
    }
    return summary;
  };

  getMergeCases = () =>
    this.getAfterSession<MergeCaseView[]>("/api/resolution/merge-cases");

  getAssetResolution = (id: string) =>
    this.getAfterSessionOrNull<AssetResolutionView>(
      `/api/resolution/assets/${encodeURIComponent(id)}`,
    );

  resolveMergeCase = async (id: string, outcome: string) => {
    await this.postAfterSession(
      `/api/resolution/merge-cases/${encodeURIComponent(id)}/resolve`,
      { outcome },
    );
  };

  mergeAssets = async (targetId: string, sourceId: string) => {
    await this.postAfterSession("/api/resolution/merge", {
      targetId,
      sourceId,
    });
  };

  private async getAfterSession<T>(path: `/api/${string}`): Promise<T> {
    await this.getSession();
    return this.requestJson<T>(path);
  }

  private async getAfterSessionOrNull<T>(
    path: `/api/${string}`,
  ): Promise<T | null> {
    await this.getSession();
    try {
      return await this.requestJson<T>(path);
    } catch (error) {
      if (error instanceof NotFoundError) return null;
      throw error;
    }
  }

  private async postAfterSession(
    path: `/api/${string}`,
    body: unknown,
  ): Promise<void> {
    await this.getSession();
    await this.request(path, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
  }

  private async requestJson<T>(
    path: "/whoami" | `/api/${string}`,
    init?: RequestInit,
  ): Promise<T> {
    const response = await this.request(path, init);
    try {
      return (await response.json()) as T;
    } catch {
      throw new InvalidResponseError();
    }
  }

  private async request(
    path: "/whoami" | `/api/${string}`,
    init: RequestInit = {},
  ): Promise<Response> {
    const token = await this.acquireAccessToken();
    const headers = new Headers(init.headers);
    headers.set("Authorization", `Bearer ${token}`);

    let response: Response;
    try {
      response = await fetch(path, {
        ...init,
        headers,
        cache: "no-store",
        credentials: "omit",
        redirect: "error",
        referrerPolicy: "no-referrer",
      });
    } catch {
      throw new NetworkRequestError();
    }

    if (response.ok) return response;

    if (response.status === 401) {
      this.clearSession();
      throw new UnauthenticatedError();
    }
    if (response.status === 403) throw new ForbiddenError();
    if (response.status === 404) throw new NotFoundError();
    throw new ServiceResponseError(response.status);
  }

  private async acquireAccessToken(): Promise<string> {
    let token: unknown;
    try {
      token = await this.accessTokenProvider();
    } catch (error: unknown) {
      if (error instanceof ReauthenticationRequiredClientError) throw error;
      throw new AccessTokenAcquisitionError();
    }

    if (
      typeof token !== "string" ||
      token.length === 0 ||
      token.length > 65_536 ||
      /\s|[\u0000-\u001f\u007f]/u.test(token)
    ) {
      throw new AccessTokenAcquisitionError();
    }
    return token;
  }
}

function parseSession(value: unknown): ControlTowerSession {
  const record = requireRecord(value);
  const tenant = requireUuid(record.tenant);
  const directoryTenant = requireUuid(record.directoryTenant);
  const actor = requireString(record.actor);
  const roles = requireKnownArray(record.roles, KNOWN_ROLES);
  const capabilities = requireKnownArray(
    record.capabilities,
    KNOWN_CAPABILITIES,
  );

  if (roles.length === 0 || capabilities.length === 0) {
    throw new NoAccessError();
  }
  if (record.organizationScope !== "TenantWide") {
    throw new InvalidResponseError();
  }

  const actorParts = actor.split(":");
  if (
    actorParts.length !== 3 ||
    actorParts[0] !== "entra" ||
    !isUuid(actorParts[1]) ||
    !isUuid(actorParts[2]) ||
    actorParts[1].toLowerCase() !== directoryTenant.toLowerCase()
  ) {
    throw new InvalidResponseError();
  }

  return Object.freeze({
    tenant,
    directoryTenant,
    actor,
    roles: Object.freeze(roles),
    capabilities: Object.freeze(capabilities),
    organizationScope: "TenantWide" as const,
  });
}

function parseAdministrationSummary(value: unknown): AdministrationSummary {
  const record = requireRecord(value);
  const tenant = requireUuid(record.tenant);
  if (
    !Array.isArray(record.areas) ||
    record.areas.length === 0 ||
    record.areas.some(
      (area) => typeof area !== "string" || area.trim().length === 0,
    ) ||
    new Set(record.areas).size !== record.areas.length ||
    typeof record.readModelOnly !== "boolean"
  ) {
    throw new InvalidResponseError();
  }

  return Object.freeze({
    tenant,
    areas: Object.freeze([...record.areas] as string[]),
    readModelOnly: record.readModelOnly,
  });
}

function requireRecord(value: unknown): Record<string, unknown> {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    throw new InvalidResponseError();
  }
  return value as Record<string, unknown>;
}

function requireString(value: unknown): string {
  if (
    typeof value !== "string" ||
    value.length === 0 ||
    value.length > 512 ||
    /[\u0000-\u001f\u007f]/u.test(value)
  ) {
    throw new InvalidResponseError();
  }
  return value;
}

function requireUuid(value: unknown): string {
  const candidate = requireString(value);
  if (!isUuid(candidate)) throw new InvalidResponseError();
  return candidate;
}

function isUuid(value: string): boolean {
  return UUID.test(value) && value.toLowerCase() !== ZERO_UUID;
}

function requireKnownArray<T extends string>(
  value: unknown,
  known: ReadonlySet<T>,
): T[] {
  if (!Array.isArray(value)) throw new InvalidResponseError();

  const result: T[] = [];
  const seen = new Set<string>();
  for (const item of value) {
    if (typeof item !== "string" || !known.has(item as T) || seen.has(item)) {
      throw new InvalidResponseError();
    }
    seen.add(item);
    result.push(item as T);
  }
  return result;
}
