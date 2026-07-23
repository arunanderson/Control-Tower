# Build risk register

| ID       | Risk                                                      | Source          | Handling                                                                                           | Status     |
| -------- | --------------------------------------------------------- | --------------- | -------------------------------------------------------------------------------------------------- | ---------- |
| R-08     | Employee trust, privacy or works-council rejection        | ADR-003/DEV-002 | L1 default; content excluded; L2+ explicit activation; jurisdiction policy; pre-deployment gate    | open       |
| R-12     | Correlation quality at real scale                         | handoff §9      | Gate-1 PoCs plus cross-source golden fixtures; uncertainty remains categorical and visible         | open       |
| R-17     | Microsoft or vendor API churn/deprecation                 | Stage 3 §8      | Versioned provider contracts, roadmap watch, health/freshness and replaceable acquisition adapters | open       |
| R-23     | Modular-monolith boundary erosion                         | Stage 7         | Architecture tests in CI; every acquisition source enters only through C4                          | mitigating |
| R-28     | Single-store concentration/RLS performance                | Stage 9         | DEC-001, representative-volume RLS/load tests, stamp scaling and restore drill                     | open       |
| R-30     | Honesty-softening or unfalsifiable visibility claims      | handoff §2      | Defensible denominators only; coverage, blind spots, freshness and evidence class always visible   | monitored  |
| R-DEV1   | Development substitute leaks into production              | DEV-001         | Production-readiness CI gate, architecture tests and substitute registry                           | mitigating |
| R-ENDPT  | Collector compromise, tampering or excessive collection   | DEV-002         | Signed packages/events, tenant enrolment, anti-replay, minimisation, kill switch and threat model  | open       |
| R-DOUBLE | The same activity is double-counted across evidence feeds | DEV-002         | Explicit snapshot/event/time-bucket semantics and evidence-link deduplication                      | open       |
| R-GATE1  | Representative Microsoft agent data/licensing unavailable | Phase-0 plan    | Sandbox readiness evidence recorded; provider build can continue, real confidence rules stay gated | open       |
