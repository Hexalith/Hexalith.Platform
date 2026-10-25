# Proposed installation and verification command plan

These shell blocks are executable, guarded **command syntax**, not resolved B01–B15 bindings or permission to run mutations. No mutation below was run. Actual approved argv, principals, credential references, target allowlists, receipt schemas, timeouts and abort/cleanup references remain null in [the ledger](decision-and-binding-ledger.json). Every path/value must come from the owner-accepted complete target; proposed files are not existing artifacts. Commands do not implement the missing FM/provider or turn refusal into qualification.

Run from the Platform owning repository. Provisioning needs an approved retained Helm release and exact-value-free manifests. No unpinned download, dependency update, guessed chart, generic administrator credential or default current kubecontext is allowed. HSM SDK/FM installation and authority bootstrap commands cannot be specified truthfully until vendor feasibility and signed artifact/serial/slot/firmware/licensing acceptance exist; **B07/B08 remain blocked**, not substituted by Kubernetes commands.

## Existing executable source checks

This is an actual local source command (fresh output paths), without deployment or hardware effects:

```bash
set -euo pipefail
custody_check_root=$(mktemp -d /tmp/party-history-source-XXXXXXXX)
dotnet build tests/Hexalith.Platform.Custody.Tests/Hexalith.Platform.Custody.Tests.csproj \
  -c Debug -m:1 --artifacts-path "$custody_check_root/artifacts" \
  -p:UseHexalithProjectReferences=true \
  -p:HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore \
  -p:NuGetAudit=false > "$custody_check_root/build.log" 2>&1
dotnet "$custody_check_root/artifacts/bin/Hexalith.Platform.Custody.Tests/debug/Hexalith.Platform.Custody.Tests.dll" \
  -class '*IdentityHistoryCustodyPolicyTests' -class '*IdentityHistoryCleanupTests' \
  -result-xml "$custody_check_root/tests.xml" > "$custody_check_root/tests.log" 2>&1
```

## Conditional environment/release installation

First verify the explicit observed cluster against the accepted witness. Use capability-specific access references supplied by the owner; this does not print their contents. Compare source/chart/value/profile/configuration/allowlist/trust digests before any server admission or installation. B01 must validate the complete rendered exact `(GVK, namespace, name)` set, namespaces/writers, no secret values, digest-pinned images and bounded ingress. Its approved executor is still missing, so stop before dry-run/install until B01 exists and returns its accepted receipt.

```bash
set -euo pipefail
: "${CUSTODY_OBSERVER_KUBECONFIG:?approved read-only access reference}"
: "${CUSTODY_CONTEXT:?approved exact context}"
: "${CUSTODY_CLUSTER_WITNESS_UID:?accepted cluster witness}"
test "$(kubectl --kubeconfig "$CUSTODY_OBSERVER_KUBECONFIG" --context "$CUSTODY_CONTEXT" \
  --request-timeout=15s get namespace kube-system -o jsonpath='{.metadata.uid}')" = "$CUSTODY_CLUSTER_WITNESS_UID"
```

The following install syntax applies separately to each approved environment layer and application release, using each layer's own kubeconfig. `CUSTODY_B01_RECEIPT` must be the cryptographically verified, current receipt bound to these exact files/target, not a string claiming approval. The approved executor must perform that verification; a shell existence/hash test alone cannot prove authority.

