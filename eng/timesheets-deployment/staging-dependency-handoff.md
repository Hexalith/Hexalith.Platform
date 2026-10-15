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

## Offline retirement continuation — 2026-10-14

The [local retirement prerequisites](../cluster-management/STAGING-PREREQUISITES.md)
add an offline-only packet preparer, exact conditional first-census / sequential
namespace-catalog-fresh-node / second-plan commands, original signature checks
and the unresolved operator evidence. The planned local pointer was superseded
when concurrent repository activity supplied this existing handoff; its full
prior history above is preserved. This continuation uses the exact external
input at
`/home/administrator/projects/hexalith/timesheets/references/Hexalith.Platform/eng/timesheets-deployment/staging-dependency-handoff.md`,
SHA-256 `b12758d8a528a876314e7d49b02a864530fc1a7852f40818701ee0f8f789ef38`.
The dated native findings above belong to that input; this continuation makes
no new live observations and treats the extended window as no approval.

Packet `20261014t100941z-s427-reviewed-preparation` is prepared under
`/home/administrator/hexalith-retirement-preparation-evidence/` with retained
source bytes, complete historical fixture-manifest/readback-receipt integrity
and metadata path/digest lineage. Recovery originals/signatures stay in their
existing private custody. See the deliberately sanitized, unsigned
[local verification receipt](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261014t100941z-s427-reviewed-preparation/verification.json)
for packet/test digests. This is no fresh decryption, off-node acceptance,
source qualification, plan, approval or operational proof. Story 4.27 remains
`in-progress`, Story 4.1 stays closed and Timesheets Story 3.6 remains open/FAIL.
All release, infrastructure, provider, source and deployed-acceptance gates
above and all default-deny providers remain unchanged. The imported enrollment
suite's 31 setup errors belong to its historical checkout; the mapped Timesheets
project is present here now, and this retirement preparation makes no new
enrollment-test claim. No command that contacts production or signs, retires,
upgrades or deploys was run here.
## Retirement prerequisite investigation — 2026-10-14

The [next operator action](../cluster-management/NEXT-OPERATOR-ACTION.md) now
names the concrete unsigned fresh 4.0 tools bundle `20261014t112353z`, its
Administrator-owned scope/signing/unlock blocker, exact retained-tool commands
and measurable recovery acceptance. This follows read-only native/provider/
Memories and narrow console investigations, not a retirement or upgrade attempt.
Historical Story 4.2 console closure was accepted; its fresh signed 4.27
attempt/plan-bound record remains required. Current provider readback matched
all 553 historical objects, but validation still fails seven age/staleness
checks. Readable old recovery is not fresh recovery.

New offline packet `20261014t112004z-s427-prerequisite-investigation` and the
[separate unsigned verification receipt](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261014t112004z-s427-prerequisite-investigation/verification.json)
retain final documentation/source/checksum lineage and separately distinguish
read-only observations from zero-remote offline preparation. The old packets,
external handoff provenance and dates remain preserved. No policy upload,
quiescence/capture/restore/signing, retirement, upgrade, deployment, domain
mutation or Git staging/commit occurred. 4.27 stays `in-progress`, 4.1 closed,
Timesheets 3.6 `in-progress`/FAIL; all later owner, infrastructure, release,
provider, source/deployed-acceptance and default-deny gates remain unchanged.
## Actual recovery policy prepared — 2026-10-14T11:40:42Z

The [next operator action](../cluster-management/NEXT-OPERATOR-ACTION.md) now
points to actual unsigned current policy/precheck bytes for recovery RID
`20261014t112353z`. Separate retained read-only precheck and local policy
generation ran; no upload or signature was produced. The remaining approval
binds exact current policy/capture/signing scope to the already recorded
October 7–31 dates, this conversation and Administrator roles, which need no
new selection. Historical freshness remains expired and no recovery capture
or restore result is supplied by the policy.

Final offline packet `20261014t114108z-s427-current-policy-preparation` and
[unsigned verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261014t114108z-s427-current-policy-preparation/verification.json)
retain corrected final command/source/private-policy lineage. The prior
investigation packet/receipt remain immutable. No retirement, upgrade,
deployment, publication, signing or domain mutation occurred; 4.27 stays
`in-progress`, 4.1 closed and Timesheets 3.6 `in-progress`/FAIL, preserving all
later gates/default-deny providers.


