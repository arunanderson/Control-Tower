#!/usr/bin/env bash
# Fail if a change touches a protected path. Human-authored changes to these go through
# CODEOWNERS + branch protection; the agent must never modify them.
# Usage: validate_protected_paths.sh [BASE_REF]   (default: origin/main)
set -euo pipefail

BASE="${1:-origin/main}"
PROTECTED_REGEX='^(docs/blueprint/|docs/build/approvals/)'

if ! git rev-parse --verify "$BASE" >/dev/null 2>&1; then
  echo "base ref '$BASE' not found; comparing against empty tree"
  BASE=$(git hash-object -t tree /dev/null)
fi

CHANGED=$(git diff --name-only "$BASE"...HEAD || true)
VIOLATIONS=$(printf '%s\n' "$CHANGED" | grep -E "$PROTECTED_REGEX" || true)

if [ -n "$VIOLATIONS" ]; then
  echo "PROTECTED-PATH VIOLATION — these paths must not be modified in this PR:"
  printf '%s\n' "$VIOLATIONS"
  exit 1
fi
echo "OK: no protected-path (docs/blueprint, docs/build/approvals) modifications."
