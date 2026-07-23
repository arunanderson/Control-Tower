import { BrowserCacheLocation } from "@azure/msal-browser";
import {
  AUTHENTICATION_ENVIRONMENT_KEYS,
  AuthenticationConfigurationError,
  ORGANIZATIONS_AUTHORITY,
  validateAuthenticationConfiguration,
} from "./config";
import { createMsalAuthenticationAdapter } from "./MsalAuthenticationAdapter";

const CLIENT_ID = "11111111-1111-4111-8111-111111111111";
const API_ID = "22222222-2222-4222-8222-222222222222";
const API_SCOPE = `api://${API_ID}/controltower.access`;

const validEnvironment = {
  [AUTHENTICATION_ENVIRONMENT_KEYS.clientId]: CLIENT_ID,
  [AUTHENTICATION_ENVIRONMENT_KEYS.authority]: ORGANIZATIONS_AUTHORITY,
  [AUTHENTICATION_ENVIRONMENT_KEYS.apiScope]: API_SCOPE,
};

test("validates the public configuration and fixes MSAL to session storage", () => {
  const configuration = validateAuthenticationConfiguration(
    validEnvironment,
    "https://controltower.example",
  );

  expect(configuration.apiScope).toBe(API_SCOPE);
  expect(configuration.redirectUri).toBe("https://controltower.example/");
  expect(configuration.msal).toEqual({
    auth: {
      clientId: CLIENT_ID,
      authority: ORGANIZATIONS_AUTHORITY,
      redirectUri: "https://controltower.example/",
      postLogoutRedirectUri: "https://controltower.example/",
    },
    cache: {
      cacheLocation: BrowserCacheLocation.SessionStorage,
    },
  });
});

test.each([
  [
    "missing client ID",
    { ...validEnvironment, VITE_ENTRA_CLIENT_ID: undefined },
  ],
  ["empty client ID", { ...validEnvironment, VITE_ENTRA_CLIENT_ID: "" }],
  [
    "non-GUID client ID",
    { ...validEnvironment, VITE_ENTRA_CLIENT_ID: "not-a-client-id" },
  ],
  [
    "personal-account authority",
    {
      ...validEnvironment,
      VITE_ENTRA_AUTHORITY: "https://login.microsoftonline.com/common",
    },
  ],
  [
    "lookalike authority",
    {
      ...validEnvironment,
      VITE_ENTRA_AUTHORITY:
        "https://login.microsoftonline.com.attacker.example/organizations",
    },
  ],
  [
    "authority with query",
    {
      ...validEnvironment,
      VITE_ENTRA_AUTHORITY:
        "https://login.microsoftonline.com/organizations?unsafe=true",
    },
  ],
  [
    "wrong delegated scope",
    {
      ...validEnvironment,
      VITE_CONTROL_TOWER_API_SCOPE: `api://${API_ID}/other.scope`,
    },
  ],
  [
    "scope with whitespace",
    {
      ...validEnvironment,
      VITE_CONTROL_TOWER_API_SCOPE: ` ${API_SCOPE}`,
    },
  ],
])("fails closed for %s", (_name, environment) => {
  expect(() =>
    validateAuthenticationConfiguration(
      environment,
      "https://controltower.example",
    ),
  ).toThrow(AuthenticationConfigurationError);
});

test.each([
  "http://controltower.example",
  "https://user:password@controltower.example",
  "https://controltower.example/path",
  "https://controltower.example?unsafe=true",
])("rejects unsafe browser origin %s", (origin) => {
  expect(() =>
    validateAuthenticationConfiguration(validEnvironment, origin),
  ).toThrow(AuthenticationConfigurationError);
});

test.each([
  "http://localhost:5173",
  "http://127.0.0.1:5173",
  "http://[::1]:5173",
])("allows loopback HTTP only for local development: %s", (origin) => {
  expect(
    validateAuthenticationConfiguration(validEnvironment, origin).redirectUri,
  ).toBe(`${origin}/`);
});

test("invalid configuration fails before an MSAL client is constructed", () => {
  const createPublicClient = vi.fn();

  expect(() =>
    createMsalAuthenticationAdapter(
      {
        ...validEnvironment,
        VITE_ENTRA_AUTHORITY: "https://login.microsoftonline.com/consumers",
      },
      "https://controltower.example",
      createPublicClient,
    ),
  ).toThrow(AuthenticationConfigurationError);
  expect(createPublicClient).not.toHaveBeenCalled();
});