## Approved recovery strict stop — 2026-10-14T13:11:55Z

The Administrator completed private-terminal signing of exact policy RID
`20261014t112353z`. The original signature and two exact immutable provider
versions verified, including fresh validator readback, unchanged policy/precheck/
37 tool files/trust, current age/window, safe static native transport, source
identity/readiness and absent isolated targets. The prior signing refusal and
all preparation packets retain their historical meaning and exact bytes.

Twelve approved top-level commands then ran sequentially through the retained
Keycloak OIDC helper. Current Backup phase was `completed`; the isolated
PostgreSQL restore became Ready and all ten database/canary comparisons matched.
The isolated Keycloak app and temporary verifier bootstrap succeeded. The OIDC
helper returned process exit zero but recorded **`allPass=false`**:
`tokenRoleClaimsEqualLive=false`, with a missing live claims reference. Its
isolated client-credentials login returned 200 and seven of eight OIDC/admin/
count checks passed. The unchanged helper requests its live reference token
from `https://auth.tache.ai/realms/master/protocol/openid-connect/token`;
it does not record that reference request's HTTP status. This is a failed
acceptance check, not proof of a particular unrecorded HTTP failure or complete
recovery. The sequence stopped on that semantic result, with no retry.

At the stopped-state read, all four protected namespace UIDs, all 15 source
PVC UIDs/bindings and 15 PV UIDs/claim references were unchanged. Source
Keycloak/OpenBao/Redis/FalkorDB were ready; Memories MCP remained 2/2 and was
never paused. Only isolated `recovery-4-0-kc` remains, with namespace UID
`dafd75fd-65b7-4650-ab2a-7e0993bd68f1`, a new isolated PVC/PV and ready DB/app.
The CNPG watcher remains `keycloak,cnpg-system,recovery-4-0-kc`; other approved
restore targets are absent. Its temporary writer credential copy remains
inside that isolated namespace, pending the incident decision. The local
verifier scratch secret was removed by the retained helper. No later isolation,
OpenBao, Memories, cleanup, records/proofs/gate/validation or signing command
was dispatched. No retirement, upgrade or Timesheets deployment ran.

**Next required input:** the Administrator, as recorded incident owner, must
decide how to handle this preserved isolated target/watcher and the missing
live-reference prerequisite before any operational retry or cleanup. The
existing recovery-scope approval, October 7–31 dates, this conversation as
incident channel and roles persist; do not request them again. Do not rerun
`new`, `precheck.py`, `policy.py`, the uploading policy composite or the failed
helper automatically; do not change the approved/bound tools, reopen the
public master route or weaken the comparison. Fresh recovery remains
unaccepted; 4.27 stays `in-progress`, 4.1 closed and Timesheets 3.6 open/FAIL.

New immutable operator custody is
`/home/administrator/hexalith-retirement-investigation-evidence/20261014t125138z-s427-approved-recovery-run`.
It retains encrypted command/result and stopped-state diagnostics without a
recipient-decryption claim, exact original input/readback bindings, actual
sequential results, failure and protected-state checks. The separate
[sanitized unsigned verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261014t125138z-s427-approved-recovery-run/verification.json)
binds local integrity/preservation; it supplies no signed accepted recovery or
production-retirement gate. Bound retirement execution/procedure/maintenance,
frozen intent, original production tasks and older receipts remain preserved.


## Private OIDC investigation; exact incident action pending — 2026-10-14

The sealed strict-stop attempt retains all twelve completed authorized
Keycloak commands and zero further dependent operational dispatches. Its
failed canonical OIDC artifact remains unchanged, SHA-256
`014463ef76fd99431f6c268edbc41274b5031157f683a1630011956cb5738578`.
This mid-sequence failure followed an actual completed backup/partial restore;
the older zero-capture refusal describes the earlier unavailable-key gate.

Separate authorized investigation through explicit static `jpiquot@local` and
accepted loopback-only private source administration obtained source/restored/
existing-verifier HTTP 200, equal same-client claims digests and all eight
unchanged checks true. It read the existing isolated verifier credential with
bounded READ ONLY SQL and kept credentials/tokens only in memory. Both started
forwards terminated and listeners were absent. Distinct create-only diagnostic
SHA-256 is `38e70fa5cc0f48bcc40884dd0e62d3a04284190cb052aa29f41e503afa2249d7`;
this is investigation, not fresh accepted recovery or incident authorization.
The public master route remains closed and the failed original/37 approved
tools/policy/signature/old receipts remain intact.

