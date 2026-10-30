# Story 4.14 offline unblocking handoff — 2026-10-29

**BLOCKED_NOT_FOR_EXECUTION. `approvalGranted=false`, `liveAcceptance=false`.**
Story 4.14 remains in-progress. This additive reassessment establishes what is
needed for a reviewable proposal; it grants no execution authority. No new
collection, cluster/network request, API dry-run, Helm action, installation,
credential issuance, key generation, unseal, secret write or recovery occurred.

The source-bound [assessment](unblocking.assessed.json) enumerates all **37**
required fields, their exact values, source hashes/pointers or individual owner
questions. Only `network.dns.namespace` and `network.dns.podLabels` are evidenced.
They describe the retained CoreDNS observation, not a qualified pod resolver
datapath. The other **35 fields remain null**. No owner answer has been received;
examples, conventional ports, existing classes, current operator identity and
synthetic tests supply no approved deployment choice.

## Four pending owner decision groups

Record exact values with evidence paths or explicit owner decisions. The numbered
questions below refer to the unchanged [individual questions](MISSING-INPUTS.md);
the JSON inventory also records each question's exact text and line. Do not send
secret material or kubeconfig contents.

| Group | Questions | Decision requested |
|---|---|---|
| Storage, custody and priority | 1–11 | Select provisioner, exact parameters, reclaim policy, new staging class, production class to deny, encryption evidence and attestor, named Administrator/deputy key holders, staging/production priority values and production class name. Supply encryption and independent-custody evidence. |
| Identities and network | 12–15, plus prerequisites below | Select distinct shared/environment identities and every job principal; supply their complete RBAC definitions. Supply the full module-agnostic data-service/native-TLS inventory, real DNS path, service selectors and required flows. Select a digest-pinned busybox-compatible (`httpd`, `nc`) probe image for a later bounded authorization. |
| Resource budgets | 16–35 | Approve container requests/limits, PVC bounds and all application/data namespace quotas, including units, ordering and capacity rationale. Current capacity observations do not select budgets. |
| Provider bootstrap and acceptance | Additional fourth question | Approve the limited foundation milestone and authorization boundary, if any, environment-owned provider bootstrap with workload deployment authority withheld, and the complete actual-provider TLS/denial verification and final 4.14 acceptance sequence. No boundary or proof waiver is selected here. |

All four groups remain unanswered; the fourth question was requested separately
and has no answer. Additional enforcement choices outside the 37-field schema
remain explicit requests, not assumed answers.

## Definitions needed before the final render and live proposal

Resolve the null fields and review forward definition corrections offline before
rendering final approval bytes. The unchanged renderer's ability to accept a
complete fixture does not close these gaps:

- **Storage:** `_quota` denies only the named production StorageClass. Choose an
  enforcement mechanism covering every other class, omitted/default selection,
  static/prebound volumes and any bypass path, so staging claims bind only
  through staging's class. Bind encryption evidence to the actual backing devices
  and independent Administrator/deputy custody.
- **Workload priority:** `_priority_denial` excludes only the named production
  class. Choose enforcement that keeps every permitted staging workload below
  production, including other high/system classes and omitted/default priority
  paths. Approving two integer values alone does not prove this requirement
  ([Story 4.14](../../_bmad-output/planning-artifacts/epics.md), lines 2288–2289).
- **RBAC:** no Role, ClusterRole or binding is emitted. Supply complete reviewed
  positive layer permissions and negative cross-layer/job checks. Include
  arbitrary data-namespace workloads/PVCs and environment-owned ConfigMaps;
  all mutation verbs, data Secret get/list/watch, bind/escalate/impersonate,
  credential/ServiceAccount and group-membership escalation paths. Define the
  verification observer and its tightly scoped impersonation authority separately
  from deploy principals. The generated denial list does not establish this contract.
- **DNS:** qualify actual pod resolver addresses, NodeLocal DNS/host-network
  behavior and both TCP/UDP paths. The CoreDNS Service selector does not establish
  those paths. Correct both foundation and probe policy definitions if required.
- **Service networking and TLS:** the current policies allow namespace-wide
  aggregated TCP ports, without per-service pod selectors or replication topology.
  Supply reviewed source/destination identities, selectors and every native-TLS,
  replication/HA, monitoring, backup and custody flow, or explicit justified
  non-applicability. A port number or TLS contract ConfigMap proves no handshake
  or plaintext denial. Certificates and keys remain downstream 4.15/4.16 work;
  their preparation never becomes a reverse foundation gate.
