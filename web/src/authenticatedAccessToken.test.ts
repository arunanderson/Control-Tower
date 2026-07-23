import {
  ReauthenticationRequiredClientError,
  type AccessTokenProvider,
} from "./api/client";
import {
  AuthenticationUnavailableError,
  ReauthenticationRequiredError,
} from "./auth";
import { createControlTowerAccessTokenProvider } from "./authenticatedAccessToken";

function providerFor(
  acquireAccessToken: () => Promise<string>,
): AccessTokenProvider {
  return createControlTowerAccessTokenProvider({
    acquireAccessToken,
  } as never);
}

test("returns only the adapter access token", async () => {
  const provider = providerFor(async () => "opaque-access-token");
  await expect(provider()).resolves.toBe("opaque-access-token");
});

test("translates only interaction-required into the API reauthentication marker", async () => {
  const provider = providerFor(async () => {
    throw new ReauthenticationRequiredError();
  });
  await expect(provider()).rejects.toBeInstanceOf(
    ReauthenticationRequiredClientError,
  );
});

test("preserves transient adapter failure for safe normalization by the API client", async () => {
  const failure = new AuthenticationUnavailableError();
  const provider = providerFor(async () => {
    throw failure;
  });
  await expect(provider()).rejects.toBe(failure);
});
