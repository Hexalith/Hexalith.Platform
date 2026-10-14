# Offline retirement preparation for Timesheets staging

This continuation prepares the next operator handoff locally. Story 4.27 stays
`in-progress`, Story 4.1's live gates stay closed, and Timesheets Story 3.6 stays
open/FAIL. It authorizes no retirement, upgrade, deployment, domain command,
signing or external publication. The existing target remains `jpiquot@local`.

The input handoff was absent from this checkout when investigation began. Its
exact external source is
`/home/administrator/projects/hexalith/timesheets/references/Hexalith.Platform/eng/timesheets-deployment/staging-dependency-handoff.md`,
SHA-256 `b12758d8a528a876314e7d49b02a864530fc1a7852f40818701ee0f8f789ef38`.
It reports native reads at `2026-10-14T06:43:46Z`: server v1.34.9, missing
`hexalith-staging`/`hexalith-staging-data` namespaces and unavailable Gateway API.
These are dated input claims; this continuation performs no live observation.
Concurrent repository activity subsequently supplied the existing local
[Timesheets handoff](../timesheets-deployment/staging-dependency-handoff.md);
its history and observations are preserved, with a narrow continuation link.
The extended window ending `2026-10-31T23:00:00Z` grants no execution approval.

## Executable local preparation

Run from this Platform checkout. Choose a new preparation ID on subsequent
runs; existing IDs refuse without changing their evidence. All paths below are
explicit local inputs. The output root is separate from this repository, the
three inspected retained roots and all input directories.

```bash
python3 eng/cluster-management/prepare_retirement.py \
  --operator 'Codex (offline preparation only)' \
  --attempt-id 20261014t100941z-s427-reviewed-preparation \
  --evidence-root /home/administrator/hexalith-retirement-preparation-evidence \
  --handoff /home/administrator/projects/hexalith/timesheets/references/Hexalith.Platform/eng/timesheets-deployment/staging-dependency-handoff.md \
  --inventory /home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261004t085143z-census/inventory.json \
  --catalog /home/administrator/hexalith-management-evidence/rehearsal/20261006t074119z-s427-catalog \
  --namespaces /home/administrator/hexalith-management-evidence/rehearsal/20261006t074119z-s427-namespaces \
  --recovery-root /home/administrator/hexalith-management-evidence \
  --recovery-root /home/administrator/hexalith-recovery-evidence \
  --recovery-root /home/administrator/hexalith-upgrade-evidence
```

The stdlib preparer dispatches no command, connects to no network and signs
nothing. Its owner-only packet has directories `0700`, files `0600`, checked
`SHA256SUMS`, a complete immutable `preparation-sources.zip` and per-member
`input-lineage.json`. It validates all record serialization and final input
custody/scope before allocating output. It builds and verifies a uniquely
named `.<attempt-id>.incomplete-*` staging directory, then exclusively reserves
the final attempt directory and atomically publishes the completed packet
over only that empty reservation. Failed staging evidence is preserved;
its attempt ID cannot be reused. An incomplete staging directory or an empty
failed reservation supplies no completed preparation receipt or operational
authority. Existing final IDs are never overwritten. Existing custody ancestry
must be trusted and nonwritable by other users, except safe sticky shared
parents owned by root or the current user. The archive retains exact consumed preparation sources,
external handoff, sanitized historical inventory, manifest-covered fixture
files (including existing encrypted exports). Inspected recovery metadata
originals and their signatures remain in their existing private custody; the
packet retains only their measured path/digest/presence lineage and untrusted
claim summaries, with `archived=false`. It makes no plaintext archive copies
of signed recovery originals while approved encryption inputs remain unset.
Raw recovery payloads, credentials and native raw inventories are not read.
Metadata/signature bytes remain private; only the deliberately sanitized local
[unsigned audit receipt](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261014t100941z-s427-reviewed-preparation/verification.json)
is repository-visible. The receipt is outside the consumed source set to avoid
self-reference. No operational `plan.json` or pass-labelled proof is generated.

