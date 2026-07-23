import {
  AccessTokenAcquisitionError,
  ForbiddenError,
  HttpControlTowerApi,
  InvalidResponseError,
  NetworkRequestError,
  NoAccessError,
  NotFoundError,
  ReauthenticationRequiredClientError,
  ServiceResponseError,
  UnauthenticatedError,
  type ControlTowerSession,
} from "./client";

const TENANT = "11111111-1111-4111-8111-111111111111";
const DIRECTORY_TENANT = "22222222-2222-4222-8222-222222222222";
const OBJECT_ID = "33333333-3333-4333-8333-333333333333";

const SESSION: ControlTowerSession = {
  tenant: TENANT,
  directoryTenant: DIRECTORY_TENANT,
  actor: `entra:${DIRECTORY_TENANT}:${OBJECT_ID}`,
  roles: ["Viewer"],
  capabilities: ["portfolio.read"],
  organizationScope: "TenantWide",
};

const FORBIDDEN_IDENTITY_HEADERS = [
  "X-Tenant-Id",
  "X-Actor",
  "X-Operator",
  "X-Role",
  "X-Roles",
  "X-Group",
  "X-Groups",
  "X-Capability",
  "X-Capabilities",
];

let fetchMock: ReturnType<typeof vi.fn>;

beforeEach(() => {
  fetchMock = vi.fn();
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function session(overrides: Record<string, unknown> = {}): unknown {
  return { ...SESSION, ...overrides };
}

function headersAt(call: number): Headers {
  return new Headers(
    (fetchMock.mock.calls[call][1] as RequestInit | undefined)?.headers,
  );
}

test("getSession is single-flight and caches one validated immutable projection", async () => {
  const tokenProvider = vi.fn().mockResolvedValue("access-token");
  fetchMock.mockResolvedValue(jsonResponse(SESSION));
  const api = new HttpControlTowerApi(tokenProvider);

  const first = api.getSession();
  const second = api.getSession();

  expect(first).toBe(second);
  const [left, right] = await Promise.all([first, second]);
  expect(left).toBe(right);
  expect(Object.isFrozen(left)).toBe(true);
  expect(Object.isFrozen(left.roles)).toBe(true);
  expect(Object.isFrozen(left.capabilities)).toBe(true);
  expect(fetchMock).toHaveBeenCalledTimes(1);
  expect(tokenProvider).toHaveBeenCalledTimes(1);
  expect(fetchMock.mock.calls[0][0]).toBe("/whoami");
  expect(headersAt(0).get("Authorization")).toBe("Bearer access-token");
});

test("clearSession permits an explicit session refresh", async () => {
  const tokenProvider = vi.fn().mockResolvedValue("access-token");
  fetchMock.mockImplementation(async () => jsonResponse(SESSION));
  const api = new HttpControlTowerApi(tokenProvider);

  await api.getSession();
  api.clearSession();
  await api.getSession();

  expect(fetchMock).toHaveBeenCalledTimes(2);
  expect(tokenProvider).toHaveBeenCalledTimes(2);
});

test("every data request waits for whoami to finish", async () => {
  let resolveSession!: (response: Response) => void;
  const pendingSession = new Promise<Response>((resolve) => {
    resolveSession = resolve;
  });
  const tokenProvider = vi.fn().mockResolvedValue("access-token");
  fetchMock
    .mockReturnValueOnce(pendingSession)
    .mockResolvedValueOnce(jsonResponse([]));
  const api = new HttpControlTowerApi(tokenProvider);

  const assets = api.getAssets();
  await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
  expect(fetchMock.mock.calls[0][0]).toBe("/whoami");

  resolveSession(jsonResponse(SESSION));
  await expect(assets).resolves.toEqual([]);
  expect(fetchMock.mock.calls[1][0]).toBe("/api/portfolio/assets");
  expect(tokenProvider).toHaveBeenCalledTimes(2);
});

type ApiCase = {
  name: string;
  path: string;
  method?: "POST";
  purpose?: string;
  body?: unknown;
  invoke: (api: HttpControlTowerApi) => Promise<unknown>;
};

const API_CASES: ApiCase[] = [
  {
    name: "assets",
    path: "/api/portfolio/assets",
    invoke: (api) => api.getAssets(),
  },
  {
    name: "asset record",
    path: "/api/portfolio/assets/asset%2Fid",
    invoke: (api) => api.getAssetRecord("asset/id"),
  },
  {
    name: "executive economics",
    path: "/api/economics/executive",
    invoke: (api) => api.getExecutive(),
  },
  {
    name: "portfolio economics",
    path: "/api/economics/portfolio",
    invoke: (api) => api.getPortfolioRoi(),
  },
  {
    name: "department economics",
    path: "/api/economics/departments",
    invoke: (api) => api.getDepartmentRoi(),
  },
  {
    name: "agent economics",
    path: "/api/economics/agents",
    invoke: (api) => api.getAgentRoi(),
  },
  {
    name: "governance cases",
    path: "/api/governance/cases",
    invoke: (api) => api.getGovernanceCases(),
  },
  {
    name: "governance debt",
    path: "/api/governance/debt",
    invoke: (api) => api.getGovernanceDebt(),
  },
  {
    name: "coverage",
    path: "/api/trust/coverage",
    invoke: (api) => api.getCoverage(),
  },
  {
    name: "privileged access",
    path: "/api/trust/privileged-access",
    purpose: "Review privileged access history",
    invoke: (api) => api.getPrivilegedAccess(),
  },
  {
    name: "administration summary",
    path: "/api/admin/summary",
    invoke: (api) => api.getAdministrationSummary(),
  },
  {
    name: "merge cases",
    path: "/api/resolution/merge-cases",
    invoke: (api) => api.getMergeCases(),
  },
  {
    name: "asset resolution",
    path: "/api/resolution/assets/asset%2Fid",
    invoke: (api) => api.getAssetResolution("asset/id"),
  },
  {
    name: "resolve merge case",
    path: "/api/resolution/merge-cases/case%2Fid/resolve",
    method: "POST",
    body: { outcome: "reviewed" },
    invoke: (api) => api.resolveMergeCase("case/id", "reviewed"),
  },
  {
    name: "merge assets",
    path: "/api/resolution/merge",
    method: "POST",
    body: { targetId: "target", sourceId: "source" },
    invoke: (api) => api.mergeAssets("target", "source"),
  },
];

test.each(API_CASES)(
  "$name uses a fresh bearer and only legitimate same-origin headers",
  async ({ path, method, purpose, body, invoke }) => {
    const tokenProvider = vi
      .fn()
      .mockResolvedValueOnce("session-token")
      .mockResolvedValueOnce("api-token");
    fetchMock.mockImplementation(
      async (input: RequestInfo | URL, init?: RequestInit) => {
        const requestPath = String(input);
        if (requestPath === "/whoami") return jsonResponse(SESSION);
        if (requestPath === "/api/admin/summary") {
          return jsonResponse({
            tenant: TENANT,
            areas: ["Administration"],
            readModelOnly: true,
          });
        }
        return init?.method === "POST"
          ? new Response(null, { status: 200 })
          : jsonResponse([]);
      },
    );
    const api = new HttpControlTowerApi(tokenProvider);

    await invoke(api);

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(tokenProvider).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[0][0]).toBe("/whoami");
    expect(fetchMock.mock.calls[1][0]).toBe(path);
    const init = fetchMock.mock.calls[1][1] as RequestInit;
    expect(init.method ?? "GET").toBe(method ?? "GET");
    expect(init.cache).toBe("no-store");
    expect(init.credentials).toBe("omit");
    expect(init.redirect).toBe("error");
    expect(init.referrerPolicy).toBe("no-referrer");

    const headers = headersAt(1);
    expect(headers.get("Authorization")).toBe("Bearer api-token");
    expect(headers.get("X-Purpose")).toBe(purpose ?? null);
    expect(headers.get("Content-Type")).toBe(
      method === "POST" ? "application/json" : null,
    );
    for (const forbidden of FORBIDDEN_IDENTITY_HEADERS) {
      expect(headers.has(forbidden)).toBe(false);
    }

    expect(init.body).toBe(
      body === undefined ? undefined : JSON.stringify(body),
    );
  },
);

test("whoami also disables ambient credentials, redirects, referrers, and HTTP caching", async () => {
  fetchMock.mockResolvedValue(jsonResponse(SESSION));
  const api = new HttpControlTowerApi(
    vi.fn().mockResolvedValue("access-token"),
  );

  await api.getSession();

  const init = fetchMock.mock.calls[0][1] as RequestInit;
  expect(init.cache).toBe("no-store");
  expect(init.credentials).toBe("omit");
  expect(init.redirect).toBe("error");
  expect(init.referrerPolicy).toBe("no-referrer");
});

test("token-provider failure is safe and performs no network request", async () => {
  const tokenProvider = vi
    .fn()
    .mockRejectedValue(new Error("secret-token-from-provider"));
  const api = new HttpControlTowerApi(tokenProvider);

  const error = await api.getSession().catch((caught: unknown) => caught);

  expect(error).toBeInstanceOf(AccessTokenAcquisitionError);
  expect(String(error)).not.toContain("secret-token-from-provider");
  expect(fetchMock).not.toHaveBeenCalled();
});

test("an interaction-required marker remains distinct and performs no network request", async () => {
  const api = new HttpControlTowerApi(
    vi.fn().mockRejectedValue(new ReauthenticationRequiredClientError()),
  );

  await expect(api.getSession()).rejects.toBeInstanceOf(
    ReauthenticationRequiredClientError,
  );
  expect(fetchMock).not.toHaveBeenCalled();
});

test.each([
  "",
  "token with whitespace",
  "token\nwith-control",
  "x".repeat(65_537),
])("invalid access token %j performs no network request", async (token) => {
  const api = new HttpControlTowerApi(vi.fn().mockResolvedValue(token));

  await expect(api.getSession()).rejects.toBeInstanceOf(
    AccessTokenAcquisitionError,
  );
  expect(fetchMock).not.toHaveBeenCalled();
});

test.each([
  { status: 401, errorType: UnauthenticatedError },
  { status: 403, errorType: ForbiddenError },
  { status: 404, errorType: NotFoundError },
  { status: 500, errorType: ServiceResponseError },
])(
  "HTTP $status produces a distinct safe typed failure",
  async ({ status, errorType }) => {
    fetchMock.mockResolvedValue(
      new Response("secret-response-body", { status }),
    );
    const api = new HttpControlTowerApi(
      vi.fn().mockResolvedValue("secret-access-token"),
    );

    const error = await api.getSession().catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(errorType);
    expect(String(error)).not.toContain("secret-response-body");
    expect(String(error)).not.toContain("secret-access-token");
  },
);

test("network failure is distinct and does not disclose its cause", async () => {
  fetchMock.mockRejectedValue(
    new Error("network failure containing secret-access-token"),
  );
  const api = new HttpControlTowerApi(
    vi.fn().mockResolvedValue("secret-access-token"),
  );

  const error = await api.getSession().catch((caught: unknown) => caught);

  expect(error).toBeInstanceOf(NetworkRequestError);
  expect(String(error)).not.toContain("secret-access-token");
});

test("an invalid JSON response is a safe typed failure", async () => {
  fetchMock.mockResolvedValue(
    new Response("{not-json", {
      status: 200,
      headers: { "Content-Type": "application/json" },
    }),
  );
  const api = new HttpControlTowerApi(
    vi.fn().mockResolvedValue("access-token"),
  );

  await expect(api.getSession()).rejects.toBeInstanceOf(InvalidResponseError);
});

test.each([
  {
    name: "unknown role",
    projection: session({ roles: ["SuperAdmin"] }),
    errorType: InvalidResponseError,
  },
  {
    name: "unknown capability",
    projection: session({ capabilities: ["tenant.override"] }),
    errorType: InvalidResponseError,
  },
  {
    name: "duplicate role",
    projection: session({ roles: ["Viewer", "Viewer"] }),
    errorType: InvalidResponseError,
  },
  {
    name: "empty role assignment",
    projection: session({ roles: [], capabilities: [] }),
    errorType: NoAccessError,
  },
  {
    name: "empty capabilities",
    projection: session({ capabilities: [] }),
    errorType: NoAccessError,
  },
  {
    name: "unknown organisation scope",
    projection: session({ organizationScope: "BusinessUnit" }),
    errorType: InvalidResponseError,
  },
  {
    name: "malformed internal tenant",
    projection: session({ tenant: "caller-tenant" }),
    errorType: InvalidResponseError,
  },
  {
    name: "actor from another directory tenant",
    projection: session({
      actor: `entra:44444444-4444-4444-8444-444444444444:${OBJECT_ID}`,
    }),
    errorType: InvalidResponseError,
  },
  {
    name: "non-object response",
    projection: [],
    errorType: InvalidResponseError,
  },
])(
  "$name fails closed before protected data is requested",
  async ({ projection, errorType }) => {
    fetchMock.mockResolvedValue(jsonResponse(projection));
    const api = new HttpControlTowerApi(
      vi.fn().mockResolvedValue("access-token"),
    );

    await expect(api.getAssets()).rejects.toBeInstanceOf(errorType);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0][0]).toBe("/whoami");
  },
);

