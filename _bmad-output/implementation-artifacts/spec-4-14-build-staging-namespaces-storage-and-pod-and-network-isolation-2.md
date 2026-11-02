---
title: 'Story 4.14: Apply the approved staging isolation recommendations offline'
type: 'feature'
created: '2026-11-02'
status: 'done'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: '52e361d4a41b681b06f5e885f9f9857d8b08f5b2'
story_key: '4-14-build-staging-namespaces-storage-and-pod-and-network-isolati'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Offline definitions lack complete storage, priority, identity and network enforcement. Actual infrastructure facts and five unaccepted prerequisites block live acceptance.

**Approach:** Implement the recommendations approved by the user's 2026-11-02 instruction, "apply recommended", as an additive offline candidate package. Keep missing facts explicit and all live effects blocked. This approval resolves the four planning questions and authorizes implementation without another approval checkpoint.

**Approved decisions:** Reuse `openebs.io/local` with a separate encrypted staging filesystem, annotation-configured BasePath, `WaitForFirstConsumer`, `Retain` and supported filesystem project quotas. Propose class `hexalith-staging`; retain actual production class, backing mount/device, compatibility, encryption and independent Administrator/deputy custody as required facts. Use native Kubernetes admission policies for staging-only PVCs, safe binding paths, required staging priority and protected environment objects; staging priority -1000, planned production priority 1000, staging preemption Never. Define separate shared/environment/application deploy roles plus a separate observer, without credential issuance. Reuse Calico with specific service flows and actual DNS qualification; native TLS is provider-enforced. Probe both peers positively before independent ingress/egress denial phases and restore positive controls afterward.

**Approved budgets:** Application requests 4 CPU/8Gi, limits 8 CPU/16Gi, 30 pods, zero PVC/storage. Data requests 4 CPU/8Gi, limits 8 CPU/16Gi, 20 pods, 12 PVCs/100Gi. Container defaults: requests 100m/128Mi, limits 500m/512Mi. PVC bounds 1Gi–50Gi and bounded temporary storage are explicit candidate design defaults; actual composition/capacity qualification remains required. Use staged foundation verification, bounded provider bootstrap and final provider TLS/isolation acceptance; withhold application deployment authority throughout. These are selected candidate values, not observed capacity or live acceptance.

## Boundaries & Constraints

**Always:** Preserve AR-32 layer identities, restricted pods, default-deny networking, staging-only storage and priority below production. Preserve receipts and six Story 4.15 source pins; add candidate files. Keep dependencies one-way and approval/live acceptance false until independently satisfied.

**Never:** Adopt fixture values as facts, claim runtime proof from unit tests, execute generated commands, call network/cluster tools, issue credentials, delete namespaces/PVCs/PVs, rewind credentials/data or use Helm rollback. Namespace creation requires accepted 4.1, 4.2, 4.3, 4.27 and 4.28 evidence and exact operational approval. Offline completion must never synchronize the broader sprint story to review/done.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Error handling |
|---|---|---|---|
| Current candidate | Approved defaults plus unresolved facts | No deployment manifests | Field-specific refusal, no output directory |
| Complete synthetic candidate | Valid inputs, source receipts and binding paths | Deterministic layered manifests and source/byte-bound blocked plan | Reject unsafe relationships, modified files or missing facts |
| Enforcement bypass | Other/omitted class, user prebinding, priority or protected-object abuse | Generated policies reject the path; legitimate controller binding remains possible | Offline checks do not claim live CEL/RBAC qualification |
| Probe evidence | Wrong attempt/UID, missing controls, unhealthy peer, replayed or duplicate phase | Refuse qualification | Every offline result retains liveAcceptance=false |
| Missing prerequisites | Any dependency unaccepted | No namespace creation | Preserve blocker and scope of partial passes |
| Stale operational evidence | Expired attempt or historical observation | No current acceptance claim | Require a fresh authorized evidence path |

</frozen-after-approval>

## Code Map

All implementation paths below are under `eng/staging-foundation/`.

