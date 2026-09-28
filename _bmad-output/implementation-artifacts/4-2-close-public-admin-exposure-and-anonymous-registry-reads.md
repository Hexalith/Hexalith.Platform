---
title: 'Close public admin exposure and anonymous registry reads'
type: 'story'
epic: 4
story: 2
created: '2026-09-28'
status: 'in-progress'
route: 'dispatch'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/evidence/epic-4/initial-cluster-inventory.md'
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

- [x] Record the sanitized ingress and external-probe baseline.
- [ ] Create `evidence/epic-4/4-2/<attempt-id>/` in the access-controlled evidence store and record DNS, ingress resource UIDs/config digests, source address category, timestamps and operator without cookies, tokens or response bodies.
- [ ] Immediately before each route, registry-auth or GC mutation, re-read the affected resource UID, resourceVersion, effective config digest, DNS answer and ingress/backend identity. Abort and produce a fresh inventory/approval if any value differs from the signed attempt baseline.
- [ ] Build the approved Administrator path in parallel with public access. Prove two authorized operators can reach Keycloak administration and the required cluster administration surface, and prove an unauthorized client on that path is refused.
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

- `admin-path-proof.json`: approved path identity, tested operators, resource/config digests, positive private checks, negative external checks and public OIDC regression checks.
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
