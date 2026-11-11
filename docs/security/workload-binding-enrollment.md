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
validate every runtime assertion. No custom TLS bypass, generated signer trust,
borrowed workload or generated grant is an activation path. Missing references
leave the profile disabled and name those missing inputs; invalid references fail
closed before catalog capture.

Resource coverage alone is insufficient. The consumer requires the complete exact
source tenant/domain set for every source resource, all existing unbound callback
operations for both actual domain-service audiences, separately bound query
digest probes for Folders and both Tenants domains, and separate Tenants unbound
bootstrap/source probes. The disposable fixture also checks its selected tenant
is included. Missing or unrelated probes refuse before any profile is enabled.

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

Actual enrollment qualification requires the protected owner references,
independent TLS and signer approval, fresh successful probes for the final
cohort, a coherent Debug/source build and runtime identity audit. Then run the
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
