import { useEffect, useRef, useState, type ReactNode } from "react";
import {
  ControlTowerClientError,
  NoAccessError,
  type AdministrationSummary,
  type ControlTowerApi,
  type ControlTowerCapability,
  type ControlTowerSession,
} from "./api/client";
import type {
  AssetLedgerView,
  CoverageView,
  ExecutiveEconomicsView,
  GovernanceCaseView,
  GovernanceDebtView,
  MergeCaseView,
  PrivilegedAccessView,
  RoiView,
} from "./api/types";
import type { AuthenticationState, MsalAuthenticationAdapter } from "./auth";
import { AdministrationArea } from "./areas/AdministrationArea";
import { EconomicsArea } from "./areas/EconomicsArea";
import { ExecutiveDashboard } from "./areas/ExecutiveDashboard";
import { GovernanceArea } from "./areas/GovernanceArea";
import { PortfolioArea } from "./areas/PortfolioArea";
import { ResolutionWorkbench } from "./areas/ResolutionWorkbench";
import { TrustArea } from "./areas/TrustArea";

export type Area =
  "Portfolio" | "Economics" | "Governance" | "Trust" | "Administration";

interface Loaded {
  session: ControlTowerSession;
  areas: readonly Area[];
  assets?: AssetLedgerView[];
  executive?: ExecutiveEconomicsView;
  portfolio?: RoiView;
  departments?: RoiView[];
  cases?: GovernanceCaseView[];
  debt?: GovernanceDebtView[];
  coverage?: CoverageView;
  mergeCases?: MergeCaseView[];
  privilegedAccess?: PrivilegedAccessView[];
  administration?: AdministrationSummary;
}

type LoadState =
  | { kind: "idle" }
  | { kind: "loading" }
  | { kind: "loaded"; value: Loaded }
  | {
      kind: "reauthentication-required" | "forbidden" | "no-access" | "error";
      message: string;
    };

