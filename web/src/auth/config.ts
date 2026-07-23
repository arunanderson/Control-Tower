import { BrowserCacheLocation, type Configuration } from "@azure/msal-browser";

export const ORGANIZATIONS_AUTHORITY =
  "https://login.microsoftonline.com/organizations";

export const AUTHENTICATION_ENVIRONMENT_KEYS = {
  clientId: "VITE_ENTRA_CLIENT_ID",
  authority: "VITE_ENTRA_AUTHORITY",
  apiScope: "VITE_CONTROL_TOWER_API_SCOPE",
} as const;

const NON_EMPTY_GUID =
  /^(?!00000000-0000-0000-0000-000000000000$)[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const API_SCOPE =
  /^api:\/\/((?!00000000-0000-0000-0000-000000000000$)[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12})\/(controltower\.access)$/i;

export class AuthenticationConfigurationError extends Error {
  constructor() {
    super("Control Tower authentication configuration is invalid.");
    this.name = "AuthenticationConfigurationError";
  }
}

export interface ValidatedAuthenticationConfiguration {
  readonly apiScope: string;
  readonly redirectUri: string;
  readonly postLogoutRedirectUri: string;
  readonly msal: Configuration;
}

export function validateAuthenticationConfiguration(
  environment: Readonly<Record<string, unknown>>,
  browserOrigin: string,
): ValidatedAuthenticationConfiguration {
  const clientId = requiredValue(
    environment,
    AUTHENTICATION_ENVIRONMENT_KEYS.clientId,
  );
  const authority = requiredValue(
    environment,
    AUTHENTICATION_ENVIRONMENT_KEYS.authority,
  );
  const apiScope = requiredValue(
    environment,
    AUTHENTICATION_ENVIRONMENT_KEYS.apiScope,
  );

  if (!NON_EMPTY_GUID.test(clientId)) fail();
  validateAuthority(authority);
  validateApiScope(apiScope);
  const origin = validateBrowserOrigin(browserOrigin);
  const redirectUri = `${origin}/`;

  return Object.freeze({
    apiScope,
    redirectUri,
    postLogoutRedirectUri: redirectUri,
    msal: Object.freeze({
      auth: Object.freeze({
        clientId,
        authority: ORGANIZATIONS_AUTHORITY,
        redirectUri,
        postLogoutRedirectUri: redirectUri,
      }),
      cache: Object.freeze({
        cacheLocation: BrowserCacheLocation.SessionStorage,
      }),
    }),
  });
}

function requiredValue(
  environment: Readonly<Record<string, unknown>>,
  key: string,
): string {
  const value = environment[key];
  if (
    typeof value !== "string" ||
    value.length === 0 ||
    value !== value.trim() ||
    value.length > 512 ||
    [...value].some((character) => /\s|[\u0000-\u001f\u007f]/.test(character))
  ) {
    fail();
  }

  return value;
}

function validateAuthority(authority: string): void {
  let parsed: URL;
  try {
    parsed = new URL(authority);
  } catch {
    fail();
  }

  if (
    parsed.protocol !== "https:" ||
    parsed.hostname.toLowerCase() !== "login.microsoftonline.com" ||
    parsed.port !== "" ||
    parsed.username !== "" ||
    parsed.password !== "" ||
    parsed.search !== "" ||
    parsed.hash !== "" ||
    parsed.pathname.replace(/\/$/, "") !== "/organizations"
  ) {
    fail();
  }
}

function validateApiScope(apiScope: string): void {
  const match = API_SCOPE.exec(apiScope);
  if (match === null || match[2] !== "controltower.access") fail();
}

function validateBrowserOrigin(browserOrigin: string): string {
  if (
    browserOrigin.length === 0 ||
    browserOrigin !== browserOrigin.trim() ||
    browserOrigin.length > 2048
  ) {
    fail();
  }

  let parsed: URL;
  try {
    parsed = new URL(browserOrigin);
  } catch {
    fail();
  }

  const isLocalHttp =
    parsed.protocol === "http:" &&
    (parsed.hostname === "localhost" ||
      parsed.hostname === "127.0.0.1" ||
      parsed.hostname === "[::1]");
  if (
    (parsed.protocol !== "https:" && !isLocalHttp) ||
    parsed.origin !== browserOrigin ||
    parsed.username !== "" ||
    parsed.password !== "" ||
    parsed.pathname !== "/" ||
    parsed.search !== "" ||
    parsed.hash !== ""
  ) {
    fail();
  }

  return parsed.origin;
}

function fail(): never {
  throw new AuthenticationConfigurationError();
}
