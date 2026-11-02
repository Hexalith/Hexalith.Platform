# Staging foundation (Story 4.14) - BLOCKED, offline only

Status: `BLOCKED_NOT_FOR_EXECUTION`. `approvalGranted=false`, no executable dispatcher exists, and nothing here touches the cluster. Story 4.14 stays in-progress. Required gates 4.1, 4.2, 4.3, 4.27 (in-progress) and 4.28 (backlog) lack full dependency acceptance. Story 4.2 has accepted scoped exposure/registry results; its full workflow review remains pending. See the evidence-backed [approval package](APPROVAL-PACKAGE.md) and [gate assessment](gates.assessed.json).

The [historical unblocking handoff](UNBLOCKING-REPORT.md) and
[source-bound assessment](unblocking.assessed.json) retain the exact 37-field
inventory (two observed DNS fields, 35 nulls), the owner decisions pending at
that historical cut, definition gaps and requirements for later approval.
The [historical offline hardening handoff](OFFLINE-HARDENING-HANDOFF.md) records
the earlier guarded path. The six foundation files pinned
by Story 4.15 and the historical receipts remain unchanged. Approval and live
acceptance remain false.

The **2026-11-02 approved offline candidate** is now
[candidate.py](candidate.py), with [selected inputs](inputs.candidate.json) and
the [continuation package](CONTINUATION-PACKAGE.md). “Apply recommended” resolved
the four design decision groups and authorized these additive definitions. Real
mounts/devices, encryption/custody, principals, provider/role inventories, actual
DNS/services, capacity, target/tool/image and accepted dependency facts remain
missing. Assessment and rendering refuse them with field-specific errors; the
current candidate creates no deployment-manifest directory. Native admission,
separate grants, per-service flows and bounded storage proposals remain subject
to independent runtime qualification. Application deployment grants stay
withheld throughout the staged foundation/provider acceptance path.

## Contents

| File | Purpose |
|---|---|
| `inputs.candidate.json` | Approved storage/priority/budgets, explicit proposed temporary-storage and XFS quota defaults, decision provenance and unresolved real facts. |
| `candidate.py` | Current additive stdlib-only `assess`, `render` and `plan`; exact source/input/manifest/target/identity binding, native admission, separated grants, per-service policies and blocked stage ownership. |
| `candidate_probe.py` | Explicit public target/exact observer binding, per-attempt read-only observer grants, staging priority, fresh owned attempt, independent ingress/egress denial phases bracketed by both-peer positive controls, strict UID/phase consistency and unqualified finite cleanup proposal. |
| `CONTINUATION-PACKAGE.md` | Selected values, precise missing facts, stage owners, provider TLS proof, operational authorization, enforcement limitations and forward recovery. |
| `test_candidate.py` / `test_candidate_probe.py` | New positive/refusal and static-definition coverage; no live CEL, RBAC, CNI or TLS proof. |
| `inputs.example.json` | Required-input schema, all `null`. The renderer refuses it. Provisioner, encryption attestation, custody holders, production StorageClass, deploy-identity names, quotas and DNS selectors are never defaulted. |
| `inputs.resolved.json` | Same input schema, with two cited DNS observations and 35 null fields; `_provenance` records source paths, JSON pointers and hashes. Still refused. |
| `MISSING-INPUTS.md` / `INPUT-EVIDENCE.md` | Exact question for every null field, additional live prerequisites and reasons historical observations cannot become defaults. |
| `APPROVAL-PACKAGE.md` / `gates.assessed.json` | Current approval boundary, per-stage refusal, target, effects, read-only checks, forward recovery and dependency acceptance audit. |
| `render.py` | `render` writes `shared-infrastructure.yaml` and `environment-layer.yaml`; `plan` prints the concrete apply plan. Refuses (exit 2, no output) on missing/placeholder input or a target other than `jpiquot@local` / `b5860674-1d1e-4a3b-b413-e1bb3b9a4557`. |
| `preflight.py` | Preserved earlier guarded entry point and quantity helper reused by the additive candidate. |
| `cni_probe.py` | Per-attempt Calico enforcement probe: digest-pinned manifests, collision and UID observation checks, strict result evaluation, and a blocked cleanup proposal that records the checked UID condition. |
| `apply-plan.json` | Static blocked package: target, unmet dependencies, effects per stage, job-identity denial checks, forward recovery. |
| `test_staging_foundation.py` | Unit tests (no cluster call; sources cannot spawn processes). |

