# Workload binding provider and protected enrollment

Platform AD-6 owns this addition to the existing Keycloak realm contract. EventStore
owns `https://hexalith.com/security/workload-binding/v1`, the scope vocabulary and
the signed claim semantics. `Platform.Identity` admission signers, enrollment
receipts and the SDK's nonconforming Development fixture are not OAuth issuers.

The source under `infrastructure/keycloak-workload-binding` targets the installed
Keycloak **26.6.4**. Build with `mvn -B -ntp package`. The resulting provider uses
the supported client-policy executor and OIDC protocol-mapper SPIs. Keycloak
authenticates credentials and signs tokens with its existing realm RSA signer.
The provider contains no credential verifier, token endpoint or private signer.
The 26.6.4 client-credentials flow retains `authorization_details` for protocol
mappers; a separate RAR processor is not needed for this flow.

## Owner-controlled installation

Installing this source is not enrollment authority. An authorized realm owner
must independently approve the provider artifact digest, TLS origin and signer
SPKI digest, exact caller inventory and grants. This change neither installs into
an existing server nor changes dependency pins, production realms or deployment.

For a separately approved installation, place the built JAR in Keycloak's
`providers` directory and perform the owner's normal server build/restart. Enroll
a client profile with executor `hexalith-workload-binding-v1`, then a realm client
policy using that profile and condition `any-client`. Attach an OpenID Connect
protocol mapper with provider `hexalith-workload-binding-v1` to each enrolled
workload client. Use confidential service-account clients, disable full scope,
interactive/password flows and refresh tokens, and give them no human claims,
identity-administration roles or default scopes. Retain only exact optional
audience and operation scopes already approved for each client. The executor is
mandatory: a mapper without a per-exchange policy admission refuses issuance.

Realm attributes (protected owner configuration):

| Attribute | Required value |
| --- | --- |
| `hexalith.workload-binding.enabled` | Literal `true` only after owner verification |
| `hexalith.workload-binding.signer-not-after` | Exclusive UTC Unix second for the approved signer enrollment |

Each client's protected `hexalith.workload-binding.workload` attribute names its
actual server-owned caller. The request never selects that caller. Its protected
`hexalith.workload-binding.grants` attribute is a JSON array, at most 64 grants and
65,536 characters. Every grant has exactly these fields:

```json
[
  {
    "grantId": "owner-assigned-stable-id",
    "revision": 1,
    "clientId": "actual-own-client",
    "workload": "actual-own-caller",
    "audience": "actual-receiver",
    "operations": ["eventstore:stream-read"],
    "bindings": {
      "eventstore:bound-tenant": ["owner-approved-exact-tenant"],
      "eventstore:bound-domain": ["folders"]
    },
    "allowUnbound": false,
    "allowQueryAuthorityDigest": false,
    "notBefore": 1800000000,
    "expiresAt": 1800003600,
    "revoked": false
  }
]
```

This is a value-free schema example, not an operational grant. Owners supply
current dates and actual identities. Revisions are positive, IDs are unique,
validity is at most 30 days and only one current nonrevoked grant may authorize
the complete request. Duplicate IDs/revisions and overlapping matches refuse;
requests cannot combine partial grants. No wildcard, default or implicit binding
exists. Strings, counts, nesting, duplicate JSON/form fields and extra fields are
bounded and rejected. Errors contain constant reason codes only. Never enable
HTTP request-body, form, token, credential, grant-value or signer-material logging.

Every request reloads protected realm/client attributes through Keycloak's normal
model. Replace a complete client grant array atomically, advancing its owner
revision; never append old and new revisions or restore old authority after
revocation. Verify readback on every server node. Keycloak's administrative cache
invalidation is required; this provider introduces no grant cache or file watcher.
Revocation blocks subsequent exchanges after that invalidation. Already-issued
tokens remain valid for their bounded lifetime: expiry is the minimum of 120
seconds, grant expiry, approved signer expiry and the active RSA certificate
expiry. Compromised signing keys require the owner's independent receiver trust
revocation procedure; changing grants does not invalidate an existing signature.

## Exact inventory for Folders qualification