The prepared private packet is
`/home/administrator/hexalith-retirement-investigation-evidence/20261014t131927z-s427-private-oidc-correction`.
Its `INCIDENT-ACTION.md` and create-only `incident-decision-final-pending.json`
(`incidentApproved=false`) bind the original approval/failure, corrected
helper/diagnostic, separate guarded proof consumer and actual retained target.
The concrete next owner decision is acceptance of those changed exact bytes
and continuation on the existing target, first replaying missing Keycloak
isolation/target identity. Original `proofs.py` still reads the failed canonical
file; the separate variant supplies only a guarded distinct reference plus
signed-proof provenance, with original acceptance logic byte-identical.
Seventeen meaningful offline tests pass, including changed-input/claims,
unknown/false checks, unavailable reference, create-only output, uncertain
forward cleanup and exact prospective/pending incident-decision boundaries.
No operational proof, isolation, later-system, cleanup or watcher change ran.

This is an incident decision, not repeat original recovery-scope/window/role
approval. Reuse those existing decisions within their narrow scope. A partial
abort needs a separate cleanup packet: unchanged full `cleanup_check.py`
assumes all three isolation records, which are absent, so no executable abort
is advertised. The retained target/watcher remains under the Administrator
incident owner. Keep 4.27 in progress, 4.1 closed and Timesheets 3.6 open/FAIL;
no retirement, upgrade or Timesheets deployment/acceptance is implied. The
[separate unsigned verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261014t131927z-s427-private-oidc-correction/verification.json)
retains exact action/source/checksum/preservation lineage without promoting
the diagnostic to recovery acceptance.


## Accepted OIDC continuation, resumed Memories and unsigned cleanup — 2026-10-14

The Administrator's actual **“Accept the OIDC correction and continue 20261014t112353z.”** decision is retained; original recovery scope, destination, October 7–31 maintenance dates, this conversation as incident channel and acknowledged Administrator incident/recovery/outage roles remain within their existing limits. No repeated scope or incident decision is pending. The supplied **“Freeze announced for this run”** C3 announcement enabled the single final Memories pre/census and approved pause/drain/capture sequence.

The new continuation completed 32 authorized bounded top-level commands with passing actual semantic gates: fresh full incident preflight; retained Keycloak isolation/identity; OpenBao snapshot, immutable transfer/readback, isolated restore and all 10 verification checks; the split paired Memories physical capture/readback/isolated restore and all 17 verification checks; each system's complete isolation/identity; immediate conditional Memories resume; guarded original cleanup; protected-source/watcher and actual cleanup-signing preflight reads. The older 12-command run and its failed canonical OIDC record remain unchanged. The accepted distinct all-eight-check diagnostic is consumed only by the future guarded proof variant; no failed record was overwritten or promoted.

Memories resumed at **2026-10-14T15:18:55Z** with MCP 2/2 ready, both source readiness probes HTTP 200/Healthy, a clean drain recheck, zero pending/in-flight/missing/duplicate work and an unchanged stream. The actual freeze end was announced in this conversation. Both source stores restarted and retained their comparable censuses/AOF health; the approved zero-tenant/zero-graph baseline supplies no logical verifier-pass claim. All three recorded isolated target namespace UIDs, all six target PVs and temporary credential/seal copies are absent. The CNPG controller identity is unchanged and `WATCH_NAMESPACE=keycloak,cnpg-system`. Fresh reads preserve the four protected source namespace UIDs and all 15 PVC/PV bindings. Host-directory removal was inferred from OpenEBS PV deletion; no node-directory inspection was claimed.

The actual cleanup record is **unsigned**, SHA-256 `3ae690daad23e6a0f028ab28ca023ba9a04d4c72f810d9129b117125d08d5b78`, and binds cleanup-check SHA-256 `ac945fb40b695e147f2e6e4dc2a9af5a8fcdb8ecb79fc0c2a814c1d6de997778`. The immediate remaining input is **the Administrator's protected signing access** for that reviewed exact record. [Concrete guarded protected-terminal signing action](/home/administrator/hexalith-retirement-investigation-evidence/20261014t144239z-s427-accepted-oidc-continuation/NEXT-PROTECTED-SIGNING-ACTION.md) binds 99 inputs, exact approved policy/precheck/tool/trust/incident bytes, actual cleanup and fresh native checks. The known noninteractive key refusal was not repeated. Authentic signature verification and fresh exact-version validator readbacks must pass before the separate guarded records prerequisite, original manifest and approved external proof consumer can run. No cleanup signature, manifest/proof writing, backup gate, non-dry final validation or later signatures was dispatched by this continuation at the recorded barrier.

