---
title: 'Close public admin exposure and anonymous registry reads'
type: 'story'
epic: 4
story: 2
created: '2026-09-28'
status: 'in-progress'
baseline_commit: '904e0f18736575d0605d08da6252b33bd0202224'
route: 'dispatch'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/evidence/epic-4/initial-cluster-inventory.md'
review_loop_iteration: 1
depends_on: []
---

# Story 4.2: Close public admin exposure and anonymous registry reads

As Administrator,
I want Keycloak and cluster administration plus registry reads removed from anonymous public access,
So that live administrative and artifact exposure does not persist while staging is built.

## Scope and current state

Read-only discovery at 2026-09-28T12:51:19Z observed:

- Public-class Keycloak ingresses on `auth.tache.ai`: `Ingress/keycloak-ingress` routes `/`, `Ingress/keycloak-admin-rate-limit` routes `/admin`, and `Ingress/keycloak-master-token-rate-limit` routes the master-realm token endpoint. Anonymous external probes reached the admin redirect and master realm.
- `Ingress/kubesphere-console` in `kubesphere-system` publishes `kube.hexalith.com` through `nginx-public`; an anonymous external probe reached `/login`.
- `Ingress/distribution-registry-ingress` publishes `registry.hexalith.com`. Anonymous `/v2/`, catalog and repository tag-list requests returned HTTP 200.

These are observed exposures, not completed acceptance. Do not remove an administrative route before a tested replacement Administrator path exists, and do not disable anonymous registry reads before every consumer proves authenticated pulls.

## Administrator decisions and dependencies

The Administrator must approve:

1. The private administration path and its operators (for example a named VPN/private network or restricted ingress), including break-glass access and monitoring. The path must cover Keycloak admin/master-realm administration and KubeSphere if the console remains installed.
2. Which Keycloak realm endpoints must remain public for non-administrative clients. Closing administration must not break approved public OIDC flows.
3. The inventory of in-cluster and external registry consumers, credential owner/rotation policy, and the cutover window.
4. The signed retained-digest source of truth and Zot garbage-collection policy. If retained digests cannot be enumerated and tested, garbage collection remains disabled.

## Ordered tasks

The approved sole-Administrator correction below supersedes the earlier two-operator requirement and all historical two-operator handoff statements. Previously collected attempts remain unchanged.

- [x] Record the sanitized ingress and external-probe baseline.
- [x] Create `evidence/epic-4/4-2/<attempt-id>/` in the access-controlled evidence store and record DNS, ingress resource UIDs/config digests, source address category, timestamps and operator without cookies, tokens or response bodies.
- [ ] Immediately before each route, registry-auth or GC mutation, re-read the affected resource UID, resourceVersion, effective config digest, DNS answer and ingress/backend identity. Abort and produce a fresh inventory/approval if any value differs from the signed attempt baseline.
- [ ] Build the approved Administrator path in parallel with public access. Prove the sole named Administrator (`jpiquot`) can reach Keycloak administration and the required cluster administration surface, and prove an unauthorized client on that path is refused. Independently qualify native cluster and Keycloak break-glass login/non-destructive reads using separately protected recovery access while ordinary credentials and public OIDC are unavailable to an isolated recovery test client/session; production accounts/public OIDC remain available to other clients. Repeat recovery checks after closure. No second operator or deputy is required under the approved correction below.
- [ ] Restrict Keycloak administration:
  1. Preserve only Administrator-approved public realm/OIDC endpoints.
  2. Move or deny `/admin`, `/admin/*`, master-realm administration and master-realm token access on `nginx-public`; cover the catch-all `Ingress/keycloak-ingress`, not only the rate-limit ingresses.
  3. From the Administrator path, prove admin login and one non-destructive administration read. From an external public probe, require refusal or non-routing for every closed path.
  4. Re-run approved public client authentication smoke tests to prove non-administrative realms still work.
- [ ] Restrict the cluster console:
  1. Confirm whether KubeSphere is retained. If retained, serve it only through the approved Administrator path; if removed under an approved operations change, retain equivalent command-line administration through that path.
  2. Require external probes of `kube.hexalith.com` and any replacement public hostname to fail closed while authorized administration remains usable.
- [ ] Prepare authenticated Zot reads before changing policy:
  1. Establish a signed registry-change generation and either freeze changes to ingress/Zot configuration, workload image references, release/rollback sets, credentials, writers and replication until cutover completes or repeat the full inventory immediately before mutation.
  2. Enumerate every Kubernetes `imagePullSecret`/ServiceAccount consumer, node/runtime pull path, Forgejo workflow, deployment executor and human/operator reader; every publication/operations writer; and every replication/off-site robot that accesses `registry.hexalith.com`.
  3. Issue least-privilege credentials through the approved secret store, never Git. Prove readers cannot push/delete, writers can push only their approved repositories and cannot delete retained content, and replication identities can perform only the approved source/destination operations.
  4. For each reader and replication path, pull a known digest from a disposable client with an empty content store (or evict only that test digest from its disposable cache) and correlate the request with Zot audit logs showing the authenticated principal and manifest/blob transfer. A cached container start is not proof.
  5. Stop if any live, rollback, publication or replication consumer is absent, changes after the signed generation, or cannot perform its least-privilege operation.
