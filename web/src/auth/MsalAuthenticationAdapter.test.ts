import {
  InteractionRequiredAuthError,
  type AccountInfo,
  type AuthenticationResult,
  type EndSessionRequest,
  type HandleRedirectPromiseOptions,
  type RedirectRequest,
  type SilentRequest,
} from "@azure/msal-browser";
import {
  AUTHENTICATION_ENVIRONMENT_KEYS,
  ORGANIZATIONS_AUTHORITY,
} from "./config";
import {
  AuthenticationStateError,
  AuthenticationUnavailableError,
  InteractiveAuthenticationInProgressError,
  ReauthenticationRequiredError,
  createMsalAuthenticationAdapter,
} from "./MsalAuthenticationAdapter";

const CLIENT_ID = "11111111-1111-4111-8111-111111111111";
const API_SCOPE =
  "api://22222222-2222-4222-8222-222222222222/controltower.access";
const environment = {
  [AUTHENTICATION_ENVIRONMENT_KEYS.clientId]: CLIENT_ID,
  [AUTHENTICATION_ENVIRONMENT_KEYS.authority]: ORGANIZATIONS_AUTHORITY,
  [AUTHENTICATION_ENVIRONMENT_KEYS.apiScope]: API_SCOPE,
};

function account(
  username: string,
  homeAccountId = `${username}-home`,
): AccountInfo {
  return {
    homeAccountId,
    environment: "login.microsoftonline.com",
    tenantId: "33333333-3333-4333-8333-333333333333",
    username,
    localAccountId: `${username}-local`,
    name: `${username} name`,
    idToken: "id-token-must-not-be-exposed",
    idTokenClaims: { roles: ["Administrator"], tenant: "untrusted" },
  };
}

function result(
  selectedAccount: AccountInfo,
  accessToken = "opaque-access-token",
  tokenType = "Bearer",
): AuthenticationResult {
  return {
    authority: ORGANIZATIONS_AUTHORITY,
    uniqueId: "unique-id",
    tenantId: selectedAccount.tenantId,
    scopes: [API_SCOPE],
    account: selectedAccount,
    idToken: "id-token-must-not-be-used",
    idTokenClaims: { roles: ["Administrator"] },
    accessToken,
    fromCache: true,
    expiresOn: new Date("2026-07-23T12:00:00Z"),
    tokenType,
    correlationId: "correlation-id",
  };
}

function fakeClient() {
  return {
    initialize: vi.fn(async (): Promise<void> => {}),
    handleRedirectPromise: vi.fn(
      async (
        _options?: HandleRedirectPromiseOptions,
      ): Promise<AuthenticationResult | null> => null,
    ),
    getAllAccounts: vi.fn((): AccountInfo[] => []),
    getActiveAccount: vi.fn((): AccountInfo | null => null),
    setActiveAccount: vi.fn((_account: AccountInfo | null): void => {}),
    acquireTokenSilent: vi.fn(
      async (_request: SilentRequest): Promise<AuthenticationResult> => {
        throw new Error("not configured");
      },
    ),
    acquireTokenRedirect: vi.fn(
      async (_request: RedirectRequest): Promise<void> => {},
    ),
    loginRedirect: vi.fn(
      async (_request?: RedirectRequest): Promise<void> => {},
    ),
    logoutRedirect: vi.fn(
      async (_request?: EndSessionRequest): Promise<void> => {},
    ),
  };
}

function adapter(client: ReturnType<typeof fakeClient>) {
  return createMsalAuthenticationAdapter(
    environment,
    "https://controltower.example",
    () => client as never,
  );
}

test("initializes and handles the redirect exactly once before resolving bootstrap", async () => {
  const client = fakeClient();
  const selected = account("redirected@example.com");
  const order: string[] = [];
  client.initialize.mockImplementation(async () => {
    order.push("initialize");
  });
  client.handleRedirectPromise.mockImplementation(async () => {
    order.push("redirect");
    return result(selected);
  });
  const authentication = adapter(client);

  const first = authentication.bootstrap();
  const second = authentication.bootstrap();

  expect(second).toBe(first);
  await expect(first).resolves.toEqual({
    kind: "authenticated",
    account: {
      username: "redirected@example.com",
      name: "redirected@example.com name",
    },
  });
  expect(order).toEqual(["initialize", "redirect"]);
  expect(client.initialize).toHaveBeenCalledTimes(1);
  expect(client.handleRedirectPromise).toHaveBeenCalledTimes(1);
  expect(client.handleRedirectPromise).toHaveBeenCalledWith({
    navigateToLoginRequestUrl: false,
  });
  expect(client.setActiveAccount).toHaveBeenCalledWith(selected);
  expect(JSON.stringify(authentication.state)).not.toContain("id-token");
  expect(JSON.stringify(authentication.state)).not.toContain("roles");
});

test("no cached account is explicitly signed out and cannot acquire a token", async () => {
  const client = fakeClient();
  const authentication = adapter(client);

  await expect(authentication.bootstrap()).resolves.toEqual({
    kind: "signed-out",
  });
  await expect(authentication.acquireAccessToken()).rejects.toBeInstanceOf(
    AuthenticationStateError,
  );
  expect(client.acquireTokenSilent).not.toHaveBeenCalled();
});

