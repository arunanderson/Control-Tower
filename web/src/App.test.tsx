import { StrictMode } from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import {
  AccessTokenAcquisitionError,
  ForbiddenError,
  NoAccessError,
  ReauthenticationRequiredClientError,
  UnauthenticatedError,
  type ControlTowerApi,
  type ControlTowerCapability,
  type ControlTowerRole,
  type ControlTowerSession,
} from "./api/client";
import type {
  CoverageView,
  EconomicAmount,
  ExecutiveEconomicsView,
  MergeCaseView,
  RoiView,
} from "./api/types";
import type { AuthenticationState, MsalAuthenticationAdapter } from "./auth";
import { App } from "./App";

const TENANT = "11111111-1111-4111-8111-111111111111";
const DIRECTORY_TENANT = "22222222-2222-4222-8222-222222222222";

function authentication(
  initial: AuthenticationState,
  logoutCompletion: Promise<void> = Promise.resolve(),
) {
  let state = initial;
  const adapter = {
    get state() {
      return state;
    },
    signIn: vi.fn(async () => {}),
    selectAccount: vi.fn(() => {
      state = {
        kind: "authenticated",
        account: { username: "selected@example.com" },
      };
      return state;
    }),
    acquireAccessToken: vi.fn(async () => "access-token"),
    reauthenticate: vi.fn(async () => {}),
    logout: vi.fn(() => {
      state = { kind: "signed-out" };
      return logoutCompletion;
    }),
  };
  return adapter as unknown as MsalAuthenticationAdapter;
}

function session(
  roles: readonly ControlTowerRole[],
  capabilities: readonly ControlTowerCapability[],
): ControlTowerSession {
  return {
    tenant: TENANT,
    directoryTenant: DIRECTORY_TENANT,
    actor: `entra:${DIRECTORY_TENANT}:33333333-3333-4333-8333-333333333333`,
    roles,
    capabilities,
    organizationScope: "TenantWide",
  };
}

function apiFor(currentSession: ControlTowerSession): ControlTowerApi {
  return {
    getSession: vi.fn(async () => currentSession),
    clearSession: vi.fn(),
    getAssets: vi.fn(async () => []),
    getAssetRecord: vi.fn(async () => null),
    getExecutive: vi.fn(async () => executive),
    getPortfolioRoi: vi.fn(async () => roi),
    getDepartmentRoi: vi.fn(async () => []),
    getAgentRoi: vi.fn(async () => roi),
    getGovernanceCases: vi.fn(async () => []),
    getGovernanceDebt: vi.fn(async () => []),
    getCoverage: vi.fn(async () => coverage),
    getPrivilegedAccess: vi.fn(async () => []),
    getAdministrationSummary: vi.fn(async () => ({
      tenant: TENANT,
      areas: ["Administration"],
      readModelOnly: true,
    })),
    getMergeCases: vi.fn(async () => [mergeCase]),
    getAssetResolution: vi.fn(async () => null),
    resolveMergeCase: vi.fn(async () => {}),
    mergeAssets: vi.fn(async () => {}),
  };
}

test("signed-out and multi-account states make no API request and require an explicit action", async () => {
  const signedOutAuthentication = authentication({ kind: "signed-out" });
  const api = apiFor(session(["Viewer"], ["trust.coverage.read"]));
  const { unmount } = render(
    <App authentication={signedOutAuthentication} api={api} />,
  );

  expect(
    screen.getByText(/Sign in with your work account/),
  ).toBeInTheDocument();
  expect(api.getSession).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole("button", { name: "Sign in" }));
  expect(signedOutAuthentication.signIn).toHaveBeenCalledTimes(1);
  unmount();

  const accountAuthentication = authentication({
    kind: "account-selection-required",
    accounts: [
      {
        id: "account-1",
        username: "one@example.com",
        name: "Work account",
      },
      { id: "account-2", username: "two@example.com" },
    ],
  });
  render(<App authentication={accountAuthentication} api={api} />);
  expect(api.getSession).not.toHaveBeenCalled();
  fireEvent.click(
    screen.getByRole("button", {
      name: "Work account (one@example.com)",
    }),
  );
  await screen.findByTestId("session-tenant");
  expect(accountAuthentication.selectAccount).toHaveBeenCalledWith("account-1");
});

