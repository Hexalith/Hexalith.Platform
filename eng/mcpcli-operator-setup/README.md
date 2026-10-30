# Story 4.17 temporary operator environment

This directory prepares a review packet for the selected private Kubernetes target
`jpiquot@local`, `https://192.168.1.30:6443`. No deployment is authorized. The current
result is **blocked before apply-ready preparation**, as detailed in
`preparation-status.json`; actual three-client and independent timing checks stay
with the human operator.

Everything added for this task lives here. Existing Platform/user changes are
preserved. The task chart copies the maintained `../helm-qualification/chart`
byte-for-byte except `reference-graph.json`, regenerated from the canonical graph.
It contains all eight declared servers, including `memories/domain`. Templates are
checked against the maintained originals before each preparation. The maintained
chart's seven-workload snapshot, stale seven/15 hosted assertions and separate
finite campaign policy are not changed: their approved nine-Pod allocation must
not silently become a ten-Pod campaign. This isolated snapshot allows the operator
task to render the current complete closure without changing that policy.

From the owning Platform root, export a new canonical graph to a fresh path:

```sh
DirectoryPackagesPropsPath="$PWD/eng/helm-qualification/source-packages.props" \
  dotnet run --project eng/mcpcli-operator-setup/graph-export/GraphExport.csproj \
  --configuration Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false \
  -p:RunAnalyzers=false -- "$PWD" /tmp/mcpcli-417-new-graph.json
HELM_BIN=/absolute/path/to/verified/helm-4.2.0 python3 -m unittest discover -v \
  -s eng/mcpcli-operator-setup -p 'test_*.py'
```

The source resolver reads declared root checkouts only. Initialize no nested
submodules and change no dependency versions. Before preparing, compare the new
graph semantically with `chart/reference-graph.json`; update only this task copy
when actual declarations change. Record source revisions and SHA-256 inputs. The
export uses the real `PlatformHostedPublishing.Describe` and
`PlatformApplicationGraph.Resolve`, without starting an AppHost or services.

Copy `profile.template.json` outside the checkout and fill every binding. An
unfilled template fails. Use `prepare.source_inputs(root)` for the exact source
hash map and `git rev-parse HEAD` for the source revision, recording dirty input
hashes independently of HEAD. No hashes can be guessed. Set a fresh
`mcpcli-417-*` namespace/release/tenant, finite expiry within 24 hours, one
`TenantContributor` operator subject, `command:submit`/`query:read` and only the
`tenants`/`party` domains. The operator's system/selected-tenant scopes are separate
from the short-lived bootstrap administrator and bound Tenants authority.

Use actual immutable images for the eight graph identities and every data service.
`local-image-candidates.json` records seven module images found in the local Docker
cache. Their `localhost:15033` registry cannot be assumed reachable from the
selected Kubernetes nodes; a focused offline EventStoreHost archive build failed with CS1704 (source
`Hexalith.Commons.UniqueIds` 1.0.0 conflicts with cached 2.30.3). The exact
network-isolated command and logs are retained under `evidence/eventstore-offline-image.*`.
A serialized Release/package-only fallback restored the exact pinned dependencies
from local caches successfully, then failed CS1061/CS0117 because current
Platform.Custody requires `ReadAdmittedOriginalAsync`, `IssueOperationId` and
`ReadCurrentOriginalAsync`, absent from those package APIs. See
`evidence/eventstore-package-offline-mapped-*`. No dependency versions or shared
files were changed to bypass either conflict. The associated
`native-package-final-output.json` is a **failed** clock-readiness run, not runtime
qualification. Owner artifact/source compatibility, a selected-target registry
binding and actual pullability remain unresolved. Never use the chart test's
`qualification.invalid`/fixture images or bindings as real inputs.

The profile must name TLS state/pubsub/configuration, secret and conversation
providers declared by the graph, their real endpoints and complete configuration
bindings. `values.environment.componentConfigMap` must contain the exact component
and ACL files consumed by each path-valued input. Every referenced credential is
a Kubernetes Secret name/key; no literal token or private signing material belongs
in values or the public packet. You may use **task-owned private files** in
`privateSecretFiles`, or an already approved namespace-owned ExternalSecret. An
External Secrets installation is not a prerequisite of this task. Private file
modes must exclude group/other access and files must be outside the checkout.
Create the task-owned Secrets from those files only after deployment authorization;
record their names/keys/types without their values. A registry auth file is optional
for public registries; private registries need their own readable private authfile.

