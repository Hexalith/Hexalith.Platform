---
title: EXT-SECRETS-1 custody and current-inventory binding
type: feature
created: 2026-10-16
status: in-review
route: dispatch
baseline_commit: b2890d20575356239555df5b113e3100c1d594c1
review_loop_iteration: 0
scope_expansion_baseline_commit: 73903003acd33ba341e12aa3c495992bab8b0eda
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EXT-SECRETS-1 stays Committed. Provider, HMAC credential, tenant inventory, current-inventory authority, tenant A/B, profile, and revocation references are null. No `IDaprPlatformHmacAuthority` binding is installed.

**Approach:** Bind the isolated provider/current inventory on `192.168.1.30`. The owner’s 2026-10-27 continuation authorizes verifying/renewing the installed authority, successor versions for exactly the four expired isolated mappings, current separate restricted credentials, and an actual custody workload composed from the existing custody services, `IDaprPlatformHmacAuthority` and `AddExplicitHmac`. Enroll and mount only after current prerequisites are verified. Record public references and keep the dependency unavailable.

## Boundaries & Constraints

**Always:** Reuse Dapr 1.18 and `openbao/StatefulSet/hexalith-keys`. Use operator credentials only for provisioning, then a new restricted application credential. Preserve 2026-10-08, target `f448db29493c1b30b0f18380188fe99b8b7606fe`, L/S/O/H/R = 300/30/600/86400/604800, Open export disposition, and decision keys outside Platform and Agents. Bind discovered current facts; new authority identities/profile records must come from real authorized provisioning and audited owner administration. Authority query and administrator credentials and persistent storage must be separate from custody/provider/registry credentials. Profiles resolve from authority state, never recursively through inventory. Permit new isolated authority resources and narrowly scoped changes to isolated inventory/custody enrollment. Preserve the unrelated dirty checkout. A missing input keeps this task incomplete and names that input, its owner, and the next action.