`historical-integrity.json` separates checksum/receipt measurements from current
qualification. It checks all 905 catalog and 98 namespace manifest files, the
846 and 85 encrypted-export stanza/digest/retained-readback records, and the real
pure `retire.validate_rehearsals` against unchanged execution/procedure bytes
and the exact October 4 historical census. It repeats neither fixtures nor
decryption, and proves no independent off-node readability or current source.
`metadata-audit.json` records all explicit inspected roots, exact selected
record basenames, paths, digests, adjacent signature presence, skipped symlinks
and untrusted claims. Signature presence is never signature validation.

The independent local audit verified all eight historical 4.0 signatures
against that bundle's retained historical recovery trust root. Its recorded
October 1 validation reports 553 matching off-node object readbacks. Neither
local signature verification nor those recorded counts establish current trust
approval, new provider readback or fresh recovery; the gate remains expired.

The preserved pre-review packet `20261014t093850z-s427-local-preparation` and
its original unsigned receipt retain their consumed bytes and dated checks.
The reviewed packet linked above captures the final corrected preparer,
31 preparation tests, review dispositions and documentation. The parent reran
all 284 cluster-management tests with no skips. The unchanged retirement
executor, fixture drivers, shared modules and procedure still bind their
original receipts; no fixture or production operation was repeated.

The sole locally found 4.0 gate remains historical `20261001t061620z`, expired
`2026-10-02T06:16:52Z`. Historical signed proofs and the 4.1
`20261001t075120z-backup-verified` point/restore/node records do not refresh it.
The `20261014t065220z-preparation` and `20261014t070550z-preparation` 4.1 packets
are unsigned, not-run placeholders. No exact-named retirement policy,
console-closure or retirement-approval record was found in the three inspected
roots. This conclusion describes that local scope only.

## Conditional operator proposals: first census, then fresh qualification

Everything in this section is proposed for separately approved scope and is
**not run by local preparation**. Even default-mode `retire.py` contacts the
native target and signs receipts; the operator must have approval for both.
Resolve every private variable from the Administrator's approved custody,
never from fixture keys, historical trust roots or this packet.

Required unresolved inputs: `APPROVED_OPERATOR`, `NATIVE_KUBECONFIG` (owner-only
static native configuration without exec/auth-provider/token-file hooks),
`ALLOWED_SIGNERS`, `ADMINISTRATOR_PRINCIPAL`, `RECEIPT_KEY`,
`ADMINISTRATOR_RECIPIENT`, distinct `READBACK_RECIPIENT` and owner-only
`READBACK_IDENTITY`, and new `OPERATIONS_EVIDENCE` outside Git, recovery and input
custody. No identity, recipient, key or approved freshness policy is selected
here. The native context does not select the signing principal.

The following absolute local tool paths were checked for presence; this is not
tool qualification or approval. The executor binds their exact SHA-256 values.

```bash
KUBECTL=/home/administrator/hexalith-upgrade-evidence/tools/v1.34.12/kubectl
HELM=/home/administrator/hexalith-upgrade-evidence/tools/helm-v3.22.0/helm
AGE=/home/administrator/hexalith-upgrade-evidence/tools/age-v1.3.2/age
SSH_KEYGEN=/usr/bin/ssh-keygen
ETCDCTL=/home/administrator/hexalith-upgrade-evidence/tools/etcd-v3.6.5/etcdctl
ETCDUTL=/home/administrator/hexalith-upgrade-evidence/tools/etcd-v3.6.5/etcdutl
KS_CHART=/home/administrator/hexalith-management-evidence/ks-core-1.2.4.tgz
```

The observed local chart SHA-256 is
`a5c87fe18477bacf9032eb9cf2968b73d73465aa3652b99eede865fe3ba7fbdd`;
`APPROVED_CHART_SHA256` still requires independent approved verification.
`QUALIFIED_LOCAL_NODE_IMAGE` must be the exact approved existing Docker
`sha256:<64 hex>` identity, with no unqualified pull. The required fixture uses
only fresh isolated credentials and sanitized census input; it accepts no
production kubeconfig, values or data. No image identity is chosen here.

