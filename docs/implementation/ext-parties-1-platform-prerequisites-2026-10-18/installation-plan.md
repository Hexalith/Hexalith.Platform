# Parties installation plan pending Platform decisions

Installation is blocked. This plan fixes the resource categories, dependency order, accountable roles and validation interfaces without selecting unknown physical targets or granting authority. `decision-packet.json` is the binding form; every execution-scope field remains `null`. No mutation command may use the observed `jpiquot@local` access merely because it is available.

## Resource scope to bind

Each staging and production installation needs an owner-approved, closed allowlist of `(cluster UID, environment instance, API group/version, kind, namespace, name)` tuples and release/profile/configuration digests. Record created UIDs at install and compare them thereafter. No wildcard namespace, cluster-wide application privilege or incidental resource discovery enlarges that allowlist.

| Resource set | Required exact objects and boundaries | Decision and accountable role |
| --- | --- | --- |
| Cluster prerequisite | Qualified workload-cluster identity/version; accepted recovery/access/retirement/management records; separate environment and application deployment identities | Administrator and Platform cluster owner; 4.0–4.3, 4.26–4.28 |
| Environment layer | Application and separate data namespace, StorageClass/PVs/PVCs, quotas/limits/PriorityClass, node encryption custody, restricted Pod Security and enforced default-deny/allow policies | Platform environment-layer owner; 4.14/6.7 |
| Shared runtime | Environment-specific Dapr trust/configuration/ACLs, sidecar capabilities, scoped components/subscriptions and qualified durable broker | Platform runtime owner and EventStore; 4.9/4.16/4.17 |
| Secrets and key custody | Environment OpenBao and separate tenant-key custody class; app policies/roles/mounts and pod token references; actor-purpose hierarchy and destruction mechanism only after Q1/Q2 approval | Platform secret/key owners and Administrator custodians; 4.15/8.6/T2 |
| Public entry | Exact Gateway/HTTPRoute/Service/listener, certificate and per-environment issuer, DNS ownership and allowed route namespaces; only EventStore gateway public for this seam | Platform ingress owner and Administrator DNS operator; 4.18/6.7 |
| Identity and tenancy | Realm/client/source/actor-registry enrollment, tenant registry deployment/issuer, exact provisioner/writer/reader/observer scope, two approved tenant references | Platform identity, Tenants and Parties owners; 4.8/4.19/6.11 |
| Existing application hosts | Digest-pinned Parties and composed EventStore hosts; Tenants/Memories and declared dependencies; approved provider, snapshot codec and retained-source admission in actual persistence paths | Platform release owner with Parties/EventStore integration owners; 4.5/4.8/4.22/6.23/T3/T4 |
| Surviving lifecycle | Independent existing lifecycle/receipt authority and durable work/copy inventory, writers/fence/reissue and authenticated time; do not place it in the same restorable cut | Selected authority owner; Q2–Q4/T2/T5 |
| Backup and qualification | All C01–C16 physical inventory and key generations, immutable object versions and owner receipts; isolated restore and exact fault targets, operator/observer and disposal | Recovery/copy owners, Administrator and qualified deputy; 4.10/4.23/6.1/8.4–8.10/8.14/8.17 |

The operator handoff records desired staging names `hexalith-staging` and `hexalith-staging-data`. These names are planning context only. The production namespace, all environment instances, workload names, tenant IDs, gateway FQDNs, key mounts and authority locations remain unset. Staging uses approved exact `hexalith.com` names; production uses approved exact `tache.ai` names under 6.7. Neither zone establishes a particular hostname.

Environment-layer identity owns shared/data/ingress objects. The per-job application identity cannot mutate the data namespace or environment layer. Only the owner of each dynamic secret namespace writes it. The current shared OpenBao, its bootstrap token, shared Redis and generic backups are not automatically in scope.

## Ordered work and exit evidence

Statuses below come from the captured sprint register. Dated operator acceptance has its own scope and freshness; the register's `done` or `in-progress` label cannot replace it.

