---
title: '4.26: Qualify Rancher and the management migration'
type: 'chore'
epic: 4
story: 26
created: '2026-10-01'
status: 'done'
route: 'dispatch'
review_loop_iteration: 2
baseline_commit: '98436a4cb884f8d11b4b5f3e6ae70e95ce687463'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/specs/spec-kubesphere-to-rancher/SPEC.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The approved replacement lacks an ownership census, retirement rehearsal and supported target. Installation tests do not establish safe removal.

**Approach:** Execute Story 4.26 qualification: collect protected native evidence, classify management dependencies, qualify exact Rancher/K3s/tool identities and independently usable access, rehearse native retirement in isolation, and deliver reviewable retirement and management plans.

**Decision:** Evaluate the Administrator's host `192.168.1.30` first; VM capability, capacity, endpoint and cost require qualification.

**Decision (2026-10-03, Administrator):** The repository is public. Security-weakness narratives and host-access specifics go to encrypted private custody for the Administrator, and Git keeps only a neutral pointer. This covers which key or account reaches which privilege, authentication or sudo weaknesses, credential file locations, modes and validity, and the remediation status of those weaknesses. Future published projections omit them. Pushed history and committed immutable evidence attempts are not rewritten. Kubernetes RBAC facts from the census are not covered and stay public: which user or ServiceAccount holds which role, including the `jpiquot` binding finding.

**Decisions (2026-10-03, Administrator, on the open qualification items):**
- The repository stays public. The workstation key's current passphrase is accepted as set, with no rotation.
- **Readback.** An agent-held readback key is a second recipient on every new private export, and each export is decrypted and checked automatically. The Administrator key remains the custody recipient. Exports encrypted before this rely on header tags and recorded digests; Administrator readback is not required.
- **Console route.** The public `kube.hexalith.com` route is closed under Story 4.2 before retirement. Retirement then removes the Ingress, its Certificate and TLS Secret, and the manager Lease.
- **Finalizer residue.** Only the KubeSphere finalizer is removed from the seven namespaces, which are kept. The license Secrets, `Cluster/host` and the app Category stay as inert archival objects with an owner and a cleanup date.
- **Native administration.** A named one-year Administrator client certificate gets its own unowned ClusterRoleBinding and is revoked by deleting that binding. Break-glass is `admin.conf` on node1 over SSH; its off-node copy is expected inside Story 4.0's encrypted node archive, pending confirmation.
- **MFA and scopes.** MFA is enforced at Rancher through Keycloak, and native certificate access is break-glass without MFA. The Administrator holds native cluster-admin and Rancher admin. There is no deputy until one is named. Grant and revocation records are signed commits in a private, off-site-mirrored operations repository.
- **Recovery.** A fixture etcd snapshot-and-restore rollback is rehearsed now. A fresh production etcd restore through Story 4.0's route runs before 4.27. The Rancher authority restore belongs to 4.28.
- **Upgrade target.** Kubernetes 1.36.5 by way of 1.35, moving to 1.37 once Rancher supports it.

## Boundaries & Constraints

**Always:** Preserve cluster/namespace/workload/PVC identities, bindings and shared dependencies. Encrypt private exports outside Git; commit sanitized evidence. Revalidate skew, identity and currency. Preserve Administrator/deputy separation, MFA and independent grant/revocation lineage; missing authority fails closed. Distinguish observation, proposal, approval and acceptance.