- [ ] Disable anonymous catalog, tag, manifest and blob reads in Zot while retaining the standards-compatible unauthenticated `/v2/` challenge behavior if required. Prove anonymous catalog/tag/manifest/blob requests are refused and every inventoried reader/writer/replicator still passes its uncached audited operation under the unchanged generation.
- [ ] Protect retained digests from garbage collection:
  1. Starting from current live workload digests, every rollback-set digest and every signed retained-release record, traverse the full OCI reachability closure: image indexes/manifest lists, every platform child manifest, configs, layers, artifact manifests/referrers, signatures, attestations and retained Helm/chart artifacts. Sign the resulting object set and registry generation.
  2. Configure Zot retention/GC so every object in that closure is excluded from deletion.
  3. Run a dry-run or disposable-repository rehearsal that includes retained and explicitly disposable content.
  4. Acquire the approved registry-wide write/replication lock (or an equivalent atomic repository generation that prevents concurrent mutation), verify the live generation equals the signed generation, run GC, and release the lock only after verification. Abort on any concurrent write or generation change.
  5. From empty disposable content stores, pull every retained index/manifest and each reachable platform child/referrer; correlate authenticated audit events and record returned content digests before releasing the lock.
- [ ] Sign `admin-exposure-result.json`, `registry-auth-result.json` and `registry-gc-result.json`; commit only sanitized summaries.

## Evidence outputs

- `admin-path-proof.json`: explicitly approved sole-Administrator policy, named Administrator, independent recovery path/custody and measured native cluster/Keycloak recovery checks with ordinary credentials and public OIDC unavailable only to the isolated recovery test client/session; resource/config digests, positive private checks, negative external checks and public OIDC regression checks.
- `registry-consumer-inventory.json`: reader, writer and replication identities, least-privilege results and uncached audited operations, with only Secret names/references where needed and never Secret data.
- `registry-auth-result.json`: signed configuration/generation identity, anonymous status results for catalog/tag/manifest/blob plus uncached authenticated reader/writer/replication results.
- `registry-gc-result.json`: signed reachability-closure hash, write-lock/generation evidence, rehearsal outcome, GC configuration digest and uncached audited post-GC pulls.

Full probe headers and operational records remain access controlled. Do not retain authorization headers, cookies, tokens, passwords, client secrets or registry credentials.

## Stop conditions

- Do not close a public administrative route until the replacement Administrator path passes positive and break-glass checks.
- Stop and roll back the route change if approved public Keycloak realm/OIDC flows fail.
- Do not disable anonymous reads until every current and rollback consumer has proved an authenticated digest pull.
- Stop on baseline UID/resourceVersion/config/DNS drift unless the inventory and approval are regenerated; stop registry cutover on any un-frozen consumer, writer, replication or release/rollback-set change.
- Stop registry cutover if any anonymous catalog, tag, manifest or blob read still succeeds after policy change.
- Do not enable/run garbage collection without a signed full OCI reachability closure, successful rehearsal and held write/replication lock or equivalent atomic generation. Stop on a concurrent write or if any retained/reachable object cannot be fetched afterward from an empty client cache.
- Never classify a rate limit, login page or redirect as closed exposure; the external request must be refused or not routed.

## Acceptance criteria

**Given** the approved sole-Administrator model and separately protected recovery access
**When** ordinary credentials and public OIDC are unavailable to an isolated recovery test client/session during a controlled qualification while production accounts/public OIDC remain available to other clients
**Then** the same named Administrator can independently authenticate and perform non-destructive native cluster and Keycloak administration reads
**And** measured recovery checks pass before and after public route closure; a second account or copied everyday credentials alone cannot satisfy recovery

**Given** the public Keycloak administration/master-realm routes and a retained KubeSphere console
**When** this story completes
**Then** they are usable only through the approved Administrator path
**And** external public probes prove every administrative path is unreachable

**Given** KubeSphere is removed by the approved operations change
**When** this story completes
**Then** its ingress, service/workload and public DNS route are absent and external probes fail closed
**And** the approved command-line Administrator path remains usable; console usability is not required

**Given** every registry reader, writer and replicator has passed its least-privilege operation from an uncached client under the signed current generation
**When** Zot anonymous read is disabled
**Then** anonymous catalog, tag, manifest and blob reads are refused
**And** every inventoried reader, writer and replicator repeats its approved least-privilege operation with correlated audit evidence