| Order | Owning stories and recorded state | Work, operators and dependency | Evidence required to exit |
| --- | --- | --- | --- |
| 1 | 4.0 done; 4.1/4.2/4.3/4.27 in progress; 4.26 done; 4.28 backlog | Administrator/cluster operators follow the existing next-operator handoff: fresh accepted recovery, external-etcd/node recovery and independent native access; accepted retirement; qualify each supported-cluster transition; qualified private Rancher/access/restore and runner separation. Reuse the accepted console-closure record within its scope. | Fresh exact signed acceptance and independent readback; current cluster/version/authority binding; no stale maintenance or recovery date reused as acceptance. This packet initiates no capture, upgrade or retirement. |
| 2 | 4.9 backlog; EventStore AD-26 upstream; 4.5/4.6/4.8/4.11–4.13 backlog | Platform/EventStore ratify runtime/toolchain/profile, module intake, routing catalog, value-free secret contract, renewal owners, identity-event lag and suites; qualify retained Helm publication and off-site provenance. | Ratified profile/template/provider/toolchain digests, complete source/image/enrollment identities and only-writer/renewal records. Planned `deploy/dapr/eventstore-routing-catalog.json` and `deploy/dapr/openbao-secret-contract.yaml` are absent. |
| 3 | 4.14–4.21 backlog; 4.7 backlog | Platform environment operators build staging isolation, separate secrets/key store, Dapr trust, durable broker, Gateway/certificates, realm and dedicated epoch-bound executor/locks/telemetry from approved definitions. Requires steps 1/2. | Enforced storage/network/pod/identity isolation, rotation and missing-generation denial; qualified broker; actual Gateway/route/cert chain and epoch/fence records. Labels or a dry run alone do not prove enforcement. |
| 4 | Dedicated PLAT-ACTOR-HISTORY-1 T2; 4.15/8.6 backlog; 8.4 backlog | Platform custody/recovery/copy owners resolve Q1–Q4/Q7 before selecting adapters. Name actual authorities, custodians, independent lifecycle placement, expiry mechanism, all-copy classes, time/currentness and failure bounds. | Signed/accepted decision references and backend capability observations; all physical copy/key owners or authenticated absence. If no existing mechanism qualifies, owner design/procurement work is required; do not install an invented backend. |
| 5 | PLAT-ACTOR-HISTORY-1 T3/T4; explicit F1; 4.10 in progress | Parties/EventStore/Platform approve Q5/Q6 and F1, then implement selected adapters/seven SDK methods, independent key envelopes and mixed-deadline codec, actual host registrations and retained-source admission. T1 is already present. | Approved mapping and continuation contract, reviewed implementation, meaningful source tests, actual host readiness/refusal and package provenance. Unavailable defaults remain unavailable. |
| 6 | PLAT-ACTOR-HISTORY-1 T5; 4.10 in progress; 8.4–8.10 backlog | Recovery and copy operators compose durable inventory, pending work, stable receipts, independent anchor reconciliation, quarantine and epoch-bound owner hooks. Requires all relevant Q decisions. | Actual hook implementations/contract version, complete inventory generations and destruction proofs, genuine pre-expiry data/key/lifecycle backup, fenced isolated target and current authority. Normative `docs/contracts/recovery-hook-v1.md` is absent; the schema alone is insufficient. |
| 7 | 4.22/4.23/4.25 backlog | Staging release operator installs the approved reference release by retained package/image digest through the existing locked, epoch-bound executor after environment readiness; qualifies restoration/isolation before candidate acceptance. | All actual Parties/EventStore/Tenants/Memories workload UIDs/digests, gateway/registry bindings, original state observations and approved restore receipts. No `aspire publish`/`aspire deploy` substitutes for the retained Helm release. |
| 8 | 6.7/6.8/6.9/6.11/6.14–6.18/6.23 backlog | Production environment/release operators apply qualified definitions, own secrets/storage/realm/Gateway/executor/locks, and require all applicable production preconditions, enrollment and fresh-install/retained-data evidence. First empty installation needs the Administrator's exact approved mode. | Production target acceptance and deployed binding; ingress restricted to executor/probes. No user opening or G2 is implied. Two-tenant qualification needs explicit approved scope; the standing single synthetic-tenant rule is not silently widened. |
| 9 | 6.1/6.24, 8.1/8.7/8.8/8.14/8.17 backlog; T6/F2 | Administrator/rehearsal/recovery owners approve exact fault/restore units, bounded substitution/disposal and observers; qualify complete installed matrix and independent persisted assertions. Whole-site/deputy claims require their own existing stories. | Exact target/cut/authority/fault manifests, all P-01–P-10 and custody installed receipts, no failed/skipped/unrun case. Full Live awaits accepted implementation/source targets/date/command. |

