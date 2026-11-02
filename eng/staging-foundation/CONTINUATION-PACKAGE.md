# Approved offline candidate — 2026-11-02

`BLOCKED_NOT_FOR_EXECUTION`. `approvalGranted=false`, `liveAcceptance=false`.
Story 4.14 stays **in-progress**. The instruction “apply recommended” approved
this additive offline implementation, including the four previously pending
decision groups. It supplied no operational facts, accepted dependency evidence,
credentials or authority to contact or mutate a cluster. No generated operation
has been executed.

The current entry point is [candidate.py](candidate.py), with selected values and
explicit nulls in [inputs.candidate.json](inputs.candidate.json). The historical
renderer, six Story 4.15 source pins, assessments and receipts retain their bytes.
The prior reports describe their original evidence cut; their unanswered design
choices are superseded only to the extent recorded by this approved candidate.

## Selected definitions and required facts

| Area | Selected candidate definition | Required operational facts and evidence |
| --- | --- | --- |
| Storage | `openebs.io/local`; new `hexalith-staging` class, empty `parameters`, annotation-configured `StorageType=hostpath` and BasePath; `WaitForFirstConsumer`, `Retain`, no expansion or default-class selection | Actual production class, separate staging/production mounts and devices, encryption and independent Administrator/deputy custody, actual provisioner/binder usernames, compatible provider/admission versions and supported local-PV binding behavior |
| Filesystem quota | Proposed XFS with `prjquota`; OpenEBS `XFSQuota` enabled, zero soft/hard grace | Actual OpenEBS support for the exact annotation and filesystem project-quota behavior, backing mount/device identity, capacity/reserve and encryption/custody proof. These are candidate defaults, not an observation of a filesystem |
| Priority | Staging `-1000`, `preemptionPolicy=Never`; planned production `1000` | Actual distinct production class and value qualification; fresh admission/defaulting and controller-created Pod proof |
| Identities | Separate Administrator, shared deploy, environment deploy, application deploy and observer; distinct provisioner/binder principals; reject anonymous authority and privileged ServiceAccounts in either workload namespace or the observer's own probe namespace | Exact principals, public credential/identity qualification, group and inherited-grant audit; no credential or binding to application deploy is issued |
| Network | Calico; default deny both directions, actual DNS peers on TCP/UDP 53; one source/destination selector and native TLS port per service | Actual resolver addresses/NodeLocal or host-network behavior, complete data-service inventory, provider TLS contracts, explicit replication/HA/monitoring/backup/custody flow review and narrowly selected additional staging flows |
| RBAC inventory | Environment owns Dapr Components, HTTPEndpoints and MCPServers; application package owns its approved Configurations, WorkflowAccessPolicies, Subscriptions and Resiliencies alongside workload/Service/HTTPRoute kinds | Explicit complete namespaced provider-CRD and application-package resource inventories, source evidence and actual effective permissions; provider CRD scope is independently qualified, never inferred from a kind name |
| Target | No observed server, identity, CA, UID, tool, image or service is adopted | Exact context/API HTTPS origin with a valid DNS host or IP, kube-system UID, CA digest, public credential identity digest, absolute kubectl path/digest/version, attempt ID and fresh observation/expiry; digest-pinned image qualified for non-root `sh`, UTC `date +%s`, `timeout -s KILL`, `httpd -f` and `nc -z -w` |

Application quota requests are **4 CPU / 8Gi**, limits **8 CPU / 16Gi**,
**30 Pods**, **0 PVCs / 0 storage**. Data has the same CPU/memory budgets,
**20 Pods**, **12 PVCs / 100Gi**. Container defaults are requests
**100m / 128Mi**, limits **500m / 512Mi**. PVC bounds are **1Gi–50Gi**.
Additional proposed temporary-storage defaults are **64Mi request / 256Mi
limit per container**, **2Gi request / 8Gi limit per namespace**, and a
**strictly positive disk emptyDir size up to 256Mi**, with zero/unbounded and
memory emptyDir refused. Quantity
validation reuses `preflight.quantity`, checks ordering and requires declared
staging capacity for the sum of namespace limits and data storage. Capacity
means staging capacity after production/system reservations. Composition,
headroom, actual filesystem enforcement, kubelet accounting and eviction
behavior still require qualification; offline arithmetic proves none of them.

