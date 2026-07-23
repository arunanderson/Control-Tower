import {
  ReauthenticationRequiredClientError,
  type AccessTokenProvider,
} from "./api/client";
import {
  ReauthenticationRequiredError,
  type MsalAuthenticationAdapter,
} from "./auth";

export function createControlTowerAccessTokenProvider(
  authentication: Pick<MsalAuthenticationAdapter, "acquireAccessToken">,
): AccessTokenProvider {
  return async () => {
    try {
      return await authentication.acquireAccessToken();
    } catch (error: unknown) {
      if (error instanceof ReauthenticationRequiredError) {
        throw new ReauthenticationRequiredClientError();
      }
      throw error;
    }
  };
}