Inventory every physical resource before catalog capture. A replica may share a
workload only when it has the same actual Dapr AppId and owner-approved identity;
the two proof Workers have distinct callers, credentials and RSA source keys.
Memories is outside this profile and is not enrolled or reconfigured by this change.
The run suffix is part of each actual AppId. Never reuse canonical credentials
under another run's caller, substitute another workload or infer grants from
`EnableResourceBindingRequests`.

| Caller | Receiver / operation | Separate permission |
| --- | --- | --- |
| Folders Server and each distinct Workers caller | EventStore / `eventstore:stream-read` | Exact tenant + `folders` bindings, metadata-only source authorization and independent RSA proof |
| EventStore | Actual Folders and Tenants receivers / process, project, metadata, replay where already required | Separate explicitly `allowUnbound` callback grants; preserve each approved operation |
| EventStore | Actual Folders and Tenants receivers / `domain-service:query` | Exact tenant + domain plus `eventstore:bound-query-authority-v1`, with `allowQueryAuthorityDigest` only for this grant |
| Tenants | EventStore / `eventstore:command-submit` | Separate existing bootstrap permission, independent command proof and catalog generation |
| Tenants | EventStore / `eventstore:stream-read` | Separate existing bootstrap/source permission and independent RSA proof |

Do not infer extra operations from this inventory. Reconcile it against the
actual approved cohort and include any additional existing workload sender or
receiver before activation. Human Keycloak authentication remains on
`Authentication:JwtBearer`; workload trust uses the supported dedicated
`Authentication:WorkloadJwtBearer` and each caller's
`Authentication:WorkloadIssuer` configuration. Source metadata-only restrictions,
sidecar channel proofs and query-envelope digests remain independent gates.

## Consumer enrollment artifact

The Platform consumer requires both `Folders:WorkloadEnrollment:ManifestPath`
and `Folders:WorkloadEnrollment:ManifestSha256`. Supply these references through
protected owner configuration outside source control. The SHA-256 approves the
exact artifact bytes, including the current complete grant-probe inventory and
client credential file references. A Boolean option does not approve enrollment.

The manifest uses schema `hexalith.workload-enrollment/v1`, HTTPS `authority`,
`issuer` and `tokenEndpoint`, current Unix-second `notBefore`/`expiresAt`, the
approved `signingKeyId` and uppercase `signingPublicKeySha256` (RSA SPKI), exact
`receivers` resource names and a `clients` array. Each client names its `resource`,
`clientId`, server-owned `workload`, absolute protected `secretFile` and `probes`.
Each probe names `grantId`, positive `revision`, `audience`, `operation`, and the
complete `bindings` object (empty for an approved unbound grant). Enroll every
actual workload resource; replicas require their own resource entry even when
sharing a physical caller. No credentials, grants or manifest examples containing
real values belong in Git or evidence.

The consumer verifies the pinned artifact, dates and exact final cohort, performs
normal platform TLS validation, checks metadata issuer and same-origin endpoints,
pins the current JWKS RSA SPKI and independently verifies a fresh signed token
for every probe, including exact audience/caller/operation/bindings and grant
identity/revision. Only then may it configure the complete cohort and enable the
SDK profile before catalog capture. SDKs and receivers still independently
validate every runtime assertion. No custom TLS bypass or borrowed workload is an activation path. Generated
trust and grants are allowed only through the explicit disposable run-owned
launcher below; persistent enrollment retains independent owner approval. Missing references
leave the profile disabled and name those missing inputs; invalid references fail
closed before catalog capture.

Resource coverage alone is insufficient. The consumer requires the complete exact
source tenant/domain set for every source resource, all existing unbound callback
operations for both actual domain-service audiences, separately bound query
digest probes for Folders and both Tenants domains (the latter use the SDK
normalized reserved storage tenant `system`), and separate Tenants unbound
bootstrap/source probes. The disposable fixture also checks its selected tenant
is included. Run-owned enrollment binds exactly one selected canonical tenant. Missing or unrelated probes refuse before any profile is enabled.

## Disposable run-owned enrollment

For the Folders acceptance fixture's random tenant and final Dapr callers, use
`infrastructure/keycloak-workload-binding/run_owned.py`. This is an explicit
**run-owned enrollment** authority. It supplies no Platform lifecycle qualification
or Folders 12.2 completion by itself. Persistent/shared/staging/production
installations still require the realm-owner process described above.

