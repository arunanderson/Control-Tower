import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { startControlTowerApplication } from "./startup";

async function start(): Promise<void> {
  const root = createRoot(document.getElementById("root")!);
  await startControlTowerApplication({
    environment: import.meta.env,
    browserOrigin: window.location.origin,
    render: (application) =>
      root.render(<StrictMode>{application}</StrictMode>),
  });
}

void start();