`network.additionalFlows=[]` is permitted only with all five explicit flow-review
statements and source evidence. Additional flows are confined to exact staging
namespace/pod peers. Every APP-to-DATA allowance must appear in the authoritative
native-TLS service inventory; additional flows cannot add another APP-to-DATA
port or selector. Service input fields are closed and the TLS ConfigMap projects
only the public name, selectors, native-TLS port/requirement and evidence reference.
Credentials and extra service fields refuse assessment before any rendering.
External provider backup/monitoring requirements require a
reviewed forward policy change before bootstrap; a guessed external allowance
is never emitted. DNS peer definitions accept only a concrete namespace plus
pod selector or a string-valued single resolver CIDR `/32` or `/128`; numeric,
non-CIDR, unspecified and multicast resolver paths refuse. Actual reachability is
independently tested. Literal-only peers must cover exactly the declared resolver
addresses. Namespace names and label keys/values use Kubernetes syntax; selector
or NAT translation paths still require independent live qualification. TLS port
allowances do not terminate or enforce TLS.

## Ordered owners and acceptance boundaries

Every stage is blocked and `executable=false`. Generated manifests are split so
namespaced grants and environment server dry-runs cannot precede namespace
creation. There is no executable dispatcher or command runner.

| Order | Owner | Exact candidate scope |
| --- | --- | --- |
| 1 | Administrator | `00-access-cluster.yaml`: install shared deploy and separate observer cluster grants after exact authorization; create no credentials |
| 2 | Shared infrastructure | `10-admission.yaml`: reviewed native admission policies/bindings, followed by isolated API type-check and runtime qualification |
| 3 | Shared infrastructure | `20-shared-infrastructure.yaml`: restricted staging namespaces, staging StorageClass and PriorityClass; requires accepted **4.1, 4.2, 4.3, 4.27 and 4.28**, plus exact namespace-creation authorization |
| 4 | Administrator | `25-access-namespaced.yaml`: environment/observer grants and an **unbound** application role; existing shared privileges cannot bootstrap or escalate these grants |
| 5 | Environment layer | `30-environment-layer.yaml`: quotas, defaults, DNS, default-deny and per-service/explicit flow rules plus the TLS contract; namespace existence precedes dry-run |
| 6 | Separate observer | Limited foundation milestone: positive/negative permission, restricted Pod, priority, storage/binding, encryption/custody, quota, DNS and independent CNI-direction checks |
| 7 | Environment layer | Separately authorized, bounded provider bootstrap using separately reviewed exact provider bytes, digests, accounts, credential custody, services, deadlines and forward recovery; no provider workload/secret values are generated here |
| 8 | Separate observer and entitled acceptance owner | Actual-provider native TLS certificate/identity and handshake proof; plaintext, cross-environment and unauthorized-peer denial with positive controls; final independent foundation acceptance |

**Application deployment authority stays withheld at every stage**, including
after a synthetic or limited foundation pass. No application RoleBinding is
rendered. Future application authority requires a separate decision and accepted
live criteria. Stories 4.15/4.16 provide downstream provider material; their story
acceptance is never a reverse gate in foundation definitions.

All five dependencies currently lack full accepted completion. Scoped 4.2
passes and original-scope 4.26 qualification remain credited only within their
recorded scope. The stopped 4.27 attempt expired **2026-11-01T23:00:00Z**; no
old timestamp, caller assertion or offline byte check renews it. The candidate
checks target observation freshness (15 minutes) and finite attempt validity
(at most one hour); these design bounds do not authenticate a clock, observer
or operational approval.

## Admission, permissions and their limits

Staging PVC admission requires the explicit staging class, permits claims only
in the data namespace and rejects omitted/other classes, static selectors,
cloning/population references and user prebinding. Only the qualified binder
may set the initial `volumeName`; a previously bound claim retains its volume.
PV admission covers both old and new staging classes and staging claimRefs,
requires the staging-data claim UID and a retained local path below BasePath,
and permits only the qualified provisioning/binding controller identities.
Controller-added binding metadata can change while claim name/namespace/UID and
local volume identity stay fixed. Actual controller identities and implementation
compatibility remain mandatory; CEL source inspection is **not runtime CEL
execution or proof of legitimate binding**.

