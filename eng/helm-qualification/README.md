# Reference-composition Helm qualification

The selected application package is the maintained chart in `chart/`. The native
probe uses Aspire **13.6.1**, `Aspire.Hosting.Kubernetes`
**13.6.1-preview.1.26506.6**, and the catalog Dapr integration
**13.6.1-beta.935**. These are the actual tools, replacing the story's historical
13.5.4 identity. No dependency upgrade is part of this qualification.

Native publishing succeeds but drops the `WithDaprSidecar` annotation from all
seven generated Pod templates. Preserving module Dapr plumbing would require a
separate Kubernetes model, which is the AD-1 fallback trigger. `qualify.py`
repeats that measurement, requires all seven native workloads to exist, retains
the generated files and render, and records their hashes and missing annotations
in `exportDecision`. A build failure or incomplete probe fails qualification.
No generated native files are patched.

`PlatformApplicationGraph` selects the actual Parties declaration and its
EventStore, Tenants and Memories closure. `PlatformServerBindings` consumes that
same neutral metadata before applying its existing local profile.
`PlatformDaprBindings` supplies capability names, consumers, environment keys,
receiver ACL operations and subscriptions to both paths. Hosted selection never
calls `PlatformLocalProfile`, creates local providers, or generates credentials.
The chart's `reference-graph.json` is a serialized metadata input from that graph,
not a second hand-written topology. Its parity with current declarations is
checked by the C# tests and every native probe. To update it after a declaration
change, run the same probe with `Platform__Hosted__GraphPath` pointing at that
file. The maintained chart remains generic.

The application owns Deployments, Services, HTTPRoutes and Dapr binding objects
(Components, Configurations and Subscriptions). Its generic component template
binds hosted provider types to endpoint parameters and existing credential
references; scopes come exclusively from the graph. Endpoint metadata can use
`endpointFormat: authority` for host:port providers or `url` for URL providers;
hosted Redis bindings require `enableTLS: "true"`. The environment owns the
underlying data services, Gateway/listeners, namespace, identity authority,
credential Secrets, registry credential, component-file ConfigMap and persistent
infrastructure. The application chart creates none of those prerequisites.
Existing component manifest files are mounted read-only from the environment
ConfigMap when an owner requires a component path. The environment writer supplies its control-plane trust domain and a non-secret
Sentry endpoint parameter. Each public workload requires a hostname in
`hostnames["module/server"]`; the reference EventStore and Memories `/api/v1`
backends therefore use distinct hosts. Identical normalized host/listener/path
matches targeting different backends fail. Route templates use their literal path prefix before
the first parameter, so Gateway PathPrefix contains valid URI path characters.
The environment writer must supply provider metadata and file contents consistent with its selected bindings.

Each enabled workload requires `images["module/server"]` as a full
`repository@sha256:<64 lowercase hex>` reference, plus all declared input bindings
under `bindings["module/server"].configuration`. Secret and dynamic-secret inputs
accept `{name, key}` references only. A non-secret input can be a string, a JSON
object/array string (flattened into .NET configuration keys),
`{endpoint: "parameter-name"}`, or `{configMapKeyRef: {name, key}}`.
Nested endpoint and ConfigMap reference leaves resolve recursively, including
Clock Sources arrays. Literal HTTPS leaves use the same endpoint validation.
Structured JSON is decoded before validating keys and values; escaped credential
keys cannot bypass the reference-only requirement. Configuration/environment keys
are compared case-insensitively, matching .NET, and equivalent duplicates fail.
Structured secret leaves can be supplied as additional existing references in
`bindings["module/server"].credentials`, keyed by flattened environment name (for
example `Clock__Sources__0__AuthenticationToken`). They cannot override canonical
Dapr/security bindings or declared channel/API-token inputs. Distinct app and API
Secret references are supported and must agree with their respective declared inputs.
Credential literals inside structured configuration fail.
Endpoint parameters permit HTTPS or TLS data-service schemes and reject embedded
credentials, queries, fragments and loopback/local endpoints. Dapr token Secret
references use the `token` key and must agree with the owner's channel inputs.
No plaintext credentials belong in values. `fixture_values()` supplies synthetic
admission-test inputs; it is not a deployable staging profile.

Normal mode has serving replicas and declared public routes. Recovery sets every
workload to zero replicas and removes every HTTPRoute. Remove-workloads omits
Deployments, Services and routes while retaining application Dapr bindings.
Unknown modes fail. Dapr HotReload remains disabled in every mode. These modes do
not implement data restoration.

Run from the Platform root with the repository's .NET test runner:

```bash
dotnet test --project tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj \
  -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:RunAnalyzers=false
HELM_BIN=/absolute/path/to/verified/helm python3 -m unittest discover \
  -s eng/helm-qualification -p 'test_*.py' -v
HELM_BIN=/absolute/path/to/verified/helm python3 eng/helm-qualification/qualify.py
```

`toolchain.json` pins Helm 4.2.0, kind, the digest-qualified Kubernetes node image,
Gateway CRDs and the Builds-catalog Dapr patch. Downloaded CRDs and the Dapr chart
must match the recorded SHA256 values. Helm's distribution URL and SHA256 are
provided for a local tool install. The transfer requires curl and bounds the whole
download with a deadline, including response bodies. The qualifier requires the pinned Helm binary
and records its binary hash. Missing tools or failed checks return nonzero;
there are no skipped qualification checks.

The qualifier creates a unique `h45-*` kind cluster and private kubeconfig, passes
that kubeconfig and context to every Kubernetes/Helm operation, and never reads
the ambient kubeconfig. It installs the pinned Dapr control plane and Gateway
CRDs, enforces restricted admission in the application namespace, and submits an
actual Pod for each rendered Deployment template with server dry-run. Both the
real templates and unsafe control use the observed ReplicaSet-controller service
account UID and groups, matching the Deployment admission caller. The injector
[authorizes these controllers](https://docs.dapr.io/concepts/dapr-services/sidecar-injector/);
its pinned fixture settings require dropping ALL sidecar capabilities. It requires
injected `daprd`, inspects every regular/init container's security settings and
requires an unsafe Pod rejection. A Deployment dry-run does not satisfy this
check.

It installs and upgrades the packaged archive using `--server-side=true`, requires
Helm `Apply` managed fields, demonstrates refusal of a deliberately conflicting
writer without Helm force flags, then verifies recovery and workload removal.
Only the synthetic rival writer uses `--force-conflicts` to set up the conflict.
Environment fixtures must retain their UIDs and remain free of Helm ownership
through removal and uninstall. Every wait is bounded. The `finally` path deletes
only the invocation's named cluster, checks owned node absence, and removes the
private kubeconfig even after cluster deletion or inventory fails, retaining that
cleanup failure in the result. Final source verification compares both complete
file inventories and hashes, detecting newly added templates as well as edits.
Local failure-path regressions exercise those behaviors without a live cluster.

The durable result is
`_bmad-output/implementation-artifacts/evidence/4-5/qualification.json`, accompanied
by version-control-visible command logs (retained through the evidence directory
ignore override), exact source/input/tool/package hashes, native output, archived
chart, rendered manifests and admitted Pods. A failed result remains failed and
does not close the story. The evidence excludes OCI publication/attestation,
off-site restoration, staging deployment and functional application readiness
with retained production images. Those are separate downstream acceptances. The
Timesheets enrollment candidate is unchanged.
