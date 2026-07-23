import type { ReactNode } from "react";
import { App } from "./App";
import { HttpControlTowerApi, type ControlTowerApi } from "./api/client";
import {
  createMsalAuthenticationAdapter,
  type MsalAuthenticationAdapter,
} from "./auth";
import { createControlTowerAccessTokenProvider } from "./authenticatedAccessToken";

type AuthenticationFactory = (
  environment: Readonly<Record<string, unknown>>,
  browserOrigin: string,
) => MsalAuthenticationAdapter;

type ApiFactory = (
  authentication: MsalAuthenticationAdapter,
) => ControlTowerApi;

export interface StartupOptions {
  readonly environment: Readonly<Record<string, unknown>>;
  readonly browserOrigin: string;
  readonly render: (application: ReactNode) => void;
  readonly createAuthentication?: AuthenticationFactory;
  readonly createApi?: ApiFactory;
}

export async function startControlTowerApplication({
  environment,
  browserOrigin,
  render,
  createAuthentication = createMsalAuthenticationAdapter,
  createApi = (authentication) =>
    new HttpControlTowerApi(
      createControlTowerAccessTokenProvider(authentication),
    ),
}: StartupOptions): Promise<void> {
  try {
    const authentication = createAuthentication(environment, browserOrigin);
    await authentication.bootstrap();
    const api = createApi(authentication);
    render(<App authentication={authentication} api={api} />);
  } catch {
    render(
      <main>
        <h1>Enterprise AI Control Tower</h1>
        <p role="alert">
          Authentication could not be initialized. Contact your Control Tower
          administrator.
        </p>
      </main>,
    );
  }
}