Prerequisites: Linux, Python 3, OpenSSL, Docker access, the exact
`quay.io/keycloak/keycloak:26.6.4` image and the built provider JAR. Build with
`mvn -B -ntp package` in the provider directory. Build the Platform Composition
and Folders AppHost test projects individually in Debug/source mode, including
the selected Tenants host and the existing observer prerequisites for strict
acceptance. Keep the selected source graph's target frameworks consistent;
resolve a mixed net10/net11 restore before interpreting any runtime result.
The launcher never builds, upgrades, publishes or mutates another authority.
The component verification's non-1000 portability control additionally requires
passwordless `sudo` and `setpriv` when the validation caller is UID 1000, with
permission to switch its private validation fixture to UID/GID 1001:1001 and
supply Docker socket group access. Root or an existing non-1000 Docker caller
can run that control directly. This requirement belongs only to component
validation; ordinary launcher use requires no UID switch or sudo authorization.

Launch a built test assembly directly so the test process retains the launcher's
recorded child identity. Supply the existing Folders integration opt-in as usual:

```bash
python3 -B infrastructure/keycloak-workload-binding/run_owned.py \
  --owner disposable-folders-acceptance --lifetime-seconds 900 -- \
  dotnet /absolute/path/to/Hexalith.Folders.AppHost.Tests.dll \
  -class Hexalith.Folders.AppHost.Tests.OperationalStatusDurabilityAcceptanceTests
```

Choose the fixed finite lifetime before launch (1–86,400 seconds). It is an upper
bound, never extended by adoption or retries. The fixture's existing four-minute
startup deadline and all strict acceptance budgets remain unchanged. Use one
launcher for one final cohort; a second adoption refuses. Do not combine generated
context with external `ManifestPath` or `ManifestSha256` configuration.

Before starting the child, the launcher creates a private 0700 run directory,
0600 files, a private CA and localhost certificate, and a process-scoped
`SSL_CERT_FILE` bundle that preserves inherited or system trust. It installs no
global trust. The child receives a one-use capability and private receipt bound
to the owner and child PIDs plus Linux boot/start identities, directory and fixed
deadline. Missing prelaunch trust refuses with `missing-prelaunch-trust`;
changing trust after xUnit starts is unsupported. A receipt copied to another
process/run, a changed tenant/cohort, or a hosted target refuses before activation.

After the fixture selects all physical hosts, replicas, proof Workers and its
tenant, the shared inventory produces the exact source, bootstrap, callback and
query permissions. Tenants queries use `TenantCanonicalizer.ReservedStorageTenant`
(`system`) after SDK normalization. Query digest permission remains dynamic;
the tenant and domain remain exact. Resource entries are never deduplicated.
Only replicas with the same actual workload and identical permissions share a
client/secret. Distinct proof callers keep separate credentials and the fixture's
existing independent RSA source proofs.

The launcher imports the complete realm at startup into a fresh container,
asks Docker to allocate a loopback port with `127.0.0.1::8443`, validates the
actual publication by owned container ID, and derives the HTTPS localhost
origin from that binding. Keycloak's [hostname-only setting](https://www.keycloak.org/server/hostname)
fixes `localhost` while resolving scheme and port from the HTTPS request.
The launcher pins the exact server version and existing provider digest, and
keeps the original Docker context or endpoint selection for the entire lease.
It discovers the RSA signer through normally validated HTTPS metadata/JWKS. It creates no workload administrative identity and makes no
administrative API calls. The unchanged consumer manifest schema contains only
owner-only secret references, and its validity, grants and signer approval are
bounded by the original deadline. Ownership metadata remains in the separate
receipt/result files. The existing consumer still verifies every TLS, inventory,
signer and fresh signed probe before enabling any host or capturing the catalog.

Each enrolled host receives prelaunch trust before startup. The operational
provider fixture merges this bundle with its Forgejo CA and existing system
roots; the workload helper propagates that merged bundle to all enrolled hosts.
Human authentication remains on its original authority and configuration.

