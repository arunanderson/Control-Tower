// @vitest-environment node

import { developmentApiTarget, developmentProxy } from "../vite.config";

test.each([
  [undefined, "https://localhost:7263"],
  ["http://localhost:5000", "http://localhost:5000"],
  ["https://127.0.0.1:7263", "https://127.0.0.1:7263"],
  ["https://[::1]:7263", "https://[::1]:7263"],
])("accepts only a loopback development origin: %s", (value, expected) => {
  expect(developmentApiTarget(value)).toBe(expected);
});

test.each([
  "not a URL",
  "ftp://localhost",
  "https://controltower.example",
  "https://localhost.example",
  "https://user:secret@localhost",
  "https://localhost/path",
  "https://localhost?unsafe=true",
  "https://localhost#unsafe",
])("rejects unsafe development target %s without disclosing it", (value) => {
  let failure: unknown;
  try {
    developmentApiTarget(value);
  } catch (error: unknown) {
    failure = error;
  }
  expect(failure).toBeInstanceOf(Error);
  expect(String(failure)).not.toContain(value);
});

test("the proxy exposes only exact whoami and api paths without changing Host", () => {
  const proxy = developmentProxy("https://localhost:7263");
  expect(Object.keys(proxy)).toEqual(["^/whoami$", "^/api(?:/|$)"]);
  expect(proxy).toEqual({
    "^/whoami$": {
      target: "https://localhost:7263",
      changeOrigin: false,
      secure: false,
    },
    "^/api(?:/|$)": {
      target: "https://localhost:7263",
      changeOrigin: false,
      secure: false,
    },
  });
});