test("ambiguous cached-account labels delegate explicit selection to Microsoft", () => {
  const auth = authentication({
    kind: "account-selection-required",
    accounts: [
      { id: "account-1", username: "same@example.com", name: "Same user" },
      { id: "account-2", username: "same@example.com", name: "Same user" },
    ],
  });
  const api = apiFor(session(["Viewer"], ["trust.coverage.read"]));

  render(<App authentication={auth} api={api} />);

  expect(
    screen.queryByRole("button", {
      name: "Same user (same@example.com)",
    }),
  ).not.toBeInTheDocument();
  fireEvent.click(
    screen.getByRole("button", { name: "Choose account with Microsoft" }),
  );
  expect(auth.signIn).toHaveBeenCalledTimes(1);
  expect(auth.selectAccount).not.toHaveBeenCalled();
  expect(api.getSession).not.toHaveBeenCalled();
});

test("Viewer loads whoami once before only its permitted reads, including under StrictMode", async () => {
  localStorage.setItem("ct-dev-tenant", "attacker-controlled");
  const api = apiFor(
    session(
      ["Viewer"],
      [
        "portfolio.read",
        "economics.executive.read",
        "economics.portfolio.read",
        "economics.detail.read",
        "economics.reporting-periods.read",
        "governance.read",
        "trust.coverage.read",
        "resolution.read",
      ],
    ),
  );
  const auth = authentication({
    kind: "authenticated",
    account: {
      username: "viewer@example.com",
      ...({ roles: ["Administrator"], tenant: "attacker" } as object),
    },
  });

  render(
    <StrictMode>
      <App authentication={auth} api={api} />
    </StrictMode>,
  );

  expect(await screen.findByTestId("session-tenant")).toHaveTextContent(TENANT);
  expect(screen.getByTestId("session-tenant")).not.toHaveTextContent(
    "attacker-controlled",
  );
  expect(api.getSession).toHaveBeenCalledTimes(1);
  expect(api.getAssets).toHaveBeenCalledTimes(1);
  expect(api.getExecutive).toHaveBeenCalledTimes(1);
  expect(api.getPortfolioRoi).toHaveBeenCalledTimes(1);
  expect(api.getDepartmentRoi).toHaveBeenCalledTimes(1);
  expect(api.getGovernanceCases).toHaveBeenCalledTimes(1);
  expect(api.getGovernanceDebt).toHaveBeenCalledTimes(1);
  expect(api.getCoverage).toHaveBeenCalledTimes(1);
  expect(api.getMergeCases).toHaveBeenCalledTimes(1);
  expect(api.getPrivilegedAccess).not.toHaveBeenCalled();
  expect(api.getAdministrationSummary).not.toHaveBeenCalled();
  expect(vi.mocked(api.getSession).mock.invocationCallOrder[0]).toBeLessThan(
    vi.mocked(api.getAssets).mock.invocationCallOrder[0],
  );
  expect(
    screen.queryByRole("button", { name: "Administration" }),
  ).not.toBeInTheDocument();

  fireEvent.click(screen.getByRole("button", { name: "Trust" }));
  expect(
    screen.queryByRole("button", { name: "Resolve" }),
  ).not.toBeInTheDocument();
  expect(
    screen.queryByRole("heading", { name: "Privileged access" }),
  ).not.toBeInTheDocument();
});

test("Operator exposes the resolution action without gaining administration", async () => {
  const api = apiFor(
    session(
      ["Operator"],
      [
        "portfolio.read",
        "economics.executive.read",
        "economics.portfolio.read",
        "economics.detail.read",
        "economics.reporting-periods.read",
        "economics.reporting-periods.manage",
        "governance.read",
        "trust.coverage.read",
        "resolution.read",
        "resolution.manage",
        "ledger.manage",
      ],
    ),
  );
  render(
    <App
      authentication={authentication({
        kind: "authenticated",
        account: { username: "operator@example.com" },
      })}
      api={api}
    />,
  );

  await screen.findByTestId("session-tenant");
  fireEvent.click(screen.getByRole("button", { name: "Trust" }));
  fireEvent.click(screen.getByRole("button", { name: "Resolve" }));
  await waitFor(() =>
    expect(api.resolveMergeCase).toHaveBeenCalledWith(
      mergeCase.mergeCaseId,
      "reviewed",
    ),
  );
  expect(api.getAdministrationSummary).not.toHaveBeenCalled();
  expect(
    screen.queryByRole("button", { name: "Administration" }),
  ).not.toBeInTheDocument();
});