- **Provider bootstrap and acceptance:** the limited foundation milestone,
  environment-owned provider bootstrap and final actual-provider TLS/acceptance
  sequence need the fourth owner decision. Workload deployment authority must
  remain withheld during any later authorized bootstrap. The existing shared
  then environment stage order is known; the complete bootstrap/acceptance
  sequence remains blocked. This handoff selects no acceptance boundary, waives
  no actual-provider proof and does not require downstream story acceptance as
  a reverse foundation gate.
- **Probe ownership and cleanup:** the historical generator uses a fixed
  namespace and reusable purpose labels. Before future authorization, require
  collision refusal, fresh namespace/attempt ownership and UID verification,
  and an owner-selected finite cleanup deadline. Never adopt or delete a
  namespace based solely on its purpose labels. The generator remains unchanged
  and unexecuted.
- **Quantities:** current validation primarily checks non-placeholder strings;
  it does not fully validate Kubernetes quantity syntax, units, signs, CPU/memory
  request-to-limit ordering, PVC min/max ordering or quota consistency. Complete
  and review these validations before any live proposal. `storage.parameters`
  currently refuses an empty map. If the chosen provisioner legitimately needs
  `{}`, record that fact and review a validator correction; never invent a key.
- **Exact target and permissions:** generated argv selects context only. The
  offline context/UID arguments do not authenticate a future cluster. Harden and
  review tool, credential/CA and argv binding to the exact API server, context
  and UID, fail on configuration drift, and collect immediate pre/post-stage
  identity checks. The existing context name alone cannot provide that guarantee.

These gaps are visible in the unchanged [renderer](render.py),
[blocked plan](apply-plan.json), [probe generator](cni_probe.py) and
[input evidence](INPUT-EVIDENCE.md). The JSON assessment separates requirements
before rendering, before live authorization and under separately authorized
live verification. No correction is fabricated in this handoff.

## Dependency reassessment

Every source cited by the unchanged [gate assessment](gates.assessed.json) was
rechecked against the repository bytes and pointers. All five acceptance flags
remain **false**:

| Dependency | Status | Credited evidence and remaining prerequisite |
|---|---|---|
| 4.1 | in-progress | Upgrade hops remain closed/unsigned and operational outcomes not-run; fresh accepted recovery, compatibility/workload qualification and accepted sequential upgrades remain required. |
| 4.2 | in-progress | Exposure closure, registry authentication and later GC confirmation pass within their recorded scopes. Full workflow review/completion remains pending. |
| 4.3 | in-progress | Local relocation preparation supplies no accepted destination qualification, cutover, credential revocation, source removal or secure erasure. |
| 4.27 | in-progress | The offline native-certificate prerequisite is done (336 tests). Operational gate stays closed, operational qualification/recovery acceptance stay null and public activation stays disabled. Accepted retirement remains pending. |
| 4.28 | backlog | Accepted private Rancher installation/registration, TLS/MFA, authority restoration and independent native maintenance/recovery qualification remain absent. |

The upstream [4.28 owner decisions](../../_bmad-output/implementation-artifacts/4-28-deploy-rancher-and-register-the-existing-cluster.md), lines 40–42, also remain pending: fresh unique native CN without an O group, its sole unowned binding and revocation proof; separately approved treatment of five retained `jpiquot` objects and dangling grant (Administrator review 2026-11-03); and disabled local Rancher admin versus sealed MFA-less break-glass with independent custody. The sealed option requires an explicit amendment to the Keycloak-MFA decision. These are upstream blockers only; this handoff selects no option or operation.

Story 4.26 remains qualified for its original scope only; management installation,
production retirement and production upgrade acceptance remain false. Historical
independent native access and retained direct authentication are credited only
within their demonstrated scope. They do not select deploy identities or prove
the remaining access/recovery requirements.

## Why Story 4.15 supplies no new foundation choices

The imported [offline preparation](../staging-secrets/APPROVAL-PACKAGE.md) and
later synthetic-test correction are preserved. The supplied commit is
`a55cafcf22520ca4d8a8699e11b2290f05d5e4c8`, its imported preparation commit is
`0e6306b090e654473c2397bb49807eb377447519`, and the preserved test correction is
`9ab03523530d6cb63c878c8ddb776a0279dddb74`. The import contains an intervening
synthetic-test edit, followed by that correction; tree or byte equivalence is
not claimed. Current non-test preparation bytes match the supplied commit.
The parent records exact Git comparisons and local integration separately. Its
[source index](../staging-secrets/source-evidence.json) pins six unchanged
foundation files: `APPROVAL-PACKAGE.md`, `inputs.resolved.json`,
`MISSING-INPUTS.md`, `DEPENDENCY-DIRECTION.md`, `render.py` and `apply-plan.json`.
All their hashes still match. Every existing foundation file except this
directory's README, every staging-secrets file and the historical foundation and
secrets receipts retain their bytes.

