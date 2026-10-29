# Story 4.14 live-apply approval package — 2026-10-29

**BLOCKED_NOT_FOR_EXECUTION. `approvalGranted=false`.** The evidence assessment
and missing-input questions are ready for owner review. Neither layer has
complete inputs or approved manifest bytes. No cluster apply, API-server
dry-run, Helm action, CNI probe or other persistent cluster write was performed.
The read-only collector completed; offline tests passed. Story 4.14 remains
in-progress, and this package does not advance any dependency status.

## Target and observed cut

| Property | Exact target |
|---|---|
| Context | `jpiquot@local` |
| API server | `https://192.168.1.30:6443` |
| Cluster UID (`kube-system` namespace UID) | `b5860674-1d1e-4a3b-b413-e1bb3b9a4557` |
| Application namespace | `hexalith-staging` |
| Data namespace | `hexalith-staging-data` |
| Fixed staging PriorityClass name | `hexalith-staging` |

The target comes from `apply-plan.json`. The
[new native observation](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-14/live-apply-approval-2026-10-29/native-observation.json)
revalidated this context, server, cluster UID and certificate-authenticated
principal `jpiquot`, completing at `2026-10-29T17:30:48.110013+00:00` with
CA/IP-SAN-verified TLS. Both target namespaces are absent from its successful
namespace listing. Gateway API and HTTPRoute queries report unavailable resource
types. These are current observations, not acceptance of recovery, historical
revocation, custody or deployment identities. Re-read target identity before and
after every future stage; this dated observation cannot authorize a later apply.

## Dependency acceptance

The source paths, hashes, exact sprint keys and acceptance scope are in
[gates.assessed.json](gates.assessed.json).

| Dependency | Current sprint status | Accepted for the live-apply gate? | Evidence disposition |
|---|---|---|---|
| 4.1 | in-progress | No | Both upgrade hop gates closed/unsigned; operational results not-run and retained recovery expired. Fresh recovery and actual accepted upgrades remain required. |
| 4.2 | in-progress | No, full review pending | Scoped exposure closure and registry authentication pass; Administrator login/native-access waiver accepted; later GC confirmation passes. The story explicitly retains full workflow review. |
| 4.3 | in-progress | No | Local runner relocation preparation only; destination qualification, cutover, revocation and secure-erasure acceptance absent. |
| 4.27 | in-progress | No | Latest 336-test receipt covers an offline certificate prerequisite. Operational gate closed, runtime/recovery acceptance absent, public activation disabled. |
| 4.28 | backlog | No | Private Rancher installation, registration, TLS/MFA/access, authority restoration and native maintenance/recovery acceptance remain unperformed. |

Story 4.26 is **accepted for its original qualification scope**. Its assessment
explicitly leaves management installation, production retirement and upgrade
acceptance false. Historical independent native access and today's direct native
authentication are credited within their demonstrated scope; they do not satisfy
the remaining 4.28 qualification or the five dependency gates.

## Inputs, renderer result and per-stage refusal

`inputs.resolved.json` resolves only the observed CoreDNS namespace and pod
selector, with per-field source paths. The other **35 fields remain null**;
[MISSING-INPUTS.md](MISSING-INPUTS.md) lists every exact owner question.
[INPUT-EVIDENCE.md](INPUT-EVIDENCE.md) explains rejected substitutions.

This exact local command was run from the repository root:

```sh
python3 -B eng/staging-foundation/render.py plan \
  --inputs eng/staging-foundation/inputs.resolved.json \
  --observed-context jpiquot@local \
  --observed-cluster-uid b5860674-1d1e-4a3b-b413-e1bb3b9a4557 \
  --manifest-dir eng/staging-foundation/manifests
```

It exited **2**, with empty stdout and all 35 missing fields named in
[stderr](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-14/live-apply-approval-2026-10-29/plan-final.stderr.txt).
The independent offline `render` attempt also exited 2 and produced no manifest
directory. These refusals are expected; neither invocation contacted the API.

| Stage | Effects when eventually approved | Dry-run/apply disposition now |
|---|---|---|
| 1 — shared-infrastructure | Creates both Namespaces with PSA restricted enforce/audit/warn, a staging StorageClass with encryption/custody annotations, and the fixed staging PriorityClass below production. No workload, Secret, PVC or data operation. | **Refused.** Provisioner/configuration/classes, encryption attestation, custody, priorities and applying principal are unknown; global validation also requires the remaining inputs. No exact server-dry-run/apply argv or manifest bytes can be issued. |
| 2 — environment-layer | Creates both namespaces' ResourceQuotas, production class/priority denial quotas, LimitRanges, default-deny/DNS/data-TLS NetworkPolicies and the data TLS contract ConfigMap. Creates no certificate/key, workload, Secret or PVC. | **Refused.** Layer/job principals, production names, quotas, limits and service TLS inventory are incomplete. Both namespaces must exist after approved stage 1 before stage 2's server dry-run can work. |

No mutation command containing a guessed identity, class, quantity or port is
provided. Once all fields are evidenced, re-run the offline renderer and plan;
review the resulting exact manifests and per-identity argv before requesting
operational approval. Passing input validation still leaves dependency, live CNI,
DNS datapath and exact-byte approval gates closed.

The current quotas deny only the named production StorageClass. They do not
prove that claims can bind only through the staging class. Before live approval,
provide the reviewed enforcement and evidence for all other class/default/static
binding paths. The current generated identity denial list also leaves the gaps
described below. These remain open qualification work, alongside the missing
inputs; completing JSON fields alone cannot close them.