test.each([
  (api: HttpControlTowerApi) => api.getAssetRecord("missing"),
  (api: HttpControlTowerApi) => api.getAssetResolution("missing"),
])("record-only 404 remains null", async (invoke) => {
  fetchMock
    .mockResolvedValueOnce(jsonResponse(SESSION))
    .mockResolvedValueOnce(
      new Response("secret-response-body", { status: 404 }),
    );
  const api = new HttpControlTowerApi(
    vi.fn().mockResolvedValue("access-token"),
  );

  await expect(invoke(api)).resolves.toBeNull();
});

test("non-record 404 remains a typed not-found error", async () => {
  fetchMock
    .mockResolvedValueOnce(jsonResponse(SESSION))
    .mockResolvedValueOnce(new Response(null, { status: 404 }));
  const api = new HttpControlTowerApi(
    vi.fn().mockResolvedValue("access-token"),
  );

  await expect(api.getAssets()).rejects.toBeInstanceOf(NotFoundError);
});

test("administration summary tenant must match the validated session", async () => {
  fetchMock.mockResolvedValueOnce(jsonResponse(SESSION)).mockResolvedValueOnce(
    jsonResponse({
      tenant: "44444444-4444-4444-8444-444444444444",
      areas: ["Administration"],
      readModelOnly: true,
    }),
  );
  const api = new HttpControlTowerApi(
    vi.fn().mockResolvedValue("access-token"),
  );

  await expect(api.getAdministrationSummary()).rejects.toBeInstanceOf(
    InvalidResponseError,
  );
});

test("401 clears the cached session, while 403 preserves it", async () => {
  fetchMock
    .mockResolvedValueOnce(jsonResponse(SESSION))
    .mockResolvedValueOnce(new Response(null, { status: 403 }))
    .mockResolvedValueOnce(new Response(null, { status: 401 }))
    .mockResolvedValueOnce(jsonResponse(SESSION));
  const api = new HttpControlTowerApi(
    vi.fn().mockResolvedValue("access-token"),
  );

  await api.getSession();
  await expect(api.getAssets()).rejects.toBeInstanceOf(ForbiddenError);
  await api.getSession();
  expect(fetchMock).toHaveBeenCalledTimes(2);

  await expect(api.getAssets()).rejects.toBeInstanceOf(UnauthenticatedError);
  await api.getSession();
  expect(fetchMock).toHaveBeenCalledTimes(4);
});