**Never:** Mutate production, uninstall KubeSphere, grant roles, provision/install Rancher, upgrade Kubernetes, overwrite recovery evidence or the hashed upgrade proposal, introduce Fleet application writers, or import production credentials/data into a fixture. Namespace/CRD wildcards and blanket finalizer removal cannot constitute retirement scope. Qualification does not authorize 4.27 or open 4.1's gate.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Census | Exact native context and compatible tools | Timestamped UID/version/ownership/storage census; encrypted private exports and sanitized digest ledger | Failed discovery or inaccessible API is an explicit coverage gap |
| Ownership | Catalog entries, installed controllers, consumers and finalizers | Distinguish available extensions from installations; classify each capability and deletion effect | Unknown owner/consumer/propagation blocks retirement acceptance |
| Target | Version-specific hosting/import matrices and stable releases | Exact separate management/workload/chart/image/tool/license identities | Unsupported, prerelease or security-unqualified identity remains unaccepted |
| Rehearsal | Isolated representative inventory and native procedure | Exact proposed allowlist, preservation assertions and cleanup evidence | Source reachability, drift or unexpected deletion stops the fixture |
| Authority | Native credentials and independent grant/revocation lineage | Authorized native access, unauthorized/public denial and scoped management-role proposal | Proxy-only access or missing/conflicting lineage fails closed |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/4-26-qualify-rancher-and-the-management-migration.md` — authoritative criteria.
- `eng/cluster-management/*.md` — existing contracts; add evidence-bound outputs.
- `eng/kubernetes-upgrade/prepare.py` — reuse private immutable-attempt/projection patterns; presence is not signature verification.
- `~/hexalith-upgrade-evidence/operations/story41-*.py` — adapt capture/fixture/recovery patterns; top-level actions prohibit direct imports.
- `~/hexalith-upgrade-evidence/tools/` — requalify retained kubectl/Helm/age/etcd identities.
- `~/hexalith-recovery-evidence/4-0/20261001t061620z/tools/` — reuse accepted signature/readback verification.
- `eng/kubernetes-upgrade/MAINTENANCE.md` — preserve digest; derive successor after retirement.
- `eng/cluster-management/evidence.py` `Attempt` (`encrypt`, `finish`) — hold redacted text in a new private attempt; only ciphertext identities and a neutral pointer are published.
- Redaction recipient: the Administrator signing public key, age tag `8XlNQg`, which was the original production census recipient (see `targetRecipient` in the `20261003t063011z-recipient-reencryption` manifest). Confirm `8XlNQg` in every new header. Never use `hexalith-kube-c1` or the fixture recipient.
- Redaction sweep: every `eng/cluster-management/*.md`, the story file, this spec's non-frozen body (including Review Triage Log rows B25, B33, B40 and B41) and this story's `deferred-work.md` entries. In QUALIFICATION.md the targets are mainly the access follow-ups, web-login clarification, authorized host observations, kubeadm credential paragraphs, access-risk section and recipient observation. Keep non-sensitive facts: criterion status, required decisions, credential revocability, and versions and topology already in the census.

## Tasks & Acceptance

**Execution:**
- [x] `eng/cluster-management/qualify.py` — collect explicit-context read-only evidence: tools/runtime, chart/images/config, installed extensions, CRDs/instances, ownership/finalizers, admissions/API services, RBAC/routes/namespaces and persistent state; protect each attempt.
- [x] `eng/cluster-management/test_qualify.py` — test secret exclusion and failed discovery/ownership/pin/authority validation.
- [x] `_bmad-output/implementation-artifacts/evidence/epic-4/4-26/<attempt-id>/` — retain inventory, capability/disposition map, dated sources, current pins/licenses/tools and encrypted-export digests.
- [x] `eng/cluster-management/QUALIFICATION.md` — evidence native access/denial/custody, MFA/roles, independent authority lineage and fail-closed restoration; manufacture no grants/approvals.
- [x] `eng/cluster-management/rehearse.py` — rehearse isolated native uninstall with representative ownership/config and synthetic state; test propagation/finalizers, namespace/PVC preservation, license independence, stop/recovery and cleanup.
- [x] `eng/cluster-management/test_rehearse.py` — test source refusal, allowlist enforcement and unexpected deletion.
- [x] `eng/cluster-management/RETIRE-KUBESPHERE.md` — deliver ordered UID/resourceVersion-bound actions, allowlist/procedure digests, preservation assertions, currency/drift stops and 4.27 approval/recovery requirements.
- [x] `eng/cluster-management/RANCHER.md` and `README.md` — deliver concrete VM/OS/resource/storage/cost/private DNS/TLS/firewall/CA/backup/owner plan, scoped access and independent backup/restore tests; retain single-node/shared-host limitations and qualification → retirement → hop → Rancher → staging order.
- [x] `_bmad-output/implementation-artifacts/4-26-qualify-rancher-and-the-management-migration.md` and `sprint-status.yaml` — retain unmet criteria; complete only with mandatory evidence. Procurement may wait for 4.28.
- [x] Redaction (2026-10-03 decision) — move the matching passages from the swept files into one encrypted private attempt and replace each with a neutral pointer: attempt ID, ciphertext SHA-256 and a neutral reason such as "criterion 5 remains unmet". Leave committed evidence attempts and history unchanged.
- [x] Review loop 3 patch groups G48–G50, G52, G54 and G56–G70 — apply each as its Review loop 3 row states, with tests for code changes. G50 and G52 apply to the redacted or neutral text.
- [x] Review loop 4 patch groups G72–G74 and G76–G83 — apply each as its Review loop 4 row states, with tests for code changes.
- [x] Readback key — generate an SSH ed25519 readback key without a passphrase, outside Git and the evidence roots (for example `~/.config/hexalith/agent-readback`). Add a second-recipient option to `qualify.py` and `rehearse.py`. In `evidence.py` `Attempt.encrypt`, decrypt each new export with it, compare the result with the recorded plaintext digest, and refuse on mismatch. Publish both stanza tags and `readbackVerified`, add tests, and drop Administrator readback and the passphrase from the criterion-1 ledger.
- [x] Retirement decisions — in `rehearse.py`, give the production plan a console-route phase (Ingress, Certificate, TLS Secret, Lease) and a named namespace-finalizer phase for the seven namespaces, each rehearsed with synthetic fixture equivalents. `RETIRE-KUBESPHERE.md` lists the inert archival objects with owner and cleanup date, the Story 4.2 closure prerequisite and the production etcd restore before 4.27.
- [x] Rollback rehearsal — in a fixture, take an etcd snapshot before retirement, restore it into a fresh fixture node and verify the baseline UIDs and KubeSphere runtime. Retain the evidence.
- [x] Access and authority handoff — `QUALIFICATION.md` and `RANCHER.md` record the named certificate, break-glass path, Rancher MFA, scopes and signed-commit ledger. Confirm from Story 4.0 evidence whether the node archive holds `admin.conf` and the PKI.
- [x] Upgrade ordering — the README and retirement handoffs record the 1.36.5 target by way of 1.35. Leave `MAINTENANCE.md` untouched.

**Owner-authorized evidence follow-through (2026-10-04):**

The Administrator answered the request for new approved capability-disposition, signed-authority or recovery records: “I am the owner and only contributor, why do I need all these ceremony. I approve, make the needed evidence”. Record this direct conversation approval and the sole-operator decision as their own evidence; no additional approval request is needed for the read-only qualification and isolated synthetic tests already in scope. Existing documented capability-disposition and management-plan decisions can use this owner approval. Measurements, signatures, independent custody and actual access/restore results still require their respective evidence. Production retirement, role issuance, provisioning and upgrades remain separate executions.

- [x] `eng/cluster-management/qualify.py` and a new immutable evidence attempt — execute a fresh explicit-context native census with current script/module/tool digests and both verified recipients. Use retained private inputs without publishing credential locations or host-access specifics. Compare protected identities/bindings with the prior census, investigate the identity-less API views separately, and retain actual discovery/consumer/ownership gaps. Inspect non-preferred API discovery, current host capacity and existing independent native break-glass through read-only checks where reachable. Retain native and public denial outcomes without treating timeouts as authorization denial.
- [x] A new immutable capability/plan assessment — bind the owner's approval to the documented replacement/preservation/retirement design, classify the fresh inventory and exact consumers, and regenerate the source-bound retirement proposal. Keep empirical coverage or propagation gaps distinct from missing owner approval; preserve the executable production allowlist boundary.
- [x] A new immutable dated target review — fetch primary version-specific Rancher hosting/import support and current stable release/security/license sources; reverify exact charts, required image digests, retained tool identities and supported OS. Assess the approved 1.35 → 1.36.5 direction explicitly; do not manufacture support or authenticity when a source/check is unavailable.
- [x] `eng/cluster-management/rehearse.py` and tests if needed — close the private full-content drift-check gap using fixture-native raw content or strict reviewed resourceVersion binding, with meaningful negative cases for spec/data/rules drift. Execute the current actual-chart native retirement, seven retained-namespace interventions and fresh-node synthetic etcd snapshot/restore with current source bindings, two-recipient readback and exact cleanup. Synthetic configuration may represent observed production cohorts; no production credentials/data enter this fixture.
- [x] `eng/cluster-management/{QUALIFICATION,RETIRE-KUBESPHERE,RANCHER,README}.md`, this spec and the story criterion ledger — publish a concise current evidence assessment, record directly approved sole-owner decisions without demanding additional contributor ceremonies, and assign production-before-retirement and Rancher-before-use checks to 4.27/4.28. Accept only demonstrated 4.26 results; retain specific technical failures and deferred execution prerequisites. Keep the protected upgrade proposal and immutable historical attempts intact.

**Acceptance Criteria:**
- Given qualification results, when assessed, then all eight criteria have traceable evidence or remain incomplete; proposals/checksums alone cannot pass.
- Given the proposed retirement scope, when reviewed, then exact ownership, native uninstall, propagation, workload preservation and recovery are demonstrated without licensed KubeSphere writes or production mutation.
- Given qualified plans, when handed to 4.28, then hosting/import support, tools/licenses, capacity, private/MFA access and independent recovery/authority are evidenced.

### Review Findings

Code review of fix commit `0e602ad..dc9a6ab` (story-4.26 code and documents; evidence JSON excluded), 2026-10-04. Four layers ran: blind, edge-case, verification-gap and acceptance. 50 raw findings: 1 decision-needed, which the Administrator resolved as patch G90, 12 patch, 1 defer and 25 rejected. IDs continue from G89.

- [x] [Review][Patch] G90 Commit the uncommitted rehearsal driver (Administrator decision, 2026-10-04) — the 19-phase run binds `scriptSha256` `35d4724c…` and the seven-namespace probe binds `probeDriverSha256` `3b608412…`. Neither is tracked, and `rehearse.py` cannot build the 1 Repo / 27 Application / 90 ApplicationVersion catalog that `README.md:36` describes. Decrypt the drivers with the readback key, confirm they hold no private data, and commit them beside `rehearse.py` with a smoke test and accurate README run instructions. If either holds private data, instead document that it is driver-built and held in private custody [eng/cluster-management/README.md:36]
- [x] [Review][Patch] G91 Named-finalizer guard refuses the `foregroundDeletion` finalizer added by its own Foreground DELETE [eng/cluster-management/rehearse.py:1056]
- [x] [Review][Patch] G92 Namespace-finalizer full-content guard has no refusal test or `privateFullContentVerified` assertion [eng/cluster-management/rehearse.py:1081]
- [x] [Review][Patch] G93 The `global-role-bindings` annotation checkpoint wiring in `retire_phase` is never executed by a test [eng/cluster-management/rehearse.py:1354]
- [x] [Review][Patch] G94 Nothing tests that `retire_kubesphere` seeds or exports the private reviewed baseline, `retirement_uids` or completed phases [eng/cluster-management/rehearse.py:1429]
- [x] [Review][Patch] G95 README and runbook step 4 list three permitted controller transitions; the code declares four and omits `granted-clusters` [eng/cluster-management/RETIRE-KUBESPHERE.md:117]
- [x] [Review][Patch] G96 Required kubelet disk-eviction decision dropped from the public plan and from ledger row 4; restore a neutral required-decision line and keep the specifics private [eng/cluster-management/RANCHER.md:85]
- [x] [Review][Patch] G97 Lease and Category "two stable native reads" are taken back to back; add the users checkpoint's 2 s separation and a test [eng/cluster-management/rehearse.py:1283]
- [x] [Review][Patch] G98 `lease_renewal_transition` raises `AttributeError` on an absent Lease; use `(current or {})` like its siblings and add a test [eng/cluster-management/rehearse.py:463]
- [x] [Review][Patch] G99 Story section dated 2026-10-04 ("remain `in-progress`", 117 tests) sits under "Historical implementation through 2026-10-03" [_bmad-output/implementation-artifacts/4-26-qualify-rancher-and-the-management-migration.md:148]
- [x] [Review][Patch] G100 RETIRE-KUBESPHERE credits the census with the 482-object proposal and drops `productionApproved: false` / `sourceAtomic: false`; link `20261004t154618z-capability-assessment/production-plan.json` [eng/cluster-management/RETIRE-KUBESPHERE.md:19]
- [x] [Review][Patch] G101 Ledger row 1 drops the pending original-ciphertext/custody decision that QUALIFICATION.md:193 still records [_bmad-output/implementation-artifacts/4-26-qualify-rancher-and-the-management-migration.md:42]
- [x] [Review][Patch] G102 Ledger row 2 omits that dynamic consumer usage rests on the owner's decision and was not mechanically enumerated (`allDynamicConsumerUsageMechanicallyProven: false`) [_bmad-output/implementation-artifacts/4-26-qualify-rancher-and-the-management-migration.md:43]
- [x] [Review][Defer] G103 Artifact signature and image SBOM/scanning verification is deferred to "later deployment hardening" with no owner [eng/cluster-management/RANCHER.md:35] — deferred: the fix assigns it to another story (likely 4.28, which has no signature/SBOM/scan item); recorded in deferred-work.md.


- [x] [Review][Patch] G104 Add a routed retained-Category content/finalizer drift regression for `RepresentativeFixture.retire_phase`, refusing before a passing catalog result.
- [x] [Review][Patch] G105 Bound the Docker image-save process before reading stderr after successful import; cover normal order and timeout cleanup.

**Rejected**

- Forced checkpoint transitions (a Category without a count annotation, a Lease without `renewTime`, a grant that is never cleared): low. These stops are fail-closed on states never demonstrated, which matches the stop-for-review design, and fixing them would add branches. The sub-claims (the discarded first `refreshed` value and the 30 s deadline) are cosmetic, and the deadline can still trigger when reads are slow.
- Global-role annotation captured with a single read after settle: low. `settled_inventory` waits at least 10 s with stable UIDs first, and a later clear still stops fail-closed at the users DELETE. The fix would need a polling loop.
- `roleRef` without a name matching a User that has no annotation: low. The state is unreachable under the CRD's required `roleRef`, the result is still an error, and the fix adds a guard.
- Users-phase binding selection by name or subject: low. A non-cluster-admin IAM ClusterRoleBinding for a scoped User has not been observed, and the outcome is fail-closed.
- Category checkpoint requires the `catalog-extension` phase: low. Both the fixture and the production plan include that phase, and the outcome is fail-closed otherwise.
- `replicas`/`images` false stops from HPAs or ephemeral containers: low. `rehearse.py` is fixture-only, and its synthetic survivors have neither.
- Strict resourceVersion when there is no content baseline: low. The strictness is intended and documented, the affected objects are fresh synthetic ones, and the outcome is fail-closed.
- Quota Secret `foregroundDeletion` delaying absence: low. The first sample is taken 2 s after the DELETE, a GC-latency stop is fail-closed, and none has been observed.
- Vacuous canary equality: false. `populate` writes the fixed constant (`rehearse.py:1709`), and nothing rewrites it before capture.
- Canary readback proves only the write: false. The synthetic volume is that hostPath file, so the target readback is the restored content that G86/V34 required. The zero-replica consumer was accepted earlier (B70).
- Criterion 5 marked met on internal-only denial: false. Row 5 states the operator-workstation, no-external-vantage limit, and the 401/403 responses come from the API server's own authentication and authorization on the public route.
- Criterion 4 marked met while the VM is unqualified: false. AC4 and AC8 let provisioning and procurement follow in 4.28, row 4 lists the remaining 4.28 checks, and the owner approved the plan.
- Census "zero failed requests" while the attempt failed closed: false. All 283 requests were observed, and the 70 non-observed entries are the identity-less views that the same cell says the follow-up covers.
- Grant checkpoint and probe cleanup never reviewed: false. This review covers `dc9a6ab`, including both, and the spec records their basis.
- Approval and assessment digests differ from the committed docs: low. The sole-owner approval is directional ("I approve, make the needed evidence"), the final records necessarily come before the notes that cite them, and re-binding would be another ceremony round.
- 482 production versus 471 fixture removals never reconciled: low. Only the counts differ, in five phases (for example `release-records` 4 vs 1 and `controllers-and-services` 32 vs 25). Story 4.27 re-reviews production anyway, and a kind-level mapping would be new analysis.
- Spec triage-log heading, the stale "verification follows" note and stale `review_loop_iteration`: rejected because the fix edits the spec under review.
- G87–G89 `source_spec`: false. `source_spec` records the originating review, as every other ledger entry does, and the summaries name the EventStore host and the identity provider.
- `chore:` commit type for a behaviour change: low. It is cosmetic, and fixing it would rewrite a recorded commit.

## Implementation Notes

**Current follow-through, 2026-10-04:** the [current original-criterion ledger](4-26-qualify-rancher-and-the-management-migration.md#criterion-evidence-and-remaining-gates) supersedes the historical all-eight-incomplete assessment below. Fresh census, owner-bound 597-entry classification/proposal, dated exact support/license/security/tool review, recovered existing unowned native administration and four measured public native-proxy refusals are retained. Approved VM/authority/handoff designs satisfy their original planning scope. The [current full-content actual-chart retirement](evidence/epic-4/4-26/20261004t154617z-rehearsal/kubesphere-retirement-result.json) passes 471 removals across all 19 phases, retains 46 CRDs and protected native state, and exercises the source-matching application catalog and its actual cleanup finalizers without licensed application writes. [Fresh-node synthetic recovery](evidence/epic-4/4-26/20261004t154617z-rehearsal/rollback-restore-result.json) restores all 820 baseline UIDs and five running/Ready deployments with new etcd identities and exact cleanup. The [current seven-equivalent probe](evidence/epic-4/4-26/20261004t164501z-seven-namespace-probe/seven-namespace-result.json) passes seven native PUTs, zero Namespace DELETEs and full namespace/storage/canary preservation plus exact cleanup. All eight original 4.26 criteria are now met for their defined qualification/planning scope; future production and manager executions remain separately gated. The [final original-criterion assessment](evidence/epic-4/4-26/20261004t165344z-qualification-assessment/qualification-assessment.json) binds exact evidence hashes, all eight original wordings and the seven-namespace equivalence; final independent implementation verification follows. No production mutation is authorized by qualification.

The [independent final verification](evidence/epic-4/4-26/20261004t165516z-final-verification/verification.json) records 142 executed tests without skips, all five frozen matrix rows, resolved local links/anchors, complete tasks, unchanged frozen intent/baseline/upgrade proposal and private ciphertext readbacks. Native namespace raw comparisons and command traces independently confirm the seven retained-namespace interventions. Implementation, review corrections and workflow verification are complete. The spec is `done`; story and sprint status are `review`.

**Review corrections G90–G102, 2026-10-04:** the encrypted catalog and seven-namespace drivers decrypted to the exact recorded `35d4724c8dcc6e7fbeabcd10f686219e06c3eba70d41ec88c05b9ab1a7fbf38b` and `3b6084122836fd8febc659c02fa0b7a3f50aad9befaf1d6fa48f2cdc033a20d6` identities. Their synthetic code contains no production private data. Portable [catalog](../../eng/cluster-management/rehearse_catalog.py) and [namespace](../../eng/cluster-management/rehearse_namespaces.py) drivers now use the explicit rehearsal CLI/shared custody lifecycle, bind/encrypt their current driver bytes, and have accurate README commands. The original executed drivers stay encrypted and immutable; their historical receipts do not bind the portable revisions. G90 is included in the root integration commit alongside both drivers, their smoke tests and CLI handoff.

G91 admits only the server-added `foregroundDeletion` finalizer for the private comparison and retains it in the PUT; unrelated finalizers/content still stop. G92–G94 add actual routed refusal/export/baseline/scope/completed-phase regression coverage. G97 separates Lease and Category native read pairs by 2 seconds, and G98 refuses absent Lease identities with the expected `ValueError`. G95/G96/G99–G102 restore the fourth grant transition, neutral required disk-eviction decision, separately dated historical snapshot, correct proposal source/approval/non-atomic flags and pending custody/dynamic-consumer limits. All 152 tests pass with zero skips/failures/errors; 13 single-guard removals are detected by the 10 new tests. Scoped whitespace, 301 local links/anchors, frozen intent and the protected upgrade-proposal digest pass. These revised bytes have unit/smoke verification only; no new native census, live fixture, production mutation, grant, provisioning or upgrade ran. G103 remains separately deferred.

**Historical implementation notes through 2026-10-03:**

Execution checkboxes above record implemented artifacts and executed bounded checks. They do **not** accept the original story criteria. The original story and sprint remain `in-progress`; its [eight-criterion ledger](4-26-qualify-rancher-and-the-management-migration.md#criterion-evidence-and-remaining-gates) lists the unmet evidence. No production mutation, grant, Rancher provisioning, upgrade, recovery overwrite or application Fleet writer occurred.

- Added `evidence.py` immutable owner-only private attempts/encrypt-before-write/projection digests, explicit native `qualify.py`, source-fenced `rehearse.py`, and twenty-one meaningful failure-boundary tests. Updated the qualification, retirement, Rancher and operations handoffs.
- `20261001t173902z-census`: Kubernetes1.34.9, direct `jpiquot@local` native TLS endpoint, 2,594 unique UIDs and zero API discovery/list failures; 23 namespaces, 27 PVCs, 28 PVs, 137 CRDs, eight mutating/twelve validating admission configurations. The superseded census with ten gaps is retained. Native authorized reads and credential-free CA-verified HTTP403 are observed; public denial, effective independent custody and maintenance authority remain unverified.
- `20261001t181200z-capability-review`: native Installed console plan confirmed separately from catalog; eight concrete capability cohorts propose replacements/retirement/native preservation with exact members, consumers and deletion-effect review. No approved disposition and no production removal actions.
- Dated source/review attempts retain Ranchercommunity2.15.2 chart/server/agent, K3s1.35.9+k3s1 and native workload1.35.9 candidates, separate hosting/import matrices, licenses, release bodies/digests and advisory ranges. Current official K3s release API at17:42:22Z reports `prerelease=false`; the earlier prerelease disagreement remains recorded. No separate channel-lag acceptance requirement was added.
- `20261001t181000z-public-verification`, `20261001t183042z-dependency-review`, `20261001t183444z-plan-render` and `20261001t184142z-dependency-images`: K3s/kubectl/kubeadm/kubelet published checksums match downloads; Helm archive and extracted binary match; fresh age/etcd archives and all retained binaries match. Backup/CRD charts match the version-specific index and render; proposed private-CA/single-replica Rancher values render. Concrete backup/kuberlr/server/shell and all eight bundled K3s registry manifest digests are retained. Relevant source licenses and66 Rancher advisory ranges are recorded. Signatures, transitive image-license/security and procedure/restore acceptance remain false.
- `20261001t180843z-rehearsal`: bounded synthetic native UID/resourceVersion/child-first/named-finalizer deletion, namespace/PVC/PV UID/binding/canary preservation and isolated cleanup passed. Zero-replica workload/synthetic finalizer limitations are explicit.
- `20261001t182403z-rehearsal`: actual core1.2.4/application4.2.1 and embedded-console1.2.0 all five deployments Ready; source API/etcd/SSH/HTTPS and external egress probes blocked; fresh credentials and different fixture UID. Native installed-plan deletion completed. Core `--no-hooks --wait --timeout90s` removed deployments but timed out, so retirement is unqualified. Cleanup removed node/network/volume, but its strict parser retained lowercase Docker not-found as unverified. The spelling fix preserves daemon/API-error refusal and its negative test passes. One diagnostic follow-up captures every served KubeSphere custom resource before/after without arbitrary finalizer edits.
- `20261001t184031z-rehearsal`: the single diagnostic follow-up also failed core uninstall, with604 before-state objects,317 expected retirements and437 after-state objects.151 expected retirements remain;17 chart CRs are Terminating with their exact finalizers (WorkspaceTemplate/system-workspace, custom ServiceAccount/ks-console, User/admin, five GlobalRoleBindings, seven GlobalRoles, Repository/extensions-museum, Extension/ks-console-embed). Controller removal preceded completion of those lifecycles.166 allowlisted objects disappeared, plus one unallowlisted completed extension Job Pod whose batch Job was omitted from the captured scope. Protected namespace/PV/PVC/synthetic-deployment metadata remained intact; post-core canary/authenticated application health is unverified. Cleanup passes explicit node/network/volume absence and fresh credential/image-alias removal. No finalizer stripping occurred.
- Final current helpers add Jobs/endpoints/storage-class snapshot coverage, refuse uncensused owners, and record/check an extension-specific expected set immediately after native InstallPlan deletion, before core removal. A negative test proves the later core allowlist cannot excuse an unexpected extension-phase core deletion. These guard changes have unit coverage only; the retained actual runtime remains failed/incomplete. Source ownership is unknown and this fixture approves no orphaning/reownership.
- [Closeout verification](evidence/epic-4/4-26/20261001t185518z-closeout/verification.json) binds executed and current procedure digests, exact allowlist/runbook hashes, retained failed evidence, local checksum/encryption/mode checks and subsequent explicit historical cleanup readback. The exact executed procedure bytes are retained encrypted from the matching interim staged snapshot. Twenty-one unit tests and staged/unstaged whitespace checks pass; off-node custody/signatures/authority and all original acceptance criteria remain unverified/incomplete.
- Host effective kubeadm/external-etcd/virtualization/reservations require the still-unanswered working SSH username/key location. Independent Administrator/deputy MFA/scopes, authenticated grant/revocation lineage, off-node export decryption/signatures and quarantined recovery remain externally dependent. No missing evidence is fabricated. VM placement/cost/DNS/CA decisions are unapproved; actual procurement/provisioning can wait for4.28.
- Historical `eng/kubernetes-upgrade/MAINTENANCE.md` SHA-256 remains `834be8225ca05175e7cbf7c6d4ee35324401c196002002269223b9000a97d8b5`. A post-retirement successor requires a fresh census/recovery point and its own approval.

The frozen matrix has named passing tests:

| Frozen row | Passing tests | Executed evidence/limit |
| --- | --- | --- |
| Census | `ProjectionTests.test_secret_and_configuration_values_never_enter_projection`; `ProjectionTests.test_terminating_custom_resource_projects_timestamp_and_named_finalizer_only`; `CensusTests.test_failed_discovery_is_not_an_empty_accepted_census`; `CensusTests.test_supported_skew_and_prerelease_fail`; `CustodyTests.test_private_attempts_immutable_and_no_recovery_overwrite`; `CustodyTests.test_symlink_evidence_custody_refused` | Completed native UID census; encrypted private export/header/digest/mode checks; independent custody/host coverage still incomplete |
| Ownership | `ProjectionTests.test_ownership_storage_and_installed_catalog_distinction`; `ProjectionTests.test_unknown_owner_consumer_and_deletion_effects_block_retirement`; `RehearsalTests.test_unknown_cascade_blocks_and_child_first_allows_explicit_scope` | Installed plan distinct from catalog; concrete unapproved capability map; unknown consumers/propagation block production scope |
| Target | `PinAuthorityTests.test_unqualified_tag_hosting_security_and_tools_fail_closed`; `PinAuthorityTests.test_github_prerelease_and_stale_review_rejected` | Exact dated candidates/matrices/artifact/tool/license records; no unsupported/prerelease/security-unqualified acceptance |
| Rehearsal | `RehearsalTests.test_source_endpoint_uid_reachability_and_credentials_refused`; `RehearsalTests.test_wildcards_missing_resources_protected_namespaces_and_claims_refused`; `RehearsalTests.test_named_finalizer_only_and_drift_stop`; `RehearsalTests.test_unexpected_deletion_incomplete_deletion_or_uid_binding_change_stops`; `RehearsalTests.test_daemon_failure_does_not_prove_cleanup_or_source_refusal`; `RehearsalTests.test_extension_phase_cannot_use_later_core_scope_to_allow_unexpected_deletion` | Synthetic preservation passes; real native core uninstall failure retained; source reachability, drift, unexpected phase deletion and Docker API errors fail closed |
| Authority | `CensusTests.test_proxy_credentials_are_refused_before_discovery`; `CensusTests.test_endpoint_userinfo_query_and_fragment_cannot_disclose_credentials`; `PinAuthorityTests.test_missing_gapped_conflicting_or_expanded_authority_fails_closed`; `PinAuthorityTests.test_post_cut_revocation_survives_source_loss` | Authorized native reads/credential-free403 observed; tests exercise metadata policy only, never authenticate signatures/grants or demonstrate actual MFA/restore |

Post-review verification supersedes the interim artifact hashes above without changing any immutable attempt:

- `20261001t195141z-census`: patched read-only collector captured2,593 unique UIDs, nine all-state Helm releases and283 encrypted requests. It adds27 previously omitted KubeSphere-marked identities to unresolved ownership review, including preserved namespaces/RBAC. It finalized `failed-closed` with70 missing-identity coverage entries: three ComponentStatus objects, one node/65 pod metric views and the built-in Calico profile. The four targeted read-only requests in `20261001t195456z-native-identities` retain the actual absent UID/resourceVersion metadata. These successful views are not approved deletion identities or malformed transport responses; do not infer complete projection from request success. All80 protected namespace/PV/PVC/storage-class identities and previously projected bindings/properties match the earlier observation; newly projected properties do not establish prior-state equality.
- `20261001t195601z-rehearsal`: fresh source-fenced standalone native fixture passes actual delete/recreate stale-UID and mutate-after-snapshot stale-resourceVersion cases. Each server response is409/Conflict and current full content/UID remains unchanged. Child-first/named synthetic finalizer removal, protected storage/lifecycle assertions and canary readback pass. Cleanup explicitly proves node/network/volume/owned-image-alias absence and fresh credential removal. No actual KubeSphere runtime rerun occurred; its finalizer/phase-propagation failure remains unqualified.
- Root verification passes all45 tests across `test_qualify.py`, `test_rehearse.py`, `test_evidence.py` and `test_cleanup.py`; new tests cover full collector pagination/nonpreferred CRDs, malformed responses/allocated preflight receipts, Helm states/pages, destination symlinks, Orphan propagation, protected-property drift, mutation ownership, bounded cleanup and native request/race behavior. Every frozen matrix row retains passing named coverage. The [post-review verification](evidence/epic-4/4-26/20261001t200342z-post-review/verification.json) binds current artifact/procedure digests and limits.
- Exact standalone execution used procedure SHA-256 `6277bf2ceb290e276405780016968eb83b0a3bd1e36694f35e311262faf94f19`; current SHA-256 `a78aa7904801e589a9aa14fd0816503b3fcae5de4d128f34916f6c0aaa7f4ee0` adds only a three-line malformed-mount schema guard. Native deletion/preservation bytes are unchanged. Current-code tests and replay against retained synthetic Docker metadata pass; the full fixture was not rerun after this edit. The audit preserves both exact revisions and corrects the interpretation of an initially misnamed encrypted export without replacing it.
- All27 review findings have individual verdicts; G1–G17 corrections are implemented and verified. The implementation worker patched collector/rehearsal; root patched custody/cleanup after agreeing file ownership. G18 is [deferred](deferred-work.md) under the agent-context rule; the operations handoff restates the existing accepted4.2/4.3 staging prerequisites. No original criterion is accepted by these fixes.
- Build/story/sprint remain `in-progress`. Step05 completion/commit is pending operational acceptance: the workflow requires every acceptance criterion satisfied, while representative retirement, effective host/native maintenance access, owner decisions, authentic authority/MFA and independent custody/recovery remain unresolved. No frozen intent or recovery proof was modified.

Additional passing matrix coverage: Census — `CensusTests.test_collect_pagination_nonpreferred_crd_and_empty_discovery_coverage`, `CensusTests.test_all_helm_states_paginate_beyond_default_limit`, `CensusTests.test_malformed_successful_lists_are_coverage_failures`, `CensusTests.test_allocated_tool_and_shape_failures_finalize_closed_criteria`, all `DestinationTests`; Ownership — `ProjectionTests.test_explicit_management_keys_and_finalizers_classify_native_objects`, `ProjectionTests.test_secret_and_config_consumers_project_only_reference_identities`, `ProjectionTests.test_ingress_backends_and_gateway_listeners_preserve_sanitized_consumers`; Rehearsal — `RehearsalTests.test_orphan_propagation_retains_children_and_stops_nested_cascade`, `RehearsalTests.test_protected_storage_properties_finalizers_and_deletion_timestamps_cannot_drift`, all `FixtureBoundaryTests` and `CleanupTests`. Target/Authority tests above remain passing and their operational limitations remain unchanged.

## Spec Change Log

## Review Triage Log

### Resumed build review, 2026-10-04

All three fresh reviewers completed before classification; no layer was skipped. The full preserved-baseline diff has 50,884.096 kB, giving `min(floor(sqrt(50,884.096) + 1), 10) = 10` blind findings. It includes older unrelated platform changes. Every finding receives its own verdict before grouping. Carried rows retain the prior verdict/route without new patches or duplicate deferrals; verification-gap rows are pre-verified.

| Finding | Verdict | Evidence | Route |
| --- | --- | --- | --- |
| B77 retained raw configuration | low | carried: B67 explicitly rejected the broader raw-value preservation subclaim. The unchanged comparator checks projected workload fields and native identities/bindings; full production configuration/application checks remain 4.27 requirements. | reject (carried) |
| B78 complete CRD schemas | low | carried: B68, same unchanged UID/name/served-storage/conversion comparison. No complete schema-digest acceptance is claimed, and a new private schema policy adds guards beyond a direct correction. | reject (carried) |
| B79 incomplete authority metadata | low | The test-only metadata validator accepts incomplete event fields, but repository callers are exclusively unit tests; it never issues or reconciles live authority, and collector acceptance stays false. Actual signer/scope/time/current-head verification is explicitly required in 4.28. Extending the illustrative model introduces new semantic guards for an unobserved operational caller. | reject |
| B80 catalog source patterns | low | The portable driver constructs the explicitly reviewed, source-sized synthetic cohort and checks exact counts; it is not a generic source-pattern qualifier. Its shared summary keeps qualification and production acceptance false. The dated source graph is separately assessed; future source-pattern changes require renewed assessment before 4.27. Generalizing the fixed fixture adds guards/state beyond a direct correction. | reject |
| B81 namespace source patterns | low | The manager-free driver proves seven fixed synthetic equivalents; the separate dated qualification assessment binds the seven source pairs. The driver claims fixture-only outcomes and no qualification/production acceptance. A generic source-cohort validator adds behavior for a future unassessed source. | reject |
| B82 non-preferred non-CRD discovery | maybe-false | carried: E40/G71, same collector path. Separate served-version evidence settles the retained census; mechanically integrating future discovery remains the earlier deferred question. | defer G71 (carried) |
| B83 identity-less diagnostic entries | false | carried: E37, same explicit closed coverage. Virtual/default-profile/metrics entries cannot supply deletion UIDs; retaining them as gaps plus targeted follow-up is intentional and does not silently accept deletion objects. | reject (carried) |
| B84 Docker cleanup identity race | low | carried: B75/E50/E78, same named-resource lifecycle and absent-at-start ownership checks. No concurrent same-attempt replacement occurred; retained-ID teardown adds new guard state. | reject (carried) |
| B85 timeout partial diagnostics | low | carried: B76, same timeout path. Timeout remains an explicit failed observation, not accepted discovery; encrypting partial exception streams adds an error branch for an unobserved acceptance failure. | reject (carried) |
| B86 default package dependency build | medium | The earlier identity project defaults to published Gateway dependencies lacking its consumed identity APIs. The reviewer reproduced 26 missing-type errors; the default package/source conditions confirm this is an older unrelated platform change. | defer G106 |
| E85 release namespace collision | low | carried: E57, same name-only retirement/extension selectors. The retained census has no duplicate release names and the exact source proposal is reviewed separately; the proposed namespace guard adds behavior for an unobserved source collision. | reject (carried) |
| E86 unprojected workload configuration | low | carried: B67's broader raw-value subclaim, same comparator. Literal environment/command/resource fields are outside the stated projected preservation proof; authenticated/full production application checks remain explicit. | reject (carried) |
| E87 retained CRD schema drift | low | carried: B68, same schema-omitting comparison and bounded retained-CRD claim. A new complete desired-content policy is more than a direct correction. | reject (carried) |
| E88 image-save post-import hang | medium | `load_vendor_images` reads stderr before its timed wait after import succeeds. A local producer closed stdout while remaining alive with stderr open; EOF was unavailable, so that read can block forever and prevent failure recording/cleanup. This differs from E54's importer backpressure timeout. Waiting with the existing 30-second bound before reading is a direct correction. | patch G105 |
| E89 cleanup name replacement | low | carried: B75/E50/E78, same unchanged cleanup lifecycle and hypothetical concurrent name replacement. | reject (carried) |
| E90 actor revision range | medium | carried: E84/G88, same older `GetInt64` admission path and missing FormatException catch. | defer G88 (carried) |
| V36 retained Category regression | medium | Pre-verified: removing only the Category desired-content comparison leaves all 152 discovered tests passing. The setup smoke test never calls the representative subclass's retirement phase, allowing its preservation receipt to lose its only content/finalizer guard unnoticed. | patch G104 |
| V37 concrete gateway execution | medium | carried: V35/G89, same older concrete admission provider with only mocked/interface coverage. | defer G89 (carried) |
| V38 global-alias test stub | medium | Pre-verified: the older login test returns the same registry entry for any alias, so tenant-dependent alias derivation still passes. This separate identity test must assert actual alias lookups. | defer G107 |

Survivors have distinct causes: G104 adds the missing retained-Category refusal test; G105 moves the existing image-save timeout before stderr EOF consumption. G106 and G107 are separate pre-existing platform problems. No intent/specification loopback is needed; both patches add no public interface and cover demonstrated states.

**Resolution:** the original implementation worker fixed G104/G105 and passed 15 focused tests. The root independently ran all 155 discovered tests with zero failures, errors or skips. The Category subclass now has unchanged/spec-drift/finalizer-drift cases; stream tests prove the timed wait precedes stderr reading and a timeout terminates/reaps the producer, records failure and reaches fixture cleanup. Whitespace checks pass. The root separately reproduced the older default-package build failure with 26 missing identity/security type errors. G106/G107 are appended to the deferred ledger; carried G71/G88/G89 are not deferred again. Revised procedures remain unit/smoke verified only; historical evidence, frozen intent, original baseline and protected upgrade proposal are unchanged.


### Review, 2026-10-04

All three fresh context-free review layers reported before classification. The preserved baseline includes older unrelated platform commits; their findings are deferred separately. Blind review metadata gives a floor of ten findings. Every finding below has an individual verdict before grouping. The verification-gap findings are pre-verified; other findings were checked against callers, recorded inputs and earlier decisions.

| Finding | Verdict | Evidence | Route |
| --- | --- | --- | --- |
| B67 survivor desired fields | medium | `assert_preserved` omits already projected workload replicas, images, account and configuration/claim references; changing replicas/image with the same UID passes. Add those comparisons. The broader raw-value/namespace-label subclaims describe unclaimed generic content coverage: namespace PUTs have full private checks and actual raw preservation proof, while full production configuration checks remain explicit 4.27 requirements; adding blanket survivor policy would guard unobserved controller transitions. | patch G84; reject broader low subclaims |
| B68 complete CRD schema guard | low | The code deliberately compares CRD UID/name/served-storage versions/conversion, and records resourceVersion churn; it does not claim a complete schema digest. No schema-changing writer or changed schema occurred in the recorded fixture. A new private CRD schema policy adds guards beyond a direct correction. | reject |
| B69 restore content acceptance | medium | Restore accepts identities/runtime/etcd IDs without reading restored canary bytes. Hash-checked snapshot restoration is measured; complete live configuration/application recovery is not claimed and remains pre-4.27 work. The demonstrated synthetic volume-data gap needs a readback. | patch G86 |
| B70 zero-replica synthetic consumer | low | `populate` deliberately uses a zero-replica Deployment and direct synthetic canary; its limitation is documented. Actual KubeSphere containers run before/after recovery. A new application workload/volume test adds behavior beyond a direct correction; authenticated business health remains a separate pre-4.27 requirement. | reject |
| B71 unexplained additions | low | Additions are recorded; recreated retired keys and any residual manager runtime are refused. Permitted controller transients have separate ledgers. No unexplained persistent addition was observed in the current isolated run; introducing an addition policy guards an unshown state. | reject |
| B72 terminating finalizer drift | medium | `remove_named_finalizer` uses a fresh projection as its own allowlist, so spec/data/rules drift after DELETE or between 409 retries is not compared with private review. Bind the named intervention to the existing private raw baseline, admitting only the expected deletion timestamp/server bookkeeping. | patch G85 |
| B73 effective source port | low | Startup probes fixed native port 6443 rather than a parsed arbitrary port. The reviewed source uses 6443 and its actual API probe passed refusal; no alternative-port source occurred. Supporting/probing another configuration adds a guard. | reject |
| B74 Traefik consumers | low | carried: same location/claim and unchanged code as E20/E41. Those installed routes are unrelated to the manager, and adding route schemas was rejected as more than a direct correction for this unobserved consumer. | reject (carried) |
| B75 cleanup identity race | low | carried: same existing timestamp/name race as E50/E78; unchanged absence prechecks/creation ownership. Concurrent same-attempt roots are unobserved, and retained-ID teardown adds guard state. | reject (carried) |
| B76 timeout partial diagnostics | low | Timeout raises and leaves an explicit failed observation/cleanup receipt; partial output is not used to accept discovery, isolation or recovery. Encrypting exception partial streams adds error-path branches for an unobserved acceptance failure. The separately retained host timeout diagnostics already preserve the actual bounded outcome. | reject |
| E82 survivor workload changes | medium | The same UID can survive replicas/image changes because projected desired fields are omitted from preservation. Same root cause as B67. | patch G84 |
| E83 request limit service resolution | medium | The older EventStore host resolves raw `KestrelServerOptions`; only options-based setup is used and no raw registration exists in the host. Its nullable callback silently skips the advertised limit. This is an unrelated pre-existing platform change. | defer G87 |
| E84 actor revision numeric range | medium | The older real gateway uses `GetInt64` after finding an active actor; an out-of-range/fractional JSON number throws `FormatException`, absent from its catch filter. Controller admission invokes this before producing proof. This is an unrelated pre-existing identity change. | defer G88 |
| V34 restored canary regression gap | medium | Pre-verified: removing only the restore-canary write still passes all 126 normal tests and the current restore result. Add a fresh target byte comparison and missing/changed-content negative cases. Same missing volume readback as B69. | patch G86 |
| V35 real gateway execution gap | medium | Pre-verified: repository searches find no test executing registered `PlatformIdentityGatewayAdmission.AdmitAsync`; controller denial tests omit/mock it. Real source/revision/provenance regressions can evade the identity tests. This is the separate earlier identity workstream. | defer G89 |

Survivors grouped only by common defect: G84 = B67/E82; G85 = B72; G86 = B69/V34. G87, G88 and G89 remain distinct pre-existing platform issues. No intent or specification loopback is required; the three direct implementation corrections add no public interface and exercise demonstrated states.

**Measured execution follow-up:** the current-source [failed-closed rerun](evidence/epic-4/4-26/20261004t133748z-rehearsal/failure.json) passed five phases and then refused User content drift. [Independent exact-pair diagnosis](evidence/epic-4/4-26/20261004t140254z-cluster-grant-diagnosis/cluster-grant-diagnosis.json) matches the private reviewed baseline and completed role checkpoint: only `iam.kubesphere.io/granted-clusters` changes from `host` to an empty string after the exact matching KubeSphere ClusterRoleBinding is retired. All other desired fields match; cleanup passed. Its causal association is inferred from the matching native phase and subject/role. This adds a bounded declared revocation checkpoint before User removal; it does not relax general content-drift refusal or expand the retirement scope. The failed receipt remains immutable. Current-source rerun and final verification are required after this correction.

**Recovery follow-up:** the [next immutable attempt](evidence/epic-4/4-26/20261004t142126z-rehearsal/rollback-restore-result.json) passed all 19 retirement phases and actual restored-canary readback, then refused a single missing baseline Secret UID. [Independent diagnosis](evidence/epic-4/4-26/20261004t153421z-probe-quota-diagnosis/probe-quota-diagnosis.json) matches its name to the actual synthetic workspace-probe template UID and its private reviewed content to the original baseline. The probe left its generated quota Secret after both parents disappeared. Snapshot-cut timing versus subsequent restored-controller cleanup is unresolved. Clean only this exact synthetic residue before baseline/snapshot using native UID/resourceVersion/full-content guards and stable absence checks; preserve strict restore UID equality and source-license retention. All other UIDs/CRDs matched, five deployments ran and were Ready, and every exact cleanup passed. Current-source verification must follow this bounded fixture correction.

**Resolution:** G84–G86 are implemented and verified. The same implementation worker completed the bounded grant checkpoint and exact synthetic-probe cleanup, with 84 focused tests passing without skips. The final `rehearse.py` SHA-256 is `bb02c5e71be3c9ea781dfd9b99b892ca59ed5abdb82c26122c80f492b17dfac5`. Its [full execution](evidence/epic-4/4-26/20261004t154617z-rehearsal/kubesphere-retirement-result.json) passed 19 phases and 471 exact removals; [fresh-node restore](evidence/epic-4/4-26/20261004t154617z-rehearsal/rollback-restore-result.json) matched all 820 baseline UIDs and 46 CRD UIDs, restored the actual canary bytes, and ran five Ready deployments. The [seven-namespace execution](evidence/epic-4/4-26/20261004t164501z-seven-namespace-probe/seven-namespace-result.json) passed seven PUTs, zero Namespace DELETEs and full raw preservation. All exact cleanup receipts passed. [Independent root verification](evidence/epic-4/4-26/20261004t165516z-final-verification/verification.json) passed 142 tests with zero skips, all five frozen matrix rows, local links and evidence-integrity checks; it independently compared executed source, actual canary/namespace bytes, grant pairs and probe cleanup. G87–G89 are appended to the deferred-work ledger as separate pre-existing platform issues. All eight original criteria are met for their qualification/planning scope, without a frozen-intent change or production mutation.


All three layers reported before triage. Blind and edge reviewers were fresh context-free agents; the verification-gap layer reused the earlier source researcher because the runtime rejected fresh threads. No layer was skipped. The blind finding-floor calculation is review metadata, not a defect. Each finding below has a separate verdict before grouping; source/callers were traced for ordinary findings, and the two gap findings are pre-verified under the workflow.

| Finding | Verdict | Evidence | Route |
| --- | --- | --- | --- |
| B1 category symlink | high | `Attempt` checked only root symlinks; an owner-only category symlink can direct private writes into Git/recovery custody. | patch G1 |
| B2 management detection | medium | Classification checked label values only and omitted explicit KubeSphere label keys/finalizers, including preserved namespaces in the census. | patch G2 |
| B3 configuration consumers | medium | Pod projection reads Secret `name` instead of `secretName` and has no env/envFrom/projected/image-pull reference extraction. Consumers disappear from retirement review. | patch G3 |
| B4 route schemas | medium | Ingress service/default/path backends and Gateway listener hostnames do not use the HTTPRoute fields currently read. | patch G4 |
| B5 malformed lists | medium | Successfully parsed `{}` or non-list `items` breaks both list loops without failed coverage; non-object JSON can escape through attribute errors. | patch G5 |
| B6 tool preflight receipt | medium | Allocated attempt precedes subprocess version probes, which are outside the failure/finalization path; tool execution errors leave no published receipt. | patch G6 |
| B7 Orphan propagation | medium | Closure starts from every delete action irrespective of propagation, so an Orphan parent incorrectly requires retained children in removal scope. | patch G7 |
| B8 storage preservation | high | `assert_preserved` compares UID/binding only; projected PV reclaim policy/storage class can change without stopping retirement. | patch G8 |
| B9 early volume coverage | medium | Volumes are captured only late in startup; early failure leaves an empty list whose vacuous `all` reports verified absence. | patch G9 |
| B10 image cleanup | medium | Alias removal exits are recorded but ignored by `passed`, with no alias absence inspection. | patch G10 |
| V1 collector regression gap | medium | Neither collector test reaches pagination or non-preferred served CRD fallback; removal of either branch would retain passing tests. | patch G11 |
| V2 native DELETE regression gap | high | Helper drift tests never send native DELETE; happy-path execution does not prove that the server rejects stale UID/resourceVersion at the request boundary. | patch G12 |
| V3 malformed-list coverage | medium | Successful JSON decoding records observed coverage before malformed `items` silently truncates collection; confirmed in both loops. | patch G5 |
| E1 category symlink | high | Complete destination ancestry is unchecked, so root validation does not prevent category symlink redirection. | patch G1 |
| E2 queued protected deletion | high | A retained object can acquire `deletionTimestamp` with unchanged UID/binding and pass preservation, despite pending namespace/storage deletion. | patch G8 |
| E3 Secret volume | medium | Native Secret volumes use `secretName`; existing projection reads `name` and omits the mounted dependency. | patch G3 |
| E4 Ingress/Gateway | medium | Kind-specific route fields are not read, omitting backend/hostname dependencies. | patch G4 |
| E5 malformed-list coverage | medium | A code-zero JSON response lacking a list of objects currently stops collection with observed coverage. | patch G5 |
| E6 Helm states | medium | `helm list --all-namespaces` does not include all pending/uninstalling states without `--all`; those release exports are omitted. | patch G13 |
| E7 Helm page limit | medium | Retained Helm help confirms the default 256 limit; no pagination exists, so later release identities/exports disappear. | patch G13 |
| E8 side effect before encryption | medium | Network creation returns successfully before `run` encrypts output; encryption failure prevents the caller's ownership flag from being set and skips network cleanup. | patch G14 |
| E9 existing fixture alias | high | Generated tags are not checked for absence before tag/build, so unrelated local aliases can be overwritten and removed during cleanup. | patch G15 |
| E10 image cleanup | medium | Cleanup's success conjunction excludes image removal errors and alias readback. | patch G10 |
| E11 source capture race | medium | Inventory is read once for hashing and again for parsing; a concurrent edit can disconnect the fencing input from its recorded digest. | patch G16 |
| E12 unbounded cleanup inspection | medium | Docker absence inspections lack timeouts, preventing receipt/summary finalization if the daemon stalls. | patch G17 |
| E13 staging context gate deletion | medium | The refreshed agent context removed the explicit accepted exposure-closure/runner-relocation gate before staging; the new ordered sequence omits that condition although upstream requirements retain it. The fix edits agent context. | defer G18 |
| E14 protected-custody claim | high | Root-only symlink validation does not enforce the claim at the category destination where private attempts are created. | patch G1 |

Survivors group by exact root cause: G1=B1/E1/E14; G2=B2; G3=B3/E3; G4=B4/E4; G5=B5/V3/E5; G6=B6; G7=B7; G8=B8/E2 (incomplete preservation comparison); G9=B9; G10=B10/E10; G11=V1; G12=V2; G13=E6/E7 (incomplete Helm listing); G14=E8; G15=E9; G16=E11; G17=E12; G18=E13. G1–G17 are bounded implementation corrections/tests with no new public surface and demonstrated inputs; approved intent already settles the behavior. G18 is deferred under the workflow's agent-context rule. No intent-gap or bad-spec entry requires loopback. The original implementation worker was re-engaged with the required patch message.

Resolution: G1–G17 patched; all45 tests pass. Fresh native409/Conflict/preservation/cleanup evidence addresses G12 at the actual request boundary. Fresh census preserves missing identities as explicit closed coverage; it does not erase prior captures or accept retirement. G18 appended to deferred work; generated agent context was not edited during patching.

### Review loop 2 (2026-10-03)

Review covered the full diff since `baseline_commit`, with code and handoffs first and evidence last. All three layers ran as fresh context-free agents and none was skipped. No earlier row matched a new finding's location and claim, so none is carried. Each verdict below came from reading the cited code, the committed census or the evidence. Verification-gap findings V4–V10 arrive pre-verified.

| Finding | Verdict | Evidence | Route |
| --- | --- | --- | --- |
| B11 console route scope | medium | Census `Ingress/kubesphere-system/kubesphere-console` (kube.hexalith.com → `ks-console`), its Certificate/CertificateRequest/Order and the manager Lease are outside `retirement_scope`'s 476 objects. `post_retirement_checks` checks only webhook/APIService/CRD-conversion backends. | patch G19 |
| B12 dynamic registration census | medium | `management_resource` ignores webhook backend namespaces. `validator.license.kubesphere.io` (backend `kubesphere-system`) is in retirement scope but absent from the 596 census capability entries. | patch G20 |
| B13 review-time resourceVersion | medium | `native_retire` deletes with the freshly read resourceVersion and never compares the planned entry. Runbook step 4 promises reviewed UID/resourceVersion actions with drift stops, which the rehearsal does not exercise. | patch G21 |
| B14 published plaintext digests | medium | `Attempt.encrypt` stores `plaintextSha256` and `finish()` publishes it, so short or partly predictable exports can be confirmed offline. The 2026-10-03 manifest deliberately omits these digests. | patch G22 |
| B15 pin/authority validators | medium | `validate_authority` links declared `sha256`/`previousSha256` values without hashing event content, so an edited event keeps a passing chain. The unwired sub-claim is V12, the TypeError sub-claim is E23, and stale pin wording joins G27. | patch G29 |
| B16 fence address coverage | maybe-false | Probes reach only the source endpoint host and 1.1.1.1. Reachability of the Docker bridge/host or another source address was not observed. Settle by probing those addresses from inside a fixture. Refusal conflation is E17. | defer G46 |
| B17 hard-coded evidence | medium | `cleanup.json` always writes `unrelatedDockerObjectsChanged: false`, `failure.json` asserts "source unchanged", and `validate_isolation` checks literals set just before. These are declarations recorded as observations, contrary to the frozen observation/proposal distinction. | patch G25 |
| B18 tracked kinds | medium | `NATIVE_RESOURCES` omits leases, ingresses, network policies, cronjobs, controller revisions, PDBs, HPAs, quotas and limit ranges, so their removal cannot fail `assert_phase`. | patch G23 |
| B19 exception tuple/PyYAML | low | `yaml.YAMLError`, `tarfile.TarError`, `ImportError` or `StopIteration` skip `failure.json`/diagnostics and `main()` prints a traceback; PyYAML is undocumented. `finally` still cleans up, and broadening the except is a direct correction. | patch G24 |
| B20 rehearsal tool provenance | medium | `--kubectl` is required but unused. The copied Helm and the age, kind and docker identities are never hashed or versioned in rehearsal evidence. The image-ID cross-check sub-claim is low and unpatched. | patch G26 |
| B21 stale current statements | medium | RETIRE-KUBESPHERE.md lines 24/34 and README lines 25/29 describe superseded procedures as current. Story criterion 1 cites the pre-G5 zero-failure census. | patch G27 |
| B22 joined words/numbers | low | Handoffs contain "Ranchercommunity2.15.2", "All45" and "in4.28". Readers meet these constantly, and the fix is a direct correction. | patch G28 |
| B23 epic context deletions | medium | The rewritten `epic-4-context.md` drops the 4.3 runner, registry/profile and Traefik requirements that `epics.md` still states. The fix edits agent context. | defer G45 |
| B24 relayed approvals | low | The 2026-10-03 kubeadm and re-encryption records cite only a coordinator relay, unlike `20261001t205847z` authorization evidence. | patch G30 |
| B25 host-access risk | high | The evidence is held in private custody: `passage-13.age` of attempt [`20261003t103215z-custody-redaction`](evidence/epic-4/4-26/20261003t103215z-custody-redaction/encrypted-exports.json) (ciphertext SHA-256 `a31309d5081dd49a0016180578817775fdd425627ae8f7da56b8914f549a1773`; criterion 5 remains unmet). The docs framed it only as export custody, and nothing tracked remediation. | patch G31 |
| B26 spec/sprint metadata | low | The spec status/body mismatch would be fixed by editing this spec, so that part is rejected. `sprint-status.yaml` `last_updated` changed from `MM-DD-YYYY HH:MM` to an ISO date. | patch G34 |
| B27 unrelated changes | false | `apphost.cs`, submodule and 4.1 amendments arrived in separate user commits such as `e7aee88`. The baseline range spans them, but this story did not make them. | reject |
| B28 evidence size/index | low | The JSON size is a repository cost rather than a functional defect. The criterion ledger links current and superseded attempts, and restructuring is more than a direct correction. | reject |
| B29 kubelet eviction | medium | RANCHER.md preserves the sampled `evictionHard` with only memory/pid. A partially set map drops kubelet's nodefs/imagefs defaults, and the storage-reservation plan omits that risk. | patch G32 |
| B30 anonymous success | low | A successful credential-free request records nothing, so anonymous read is visible only as missing fields. | patch G33 |
| B31 publication tests | medium | Same untested `finish()` projection as V4. | patch G37 |
| B32 locale-dependent values | low | One immutable attempt records a French `stat` value and C-date strings. Their meaning is intact, the problem is rarely met, and re-capture is not a direct correction. | reject |
| E15 orphan inside cascade | low | The defect is real only for mixed Orphan/Foreground allowlists. Every current planner action is Foreground, and the fix adds a guard. | reject |
| E16 escaped exceptions | low | Same defect as B19. | patch G24 |
| E17 refused probe | low | A `/dev/tcp` refusal exits 1 and records `blocked`. The probed source ports are listening services, and distinguishing refusal adds branching. | reject |
| E18 fixed resource list | medium | Same defect as B18. | patch G23 |
| E19 route health gate | medium | Same root cause as B11. | patch G19 |
| E20 Traefik routes | low | IngressRoute backends are not projected, but the five census routes sit in unrelated namespaces and consumer coverage is already marked for review. A new projection branch is more than a direct correction. | reject |
| E21 StatefulSet claim templates | low | Only scaled-to-zero StatefulSets lose PVC linkage, and PVCs are protected kinds that never enter scope. | reject |
| E22 proxy environment | low | No proxy variables are set here and the recorded census path was direct. The fix adds a guard. | reject |
| E23 pin validator types | false | Malformed input raises loudly instead of passing, and no non-test caller supplies such records. | reject |
| E24 authority principals | false | Grants still require Administrator approval and verified signatures. Intent restricts only deputy scope, and an unmatched revoke cannot expand authority. | reject |
| E25 probe member finalizer | low | The passing probe recorded the member finalizer, and a `None` read fails loudly. The added guard is not a direct correction. | reject |
| E26 extension installation | low | All ten actual-chart attempts reproduced `Installed`, and `kubesphere-runtime.json` retains the flag. The proposed stop guards an unobserved state. | reject |
| E27 CRD list cache | low | `diagnostics()` can cache the custom-resource list before the retirement baseline. A one-line reset is a direct correction. | patch G35 |
| E28 foregroundDeletion race | low | A Foreground delete can transiently add `foregroundDeletion`, and the synthetic replace then sets finalizers to `[]` instead of removing only the named hold. | patch G36 |
| E29 completed manager pods | low | The gate fails closed on any manager-namespace Pod. No attempt observed such residue, and exempting completed Pods needs a projection change. | reject |
| E30 other InstallPlan | false | The production census holds only `ks-console-embed`, and another single plan would stop loudly on unexpected removal. | reject |
| E31 plaintext digest | medium | Same defect as B14. | patch G22 |
| E32 sprint timestamp | low | Same sprint-format defect as B26. | patch G34 |
| E33 Story 4.1 context bullet | medium | The diff confirms deletion of the post-upgrade requirement from agent context. The fix edits agent context. | defer G45 |
| E34 runner context bullet | medium | The diff confirms deletion of the runner relocation requirements. The fix edits agent context. | defer G45 |
| E35 registry/profile context | medium | The diff confirms deletion of the registry immutability and profile-template requirements. The fix edits agent context. | defer G45 |
| E36 recovery/license claims | low | Handoffs already state that no restore was tested. The only overclaim is this spec's task wording, which cannot be edited. | reject |
| V4 publication projection | medium | Pre-verified: copying all private files or disabling publication still passes all 60 tests. | patch G37 |
| V5 encryption refusal | medium | Pre-verified: disabling the age exit/header check passes all tests. | patch G38 |
| V6 access fields | medium | Pre-verified: forcing denial on any HTTP error or on unreachability passes all tests. | patch G39 |
| V7 failed-closed coverage | medium | Pre-verified: removing the non-observed coverage check passes all tests. | patch G40 |
| V8 retained CRD guard | medium | Pre-verified: disabling `retained-crd-changed` passes all tests. | patch G41 |
| V9 tenant-sync gate | low | Pre-verified: disabling the gate passes all tests. | patch G42 |
| V10 attempt id/custody | medium | Pre-verified: removing either refusal passes all tests. | patch G43 |
| V11 real home in test | medium | `test_private_attempts_immutable_and_no_recovery_overwrite` uses the real `Path.home()`. A regressed recovery check would create directories in the actual `~/hexalith-recovery-evidence`. | patch G44 |
| V12 uncalled validators | low | Validators are test-only policy models, and collected records already mark pins/authority unaccepted. Wiring them adds CLI surface. | reject |

Groups by shared root cause:

| Group | Members |
| --- | --- |
| G19 | B11, E19 (route consumers of retired Services) |
| G20 | B12 |
| G21 | B13 |
| G22 | B14, E31 |
| G23 | B18, E18 |
| G24 | B19, E16 |
| G25 | B17 |
| G26 | B20 |
| G27 | B21, plus the stale pin wording from B15 |
| G28 | B22 |
| G29 | B15 |
| G30 | B24 |
| G31 | B25 |
| G32 | B29 |
| G33 | B30 |
| G34 | B26, E32 |
| G35 | E27 |
| G36 | E28 |
| G37 | V4, B31 |
| G38–G44 | V5–V11, one each |
| G45 | B23, E33, E34, E35 |
| G46 | B16 |

No group needs an intent or spec change. G19–G44 are bounded code, test and handoff corrections that add no public surface, and approved intent already settles the behavior they need. G45 is deferred under the agent-context rule. G46 is deferred as unverified: medium if true, settled by probing the bridge/host and other source addresses from inside a fixture.

Resolution: G19–G44 are patched, and G45/G46 are appended to [deferred work](deferred-work.md). Two code corrections were completed during resolution:

- **G19.** Besides the stale-route health check, `dependency_plan` now refuses any out-of-scope Ingress or HTTPRoute that sends traffic to a retired Service (`route-consumer-of-retired-service-outside-scope`). On the committed census it refuses on `Ingress/kubesphere-system/kubesphere-console`. Excluding that route, it reproduces 476 objects in 17 phases.
- **G21.** `native_retire` compares each fresh read with the root's reviewed allowlist entry. A changed resourceVersion proceeds only when the sanitized reviewed projection is otherwise identical, and any other drift stops with `reviewed-entry-drift-before-native-delete`. The executed `42e8e6ef…` run carried a newer resourceVersion than planned for 13 of 178 requests, so a rerun may surface reviewed-field changes made by controllers.

All 76 tests pass without skips, and `git diff --check` is clean. Disabling any single guard from G19–G23, G25, G26, G29, G33 or G35–G43 makes at least one test fail. Final `rehearse.py` SHA-256 `1ac666c06c93ecb619e737cce198204f8a8d4644a9e1382aa3e626b2a1d6d2f9` supersedes the interim `1e723b75…` cited in the dependency-first follow-up. These bytes were later executed in the [current-procedure fixture rerun](#current-procedure-fixture-rerun-2026-10-03). No production read or mutation, grant, Rancher action or frozen-intent change occurred, and all eight criteria remain incomplete.

### Review loop 3 (2026-10-03)

The review covered the full diff since `baseline_commit`, with code and handoffs first and evidence last. All three layers ran as fresh context-free agents, and none was skipped. A finding whose location and claim match an earlier row, where the code still reads as that row describes, is marked `carried`; it keeps the earlier verdict and route and is not patched or deferred again. Verification-gap findings V13–V23 arrive pre-verified. Every other verdict comes from reading the cited code, the committed census, the local Docker state or the repository settings.

| Finding | Verdict | Evidence | Route |
| --- | --- | --- | --- |
| B33 public repository exposure | high | `gh repo view` reports `Hexalith/Hexalith.Platform` as `PUBLIC`. The 2026-10-03 commit `c3c473a` is pushed (`origin/main` = HEAD). It publishes access-weakness and credential observations, now in the same private passage (`passage-13.age`), and census inventories with Secret names, hostnames and image versions. The frozen intent says to commit sanitized evidence but does not say whether security-weakness narratives and production maps may go into a public repository. | intent_gap G47 |
| B34 historical plaintext digests | low | Pre-G22 attempts still publish `plaintextSha256`. The guessable short outputs (`whoami`, `stat`, sudo exit) are already stated in the projections and prose, so confirming them reveals nothing new. High-entropy exports cannot be brute-forced, and scrubbing immutable attempts is not a direct correction. | reject |
| B35 recipient unrecorded | medium | `Attempt.encrypt` and `finish()` record no recipient stanza, so Git readers cannot check the `8XlNQg`/`yYjuKA`/`bHCI3g` tags the handoffs cite. The 12 wrong-recipient attempts show the state occurs. Recording each export's age stanza type and tag adds no surface. Pinning an expected recipient would add a parameter, so that sub-claim is rejected. | patch G48 |
| B36 drift-guard overclaim | medium | `project_resource` excludes specs, data, rules and non-management labels and annotations. RETIRE-KUBESPHERE.md line 85 still says "an identical projection means status-only churn", and step 4 (line 55) says "stop and re-review on any difference". The code tolerates a changed resourceVersion whenever projected fields match. | patch G49 |
| B37 in-run allowlist | false | Fixture UIDs differ from production by design, so a fixture cannot bind an externally reviewed production allowlist. A 4.27 executor is excluded because the intent says qualification does not authorize 4.27. | reject |
| B38 fence address coverage | carried | Same claim as B16: probes reach only the source host and 1.1.1.1. `Fixture.start` still reads as B16 describes. maybe-false; settle by probing the bridge/host and other addresses from inside a fixture. | defer G46 (carried) |
| B39 authorization evidence | low | `givenBefore: 2026-10-03T06:28:00Z` shows the answers preceded the actions, and the relay provenance is explicit. Fixture-only reruns need no production authorization record, and the handoffs cite the approval. Recording the earlier passphrase request would need the Administrator to restate it, which is not a direct correction. | reject |
| B40 action-1 overclaim | medium | QUALIFICATION.md overclaimed completion of a remediation action and kept contradicting statements in the same section; the specifics are in the same private passage (`passage-13.age`). | patch G50 |
| B41 remediation untracked | medium | Remediation actions are listed only in prose, and the conditions behind them predate this story; the specifics are in the same private passage (`passage-13.age`). | defer G51 |
| B42 kubeadm credential revocation | medium | The criterion-5 section offers `admin.conf` without saying that a shared `kubeadm:cluster-admins` certificate cannot be revoked short of CA rotation or removing a binding kubeadm relies on. That bears on the intent's independent revocation lineage. The alternative, an unowned binding for an approved identity, is listed but not contrasted on revocability. | patch G52 |
| B43 Kubernetes 1.34 end of life | medium | Kubernetes 1.34 reaches end of life on 2026-10-27. All eight 4.26 criteria are open and 4.27 has not started, and nothing records a critical path or fallback. The schedule belongs to 4.1, which predates this story. | defer G53 |
| B44 stale handoff text | low | QUALIFICATION.md line 3 links the "criterion ledger" to the superseded `20261001t173902z-census/criteria.json`. The story says "Each correction has unit tests", which is untrue for the doc-only G27, G28, G30–G32 and G34. Each fix is a direct correction. The sub-claim about an "operator's working SSH login" sentence is false because it no longer exists. | patch G54 |
| B45 spec stale text and metadata | low | The run-together tokens, "no fixture rerun" wording and frontmatter are in this build's spec. | reject (spec edit) |
| B46 further context deletions | medium | The `epic-4-context.md` rewrite also drops off-cluster record storage, distinct staging promotion-stop state, separate-writer off-site replication and "signed" release records, beyond G45's list. The fix edits agent context. The deferred-work overlap and path-style sub-claim would modify existing entries, so it is rejected. | defer G55 |
| B47 eviction thresholds | false | RANCHER.md line 88 says the proposal reproduces the observed disk-eviction gap and requires an explicit disk-eviction decision before relying on the reservation. Choosing thresholds is that owner decision. | reject |
| B48 stale Rancher wording | low | RANCHER.md line 55 still leads with "when node1 is a guest", although node1 is observed bare metal. Line 37 pairs "split-horizon" with "no public ... record". Both are direct corrections, part of the same incomplete stale-text sweep as B44. The v2.14 link is in the spec, so that part is rejected. | patch G54 |
| B49 kind and docker binaries | low | `tool_identities` hashes only the version output of PATH `kind` and `docker`. Hashing the resolved binaries is a direct correction. | patch G56 |
| B50 vendor images by local tag | carried | Same as B20's image-ID cross-check sub-claim, recorded low and unpatched. The code is unchanged. | reject (carried) |
| B51 locale-dependent values | low | Same kind as B32, in new immutable attempts. The meaning is intact, and re-capture is not a direct correction. | reject |
| B52 unrelated changes | carried | Same as B27. `apphost.cs` and the submodule bumps are separate user commits such as `73421308`. | reject (carried) |
| E37 identity-less virtual items | false | ComponentStatus, metrics views and the built-in Calico profile cannot satisfy a UID census. Recording them as explicit closed coverage matches the frozen Census row, and targeted metadata for all 70 is retained. Accepting them is a reviewer decision, not a collector defect. | reject |
| E38 HTTPException in anonymous probe | false | Only a non-HTTP or broken TLS peer at the native API port raises `http.client.HTTPException`. That was never shown, and the failure is loud. | reject |
| E39 proxy environment | carried | Same as E22, and the code is unchanged. | reject (carried) |
| E40 non-preferred non-CRD versions | maybe-false | `collect()` lists only each group's preferred version, adding non-preferred versions only for CRDs. Whether any production group serves a resource only in a non-preferred version is unobserved. Medium if true; settle by comparing per-version discovery on production. | defer G71 |
| E41 Traefik routes | carried | Same as E20: the five census IngressRoutes sit in unrelated namespaces. | reject (carried) |
| E42 dotted Helm release name | false | None of the nine census releases contains a dot, and an unshown dotted name fails loudly on `invalid-export-name`. | reject |
| E43 pin validator types | carried | Same as E23. | reject (carried) |
| E44 naive `checkedAt` | low | Subtracting a naive datetime raises before the `tzinfo` check, so the guard never runs and the error code reads `missing-pin-review-date`. It still fails closed, and the reorder is a direct correction. | patch G68 |
| E45 malformed authority event | false | A non-dict event or non-string digest raises loudly, as E23 recorded, and no non-test caller supplies such records. | reject |
| E46 revoke without grant | carried | Same as E24. | reject (carried) |
| E47 published attempt collision | low | Collision needs the same timestamped attempt ID and category under a different evidence root. A pre-check adds a guard. | reject |
| E48 ancestor symlink race | low | Needs a concurrent local attacker with write access to the custody path. An `openat` rewrite is not a direct correction. | reject |
| E49 unsanitized operator | low | `rehearse.py` records `args.operator` raw, while `qualify.py` applies `safe()`. Importing and applying `safe` is a direct correction. | patch G69 |
| E50 network/cluster creation race | low | Names derive from the unique attempt ID and are checked absent just before. A concurrent same-named creation is unlikely, and the fix adds ID tracking. | reject |
| E51 base tag sole reference | false | The retained node image `sha256:8a9be59e…` also carries `hexalith-s426-node:v1.34.9` and survived ten runs, so removing the base alias only untags. | reject |
| E52 refused probe | carried | Same as E17. | reject (carried) |
| E53 hard-coded isolation endpoint | carried | Same as B17's "`validate_isolation` checks literals set just before"; the endpoint literal is unchanged. | patch G25 (carried; not re-patched) |
| E54 undrained save stderr | low | `docker save` writes little to stderr, and a stall ends in a loud timeout. | reject |
| E55 probe observe timeout | carried | Same as E25. | reject (carried) |
| E56 synthetic replace race | low | A changed resourceVersion makes the replace fail loudly with 409. A retry loop adds logic. | reject |
| E57 release-name namespace | low | The census has no duplicate release names across namespaces, and the plan is reviewed exactly. The namespace condition adds a guard. | reject |
| E58 fixed resource list | low | G23 added the observed missing kinds. Discovery-driven listing is more than a direct correction, and the fixture holds no other cascade targets. | reject |
| E59 repeatable attempt ID | false | The sentence before the command says to supply a new attempt ID each time, and refusing reuse is intended. | reject |
| E60 reviewed-entry drift claim | medium | Same root cause as B36: changes to spec, data or RBAC rules after review pass the projection comparison, while the handoffs promise drift stops. | patch G49 |
| E61 procedure digest scope | medium | `rehearse.py` imports `evidence` and `qualify`, including `project_resource`, but `attempt.json` binds only `scriptSha256`. Recording those module digests adds no surface. | patch G70 |
| V13 fixture isolation gate | medium | Pre-verified: deleting `validate_isolation`, the attached-network check or the IP-literal guard in `start()` passes all 76 tests. | patch G57 |
| V14 synthetic `execute()` assertions | medium | Pre-verified: removing the canary check, the final `assert_preserved`, the hold check or per-action revalidation passes all tests. | patch G58 |
| V15 final phase and settle gates | medium | Pre-verified: removing the final `assert_phase`, the settle/`wait_absent` checks or the `did-not-settle` raises passes all tests. | patch G59 |
| V16 health-gate branches | medium | Pre-verified: removing the stale-APIService line or the new-namespace finalizer term passes all tests. | patch G60 |
| V17 census native-path refusals | medium | Pre-verified: removing the TLS/proxy/CA refusal, the skew check or the single-cluster check passes all tests. | patch G61 |
| V18 census raw encryption | medium | Pre-verified: making `Capture.command` skip `encrypt` passes all tests. | patch G62 |
| V19 CRD instance-discovery gaps | medium | Pre-verified: removing either CRD coverage append passes all tests. | patch G63 |
| V20 `native_read` mapping | medium | Pre-verified: an unconditional `return None` passes all tests. | patch G64 |
| V21 cleanup pass criteria | medium | Pre-verified: dropping `networkAbsent`, dropping `freshCredentialFileAbsent` or removing the `failed-cleanup` state passes all tests. | patch G65 |
| V22 chart digest and readiness | medium | Pre-verified: removing the chart-digest or readiness refusal passes all tests. | patch G66 |
| V23 upgrade-evidence root | medium | Pre-verified: removing `hexalith-upgrade-evidence` from the refused roots passes all tests. | patch G67 |
| V24 protected-propagation guard | false | Propagation into protected kinds is still stopped by the earlier per-action and closure checks, so the redundant guard causes no missing protection. | reject |
| V25 literal success records | false | The records are written only after their guards. The real gap is untested guards, which V13–V15 cover. | reject |

Groups: G47=B33; G48=B35; G49=B36, E60; G50=B40; G51=B41; G52=B42; G53=B43; G54=B44, B48; G55=B46; G56=B49; G57–G67=V13–V23, one each; G68=E44; G69=E49; G70=E61; G71=E40.

Routing: G47 is an intent gap, which triggers a loopback under the cascade, so the patch and defer entries wait for its resolution. `review_loop_iteration` is incremented. The pushed public history cannot be undone by reverting the local tree, and the commits are the user's own, so no revert or remote change was made. The intent gap goes to the Administrator for a decision.

Resolution: the Administrator's 2026-10-03 decision settles G47. The redaction and G48–G50, G52, G54 and G56–G70 are applied, and G51, G53, G55 and G71 are appended to [deferred work](deferred-work.md), G51 as a neutral pointer.

- **Redaction.** 41 fragments from QUALIFICATION.md, README.md, RANCHER.md, the story and this spec's non-frozen body, including rows B25, B33, B40 and B41, moved verbatim into 15 encrypted passages of the private attempt [`20261003t103215z-custody-redaction`](evidence/epic-4/4-26/20261003t103215z-custody-redaction/redaction-index.json). The fragments cover which key or account reaches which privilege, authentication and sudo weaknesses, credential file locations, modes and validity, and remediation status. Each passage is encrypted with the retained age 1.3.2 to the Administrator signing key. All 15 headers carry `-> ssh-ed25519 8XlNQg`, and the published `encrypted-exports.json` records that stanza for each export. The private directory is 0700 with 0600 files, both `SHA256SUMS` files verify, and no plaintext digest is published. Every moved passage is replaced by a pointer that names the attempt, export and ciphertext SHA-256 with a neutral reason. Row B25's title is also neutralized; its substance is in passages P07 and P13. RETIRE-KUBESPHERE.md and this story's existing deferred-work entries held no matching passage. Committed evidence attempts and Git history are unchanged. The later 2026-10-03 decision relies on recorded tags/digests for these older exports; Administrator readback is not required. New exports use the agent readback recipient.
- **G50 and G52.** G50 is applied to the private copy of the access assessment: the passage carries a corrected text in which the passphrase step does not complete the remediation action. G52 is applied to the neutral text: QUALIFICATION.md contrasts the shared kubeadm group certificate, revocable only by CA rotation or by deleting the binding kubeadm relies on, with an unowned per-identity binding, revocable by deleting that binding.
- **Frozen decision in code.** Following "future published projections omit them", `qualify.py` writes the native kubeconfig file-mode observation only to the encrypted export `native-credential-custody`; the published `access.json` names the export.
- **G54 note.** QUALIFICATION.md still held the "operator's working SSH login" sentence that row B44 called absent; it is corrected with the other stale text.
- **Verification.** 97 tests pass without skips, and `git diff --check` is clean. A scratch mutation run removed each guard that V13–V23 named, plus the new G48, G56, G68–G70 and custody guards, one at a time: all 39 removals fail at least one test. Final SHA-256: `rehearse.py` `4862637dc063d06742392e21c98262d16127b4a1b055c56621ecb1299adf98a5`, `qualify.py` `86a7d65c8248f8cda197f7656712a1d45413cd54bba9fdef6c56de98c97ca17a`, `evidence.py` `96d25efa03cdeb59f865712b148243799d5ad813a828000c02a972e998a59024`. These bytes have unit verification only, with no census or fixture rerun. No production read or mutation, grant, Rancher action, recovery overwrite or change to `MAINTENANCE.md` occurred. All eight criteria and story/sprint remain `in-progress`.

### Review loop 4 (2026-10-03)

The review covered the full diff since `baseline_commit` after the redaction and the loop 3 corrections. All three layers ran as fresh context-free agents, and none was skipped. The carry rule is the same as in loop 3. Verification-gap findings V26–V33 arrive pre-verified.

| Finding | Verdict | Evidence | Route |
| --- | --- | --- | --- |
| B53 unrecorded intent amendment | low | The story's loop 3 section says no "frozen-intent change occurred", but the 2026-10-03 decision was added to the frozen block. Rewording the story is a direct correction. The Spec Change Log sub-claim would edit this spec, so it is rejected. | patch G72 |
| B54 spec frontmatter | low | Editing it would edit this build's spec. | reject (spec edit) |
| B55 stale spec body text | carried | Same claim as B45. | reject (carried) |
| B56 published credential path | low | QUALIFICATION.md line 49 still shows the actual native context and kubeconfig file path in the example command. The decision explicitly covers credential file locations. Placeholders are a direct correction. | patch G73 |
| B57 residual account-to-privilege text | intent gap | Residual certificate-metadata details are held in private custody: `residual-certificate-metadata.age` of attempt [20261003t150903z-residual-custody](evidence/epic-4/4-26/20261003t150903z-residual-custody/encrypted-exports.json) (ciphertext SHA-256 `95d5e7d5a3cd600cdee5d9787f14bba54f9eb4e4a055e75fdd816d128a69eff2`; criterion 5 remains unmet). The decision covers "which key or account reaches which privilege", while the approved Code Map keeps facts "already in the census". The census itself publishes these bindings. | intent_gap G75 |
| B58 census RBAC projections | intent gap | `project_resource` publishes every RoleBinding and ClusterRoleBinding `roleRef` and `subjects` into `inventory.json`. Read literally, the decision's "future published projections omit them" covers account-to-privilege mappings, yet the frozen Census and Ownership rows rely on RBAC projection. Only the Administrator can settle which reading applies. | intent_gap G75 |
| B59 anonymous access fields | low | `access.json` would publish `anonymousReadAllowed: true` only if credential-free reads succeeded. The observed result is 401/403, kubeadm defaults deny `system:anonymous`, and the operator reviews a census before committing it. Withholding the field conditionally adds a branch for an unobserved state. | reject |
| B60 recipient not enforced | carried | Same claim as B35's rejected pinning sub-claim. | reject (carried) |
| B61 census procedure binding | medium | `qualify.py` `collect()` writes no script or module digests, because G70 covered only `rehearse.py`. The `20261003t073627z` rerun handoffs do not say that its imported-module bytes are unbound. Recording the digests and adding that disclosure are direct corrections. | patch G74 |
| B62 terminated manager pods | false | `post_retirement_checks` exempts only Succeeded or Failed Pods, which run nothing. The stricter per-phase gate is the fail-closed behavior E29 recorded. | reject |
| B63 remediation untracked | carried | Same claim as B41. | defer G51 (carried) |
| B64 per-fragment redaction index | low | The published index belongs to an immutable attempt. The pushed history already gives a fragment-level map through `git diff c3c473a`, and adding a private per-fragment index is more than a direct correction. | reject |
| B65 Kubernetes 1.34 end of life | carried | Same claim as B43. | defer G53 (carried) |
| B66 context deletions | carried | Same claim as B23 and B46. | defer G45/G55 (carried) |
| E62 proxy environment | carried | Same as E22. | reject (carried) |
| E63 refused probe | carried | Same as E17. | reject (carried) |
| E64 multi-owner dependents | false | Over-approximating the closure only demands more explicit members, or ends in a loud wait timeout. It never deletes an unreviewed object, and no in-scope multi-owner dependent was shown. | reject |
| E65 per-phase manager Pod gate | carried | Same as E29. | reject (carried) |
| E66 later-phase path discovery | false | Roots come from the same fixture's inventory and discovered kinds. An unshown missing kind fails loudly. | reject |
| E67 retained CRD conversion to a retired Service | low | The final health gate checks retained CRD conversion backends, and the current-procedure rerun found none stale. Planning earlier adds logic. | reject |
| E68 transient errors while polling | low | A transient read error fails the attempt loudly and stays fail-closed. A retry adds branches. | reject |
| E69 ServiceAccount reference domain | low | A mismatched token-Secret expectation stops on a wait or an unexpected deletion. The rerun completed with exact removals. | reject |
| E70 probe member finalizer | carried | Same as E25. | reject (carried) |
| E71 null `checkedAt` | carried | Same class as E23: malformed test-only input raises loudly. | reject (carried) |
| E72 null validator fields | carried | Same as E23. | reject (carried) |
| E73 revoke without grant | carried | Same as E24. | reject (carried) |
| E74 published attempt collision | carried | Same as E47. | reject (carried) |
| E75 partial publication directory | low | Needs a write failure partway through publication. A staged rename is more than a direct correction. | reject |
| E76 system ancestor symlink | false | This host's custody path has no symlinked ancestor, and an unshown symlinked host fails loudly. | reject |
| E77 ancestor symlink race | carried | Same as E48. | reject (carried) |
| E78 network/cluster creation race | carried | Same as E50. | reject (carried) |
| E79 build cache residue | low | Build cache is not a named fixture object that cleanup claims to remove. Pruning adds logic. | reject |
| E80 census RBAC claim | intent gap | Same root cause as B58. | intent_gap G75 |
| E81 anonymous-read claim | low | Same as B59. | reject |
| V26 projected Helm annotations | medium | Pre-verified: dropping the `helmRelease` projection passes all 97 tests. Replaying the committed census shrinks the refused 476-object scope to an accepted 38-object plan. | patch G76 |
| V27 fixture custom-resource listing | medium | Pre-verified: omitting the served KubeSphere custom resources passes all tests. Replaying the rerun baseline shrinks 347 objects in 16 phases to 62 in 5. | patch G77 |
| V28 health gate and probe invocation | medium | Pre-verified: removing the `post_retirement_checks` or probe call still records a pass. | patch G78 |
| V29 probe outcomes | medium | Pre-verified: misclassifying a deleted or terminating member namespace passes all tests. | patch G79 |
| V30 fixture command exit check | medium | Pre-verified: removing the refusal of disallowed exit codes passes all tests. | patch G80 |
| V31 owner resolution record | medium | Pre-verified: a constant `ownersResolved`, an empty dependent list or a missing empty-inventory error passes all tests. | patch G81 |
| V32 management classification terms | medium | Pre-verified: removing the CRD-group or Helm release-namespace term passes all tests. On the committed census that drops 41 CRDs and one binding. | patch G82 |
| V33 census coverage branches and Helm exports | medium | Pre-verified: removing the malformed owner, finalizer and namespace, preferred-version or Helm-entry gap branches, or skipping KubeSphere `helm get` exports, passes all tests. | patch G83 |

Groups: G72=B53; G73=B56; G74=B61; G75=B57, B58, E80; G76–G83=V26–V33, one each.

Routing: G75 is an intent gap, so the patch entries wait for its resolution and survive the loopback. `review_loop_iteration` is incremented. No revert was made, because the verified code changes do not depend on how G75 is resolved and the user has not asked to discard them.

## Design Notes

Prefer a separate VM on existing safe capacity. If node1 is a guest, use a sibling VM on its hypervisor. A different physical host reduces correlated outages but adds cost/connectivity work. Neither provides HA. Propose 4 vCPU/16 GiB RAM/80 GiB SSD; CPU/RAM follows Rancher's [small-tier guidance](https://ranchermanager.docs.rancher.com/v2.14/getting-started/installation-and-upgrade/installation-requirements/), disk is estimated. Current authenticated observations report node1 as bare metal with 32 logical CPUs/~126 GiB RAM and existing AMD-V/KVM/QEMU/libvirt. Use the [concrete reservation proposal](../../eng/cluster-management/RANCHER.md#observed-host-and-reservation-proposal) to assess contention and additional host headroom; safe guest allocation, disk performance and owner decisions remain unaccepted. No allocation/procurement is approved.

## Verification

- `python3 -m unittest discover -s eng/cluster-management -p 'test_*.py'` — meaningful evidence/isolation failure cases pass.
- `git diff --check` — clean whitespace.
- Inspect digests, custody, isolation/cleanup, criterion evidence and preserved identities; retain failures/limitations.

## Current standalone procedure follow-up

The [20261001t204952z-rehearsal result](evidence/epic-4/4-26/20261001t204952z-rehearsal/native-result.json) executes the exact current `rehearse.py` SHA-256 `a78aa7904801e589a9aa14fd0816503b3fcae5de4d128f34916f6c0aaa7f4ee0`, including the malformed-mount guard added after the previous run. All six source/external refusal probes, real stale-UID/resourceVersion409/Conflict and unchanged-content assertions, child-first/named synthetic finalizer deletion, ten-before/eight-after exact preservation and canary checks pass. [Cleanup](evidence/epic-4/4-26/20261001t204952z-rehearsal/cleanup.json) explicitly proves node/network/owned-volume/image-alias and fresh-credential absence. Its 102 encrypted exports use the existing **synthetic fixture recipient**; its filename/comment establishes no Administrator custody, independent decryption or off-node acceptance. No production credentials/data were imported.

[Current verification](evidence/epic-4/4-26/20261001t205700z-current-procedure/verification.json) binds exact encrypted executed procedure bytes, the current artifacts, unit/local integrity checks and unaccepted limits. Historical attempts and frozen intent remain intact. Only the standalone current-byte execution limitation is superseded: actual KubeSphere runtime/uninstall was not rerun; finalizers, phase propagation, representative configuration/authenticated workload/recovery, effective host/access, MFA/authority and independent custody remain unresolved. All eight criteria and story/sprint remain `in-progress`; no gate opens.

## User-authorized access follow-up

The user approved creation of evidence. [Conversation authorization](evidence/epic-4/4-26/20261001t205847z-native-access/authorization.json) is recorded without manufacturing authenticated grants, MFA or acceptance. [Native authority observations](evidence/epic-4/4-26/20261001t210315z-native-authority/native-access.json) verify the unchanged direct TLS cluster UID, authenticated native access, private credential-free HTTP 403 and invalid-bearer HTTP 401. The principal's sampled permissions and the SSH inputs are held in private custody: `passage-14.age` of attempt [`20261003t103215z-custody-redaction`](evidence/epic-4/4-26/20261003t103215z-custody-redaction/encrypted-exports.json) (ciphertext SHA-256 `b47f8e24efb4754db4e1cdec6bccc559342786218742a1bec123d11d2d2018a4`; criterion 5 remains unmet). These checks changed no persistent resource or role and establish no distinct authenticated unauthorized/public denial, current signed lineage or independent custody.

Host-access trials from 2026-10-01 are in the same private passage (`passage-14.age`). Host facts/capacity remained unavailable at that point. Sensitive command output uses the retained production census recipient, encrypted outside Git, with off-node decryption/signatures unverified. Frozen intent, historical evidence, procedure code and story/sprint statuses remain unchanged.

[Follow-up verification](evidence/epic-4/4-26/20261001t211014z-qualification-followup/verification.json) binds the current handoff after these additions, local evidence integrity, actual decrypted synthetic unit-test output and all five frozen matrix rows. The full baseline diff is retained in a unique system-temporary file; structured evidence was parsed and the current handoff diff inspected. All 45 executed tests passed without skips. This is implementation verification; operational acceptance remains incomplete and Build stays at Step 3.

## Resumed implementation verification

On 2026-10-02, bounded fixture-schema corrections reject non-object or non-string source identities before allocation and route malformed successful Docker/native responses through sanitized failure receipts, diagnostic refusal and cleanup/summary finalization. `FixtureBoundaryTests.test_malformed_source_identity_refused_before_fixture_allocation`, `FixtureBoundaryTests.test_malformed_successful_docker_image_schema_finalizes_closed` and `FixtureBoundaryTests.test_malformed_native_diagnostics_cannot_interrupt_failure_receipt_or_cleanup` pass with all prior cases: 48 executed tests without skips. `git diff --check` passes.

The procedure SHA-256 changes from `a78aa7904801e589a9aa14fd0816503b3fcae5de4d128f34916f6c0aaa7f4ee0` to `9f84f36382efecec137f2dad936421c12e48e6afbbb956330470e12df169716b`. The new revision has unit verification only; no full fixture rerun followed it. The historical standalone attempt proves only its recorded prior procedure bytes, and the failed actual KubeSphere retirement/finalizer/phase-propagation evidence remains unchanged. No native deletion/preservation semantics, frozen intent, recovery evidence or hashed upgrade proposal changed. All eight original criteria and story/sprint remain `in-progress`; operational acceptance keeps Build at Step 3.

[Resume verification](evidence/epic-4/4-26/20261002t051917z-resume-verification/verification.json) records the root's independent 48-case run without skips, passing coverage for all five frozen matrix rows, inspection of the resumed diff, and local integrity of 31 prior projection attempts and 1,539 encrypted exports. It binds the current procedure and handoffs; local checks establish no independent decryption, signatures, authority or operational acceptance. The pending inputs are a working SSH identity and references to existing Administrator-approved capability/authority records; no response or approval is inferred.

Host-account clarifications from 2026-10-02 are in the same private passage (`passage-14.age`). They accept no MFA, role, custody or retirement criterion and do not alter historical evidence.

The [20261002t054119z targeted native read](evidence/epic-4/4-26/20261002t054119z-native-node-read/native-node-observation.json) independently confirms the same cluster UID, node `node1` reporting Ubuntu 24.04.3 LTS/kubelet v1.34.9/32 CPUs/~126 GiB RAM, and kubeadm configuration with one external-etcd endpoint. Raw outputs are encrypted outside Git. This uses the existing native credential and verifies no host account, effective kubeadm/etcd binary, virtualization, reserved capacity, MFA or independent custody. No persistent source resource or role changed; all eight criteria remain incomplete.

Further host-account SSH trials on 2026-10-02 are in the same private passage (`passage-14.age`). No account/key/role grant or production change occurred, and all original criteria remain incomplete.

An operator-supplied host-login report followed; it is in the same private passage (`passage-14.age`) and supersedes only the unknown operator host-account input; effective host/virtualization/capacity facts, approved management authority/MFA and independent custody/retirement acceptance remain incomplete.

## Independently authenticated host follow-up

Authorized read-only host access then became available; its specifics are in the same private passage (`passage-14.age`). OS/native/web identities are not automatically mapped to approved management roles.

[Host facts](evidence/epic-4/4-26/20261002t072401z-host-facts/host-observation.json) and [service/process bindings](evidence/epic-4/4-26/20261002t072631z-host-services/host-services.json) establish installed kubeadm/running kubelet1.34.9, running host containerd2.3.3 and external-etcd3.6.5 with exact binary digests. A second container-scoped containerd has different bytes despite the same version. [Native datastore reads](evidence/epic-4/4-26/20261002t072944z-native-datastore-host/datastore-host-observation.json) confirm client/peer certificate authentication, one current member and no sampled status errors/alarms. They take no snapshot, perform no health-write probe and test no restore.

Node1 reports bare metal, AMD-V/KVM, installed QEMU8.2.2/libvirt10.0.0, ~125.66GiB total/~113.84GiB available RAM and ~791.16GiB available root space. Libvirt is inactive with listening activation sockets, so live daemon queries were omitted; no running QEMU or offline system guest definition was observed. [Current node budget](evidence/epic-4/4-26/20261002t073151z-node-resource-budget/node-resource-budget.json) revalidates source/node UIDs and Ready/no-pressure conditions, with allocatable31.6CPUs/~118.89GiB, requests12.3CPUs/20,702MiB and limits49.7CPUs/80,484MiB. The [reviewable reservation proposal](../../eng/cluster-management/RANCHER.md#observed-host-and-reservation-proposal) adds4500m/18GiB/100GiB for the guest and estimated overhead, subject to measured contention/owner approval and separately scoped implementation. Host SSH/binary/topology facts are now observed; safe reservations, disk health/IOPS, private endpoint/cost/owners, approved capabilities/authority/MFA, target qualification, representative retirement and independent custody/recovery remain unresolved.

All raw outputs are encrypted outside Git before writes, with only allowlisted projections published. Agent commands were read-only and started no services. Historical failures, current procedure bytes, frozen intent, protected recovery/upgrade artifacts and story/sprint statuses remain intact. Build remains at Step3 because original operational criteria are incomplete.

## Dependency-first retirement follow-up

On 2026-10-02 `rehearse.py` replaced its no-hooks Helm path with the runbook's recommended dependency-first native retirement:

- An exact scope is planned into 17 ordered child-first phases before any request is sent.
- Every root gets a fresh UID/resourceVersion-bound DELETE with Foreground propagation.
- KubeSphere controllers complete their lifecycles before the manager is removed.
- Reconciled registrations, remaining release objects and release records follow controller removal.
- `system-workspace` objects receive one named finalizer intervention each.
- Each phase is compared exactly after settling, and a post-retirement native health gate runs at the end.

`qualify.py` additionally projects a Secret's service-account reference and a CRD's conversion service. 60 tests pass without skips.

Nine isolated attempts executed. [`20261002t121114z-rehearsal`](evidence/epic-4/4-26/20261002t121114z-rehearsal/kubesphere-retirement-result.json) passed with procedure SHA-256 `42e8e6efc64ed6e383509ca461556d566ccf836473bbad208e6031ed47580b7c`, the bytes current at execution. Review loop 2 changed `rehearse.py` to SHA-256 `1e723b7579f5bddc8c7e6fa5dac21952ae75cb8243971e4115cfa657fba6d92a`; those patched bytes have unit verification only, with no fixture rerun. It recorded 660 baseline objects and 347 exact removals in 16 executed phases, with zero unexpected/incomplete/recreated objects. Protected namespaces/PV/PVC/StorageClasses and 43 CRDs were unchanged, post-retirement health was clean and cleanup was verified. Each earlier stop is retained with its correction: kind timeout, label-linked RoleBindings, user kubeconfig/token Secrets, the probe wait, an own-gate defect, the stale license webhook, missing tenant sync and the manager recreating that webhook. The synthetic probe shows that controller-processed workspace deletion unbinds member namespaces (label and finalizer removed) without deleting them.

Analysis of committed evidence only:

- The census-derived production plan proposes 476 objects in 17 phases and includes an unrehearsed application-store phase.
- License Secrets, `User/jpiquot`, its GlobalRoleBinding, `Cluster/host`, an app Category and 7 namespaces would retain inert KubeSphere finalizers.
- The only `jpiquot` cluster-admin binding is ownerReferenced by `GlobalRoleBinding/jpiquot-platform-admin`, so native authorization is not KubeSphere-independent.
- 12 qualification attempts from `20261002t051917z` through `20261002t074902z` used a non-Administrator recipient; the custody finding is held in private custody: `passage-15.age` of attempt [`20261003t103215z-custody-redaction`](evidence/epic-4/4-26/20261003t103215z-custody-redaction/encrypted-exports.json) (ciphertext SHA-256 `c82e112058320bf6bfad7553eb02a8e11b40524316dfdcfe5798a9cf1e816403`; criteria 1 and 5 remain incomplete).

These findings need Administrator decisions. No production mutation, grant, Rancher action, recovery overwrite or hashed upgrade-proposal change occurred, and frozen intent is unchanged. The [receipt](evidence/epic-4/4-26/20261002t125604z-dependency-first-verification/verification.json) binds the executed and current procedure digests. All eight criteria and story/sprint remain `in-progress`; Build stays at Step 3.

## Administrator-approved custody follow-ups

On 2026-10-03, two Administrator-approved follow-ups ran.

- **Native admin credential (A).** A read-only host check observed a kubeadm administrator credential; its local certificate metadata is held in private custody: `residual-certificate-metadata.age` of attempt [20261003t150903z-residual-custody](evidence/epic-4/4-26/20261003t150903z-residual-custody/encrypted-exports.json) (ciphertext SHA-256 `95d5e7d5a3cd600cdee5d9787f14bba54f9eb4e4a055e75fdd816d128a69eff2`; criterion 5 remains unmet). The census shows the `kubeadm:cluster-admins` and `cluster-admin` (`system:masters`) bindings have no owners, so this path does not depend on KubeSphere. Its location, custody and validity, and the diagnosis of the expiry tooling, are in the same private passage (`passage-15.age`).
  - The credential was not used, copied or changed.
  - Criterion 5 stays unmet until the Administrator decides the holder and encrypted off-node copy and working use is proven.
- **Re-encryption (B).** The 33 private exports of the 12 attempts `20261002t051917z` to `20261002t074902z` were stream-re-encrypted to the Administrator recipient (`8XlNQg`) without writing plaintext to disk ([manifest](evidence/epic-4/4-26/20261003t063011z-recipient-reencryption/reencryption-manifest.json)). File counts match, every new header carries `8XlNQg`, every plaintext matched its original record, and the originals are unchanged.
  - Administrator readback, the decision on the original ciphertexts and the remaining custody actions in the same private passage (`passage-15.age`) are pending.

Frozen intent, `rehearse.py`, `MAINTENANCE.md` and sprint status are unchanged. No commit was made.

## Current-procedure fixture rerun (2026-10-03)

The Administrator approved one rerun of the dependency-first rehearsal in an isolated kind fixture with the current procedure.

- **Inputs.** The procedure SHA-256 `1ac666c06c93ecb619e737cce198204f8a8d4644a9e1382aa3e626b2a1d6d2f9` was confirmed before the start and recorded as executed. Every other input was copied from the recorded `20261002t121114z` invocation:
  - the committed sanitized census `20261001t195141z` (`84c44e0a…`);
  - node image `sha256:8a9be59e…`;
  - public chart `a5c87fe1…`;
  - retained kubectl `90b7b905…`, Helm `7a319dee…` and age `eb7dd1b5…`;
  - the existing synthetic fixture recipient (tag `bHCI3g`).

  No production credential or data entered the fixture, and nothing outside the new fixture was mutated.
- **Outcome.** [`20261003t073627z-rehearsal`](evidence/epic-4/4-26/20261003t073627z-rehearsal/kubesphere-retirement-result.json) **passed**, and no code change was needed.
  - Source and egress refusal: 6/6 probes blocked.
  - Native stale-UID and stale-resourceVersion requests returned 409/Conflict.
  - All five deployments were Ready, the InstallPlan reproduced `Installed` and tenant sync took 3 s.
  - The baseline has 667 objects, of which 347 were removed exactly in 16 executed phases. Allowlist SHA-256 is `76b32cf39e60ff05ce407c844e90e281acfe3134628f3b528c8ffa7970b3b22c`.
- **Reviewed-entry binding.** There were 179 root actions: 1 `absent-before-request` (`ExtensionVersion/ks-console-embed-1.2.0`) and 178 accepted DELETEs with no conflict retries.
  - 165 DELETEs carried their reviewed resourceVersion.
  - 13 saw a changed resourceVersion, and all 13 passed the reviewed-projection comparison, so no reviewed field had drifted. They are `User/admin` 1345→2704, `Extension/ks-console-embed` 1657→2613, `Repository/extensions-museum` 1760→3397 and ten extension Categories (`ai-machine-learning`, `computing`, `database`, `deepseek`, `dev-tools`, `integration-delivery`, `networking`, `observability`, `security`, `storage`).
- **Route refusal.** The route-consumer check did not fire, because the fixture has no out-of-scope route. Its refusal path has unit verification only.
- **Preservation.**
  - There were zero unexpected, incomplete, recreated or transient removals.
  - The 13 protected identities, the synthetic canary and all 43 CRDs with their resourceVersions are unchanged.
  - The post-retirement gate found no stale admission, APIService, CRD-conversion or route reference and no manager runtime, and the new-namespace lifecycle was clean.
  - The only finalizer interventions were the two named `system-workspace` removals.
  - 27 KubeSphere-marked objects remain: the earlier 26 plus the newly inventoried manager Lease.
- **Cleanup and custody.**
  - [Cleanup](evidence/epic-4/4-26/20261003t073627z-rehearsal/cleanup.json) passed: node, network, owned volume, both image aliases and the fresh kubeconfig are absent, and `kind delete` exited 0.
  - An independent readback found no kind cluster or `s426` container or network.
  - All 639 exports are encrypted to the synthetic fixture recipient, with 0700/0600 modes, and both `SHA256SUMS` files verify. The published export record omits plaintext digests.

76 unit tests pass and `git diff --check` is clean. Frozen intent, `MAINTENANCE.md` (`834be822…`), recovery evidence and sprint status are unchanged, and nothing was committed. This is fixture evidence only. Source ownership and consumer decisions, the production-only cohorts, production values, authenticated workload and recovery evidence and the other criteria's gates remain. All eight criteria and story/sprint remain `in-progress`.

## Review loop 4 implementation and decision follow-through

G72 acknowledges the approved frozen-intent amendment in the story. G73 replaces the actual native context/kubeconfig example with placeholders. G74 adds collector script/imported-module digests and discloses that the `20261003t073627z` fixture's imported modules were unbound. G75 is resolved by the Administrator's public census-RBAC exception; residual local certificate metadata is encrypted at `residual-certificate-metadata.age` of attempt [20261003t150903z-residual-custody](evidence/epic-4/4-26/20261003t150903z-residual-custody/encrypted-exports.json) (ciphertext SHA-256 `95d5e7d5a3cd600cdee5d9787f14bba54f9eb4e4a055e75fdd816d128a69eff2`; criterion 5 remains unmet). G76–G83 have regression cases for every routed branch; [worker verification](evidence/epic-4/4-26/20261003t150550z-loop4-verification/verification.json) records 117 executed tests without skips and all 17 targeted mutations detected.

The [current vendor-chart retirement](evidence/epic-4/4-26/20261003t135807z-rehearsal/kubesphere-retirement-result.json) passed 353 exact removals, 46 retained CRDs, the exact console chain/Lease and six native namespace metadata-finalizer interventions. Its extra synthetic namespaces had already been unbound by the controller; the [separate seven-namespace probe](evidence/epic-4/4-26/20261003t145655z-seven-namespace-probe/seven-namespace-result.json) closes that coverage with seven UID/resourceVersion-bound PUTs, zero Namespace DELETEs, full labels/spec-finalizers/unrelated-finalizers retained, preserved storage and equal canary content. Both fixtures cleaned up exactly. [Fixture snapshot/restore](evidence/epic-4/4-26/20261003t135807z-rehearsal/rollback-restore-result.json) destroyed the original node and restored all 714 baseline object UIDs/CRDs and five actually running/Ready deployments on a fresh fenced node with new etcd identities, hash checking and revision bump/compaction.

The full run binds `rehearse.py` `72c97602…`, `qualify.py` `5e1e2646…`, `evidence.py` `94b4e2d0…`. Final `evidence.py` `41ce544a…` additionally refuses direct-library export without automatic readback, and ran in the seven-namespace probe. [Exact executed source bytes](evidence/epic-4/4-26/20261003t142400z-executed-procedure-binding/procedure-binding.json) and the failed tenant-sync attempt remain immutable. New private exports include both Administrator/readback tags and automatically decrypt/digest-check before writes; older exports need no Administrator readback under the approved decision.

The [archive confirmation](evidence/epic-4/4-26/20261003t134235z-recovery-archive-confirmation/archive-confirmation.json) locates the recorded administrator kubeconfig/PKI in the **complementary Story 4.1 node archive**. Story 4.0's manifest has no node-archive reference. Recorded historical recovery/readback is confirmed without new archive decryption or production access; a fresh production etcd restore through 4.0 is still required before 4.27. The [retirement proposal](evidence/epic-4/4-26/20261003t135922z-decided-retirement-plan/production-plan.json) has 482 deletion objects, seven namespace interventions and 19 phases, retaining all 70 old coverage gaps; it is source-unrevalidated and unapproved. Administrator owns inert license Secrets/Cluster/app Category; 2026-11-03 is an unapproved proposed cleanup review date, with no automatic deletion.

The four runbooks and criterion ledger now record named one-year certificate/unowned-binding revocation, sealed native host break-glass, Rancher Keycloak MFA, Administrator scopes/no deputy, private off-site-mirrored signed-commit lineage and **1.35 → 1.36.5** ordering (1.37 after Rancher support). Actual issuance/access/denial/MFA/lineage and 4.28 authority restore remain unexecuted. Original operational criteria remain incomplete, so Build stays at Step 3; task completion is implementation evidence only. Frozen intent, original baseline, signed recovery proofs and the hashed `MAINTENANCE.md` remain unchanged; no production mutation, grant, install, upgrade, commit or push occurred.

## Root verification after the sequential handoff

The root independently ran all **117 tests** with zero skips/failures/errors and inspected the current implementation and runbook/story/spec diff. The complete diff since the preserved baseline, including untracked and concurrent workspace changes, was captured with a temporary Git index; the real index was untouched. All 572 changed historical/current evidence JSON files in that snapshot parsed. This is implementation verification, not a new full formal review of every historical baseline change. All 958 new exports through residual redaction independently passed both recipient tags, ciphertext/ledger checksums, custody modes and actual decryption/plaintext-digest comparison. The separate root receipt adds two automatically verified exports.

[Root verification](evidence/epic-4/4-26/20261003t152422z-root-verification/verification.json) binds code/test/runbook/story digests, independent test output, scoped whitespace and evidence/link checks. All 237 local links/anchors resolve. Story-scoped whitespace and the untracked test file pass; repository-wide whitespace reports CRLF in concurrent unrelated .NET/apphost edits, left untouched. Frozen intent, original baseline, sprint status and `MAINTENANCE.md` (`834be8225ca05175e7cbf7c6d4ee35324401c196002002269223b9000a97d8b5`) are preserved.

| Frozen matrix row | Root-run covering tests | Result |
| --- | --- | --- |
| Census | `test_secret_and_configuration_values_never_enter_projection`, `test_failed_discovery_is_not_an_empty_accepted_census`, `test_private_attempts_immutable_and_no_recovery_overwrite` | Executed and passed, no skips |
| Ownership | `test_ownership_storage_and_installed_catalog_distinction`, `test_unknown_owner_consumer_and_deletion_effects_block_retirement`, `test_unknown_cascade_blocks_and_child_first_allows_explicit_scope` | Executed and passed, no skips |
| Target | `test_unqualified_tag_hosting_security_and_tools_fail_closed`, `test_github_prerelease_and_stale_review_rejected` | Executed and passed, no skips |
| Rehearsal | `test_source_endpoint_uid_reachability_and_credentials_refused`, `test_named_finalizer_only_and_drift_stop`, `test_unexpected_deletion_incomplete_deletion_or_uid_binding_change_stops` | Executed and passed, no skips |
| Authority | `test_proxy_credentials_are_refused_before_discovery`, `test_missing_gapped_conflicting_or_expanded_authority_fails_closed`, `test_post_cut_revocation_survives_source_loss` | Executed and passed, no skips |

All implementation tasks are checked. All eight original operational criteria remain incomplete as the criterion ledger records; no production actions or authority were inferred from fixture success. Build remains at Step 3 under its requirement to satisfy every acceptance criterion before the completion review. No commit or push was made.

## Resumed build completion, 2026-10-04

G90–G102 and G104/G105 are complete. The root independently passed all 155 tests with zero skips/failures/errors, all five frozen matrix rows, all 303 local links/anchors and whitespace checks. The audit of 111 existing published attempts covered 1,029 checksum-matching, parseable JSON records; no immutable evidence changed. Three fresh review layers completed and all 19 findings have individual triage rows. New unrelated platform issues G106/G107 are deferred; earlier carried deferrals are not repeated. The spec is done; original story and sprint are review. Current portable drivers and revised procedure have unit/smoke coverage only, with no new live fixture or production action. Frozen intent, the full original baseline and MAINTENANCE.md digest are preserved.
