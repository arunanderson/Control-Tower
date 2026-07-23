# PoC-2 findings — sandbox readiness run (2026-07-23)

## Observations

- The sandbox contains no published Copilot Studio test agent and therefore no package
  `manifestId` to use as the comparison value.
- No representative `TEST_BOT_ID` or corresponding Dataverse `bot` / `botcomponent` publication
  rows were available.

## Outcome

**Not executable — representative test data is absent.** No conclusion can be drawn about manifest
ID recoverability. This is not evidence of a permanent manifest-ID hole.

## Required rerun condition

Rerun after a representative Copilot Studio agent is published to Microsoft 365/Teams and its
Dataverse environment and bot identifier are available.