**Given** the signed live/rollback/release OCI reachability closure and a held write/replication lock or equivalent atomic generation
**When** registry garbage collection runs
**Then** every retained index, platform manifest, config, layer and artifact/referrer survives
**And** uncached authenticated post-GC pulls with correlated audit evidence prove preservation

## Approved management handoff — 2026-10-01

The [approved Rancher correction](../planning-artifacts/sprint-change-proposal-2026-10-01.md) retains this story’s private-CLI/removal option. Story 4.27 owns retirement; this story owns public exposure closure and external negative tests. Close existing public administrative routes independently of Rancher installation. Revalidate exact route/DNS/UID state; preserve private authorized administration and approved public OIDC. Story 4.28 qualifies the later private Rancher UI/API/proxy and issued-credential path before accepting that endpoint. Keep registry-consumer, anonymous-denial and retained-digest GC criteria and `in-progress` status.

## Administrator decision — 2026-10-03

Close the public `kube.hexalith.com` KubeSphere console route now, before Story 4.27 retirement, by deleting the console Ingress or restricting it with a Traefik IP allowlist. Until retirement, use the console only through `kubectl port-forward`. Retirement later removes the remaining route objects. This records the decision only: the production change needs its own go, and the route's DNS record is removed separately.

## Local implementation handoff — 2026-10-06

The [4.2 preparation helper](../../eng/admin-exposure/prepare.py) and [operations procedure](../../eng/admin-exposure/README.md) now create immutable owner-only private attempts with unsigned pending templates for every required output. They also check externally supplied detached Administrator SSH signatures and the consistency of resource/DNS/backend identities, approved route coverage, two-operator/private/break-glass proof, pre/post-change administration and OIDC, complete consumer scopes, uncached audited operations, OCI reachability, retention, rehearsal and GC lock sequencing. Local checks never authorize production mutation or independently establish operational acceptance. The existing 4.26/4.27 executors and exact-byte rehearsal bindings are unchanged.

[Local verification](evidence/epic-4/4-2/20261006t093207z-local-preparation/verification.json) records 26 passing synthetic boundary tests, including actual local verification of ephemeral SSH signatures. A private preparation attempt exists at `evidence/epic-4/4-2/20261006t093207z-local-preparation/` within dedicated owner-only custody outside Git. Its operational records remain unsigned, pending and unaccepted; their presence is not a completed ordered task or production proof. No cluster/DNS/HTTP/registry discovery, credential issuance, route change, policy cutover or GC occurred.

The reviewable early console procedure deletes only the freshly inventoried public Ingress through a native DELETE bound to **both UID and resourceVersion**, preserves a loopback-only private port-forward and independent native administration, and requires post-change external refusal/private access/OIDC checks. All fresh resource/configuration/DNS/route identities and private-path proofs still need collection and a separate exact-attempt production go. Its accepted production result must produce a new `console-closure.json` bound to the exact 4.27 retirement attempt/plan/source cluster and signed under `hexalith-retirement`; the pending template cannot unblock retirement. Console acceptance can proceed independently while other 4.2 criteria remain open.

Remaining Administrator inputs are the named Keycloak/private administration path, **two authorized operators**, break-glass/monitoring and approved public OIDC checks; complete registry reader/writer/replicator and live/rollback inventory, secret-store owner/rotation and cutover window; and the signed retained-digest source plus Zot retention/GC policy. The story's two-operator requirement remains in force unless explicitly corrected. Missing exhaustive retained source, rehearsal or held registry-wide writer/replication lock leaves GC disabled. Story and sprint status stay `in-progress`; all uncompleted live tasks and acceptance criteria remain pending.

## Local approval-boundary hardening — 2026-10-06

The Administrator delegated routine design choices for private administration. The selected design uses existing native CLI administration plus loopback-only SSH tunnels and `kubectl port-forward`, preserving existing non-master realm/OIDC client flows. Actual endpoint/context/host bindings, the exact public-flow inventory, **two verified existing authorized operators**, break-glass/monitoring and measured access proofs remain pending. No operator identities or successful checks were invented, and this design delegation is not the separate exact-attempt production go.

The preparation checker now binds that prior go to completed private-path or registry-consumer proof bytes. GC approval additionally binds the exact retained-closure record and successful rehearsal evidence. Approval must follow the completed baseline/prerequisites and precede a newly collected pre-mutation checkpoint and the mutation; approval or proof collected afterward fails. Pre-cutover audit operations must name their inventoried consumer. Post-GC preservation proof accepts only authenticated reader/source-replication fetches, rejecting writer pushes and destination replication. Signed records with duplicate JSON keys or a shared attempt-directory mode are refused. The operations procedure documents the added bindings and sequence.

