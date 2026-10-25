# Implementation-stage review findings

The parent identified these concrete issues during implementation. They are resolved in source and covered by the final executed regressions; this table is not a substitute for the subsequent independent workflow review.

| Finding | Resolution / executed evidence |
| --- | --- |
| Public owned key could expose bytes through default JSON serialization | `CustodyReleasedKey.Bytes` is JSON-ignored. Both the actual SDK/service scenario and Runtime public-contract test assert no bytes serialize; disposal clears the borrowed backing memory and is idempotent. |
| Empty deployment sections and missing G1 proof could evade offline input reporting | Closed required field sets reject missing/extra/wrong-type entries. A retained hash-bound `artifacts.g1Evidence` is required independently of the boolean; complete fixture presence still reports every installed/physical/availability flag false. Offline regressions pass. |
| Package-only Docker source recipe depended on an unavailable source/package graph | Dockerfile consumes a complete reviewed publish-output inventory. The explicit source-graph Release publish succeeded and all 41 emitted files passed offline context validation. The initial blocked context remains as intermediate evidence. |
| Sidecar-local output binding placement and startup were unclear | Template explicitly places the separately reviewed custody app with daprd/component, starts the component's protected exact socket before daprd discovery, and documents authenticated remote ingress plus independent authorizer prerequisites. YAML parses offline; installed qualification remains absent. |
| Per-key Destroy needed its original complete reserved manifest | The existing lifecycle port now resolves that manifest by exact reservation, verifies full retained registration, delegates all-or-none consumption and recovers exact target receipt/restore barrier. Full retry and subset-manifest refusal execute in the two-target lifecycle scenario. |
| Target-triple receipt could be projected onto substituted key/lifecycle/store/contract versions | Authenticated full-registration lookup now precedes any destruction delegation. All four full-identity substitutions return Unknown in the public two-target recovery scenario. |
| Generic profile/copy/holder inputs hid installation detail; several null-profile reasons repeated | Explicit member inventory names account/region/table, bucket enrollment/version/retention, epochs/KEKs, genesis/trust/revocation and actual copy/holder/producer/closure owners. Top-level ledger deduplicates reasons per input; missing owning services and EventStore source work have separate dispositions. |

Parent-owned independent review findings and final acceptance remain to be appended by that workflow.