The actor-purpose irreversibility and independent-authority requirements remain stricter than a general RPO exception. Do not borrow the Platform one-hour recovery-point RPO, 4.9 identity capture lag, or 6.2 monitoring cadence as permission to release expired actor evidence.

## Validation commands

The current read-only commands and exits are retained in `native-metadata.json`. The commands below are exact **conditional validation syntax**, not resolved deployment commands. Owners must bind their variables through an approved reference manifest first. Run from Platform; retain argv, tool version/hash, exit, timestamp and sanitized output per invocation. Do not print values from credential sources or retrieve Kubernetes Secret data.

Every block starts with shell nounset handling or required-value assertions. An absent reference is a stop; none of these variables defaults to the observed cluster. Use each capability's own kubeconfig. Server dry-run invokes admission validation; run it only under the approved environment preparation scope.

```bash
set -euo pipefail
: "${APPROVED_KUBECONFIG:?exact approved access reference required}"
: "${APPROVED_CONTEXT:?exact environment context required}"
: "${APPROVED_CLUSTER_WITNESS_UID:?approved kube-system namespace UID required}"
: "${APPROVED_APP_NAMESPACE:?approved application namespace required}"
: "${APPROVED_DATA_NAMESPACE:?approved data namespace required}"
test "$(kubectl --kubeconfig "$APPROVED_KUBECONFIG" --context "$APPROVED_CONTEXT" --request-timeout=15s get namespace kube-system -o jsonpath='{.metadata.uid}')" = "$APPROVED_CLUSTER_WITNESS_UID"
kubectl --kubeconfig "$APPROVED_KUBECONFIG" --context "$APPROVED_CONTEXT" --request-timeout=15s get --raw=/version
kubectl --kubeconfig "$APPROVED_KUBECONFIG" --context "$APPROVED_CONTEXT" --request-timeout=15s get namespaces "$APPROVED_APP_NAMESPACE" "$APPROVED_DATA_NAMESPACE" -o 'custom-columns=NAME:.metadata.name,UID:.metadata.uid,LABELS:.metadata.labels'
kubectl --kubeconfig "$APPROVED_KUBECONFIG" --context "$APPROVED_CONTEXT" --request-timeout=15s api-resources -o name
```

Inspect source/profile/configuration and retained chart bytes before server validation. The digest is for the owner-selected retained file; no chart URI/version is fabricated.

```bash
set -euo pipefail
: "${APPROVED_RETAINED_CHART:?retained chart file required}"
: "${APPROVED_CHART_SHA256:?approved chart digest required}"
: "${APPROVED_HELM_RELEASE:?approved release name required}"
: "${APPROVED_APP_NAMESPACE:?approved application namespace required}"
: "${APPROVED_VALUES_FILE:?value-free approved configuration file required}"
: "${APPROVED_RENDERED_MANIFEST:?new approved output file path required}"
test "$(sha256sum "$APPROVED_RETAINED_CHART" | cut -d ' ' -f 1)" = "$APPROVED_CHART_SHA256"
test ! -e "$APPROVED_RENDERED_MANIFEST"
helm template "$APPROVED_HELM_RELEASE" "$APPROVED_RETAINED_CHART" --namespace "$APPROVED_APP_NAMESPACE" --values "$APPROVED_VALUES_FILE" > "$APPROVED_RENDERED_MANIFEST"
```

Require the release validator to prove the rendered `(GVK, namespace, name)` set equals its approved allowlist, no embedded Secret values, digest-pinned workload images, namespace/hostname isolation and correct writer ownership before dry-run. That validator/executor command is an unresolved binding in `command-bindings.json`; this packet provides no permissive substitute.

```bash
set -euo pipefail
: "${APPROVED_ENV_LAYER_KUBECONFIG:?environment-layer access reference required}"
: "${APPROVED_CONTEXT:?exact context required}"
: "${APPROVED_RENDERED_ENVIRONMENT_MANIFEST:?approved rendered environment manifest required}"
kubectl --kubeconfig "$APPROVED_ENV_LAYER_KUBECONFIG" --context "$APPROVED_CONTEXT" --request-timeout=15s apply --dry-run=server -f "$APPROVED_RENDERED_ENVIRONMENT_MANIFEST"
```