For each proposed command, unset variables stop before dispatch. Use three
unused, mutually distinct IDs, such as `s427-native-census-YYYYMMDDtHHMMSSz`,
`s427-final-plan-YYYYMMDDtHHMMSSz` and `s427-execution-YYYYMMDDtHHMMSSz`, replacing
the date/time token with the actual approved IDs. Both planning attempts name
the same future execution ID; the first plan is a proposal without receipts.

```bash
: "${FIRST_PLANNING_ID:?select unused first census ID}"
: "${FINAL_PLANNING_ID:?select a different unused final planning ID}"
: "${EXECUTION_ID:?select a different unused future execution ID}"
test "$FIRST_PLANNING_ID" != "$FINAL_PLANNING_ID" && \
test "$FIRST_PLANNING_ID" != "$EXECUTION_ID" && \
test "$FINAL_PLANNING_ID" != "$EXECUTION_ID" || exit 1

native_plan_proposal() {
  python3 eng/cluster-management/retire.py \
    --attempt-id "$1" --execution-attempt-id "$EXECUTION_ID" \
    --operator "${APPROVED_OPERATOR:?approved operator required}" --context jpiquot@local \
    --kubeconfig "${NATIVE_KUBECONFIG:?approved static native configuration required}" \
    --kubectl "$KUBECTL" --helm "$HELM" --age "$AGE" --ssh-keygen "$SSH_KEYGEN" \
    --allowed-signers "${ALLOWED_SIGNERS:?approved trust root required}" \
    --administrator-principal "${ADMINISTRATOR_PRINCIPAL:?approved principal required}" \
    --receipt-key "${RECEIPT_KEY:?approved receipt signing key required}" \
    --recipient "${ADMINISTRATOR_RECIPIENT:?approved Administrator recipient required}" \
    --readback-recipient "${READBACK_RECIPIENT:?distinct approved readback recipient required}" \
    --readback-identity "${READBACK_IDENTITY:?private readback identity required}" \
    --evidence-root "${OPERATIONS_EVIDENCE:?new approved external custody required}" "${@:2}"
}

# Only after separate approval for native census and receipt signing:
native_plan_proposal "$FIRST_PLANNING_ID" || exit 1
FIRST_CENSUS="$OPERATIONS_EVIDENCE/retirement/$FIRST_PLANNING_ID/source-census.json"
```

Compare the first fresh census with the archived October 4 source: ownership,
consumers, coverage, cluster/native identity, every UID/resourceVersion, desired
content, protected namespace/storage bindings and retained authority. Any
changed scope or unknown required consumer needs a new decision. Existing
historical fixture receipts bind the old census and cannot bind these new bytes.

After separately approving isolated fixture operations and recipient custody,
run the manager-free seven-namespace fixture first, then the full catalog with
fresh-node external-etcd restore, sequentially. Select distinct unused
`NAMESPACE_FIXTURE_ID` and `CATALOG_FIXTURE_ID`. Capture/retain every failure and
stop the sequence on any nonzero outcome; no automatic retries are proposed.

```bash
python3 eng/cluster-management/rehearse_namespaces.py \
  --attempt-id "${NAMESPACE_FIXTURE_ID:?new namespace fixture ID required}" \
  --operator "$APPROVED_OPERATOR" --evidence-root "$OPERATIONS_EVIDENCE" \
  --source-inventory "$FIRST_CENSUS" \
  --production-executor /home/administrator/projects/hexalith/platform/eng/cluster-management/retire.py \
  --node-image "${QUALIFIED_LOCAL_NODE_IMAGE:?approved exact local node image required}" \
  --kubectl "$KUBECTL" --age "$AGE" \
  --recipient "$ADMINISTRATOR_RECIPIENT" --readback-recipient "$READBACK_RECIPIENT" \
  --readback-identity "$READBACK_IDENTITY" || exit 1

python3 eng/cluster-management/rehearse_catalog.py \
  --attempt-id "${CATALOG_FIXTURE_ID:?new catalog fixture ID required}" \
  --operator "$APPROVED_OPERATOR" --evidence-root "$OPERATIONS_EVIDENCE" \
  --source-inventory "$FIRST_CENSUS" \
  --production-executor /home/administrator/projects/hexalith/platform/eng/cluster-management/retire.py \
  --node-image "$QUALIFIED_LOCAL_NODE_IMAGE" --kubectl "$KUBECTL" --age "$AGE" \
  --recipient "$ADMINISTRATOR_RECIPIENT" --readback-recipient "$READBACK_RECIPIENT" \
  --readback-identity "$READBACK_IDENTITY" --ks-chart "$KS_CHART" \
  --ks-chart-sha256 "${APPROVED_CHART_SHA256:?independently verified approved chart digest required}" \
  --helm "$HELM" --rollback --etcdctl "$ETCDCTL" --etcdutl "$ETCDUTL" || exit 1
```