Current read-only M1 preparation matched both live Redis/FalkorDB run IDs to the approved post-copy restart. The Administrator's current GET-only M2 investigation at **2026-10-14T15:32:00Z** enumerated all IAM policies/groups stably, found the sole approved writer policy with exact three permission sets/recovery-project scope and no group membership, and verified the exact current bucket policy's allowed writer actions with no delete/retention/legal-hold rights. The owner-only M2 v2 receipt SHA-256 is `08d587bb85da790b5bbb245eca259ec6ee15cf3d2ea1b27a952d707108dab32b`; encrypted raw readback and the first false local resource-syntax check/correction remain preserved. No IAM write/delete attempt occurred. Neither preparation is the later honest Administrator M1/M2 final-validation attestation; all eight required authentic signatures, immutable readbacks and final non-dry validation remain required.

The [separate unsigned continuation receipt](/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261014t144239z-s427-accepted-oidc-continuation/verification.json) binds owner-only encrypted events, exact source archive, authentic approval, observed resume/freeze-end, provider/IAM read-only receipts and the current signing action. Earlier packets/receipts/archives, original 37 tools, frozen intent, original production Tasks & Acceptance, legacy baseline and bound retirement/procedure/maintenance bytes remain preserved. Concurrent unrelated deferred-work/spec-1-6 edits are retained as workspace activity, with private current snapshots and truthful digest drift; they are not consumed recovery inputs. Unchanged fixture/test suites were not rerun for this operational checkpoint; actual semantic/native gates, syntax, custody, archive and preservation checks supply the current evidence.

This completed recovery work supplies no accepted fresh backup gate, retirement plan/approval, independent native break-glass recovery, attempt/plan-bound signed console closure, 4.1 external-etcd/node recovery, upgrade, Rancher or Timesheets deployment/activation. **4.27 remains in-progress, 4.1 closed and Timesheets 3.6 open/FAIL.**


## Signed cleanup and concurrent unsigned records reconciled — 2026-10-15

At **2026-10-15T08:49:13Z**, this continuation independently authenticated the
unchanged original policy and cleanup signatures and freshly read their four
exact immutable provider versions with the validator credential. All hashes,
byte counts, AES256, COMPLIANCE and retention checks passed. Policy age was
76,105 seconds against 86,400; its earliest deadline remains
**2026-10-15T11:40:42Z (13:40:42 Europe/Paris)**. The maintenance window does not
extend freshness. No new scope/window/role approval is pending.

Separate concurrent custody `20261015t084001z-s427-signed-cleanup-continuation`
created the manifest at 08:45:16Z and three unsigned passing restore proofs at
08:46:19Z. Those stages were not dispatched by this continuation. Their actual
receipt/digest bindings, all 73 manifest local files, three proof results,
RPO/isolation/expiry and accepted OIDC-correction provenance independently
matched. Its manifest records 4,714 provider-version readbacks by that run;
this continuation freshly reread only the four policy/cleanup versions. The
failed original OIDC artifact and original 37 tools remain unchanged. Fresh
native reads preserve four source namespace UIDs, all 15 PVC/PV bindings and
CNPG identity/watch scope; isolated targets, their storage claim references
and temporary credential/seal copies are absent.

This run's new prerequisite command exited 1 without timeout at 08:46:02Z,
while the external records stage was progressing. Its exact stderr remains
sealed encrypted; no recipient decryption or particular original error is
claimed. No dependent stage was dispatched after that stop. A later offline
provider-disabled diagnosis refused the now-existing proof; that later error
does not establish the original failure. The subsequent authorized read-only
reconciliation passed and performed no remote write or record replay.

