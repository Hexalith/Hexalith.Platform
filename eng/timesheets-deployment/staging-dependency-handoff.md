# Timesheets staging dependency handoff — 2026-10-14

Timesheets has an existing Development/source binding, but staging deployment
and activation remain unavailable and unauthorized. **Timesheets Story 3.6
acceptance and release remain FAIL.** This handoff prepares the supported-cluster
dependency only. No retirement, upgrade, signing, publication, domain command
or deployment was performed by this preparation.

## Native findings

Read-only native observations at `2026-10-14T06:43:46Z` used the retained
v1.34.12 client with context `jpiquot@local` and a 15-second request timeout:

| Read | Exit | Sanitized observation |
| --- | --- | --- |
| `version --client -o json` | 0 | Client v1.34.12 |
| `get --raw=/version` | 0 | Server v1.34.9 |
| `get namespace kube-system -o json` | 0 | UID `b5860674-1d1e-4a3b-b413-e1bb3b9a4557` |
| `get namespaces hexalith-staging hexalith-staging-data -o json` | 1 | Both namespaces NotFound |
| `get gateways -A -o json` | 1 | Gateway resource type unavailable |

These native reads establish the dated observations listed above. Current target
acceptance and authorization remain unvalidated. Repeat native binding
immediately before any separately approved operation. The exact version read is:

```bash
/home/administrator/hexalith-upgrade-evidence/tools/v1.34.12/kubectl \
  --context jpiquot@local --request-timeout=15s get --raw=/version
```

The historical Story 4.0 bundle `20261001t061620z` expired at
`2026-10-02T06:16:52Z`. Its retained signatures and `done` status extend no
validity. Current-byte retirement fixture receipts do not establish a current
signed source-bound production plan, prerequisite packet, approval or accepted
4.27 result. The extended maintenance window ending
`2026-10-31T23:00:00Z` also grants no technical gate or procedure approval.

## Dependency and approval order

Follow the [cluster-management order](../cluster-management/README.md):

1. Preserve 4.26 qualification; accept 4.2 public-console closure and obtain
   fresh signed 4.0 proof/validation/readback plus a fresh production external-etcd
   point, isolated restore and node/configuration recovery under 4.1's contract.
   Confirm independent private native Administrator/break-glass access.