test("Administrator requests only Trust and Administration data from the server session", async () => {
  const api = apiFor(
    session(
      ["Administrator"],
      [
        "trust.coverage.read",
        "trust.privileged-access.read",
        "trust.legal-holds.read",
        "trust.legal-holds.manage",
        "administration.read",
      ],
    ),
  );
  render(
    <App
      authentication={authentication({
        kind: "authenticated",
        account: { username: "admin@example.com" },
      })}
      api={api}
    />,
  );

  await screen.findByTestId("session-tenant");
  expect(api.getCoverage).toHaveBeenCalledTimes(1);
  expect(api.getPrivilegedAccess).toHaveBeenCalledTimes(1);
  expect(api.getAdministrationSummary).toHaveBeenCalledTimes(1);
  expect(api.getAssets).not.toHaveBeenCalled();
  expect(api.getExecutive).not.toHaveBeenCalled();
  expect(api.getPortfolioRoi).not.toHaveBeenCalled();
  expect(api.getDepartmentRoi).not.toHaveBeenCalled();
  expect(api.getGovernanceCases).not.toHaveBeenCalled();
  expect(api.getMergeCases).not.toHaveBeenCalled();
  expect(
    screen.queryByRole("button", { name: "Portfolio" }),
  ).not.toBeInTheDocument();
  expect(
    screen.getByRole("heading", { name: "Privileged access" }),
  ).toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: "Administration" }));
  expect(screen.getByTestId("admin-tenant")).toHaveTextContent(TENANT);
});

test("Executive-scope stays on prescribed aggregate paths", async () => {
  const api = apiFor(
    session(
      ["Executive-scope"],
      [
        "portfolio.read",
        "economics.executive.read",
        "economics.portfolio.read",
        "economics.reporting-periods.read",
        "trust.coverage.read",
      ],
    ),
  );
  render(
    <App
      authentication={authentication({
        kind: "authenticated",
        account: { username: "executive@example.com" },
      })}
      api={api}
    />,
  );

  await screen.findByTestId("session-tenant");
  expect(api.getAssets).toHaveBeenCalledTimes(1);
  expect(api.getExecutive).toHaveBeenCalledTimes(1);
  expect(api.getPortfolioRoi).toHaveBeenCalledTimes(1);
  expect(api.getCoverage).toHaveBeenCalledTimes(1);
  expect(api.getDepartmentRoi).not.toHaveBeenCalled();
  expect(api.getGovernanceCases).not.toHaveBeenCalled();
  expect(api.getMergeCases).not.toHaveBeenCalled();
  expect(api.getPrivilegedAccess).not.toHaveBeenCalled();
  expect(api.getAdministrationSummary).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole("button", { name: "Economics" }));
  expect(
    screen.queryByRole("heading", { name: "By department" }),
  ).not.toBeInTheDocument();
});

test.each([
  [
    "expired session",
    new UnauthenticatedError(),
    "Your session must be reauthenticated.",
    "Reauthenticate",
  ],
  [
    "forbidden session",
    new ForbiddenError(),
    "This Control Tower view is not available to your account.",
    "Sign out",
  ],
  [
    "missing assignment",
    new NoAccessError(),
    "No Control Tower access is assigned to this account.",
    "Sign out",
  ],
])(
  "%s fails before data calls with a generic explicit state",
  async (_name, failure, message, action) => {
    const api = apiFor(session([], []));
    vi.mocked(api.getSession).mockRejectedValue(failure);
    render(
      <App
        authentication={authentication({
          kind: "authenticated",
          account: { username: "user@example.com" },
        })}
        api={api}
      />,
    );

    expect(await screen.findByText(message)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: action })).toBeInTheDocument();
    expect(api.getAssets).not.toHaveBeenCalled();
    expect(api.getCoverage).not.toHaveBeenCalled();
    expect(document.body.textContent).not.toContain(TENANT);
  },
);