Require seven exact UID/resourceVersion-bound namespace PUTs, zero Namespace
DELETEs and preservation; the exact catalog cohort, all 19 guarded retirement
phases, fresh source-node destruction before isolated restore, new node/member
identities, matching baseline/CRD UIDs and canary, two-recipient export readback
and exact owned-resource cleanup. These are isolated outcomes, not production
recovery acceptance. Never restore a fixture or an old recovery point into the
running cluster. The production namespace interventions are limited to
`default`, `kube-node-lease`, `kube-public`, `kube-system`, `kubekey-system`,
`kubesphere-controls-system` and `kubesphere-system`; their distinct synthetic
equivalents stay isolated.

Only after both source-bound receipts pass, obtain a second fresh native plan:

```bash
CATALOG_RECEIPT="$OPERATIONS_EVIDENCE/rehearsal/$CATALOG_FIXTURE_ID"
NAMESPACE_RECEIPT="$OPERATIONS_EVIDENCE/rehearsal/$NAMESPACE_FIXTURE_ID"
native_plan_proposal "$FINAL_PLANNING_ID" \
  --catalog-rehearsal "$CATALOG_RECEIPT" --namespace-rehearsal "$NAMESPACE_RECEIPT" \
  --rehearsal-source-inventory "$FIRST_CENSUS" || exit 1
FINAL_PLAN="$OPERATIONS_EVIDENCE/retirement/$FINAL_PLANNING_ID/plan.json"
```

Bind exact `planSha256`, canonical action `allowlistSha256`, `codeSha256`,
`procedureSha256`, `maintenanceSha256`, `toolSha256`, `nativeKubeconfigSha256`,
`resolvedNativeConfigurationSha256`, `receiptSigningKeySha256`,
`trustRootSha256`, `administratorPrincipal`, `sourceClusterUid`,
`sourceInventorySha256`, `rehearsalSourceInventorySha256` and all rehearsal
record/manifest hashes. Any bound-byte change forces renewed fixtures, a new
final plan and new exact approval. First/final planning or successful software
checks never populate the production allowlist with authority.

The signed plan's native Lease bindings name exact API/name/namespace/UID,
`originalRenewTime`, `contentWithoutRenewTimeSha256` and complete original
content. Only valid nonfuture monotonic `spec.renewTime` may advance; every
other desired field remains fixed. It grants no mutation authority. The
unchanged exact manager Lease checkpoint after controller removal still applies.

## Fresh signed prerequisites and original signatures

Select new approved `BACKUP_BUNDLE`, `PREFLIGHT` and `EXACT_APPROVAL` private
paths. The eight `BACKUP_FILES` below each require their original adjacent
`.sig`, verified as the approved `ADMINISTRATOR_PRINCIPAL` in namespace
`hexalith-recovery` under the approved `ALLOWED_SIGNERS` root.