The immediate prerequisite is the Administrator's own signatures/publication
of the **existing manifest and three proofs**, using the verified
[guarded protected-terminal action](/home/administrator/hexalith-retirement-investigation-evidence/20261015t084001z-s427-signed-cleanup-continuation/NEXT-PROTECTED-SIGNING-ACTION.md)
(SHA-256 `a4cb7d9ce0ff2ccc51332e93bb4ab5525fc42a0417b2a20de0fd3b39d7873bfe`).
Its exact guard passed local-only checks with zero remote requests; its Bash
block passed syntax validation. Full terminal guard/signing was not invoked
and the known unlock refusal was not repeated. All four signatures, the gate
and final validation were absent at reconciliation. Authenticate actual
signatures and fresh immutable versions before any dependent gate; later gate
signing, non-dry validation and honest current M1/M2 remain required. Preserve
an expired attempt rather than changing timestamps or repeating completed
captures/restores/cleanup.

New owner-only custody `20261015t084153z-s427-signed-cleanup-resume` retains the
initial sources, stopped event, encrypted native diagnostics and distinct
read-only reconciliation. The [separate unsigned verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261015t084153z-s427-signed-cleanup-resume/verification.json)
records exact hashes and preservation checks without accepting fresh recovery.
Prior text and concurrent progress are preserved. No unchanged fixture or test
suite was rerun. Original retirement tasks remain pending: **4.27 in-progress,
4.1 closed and Timesheets 3.6 open/FAIL**. No retirement, node/etcd mutation,
upgrade, Rancher, Timesheets deployment/activation or Git staging/commit ran.


## Signed cleanup and prepared recovery proof barrier — 2026-10-15