```bash
set -euo pipefail
: "${CUSTODY_ENV_KUBECONFIG:?approved environment-layer access reference}"
: "${CUSTODY_APP_KUBECONFIG:?approved app-only access reference}"
: "${CUSTODY_CONTEXT:?approved exact context}"
: "${CUSTODY_ENV_MANIFEST:?accepted environment manifest file}"
: "${CUSTODY_ENV_SHA256:?accepted raw file digest}"
: "${CUSTODY_CHART:?accepted retained chart file}"
: "${CUSTODY_CHART_SHA256:?accepted raw chart digest}"
: "${CUSTODY_VALUES:?accepted value-free values file}"
: "${CUSTODY_VALUES_SHA256:?accepted raw values digest}"
: "${CUSTODY_RELEASE:?accepted release name}"
: "${CUSTODY_APP_NAMESPACE:?accepted app namespace}"
: "${CUSTODY_B01_RECEIPT:?verified current B01 receipt path}"
test -f "$CUSTODY_B01_RECEIPT"
test "$(sha256sum "$CUSTODY_ENV_MANIFEST" | cut -d ' ' -f 1)" = "$CUSTODY_ENV_SHA256"
test "$(sha256sum "$CUSTODY_CHART" | cut -d ' ' -f 1)" = "$CUSTODY_CHART_SHA256"
test "$(sha256sum "$CUSTODY_VALUES" | cut -d ' ' -f 1)" = "$CUSTODY_VALUES_SHA256"
# Run only inside the accepted locked/epoch-bound B02/B03 executor after it
# authenticates B01, exact resource scope, rollout/rollback and current authority.
kubectl --kubeconfig "$CUSTODY_ENV_KUBECONFIG" --context "$CUSTODY_CONTEXT" \
  --request-timeout=15s apply --dry-run=server -f "$CUSTODY_ENV_MANIFEST"
kubectl --kubeconfig "$CUSTODY_ENV_KUBECONFIG" --context "$CUSTODY_CONTEXT" \
  --request-timeout=15s apply -f "$CUSTODY_ENV_MANIFEST"
helm --kubeconfig "$CUSTODY_APP_KUBECONFIG" --kube-context "$CUSTODY_CONTEXT" upgrade --install \
  "$CUSTODY_RELEASE" "$CUSTODY_CHART" --namespace "$CUSTODY_APP_NAMESPACE" \
  --values "$CUSTODY_VALUES" --wait --timeout 5m
```

Do not add `--create-namespace`: environment owners own namespaces. No cluster-wide application permission, wildcard release scope, credentials in arguments, HSM erasure or application user opening is implied. The complete executor must bind each kubeconfig/context to the same accepted cluster and validate workload UIDs/image IDs independently after rollout. On a partial install failure, preserve the attempt and follow the accepted rollback procedure; there is no automatic broad uninstall in this plan.

Read back each exact installed deployment under the independent observer, checking UID and actual runtime image IDs against the accepted complete target:

```bash
set -euo pipefail
: "${CUSTODY_OBSERVER_KUBECONFIG:?approved observer access reference}"
: "${CUSTODY_CONTEXT:?approved exact context}"
: "${CUSTODY_APP_NAMESPACE:?accepted namespace}"
: "${CUSTODY_DEPLOYMENT:?accepted exact deployment name}"
: "${CUSTODY_DEPLOYMENT_UID:?accepted installed deployment UID}"
test "$(kubectl --kubeconfig "$CUSTODY_OBSERVER_KUBECONFIG" --context "$CUSTODY_CONTEXT" \
  --request-timeout=15s -n "$CUSTODY_APP_NAMESPACE" get deployment "$CUSTODY_DEPLOYMENT" \
  -o jsonpath='{.metadata.uid}')" = "$CUSTODY_DEPLOYMENT_UID"
kubectl --kubeconfig "$CUSTODY_OBSERVER_KUBECONFIG" --context "$CUSTODY_CONTEXT" \
  --request-timeout=15s -n "$CUSTODY_APP_NAMESPACE" get deployment "$CUSTODY_DEPLOYMENT" \
  -o 'custom-columns=NAME:.metadata.name,UID:.metadata.uid,IMAGES:.spec.template.spec.containers[*].image'
kubectl --kubeconfig "$CUSTODY_OBSERVER_KUBECONFIG" --context "$CUSTODY_CONTEXT" \
  --request-timeout=15s -n "$CUSTODY_APP_NAMESPACE" get replicasets \
  -o 'custom-columns=NAME:.metadata.name,UID:.metadata.uid,OWNERUIDS:.metadata.ownerReferences[*].uid,REVISION:.metadata.annotations.deployment\.kubernetes\.io/revision'
kubectl --kubeconfig "$CUSTODY_OBSERVER_KUBECONFIG" --context "$CUSTODY_CONTEXT" \
  --request-timeout=15s -n "$CUSTODY_APP_NAMESPACE" get pods \
  -o 'custom-columns=NAME:.metadata.name,UID:.metadata.uid,OWNERUIDS:.metadata.ownerReferences[*].uid,PHASE:.status.phase,READY:.status.containerStatuses[*].ready,IMAGEIDS:.status.containerStatuses[*].imageID,INITIMAGEIDS:.status.initContainerStatuses[*].imageID'
```

