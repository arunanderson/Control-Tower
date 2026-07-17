# Build risk register (Phase 0)

| ID      | Risk                                          | Source       | Phase-0 handling                                     | Status     |
| ------- | --------------------------------------------- | ------------ | ---------------------------------------------------- | ---------- |
| R-12    | Correlation quality at real scale             | handoff §9   | Gate-1 PoCs (E4) — blocked on tenant                 | open       |
| R-17    | Microsoft API churn / deprecation             | Stage 3 §8   | Stage 3 re-validation at kickoff (E5)                | open       |
| R-23    | Modular-monolith boundary erosion             | Stage 7      | Architecture tests in CI from first code (E1/E2)     | mitigating |
| R-28    | Single-store concentration / RLS perf         | Stage 9      | DEC-001 + RLS spike (E2/E6)                          | open       |
| R-30    | Honesty-softening pressure                    | handoff §2   | Escalation path; categorical-not-numeric enforced    | monitored  |
| R-DEV1  | Dev substitute leaks into production          | DEV-001      | production-readiness CI gate + arch tests + registry | mitigating |
| R-GATE1 | Gate-1 tenant not provisioned → Stage 5 slips | Phase-0 plan | Escalated to Arun; PoC execution BLOCKED             | open       |