Supply reviewed namespace-owned environment manifests for bounded private data
services, PVCs, the component-file ConfigMap, access controls and any chosen
credential-provider objects. Cluster-scoped resources, another namespace,
NodePort/LoadBalancer Services, host mounts and privileged workloads are refused.
Record data service CPU/memory/storage limits and provider maintenance/expiration.
Do not change shared Dapr, cluster identity, CRDs or other applications. Existing
Dapr mTLS, a restricted sidecar injector, catalog/owner route bindings and
EventStore writer activation are explicit prerequisites. The task chart binds
app limits at 1 CPU/512Mi each; the task render sets Dapr sidecars to at most
500m/256Mi each and tmp to 128Mi. The eight apps/sidecars therefore reserve at most
12 CPUs/6Gi memory, plus the explicitly bounded environment data services and
control-plane capacity. This is a configured ceiling, not a performance result.

```sh
python3 eng/mcpcli-operator-setup/prepare.py \
  --profile /private/task/profile.json --canonical-graph /tmp/mcpcli-417-new-graph.json \
  --helm /absolute/path/to/verified/helm-4.2.0 --output /private/task/new-review
```

The preparer runs only pinned Helm template/version and read-only registry
manifest inspection, verifying actual manifest digests. It never invokes kubectl,
Helm install/upgrade, registry publication or application APIs. Template/registry
failures do not produce a success receipt. A rendered result is `review-required`
with `applyAuthorized: false`. Review source/chart/graph/image identities, the
entire resource inventory, private Secret creation references, source stability,
app/environment manifests, omitted public HTTPRoutes, ordered seed operations,
principal scopes, local port-forward, expiry and cleanup. There are no public
routes in the task render. Do not apply a profile containing synthetic test values.

The seed helper is a task-only owner API client, never a product CLI or new MCP
surface. `privateSeedFile` names a private plan containing the nine ordered
steps tested by `test_seed.py`: bound-workload `BootstrapGlobalAdmin`, its committed
status, Gateway `CreateTenant`, committed status, `AddUserToTenant` with
`TenantContributor`, committed status, Gateway `get-tenant` persisted response,
Memories `/api/v1/tenants` provisioning and persisted Active response. Use the
actual owner-minted workload assertion and exact bound command proof from
`PlatformNativeTestPreparation`/`PlatformRoutingCatalogBootstrap`; do not invent
authentication headers or bypass the owner bootstrap. Those native runner seams
are not installed endpoints. Their hosted route/catalog binding is an unresolved
input until the owning environment supplies it. The Gateway response uses
`payload`, while McpCli's public result wraps it as `document`.

The private authority file maps `bootstrap`, `admin`, `operator` and `memories`
to owner-minted HTTP header dictionaries. Keep it separate from the operator's
McpCli profile and signing material, with mode 0600. Validate the seed plan without
network activity:

```sh
python3 eng/mcpcli-operator-setup/seed.py --plan /private/task/seed.json \
  --namespace mcpcli-417-APPROVED --tenant mcpcli-417-APPROVED
```

Only after the concrete packet is reviewed and deployment is authorized: recheck
context/API, namespace nonexistence, resource identities, image pullability,
private Secret bindings and expiry. Apply only the reviewed task resources and
private Secret creations. Capture the namespace UID/label and every readiness
check. Establish loopback-only Gateway and Memories forwards with the reviewed
ports. Run the helper with `--execute-reviewed --review /private/task/review/review.json
--review-sha256 EXACT-APPROVED-REVIEW-SHA256 --authorities /private/task/headers.json
--journal /private/task/new-seed-journal`. It refuses an altered seed plan, altered
review packet, namespace/tenant mismatch or expired review before any write. Each mutation is sent once; observations
poll for at most 45 seconds and terminal failures stop immediately. A partial or
uncertain journal prevents re-execution. Investigate message IDs through owner
status/state APIs; never automatically repeat a command. Seed state does not prove
the accepted 1.2.0 tool or any human MCP client works.

Supply only the scoped operator profile and approved synthetic identifiers to the
human sessions described by McpCli's README. No signing key or bootstrap authority
may reach client registration or transcripts. Keep the private port-forwards
running in the operator's actual native/WSL environment.

After the sessions, stop their server processes and port-forwards. Compare the
namespace UID and `hexalith.com/operator-task` label with the install receipt;
delete **only** that task namespace, wait for owned PVC/data removal and record any
leftovers. Revoke/expire task credentials and remove only task-owned private files.
Never delete another namespace, the shared Dapr control plane, cluster credentials,
source checkouts or earlier evidence. Record cleanup failure honestly.
