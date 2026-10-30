# Input evidence assessment — 2026-10-29

All 37 required fields were checked against Platform cluster-management definitions,
Epic 4 evidence, the original management qualification and current native observations.
Only the two DNS fields are supported. Every populated field in
`inputs.resolved.json` has its source path, JSON pointer, observation cut and content
hash in `_provenance`; this metadata leaves the input values compatible with the
unchanged renderer. Unknown values remain JSON null.

## Resolved facts and their limits

The [current native observation](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-14/live-apply-approval-2026-10-29/native-observation.json)
records the workload query at `2026-10-29T17:30:45.207505+00:00`
(collection completed `2026-10-29T17:30:48.110013+00:00`). CoreDNS Service
`/observations/workloads/records/105`: namespace `kube-system`, selector
`{"k8s-app":"kube-dns"}`, UDP/TCP port 53. These establish
`network.dns.namespace` and `network.dns.podLabels` as observations, without
proving that the rendered policy permits the effective resolver path.

The [4.26 production census](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261004t085143z-census/inventory.json)
corroborates the historical namespace at `/resources/274`, `/resources/275`
(CoreDNS pods) and `/resources/760` (Deployment). Its sanitized management labels
do not expose DNS pod selectors; the selector comes from the new observation.
Current record 54 is a NodeLocal DNS DaemonSet. The pod resolver address,
host-network policy behavior and end-to-end DNS allow path are unqualified.

## Observations that do not resolve deployment choices

| Input family | Investigated evidence | Why the fields stay null |
|---|---|---|
| Storage provider, parameters, reclaim policy and class names | 4.26 census `/resources/1337` (`openebs-hostpath`, `openebs.io/local`, `Delete`) and `/resources/1338` (`openebs-hostpath-retain`, same provider, `Retain`) | Existing classes do not select an encrypted staging provisioner/configuration or designate the production class to deny. Parameters are not exposed as approved staging inputs. |
| Encryption attestation and Administrator/deputy holders | Story 4.14 storage criterion; [management qualification](../cluster-management/QUALIFICATION.md), approved access/custody section; [4.26 authorization](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261004t085020z-owner-authorization/authorization.json) `/deputy=null` | A management design with no named deputy does not attest staging volume encryption or key custody. Native principal authentication supplies neither key holders nor an encryption attestor. |
| Production class and priority values | 4.26 census `/resources/1496` and `/resources/1497` | System-critical PriorityClasses are not designated production environment classes; no approved staging/production values are recorded. |
| Layer and per-job deploy principals | [Independent native access](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261004t112212z-independent-native-access/independent-native-access.json), `/productionAccepted=false`; current native identity observation | Recovered/current operator access is not a selection or qualification of separated shared, environment and job deploy identities. |
| LimitRange quantities and all quotas | [Story 4.14 requirements](../../_bmad-output/planning-artifacts/epics.md), Story 4.14; existing input example and tests | Requirements impose isolation and limits but supply no approved quantities. Test values are fixtures. Current host capacity is not a quota decision. |
| Native-TLS service inventory and ports | Story 4.14 data-service criterion; existing renderer/test fixtures and future 4.15/4.16 contracts | Conventional PostgreSQL, Redis or OpenBao ports do not establish a staging service's TLS configuration. No complete approved service inventory is evidenced. |

The actual evidence directory is
`_bmad-output/implementation-artifacts/evidence/epic-4/`; there is no root-level
`evidence/epic-4/`. Platform source hashes are in `inputs.resolved.json`
(`_provenance`) and `gates.assessed.json`; the separate external-context inspection
and its hashes are retained in the [investigation receipt](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-14/live-apply-approval-2026-10-29/investigation.json).
External consumer context supplies no additional Platform input or acceptance gate.

The existing policy generator also does not encode per-service pod selectors or
data replication topology and denies the named production StorageClass rather
than every other class. Input completion alone is not proof of all storage or
network acceptance criteria; those constraints need reviewed definitions and live
qualification before Story 4.14 can be accepted. No validator or policy behavior
was changed by this approval preparation.