[Local verification](evidence/epic-4/4-2/20261006t094705z-approval-boundaries/verification.json) records 33 passing synthetic boundary tests, including actual local SSH verification of ephemeral signatures and regressions that previously accepted a late go, late prerequisites, ambiguous signed JSON and non-read GC operations. A new immutable owner-only preparation attempt exists in dedicated custody outside Git. Its operational templates remain unsigned, pending and unaccepted. No remote discovery, credential issuance, route change, registry policy cutover or GC occurred; no live task or acceptance criterion was marked complete. The existing `baseline_commit`, `in-progress` status and exact-plan signed 4.27 handoff requirement are preserved.

## Authorized read-only discovery and proposals — 2026-10-06

The Administrator answered **yes** to fresh live read-only discovery. The [discovery summary](evidence/epic-4/4-2/20261006t102707z-change-proposals/summary.md) records compatible direct-native reads, fresh DNS/Ingress UID/resourceVersion/backend/configuration projections, the current Traefik compatibility provider, workload/ServiceAccount and local workflow references, and read-only Keycloak realm/client metadata. The evidence-custody/discovery task is complete; it is unsigned inventory, not a signed mutation baseline or operational acceptance.

Both private loopback transports were tested and cleaned up. Authentication, two authorized operators, break-glass, public OIDC regression and independent external routing acceptance are unproved. Current anonymous catalog/tag/manifest/blob reads succeed from the operator workstation. Zot is already configured for daily GC; the approved retained-source/rehearsal/lock prerequisites remain absent and no GC action was taken.

Exact console DELETE/restoration, candidate Keycloak public-path/obsolete-admin-route requests, and a separate scheduled-GC pause proposal are reviewable in dedicated private custody. All execution/approval/acceptance flags are false. The preserved `tache` realm has 11 client configurations; that inventory is not a successful authentication flow. No route, registry configuration, DNS, credential, grant or workload was changed. Read-only authorization does not supply the separate production go, and the story's two-operator requirement remains pending. Keep story and sprint `in-progress`.

## Approved sole-Administrator correction — 2026-10-06

The Administrator instructed **“do recommended”** after reviewing the two-operator alternative and the recommendation to use one named Administrator plus independent tested recovery. This explicitly corrects this story to the existing sole-owner/no-deputy management policy. `jpiquot` is the selected Administrator identity; actual per-system account bindings and authenticated checks still need qualification. There is no claim of a second person, newly issued credential or completed recovery test.

Use existing private native CLI access, temporary loopback-only port-forwards and Keycloak Admin CLI/API access. Reuse the approved separately protected native emergency access and qualify an independent Keycloak recovery procedure. Recovery must work with ordinary credentials and public OIDC unavailable only to an isolated recovery test client/session, cover authenticated native cluster and Keycloak non-destructive reads, and pass again after closure. Production accounts/public OIDC remain available to other clients; qualification does not disable, revoke or mutate them. Recovery custody, path identity, monitoring, timestamps and sanitized evidence digests must be bound to the exact signed attempt. This protects credential/authentication lockout; the sole Administrator remains the only person available to intervene.

Apply this correction in `eng/admin-exposure/prepare.py`, `eng/admin-exposure/test_prepare.py` and `eng/admin-exposure/README.md`: require an explicitly approved sole-Administrator policy and exactly one named tested Administrator before and after closure; require measured independent recovery for both administration surfaces before and after closure. Keep unauthorized refusal, public OIDC regressions, fresh UID/resourceVersion/configuration/DNS checkpoints, exact prerequisite hash/signature/approval ordering, registry-consumer and GC gates, and the exact signed 4.27 handoff unchanged. Empty templates and local synthetic checks remain unaccepted for production. Historical private attempts and committed evidence are immutable; create a new preparation/verification attempt for this correction.

This is requirements/design approval and authorizes the corresponding local implementation and qualification preparation. It is not a route/configuration/credential/grant/DNS mutation go. Story and sprint remain `in-progress` until actual operational criteria pass.

## Sole-Administrator implementation handoff — 2026-10-06

The preparation checker, pending templates and procedure now implement the approved `sole-administrator` policy for `jpiquot`. Signed private-path and result records require exactly one tested Administrator, approved native cluster/Keycloak account bindings and independently protected recovery custody. Both the prior `breakGlass` proof and the new `postChangeBreakGlass` result must prove native cluster authentication/non-destructive reads and Keycloak login/non-destructive reads while ordinary credentials and public OIDC are unavailable to the isolated recovery test client/session, with sanitized measurement and credential-independence evidence hashes. Production accounts/public OIDC stay available to other clients. Another account or copied everyday access alone cannot satisfy recovery. Unauthorized refusal, normal access, OIDC regressions, prior exact-hash approval/checkpoint sequencing, registry/GC gates and the existing signed 4.27 handoff remain required.