An application deploy identity must fail data/environment-layer writes. Execute the negative check with that identity, not impersonation by an administrator. Retain the expected denial separately from failed authentication/transport; the latter cannot prove RBAC.

```bash
set -euo pipefail
: "${APPROVED_APP_DEPLOY_KUBECONFIG:?application deploy access reference required}"
: "${APPROVED_CONTEXT:?exact context required}"
: "${APPROVED_DATA_NAMESPACE:?approved data namespace required}"
if permission_result=$(kubectl --kubeconfig "$APPROVED_APP_DEPLOY_KUBECONFIG" --context "$APPROVED_CONTEXT" --request-timeout=15s auth can-i create secrets --namespace "$APPROVED_DATA_NAMESPACE"); then
    exit 1
else
    permission_exit=$?
fi
test "$permission_exit" -eq 1
test "$permission_result" = no
```

For each existing owning deployment, compare the returned UID and every intended/runtime image digest with the accepted manifest. Run the rollout command once for each approved deployment, including declared dependencies; no workload name is assumed.

```bash
set -euo pipefail
: "${APPROVED_KUBECONFIG:?approved observer access reference required}"
: "${APPROVED_CONTEXT:?exact context required}"
: "${APPROVED_APP_NAMESPACE:?approved application namespace required}"
: "${APPROVED_DEPLOYMENT_NAME:?exact deployment name required}"
: "${APPROVED_READINESS_TIMEOUT:?owner-bound readiness budget required}"
kubectl --kubeconfig "$APPROVED_KUBECONFIG" --context "$APPROVED_CONTEXT" --request-timeout=15s --namespace "$APPROVED_APP_NAMESPACE" get deployment "$APPROVED_DEPLOYMENT_NAME" -o 'custom-columns=NAME:.metadata.name,UID:.metadata.uid,IMAGES:.spec.template.spec.containers[*].image'
kubectl --kubeconfig "$APPROVED_KUBECONFIG" --context "$APPROVED_CONTEXT" --request-timeout=15s --namespace "$APPROVED_APP_NAMESPACE" rollout status "deployment/$APPROVED_DEPLOYMENT_NAME" --timeout="$APPROVED_READINESS_TIMEOUT"
kubectl --kubeconfig "$APPROVED_KUBECONFIG" --context "$APPROVED_CONTEXT" --request-timeout=15s --namespace "$APPROVED_APP_NAMESPACE" get pods -o 'custom-columns=NAME:.metadata.name,UID:.metadata.uid,IMAGEIDS:.status.containerStatuses[*].imageID'
```

Inspect gateway bindings and private recovery metadata through exact approved identities. Readiness, route existence and backup names remain insufficient for custody acceptance.

```bash
set -euo pipefail
: "${APPROVED_KUBECONFIG:?approved observer access reference required}"
: "${APPROVED_CONTEXT:?exact context required}"
: "${APPROVED_DATA_NAMESPACE:?approved data namespace required}"
: "${APPROVED_APP_NAMESPACE:?approved application namespace required}"
kubectl --kubeconfig "$APPROVED_KUBECONFIG" --context "$APPROVED_CONTEXT" --request-timeout=15s --namespace "$APPROVED_DATA_NAMESPACE" get gateways.gateway.networking.k8s.io -o 'custom-columns=NAME:.metadata.name,UID:.metadata.uid,HOSTS:.spec.listeners[*].hostname'
kubectl --kubeconfig "$APPROVED_KUBECONFIG" --context "$APPROVED_CONTEXT" --request-timeout=15s --namespace "$APPROVED_APP_NAMESPACE" get httproutes.gateway.networking.k8s.io -o 'custom-columns=NAME:.metadata.name,UID:.metadata.uid,PARENTS:.spec.parentRefs[*].name,HOSTS:.spec.hostnames,BACKENDS:.spec.rules[*].backendRefs[*].name'
```

Backend unit/key/receipt probes, actual tenant enrollment, runtime network-isolation tests, authenticated source reads, genuine restore, cleanup and failure injection need **real owner-provided commands**. Their exact argv/identity/target/receipt bindings remain `null` in [command-bindings.json](command-bindings.json). Neither new endpoints nor private administrative commands are invented here. This is the precise remaining execution gap, not a completed installation recipe.
