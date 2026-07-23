import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import { loadEnv } from "vite";

export function developmentApiTarget(value: string | undefined): string {
  let target: URL;
  try {
    target = new URL(value ?? "https://localhost:7263");
  } catch {
    throwInvalidDevelopmentTarget();
  }
  const loopbackHosts = new Set(["localhost", "127.0.0.1", "[::1]"]);
  if (
    !["http:", "https:"].includes(target.protocol) ||
    !loopbackHosts.has(target.hostname) ||
    target.username ||
    target.password ||
    target.pathname !== "/" ||
    target.search ||
    target.hash
  ) {
    throwInvalidDevelopmentTarget();
  }
  return target.origin;
}

export function developmentProxy(apiTarget: string) {
  return {
    "^/whoami$": {
      target: apiTarget,
      changeOrigin: false,
      secure: false,
    },
    "^/api(?:/|$)": {
      target: apiTarget,
      changeOrigin: false,
      secure: false,
    },
  };
}

export default defineConfig(({ mode }) => {
  const environment = loadEnv(mode, ".", "");
  const apiTarget = developmentApiTarget(
    environment.CONTROL_TOWER_DEV_API_TARGET,
  );

  return {
    plugins: [react()],
    server: {
      proxy: developmentProxy(apiTarget),
    },
    test: {
      globals: true,
      environment: "jsdom",
      setupFiles: ["./src/test-setup.ts"],
    },
  };
});

function throwInvalidDevelopmentTarget(): never {
  throw new Error(
    "CONTROL_TOWER_DEV_API_TARGET must be a credential-free loopback origin.",
  );
}
