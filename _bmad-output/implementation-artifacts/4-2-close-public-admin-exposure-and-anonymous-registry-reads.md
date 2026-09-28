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
  1. Enumerate every Kubernetes `imagePullSecret`/ServiceAccount consumer, node/runtime pull path, Forgejo workflow, deployment executor and human/operator client that reads `registry.hexalith.com`.
  2. Issue scoped read credentials through the approved secret store, never Git. For each consumer, pull a known digest and record consumer identity, digest and success without recording the credential.
  3. Stop if any active or rollback consumer is absent from the inventory or cannot pull by digest.
- [ ] Disable anonymous catalog, tag, manifest and blob reads in Zot while retaining the standards-compatible unauthenticated `/v2/` challenge behavior if required. Prove anonymous catalog/tag/manifest/blob requests are refused and every inventoried authenticated consumer still pulls its recorded digest.
- [ ] Protect retained digests from garbage collection:
  1. Materialize the signed retained-digest set from the approved release records and configure Zot retention/GC so those manifests and referenced blobs are excluded from deletion.
  2. Run a dry-run or disposable-repository rehearsal that includes retained and explicitly disposable content.
  3. Run GC only after the rehearsal proves retained content survives, then pull every retained digest with an authenticated reader and record the content digest.
- [ ] Sign `admin-exposure-result.json`, `registry-auth-result.json` and `registry-gc-result.json`; commit only sanitized summaries.

## Evidence outputs

- `admin-path-proof.json`: approved path identity, tested operators, resource/config digests, positive private checks, negative external checks and public OIDC regression checks.
- `registry-consumer-inventory.json`: consumer identities and successful digest pulls, with only Secret names/references where needed and never Secret data.
- `registry-auth-result.json`: anonymous status results for catalog/tag/manifest/blob plus authenticated digest-pull results.
- `registry-gc-result.json`: signed retained-digest-set hash, rehearsal outcome, GC configuration digest and post-GC digest pulls.

Full probe headers and operational records remain access controlled. Do not retain authorization headers, cookies, tokens, passwords, client secrets or registry credentials.

## Stop conditions

- Do not close a public administrative route until the replacement Administrator path passes positive and break-glass checks.
- Stop and roll back the route change if approved public Keycloak realm/OIDC flows fail.
- Do not disable anonymous reads until every current and rollback consumer has proved an authenticated digest pull.
- Stop registry cutover if any anonymous catalog, tag, manifest or blob read still succeeds after policy change.
- Do not enable/run garbage collection without a signed retained-digest set and a successful rehearsal. Stop if any retained digest cannot be pulled afterward.
- Never classify a rate limit, login page or redirect as closed exposure; the external request must be refused or not routed.

## Acceptance criteria

**Given** the public Keycloak administration/master-realm routes and KubeSphere console
**When** this story completes
**Then** they are usable only through the approved Administrator path
**And** external public probes prove every administrative path is unreachable

**Given** every registry consumer has proved an authenticated digest pull
**When** Zot anonymous read is disabled
**Then** anonymous catalog, tag, manifest and blob reads are refused
**And** every inventoried consumer continues to pull its required digests

**Given** the signed retained-digest set
**When** registry garbage collection runs
**Then** every retained manifest and referenced blob survives
**And** authenticated post-GC digest pulls prove preservation