[Local verification](evidence/epic-4/4-2/20261006t105730z-sole-administrator-verification/verification.json) records 42 passing synthetic tests with zero skips and a retained verbose test-log hash. A new immutable owner-only preparation attempt uses the corrected pending schema; previous private attempts and committed receipts remain unchanged. All operational template acceptance/signature fields remain pending, and local verification does not establish production authentication or recovery. This implementation performed no remote discovery or production mutation. Fresh signed private/recovery qualification, exact-attempt production go, route denial/OIDC acceptance and the registry/retained-content criteria remain incomplete; story and sprint stay `in-progress`.

## Local recovery-session isolation hardening — 2026-10-06

The preparation checker now explicitly binds both pre-closure and post-closure recovery qualification to an identified isolated client/session. Each recovery record requires `qualificationScope: isolated-client-session`, a sanitized `testSessionId`, measured isolation and production-availability evidence hashes, continued production-account/public-OIDC availability to other clients, and unchanged production authentication during qualification. Successful emergency access during a global authentication outage cannot satisfy the approved isolated-session qualification. New pending templates expose these fields with null values; every historical private attempt and committed receipt remains unchanged.

[Local verification](evidence/epic-4/4-2/20261006t112439z-recovery-isolation-verification/verification.json) records 45 passing synthetic tests with zero skips, including real local SSH verification and rejection of signed recovery records reporting a production OIDC outage before or after closure. A new immutable owner-only private preparation attempt and retained verbose test log were created outside Git. Signatures and supplied evidence hashes still require independent review of the actual protected measurements. No remote operation, production mutation, credential change, route closure, registry cutover or GC occurred; all operational acceptance remains pending and story/sprint stay `in-progress`.

## Local GC checkpoint sequencing hardening — 2026-10-06

The preparation checker now requires the complete fresh GC checkpoint collection to occur after acquiring the registry-wide write/replication lock and before GC starts. Both a checkpoint completed before lock acquisition and a collection that straddles acquisition are refused, including when all supplied records have valid detached Administrator SSH signatures. This enforces the ordered task's acquire-lock, verify-signed-generation, run-GC sequence while preserving exact baseline/prerequisite/approval bindings and the held-lock post-GC fetch gate. The operations procedure documents the required collection interval; local checks still cannot independently establish actual lock enforcement or measurements.

[Local verification](evidence/epic-4/4-2/20261006t115747z-gc-lock-verification/verification.json) records 47 passing synthetic tests with zero skips and retained verbose test-log hashes. The new sequencing regressions reproduced the prior acceptance gap before the fix, then passed with the gate tightened. A new immutable owner-only preparation attempt was created outside Git; historical attempts and committed receipts remain unchanged. No remote discovery, route/DNS/configuration/credential change, registry cutover or GC occurred. Fresh signed administration/recovery qualification, separate exact-attempt production go, external denial/public OIDC proof, complete registry-consumer operations and retained-content GC acceptance remain pending. Story and sprint remain `in-progress`.


## Local preparation verification contract — review correction 2026-10-06

These requirements authorize local implementation and synthetic qualification only. Existing live tasks, production approval, independent measurement/acceptance and the exact signed 4.27 handoff remain pending. Preserve all historical attempts and receipts, including the 47-test GC receipt, with their original byte identities and meaning.