Every staging Pod must resolve to staging priority `-1000` and Never preemption.
Direct inline/external volumes are excluded through a volume-type whitelist;
PVC, configuration and bounded disk emptyDir volumes remain possible. Namespace
Pod Security is restricted. The package must be server type-checked, then tested
against omitted/default/other/system priorities, controller-created Pods,
ephemeral-container updates, restricted-Pod violations and storage bypass paths
before use. The installed Kubernetes quantity CEL library and admission
defaulting behavior are compatibility inputs, not assumed runtime results.

Protected objects use fixed names/resource identities, with old/new ownership
labels additionally protecting environment ConfigMaps from label removal or
spoofing. Quotas, LimitRanges, NetworkPolicies, environment ConfigMaps and grants
cannot be modified by application callers. Shared create grants for namespaces,
StorageClasses and PriorityClasses are constrained to staging names by admission.
Kubernetes deliberately excludes native admission policies/bindings themselves
from native policy matching; those two kinds use **RBAC and the trusted exact-byte
shared deployment workflow**. RBAC cannot scope `create` by resourceName, so
shared policy-creation authority is trusted, isolated and independently audited.
It is not a claim of native admission self-protection. There are no shared
workload/Secret permissions, environment shared/RBAC permissions, wildcard,
bind/escalate, impersonation or token-request grants. The separate observer
uses create-only SubjectAccessReview queries for exact deploy users and qualified
group context; that query grants no deployment authority. Impersonation cannot
be restricted to permission queries by RBAC, so no impersonation grant is emitted.
The observer has no Secret read or workload mutation grant. Effective inherited/group
permissions and controller/service-account paths require independent live audit.

## Probe proposal, evidence and cleanup

[candidate_probe.py](candidate_probe.py) reuses the existing attempt, security,
namespace, policy and UID helpers. It proposes one fresh uniquely named probe
namespace and fixed client/peer identities under a finite deadline (at most one
hour), rejects collisions and pins the image by digest. Pods have finite active
deadlines, staging `priorityClassName=hexalith-staging`, Never preemption and
bounded scratch owned through `fsGroup=65532`. Each Pod command checks the
absolute UTC cleanup epoch before starting and wraps its payload in a finite
`timeout`; delayed starts at/after the deadline exit. `activeDeadlineSeconds`
remains a relative fallback. Image command compatibility, volume ownership,
clock accuracy and actual deadline behavior require runtime qualification.
The supported image-reference subset is a lower-case repository path of
alphanumeric components with single `.`, `_` or `-` separators, optionally
preceded by a DNS/IPv4 registry and numeric port, optionally followed by a
1–128-character alphanumeric/underscore/dot/hyphen tag, and always followed by
`@sha256:<64 lower-case hex>`. Tags start with alphanumeric or underscore;
IPv6 registry literals and other repository syntax need a reviewed parser change.
Creation proposals require creation within five minutes;
evaluation accepts an existing attempt for its complete finite window.

Every proposal, evaluation and cleanup request requires explicit `target` and
`observer` arguments. `target` has the same closed public metadata shape and
freshness validation as `candidate.py`: context, HTTPS server, kube-system UID,
CA/public credential hashes, local identity-evidence path/hash, absolute kubectl
path/hash/version, attempt ID, observation and expiry. Its attempt ID must match
the probe. No historical target context or observer is defaulted. Collision and
cleanup identity-read argv use that exact tool/context/server; separate immediate
CA/UID/credential/tool checks remain mandatory. These argv contain no impersonation
flag and must run under the exact observer's own credentials. Inputs authenticate
neither the target nor the observer offline.

