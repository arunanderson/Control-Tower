import { isValidElement } from "react";
import type { ControlTowerApi } from "./api/client";
import type { MsalAuthenticationAdapter } from "./auth";
import { startControlTowerApplication } from "./startup";

test("redirect bootstrap completes before the API and protected React tree are created", async () => {
  let completeBootstrap!: () => void;
  const bootstrap = vi.fn(
    () =>
      new Promise<void>((resolve) => {
        completeBootstrap = resolve;
      }),
  );
  const authentication = {
    bootstrap,
  } as unknown as MsalAuthenticationAdapter;
  const api = {} as ControlTowerApi;
  const createAuthentication = vi.fn(() => authentication);
  const createApi = vi.fn(() => api);
  const render = vi.fn();

  const pending = startControlTowerApplication({
    environment: { public: "configuration" },
    browserOrigin: "https://controltower.example",
    render,
    createAuthentication,
    createApi,
  });

  expect(bootstrap).toHaveBeenCalledTimes(1);
  expect(createApi).not.toHaveBeenCalled();
  expect(render).not.toHaveBeenCalled();

  completeBootstrap();
  await pending;

  expect(createApi).toHaveBeenCalledWith(authentication);
  expect(render).toHaveBeenCalledTimes(1);
  const application = render.mock.calls[0][0];
  expect(isValidElement(application)).toBe(true);
  expect(application.props).toMatchObject({ authentication, api });
});

test("startup failures render one generic administrator-facing state", async () => {
  const render = vi.fn();
  const createAuthentication = vi.fn(() => {
    throw new Error("secret configuration details");
  });

  await startControlTowerApplication({
    environment: {},
    browserOrigin: "https://controltower.example",
    render,
    createAuthentication,
  });

  expect(render).toHaveBeenCalledTimes(1);
  expect(JSON.stringify(render.mock.calls[0][0])).not.toContain(
    "secret configuration details",
  );
});