- In `eng/admin-exposure/prepare.py`, require repository scopes, Ingress hostnames and Ingress paths to be nonempty arrays of valid strings, and require the affected-route approval map to be an object. Reject malformed signed structures through the fixed failure report. For console changes, bind every approved public console hostname to its signed DNS/route/backend snapshot and require the approved phase UID set to equal the union of those route UIDs. Preserve exact baseline drift checks.
- Bind unauthorized private refusal to both administration surfaces. Add `privateAdministrationTargets` to signed decisions: exactly `nativeCluster` and `keycloak`, each with the approved `hostname`, absolute `path` and HTTP `method`. Add `unauthorizedPrivateChecks` to private proof and `postChangeUnauthorizedPrivateChecks` to the admin result, each containing exactly one check per surface. Each check carries `surface`, the approved `privatePathId`, and the exact approved hostname/path/method, plus the existing measured refusal fields and timestamp. Pre-change checks precede approval; post-change checks follow closure. Targets may be the approved access gateway when it gates the corresponding surface. Leave actual bindings and checks pending in templates; do not invent production endpoints or successful tests.
- Require `capturedAt` in every least-privilege measurement. Pre-cutover consumer permission tests fall within the inventory observation interval; post-cutover permission tests fall within the result interval and after mutation completion. Preserve each role's permission and audited-operation requirements. Update the synthetic fixtures so their permission checks actually occur in the corresponding interval.
- For Keycloak, require GET and POST refusal for every `closedAdminPaths` entry, including discovered encoded, normalized and trailing-slash aliases. This covers the existing known master token endpoint without inferring methods from a string suffix. Continue requiring reviewed effective wildcard/controller rules and independent operational coverage; method probes alone cannot establish complete route closure.
- Revalidate all signed records against the current clock at check completion so expiry during SSH verification cannot produce a pass. Refuse `access_token`, `refresh_token` and `id_token` field spellings under the existing normalization, alongside existing prohibited fields. Keep report values sanitized and every authorization/acceptance/completion flag false.
- Preserve the GC sequencing correction: `lock.acquiredAt <= checkpoint.observationStartedAt <= checkpoint.capturedAt <= gcStartedAt`, followed by the existing held-lock verification/release checks. Keep sole-Administrator/recovery-isolation, exact hash/prior approval, public OIDC and registry-generation gates.
- In `eng/admin-exposure/test_prepare.py`, add meaningful direct and real SSH-signed regressions for the demonstrated schema, scope, private-target, stale-permission, GET/POST alias, expiry and token-field failures. Add correctly signed false/absent `productionGo` rejection and failed pre/post public OIDC rejection tests. Keep all prior boundary coverage, adapting fixtures to the explicitly strengthened pending contract.
- Update `eng/admin-exposure/README.md` to document these fields and sequencing. Run the focused suite with zero skips, CLI help and diff checks. Create a new owner-only immutable private preparation and sanitized local verification receipt bound to the final source/test/log bytes; prior receipts remain unchanged. Report any remaining operational inputs without marking live tasks or story acceptance complete.

## Spec Change Log

- 2026-10-06 review correction: demonstrated unbound private refusal targets, stale permission timestamps and GET-only token alias evidence require an explicit local verifier contract. Added approved target bindings, pre/post refusal and permission timing, and GET/POST coverage to avoid deriving unsupported production identities or token methods. KEEP: existing sole-Administrator and isolated recovery checks, exact prior approval/hash/drift gates, offline-only behavior, pending templates, immutable historical evidence, the GC lock-before-checkpoint fix and held-lock retained-read checks. Local code was reset to its continuation-start bytes before re-derivation; historical receipts retain their original identities. Production status stays `in-progress`.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| Blind 1: unrelated private denial target | medium | bad_spec | Reproduced acceptance after changing the nested denial hostname/path to an unrelated endpoint. The old contract lacks approved per-surface target bindings; the correction defines them. |
| Blind 2: replacement console hostname absent from baseline | medium | patch | Reproduced accepted negative probes for an added approved hostname while `target_baseline` validates only the primary host. All approved hostnames can be bound using existing route and UID arrays. |
| Blind 3: anonymous content not tied to a consumer pull | maybe-false | defer | The contract requires signed known-existing content backed by protected authenticated probe evidence, which may include collector operations beyond the representative consumer pull. Inspect that protected evidence to determine whether a purported target is actually unpulled/nonexistent; the supplied synthetic consumer list alone cannot settle it. |
| Blind 4: stale least-privilege timestamps | medium | bad_spec | Reproduced accepted post-cutover permission records dated in 2000. The permission schema previously omitted time; the correction defines observation and post-mutation bounds. |
| Blind 5: duplicate digest in multiple repositories | maybe-false | defer | The signed closure contract enumerates content digests; actual retained repository/location requirements and registry retention semantics are not present in the diff. Inspect the signed retained source, Zot policy and repository-specific fetch evidence to establish whether a location can disappear while required content preservation passes. |
| Blind 6: rehearsal generation/configuration identity | maybe-false | defer | Prior approval binds the exact protected rehearsal evidence hash, and those protected bytes are unavailable to this review. The proposed extra hash fields are not part of the existing rehearsal schema. Inspect the approved rehearsal record to determine whether its actual source/configuration/closure differs before designing additional automatic bindings. |
| Blind 7: non-object affectedRouteUids crash | medium | patch | Reproduced an uncaught AttributeError from a list-valued route map; validate the existing object contract before lookup. Same defect as Edge 3. |
| Blind 8: missing proposed request hash | false | reject | The helper issues no mutations and always returns mutationAuthorized=false. Exact request/restoration review and dispatch belong to the separate approved operations procedure; this checker does not claim to authorize a selected DELETE or patch. No caller in this change consumes its pass as mutation authority. |
| Blind 9: numeric checkpoint age/dispatch reread | maybe-false | defer | The helper checks signed collection/mutation order and expiry; the procedure separately requires a dispatch reread. No numeric maximum age is specified. An approved operational age/dispatch evidence policy and actual dispatch records are needed to establish the claimed stale-operation violation. |
| Blind 10: Custody solution/tests absent | medium | defer | Both Custody projects are absent from the solution and the test project has no tests. They were introduced by the separate existing custody commit 711a70fd94794ef1aea1b91f5524136b0d660468 and have no caller in this exposure work. |
| Edge 1: string repository scope | medium | patch | Reproduced a simple repository substring being allowed by a string-valued scope. Require the existing scope to be an array before exact membership. |
| Edge 2: expiry during verification | medium | patch | The clock is sampled once before sequential SSH checks, so completion can occur after expiry. Recheck freshness at completion against the current clock. |
| Edge 3: non-object affected route map | medium | patch | Same demonstrated uncaught list.get failure as Blind 7; one typed-map guard fixes both outcomes. |
| Edge 4: string Ingress hostname/path arrays | medium | patch | Reproduced substring hostname acceptance and `/admin` passing the `/` catch-all membership test. Validate the existing Ingress array contracts. |
| Edge 5: GET-only token aliases | medium | bad_spec | Reproduced acceptance of an encoded trailing-slash token alias with only GET refusal. The contract now requires GET and POST for every declared closed administrative path. |
| Edge 6: credential-bearing token field names | medium | patch | Direct checks accepted access_token, refresh_token and id_token keys. Extend the existing prohibited-field set under its normalization. |
| Verification 1: false/absent signed production go | medium | patch | Pre-verified mutation test removed the productionGo condition while all 47 tests still passed. Add correctly signed negative evidence reaching that exact gate. |
| Verification 2: failed signed OIDC result | medium | patch | Pre-verified mutation test removed the result=pass condition while all 47 tests still passed. Add signed failed pre/post checks with valid prerequisite byte bindings. |