The initial documents include a per-attempt namespaced `cni-probe-observer` Role
and RoleBinding for that exact observer. Administrator installs these only after
separately authorized namespace creation; the role reads Pods, named Pod logs,
Services, Endpoints, EndpointSlices and NetworkPolicies, with no exec, mutation,
Secret access or impersonation. Namespace identity inspection uses the separate
foundation observer's cluster read grant. Policy/Pod creation and local-listener
health evidence need independently authorized authority; the read-only observer
cannot execute into a Pod or mutate policies. Complete cleanup inventory,
including the per-attempt grants, also needs an independently authorized reader.
No identity or credential is issued here.

The five ordered phases are:

1. Both peers connect before denial.
2. Ingress to the denied peer is blocked while client egress to both peers stays
   allowed and the other peer remains a positive control.
3. Both peers connect again after ingress restoration.
4. Egress to the denied peer is blocked while both peers' ingress stays allowed
   and the other peer remains a positive control.
5. Both peers connect after egress restoration.

Each phase includes a blocked command/exec proposal that preserves the original
client Pod UID, prints `PROBE_ATTEMPT` and `PROBE_PHASE` markers and measures both
peers with `nc -z -w 3`, a five-second outer bound per connection and a fifteen-second
overall measurement bound, also guarded by the absolute cleanup deadline. A
separately authorized active-measurement principal runs that measurement and
local-listener checks; it captures stdout, stderr and exit status. Exec stdout is
separate from Pod logs. A separately authorized writer applies the exact phase
policies and observes convergence. The read-only observer collects policy,
Pod/Service/endpoint identity and health immediately around each measurement;
the proposal includes those read argv. These are proposal data, never a dispatcher
or a grant of exec/mutation authority to the observer.

Each phase requires current independent readiness, local listening and endpoint
health for **both** unchanged peers. Evidence binds attempt ID, namespace UID,
client/peer creation-time Pod UIDs, marked client output, ordered phase/ordinal
and strictly increasing timestamps inside the current attempt. Each record's
`policies` lists exactly `client-egress`, `allowed-server-ingress` and
`denied-server-ingress` with namespace, stable observed UID and `specSha256`
matching that phase's `expectedPolicyFingerprints`. Hashes use SHA-256 of sorted,
indented JSON plus newline (`candidate.encoded`), after normalizing omitted empty
rules for declared policy directions. Missing/wrong-direction/replaced/duplicate
policies refuse consistency. Missing controls, dead/replaced peers,
wrong identity, duplicate/replayed phase, failed restoration or stale evidence
refuse consistency. A supplied consistent JSON log yields only
`offlineConsistent=true`: **`passed=false`, `evidenceAuthenticated=false`,
`liveAcceptance=false` and action/cleanup qualification remain false**. No
caller-supplied log or namespace observation authenticates runtime evidence.
The result envelope must also match the supplied public target binding and
observer exactly. Malformed nested metadata refuses with a structured failure.

Cleanup proposal requires fresh complete exact resource inventory, unchanged
Pod UIDs, namespace UID/attempt ownership and deadline, and records an atomic UID
precondition plus immediate recheck requirement. It emits **no delete action**.
Grant rules/subjects, authoritative Service routing fields and policy specs must
match the proposal definitions, allowing API metadata, Service allocation/defaults
and one complete actual phase state. The proposal saves the DNS policy spec hash
in Namespace annotation `hexalith.com/probe-dns-spec-sha256`; cleanup compares that
fingerprint through the existing namespace observation. This is supplied,
unverified provenance: the later live verifier must authenticate the saved proposal
and Namespace annotation independently before relying on it.
Normal namespace-controller `default` ServiceAccount and `kube-root-ca.crt`,
Service-attributed Endpoints/EndpointSlices and Events referring to exact known
object UIDs are accounted for separately with exact namespace, UID and creation
window checks. They need no attempt labels and are never claimed as attempt-owned.
EndpointSlices require the known Service owner UID; Endpoints use the exact
Service name and any target addresses must reference its exact peer Pod UID.
Foreign, credential-bearing or unattributed extra contents still refuse cleanup.
Creation time must precede or equal inventory observation, which must be at or
before now and no older than one minute. An expired finite attempt can still
have a later cleanup proposal using fresh target/ownership/inventory observations.
This scope never deletes namespaces, PVCs or PVs. Finite Pod deadlines bound
probe execution; resource cleanup still needs independent content/target
verification and separate exact authorization. Read/check and effect are not
atomic. An expired probe never qualifies isolation; a fresh owned inventory may
still be proposed for later authorized cleanup.