| Exact backup file | Required fresh evidence/bindings |
| --- | --- |
| `policy/recovery-policy.json` | Approved common `recoveryId`, per-system `maximumProofAgeSeconds`, RPO/custody policy |
| `manifest/evidence-manifest.json` | Exact immutable encrypted off-node object identities and versions, same recovery ID |
| `cleanup/restore-target-cleanup.json` | Actual passing isolated-target cleanup, same recovery ID |
| `gate/backup-gate.json` | Actual pass, `assembledAt`/`expiresAt`, exact policy/manifest hashes and proof/signature hashes |
| `keycloak/keycloak-restore-proof.json` | Final signed PostgreSQL restore and `sourceUid` for CNPG `keycloak/keycloak-postgres`, policy/manifest hashes, measured RPO within policy, freshness |
| `openbao/openbao-restore-proof.json` | Final signed OpenBao restore and `sourceUid` for `openbao/hexalith-keys`, same exact policy/manifest/RPO/freshness bindings |
| `memories/memories-restore-proof.json` | Final signed Memories restore and source UIDs for `hexalith-memories/redis-stack` and `hexalith-memories/falkordb`, same bindings |
| `validation/validation.json` | Fresh `validatedAt`, `dryRun=false`, gate/policy/manifest/input/signature hashes, nonempty all-pass checks, zero failed/skipped, positive equal `manifestObjectsReRead`/`manifestObjectsMatching` |

The approved trust root must separately allow namespace `hexalith-retirement`
for the fresh policy, seven `GATE_FILES`, exact execution approval and later
acceptance. Every gate record names the future execution `attemptId`, exact
`planSha256`, current `sourceClusterUid`, `capturedAt`, `expiresAt` and actual
`verificationResult: "pass"`. No field may be filled from this preparer's nulls.

| Exact policy/gate file | Required actual signed contents |
| --- | --- |
| `retirement-policy.json` | Approved `trustRootSha256`/`administratorPrincipal`, captured/expiry, positive integer `maximumPlanAgeSeconds`, `maximumValidationAgeSeconds`, `maximumRecoveryAgeSeconds`, each at most 86400 |
| `recovery-validation.json` | Exact complete `backupInputs`, `independentOffNodeReadback=true`, `readOnlyValidatorCredentialUsed=true` |
| `console-closure.json` | Accepted Story 4.2, `externalPathIndependent`, `publicConsoleDenied`, `publicOidcPassed` all true, independent external denial and private administration |
| `external-etcd-recovery-point.json` | Fresh production `sourceMemberId`, positive `revision`/`keyCount`, exact `keyHash`/`canarySha256`/`configurationSha256`, `snapshotIntegrityVerified`, encrypted immutable off-node version, independent readback |
| `external-etcd-isolated-restore.json` | Exact point hash, distinct target cluster/member, fenced source, isolation/member health/cleanup true, matching revision/key count/hash/canary |
| `node-recovery-bundle.json` | Exact point/configuration hashes, encrypted immutable off-node version, independent readback, source-matching configuration, preserved owner modes and tested recovery procedure; static manifests/PKI/keys/kubelet/installed binary/package state |
| `native-access.json` | Revalidated recovered unowned independent cluster-admin binding UID with native `rbac.authorization.k8s.io/v1` roleRef; direct private Administrator access, independent custody readback and independent break-glass true; outside removal and retained `jpiquot` authority |
| `pre-retirement-health.json` | Actual native API/DNS/CNI/storage/admission/public application/OIDC pass and authenticated Keycloak/OpenBao/Memories/Forgejo outcomes; detailed observations encrypted |

The following proposal verifies originals only after approved trust/principal
selection. It does not sign, refresh evidence or grant approval. The executor
must independently perform its complete binding/freshness/preflight checks.