| Corrected Blind 1: unprobed alternate Keycloak hostname | medium | patch | The existing signed Ingress/routes can name another public host on an approved Keycloak route while admin_result checks only the primary host. Bind denial coverage to every concrete hostname on those approved routes using the existing snapshot fields. |
| Corrected Blind 2: reader out-of-scope denial | maybe-false | defer | The stated reader policy requires push/delete denial; out-of-scope read policy is not specified as a reader permission field. Inspect the approved credential/read-scope policy and protected negative probes to establish whether broader reads violate that policy before adding a new mandatory field. |
| Corrected Blind 3: extra permission principal fields | maybe-false | defer | Permission proofs are nested in their consumer/generation context and bind protected evidence hashes; an extra principal field is not part of the current permission schema. Inspect the actual negative-operation evidence and its actor/scope to establish misbinding before changing that schema. |
| Corrected Blind 4: contradictory shared reader/writer grant | medium | patch | A reader and writer with the same principal, same credential reference and overlapping repository scope claim both push denial and successful publication under one generation. Reject that demonstrated contradiction without preventing separately scoped/credentialed operation entries. |
| Corrected Blind 5: writer transfer digest absent | false | reject | Writers intentionally use audited push operations with approved repository, principal, correlation/evidence and successful result; requested/returned digest transfer fields are required for uncached reads. The story's writer acceptance concerns publication permission and retained-delete denial, not verification of a particular published digest. |
| Corrected Blind 6: valid long DNS hostname rejected | medium | patch | hostname() admits DNS names up to 253 characters, while route indexing still uses identifier()'s 128-character limit. Use the hostname validator consistently for route identities. |
| Corrected Blind 7: substituted signature verifier | maybe-false | defer | /bin/true demonstrates the existing dependence on the externally selected retained verifier, just as verification depends on the externally supplied trust root. Determine the independently approved verifier/toolchain identity to establish an actual unapproved substitution; the helper reports its hash and never supplies mutation authority. |
| Corrected Blind 8: nonfinite JSON constants | medium | patch | Python's permissive parser accepts signed NaN/Infinity values outside standard JSON. Reject nonfinite constants and serialization within the existing JSON contract. |
| Corrected Blind 9: unsupported schema version | medium | patch | The tool emits and prepares version-one records but does not reject signed unsupported/malformed version metadata. Require the existing integer schemaVersion=1 at the common record gate. |
| Corrected Blind 10: revoked HMAC snapshot | medium | defer | The separate pre-existing custody component admits Revoked metadata and its internal ComputeTag has no state guard. It has no exposure-helper consumer; track its lifecycle validation with that component rather than editing it in this story. Same root cause as Corrected Edge 4. |
| Corrected Blind 11: HMAC use outside lifetime | medium | defer | The separate custody component's internal ComputeTag checks disposal but not its declared validity interval. Its eventual custody caller/provider must qualify use-time lifetime enforcement; this component predates this exposure change and has no caller here. |
| Corrected Edge 1: unrelated anonymous target | maybe-false | defer | Carried: same location/claim and unchanged target semantics as Blind 3 in the initial pass. Protected authenticated probe evidence behind knownExistingContent/evidenceSha256 is required to settle it; representative consumer pulls alone are insufficient. |
| Corrected Edge 2: recursive signed JSON crashes | medium | patch | Parsing or traversing sufficiently nested signed JSON can raise RecursionError outside the current fixed-error handlers. Refuse it through the existing sanitized input failure path. |
| Corrected Edge 3: approved wildcard ingress rejected | medium | patch | The strengthened Ingress validator admits wildcard patterns, but target matching still uses exact membership. Match approved hosts to valid single-label wildcard patterns while retaining exact resource/DNS/drift and namespace/UID guards. |
| Corrected Edge 4: revoked HMAC snapshot | medium | defer | Same independent custody defect as Corrected Blind 10; one deferred entry carries both reported outcomes. |
| Corrected Verification 1: retained-delete denial regression | medium | patch | Pre-verified mutation removed the writer/replicator retainedDeleteDenied condition while all 72 tests passed. Add signed false/missing measurements for both roles before and after cutover with valid prerequisite bindings. |
| Corrected Verification 2: CLI failure exit regression | medium | patch | Pre-verified mutation returned zero for rejected evidence while all 72 tests passed. Exercise the real check command with signed valid and rejected bundles and assert return codes and fixed report fields. |