## Preserved historical layers (AR-32)

- Shared-infrastructure identity: Namespaces `hexalith-staging` and `hexalith-staging-data` (Pod Security `restricted` enforce/audit/warn), staging StorageClass, PriorityClass `hexalith-staging` (value strictly below the production class).
- Environment-layer identity: ResourceQuota (denies the production StorageClass by zeroing its PVC count and storage), LimitRange (including PVC min/max), `default-deny-all` (Ingress+Egress), DNS egress, application-to-data TLS-port-only policies, and the data-service TLS contract ConfigMap. Certificates and keys belong to 4.15/4.16.
- Workload Gateway/egress allowances remain the workload layer's job. The foundation is module-agnostic; see [DEPENDENCY-DIRECTION.md](DEPENDENCY-DIRECTION.md).

## Offline use

The current candidate commands are:

```sh
python3 -B eng/staging-foundation/candidate.py assess --inputs eng/staging-foundation/inputs.candidate.json
python3 -B eng/staging-foundation/candidate.py render --inputs eng/staging-foundation/inputs.candidate.json --output /tmp/staging-candidate-not-created
python3 -B eng/staging-foundation/candidate.py plan --inputs <completed-inputs.json> --manifest-dir <exact-rendered-directory>
python3 -B -m unittest discover -s eng/staging-foundation -v
python3 -B -m unittest discover -s eng/staging-secrets -v
```

The first two exit 2 on current missing facts; render creates no directory.
Only complete, freshly bound inputs render five deterministic layer files into
a new directory. Plan rejects missing/extra/altered/symlink files and remains
blocked even for complete synthetic inputs. It authenticates no local evidence.
Native policy/binding ownership uses RBAC and the trusted shared workflow:
Kubernetes excludes those two kinds from native policy self-matching. Read the
continuation package for the exact qualification and recovery boundary.

