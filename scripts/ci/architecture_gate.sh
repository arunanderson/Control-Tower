#!/usr/bin/env bash
# Placeholder architecture gate. Reserves the gate before feature code exists.
# TODO (Phase-0 E2): replace with NetArchTest-class rules enforcing:
#   two doors (C4 in/out, C7 experience), I3/I4, module boundaries,
#   "no provider SDK outside /Providers", "/poc never referenced by /src",
#   "no query without tenancy context", and the DEV-001 adapter boundary.
set -euo pipefail
echo "architecture-gate: placeholder (no domain code yet). TODO: wire NetArchTest in E2."
exit 0
