# Story 4.14 offline hardening handoff

Status: `BLOCKED_NOT_FOR_EXECUTION`. Story 4.14 remains in-progress.
`approvalGranted=false`, `liveAcceptance=false`; no cluster or network call was
made, no generated command was run, and no operational approval is implied.

## Guarded candidate path

Use [preflight.py](preflight.py) as the candidate `render` and `plan` entry
point. It validates all required fields through the source-pinned
[renderer](render.py), then checks Kubernetes quantity syntax and nonnegative
values for LimitRange and ResourceQuota inputs. It also checks default CPU and
memory requests do not exceed default limits, PVC minimum does not exceed
maximum, and each namespace with a positive pod or PVC quota can fit one
declared default pod or minimum claim within the matching resource quota.
Only a passing input reaches the pinned renderer in process. Invalid and
unresolved candidates exit 2 without qualification or manifest output.

The current [resolved inputs](inputs.resolved.json) contain two observed DNS
fields and 35 null fields, so guarded render and plan both refuse. The
[historical assessment](unblocking.assessed.json) and
[owner questions](MISSING-INPUTS.md) retain the exact inventory and evidence.
Synthetic fixtures in tests demonstrate validation behavior; they are not
owner choices or approved deployment values. A valid offline plan remains
blocked and all emitted stage actions remain `executable: false`.

## Probe ownership

[cni_probe.py](cni_probe.py) now requires a caller-supplied unique attempt ID,
future timezone-aware cleanup deadline, and digest-pinned image. It derives
the namespace from the attempt ID and marks resources with that ID. Its
proposal lists a read-only collision check before creation, a post-creation
UID check, and a read-only identity check before cleanup. A reported collision
or missing absence claim refuses the creation proposal; an absence claim is
still unverified offline and never qualifies a create action. Result evaluation binds
one attempt marker to one control and one denied-peer result; missing,
duplicate or malformed output is inconclusive, and a reachable denied peer
fails. Evaluation does not qualify cleanup.

A cleanup proposal requires the creation-time UID and a fresh namespace
observation with matching name, UID, ownership labels and deadline. It records
the matched UID as a required precondition but emits no delete action. Absent
or mismatched observation refuses; matching supplied observations remain
unverified and cleanup remains blocked. A later authorized operator must
independently verify ownership, namespace contents and exact target before any
effect. A read-only check and a subsequent operation are not atomic; the later
live procedure must account for drift and obtain exact operational approval.

## Remaining gates

Four owner decision groups remain unanswered: storage/custody/priority,
deploy identities and RBAC, resource budgets, and the limited foundation
milestone/provider-bootstrap/final acceptance sequence. Five prerequisites
(4.1, 4.2, 4.3, 4.27 and 4.28) lack full acceptance. No exact operational
approval exists. The [unblocking report](UNBLOCKING-REPORT.md) details
remaining definition gaps in storage and priority admission, RBAC, effective
DNS, per-service network/TLS flows, provider bootstrap, and exact target and
credential binding. Quantity validation and probe ownership proposals close
only their offline gaps; they do not prove runtime isolation, encryption,
permissions, TLS, CNI enforcement or an authenticated target. No credentials,
policies, dependency acceptance or live effects were selected here.

The six Story 4.15-pinned foundation files and historical evidence/receipts
retain their original bytes. The guarded path is additive. The source-pinned
renderer and its blocked plan remain as preserved historical inputs.