B03/B09 must join each observed Pod owner UID to the current ReplicaSet owned by the exact accepted Deployment UID, reject old/foreign/unready/missing replicas, and compare every container and init-container runtime `imageID` to the accepted image/platform digest mapping. Template image strings are insufficient. These commands collect metadata; the approved independent comparison executor remains missing. Namespace-wide metadata requires explicit observer scope and must not be published as foreign-tenant information.

## Required executable bindings and installation exit evidence

For **each** row, owners must deliver and approve an actual argv array/cwd/tool digest, exact operator and independent observer credential refs, target/resource allowlist, bound cancellation/deadline, receipt schema, abort/cleanup and retained result. The original eleven null fields per binding are preserved. This map assigns proposed implementation responsibility, not a real principal. No row is executable as a qualifying operation today.

| Binding | Proposed implementation and required independent result |
| --- | --- |
| B01 | Platform retained-release validator: exact rendered resources/images/writers/ingress/profile/config/trust. Reject missing approvals/null anchors/extra objects/embedded secrets. No validator is fabricated here. |
| B02 | Existing locked, epoch-bound environment executor runs the approved `kubectl` syntax; receipts bind all allocated UIDs, isolation and only-writer denial probes. |
| B03 | Existing app release executor runs approved retained Helm chart; before/after image IDs and package/source targets must match both owning hosts. |
| B04 | Tenants and identity owners deliver actual enrollment commands and two-tenant lookup proof. Resource names or labels cannot enroll tenants. |
| B05 | Credential issuer/renewers deliver reference-only resolve/renew/revoke probes and missing/old-generation readiness denial. Never log secret values. |
| B06 | Runtime owner delivers scoped Dapr mTLS/enrollment/ACL, app-channel, RBAC/network isolation and negative foreign-principal probes. Bind public CA/issuer/key versions and certificate identities from installation. |
| B07 | Custody/vendor deliver signed FM installation commands for exact unused devices/partitions and physical per-unit destruction/rotation/all-generation/key-backup probes. Unsupported autonomous zeroization rejects backend selection. Do not run destructive generic HSM policy commands. |
| B08 | Independent authority owner delivers genesis/enrollment/time/epoch/fence-reissue commands and rollback/old-writer/clock-uncertainty probes outside the restore cut. |
| B09 | Independent copy observer delivers actual source/snapshot/key/copy/work/receipt reads; close C01–C16 with signed outcomes/absence. A provider Boolean is insufficient. |
| B10 | Destruction operator delivers original-unit cleanup and receipt lookup through the approved existing custody/Dapr surface; uncertain completion remains Pending and retry returns original receipt. |
| B11 | Backup operator delivers genuine pre-expiry immutable cut and wrapped-key-envelope coverage commands. No raw unit root backup is permitted; record authenticated absence and root-device continuity. |
| B12 | Recovery owner delivers exact isolated restore/reconcile/quarantine/disposal commands and positive unexpired controls, with both original root devices and current independent authority outside the restored cut. |
| B13 | Rehearsal owner delivers exact fault/lost-ack/restart commands at all five custody phases, durations, abort and verified cleanup. Scaling an unrelated rollout is insufficient. |
| B14 | Source/Parties owners deliver approved actor-free certificate producer/verifier and gap/overlap/foreign/stale/expiry tests. No new administrative endpoint is invented. |
| B15 | Delivery owners deliver a real full Live pass path and exact accepted joint source/install target guard, command/date and P-01–P-10 plus installed custody/restore/fault evidence. The current `eng/verify-ext-parties-1.ps1 -Mode Live` deliberately exits 1 even with inputs; it cannot fill B15. |

The proposed full command must guard clean exact Parties/EventStore/Platform checkouts and every installed profile/configuration/plan/trust digest **before any protected lookup or effect**. The complete target includes source, packages, images, devices/FM, environments/tenants/issuers, key hierarchy, independent authority/time, C01–C16, restore/fault scopes and approved executor/receipt bindings. It must reject drift and fail/skip/unrun lanes. Do not replace its missing pass path with an always-successful shell command, Local tests or LiveReadiness.
