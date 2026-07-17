#!/usr/bin/env bash
# Architecture gate. Runs the NetArchTest boundary rules when the .NET solution is present;
# falls back to a reserved placeholder before any solution exists.
# Rules enforced (tests/ControlTower.ArchitectureTests): Platform kernel depends on no module;
# no module depends on another module; modules depend on no host. Extended in later phases with
# two-doors / I3-I4 / "no provider SDK outside Providers" / "/poc never referenced by /src".
set -euo pipefail

if command -v dotnet >/dev/null 2>&1 && [ -f ControlTower.sln ]; then
  echo "architecture-gate: running NetArchTest boundary rules"
  dotnet test tests/ControlTower.ArchitectureTests -c Release --nologo
else
  echo "architecture-gate: placeholder (no dotnet/solution yet). TODO: wired in E2."
fi