```bash
BACKUP_FILES=(policy/recovery-policy.json manifest/evidence-manifest.json \
  cleanup/restore-target-cleanup.json gate/backup-gate.json \
  keycloak/keycloak-restore-proof.json openbao/openbao-restore-proof.json \
  memories/memories-restore-proof.json validation/validation.json)
GATE_FILES=(recovery-validation.json console-closure.json \
  external-etcd-recovery-point.json external-etcd-isolated-restore.json \
  node-recovery-bundle.json native-access.json pre-retirement-health.json)
: "${BACKUP_BUNDLE:?fresh approved 4.0 bundle required}"
: "${PREFLIGHT:?fresh approved preflight required}"
: "${EXACT_APPROVAL:?exact Administrator execution approval required}"
for name in "${BACKUP_FILES[@]}"; do
  record="$BACKUP_BUNDLE/$name"
  "$SSH_KEYGEN" -Y verify -f "$ALLOWED_SIGNERS" -I "$ADMINISTRATOR_PRINCIPAL" \
    -n hexalith-recovery -s "$record.sig" < "$record" || exit 1
done
for name in retirement-policy.json "${GATE_FILES[@]}"; do
  record="$PREFLIGHT/$name"
  "$SSH_KEYGEN" -Y verify -f "$ALLOWED_SIGNERS" -I "$ADMINISTRATOR_PRINCIPAL" \
    -n hexalith-retirement -s "$record.sig" < "$record" || exit 1
done
"$SSH_KEYGEN" -Y verify -f "$ALLOWED_SIGNERS" -I "$ADMINISTRATOR_PRINCIPAL" \
  -n hexalith-retirement -s "$EXACT_APPROVAL.sig" < "$EXACT_APPROVAL" || exit 1
```

The separately signed `production-retirement-approval` names
`approvedBy: "Administrator"`, future execution ID, source UID, exact plan and
allowlist hashes, all execution/procedure/tool/maintenance/rehearsal hashes,
complete `preflightInputs`/`backupInputs`, matching operator, outage scope,
incident/recovery owner/channel, window, stop/recovery conditions and fresh
capture/expiry. The original plan bytes are bound by this approval's
`planSha256`; editing `productionApproved` cannot authorize it. Preserve
`jpiquot` for its separately approved Story 4.28 replacement/removal. No guessed
signing command is supplied because signing authority/key/scope remain unset.