test("one cached account is selected deterministically", async () => {
  const client = fakeClient();
  const selected = account("only@example.com");
  client.getAllAccounts.mockReturnValue([selected]);
  const authentication = adapter(client);

  await expect(authentication.bootstrap()).resolves.toMatchObject({
    kind: "authenticated",
    account: { username: "only@example.com" },
  });
  expect(client.setActiveAccount).toHaveBeenLastCalledWith(selected);
});

test("multiple unselected accounts fail closed until one sanitized choice is explicit", async () => {
  const client = fakeClient();
  const second = account("second@example.com", "b-home");
  const first = account("first@example.com", "a-home");
  client.getAllAccounts.mockReturnValue([second, first]);
  const authentication = adapter(client);

  await expect(authentication.bootstrap()).resolves.toEqual({
    kind: "account-selection-required",
    accounts: [
      {
        id: "account-1",
        username: "first@example.com",
        name: "first@example.com name",
      },
      {
        id: "account-2",
        username: "second@example.com",
        name: "second@example.com name",
      },
    ],
  });
  expect(client.setActiveAccount).toHaveBeenLastCalledWith(null);
  await expect(authentication.acquireAccessToken()).rejects.toBeInstanceOf(
    AuthenticationStateError,
  );

  expect(authentication.selectAccount("account-2")).toMatchObject({
    kind: "authenticated",
    account: { username: "second@example.com" },
  });
  expect(client.setActiveAccount).toHaveBeenLastCalledWith(second);
});

test("silent acquisition requests exactly the API scope and returns only its bearer access token", async () => {
  const client = fakeClient();
  const selected = account("operator@example.com");
  client.getAllAccounts.mockReturnValue([selected]);
  client.acquireTokenSilent.mockResolvedValue(result(selected));
  const authentication = adapter(client);
  await authentication.bootstrap();

  await expect(authentication.acquireAccessToken()).resolves.toBe(
    "opaque-access-token",
  );
  expect(client.acquireTokenSilent).toHaveBeenCalledTimes(1);
  expect(client.acquireTokenSilent).toHaveBeenCalledWith({
    account: selected,
    scopes: [API_SCOPE],
  });
});

test("an ID token is never substituted for a missing bearer access token", async () => {
  const client = fakeClient();
  const selected = account("operator@example.com");
  client.getAllAccounts.mockReturnValue([selected]);
  client.acquireTokenSilent.mockResolvedValue(result(selected, ""));
  const authentication = adapter(client);
  await authentication.bootstrap();

  await expect(authentication.acquireAccessToken()).rejects.toBeInstanceOf(
    AuthenticationUnavailableError,
  );
});

test("interaction-required is typed and redirects only after one explicit action", async () => {
  const client = fakeClient();
  const selected = account("operator@example.com");
  const claims = '{"access_token":{"acrs":{"essential":true}}}';
  client.getAllAccounts.mockReturnValue([selected]);
  client.acquireTokenSilent.mockRejectedValue(
    new InteractionRequiredAuthError(
      "interaction_required",
      "correlation-id",
      "MFA required",
      "",
      "",
      "",
      claims,
    ),
  );
  const authentication = adapter(client);
  await authentication.bootstrap();

  await expect(authentication.acquireAccessToken()).rejects.toBeInstanceOf(
    ReauthenticationRequiredError,
  );
  expect(client.acquireTokenRedirect).not.toHaveBeenCalled();

  await authentication.reauthenticate();
  expect(client.acquireTokenRedirect).toHaveBeenCalledTimes(1);
  expect(client.acquireTokenRedirect).toHaveBeenCalledWith({
    account: selected,
    scopes: [API_SCOPE],
    claims,
  });
  await expect(authentication.reauthenticate()).rejects.toBeInstanceOf(
    InteractiveAuthenticationInProgressError,
  );
  expect(client.acquireTokenRedirect).toHaveBeenCalledTimes(1);
});

test("sign in is explicit, scope-limited and account-selecting", async () => {
  const client = fakeClient();
  const authentication = adapter(client);
  await authentication.bootstrap();

  expect(client.loginRedirect).not.toHaveBeenCalled();
  await authentication.signIn();
  expect(client.loginRedirect).toHaveBeenCalledWith({
    scopes: [API_SCOPE],
    prompt: "select_account",
  });
});

test("logout is account-specific and clears protected adapter state first", async () => {
  const client = fakeClient();
  const selected = account("operator@example.com");
  client.getAllAccounts.mockReturnValue([selected]);
  client.acquireTokenSilent.mockResolvedValue(result(selected));
  const authentication = adapter(client);
  await authentication.bootstrap();

  await authentication.logout();

  expect(authentication.state).toEqual({ kind: "signed-out" });
  expect(client.setActiveAccount).toHaveBeenLastCalledWith(null);
  expect(client.logoutRedirect).toHaveBeenCalledWith({
    account: selected,
    postLogoutRedirectUri: "https://controltower.example/",
  });
  await expect(authentication.acquireAccessToken()).rejects.toBeInstanceOf(
    AuthenticationStateError,
  );
});

test("MSAL runtime failures are generic and do not disclose their contents", async () => {
  const client = fakeClient();
  client.initialize.mockRejectedValue(
    new Error("opaque-access-token must never appear"),
  );
  const authentication = adapter(client);

  let failure: unknown;
  try {
    await authentication.bootstrap();
  } catch (error: unknown) {
    failure = error;
  }

  expect(failure).toBeInstanceOf(AuthenticationUnavailableError);
  expect(String(failure)).not.toContain("opaque-access-token");
});