- `preflight.py`, `cni_probe.py` — reuse quantities and attempt ownership; live target, peer-health control and log provenance remain unqualified.
- `UNBLOCKING-REPORT.md`, `MISSING-INPUTS.md` — exact decisions and enforcement gaps; `OFFLINE-HARDENING-HANDOFF.md` records completed scope.
- `render.py:199,212,293` — pinned quota/plan limitations; preserve bytes. `eng/staging-secrets/source-evidence.json` identifies six pins.
- `_bmad-output/implementation-artifacts/4-27-retire-kubesphere-without-changing-workload-data.md:1594` — stopped attempt expired 2026-11-01T23:00:00Z; no accepted retirement/upgrade.

## Tasks & Acceptance

**Execution:**
- [x] `inputs.candidate.json` — record selected values, source/decision provenance and all unresolved operational facts; no synthetic identity, evidence, service or image.
- [x] `candidate.py` — stdlib-only assess/render/plan entrypoints. Reuse preflight quantity checks; preserve renderer bytes. Render AR-32 layers including OpenEBS annotations, narrowly scoped native admission, explicit RBAC, per-service policies and quotas including temporary storage. Bind plans to exact input/manifest bytes, ordered identities and server/UID/CA/tool metadata; never add an executable dispatcher. Block incomplete input before writing; distinguish offline completeness from dependency/live acceptance. Validate evidence paths/hashes without treating their contents as authenticated runtime proof.
- [x] `candidate_probe.py` — additive proposal/evaluator using existing probe helpers, fresh attempt/namespace ownership and finite cleanup. Test both peers before and after independent deny phases, with strict attempt/Pod UID/phase identity; authenticate no caller-supplied log and qualify no action/cleanup offline.
- [x] `test_candidate.py`, `test_candidate_probe.py` — meaningful positive and refusal cases for every matrix row; verify bytes, layer/role boundaries, controller-compatible binding, network peers, quantity/capacity declarations, no process/network calls and historical source pins. Static CEL inspection is not runtime CEL proof; document that limit.
- [x] `CONTINUATION-PACKAGE.md`, `README.md` — explain selected values, remaining facts, exact stage ownership, provider bootstrap/TLS proof, verification, forward recovery and separately required operational authorization. Link the additive entrypoint without editing historical pins.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` — retain in-progress until every original live criterion has accepted evidence.

**Acceptance Criteria:**
- Given unresolved facts, when preparation runs, then qualification refuses with exact missing inputs and no manifest output.
- Given selected definitions, when validated, then storage, priority, permission and network bypasses have meaningful coverage.
- Given only offline or scoped upstream passes, when assessed, then approval/live acceptance remain false and 4.14 stays in-progress; probe and dependency failures never qualify deployment.


Review-driven implementation corrections (all paths under `eng/staging-foundation/`):

- `candidate_probe.py`: derive collision/cleanup identity-read proposals from a supplied qualified public target binding and exact observer principal, never the historical fixed context. Render narrowly scoped read-only per-attempt observer Role/RoleBinding (Pods/logs, Services/endpoints, EndpointSlices and policies; no exec, impersonation or mutation). These target/observer inputs are required facts, remain unverified offline, and are never defaulted. Validate their public shape/freshness consistently with the candidate path. Keep all action/acceptance flags false. Set every probe Pod's staging PriorityClass and Never preemption explicitly. Creation freshness applies at proposal time; result evaluation uses the finite attempt window. Validate nested metadata types; cleanup requires created <= inventory observation <= now and permits later cleanup of an expired attempt.
- `candidate.py`: reject deploy/observer/Administrator ServiceAccounts located in either workload namespace; close data-service input fields and project only public TLS-contract fields; reject additional APP-to-DATA flows that bypass the authoritative native-TLS service inventory. Validate Kubernetes label key/value syntax and string namespace names. For literal-only DNS peers, require exact coverage of declared resolver addresses; selector/NAT paths remain explicitly unqualified until live evidence. Correct Dapr ownership against architecture lines 143/260–261: application package Configurations, WorkflowAccessPolicies, Subscriptions and Resiliencies belong to its approved inventory; Components/HTTPEndpoints/MCPServers remain environment-owned.
- `test_candidate.py`, `test_candidate_probe.py`: exercise each refusal above; assert exact RoleBinding/ClusterRoleBinding role-to-principal mappings, both directions of each service and additional flow, per-attempt read-only observer grants, probe target binding, priority, valid attempts older than five minutes, malformed metadata and cleanup time ordering. Keep all historical/current tests and source pins.
- `CONTINUATION-PACKAGE.md`, `README.md`: document the corrected probe request shape, observation authority, actual unknown facts and static-versus-runtime verification limits. No new policy service, credential issuer or executor.

Prior generation reference: `/tmp/story414-reviewed-generation-qqd5k625/generation.diff` and the adjacent source files. These are preservation references, not authority; re-derive from this amended spec and remove every known-bad state below.

## Implementation Notes

Planning facts: four human-only decision groups; later cluster/configuration and resource lifecycle effects; proposed footprint of four additive files plus justified status tracking. Design depends on answers. Initially clean on main; no cluster request or collector run.

Investigation: 44 foundation and 48 staging-secrets tests pass, including source pins. No newer evidence resolves nulls or accepts dependencies. Implementation has not started. A concurrent Folders submodule change appeared during investigation and was left untouched.

2026-11-02 approval: the user selected all recommendations. The bounded offline implementation is authorized; missing real facts are required outputs/blockers, not unanswered design choices. Main advanced concurrently to `38de4c15aa5990d169cd20be34e37ef6cc823491`; the original canonical baseline is preserved. Builds, ChatBot and Folders gitlinks/worktrees are unrelated and must be left untouched. Additional candidate defaults and implementation details are recorded explicitly, never promoted to operational facts. Implement only this package; keep the sprint story in-progress even when this scoped spec is done.

2026-11-02 implementation: added candidate inputs, stdlib assessment/render/byte-bound blocked plan, independent-direction CNI proposal/evaluator, 37 tests and current continuation documentation. Five manifest files separate cluster grants, admission, shared namespaces/classes, namespaced grants and environment policies. Application roles remain unbound; observer uses SubjectAccessReview without impersonation. All six historical source pins match. Parent read the implementation diff and independently verified 81 foundation tests and 48 staging-secrets tests; no skips/failures. Matrix coverage: current refusal, deterministic/tampered manifests, static storage/priority/protected-object definitions, probe identity/controls/replay failures, dependency claims remaining unaccepted, and stale target/attempt refusals all ran. Static CEL assertions remain explicitly limited and do not establish API type-check or runtime proof. The scoped package is implemented; sprint 4.14 remains in-progress.

2026-11-02 review re-derivation: all 18 logged findings are corrected in the new generation. Parent read the archived implementation plus the complete correction diff, regenerated the full candidate diff, and independently ran all 95 foundation and 48 staging-secrets tests with no failures/skips. Matrix coverage includes the 14 added regression tests for target/observer binding, read-only probe grants and priority, selectors/DNS, service-field and plaintext-flow refusals, exact role bindings, ingress/additional flows, Dapr ownership and probe timing/metadata. Current inputs still refuse with 53 problems, no manifest output; source pins and sprint status remain unchanged.

2026-11-02 final patch verification: all 17 second-review findings are patched with no new top-level function arguments or CLI operations. The existing phase record now includes marked measurements and policy UID/spec fingerprints; Namespace observations carry a saved DNS fingerprint whose provenance remains unverified. Normal controller resources are attributed separately, never treated as attempt-owned. Parent inspected code/test/documentation deltas and independently passed 107 foundation tests plus 48 staging-secrets tests (155 total; zero failures/errors/skips), then checked scoped whitespace. All six historical pins remain exact. No generated command or cluster operation ran. Full test transcript is retained locally at `/tmp/story414-final-foundation-tests.log`. Live CEL/RBAC, image/deadline/fsGroup behavior, DNS/CNI/TLS, encryption/custody and actual dependency acceptance remain separately required.

Completion: this scoped offline spec is done. Sprint Story 4.14 remains `in-progress` under the approved frozen boundary; the workflow's ordinary review synchronization is intentionally not applied to the broader live story. No review work was deferred.

## Spec Change Log


2026-11-02 review loop 1: BH-3/BH-5/EH-3 exposed missing probe target/observer inputs in the non-frozen implementation handoff. Code was archived and reverted before re-derivation; the handoff now requires qualified public target metadata and exact per-attempt read-only observer grants. Known-bad states to eliminate also include workload-namespace privileged ServiceAccounts, credential-bearing service objects, undeclared plaintext APP-to-DATA flows, missing probe priority, inconsistent DNS/invalid selectors, malformed metadata crashes, incorrect freshness/cleanup time checks, Dapr ownership drift and the three verified coverage gaps. KEEP: stdlib-only implementation; preserved six pins; approved budgets and unresolved real facts; five ordered layer files; unbound application role; SAR-only observer without impersonation; source/input/manifest hash binding; independent-direction probe phases with restored positive controls; every offline action/live/acceptance flag false. Frozen approved intent is unchanged.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| BH-1 | high | patch | Accepted data-namespace shared ServiceAccount can be mounted by environment-created Pods, crossing the layer boundary; reproduced with complete synthetic inputs. |
| BH-2 | high | patch | An extra password field passes assessment and reaches the rendered TLS ConfigMap; reproduced with a sentinel, requiring closed fields and explicit projection. |
| BH-3 | medium | bad_spec | Probe collision/cleanup argv retain jpiquot@local although candidate targets are supplied separately; the handoff lacked required probe target binding. |
| BH-4 | medium | patch | All generated probe Pods omit priorityClassName and can inherit a cluster default outside staging admission; inspected all three Pods. |
| BH-5 | medium | bad_spec | Foundation observer grants cover only two namespaces; dynamic probe namespace has no observer Role/Binding or endpoint/log access. Handoff omitted these required facts/grants. |
| BH-6 | medium | patch | Evaluation rejects a six-minute-old attempt despite a valid twenty-minute deadline; reproduced. Creation freshness must not shorten the existing attempt window. |
| BH-7 | medium | patch | Literal resolver address and literal peer can differ while offlineComplete remains true; reproduced with two distinct IPs. |
| BH-8 | medium | patch | Selector strings with spaces pass and render invalid Kubernetes selectors; reproduced. Namespace identity values also need strict string syntax. |
| BH-9 | medium | patch | Observed metadata=[] causes AttributeError rather than a structured refusal; reproduced. Inventory metadata has the same unchecked access. |
| BH-10 | medium | patch | Cleanup returns a proposal for a creation timestamp five minutes after its inventory observation; reproduced. |
| EH-1 | medium | patch | Additional APP-to-DATA port 12345, absent from the TLS service inventory, passes assessment and renders allowances; reproduced. |
| EH-2 | high | patch | Same independently reported credential-field copy as BH-2; whole service object reaches ConfigMap. |
| EH-3 | medium | bad_spec | Same independently reported historical probe context as BH-3; new target/observer handoff is required. |
| EH-4 | medium | patch | Same independently reported metadata type crash as BH-9; validate before dictionary access. |
| VG-1 | medium | patch | Pre-verified mutation points observer binding at shared role and all 81 tests pass; exact role/subject mapping assertions are missing. |
| VG-2 | medium | patch | Pre-verified removal of every service ingress policy leaves all 81 tests green; assert each destination namespace, peer, selector and TLS port. |
| VG-3 | medium | patch | Pre-verified omission of additionalFlows from rendering leaves all 81 tests green; assert both policies for the declared replication flow. |
| Parent-1 | medium | patch | Application inventory rejects all Dapr kinds while APP environment role owns configurations/resiliencies/subscriptions, contradicting architecture release-tier ownership at lines 143/260–261; correct the fixed kind assignments. |



Second review, 2026-11-02. Each item is a direct validation, template, existing phase-record consistency or test correction; no new CLI operation or required top-level function argument is needed. The prior generation remains preserved; all patches retain the frozen intent and blocked live boundary.

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| BH2-1 | medium | patch | A complete inventory with Kubernetes-created default ServiceAccount is rejected; normal root CA, endpoint and event objects also need explicit controller/resource attribution without treating them as attempt-owned. |
| BH2-2 | medium | patch | Wildcard observer Role rules pass cleanup inventory validation unchanged; reproduced. Existing reviewed Role/Binding, Service and policy fields must be compared, preserving legitimate API defaults. |
| BH2-3 | medium | patch | An observer ServiceAccount in the dynamic probe namespace passes proposal validation; probe creation authority could assume it. Reject that same namespace. |
| BH2-4 | medium | patch | Malformed IPv6 and DNS origins [:::], a..b and - pass assessment; reproduced. Validate actual host/address syntax. |
| BH2-5 | medium | patch | Service name a..b passes and becomes invalid NetworkPolicy names; reproduced. Reuse strict DNS-subdomain validation across object-name fields. |
| BH2-6 | medium | patch | Image !@sha256:<64 hex> passes candidate and probe checks; reproduced. Validate a supported repository-reference syntax as well as digest. |
| BH2-7 | medium | patch | Probe uses inherited root-owned emptyDir with UID/GID 65532 and no fsGroup; source confirms missing volume group ownership despite promised writable scratch. |
| BH2-8 | medium | patch | activeDeadlineSeconds is relative to Pod start, so proposal-time subtraction cannot enforce absolute cleanup time after delayed scheduling; add a small absolute-deadline payload guard and distinguish both bounds. |
| BH2-9 | medium | patch | Client only sleeps; proposal contains phase expectations but no bounded connection payload/collection procedure. Add blocked per-phase command data and explicit collection authority, without a dispatcher or running it. |
| BH2-10 | medium | patch | Phase records lack observed policy identities/spec hashes, so incorrect direction can retain matching labels/outcomes. Bind consistency to the already proposed phase policy identities/specifications; all observations remain unverified. |
| EH2-1 | high | patch | system:anonymous passes observer/deploy principal validation; reproduced. Bindings must never grant anonymous authority. |
| EH2-2 | medium | patch | Independent duplicate of BH2-5, verified by the invalid generated allow-a..b policy names. |
| EH2-3 | medium | patch | Numeric CIDR 1234 with resolver 0.0.4.210 passes assessment and renders a non-string CIDR; reproduced. Require canonical string host CIDRs. |
| EH2-4 | medium | patch | Pod CEL accepts emptyDir sizeLimit=0 because only an upper bound is checked; Kubernetes eviction code requires size.Sign()==1. Require a strictly positive limit too. |
| VG2-1 | medium | patch | Pre-verified mutation assigns environment stage to shared principal and all 95 tests pass. Assert explicit expected stage/identity/manifest mappings independent of generated fields. |
| VG2-2 | medium | patch | Pre-verified CLI mutation returns success for inconsistent evaluation and all 95 tests pass. Test request-file CLI success/refusal status and emitted false live flags. |
| Parent-2 | medium | patch | Mixed selector/IP DNS paths accept unspecified/multicast resolver addresses; require real non-unspecified/non-multicast IP strings even where translation remains unqualified. |

## Verification

- `python3 -B -m unittest discover -s eng/staging-foundation -v` — 44 passed on the recorded baseline.
- `python3 -B -m unittest discover -s eng/staging-secrets -v` — 48 passed on the recorded baseline.
- Re-run both suites and new candidate tests after changes; runtime evidence remains separate.

Final verification: `python3 -B -m unittest discover -s eng/staging-foundation -v` — 107 passed; `python3 -B -m unittest discover -s eng/staging-secrets -q` — 48 passed. Current `candidate.py render` refuses with exit 2, empty stdout and no manifest directory; the candidate reports 53 unresolved facts. This completes only the approved offline package.