test.each([
  [
    "interaction required",
    new ReauthenticationRequiredClientError(),
    "Your session must be reauthenticated.",
    true,
  ],
  [
    "transient token failure",
    new AccessTokenAcquisitionError(),
    "The Control Tower could not be loaded. Please try again later.",
    false,
  ],
])(
  "%s has the correct explicit non-looping action",
  async (_name, failure, message, hasReauthentication) => {
    const api = apiFor(session(["Viewer"], ["trust.coverage.read"]));
    vi.mocked(api.getSession).mockRejectedValue(failure);
    const auth = authentication({
      kind: "authenticated",
      account: { username: "user@example.com" },
    });

    render(<App authentication={auth} api={api} />);

    expect(await screen.findByText(message)).toBeInTheDocument();
    if (hasReauthentication) {
      expect(
        screen.getByRole("button", { name: "Reauthenticate" }),
      ).toBeInTheDocument();
    } else {
      expect(
        screen.queryByRole("button", { name: "Reauthenticate" }),
      ).not.toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Retry" })).toBeInTheDocument();
    }
    expect(auth.reauthenticate).not.toHaveBeenCalled();
  },
);

test("sign-out clears the protected UI and API session before redirect completion", async () => {
  let completeLogout!: () => void;
  const pendingLogout = new Promise<void>((resolve) => {
    completeLogout = resolve;
  });
  const auth = authentication(
    {
      kind: "authenticated",
      account: { username: "viewer@example.com" },
    },
    pendingLogout,
  );
  const api = apiFor(session(["Viewer"], ["trust.coverage.read"]));
  render(<App authentication={auth} api={api} />);
  await screen.findByTestId("session-tenant");

  fireEvent.click(screen.getByRole("button", { name: "Sign out" }));

  expect(api.clearSession).toHaveBeenCalledTimes(1);
  expect(screen.queryByTestId("session-tenant")).not.toBeInTheDocument();
  expect(
    screen.getByText(/Sign in with your work account/),
  ).toBeInTheDocument();
  expect(auth.logout).toHaveBeenCalledTimes(1);

  completeLogout();
  await pendingLogout;
});

function amount(value: number): EconomicAmount {
  return {
    amount: value,
    currency: "EUR",
    evidenceClass: "Measured",
    source: "test",
    methodology: "test",
    asOf: "2026-07-23T00:00:00Z",
    validationState: "SystemObserved",
  };
}

const executive: ExecutiveEconomicsView = {
  totalSpend: amount(10),
  declaredValue: amount(20),
  validatedValue: amount(15),
  validatedToDeclaredRatio: 0.75,
  unattributedCost: amount(0),
  unattributedPercent: 0,
  confidenceMix: { Measured: 1 },
  asOf: "2026-07-23T00:00:00Z",
};

const roi: RoiView = {
  scope: "portfolio",
  cost: amount(10),
  value: amount(20),
  netBenefit: amount(10),
  singlePointRoi: 1,
  validatedOnlyRoi: 1,
  lowRoi: 1,
  highRoi: 1,
  paybackMonths: 6,
  singlePointSuppressed: false,
  compositeEvidenceClass: "Measured",
  confidenceMix: { Measured: 1 },
  asOf: "2026-07-23T00:00:00Z",
};

const coverage: CoverageView = {
  providersConnected: 0,
  assetsKnown: 0,
  lastSuccessfulSweep: null,
  coverageNote: "No providers connected.",
  asOf: "2026-07-23T00:00:00Z",
  surfaces: [],
};

const mergeCase: MergeCaseView = {
  mergeCaseId: "44444444-4444-4444-8444-444444444444",
  reason: "Manual review required.",
  confidence: "Low",
  identifiers: [],
  candidateAssetIds: [],
  observationRef: null,
  openedAt: "2026-07-23T00:00:00Z",
};
