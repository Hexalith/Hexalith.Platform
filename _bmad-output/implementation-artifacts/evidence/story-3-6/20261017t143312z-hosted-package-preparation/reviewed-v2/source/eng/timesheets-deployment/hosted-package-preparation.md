# Timesheets hosted package preparation — 2026-10-17

Platform now has a maintained Helm fallback for its single Timesheets host.
The [application chart](chart/Chart.yaml) and separate
[environment-layer chart](environment-chart/Chart.yaml) implement the missing
hosting definitions. They are local enrollment candidates, not published or
qualified releases. The existing Development AppHost and historical enrollment
candidate remain unchanged. This increment invokes the small maintained-chart
fallback in Platform architecture AD-1; a qualified retained Platform release
must include its complete EventStore/Tenants/Memories closure before deployment.
This Timesheets increment cannot become a second independent workload writer.

The fresh [native observation](../../../../_bmad-output/implementation-artifacts/evidence/3-6/20261017t142841z-deployment-authority-runtime/native-observation.json)
at 14:28 UTC found context `jpiquot@local`, API
`https://192.168.1.30:6443`, cluster namespace UID
`b5860674-1d1e-4a3b-b413-e1bb3b9a4557`, and 24 namespaces. Both
`hexalith-staging` and `hexalith-staging-data` are absent. No Timesheets
workload/image was found; Gateway and HTTPRoute resource types are unavailable.
The other story's EventStore in `ext-secrets-1-isolated` is not this deployment.
There is therefore no installed Timesheets artifact, endpoint, subscriber or
shared-rebuild binding to certify. These are observations of the inspected
configuration, not assumptions from the offline plan.

The sole-owner policy permits the owner's own documentary decisions and
self-review. No distinct human reviewer or new personal key is required for
this preparation. Actual PKI, installed grants, maintenance, completeness,
source continuity and runtime outcomes still require observations. This record
does not reclassify the expired recovery candidate, original verifier refusal
or any historic receipt.

The application chart binds a digest-pinned image, exact source revision and
required existing namespace-local registry pull Secret. Only the Pod's
`imagePullSecrets` references that credential; the chart creates no registry
Secret or application permission to read it.
Its two ClusterIP Services select the single host: `timesheets-public:8080` and
`timesheets-internal:8081`. Kestrel listens on both ports; Dapr targets 8081;
the owner internal-surface guard stays enabled. The public hostname remains the
proposal `timesheets.hexalith.com`. Ingress is closed by default. An enabled
HTTPRoute uses the separate data-namespace Gateway and exactly the existing
confirm/adjust GET and submit routes, capability-management prefix and metadata
route. No internal path or broad root route is advertised. `recovery` scales
the host to zero and removes the public route; `remove-workloads` omits the host
and route. Neither mode performs restoration or rewinds domain state.

The application package owns only its ConfigMap, Services, Deployment and optional
HTTPRoute. Existing token Secrets are referenced with Dapr's `token` key by both
the sidecar annotations and application environment. Client credentials remain
separate existing Secret references. HTTPS authority/issuer/token endpoint and
an asymmetric algorithm are mandatory. The SDK's dedicated
`Authentication:WorkloadJwtBearer` contract keeps broker trust separate from
public/human JWT trust; both are explicitly bound. The EventStore resource-binding
request opt-in is enabled. No credential, generated grant, certificate, Secret object,
domain authorization provider or migration override is embedded. The generic
workload authority must be enrolled with an actual signer and exact currently
allowed resources before this configuration can authorize a request.

The separately approved environment-layer candidate supplies the exact
`timesheets-readmodels` component, Redis TLS and credential references,
Timesheets-only scope, disabled actor state store, default-deny Dapr caller policy,
disabled HotReload, a narrowly scoped ServiceAccount/Role and listener-specific
NetworkPolicy. Persistent Redis service/data/PVC/backup and its current TLS
identity remain environment prerequisites. The component preserves SDK control,
transport, epoch and journal records used by shared rebuilds; no direct domain
write path is added. Existing SDK handlers and gateway dispatch own projection
delivery and `/project/rebuild/shared/v1`; Timesheets has no new pub/sub consumer.
The private ACL is tested against the installed SDK's complete operation set.
Fresh installed delivery/rebuild and restart outcomes remain required.

The gateway's existing composed-host release must retain its Timesheets domain
registration (`*|timesheets|v1`, app ID `timesheets`, method `process`), preserve
other module registrations, and explicitly allow the Timesheets workload.
Gateway certificate, authority enrollment, broker retention/dead-letter recovery,
catalog contents, shared-rebuild protocol and EventStore checkpoint persistence
must be qualified together. A generic Subscription object on the wrong app or
an empty legacy inventory cannot replace this evidence. This chart does not
adopt another story's gateway or fabricate an installed subscriber.

Verification uses the actual Helm renderer. It checks the exact route allowlist,
split listeners, identical app/sidecar Secret references, pod security, immutable
image/source identity, tier separation, recovery/removal behavior, HTTPS and
asymmetric denial cases, namespace refusal, default-deny network/Dapr policy,
credential-literal refusal and SDK route parity. Fixture names ending in
`fixture.invalid` are test inputs only. They are never retained as deployable
environment values. Reproduce with an existing Helm 4.2.0+ binary:

```bash
HELM_BIN=/path/to/verified/helm python3 -m unittest discover \
  -s eng/timesheets-deployment -p test_hosted_chart.py -v
```

The [original preparation receipt](../../_bmad-output/implementation-artifacts/evidence/story-3-6/20261017t143312z-hosted-package-preparation/verification.json)
and its archives remain unchanged as dated pre-review evidence. They predate the
registry pull-Secret repair. The separate
[reviewed-v2 receipt](../../_bmad-output/implementation-artifacts/evidence/story-3-6/20261017t143312z-hosted-package-preparation/reviewed-v2/verification.json)
binds the reviewed source snapshot, new local archives, focused test results and
original evidence hashes. Those archives can be inspected and rendered offline; there is
no registry publication, application deployment, permission change, recovery,
migration or domain command. Story 3.6 stays incomplete and release FAIL.

An operational deployment approval is not ready. Missing factual inputs are the
qualified environment/profile and complete composed-host release, immutable
Timesheets image/provenance and registry pull Secret, enrolled HTTPS issuer/client/Secret references,
exact Gateway TLS listener, scoped egress destinations, persistent Redis binding,
fresh accepted recovery, legacy inventory/continuity/maintenance and operational
provider enrollment. Existing access does not create those resources. Default
chart rendering refuses these missing values. Once they exist, retain their
exact bytes and a concrete operator command set before requesting approval.

The future target remains only `jpiquot@local`, API
`https://192.168.1.30:6443`, app namespace `hexalith-staging` and separate data
namespace `hexalith-staging-data`. An approval must name the exact package
digests, environment/application values hashes, release writer, current cluster
UID and all proposed RBAC/NetworkPolicy/issuer effects. Environment changes are
forward-only and separately authorized. Start the retained application package
with ingress closed, inspect readiness/private denial and module acceptance,
then request any required ingress-opening approval. If the candidate fails,
keep ingress closed, retain diagnostics, and render the approved compatible
baseline/rollback set exactly once through the retained package. Helm automatic
rollback and `helm rollback` are forbidden by AD-3. Never revert environmental
credentials, grants, durable authority, events, index state or data as application
rollback; incompatible data requires its separately approved owner recovery.