export function App({
  authentication,
  api,
}: {
  authentication: MsalAuthenticationAdapter;
  api: ControlTowerApi;
}) {
  const [authenticationState, setAuthenticationState] =
    useState<AuthenticationState>(authentication.state);
  const [loadState, setLoadState] = useState<LoadState>({ kind: "idle" });
  const [area, setArea] = useState<Area>();
  const [selectedAsset, setSelectedAsset] = useState<string>();
  const [loadAttempt, setLoadAttempt] = useState(0);
  const load = useRef<Promise<Loaded>>();

  useEffect(() => {
    if (authenticationState.kind !== "authenticated") return;

    load.current ??= loadAuthorizedExperience(api);
    const currentLoad = load.current;
    let active = true;
    setLoadState({ kind: "loading" });
    void currentLoad.then(
      (value) => {
        if (!active) return;
        setArea((current) =>
          current !== undefined && value.areas.includes(current)
            ? current
            : value.areas[0],
        );
        setLoadState({ kind: "loaded", value });
      },
      (error: unknown) => {
        if (active) setLoadState(loadFailure(error));
      },
    );
    return () => {
      active = false;
    };
  }, [api, authenticationState.kind, loadAttempt]);

  const beginSignIn = async () => {
    try {
      await authentication.signIn();
    } catch {
      setLoadState({
        kind: "error",
        message: "Sign-in could not be started. Please try again.",
      });
    }
  };

  const selectAccount = (id: string) => {
    api.clearSession();
    load.current = undefined;
    setLoadState({ kind: "idle" });
    setAuthenticationState(authentication.selectAccount(id));
  };

  const reauthenticate = async () => {
    try {
      await authentication.reauthenticate();
    } catch {
      setLoadState({
        kind: "error",
        message: "Reauthentication could not be started. Please try again.",
      });
    }
  };

  const retry = () => {
    api.clearSession();
    load.current = undefined;
    setLoadState({ kind: "idle" });
    setLoadAttempt((current) => current + 1);
  };

  const logout = async () => {
    api.clearSession();
    load.current = undefined;
    setArea(undefined);
    setSelectedAsset(undefined);
    setLoadState({ kind: "idle" });
    const pending = authentication.logout();
    setAuthenticationState(authentication.state);
    try {
      await pending;
    } catch {
      setLoadState({
        kind: "error",
        message: "Sign-out could not be completed. Please try again.",
      });
    }
  };

  if (authenticationState.kind === "uninitialized") {
    return <AuthenticationMessage message="Authentication is starting…" />;
  }

  if (authenticationState.kind === "signed-out") {
    return (
      <AuthenticationMessage
        message="Sign in with your work account to open the Control Tower."
        action={<button onClick={beginSignIn}>Sign in</button>}
        error={loadState.kind === "error" ? loadState.message : undefined}
      />
    );
  }

  if (authenticationState.kind === "account-selection-required") {
    const labels = authenticationState.accounts.map(accountLabel);
    const localChoicesAreDistinct = new Set(labels).size === labels.length;
    return (
      <AuthenticationMessage
        message="Choose the work account for this Control Tower session."
        action={
          <>
            {localChoicesAreDistinct &&
              authenticationState.accounts.map((account, index) => (
                <button
                  key={account.id}
                  onClick={() => selectAccount(account.id)}
                >
                  {labels[index]}
                </button>
              ))}
            <button onClick={beginSignIn}>
              {localChoicesAreDistinct
                ? "Use another account"
                : "Choose account with Microsoft"}
            </button>
          </>
        }
        error={loadState.kind === "error" ? loadState.message : undefined}
      />
    );
  }

  if (loadState.kind === "idle" || loadState.kind === "loading") {
    return <AuthenticationMessage message="Loading your Control Tower…" />;
  }

  if (loadState.kind === "reauthentication-required") {
    return (
      <AuthenticationMessage
        message={loadState.message}
        action={
          <>
            <button onClick={reauthenticate}>Reauthenticate</button>
            <button onClick={logout}>Sign out</button>
          </>
        }
      />
    );
  }

  if (loadState.kind === "error") {
    return (
      <AuthenticationMessage
        message={loadState.message}
        action={
          <>
            <button onClick={retry}>Retry</button>
            <button onClick={logout}>Sign out</button>
          </>
        }
      />
    );
  }

  if (loadState.kind === "forbidden" || loadState.kind === "no-access") {
    return (
      <AuthenticationMessage
        message={loadState.message}
        action={<button onClick={logout}>Sign out</button>}
      />
    );
  }

  if (loadState.kind !== "loaded") return null;
  const data = loadState.value;
  const capabilities = new Set(data.session.capabilities);
  const resolveMergeCase = async (id: string, outcome: string) => {
    try {
      await api.resolveMergeCase(id, outcome);
      const mergeCases = await api.getMergeCases();
      setLoadState({
        kind: "loaded",
        value: { ...data, mergeCases },
      });
    } catch (error: unknown) {
      setLoadState(loadFailure(error));
    }
  };

  return (
    <main>
      <header>
        <h1>Enterprise AI Control Tower</h1>
        <p data-testid="session-tenant">Tenant: {data.session.tenant}</p>
        <p data-testid="session-roles">
          Access: {data.session.roles.join(", ")}
        </p>
        <nav>
          {data.areas.map((availableArea) => (
            <button
              key={availableArea}
              onClick={() => setArea(availableArea)}
              aria-current={area === availableArea}
            >
              {availableArea}
            </button>
          ))}
          <button onClick={logout}>Sign out</button>
        </nav>
      </header>

      {area === "Portfolio" && (
        <>
          {data.executive && <ExecutiveDashboard view={data.executive} />}
          {data.assets && (
            <PortfolioArea
              assets={data.assets}
              selectedId={selectedAsset}
              onSelect={setSelectedAsset}
            />
          )}
        </>
      )}
      {area === "Economics" && data.portfolio && (
        <EconomicsArea
          portfolio={data.portfolio}
          departments={data.departments}
        />
      )}
      {area === "Governance" && data.cases && data.debt && (
        <GovernanceArea cases={data.cases} debt={data.debt} />
      )}
      {area === "Trust" && data.coverage && (
        <>
          <TrustArea
            coverage={data.coverage}
            privilegedAccess={data.privilegedAccess}
          />
          {data.mergeCases && (
            <ResolutionWorkbench
              mergeCases={data.mergeCases}
              canResolve={capabilities.has("resolution.manage")}
              onResolve={resolveMergeCase}
            />
          )}
        </>
      )}
      {area === "Administration" && data.administration && (
        <AdministrationArea summary={data.administration} />
      )}
    </main>
  );
}