## Local verification-contract implementation handoff — 2026-10-06

The preparation checker and pending templates now implement the review correction: signed decisions bind both native cluster and Keycloak private refusal targets; pre/post records require exactly one measured refusal per surface with matching path identity, hostname, absolute path, HTTP method and collection order. Ingress hostname/path and repository scopes require nonempty valid string arrays, the affected-route map requires an object, and every approved console hostname binds its DNS/route/backend snapshot with an exact union of approved route UIDs. Least-privilege measurements require timestamps within their inventory/result intervals, with post-cutover tests after mutation completion. Every declared Keycloak administrative path and discovered alias requires both GET and POST refusal. The checker rejects normalized access/refresh/ID-token fields and rechecks signed-record expiry at completion. The existing sole-Administrator, isolated recovery, prior exact-hash approval, drift/OIDC/registry gates, GC lock-before-checkpoint and held-lock retained-read sequencing, and exact signed 4.27 handoff remain required.

[Local verification](evidence/epic-4/4-2/20261006t122405z-verification-contract/verification.json) records 72 passing synthetic tests with zero skips, including real local SSH verification for the strengthened schema, console-host bindings, private-target mismatches, stale permissions, GET/POST token aliases, expiry during verification, prohibited token fields, false/absent production go and failed pre/post public OIDC. The simple scalar repository-scope fixture reproduces substring acceptance in the original checker with real detached signatures, then fails the corrected array guard. Final source/test bytes, the verbose test log and a new owner-only immutable private preparation are bound by the sanitized receipt; prior attempts and receipts, including the initial local review-correction draft, retain their original bytes and meaning. All new operational target/proof/signature/acceptance fields remain pending. No remote discovery, credential issuance, route/configuration/grant/DNS mutation, registry cutover or GC occurred. Fresh operational inputs, independent measured qualification/acceptance and the separate exact-attempt production go remain incomplete; story and sprint stay `in-progress`.

## Local review verification — 2026-10-06

The second review's patch findings are resolved: denial covers every concrete hostname bound to the approved affected Keycloak Ingress UIDs, with exact DNS/route/backend bindings and GET/POST checks for every declared closed path. Valid long DNS names and single-label wildcard Ingress rules are supported without relaxing identifier, namespace, UID or drift checks. A shared reader/writer principal and credential cannot claim push denial and successful publication on overlapping repository scopes. Signed input requires integer schema version 1, rejects nonfinite JSON constants and reports excessive nesting through the existing fixed failure report.

[Final local verification](evidence/epic-4/4-2/20261006t124852z-review-verification/verification.json) records 87 passing synthetic tests with zero skips, including real SSH signatures, nested false/missing retained-delete denial for writers and replicators before and after cutover, and actual CLI success/failure exit codes. The parent ran the complete focused suite, all three CLI help commands and the diff check after the implementation agent's targeted checks. A new owner-only immutable private preparation and retained log bind the final source/test/story bytes. Historical attempts and receipts remain unchanged.

All patch findings from both review passes were corrected; refuted claims and carried findings retain their logged verdicts. Ten grouped findings requiring protected operational evidence or work on the pre-existing Custody component are recorded in the [deferred-work ledger](deferred-work.md); none is asserted to be resolved by local tests. Local verification remains unsigned and production-unaccepted, with mutation authorization, operational acceptance and completion false. No remote operation or production mutation occurred. Actual private/recovery qualification, independent external denial/public OIDC evidence, fresh signed baseline and separate production go, complete registry consumer operations and retained-content GC acceptance remain pending. Story and sprint remain `in-progress`; live tasks are unchanged.