Fixture disposal requests producer cleanup. Child success/failure, cancellation,
launcher signals, expiry and partial provisioning also remove the exact owned
container, manifest, credentials, realm import and certificates. Cleanup checks
the container's run label before removal and removes only the private owned
directory; unrelated resources survive. Child cleanup includes descendants that
start another session. TERM, INT, HUP, QUIT and ABRT initiate cleanup; subsequent
signals do not interrupt it. The bridge allows up to 30 seconds for cleanup
acknowledgement after expiry without extending enrollment validity.
The launcher reports cleanup failures as
`owned-container-cleanup-incomplete` or `owned-private-material-cleanup-incomplete`
and exits unsuccessfully. A failed cleanup must be resolved before claiming a
clean run. Evidence output is labeled `run-owned enrollment` and contains only
counts, version/digests, control names and observed leftovers; retain no private
run directory, credentials, tokens, grants, certificates or keys.

Run the focused controls from the provider directory:

```bash
python3 -B -m unittest test_run_owned
python3 -B verify_component.py --run-owned
python3 -B verify_component.py --run-owned --apphost-assembly /absolute/path/to/Hexalith.Folders.AppHost.Tests.dll
python3 -B verify_component.py --run-owned --platform-assembly /absolute/path/to/Hexalith.Platform.Composition.Tests.dll
```

The assembly commands launch explicit real-provider/consumer controls through
the same prelaunch receipt path and require the selected test to pass, with
observed adoption, provisioning and cleanup. Component validation requires
unoptimized Python execution. The Platform assembly also checks cancellation
after request publication while the child remains alive. It offers a focused bridge
and unchanged-consumer lane when the larger Folders source graph is blocked;
record that blocker separately from the focused result. The ordinary `verify_component.py` path retains its
component-only administrative mutation controls and must never be interpreted as
run-owned enrollment or persistent authority approval.
The component expiry control resolves the image before starting its fixed
eight-second lease, and its unrelated live control remains running until the
entire scenario set finishes. Caller identity and private-file modes in component
evidence are read from the actual container and host files.

## Verification, disable and cleanup

Run `python3 verify_component.py` after `mvn package` for an owned disposable
26.6.4 realm. Its generated TLS certificate, realm signer, clients and grants are
**component fixtures only**. The script verifies TLS mechanics, normal credential
validation, independent RSA signatures, exact refusals and live grant replacement;
it removes only its own container and temporary directory in `finally`. It does
not qualify the actual protected trust, enrollment authority or real-provider
Folders acceptance. Keep those evidence labels separate.

For explicit SDK and consumer component suites, add `--sdk-assembly` and
`--apphost-assembly` pointing to the corresponding built Debug test assemblies.
The consumer's temporary TLS fixture trust is scoped to that test child process.
Its complete fixture enrollment, invalid live probe, TLS, signer and incomplete
inventory controls establish consumer behavior only. The SDK's mismatched scope
and expiry controls use genuine Keycloak signatures from the same client; the
expiry token naturally expires under a two-second temporary component grant.

Actual enrollment qualification requires protected external owner references or
the explicit disposable run-owned path above, normally validated TLS and pinned
signer approval, fresh successful probes for the final cohort, a coherent
Debug/source build and runtime identity audit. Then run the
unchanged strict `OperationalStatusDurabilityAcceptanceTests`, including its
original 403 control and current source/Workers evidence. Preserve all original
deadlines, budgets, retries and bodies. Privacy, governance and performance gates
remain separate. Until those inputs and results exist, story 12.2 is incomplete.

To disable, remove the consumer references and set the protected realm enabled
attribute false, revoke grants and invalidate administrative caches on every
node. Check subsequent exchanges refuse, wait for the maximum outstanding token
lifetime, and apply the owner's signer trust revocation if necessary. Remove only
the task-owned provider mounts, disposable clients/realm, manifests, credentials,
TLS fixture files and containers after retaining metadata-only evidence. Never
delete another runtime's trust, credentials, grants or persistent data.

The SPI behavior is bound to [Keycloak 26.6.4 client credentials](https://github.com/keycloak/keycloak/blob/26.6.4/services/src/main/java/org/keycloak/protocol/oidc/grants/ClientCredentialsGrantType.java)
and its [OIDC mapper contract](https://github.com/keycloak/keycloak/blob/26.6.4/services/src/main/java/org/keycloak/protocol/oidc/mappers/AbstractOIDCProtocolMapper.java).