## Offline reproduction and recovery

Run from the repository root:

```sh
python3 -B eng/staging-foundation/candidate.py assess --inputs eng/staging-foundation/inputs.candidate.json
python3 -B eng/staging-foundation/candidate.py render --inputs eng/staging-foundation/inputs.candidate.json --output /tmp/staging-candidate-not-created
python3 -B -m unittest discover -s eng/staging-foundation -v
python3 -B -m unittest discover -s eng/staging-secrets -v
```

The current assessment and render exit **2**, list field-specific missing facts,
and render creates **no output directory**. Complete inputs can render to a new,
non-symlink directory. `candidate.py plan --inputs <completed-file>
--manifest-dir <rendered-directory>` refuses changed/missing/extra/symlink
manifests and binds exact input bytes, all five manifest bytes, ordered owner
identities, source receipts/pins, implementation hashes and server/UID/CA/public
credential/tool/attempt metadata into a deterministic hashed **blocked** plan.
Evidence paths must be real repository files with matching SHA-256, without
traversal or symlinks. That validates local provenance, never signatures or
acceptance. Synthetic complete facts exist only in tests and temporary paths.

`candidate_probe.py proposal|evaluate|cleanup-proposal --request <JSON>` accepts
the following named arguments. All fields are required; `now` is an internal
test seam and is omitted from operational request files.

| Command | Request fields |
| --- | --- |
| `proposal` | `image`, `dns_peers`, `attempt_id`, `created_at`, `cleanup_deadline`, `collision_observation`, `target`, `observer` |
| `evaluate` | `records`, `attempt_id`, `created_at`, `cleanup_deadline`, `namespace_uid`, `pod_uids`, `observed_namespace`, `target`, `observer` |
| `cleanup-proposal` | `attempt_id`, `created_at`, `cleanup_deadline`, `namespace_uid`, `pod_uids`, `observed_namespace`, `inventory`, `observed_at`, `target`, `observer` |

`collision_observation` is exactly `{"checkedNamespace": <derived namespace>,
"exists": false}`. `observed_namespace` is a resource object with object-valued
`metadata`, ownership `labels` and attempt-timing `annotations`. `pod_uids` names
exactly `client`, `allowed-server` and `denied-server`. `records` uses the proposal's
`resultEnvelope`, replacing its descriptions with actual observations and the
five ordered records; it includes exact `targetBinding` and `observer`, each
phase's `policies` identity/hash list and marked `clientLog` matching its results.
`inventory` contains every declared Pod, Service, NetworkPolicy and per-attempt
Role/RoleBinding, plus observed ordinary controller objects described above, each
with its exact namespace/UID and controller creation timestamp. Other content
refuses the proposal and requires a separately reviewed ownership procedure.
It prints reviewable JSON and runs nothing. Evaluation returns exit 0 for offline
consistency and exit 2 for inconsistency; malformed request files return structured
JSON refusals with every action/live/pass flag false. The tests give synthetic consistency
examples. No current image, target, observer or real UID is supplied.

Tests exercise missing facts/no writes, deterministic source/byte binding,
storage/controller and priority definition guards, layer/role boundaries,
both directions of every service/additional flow, exact role-to-principal mappings,
service/DNS peers, quantities/capacity, dependency/authorization refusal,
stale/probe failure cases, source pins and absence of process/network calls.
Static CEL inspection and Python tests do not prove live admission, effective
RBAC, encryption, filesystem quotas, DNS, CNI or native TLS. Final acceptance
requires the separate authorized runtime path described above.

Recovery is forward convergence through reviewed server dry-runs and exact
re-application/corrections after immediate pre/post target checks. Stop on drift
or partial failure. Immutable StorageClass/PriorityClass changes require new
reviewed class names and definitions. Never delete namespaces/PVCs/PVs, rewind
credentials/data or use Helm rollback. Offline completion never advances the
broader sprint story to review/done.