The additive probe entry point is `candidate_probe.py
proposal|evaluate|cleanup-proposal --request <JSON>`. Every request requires the
explicit public `target` object used by the candidate and an exact `observer`
principal. Target facts and observer are unknown, never defaulted; proposed read
argv bind the supplied absolute tool/context/server and require independently
verified credentials, CA and cluster UID. Administrator separately installs the
proposed per-attempt observer Role/Binding after namespace creation. Its grants
read Pods/logs, Services/Endpoints, EndpointSlices and policies without exec,
impersonation or mutation. The [request field table](CONTINUATION-PACKAGE.md#offline-reproduction-and-recovery)
and [observation authority](CONTINUATION-PACKAGE.md#probe-proposal-evidence-and-cleanup)
describe the exact shapes and remaining authority requirements. Creation freshness
is five minutes; existing results use the whole finite attempt window. Fresh
owned cleanup observations may follow an expired attempt; no cleanup action is
qualified or executed offline. Pods use `fsGroup=65532` for scratch and an absolute
UTC deadline guard plus relative `activeDeadlineSeconds`. The image must qualify
non-root `sh`, UTC `date +%s`, `timeout -s KILL`, `httpd -f` and `nc -z -w` behavior.
Each phase proposes a marked, bounded client exec measurement under separately
authorized active authority and read-only observer collection. Phase evidence
includes the exact policy names, stable UIDs and canonical spec hashes for that
independent direction; missing/drifted/replaced policy observations refuse.
Cleanup checks reviewed grants, Service routing and complete phase specs, and
compares the DNS fingerprint saved in the Namespace annotation. A later live
verifier must authenticate that saved proposal/annotation; offline comparison
qualifies nothing. Ordinary default ServiceAccount/root-CA, Service-attributed
Endpoints/EndpointSlices and relevant UID-attributed Events are inventoried
separately without claiming attempt ownership. Foreign/unattributed content
still refuses. CLI evaluation exits 0 for offline consistency and 2 for refusal;
malformed requests emit structured blocked JSON.

The candidate rejects anonymous authority, an observer ServiceAccount in its own
probe namespace, privileged ServiceAccounts in either workload namespace,
extra service fields, additional APP-to-DATA flows outside the native-TLS service
inventory and invalid namespace/label syntax. Literal-only DNS peers cover exactly
the declared resolver IPs; resolver literals must be specified unicast addresses
and peers use actual CIDR strings. HTTPS API hosts and DNS-subdomain names receive
syntax checks. Images use the [documented practical pinned-reference subset](CONTINUATION-PACKAGE.md#probe-proposal-evidence-and-cleanup).
Selector/NAT behavior remains unqualified. Disk emptyDir caps are strictly positive
and at most 256Mi. Dapr
Configurations, WorkflowAccessPolicies, Subscriptions and Resiliencies belong to
the application's approved inventory; Components, HTTPEndpoints and MCPServers
remain environment-owned. Static policy tests establish no API type-check,
runtime CEL, effective RBAC, DNS, TLS or live isolation acceptance.

The following historical entry points remain preserved for reproducibility:

```
python3 -B -m unittest discover -s eng/staging-foundation -v
python3 -B eng/staging-foundation/preflight.py render --inputs <real-inputs.json> --output <dir>
python3 -B eng/staging-foundation/preflight.py plan --inputs <real-inputs.json> --observed-context <ctx> --observed-cluster-uid <uid> --manifest-dir <dir>
```

The historical `inputs.resolved.json` still
has 35 missing values; both guarded commands exit 2 without manifest output.
The `plan` output lists, per identity, dry-run argv (`--dry-run=server`) and
apply argv (marked `executable: false`), read-only verification `get` argv, and
`auth can-i ... --as <job>` checks that must all answer `no` (exit 1). Run
nothing until every dependency has accepted evidence and the owner approves the
exact bytes.

## Recovery

Forward-only: re-run dry-run then re-apply the same reviewed manifests. Never `helm rollback`, delete namespaces/PVCs/PVs, or rewind data or credentials. StorageClass provisioner/parameters/reclaimPolicy are immutable; change them only through a new class name. Data recovery needs a separate exact owner-approved operation.

Notes: the PriorityClass `value` is immutable, so a changed value needs a new class name. The environment-layer server dry-run only works after the shared-infrastructure apply has created the namespaces. The first verification command reads the `kube-system` namespace UID and must equal the target cluster UID before and after every stage. A second quota (`deny-production-priority`) sets `pods=0` for the production PriorityClass name, since a quota scoped to the staging class could not stop use of the production one.

## CNI probe

`cni_probe.py manifests` requires `--attempt-id`, `--cleanup-deadline` (a future
ISO 8601 timestamp with timezone), a digest-pinned busybox-compatible (`httpd`,
`nc`) `--image`, DNS inputs and `--output`. It writes a restricted,
default-deny namespace named `hexalith-cni-probe-<attempt-id>`, plus probe and
client manifests. The printed sequence includes a collision check, a
post-creation UID check and a cleanup identity check; creation and cleanup
remain unqualified. `creation-proposal` takes a checked-absence JSON claim and
refuses a reported collision or missing claim. The supplied claim remains
unverified offline and contains no create action. `cni_probe.py
evaluate --attempt-id <id> --log-file
<client-log>` requires one matching attempt marker and one result for each peer;
a missing control, malformed or duplicate output cannot pass. A reachable
denied peer fails. `cleanup-proposal` requires the creation-time UID and fresh
observed namespace JSON with matching name, UID, attempt label and deadline.
It records the matching UID as a required precondition, with no delete action;
the observation and cleanup remain unqualified offline. A later live procedure
requires independent ownership and namespace-content evidence, exact target
binding and operational approval before any cleanup.