For future armed execution and postflight assessment, use the unchanged
[bound retirement handoff](RETIRE-KUBESPHERE.md#production-executor-and-exact-attempt-handoff-2026-10-05).
Stop on any unexpected deletion, finalizer conflict, controller recreation,
source/content/Lease/time drift, expiry or failed/unobserved smoke; retain failed
custody and require an accountable incident decision. Never auto-restore an
old point or mark an incomplete outcome accepted. Actual signed preservation,
native/access/health/external-denial acceptance and fresh post-retirement 4.1
recovery inputs remain required before 4.27 can complete. Historical fixtures,
timestamps, signatures or the extended window close none of these blockers.

## Later Timesheets gates remain unchanged

Accepted retirement precedes fresh post-retirement recovery and separately
qualified/approved supported Kubernetes hops, then private Rancher/access/initial
restore under 4.28 and 4.3 runner relocation before staging. Preserve historical
`MAINTENANCE.md`; any successor procedure requires exact independent approval.

| Later owner gate | Required evidence still missing |
| --- | --- |
| Platform / Builds / Timesheets release | Qualified Release image digest, owner package identities, chart OCI digest, provenance/catalog identity and retained rollback package |
| Platform infrastructure | Accepted supported target, separate application/data namespaces, Gateway/certificates, OpenBao, staging realm, Dapr trust/ACLs, durable broker, exact `timesheets-readmodels` binding/private 8081 network and qualified 4.14–4.17 / 4.22 composition |
| EventStore / identity / sibling providers | Production resource-binding issuer, real Timesheets authorization/tenant access and Projects/Works/Parties reference providers |
| Timesheets source / operator | Certified complete cross-tenant magic-link inventory and immutable cut, enforced writer/delivery quiescence, qualified migration authority and source/pending-delivery continuity/recovery |
| Timesheets / EventStore deployed acceptance | Actual subscriber delivery, shared rebuild/persisted end state and deployed magic-link critical flow bound to exact retained release and observed target |

Default-deny authorization/reference/migration providers and unavailable
inventory providers remain unchanged. Development/source qualification, census,
readiness and infrastructure approval close no release or deployed-acceptance
gate. The imported enrollment suite's 31 missing-input setup errors describe
its historical checkout; the mapped Timesheets project is now present in this
checkout. This preparation reruns no enrollment tests, initializes no nested
dependency and makes no new enrollment-test claim. No
deploy command can be fully bound while owner-qualified retained release and
approved Platform release procedure remain absent. Story 3.6 remains FAIL.
## Operational-prerequisite checkpoint — 2026-10-14

The subsequent [next operator action](NEXT-OPERATOR-ACTION.md) records the
current investigation and concrete fresh recovery bundle `20261014t112353z`.
Its first unmet prerequisite is Administrator approval of that current
recovery/capture/interruption/signing scope and protected signing unlock,
followed by a genuinely fresh 4.0 run. Standing destination/RPO/retention/
freshness/single-signer decisions are reusable; October 1's delegated signing
and 4.2's native-access waiver do not broaden current authority.

Story 4.2's console closure was accepted October 7; the absent exact retirement
`console-closure.json` is a separate fresh plan/attempt/signature requirement.
Current narrow external GET samples found console/master paths not routed,
with a 200 OIDC-discovery control and matching public DNS; independent external
vantage and authenticated OIDC were not accepted by those samples. The named
static `jpiquot@local` transport works on v1.34.9, but its retained authority
does not prove the separately recovered independent native/break-glass path.

Current-clock historical provider validation reread all 553 object versions
with matching bytes and returned FAIL: 641/648 checks pass, seven proof-age/
RPO-staleness failures, zero skips. Eleven original historical 4.0/4.1 signatures
verified locally against retained historical recovery trust. Fresh bounded
read-only Memories census observed three Redis keys, zero tenant-registry,
in-flight/workflow/reminder counts, and zero FalkorDB keys/graphs. This supports
the current D1 convention; the approved recovery run must repeat the census.
None of these reads refreshes the October 2 gate or approves capture/signing.

The existing offline preparer is invoked once with new packet ID
`20261014t112004z-s427-prerequisite-investigation`, the same explicit external
handoff/inventory/fixture/recovery-root inputs as above, and operator
`Codex (offline prerequisite preparation only)`. The new action document is
outside its unchanged fixed source list and is retained with tool/read-only
lineage in separate owner-only supplemental custody. The separate
[sanitized unsigned verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261014t112004z-s427-prerequisite-investigation/verification.json)
binds both artifacts and their actual checks. This offline packet still makes
zero remote/signing/mutation calls and leaves every operational field closed;
actual read-only observations are reported separately. No old packet/receipt,
bound execution/procedure/maintenance bytes, default-deny provider or story
acceptance is changed. 4.27 stays `in-progress`, 4.1 closed and Timesheets 3.6
`in-progress`/FAIL; later commands/gates above retain their original meaning.
## Concrete unsigned policy checkpoint — 2026-10-14T11:40:42Z

The authorized read-only/local continuation prepared an actual current policy
for RID `20261014t112353z`: retained `precheck.py` used only validator provider
reads and local output; `policy.py` then wrote the unsigned policy locally.
Neither the uploading composite `final-run-order.sh policy` nor signing,
upload, capture, quiescence or restore was run. The
[next action](NEXT-OPERATOR-ACTION.md) now binds exact policy/precheck bytes
before a separate deliberate signing action. Review and bind that prepared RID
to the already selected October 7–31 dates, this conversation as incident
channel and existing Administrator incident/recovery/outage roles; do not ask
the owner to choose those dates/roles again. Their acknowledgements do not
approve this new exact capture/signing procedure or open any technical gate.

Final packet `20261014t114108z-s427-current-policy-preparation` and its
[separate unsigned verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261014t114108z-s427-current-policy-preparation/verification.json)
retain the corrected final sources and private unsigned policy/precheck
lineage. The preceding `20261014t112004z-s427-prerequisite-investigation` packet,
supplement and receipt remain unchanged with their original dated meaning;
each ID is used once. This preparer still emits no operational plan or accepted
gate, with zero remote/signing/mutation calls. 4.27 remains `in-progress`, 4.1
closed and Timesheets 3.6 `in-progress`/FAIL.


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