Withhold workload deployment authority until **both layers and complete isolation
verification succeed**. This applies to the interval after namespace creation
and before stage 2, and to any partial apply. Existing workload principals must
not gain usable namespace deployment authority during that interval. This
package neither grants principals nor creates workloads.

## Read-only verification

These are reviewable read-only commands for the known target and names. They
were **not separately executed** by this package; the recorded native collector
used its own fixed query allowlist. Missing objects before apply are expected.
Pin the API address, enable CA verification and verify the IP SAN as shown;
first verify the selected context has the intended CA, inline certificate identity,
no proxy override and no alternate TLS server name. The separate sealed-context
collector remains the recorded authenticated observation; matching a reported UID
alone cannot reproduce its trust checks. Never substitute list success or object
existence for isolation acceptance.

```sh
kubectl --context jpiquot@local config view --minify -o 'jsonpath={.clusters[0].cluster.server}'
kubectl --context jpiquot@local --server=https://192.168.1.30:6443 --insecure-skip-tls-verify=false --tls-server-name=192.168.1.30 get namespace kube-system -o 'jsonpath={.metadata.uid}'
kubectl --context jpiquot@local --server=https://192.168.1.30:6443 --insecure-skip-tls-verify=false --tls-server-name=192.168.1.30 auth whoami -o json
kubectl --context jpiquot@local --server=https://192.168.1.30:6443 --insecure-skip-tls-verify=false --tls-server-name=192.168.1.30 get namespace hexalith-staging hexalith-staging-data -o json
kubectl --context jpiquot@local --server=https://192.168.1.30:6443 --insecure-skip-tls-verify=false --tls-server-name=192.168.1.30 get storageclasses.storage.k8s.io,priorityclasses.scheduling.k8s.io -o json
kubectl --context jpiquot@local --server=https://192.168.1.30:6443 --insecure-skip-tls-verify=false --tls-server-name=192.168.1.30 -n hexalith-staging get resourcequota,limitrange,networkpolicy -o json
kubectl --context jpiquot@local --server=https://192.168.1.30:6443 --insecure-skip-tls-verify=false --tls-server-name=192.168.1.30 -n hexalith-staging-data get resourcequota,limitrange,networkpolicy -o json
kubectl --context jpiquot@local --server=https://192.168.1.30:6443 --insecure-skip-tls-verify=false --tls-server-name=192.168.1.30 -n hexalith-staging-data get configmap data-service-tls-contract -o json
```

Before and after every authorized stage, context/API/UID must match the table.
Inspect PSA labels, exact storage provider/configuration and encryption/custody
evidence, staging priority below production, quotas/limits, production denials,
default-deny and exact DNS/TLS allowances. Per-job and cross-layer `auth can-i`
denial commands are **refused until the real principals are supplied**; every
denial in the generated plan must answer `no` (exit 1), using an observer allowed
to impersonate those exact identities. No wildcard or operator identity fills
that gap. The generated list covers selected object kinds, verbs and Secret
reads; it does not prove that jobs cannot write arbitrary data-namespace workloads,
PVCs or the environment-owned TLS ConfigMap. The cross-layer list is also narrower
than a complete write-denial contract. Supply reviewed positive layer permissions
and complete negative job/cross-layer coverage before live approval and before
any workload identity is enabled. The live CNI allow/deny probe needs a
digest-pinned compatible image and separate exact write authorization; neither
was supplied or run.

## Forward-only recovery

No recovery operation is authorized now. After a separately approved partial
apply, stop on a target change, preserve observations, and re-run the approved
server dry-run followed by the same reviewed manifests under the same layer
identity to converge. Stage 2 follows successful stage 1 namespace creation.
Keep workload deployment authority withheld throughout partial recovery until
both layers and their isolation checks pass; do not admit workloads into a
namespace with incomplete quotas or policies. Never remove default-deny to bypass a policy failure: review and apply a forward
policy/quota correction under the environment identity.

StorageClass provisioner/parameters/reclaimPolicy and PriorityClass value are
immutable; changed values require a newly reviewed class name. The renderer
hardcodes the staging PriorityClass name, so changing that name requires a
separately reviewed offline definition/renderer update and related references;
changing only `priority.stagingValue` cannot implement this recovery. Existing PVCs
retain their class. Never use Helm rollback, namespace/PVC/PV deletion or data or
credential rewind. Any data restore needs its own exact owner-approved source,
destination and operation; this package supplies none.

## Evidence and next story

The [evidence directory](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-14/live-apply-approval-2026-10-29/)
retains commands, exit codes, logs, input hashes and sanitized native observations.
The foundation suite passed **34 tests**, including no-process/network-call and
module-boundary checks. The existing module-boundary unit test inspects only four
historical files; it does not cover every new artifact. A separate package
validation checks the new Markdown/JSON content, source pointers/hashes, exact
missing-field set, status gates and recorded native argv; see `verification.json`
in that evidence directory. Native collection made only bounded reads and
non-persistent authentication/authorization introspection, with the existing
credential frozen in sealed anonymous memory and no credential export.

**Next: Story 4.15 — per-environment OpenBao and tenant-key store.** Its data
namespace prerequisite remains absent. Preparation can define the separate
OpenBao/key-store boundary, exclusions from ordinary snapshots, per-app tokens
and renewal ownership offline. No install, token issuance, key initialization,
snapshot/restore or secret provisioning is authorized before the 4.14 live apply
and its required qualification. Consumer acceptance never gates this foundation;
the dependency direction remains [one-way](DEPENDENCY-DIRECTION.md).

The native collector receipt binds its unchanged observer source bytes. A separate
`native-client-post-collection.json` records local client version/binary identity
measured after collection, without a new cluster request. The client binary/version
at the original query cut was not recorded and is not retrospectively asserted.