Story 4.15 inherits unresolved foundation choices. Its own null inputs, symbolic
definitions and synthetic fixtures do not approve storage, budgets, principals
or ports. Any limited foundation milestone, environment-owned provider bootstrap
and final actual-provider TLS/acceptance sequence remains blocked on the fourth
owner decision; no revised acceptance boundary is inferred from the preparation.
The [dependency direction](DEPENDENCY-DIRECTION.md) remains one-way; consumer
facts and acceptance never enter foundation definitions or gates. Integration
preservation of unrelated repositories and statuses is recorded by the parent
outside foundation evidence.

## Ordered requirements for a separately authorized live apply

1. Settle the four decision groups and all additional definition gaps; bind
   every resolved value to evidence or an explicit owner record. Reassess all
   five dependency gates from authoritative completion evidence. Neither owner
   answers nor offline test passes alone accept those dependencies.
2. Review and qualify forward definition/validation/target changes offline.
   Render actual approved-input manifests and the ordered blocked proposal only
   once inputs permit it. Hash exact bytes, principals, target, tool identities,
   effects, full verification matrix and forward recovery. Never execute generated
   argv as part of preparation.
3. Obtain a separate Administrator record approving those exact bounded effects,
   server dry-runs, applies, probe creation/cleanup and verification/recovery.
   No such operational approval exists here.
4. Under that authority, revalidate API CA/SAN, context/server/UID and identity
   immediately before any operation. Demonstrate CNI allow and deny behavior
   with the approved image, namespace collision refusal, fresh namespace/attempt
   ownership and UID verification, and the owner-selected finite cleanup deadline.
   No adoption or deletion based solely on reusable purpose labels is permitted.
5. Check target; run the shared-infrastructure server dry-run then its apply;
   verify target/effects. Only after namespaces exist, run environment-layer
   server dry-run then its apply; verify target/effects again. Stop on drift or
   partial failure and withhold workload deployment authority until complete
   isolation succeeds. The environment dry-run cannot precede namespace creation.
   This known stage order does not settle the complete provider bootstrap and
   acceptance sequence; follow only the later explicit owner decision for any
   limited milestone and separately authorized environment-owned bootstrap.
6. Collect actual positive/negative RBAC, restricted pod, quota/limit, priority
   path, staging-only storage/binding, encryption/custody, DNS and per-service
   native-TLS/denial evidence. A successful API dry-run is no runtime proof.
   Withhold workload deployment authority during provider bootstrap and until
   final actual-provider proof and required acceptance are complete. Downstream
   4.15/4.16 story acceptance never becomes a reverse foundation gate.
7. Assess 4.14 against actual observations and record separate operational
   acceptance. For partial failure, use reviewed forward convergence/corrections;
   immutable storage/priority changes need reviewed new class definitions and
   names (the current priority name is hardcoded). Never use Helm rollback,
   namespace/PVC/PV deletion, data/credential rewind or an unapproved restore.

## Reproducible offline verification

Run from the repository root:

```sh
python3 -B _bmad-output/implementation-artifacts/evidence/epic-4/4-14/offline-unblocking-2026-10-29/verify.py
```

The stdlib verifier checks consistency with this retained, reviewed current cut.
It binds the fixed preservation-manifest digest, all 69 protected file hashes and
source import boundaries before importing project modules. It validates source
hashes, JSON pointers and line citations, exact field/null/question inventories,
false approval/status boundaries, dependency credits and Story 4.15 source pins.
A fixed canonical fingerprint binds the reviewed structured owner questions,
prerequisite wording, upstream decisions and preparation history, including the
pending fourth bootstrap decision. These are structural/provenance checks; they
do not generically determine the meaning or sufficiency of newly edited claims.
Changed source bytes, stale hashes, invalid pointers/lines, missing artifacts,
false saved outcomes, omitted decisions, waivers and fabricated history are
refused. Receipt regeneration validates every output destination before writing
and refuses symlinks. An audit guard precedes project code; no native collector
is loaded. The verifier scans all new foundation artifacts and evidence for
module coupling and runs both existing suites. Render and plan use the actual
input file through their real entrypoint in process: each must exit 2 with empty
stdout; render must create no output directory. Complete fixtures remain confined
to existing synthetic tests and temporary directories.

The [receipts](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-14/offline-unblocking-2026-10-29/verification.json)
retain per-check results, logs and hashes. Passing means this blocked offline
handoff is internally consistent. It does not establish live behavior or approve
execution. Historical observations and receipts remain verbatim beside this
new evidence directory.
