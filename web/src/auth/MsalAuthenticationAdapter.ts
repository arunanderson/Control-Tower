import {
  InteractionRequiredAuthError,
  PublicClientApplication,
  type AccountInfo,
  type Configuration,
  type IPublicClientApplication,
} from "@azure/msal-browser";
import {
  validateAuthenticationConfiguration,
  type ValidatedAuthenticationConfiguration,
} from "./config";

export interface AccountChoice {
  readonly id: string;
  readonly username: string;
  readonly name?: string;
}

export type AuthenticationState =
  | { readonly kind: "uninitialized" }
  | { readonly kind: "signed-out" }
  | {
      readonly kind: "account-selection-required";
      readonly accounts: readonly AccountChoice[];
    }
  | {
      readonly kind: "authenticated";
      readonly account: Omit<AccountChoice, "id">;
    };

export class AuthenticationUnavailableError extends Error {
  constructor() {
    super("Authentication is currently unavailable.");
    this.name = "AuthenticationUnavailableError";
  }
}

export class AuthenticationStateError extends Error {
  constructor() {
    super("The requested authentication action is not available.");
    this.name = "AuthenticationStateError";
  }
}

export class ReauthenticationRequiredError extends Error {
  constructor() {
    super("Reauthentication is required.");
    this.name = "ReauthenticationRequiredError";
  }
}

export class InteractiveAuthenticationInProgressError extends Error {
  constructor() {
    super("An interactive authentication action is already in progress.");
    this.name = "InteractiveAuthenticationInProgressError";
  }
}

export type PublicClientApplicationFactory = (
  configuration: Configuration,
) => IPublicClientApplication;

export function createMsalAuthenticationAdapter(
  environment: Readonly<Record<string, unknown>>,
  browserOrigin: string,
  createPublicClient: PublicClientApplicationFactory = (configuration) =>
    new PublicClientApplication(configuration),
): MsalAuthenticationAdapter {
  const configuration = validateAuthenticationConfiguration(
    environment,
    browserOrigin,
  );
  return new MsalAuthenticationAdapter(
    configuration,
    createPublicClient(configuration.msal),
  );
}

export class MsalAuthenticationAdapter {
  private currentState: AuthenticationState = Object.freeze({
    kind: "uninitialized",
  });
  private bootstrapPromise?: Promise<AuthenticationState>;
  private selectedAccount?: AccountInfo;
  private choices = new Map<string, AccountInfo>();
  private interactionStarted = false;
  private pendingClaims?: string;

  constructor(
    private readonly configuration: ValidatedAuthenticationConfiguration,
    private readonly client: IPublicClientApplication,
  ) {}

  get state(): AuthenticationState {
    return this.currentState;
  }

  bootstrap(): Promise<AuthenticationState> {
    this.bootstrapPromise ??= this.bootstrapCore();
    return this.bootstrapPromise;
  }

  selectAccount(id: string): AuthenticationState {
    if (this.currentState.kind !== "account-selection-required") {
      throw new AuthenticationStateError();
    }

    const account = this.choices.get(id);
    if (account === undefined) throw new AuthenticationStateError();
    this.select(account);
    return this.currentState;
  }

  async signIn(): Promise<void> {
    if (
      this.currentState.kind !== "signed-out" &&
      this.currentState.kind !== "account-selection-required"
    ) {
      throw new AuthenticationStateError();
    }

    await this.startInteraction(() =>
      this.client.loginRedirect({
        scopes: [this.configuration.apiScope],
        prompt: "select_account",
      }),
    );
  }

  async acquireAccessToken(): Promise<string> {
    const account = this.requireSelectedAccount();
    try {
      const result = await this.client.acquireTokenSilent({
        account,
        scopes: [this.configuration.apiScope],
      });
      if (
        result.accessToken.length === 0 ||
        result.tokenType.toLowerCase() !== "bearer"
      ) {
        throw new AuthenticationUnavailableError();
      }

      this.pendingClaims = undefined;
      return result.accessToken;
    } catch (error: unknown) {
      if (error instanceof InteractionRequiredAuthError) {
        this.pendingClaims = error.claims.length > 0 ? error.claims : undefined;
        throw new ReauthenticationRequiredError();
      }
      if (error instanceof AuthenticationUnavailableError) throw error;
      throw new AuthenticationUnavailableError();
    }
  }