async function loadAuthorizedExperience(api: ControlTowerApi): Promise<Loaded> {
  const session = await api.getSession();
  const capabilities = new Set(session.capabilities);
  const areas = availableAreas(capabilities);
  if (areas.length === 0) throw new NoAccessError();

  const [
    assets,
    executive,
    portfolio,
    departments,
    cases,
    debt,
    coverage,
    mergeCases,
    privilegedAccess,
    administration,
  ] = await Promise.all([
    optional(capabilities, "portfolio.read", () => api.getAssets()),
    optional(capabilities, "economics.executive.read", () =>
      api.getExecutive(),
    ),
    optional(capabilities, "economics.portfolio.read", () =>
      api.getPortfolioRoi(),
    ),
    optional(capabilities, "economics.detail.read", () =>
      api.getDepartmentRoi(),
    ),
    optional(capabilities, "governance.read", () => api.getGovernanceCases()),
    optional(capabilities, "governance.read", () => api.getGovernanceDebt()),
    optional(capabilities, "trust.coverage.read", () => api.getCoverage()),
    optional(capabilities, "resolution.read", () => api.getMergeCases()),
    optional(capabilities, "trust.privileged-access.read", () =>
      api.getPrivilegedAccess(),
    ),
    optional(capabilities, "administration.read", () =>
      api.getAdministrationSummary(),
    ),
  ]);

  return {
    session,
    areas,
    assets,
    executive,
    portfolio,
    departments,
    cases,
    debt,
    coverage,
    mergeCases,
    privilegedAccess,
    administration,
  };
}

function availableAreas(
  capabilities: ReadonlySet<ControlTowerCapability>,
): readonly Area[] {
  const areas: Area[] = [];
  if (
    capabilities.has("portfolio.read") ||
    capabilities.has("economics.executive.read")
  ) {
    areas.push("Portfolio");
  }
  if (
    capabilities.has("economics.portfolio.read") ||
    capabilities.has("economics.detail.read")
  ) {
    areas.push("Economics");
  }
  if (capabilities.has("governance.read")) areas.push("Governance");
  if (
    capabilities.has("trust.coverage.read") ||
    capabilities.has("trust.privileged-access.read") ||
    capabilities.has("resolution.read")
  ) {
    areas.push("Trust");
  }
  if (capabilities.has("administration.read")) {
    areas.push("Administration");
  }
  return Object.freeze(areas);
}

function optional<T>(
  capabilities: ReadonlySet<ControlTowerCapability>,
  capability: ControlTowerCapability,
  load: () => Promise<T>,
): Promise<T | undefined> {
  return capabilities.has(capability) ? load() : Promise.resolve(undefined);
}

function loadFailure(error: unknown): LoadState {
  if (error instanceof ControlTowerClientError) {
    if (
      error.kind === "reauthentication-required" ||
      error.kind === "unauthenticated"
    ) {
      return {
        kind: "reauthentication-required",
        message: "Your session must be reauthenticated.",
      };
    }
    if (error.kind === "forbidden") {
      return {
        kind: "forbidden",
        message: "This Control Tower view is not available to your account.",
      };
    }
    if (error.kind === "no-access" || error.kind === "invalid-response") {
      return {
        kind: "no-access",
        message: "No Control Tower access is assigned to this account.",
      };
    }
  }

  return {
    kind: "error",
    message: "The Control Tower could not be loaded. Please try again later.",
  };
}

function AuthenticationMessage({
  message,
  action,
  error,
}: {
  message: string;
  action?: ReactNode;
  error?: string;
}) {
  return (
    <main>
      <h1>Enterprise AI Control Tower</h1>
      <p>{message}</p>
      {action}
      {error && <p role="alert">{error}</p>}
    </main>
  );
}

function accountLabel(account: {
  readonly username: string;
  readonly name?: string;
}): string {
  return account.name === undefined
    ? account.username
    : `${account.name} (${account.username})`;
}