**Never:** Fabricate pre-existing tenants, mounts, issuers, key versions, anchors, enrollment, independence, or Available status. Change original HMAC versions or validity records, or create successors outside the four explicitly authorized expired mappings. Never extend expired versions. Use operator or Memories credentials as application credentials. Change production workloads, keys, data, Memories, or existing OpenBao policies. Apply null proposed manifests. Edit the Agents register. Run Live or any contract case. Write credential values to git, evidence, or logs.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Required behavior | Failure |
| --- | --- | --- | --- |
| Facts present | HMAC mount, TLS name, and two distinct tenants | Install the isolated binding and fill those references | Stop before an invented field |
| Fact absent | Registry, mount, profile, or revocation missing | Leave that reference null and name input, owner, and action | No fixture authority |
| Production overlap | Production workload, existing key/policy, or Memories object | Leave it unchanged | No apply |
| Authority query | Authenticated enrolled caller and current persisted profile/exact scope | Return exact public contract; coherent stable revision and proof TTL at most 30 seconds | Missing, stale, revoked, foreign, unenrolled or unavailable state denies |
| Owner administration | Separate authenticated administrator, original operation ID and expected revision | Persist profile/enrollment/revocation plus audit atomically; identical retries return original outcome | Unauthorized, conflicting, changed-payload or uncertain storage fails closed |
| Expired keys | Exactly the four original isolated version-1 mappings are expired | Preserve original material and validity records; generate one new successor per mapping inside the provider with CAS and retained operation journals; verify metadata before enrollment | No expired-version extension, extra mapping, blind repeat or key-value export |
| Current custody binding | Current provider credentials, authority profile, exact successor mappings and actual host identities | Compose existing custody services with `IDaprPlatformHmacAuthority` and `AddExplicitHmac`; authenticate and enroll separate inventory/custody identities, then mount current credential/CA references | Missing or uncertain prerequisite leaves that binding uninstalled and records exact input/owner/action |
| Validation | Installed object or missing input | Metadata and health only | Live stays unrun |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Custody/DaprPlatformHmacBinding.cs` — reuse `IDaprPlatformHmacAuthority`. Do not change timing or secret handling.
- `src/Hexalith.Platform.Secrets.Runtime/DaprHmacAuthorityAdapter.cs` — unchanged profile/inventory client. New server belongs in the separate authority host, never in this adapter.
- `src/Hexalith.Platform.Secrets.Runtime/SecretsRuntimeConfiguration.cs` — reference allowlist. Do not add fields.
- `src/Hexalith.Platform.Secrets.Runtime/SecretsDependency.cs` — owner action for each missing input.
- `eng/ext-secrets-1/runtime-reference.example.json` — schema and settled constants, not installed configuration.
- `eng/ext-secrets-1/dapr-openbao.component.proposed.yaml`, `openbao-hmac.policy.proposed.hcl`, `tenant-inventory.example.json` — shape only. Do not apply nulls.
- `eng/ext-secrets-1/provisioning/owner-input-map.json` — other role owners. `apply` stays false.
- Agents `external-dependency-register.md` EXT-SECRETS-1 row — Committed. Do not edit.
- Evidence `isolated-runtime-20261016/runtime-reference.json` — update verified fields only. Keep prior `discovery.json` and packet.
- Evidence `isolated-runtime-20261016/discovery.json` — OpenBao ready, no owner component. Re-query before binding.

- `src/Hexalith.Platform.Secrets.Authority/` — new .NET 10 ASP.NET host: persisted coherent profile/host/caller/exact-version revocation state, owner administration, query evaluator, authenticated HTTP composition; no provider data or decision signer.
- `tests/Hexalith.Platform.Secrets.Authority.Tests/` — xUnit v3/Shouldly; actual loopback HTTP/adapter and persistence/restart/concurrency tests.
- `src/Hexalith.Platform.Secrets.Inventory/{InventoryHttp,InventoryWorkloadToken,AuthenticatedSecretsInvoker,DaprHmacCurrentTrust}.cs` — reuse exact query/authentication contracts. JWT must validate HTTPS issuer/audience, enrolled caller and operation plus app-channel authentication. Distinct authority app ID prevents recursive profile reads.
- `eng/ext-secrets-1/{install_isolated_registry,provision_registry_identity,preload_isolated_image}.py` — reuse sanitized transient credential transport, create-only resources, public preservation snapshots, verified OIDC and exact image preloading. Existing registry IdP may authenticate a new separately scoped realm; registry identity grants no secrets authority.
- `eng/ext-secrets-1/install_hmac_authority.py` — new bounded journaled installer; owner-approved admin operations establish profile/enrollment facts; new token/Secret names, unchanged existing policies. Never reuse token issuers that delete preserved policies on failure.
- `Hexalith.Platform.slnx`, `eng/ext-secrets-1/README.md` — register projects and document actual protocols, maintenance, installation facts and limits.

- `src/Hexalith.Platform.Secrets.Custody/` — actual isolated ASP.NET custody workload using existing `AddPlatformCustody`, `IDaprPlatformHmacAuthority` and `AddExplicitHmac`; authenticated receiving composition and metadata/health endpoints. No new lifecycle provider or decision signer.
- `tests/Hexalith.Platform.Secrets.Custody.Tests/` — focused source composition/authentication/fail-closed tests without installed contract/cohort calls.
- `eng/ext-secrets-1/bind_isolated_custody.py` and `test_bind_isolated_custody.py` — bounded successor-only journaled continuation, separate data/metadata credentials, owner administration and prerequisite-gated enrollment/mounting; preserve original journals and use new evidence. Reuse sanitized transports and image preloading, never invoke the original key installer or its policy-deleting token issuer.

## Tasks & Acceptance

**Execution:**
- [x] `custody-inventory-20261016/discovery.json` — read-only Kubernetes, Dapr, OpenBao mount/TLS, and tenant-registry names. References only.
- [x] Namespace `ext-secrets-1-isolated` — apply a read-only policy, restricted token, and `secretstores.hashicorp.vault` component only when mount, TLS, and two tenants were discovered. Mount token and CA by reference.
- [x] `custody-inventory-20261016/tenant-inventory.json` — publish discovered tenant A/B and current opaque-key to numeric KV2 mappings, or leave it unenrolled.
- [x] `isolated-runtime-20261016/runtime-reference.json` — set provider, credential, inventory, authority, tenant A/B, issuer, audience, profile version, validity, and revocation only from facts. Set `callTimeout` to 30 only with an installed provider. Preserve host, provider, timing, and Open disposition.
- [x] `custody-inventory-20261016/blockers.json` — each remaining required null: input, owner, and next action.
- [x] `custody-inventory-20261016/health.json` — component, namespace, and OpenBao health metadata. No secret bytes and no Live command.

- [x] `src/Hexalith.Platform.Secrets.Authority/` — implement authenticated persisted authority and separately authorized owner administration; deny incomplete state and exact-scope/revision/validity mismatches.
- [x] `tests/Hexalith.Platform.Secrets.Authority.Tests/` — cover every new matrix scenario, durable restart, lost-response retry, conflicting concurrent admin operations and storage errors; run focused tests.
- [x] `eng/ext-secrets-1/install_hmac_authority.py` — implement and execute bounded isolated provisioning with durable original journals, pinned image, reference-only evidence and preservation checks.
- [x] `Hexalith.Platform.slnx`, `eng/ext-secrets-1/README.md` — register host/tests and document administration, observed installation, safe credential replacement and unresolved gates.
- [x] New evidence `authority-installation-<timestamp>/` — retain authenticated admin receipts, independent credential/storage boundary, native metadata, per-null blockers and health; bind only actual current references. Withhold custody activation while expired keys or absent actual custody composition remain.

- [x] `eng/ext-secrets-1/bind_isolated_custody.py` — inspect original authority/key/credential/resource journals and current installed metadata/health; verify original authority receipts against authenticated state and renew the profile through a new revision-checked operation only if needed.
- [x] Same continuation — provision current separately restricted data and metadata credentials using existing unchanged policies; generate exactly four successor versions inside OpenBao, CAS against verified current versions, append mappings preserving original validity records, and retain original native write receipts. Unknown outcomes stop for exact reconciliation; never repeat a key write.
- [x] `src/Hexalith.Platform.Secrets.Custody/`, its focused tests and solution registration — implement the actual custody composition and fail-closed authenticated workload; build and run focused source tests.
- [x] Same continuation — enroll actual UID-qualified inventory/custody host and caller identities and exact current key statuses through authenticated owner administration; install/mount credential and CA references only after current prerequisites are verified. Preserve production and other pre-existing resources; only narrowly scoped isolated inventory/authority/custody updates are authorized.
- [x] New `custody-binding-<timestamp>/` evidence and this spec/README — retain public journals, receipts, successor metadata, profile and enrollment references, image/source provenance, metadata/health, preservation and credential-exposure checks; update only verified public runtime fields and record each remaining blocker with exact input, owner and next action. Do not run Live or installed contract/cohort cases or claim availability.

**Acceptance Criteria:**
- Given mount, TLS, and two distinct tenants, when installation finishes, then the new namespace holds the provider and restricted credential reference, and production objects are unchanged.
- Given a missing registry, mount, profile, or revocation source, when binding is attempted, then that reference stays null and the blocker names input, owner, and next action.
- Given any result, when evidence is written, then it has no credential values, the register stays Committed, and Live remains unrun.

- Given exactly four expired isolated mappings, when successor provisioning runs, then exactly one provider-generated current successor is created for each verified original, old material/validity is preserved, unknown writes are reconciled without replay, and no key value leaves the provider.
- Given current authority/profile, separate credentials, exact mappings and authenticated actual hosts, when custody binding is installed, then `IDaprPlatformHmacAuthority` and `AddExplicitHmac` use enrolled references and missing prerequisites fail closed without an availability claim.
- Given the approved new authority scope, when an authorized owner publishes actual profile/enrollment/revocation records, then a separately authenticated and persisted service serves the existing exact contracts, and restart preserves revisions and original administrative outcomes.
- Given separate isolated resources and preserved production/keys/policies, when installation finishes, then public references identify actual installed objects and owner-administered state, while absent/expired key prerequisites keep affected consumers unbound.

## Implementation Notes

### 2026-10-27 explicit isolated custody continuation authorization

Implementation necessity under the authorized binding: installed source metadata checks proved the existing provider ingress excludes both isolated consumers. A new additive, run-owned Kubernetes NetworkPolicy may select only the existing `hexalith-keys` server pods in `openbao` and allow only the combined namespace `ext-secrets-1-isolated` plus pod app labels `inventory` or `ext-secrets-1-custody` on TCP/8200. This supplies the route needed by the explicitly authorized binding; it changes no existing NetworkPolicy, OpenBao ACL policy, workload, service or Memories object. Verify actual selectors/endpoints, use create-only UID-journaled provisioning, and include the new policy in preservation evidence. No broader ingress or other namespace source is authorized.


Before implementation, the owner explicitly authorized: (1) verify the installed profile/revocation authority and renew its profile through authenticated owner administration if needed, preserving existing operation receipts; (2) create successor versions for only the four expired HMAC mappings in `ext-secrets-1-isolated`, preserving original versions and validity records and never extending expired versions; (3) provision current, separately restricted data and metadata credentials; (4) implement the actual custody workload using existing custody services, `IDaprPlatformHmacAuthority` and `AddExplicitHmac`, enroll inventory/authority identities, and mount credentials/CA references only after verified current prerequisites. This supersedes the earlier successor/key-creation exclusion and “outside approved scope” statements below for these four mappings only. Implementation and bounded isolated installation are authorized without another approval checkpoint. All other constraints remain.

Original journals were inspected before any new provisioning: authority run `ba457c222fcb483ebd114ca34157b7df` is `InstalledConsumerEnrollmentBlocked`; original PublishProfile `2d12030ed8bc4060b44c5f737ad4c05d` and SetCaller `681ac86f05434a7eb4a4226bc490153f` committed revisions `:1` and `:2`; observer issuance ended `RevokedAndOwnedSecretRemoved`; image update is `Verified`. Original `provider-key-installation-20261017T100804405564Z/operational-key-plan.json` records all four mappings `MetadataVerified` at numeric version 1. Its first uncertain write was reconciled against native audit request `db61ddba-116a-d60b-0f39-e8e5022363d5` and exact metadata, without repeating the key write. The later `...103315423057341Z` attempt failed at existing component-name overlap. Preserve all originals, including failure/recovery records, and re-query current state; these historical facts alone do not authorize a repeat.

Evidence root is `/home/administrator/hexalith-secrets-evidence/ext-secrets-1`. Installed authority evidence is `authority-installation-20261027T173655237972Z`; public runtime reference is `isolated-runtime-20261016/runtime-reference.json`. Target remains `192.168.1.30`, namespace `ext-secrets-1-isolated`, mount `ext-secrets-1-isolated/`, tenants `01M54GTX47NH1PRB00T2YS5048` and `01M54GTX479F4Y9A3MRW578C4R`, purposes `TrustedEnvelope` and `ContentDigest`, each under `tenants/<tenant>/<purpose>/operational`. Existing original key installer is reference-only; never run it. Credential issuance must not call helpers that delete existing policies on failure. Use durable creation/issuance/native-write journals and original-operation reconciliation. Do not export key data, credential values or hashes of credential values. Keep all historical evidence, settled timing, production/Memories workloads, existing policies and Agents register unchanged.


### Approved scope expansion

The project owner approved the concrete authority proposal with “apply recommended” on 2026-10-27. [Approval evidence](../../../../../hexalith-secrets-evidence/ext-secrets-1/custody-inventory-20261016/authority-discovery-20261027T171614Z/approval.json) and the prior spec are retained. This supersedes the earlier no-server restriction and proposal-only no-deployment status; existing key/production/policy, decision-signer, register and Live exclusions remain. Implement and install authorized portions without another approval checkpoint. A missing usable HMAC key does not justify skipping the independent service implementation, tests or safe provisioning.

Re-queried `192.168.1.30` at `2026-10-16T17:20:46Z`. OpenBao `hexalith-keys` is unsealed at 3/3 ready and Dapr is 1.18.1. The server certificate and the existing Memories vault components name TLS `hexalith-keys.openbao.svc.cluster.local`. No KV mount is an HMAC mount: `secret/` contains `hexalith/memories` and `tls/`, and `deployment-seal-custody/` list is denied. Memories Redis has no `tenant-registry-index`. The install gate failed, so `ext-secrets-1-isolated`, a new policy, a restricted token, and the vault component were not created. `runtime-reference.json` is unchanged: host, provider, timing, and Open disposition stay as recorded, and the unbound fields stay null. `callTimeout` stays null. The dependency stays unavailable. Live was not run.

### 2026-10-27 current-fact refresh

Fresh [discovery](../../../../../hexalith-secrets-evidence/ext-secrets-1/custody-inventory-20261016/refresh-20261027T170637456955Z/discovery.json) supersedes the historical installation observations above. Later work already installed namespace `ext-secrets-1-isolated`, KV2 mount `ext-secrets-1-isolated/`, and custody-scoped `secretstores.hashicorp.vault` component `ext-secrets-1-hmac`. Its TLS name remains `hexalith-keys.openbao.svc.cluster.local`; token and CA are file references. Existing names were preserved without apply, new policy, or token issuance. Current restricted registry reads verified both original tenant enrollments; no tenants were created.

Updated only `providerBinding`, `callTimeout` (30), `tenantA`, `tenantB`, and the UID-qualified `tenantInventoryReference` in `isolated-runtime-20261016/runtime-reference.json`. The [refreshed reference](../../../../../hexalith-secrets-evidence/ext-secrets-1/custody-inventory-20261016/refresh-20261027T170637456955Z/runtime-reference.json) and its pre-change copy are retained with the new evidence. Current HMAC-data and metadata credentials both return native HTTP 403 from lookup-self. The HMAC credential reference therefore stays null; denied metadata reads establish no current opaque/numeric mappings, and the inventory stays Unenrolled. Independent inventory/profile/revocation references, issuer, audience and profile validity stay null. No custody workload binding was discovered. Inventory health reports `InstalledUnbound` and readiness is HTTP 503.

The [blockers](../../../../../hexalith-secrets-evidence/ext-secrets-1/custody-inventory-20261016/refresh-20261027T170637456955Z/blockers.json) name an owner and next action for all 52 remaining null references, plus current key mappings and custody workload enrollment. The provider owner must return current separately restricted credential and metadata references; the independent authority owner must supply profile/revocation facts; the inventory and custody workload owners must enroll the actual host, mounted references and authority registration. Existing policies, keys and workloads must remain preserved. Status stays `in-progress`, the installation task stays unchecked, and the dependency remains unavailable.

The [verification](../../../../../hexalith-secrets-evidence/ext-secrets-1/custody-inventory-20261016/refresh-20261027T170637456955Z/verification.json) confirms all 926 observed Kubernetes objects and all OpenBao mount/auth/policy fingerprints unchanged; OpenBao is unsealed at 3/3 ready and Dapr remains 1.18.1. Reference allowlisting, settled constants, per-null owner/actions and literal/base64 credential-exposure checks passed. Original discovery, detached packet and command bytes, Agents register and unrelated checkout paths were preserved. The historical packet was not regenerated or accepted against the changed reference. Verification used metadata, native tenant-enrollment reads and health only: no key-data read, Live invocation or contract case ran.

### 2026-10-27 authority implementation and installation

Implemented the separate .NET authority host, atomic persisted state/audit, strict JWT and channel authentication, revision-checked owner administration, and original-operation recovery. The new installer retains original provisioning journals, uses new credential objects and the unchanged existing metadata policy, and preserves pre-existing resource identities. The authority resolves profiles directly from its own state, avoiding the inventory recursion. Query/profile/revocation protocols and settled timing remain compatible with the existing clients. The new image is `registry.hexalith.com/ext-secrets-1-hmac-authority@sha256:1a8d238003e6a689eb2fd9ba77bc2e78aae35ec3c1fdf5068ac9c8d08fc3feb2`.

[Installation evidence](../../../../../hexalith-secrets-evidence/ext-secrets-1/authority-installation-20261027T173655237972Z/verification.json) records a ready 1/1 StatefulSet and its own bound persistent volume, separate authority realm/query/administrator credentials, and profile/caller records committed through authenticated owner administration. This establishes credential, domain-state and administration separation within the existing cluster; no independent physical or rollback-resistant authority is claimed. A real rollout preserved revision `e19e68c9537246148141ec9d3841cfe4:2`, the full audit and both original administrative receipts. The newly published profile expires at `2026-10-28T17:37:33.663741Z`; profile renewal is owner-managed and no controller was installed.

Updated only eight newly verified authority fields in the public runtime reference: profile reference, public trust reference, revocation reference, issuer, audience, version and validity bounds. Existing tenant/provider fields and constants were preserved. Current restricted metadata reads confirm all four original version-1 mappings expired on 2026-10-24. Their metadata and key material were unchanged. The query caller remains disabled, no inventory host or exact key status is enrolled, and no authority credential is mounted into a consumer. Actual custody executable/composition remains absent. HMAC data credentials and current-inventory authority stay null; [44 remaining nulls and additional enrollment blockers](../../../../../hexalith-secrets-evidence/ext-secrets-1/authority-installation-20261027T173655237972Z/blockers.json) retain owner/actions. Key replacement/rotation remains outside the approved scope. The existing provider-binding task therefore remains unchecked and overall status stays `in-progress`.

Verification passed 65 authority tests, 55 existing inventory tests, 11 installer tests and 3 preload tests. Parent inspection and [17 coordinator checks](../../../../../hexalith-secrets-evidence/ext-secrets-1/authority-installation-20261027T173655237972Z/coordinator-verification.json) independently verified source/image provenance, live metadata/health, preservation, persisted restart outcome, per-null blockers and unchanged constants. All 926 pre-existing Kubernetes objects, OpenBao mount/auth/policy fingerprints, original key metadata and the Agents register were preserved. Literal/base64 credential scanning found zero matches; the temporary observer was revoked and its Secret removed, and temporary image-preload credentials/Pods were removed. No Live or installed contract case ran. Full Build review/completion remains gated by the incomplete custody/provider binding.

## Spec Change Log

### 2026-10-27 isolated custody binding implementation

[Continuation evidence](../../../../../hexalith-secrets-evidence/ext-secrets-1/custody-binding-20261027T182648692778Z/) retains all original journals, four CAS=1 native write request IDs/receipts, exact original and successor metadata, separate current credential references, eight authenticated enrollment receipts, image/source provenance and every failed preflight/recovery observation. Exactly one numeric version-2 successor was generated inside OpenBao for each authorized expired mapping. Original version-1 native records and opaque validity mappings are unchanged. Effective retention allows ten versions and has no automatic deletion. A new exact-scope temporary provisioner policy/token supplied permissions absent from the operator identity; its token was revoked and its owned Secret/policy removed without changing existing policies.

The existing authority profile and original two administration receipts were verified against authenticated state. Its profile remains current until `2026-10-28T17:37:33.663741Z`, so renewal was unnecessary. Revision `e19e68c9537246148141ec9d3841cfe4:10` now includes actual UID-qualified inventory/custody hosts, separate enabled query callers and four exact successor statuses. The new custody host uses `AddPlatformCustody`, `IDaprPlatformHmacAuthority` and `AddExplicitHmac`; it adds no lifecycle provider or decision signer. Runtime references now bind the current restricted data credential and `ext-secrets-1-inventory`. All remaining 42 null references retain exact owners and next actions in `blockers.json`.

Both public runtime files use regular `subPath` mounts under canonical `/run/...` paths, as required by the shipped no-follow file reader; original failed mount/update journals are preserved. The new additive provider ingress policy is `openbao/NetworkPolicy/ext-secrets-1-custody-provider-29a4ace57e04@uid:4d6c461e-6c19-42fd-a9d1-bf3995200d6b`. It permits only the approved combined namespace/app selectors on TCP/8200. It changes no existing policy, workload or service. Metadata-only source diagnosis established successful authenticated profile/trust/native-enrollment reads and identified the previously denied provider route before that correction.

At `2026-10-27T18:45:25Z`, custody, inventory and authority health/readiness all returned HTTP 200. Preservation checked 939 pre-existing Kubernetes objects: only the four explicitly authorized isolated references changed; no unexpected original object changed. Existing OpenBao mount/auth/policy fingerprints, original journals, old key metadata/validity and the Agents register are preserved. Literal/base64 credential scanning found zero matches. Focused verification passed 26 custody tests, 56 inventory tests and 19 continuation tests, plus Release container publishing. No provider key-data read, installed cryptographic operation, Live or contract/cohort case ran. Dependency availability and qualification remain false; status stays `in-progress` pending the root Build review/completion phase.

The continuation began against scope-expansion baseline `73903003acd33ba341e12aa3c495992bab8b0eda`. A concurrent owner version-control operation advanced shared HEAD to `9ead86f9061695bf3e30bcb0e020c0b830ddaf7e`; this implementation neither staged nor committed that operation and preserved the shared checkout. Exact source/image observations are retained separately from the settled target `f448db29493c1b30b0f18380188fe99b8b7606fe`.

Four reused source files changed concurrently after the recorded build snapshot: `SecurityEventRecorderLookup.cs`, `IndependentAuthorityHttpApplication.cs`, `IndependentAuthorityReceiver.cs` and `IndependentAuthorityReceiverInstallation.cs`. The installed immutable images contain the recorded build outputs; current new host/HMAC entry sources match that snapshot. The evidence explicitly records the broader checkout divergence and exact build/publish file inventory. No unrelated code was rebuilt or reverted. A source-only follow-up moves provider ingress creation after original-journal/current-prerequisite and preservation checks, including on recovery; two focused ordering tests passed. The completed installer was not rerun for that source-only change.

### 2026-10-27 explicit successor and actual custody authorization

Recorded the owner’s four-part continuation instruction before implementation. Frozen intent, successor-only matrix and actionable tasks now reflect that authorization. Prior authority/key journals were read; original receipts and audit recovery remain historical evidence. Baseline and unrelated dirty work are preserved.

### 2026-10-27 owner clarification and authority discovery

The user confirmed they are the owner and do not know whether the required authority service exists. A fresh read-only [authority discovery](../../../../../hexalith-secrets-evidence/ext-secrets-1/custody-inventory-20261016/authority-discovery-20261027T171614Z/result.json) inspected 24 namespaces, 102 workload objects and 65 services, plus the relevant Platform, Agents, Tenants, EventStore and Memories source. No usable independently enrolled profile/revocation authority was found within that boundary. All installed authority references remain null; inventory health reports no independent profile and readiness 503. Existing Keycloak authentication does not implement the missing domain contracts. The prior runtime-foundations handoff explicitly records the services as missing.

The [proposed next scope](../../../../../hexalith-secrets-evidence/ext-secrets-1/custody-inventory-20261016/authority-discovery-20261027T171614Z/next-scope.proposed.json) describes a new authenticated current-profile/revocation authority and isolated consumer enrollment. It is unapplied and requires an explicit scope expansion because this spec only binds discovered facts and excludes adding a server. No frozen intent, existing workload, key, policy or runtime reference was changed by this discovery. No Live or contract operation ran; this spec remains incomplete.

### 2026-10-27 approved authority expansion

The owner's “apply recommended” instruction authorizes the new authority implementation and bounded isolated installation/enrollment described in the retained proposal. The frozen intent, matrix and tasks above now reflect that approval. Prior discovery-only evidence and proposal remain historical and are not rewritten.

## Review Triage Log

## Design Notes

HEAD `b2890d20575356239555df5b113e3100c1d594c1` stays dirty with unrelated files. New evidence is `custody-inventory-20261016` under the existing EXT-SECRETS-1 evidence root. Namespace `ext-secrets-1-isolated` already contains preserved objects. The approved new authority uses independent app identity, credentials and durable state. Rollback may delete only this run's newly created UID-qualified resources, never the existing namespace, workload, key or policy. Preserve public original journals on uncertain effects.

## Verification

The [coordinator verification](../../../../../hexalith-secrets-evidence/ext-secrets-1/custody-inventory-20261016/refresh-20261027T170637456955Z/coordinator-verification.json) passed 16 independent evidence checks. The matrix audit passes missing-input, production-overlap and metadata/health behavior; the facts-present installation row remains blocked by the unverified current credential and missing custody workload binding. Build has not advanced to review or completion.

**Commands:**
- `kubectl get namespace,component.dapr.io -n ext-secrets-1-isolated` and `kubectl get statefulset hexalith-keys -n openbao` — expected: new objects only after a real apply; OpenBao replicas unchanged.
- Parse `runtime-reference.json` — expected: settled timing and Open disposition unchanged; new fields match discovery; missing fields stay null.
- `git status --short` — expected: the same unrelated dirty paths, plus this spec.