2. Use the existing [4.27 production executor handoff](../cluster-management/RETIRE-KUBESPHERE.md#production-executor-and-exact-attempt-handoff-2026-10-05)
   as the next retirement workflow entry: a new default-mode read-only planning
   census with explicit retained tools, private static native configuration,
   approved trust root and two-recipient custody. Bind final-byte catalog,
   seven-namespace and fresh-node restore receipts, then produce a new fresh
   plan and signed prerequisites for a distinct future execution attempt.
   The existing handoff supplies all CLI inputs; private paths/principals and
   signing/execution require their separately approved scope. No such command
   was run here. Only separately armed exact-attempt approval permits retirement.
3. Obtain the exact signed retirement/preservation acceptance and current
   private native-access proof. Verify absent KubeSphere runtime, owned blocking
   admission dependencies and public route, and qualified owners of remaining
   dependencies. A successful executable or rehearsal alone supplies no acceptance.
4. Collect fresh post-retirement census, external-etcd point and matching
   isolated restore, encrypted off-node node/configuration recovery and signed
   4.0 validation/readback. Derive the successor maintenance/outage/stop/recovery
   procedure from the remaining controllers and admission dependencies only
   after accepted retirement and that fresh census. Obtain approval of its
   exact SHA-256 and bind it to each hop; preserve historical `MAINTENANCE.md`.
5. Qualify and separately approve 1.34 → 1.35, accept its actual outcome,
   refresh recovery/preflight/compatibility/health/approval, then separately
   qualify and approve 1.35 → 1.36. The selected dated patches are v1.35.9 and
   v1.36.5; resolve support/security and Rancher import compatibility afresh
   before each hop. Any changed target requires a new qualified, approved plan.
6. Complete 4.28 private Rancher/access/initial restore, accept 4.3 runner
   relocation, then qualify staging infrastructure and release/provider gates.
   4.3 may proceed independently earlier. Kubernetes 1.37 awaits Rancher support
   and fresh qualification. This handoff selects no additional hop.

## Local preparation command

Run from this Platform checkout:

```bash
python3 eng/kubernetes-upgrade/prepare.py --operator 'Codex (preparation only)'
```

This reads local sources and creates a new owner-only, checksummed, unsigned
attempt outside Git. It executes no operational command. Both hop gates stay
closed; live source and actual final versions stay unknown in its records.
`--backup-bundle` inventories a selected local bundle only; signature presence,
passing declarations and timestamps cannot validate trust, off-node custody,
retirement, recovery, currency or approval. Missing required sources refuse
before publishing an attempt. Existing IDs and repository/recovery-bundle
output destinations are refused and preserved.

## Proposed sequential kubeadm commands

The following is a command proposal for later exact-procedure approval, not an
approved successor procedure or an executable chain. The
[1.35 kubeadm guide](https://v1-35.docs.kubernetes.io/docs/tasks/administer-cluster/kubeadm/kubeadm-upgrade/)
and [1.36 kubeadm guide](https://v1-36.docs.kubernetes.io/docs/tasks/administer-cluster/kubeadm/kubeadm-upgrade/)
support the sequential plan/apply syntax. Minor versions cannot be skipped.
External etcd needs its own verified recovery: kubeadm's local-etcd backup is
empty for external etcd. No downgrade or improvised restore is proposed.

The node has manually installed binaries. The successor must bind the exact
effective executable paths, upstream checksums, control-plane/add-on image
digests and approved replacement steps; there is no apt-install assumption.
`HOP1_KUBEADM` and `HOP2_KUBEADM` below must name the exact qualified absolute
node-side target binaries. No values are selected here. Planning must use the
current or separately approved standalone binary without replacing installed
state; any binary installation/replacement is itself a gated mutation.

For hop 1, revalidate accepted retirement, fresh recovery, exact successor
procedure/outage approval, target currency/compatibility, live source/skew,
authenticated health, Forgejo consistency disposition and dynamic storage
provision/write/read/cleanup. Then propose target planning:

```bash
"$HOP1_KUBEADM" upgrade plan v1.35.9
```

Immediately before the first outage or installed-binary change, require the new
Administrator-signed open `hop-gate-1.34-to-1.35.json` bound to all exact fresh
inputs. Re-run target planning after any approved kubeadm replacement. Record
terminal approved quiescence of every tracked workflow, observed cordon and
successful drain before applying; the successor defines exact workload steps
and named static-pod/DaemonSet/local-data exceptions. Failed preflight, drift,
disruption-budget refusal or incomplete transitions stop the attempt.

```bash
"$HOP1_KUBEADM" upgrade apply v1.35.9
```

Follow the approved target instructions for kubelet/kubectl replacement and
required kubelet restart. Require API/node Ready, native external-etcd health,
skew, CNI/DNS/operator/admission clearance, fresh storage proof and protected
workload outcomes before uncordon/service restoration. Record actual component
versions and accept the signed first-hop outcome. Failure stops subsequent hops
and invokes the approved accountable recovery decision with diagnostics retained.

Hop 2 begins only after that accepted outcome and newly refreshed recovery,
4.0 validation/readback, live versions, target currency/compatibility,
preflight/storage/health and exact procedure/outage approval. It has its own
new signed-open `hop-gate-1.35-to-1.36.json`; repeat successful quiesce/cordon/drain
before apply. Neither the first gate nor this preparation authorizes it.

```bash
"$HOP2_KUBEADM" upgrade plan v1.36.5
```

After the separate gate and terminal transitions:

```bash
"$HOP2_KUBEADM" upgrade apply v1.36.5
```

Repeat the approved kubelet/kubectl, health/storage/skew and restoration checks.
Accept only the measured actual final versions and full protected-workload
outcome, never the selected targets or pending local records.

## Release and Timesheets acceptance prerequisites

The [local binding qualification](local-host-binding-qualification.md) supplies
Development/source evidence only. The historical [plan](plan.json) is preserved.
Hosted enrollment still needs:

| Owner gate | Missing prerequisite |
| --- | --- |
| Platform / Builds / Timesheets release | Qualified Release image digest, owner package identities, application chart OCI digest, provenance/catalog identity and retained rollback package |
| Platform environment | Supported accepted target; separate application/data namespaces; Gateway/certificates; OpenBao, staging realm, Dapr trust/ACLs, durable broker, exact `timesheets-readmodels` binding and private 8081 networking; qualified 4.14–4.17 / 4.22 hosted composition |
| EventStore / identity / sibling owners | Production resource-binding issuer and real Timesheets authorization, tenant access and Projects/Works/Parties reference providers |
| Timesheets source / operator authority | Certified complete cross-tenant magic-link inventory and immutable source cut; enforced writer/delivery quiescence and separately qualified migration authority; source/pending-delivery continuity and recovery |
| Timesheets / EventStore deployed acceptance | Installed subscriber delivery, shared rebuild and persisted end-state proof; full deployed magic-link critical flow bound to exact retained release and observed target |

Default-deny authorization/reference/migration providers and unavailable inventory
providers remain unchanged. A Kubernetes census, successful local binding,
readiness or infrastructure approval closes none of these gates. No application
Helm invocation can be specified until the owner-qualified retained release
package and approved Platform release procedure exist. Story 3.6 stays open/FAIL.

## Historical preparation receipt and verification

The local receipt below records preparation custody and checks before review
fixes. Its source hashes matched the consumed bytes at that verification time;
subsequent preparer changes mean this attempt does not bind the final implementation.
A new final-byte preparation receipt remains pending. The historical receipt
supplies no signature, acceptance, current operational evidence or mutation authority.

- Attempt: `20261014t065220z-preparation`, recorded `2026-10-14T06:52:20Z`.
- Private location: `~/hexalith-upgrade-evidence/evidence/epic-4/4-1/20261014t065220z-preparation/`.
- Command: `python3 eng/kubernetes-upgrade/prepare.py --operator 'Codex (preparation only)'`, exit 0.
- All 16 JSON records are unsigned, `mutationAuthorized=false` and
  `liveEvidenceCollected=false`; both hop gates are closed and all command lists
  are empty. No backup bundle was selected by this command.
- At verification, attempt and evidence ancestors through the evidence root
  were `0700`, all 17 files were `0600`, and every checksum and all 13 consumed
  source hashes matched the then-inspected bytes.

| Retained identity | SHA-256 |
| --- | --- |
| `SHA256SUMS` | `1f91958a0c0a06ae95c38deb34954ce150e3dbed0f6b77278b10c046e3970a2f` |
| `attempt.json` | `e9a0d93d1b9455064eb16d544b4d64de76a2372e50f7a277e687256e53249984` |
| `gate-validation.json` | `eb29412e2d03ea466df7b54c7b1c35039d264511f2c21c1f00b710d551810dca` |
| `hop-gate-1.34-to-1.35.json` | `ed9debc0b660c90e0109bb673d5dad577a0d52657af20828039228471e95ba9c` |
| `hop-gate-1.35-to-1.36.json` | `7ed18e4694e22f292c85d7762d93776f38032d4567766a9e7c2f913d1a2bf083` |
| Dated target-review source | `29342d36b3c859eeab2e597b495b4deaa907108411bcb2a485634cc8bed4e10b` |
| Preserved `eng/kubernetes-upgrade/MAINTENANCE.md` | `834be8225ca05175e7cbf7c6d4ee35324401c196002002269223b9000a97d8b5` |
| Preserved `eng/timesheets-deployment/plan.json` | `04b24f38d1910390bb075b8fe27e8f159ba202b6aeacf35f517bcc272a8043dc` |

Before review fixes,
`python3 -m unittest discover -s eng/kubernetes-upgrade -p 'test_*.py' -v`
passed all 14 tests without skips. That executed matrix covered both dated hops
and source/procedure lineage; missing-source refusal before publication;
untrusted passing/status/signature claims; absent first-hop acceptance and new
second-hop inputs; existing-output/repository/recovery custody refusal; private
modes/checksums; and consistent selection hashes during source replacement.
`git diff --check` passed. Historical artifact hashes above remained unchanged;
no Timesheets runtime/provider code, historical plan, maintenance proposal,
submodule input, Git index or branch was changed by this implementation.

The enrollment check
`python3 -m unittest discover -s eng/timesheets-deployment -p 'test_*.py'`
returned **exit 1, 31 setup errors**, because the mapped Platform input
`references/Hexalith.Timesheets/src/Hexalith.Timesheets/Hexalith.Timesheets.csproj`
is absent. The pre-existing environment blocker remains; no nested dependency
was initialized and no test was weakened. Enrollment safety tests therefore
have no passing result in this checkout. Actual retirement, recovery refresh,
per-hop execution/acceptance and Timesheets deployed acceptance remain blocked.

## Final preparation after review

Three independent reviews completed. Corrected malformed/ambiguous target
selection, future dates and UTC-overflow handling; added explicit pending
pre-upgrade health and approved-gate/procedure/postflight bindings. The
pre-existing absence of an immutable preparation-source archive remains in the
[deferred ledger](../../_bmad-output/implementation-artifacts/deferred-work.md).
This is an operational audit prerequisite; source hashes alone supply no
retained Release artifact or execution authority.

Final-byte attempt `20261014t070550z-preparation` was created by the same exact
local preparation command at `2026-10-14T07:05:50Z`, exit 0. Its private location
is `~/hexalith-upgrade-evidence/evidence/epic-4/4-1/20261014t070550z-preparation/`.
All 16 JSON records remain unsigned and mutation unauthorized, both hop gates
are closed, and all actual source/result versions and approval/health/postflight
bindings remain unestablished. All 17 files are `0600`, attempt/evidence
ancestors are `0700`, every checksum matches, and all 13 consumed source hashes
match the final implementation. The earlier receipt is preserved and binds its
own pre-review source bytes.

| Final retained record | SHA-256 |
| --- | --- |
| `SHA256SUMS` | `3bc626a86acbe70b1930765b2654df4b851d05ebb6b02ede06e170d88f702e0b` |
| `attempt.json` | `d34ab2fe71cba4827840826196bcfd5e1ef90f97c488455205cbf6da4cc7dfdb` |
| `gate-validation.json` | `ab4eca0f4e53c5deeafcbed5bd4d3e0f2bec7ddba37adc740d0c20cc29db75a1` |
| `hop-gate-1.34-to-1.35.json` | `80be5aeb1d94dc5720fc28a63093667e45cad0f5a2188bfe5a1c26179bf7007c` |
| `hop-gate-1.35-to-1.36.json` | `a9c66306a1024c3a759e8c4d47cd13ba342dcc771c604cbd9ddf07c1b0be1244` |

Final verification: `python3 -m unittest discover -s eng/kubernetes-upgrade -p
"test_*.py" -v` exited 0, **17/17 passed**, no skips. The parent independently
verified the final receipt and reviewed the patched code/tests. The unchanged
31 enrollment-test setup errors remain the separately recorded checkout-input
blocker; that suite was not rerun because no input or enrollment code changed.
`git diff --check` passes. Both historical artifact hashes remain unchanged.

**Closed:** the successor upgrade-preparation tooling gap: dated two-hop
selection and independent closed gates now represent the adopted ordering.
**Next operational blocker:** fresh signed recovery/readback and a fresh exact
4.27 retirement plan, prerequisite packet and approval, followed by accepted
retirement/preservation. The expired October 2 backup gate, historical fixture
receipts and extended maintenance window cannot close it. The proposed kubeadm
commands above remain conditional on exact binary/procedure qualification;
fully bound Timesheets deployment commands cannot be produced while the chart,
image and approved release workflow are absent. No infrastructure command,
signing, publication, Git staging/commit or Timesheets acceptance change occurred.