  async reauthenticate(): Promise<void> {
    const account = this.requireSelectedAccount();
    const claims = this.pendingClaims;
    await this.startInteraction(() =>
      this.client.acquireTokenRedirect({
        account,
        scopes: [this.configuration.apiScope],
        ...(claims === undefined ? {} : { claims }),
      }),
    );
  }

  async logout(): Promise<void> {
    const account = this.requireSelectedAccount();
    this.selectedAccount = undefined;
    this.pendingClaims = undefined;
    this.choices.clear();
    this.client.setActiveAccount(null);
    this.currentState = Object.freeze({ kind: "signed-out" });
    await this.startInteraction(() =>
      this.client.logoutRedirect({
        account,
        postLogoutRedirectUri: this.configuration.postLogoutRedirectUri,
      }),
    );
  }

  private async bootstrapCore(): Promise<AuthenticationState> {
    try {
      await this.client.initialize();
      const redirectResult = await this.client.handleRedirectPromise({
        navigateToLoginRequestUrl: false,
      });
      if (redirectResult !== null) {
        this.select(redirectResult.account);
        return this.currentState;
      }

      const accounts = [...this.client.getAllAccounts()].sort(compareAccounts);
      const active = this.client.getActiveAccount();
      const selectedActive =
        active === null
          ? undefined
          : accounts.find((account) => sameAccount(account, active));
      if (selectedActive !== undefined) {
        this.select(selectedActive);
      } else if (accounts.length === 0) {
        this.client.setActiveAccount(null);
        this.currentState = Object.freeze({ kind: "signed-out" });
      } else if (accounts.length === 1) {
        this.select(accounts[0]);
      } else {
        this.client.setActiveAccount(null);
        this.choices = new Map(
          accounts.map((account, index) => [`account-${index + 1}`, account]),
        );
        this.currentState = Object.freeze({
          kind: "account-selection-required",
          accounts: Object.freeze(
            accounts.map((account, index) =>
              Object.freeze({
                id: `account-${index + 1}`,
                username: account.username,
                ...(account.name === undefined ? {} : { name: account.name }),
              }),
            ),
          ),
        });
      }

      return this.currentState;
    } catch {
      throw new AuthenticationUnavailableError();
    }
  }

  private select(account: AccountInfo): void {
    this.selectedAccount = account;
    this.choices.clear();
    this.client.setActiveAccount(account);
    this.currentState = Object.freeze({
      kind: "authenticated",
      account: Object.freeze({
        username: account.username,
        ...(account.name === undefined ? {} : { name: account.name }),
      }),
    });
  }

  private requireSelectedAccount(): AccountInfo {
    if (
      this.currentState.kind !== "authenticated" ||
      this.selectedAccount === undefined
    ) {
      throw new AuthenticationStateError();
    }

    return this.selectedAccount;
  }

  private async startInteraction(action: () => Promise<void>): Promise<void> {
    if (this.interactionStarted) {
      throw new InteractiveAuthenticationInProgressError();
    }

    this.interactionStarted = true;
    try {
      await action();
    } catch {
      this.interactionStarted = false;
      throw new AuthenticationUnavailableError();
    }
  }
}

function compareAccounts(left: AccountInfo, right: AccountInfo): number {
  return accountKey(left).localeCompare(accountKey(right));
}

function sameAccount(left: AccountInfo, right: AccountInfo): boolean {
  return accountKey(left) === accountKey(right);
}

function accountKey(account: AccountInfo): string {
  return [
    account.homeAccountId,
    account.localAccountId,
    account.tenantId,
    account.environment,
  ].join("\u0000");
}