The [current Platform operator action](../cluster-management/NEXT-OPERATOR-ACTION.md#authenticated-cleanup-and-prepared-unsigned-proofs--2026-10-15) records authenticated cleanup with independent exact-version readbacks, then the passing prerequisite, manifest and accepted private-OIDC consumer in sequence. The manifest read back 4,714 versions; all three prepared unsigned proofs pass, preserving the failed original. Administrator manifest/proof signing remains the immediate barrier; the policy age deadline is 2026-10-15T11:40:42Z. No captures/restores/cleanup were repeated. Actual gate/signatures, non-dry validation and honest final M1/M2 attestation still precede fresh 4.0 acceptance. No later infrastructure/release/provider/source/deployed-acceptance or default-deny gate changes: 4.27 stays in progress, 4.1 closed and Timesheets 3.6 open/FAIL.


## Fresh signed 4.0 accepted; next recovery prerequisite prepared — 2026-10-15

Recovery `20261014t112353z` passed fresh signed acceptance at
**2026-10-15T11:27:34.340622Z**, valid only until
**2026-10-15T11:40:42Z (13:40:42 Paris)**. All eight original Administrator
signatures authenticate, and a separate validator independently reread all
**16 exact immutable record/signature versions**, matching bytes, hashes,
AES256 and COMPLIANCE retention. The actual non-dry validator completed at
11:12:03Z with **4,809/4,809 checks passing, zero failures/skips**, including
**4,714/4,714 manifest versions**. Actual M1 at 11:14:28Z matched both live
post-copy run IDs; M2 at 11:16:14Z confirmed the complete current writer
IAM/group and bucket-policy restrictions through GET-only evidence. The
Administrator's authentic final validation signature attests those manual
steps. Its record version is `1792063538323774`, signature version
`1792063538589066`; the signed validation SHA-256 is
`b269db6ab85f3a99d27560742921f0192d41925d9a3fe2bebd3adc95c6b234ae`.

The delegated manifest/three-proof signatures and eight uploads completed at
11:01:09Z; the temporary signing agent was stopped. The unchanged gate was
assembled once at 11:03:34Z and then signed by the Administrator in the
protected terminal, followed by the single non-dry validation and final owner
signature. No completed capture, restore or cleanup was repeated. The failed
original OIDC record, accepted distinct correction, all 37 original tools and
all 143 checksum-covered previous-checkpoint files remain unchanged. Earlier
pending receipts retain their dated meaning; this checkpoint resolves those
signing barriers. No repeat scope/window/role approval was requested.

After acceptance, the unchanged offline 4.1 preparer created a new private
packet `20261015t112841z-post-40-recovery-prerequisite`: 16 unsigned original
records, checksum manifest, exact read-back archive of 13 source files and a
separate acceptance/lineage supplement. Its own metadata-only/unvalidated
claims and closed gates were preserved. Preparation made **zero remote
requests** and expanded no operational authorization. The first missing
prerequisite is fresh production external-etcd recovery with a matching
isolated restore and encrypted off-node node/configuration recovery, plus
fresh independent private native/break-glass proof. The retained October 1
scripts hardcode stale paths and were not run. The dated upgrade targets
remain unvalidated. The new handoff binds concrete evidence requirements and
current interface gaps; no node/etcd capture, decryption, new native grant,
retirement, upgrade, Rancher or Timesheets operation was dispatched.

Freshness ends at the deadline above regardless of the maintenance window.
Preserve all completed evidence after expiry; no timestamp change or replay
of completed captures/restores/cleanup can extend this acceptance. **4.27
remains in-progress, 4.1's operational gate closed and Timesheets 3.6
open/FAIL**. Later retirement/console/native/recovery/health/execution,
release/provider/source/deployed-acceptance and default-deny gates remain.

[Completed Platform recovery verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261015t110948z-s427-signed-gate-final-validation/verification.json); [next offline prerequisite](/home/administrator/hexalith-upgrade-evidence/evidence/epic-4/4-1/20261015t112841z-post-40-recovery-prerequisite/NEXT-PREREQUISITE.md).


## Isolated offline recovery closeout — 2026-10-15

The [attempt-specific guarded procedure](../kubernetes-upgrade/recovery-20261015t140709z/PROCEDURE.md) and its local-only
`readiness.py` / `test_readiness.py` complete this continuation's reviewable
offline preparation. Logical proposal/target stamp remains `20261015t140709z`,
with lineage `20261015t112841z-post-40-recovery-prerequisite`; the distinct
repository checkpoint is `20261015t143451z-s427-isolated-offline-closeout`. Full current VCS identity
is `55bee680e6901a45946dd8bf1e4a4060952bd7ea`, separate from the preserved legacy baseline.

The root's local audit authenticated the eight original 4.0 signatures and
preserved historical acceptance, but the earliest signed deadline remains
**2026-10-15T11:40:42Z**, basis `policy.preparedAt`. The actual isolated
readiness action at 14:38:15Z returned **exit 1**, `workload recovery expired or
future`, with that exact deadline, zero remote requests and authorization false.
Current provider freshness, new captures/restores and recovered access are not
claimed. No timestamp, historical receipt or failed OIDC record was replaced.

The focused isolated suite passed **19/19 tests**, no skips, including the
coherently rebound freshness case and a mutation that demonstrates omission of
non-approval record freshness is detected. It covers the declared zero-host-
mount isolation profile, retention duration from creation and both API results,
archive/plaintext digests, source/node and native-subject/binding/lineage
identity, event chronology, complete tool roles, custody and side-effect absence.
Metadata consistency remains untrusted and never authenticates or opens a gate.
The exact private bindings pin the public encryption recipient, all five
retained `jpiquot` UIDs, existing encrypted native client, source/tool hashes and
explicit unknowns. No live adapter or operational execution mode is supplied.

Unidentified concurrent edits affected the generic guard/tests/procedure and
shared packet. Those bytes and our earlier drafts/candidate snapshot remain
untouched and unaccepted; this implementation is isolated under
`eng/kubernetes-upgrade/recovery-20261015t140709z/`. New owner-only custody is
`/home/administrator/hexalith-retirement-investigation-evidence/20261015t140709z-s427-expired-recovery-preparation/closeout-20261015t143451z-4fbeb896`. Its actual local failed
rerun and reconstruction corrections, root review-diff paths and 162-file
historical-preservation snapshot are retained. The root independently reran
the final focused suite. Final checksum sealing awaits the root's final checks.

The [sanitized unsigned checkpoint](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261015t143451z-s427-isolated-offline-closeout/verification.json) binds exact files and outcomes.
Uncovered actions remain fresh 4.0 prerequisite acceptance, exact 4.1 scope and
freshness policy, source snapshot/node capture, provider-prefix permissions,
recipient decryption/independent readback, qualified image-baked tools/stdin
delivery/extraction capacity, recovered authority lineage and 4.1 signing.
The original resolved Docker executable remains group-writable and blocked;
no concurrent staged copy is accepted and no tool repair or daemon call ran.
There was no production command, capture, restore, decryption, signature, new
grant, retirement, upgrade, Rancher, Timesheets deployment or Git staging/commit.
Unchanged broad tests/fixtures were not rerun. **4.27 remains in-progress, 4.1's
operational gate closed and Timesheets 3.6 open/FAIL**; all original production
tasks, later owner gates and default-deny controls remain pending/preserved.
