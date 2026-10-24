- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-4-0-4-3-start-urgent-kubernetes-upgrade-track.md`
  summary: Restore the completed `stepsCompleted` markers in the Epic plan.
  evidence: Both review lenses found that concurrent `/pushall` commit `63af0c72` changed the completed marker list to `[]`, which can make planning automation treat the artifact as unvalidated.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-4-0-4-3-start-urgent-kubernetes-upgrade-track.md`
  summary: Reconcile the accepted Kubernetes 1.34 staging risk with the requirement to upgrade before staging exists.
  evidence: Pre-existing `epics.md` AR-64 permits staging on 1.34 after 2026-10-27 while Story 4.1 requires a supported minor before staging; one sequence must become authoritative.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-4-0-4-3-start-urgent-kubernetes-upgrade-track.md`
  summary: Make recovery-job credential cleanup fail safe on abnormal executor termination.
  evidence: Concurrent AR-28 text promises destruction or revocation at job end but does not define a short lease or out-of-band finalizer for a crashed or killed job.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-4-0-4-3-start-urgent-kubernetes-upgrade-track.md`
  summary: Assign every unlisted Kubernetes object kind to exactly one release tier.
  evidence: Concurrent AR-32 text requires a unique tier but does not classify RBAC, ServiceAccounts, CRDs, PDBs or admission objects, allowing ownership and rollback behavior to diverge.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-4-0-4-3-start-urgent-kubernetes-upgrade-track.md`
  summary: Add router-level coverage for the canonical Tenants workspace URLs.
  evidence: Existing tests verify aliases or render `TenantsWorkspace` directly; neither `/tenants/tenants` nor `/tenants/workspace-users` is routed through the production assembly, so the generic landing-page catch-all can win unnoticed.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/4-0-prove-off-node-backups-and-isolated-restores.md`
  summary: Make Story 4.1's mutation gate also verify the Administrator-signed, passing Story 4.0 `validation.json`, not only `backup-gate.json` and the three proofs.
  evidence: Story 4.0 signs `backup-gate.json` before `validation.json` exists, and Story 4.1's gate item 2 plus the 4.0 evidence contract check only the gate and proofs, so a failed validation blocks the upgrade only through the sprint-status `done` flag (review finding B3, 2026-10-01).
  status: resolved
  resolution: Story 4.1 now requires signed passing fresh final validation with exact gate/manifest/policy/recovery-ID bindings independently of sprint status. Its preparation tool always closes mutation authorization, and operational attempt 20261001t075120z-backup-verified directly verifies those bindings/signatures and retains signed gate-validation.json; see evidence/epic-4/4-1/20261001t075120z-backup-verified/summary.md. Remaining hop gates are still closed.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/4-0-prove-off-node-backups-and-isolated-restores.md`
  summary: Give the CloudNativePG barman-cloud plugin a credential scoped to the `keycloak/` prefix instead of the general recovery writer key.
  evidence: Secret `keycloak/recovery-writer-s3` holds the writer key inside the source workload's namespace, so that workload can add new latest versions under `openbao/`, `memories/` and `evidence/`; object lock keeps existing versions immutable and `validate.py` would fail on a forged latest record, but it is an avoidable tamper and denial path (review finding B15b, 2026-10-01).
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/4-0-prove-off-node-backups-and-isolated-restores.md`
  summary: Complete the renumbering note in `implementation-readiness.md` and pin the superseded 2026-09-28 report by commit.
  evidence: The note says two renumberings apply, but the same sprint-status diff also shifts 5.9→5.10, 5.10→5.11 and 8.16→8.19 and retitles 6.19, 6.20 and 6.22; the "carried forward unchanged" lists point to an overwritten report with no commit hash (review finding B19, 2026-10-01).
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/4-0-prove-off-node-backups-and-isolated-restores.md`
  summary: Remove two rehearsal assumptions from the retained final-run tooling before the next backup proof run.
  evidence: Fresh run 20261001t061620z required a source-pre-derived memories/census.json input because mem.py manifest reads it while final-run-order.sh does not create it. proofs.py also emitted an OpenBao isolatedTargetIdentity.abortedAttempt note despite no aborted target in that fresh run. The input was supplied from the real census without changing retained tool checksums; a signed final-run-observations.json supplement clarifies the descriptive note and binds the original signed records. See evidence/epic-4/4-0/20261001t061620z/summary.md.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Restore the explicit requirement for accepted Story 4.2 exposure closure and Story 4.3 runner relocation before staging in the generated Epic 4 agent context.
  evidence: Review E13 verified that refreshing `epic-4-context.md` removed the previous explicit staging gate and the replacement dependency sequence omits it; upstream approved requirements retain both controls. Deferred because the workflow routes agent-context edits to deferred work.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Restore the Story 4.1 post-upgrade evidence, Story 4.3 runner relocation/namespace removal/no-cluster-access, registry immutability/per-environment credential, staging profile-template and Traefik/Gateway requirements dropped from the regenerated `epic-4-context.md` (review loop 2, G45).
  evidence: The baseline diff removes those bullets from `epic-4-context.md` while `epics.md` still requires them; the fix edits agent context, which the review workflow defers.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Prove that the rehearsal fixture's internal Docker network cannot reach the Docker bridge/host or other source addresses (node InternalIP, etcd advertise address) besides the probed endpoint host (review loop 2, G46).
  evidence: Unverified, medium if true. `rehearse.py` probes only the source endpoint host and 1.1.1.1:443. Settle by probing the bridge gateway and every source address from inside a fresh fixture and recording the address and failure mode.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Track the Administrator's host-access remediation actions recorded in private custody (review loop 3, G51).
  evidence: Review B41 found the actions listed only in prose. Their specifics are held in private custody: `passage-07.age` of attempt `20261003t103215z-custody-redaction` (ciphertext SHA-256 `5c5732fe3e8b45fe01d9bcf19eaee382905acae8800bdbb9187335536eba7cab`; criterion 5 remains unmet). Deferred because the underlying conditions predate this story and remediation is an Administrator decision.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Record a critical path and fallback for leaving Kubernetes 1.34 before its 2026-10-27 end of life (review loop 3, G53).
  evidence: All eight 4.26 criteria are open and 4.27 has not started, and nothing records a critical path or fallback. Deferred because the schedule belongs to Story 4.1, which predates this story.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Restore off-cluster record storage, the distinct staging promotion-stop state, separate-writer off-site replication and signed release records dropped from the regenerated `epic-4-context.md` (review loop 3, G55).
  evidence: Review B46 found these dropped beyond G45's list while the upstream planning artifacts still require them. Deferred because the fix edits agent context, which the review workflow defers.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Check whether any production API group serves a resource only in a non-preferred version, which `collect()` would miss (review loop 3, G71).
  evidence: Unverified, medium if true. `collect()` lists each group's preferred version and adds non-preferred versions only for CRDs. Settle by comparing per-version discovery on production.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Configure the EventStore host's advertised request body limit through Kestrel options during host construction.
  evidence: G87; Program.cs resolves raw KestrelServerOptions with GetService in its ApplicationStarted callback, with no raw service registration, so the nullable assignment skips the one-megabyte limit. This file belongs to separate earlier platform commits.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Reject out-of-range or fractional actorRevision values in the real platform identity admission provider.
  evidence: G88; PlatformIdentityGatewayAdmission.AdmitAsync uses JsonElement.GetInt64 after resolving an active actor, but FormatException is absent from its rejection catch filter, allowing malformed numeric revisions to escape as server errors. The identity provider is separate earlier platform work.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Add executing admission tests for the real registered platform identity gateway provider.
  evidence: G89; verification-layer symbol/import searches found no test invoking PlatformIdentityGatewayAdmission.AdmitAsync. Enrollment/login and mocked controller denial tests cannot detect source allowlist, actor revision or operator provenance regressions in that separate identity workstream.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Assign an owner for artifact signature and image SBOM/scanning verification of the Rancher/K3s management deployment.
  evidence: G103, from the 2026-10-04 code review of the fix commit `0e602ad..dc9a6ab`. `eng/cluster-management/RANCHER.md:35` defers "artifact signatures and comprehensive image SBOM/scanning" to "later deployment hardening", but neither the 4.28 story nor this ledger owns them. The spec follow-through still requires signature evidence. Medium: a 4.28 installation could proceed without authenticity checks.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Restore a buildable default package-reference dependency lane for the pre-existing Platform identity project (G106).
  evidence: Blind B86 and an independent root `dotnet build Hexalith.Platform.slnx --no-restore` both fail with 26 missing identity/security type errors. Directory.Build.props defaults UseHexalithProjectReferences=false, while the published Gateway 3.110.0 dependency lacks the consumed APIs. The project and dependency setup predate this resumed qualification run; fix in the separate identity workstream.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Make the pre-existing global-login alias test assert real registry lookup aliases (G107).
  evidence: Verification-gap V38 found SameVerifiedLoginAcrossTenantClaims_UsesOneGlobalActorAndNeverEnrolls returns one registry entry for Arg.Any<string>() and compares those predetermined actor IDs. Tenant-dependent alias derivation therefore evades its assertions; use an alias-bound registry response and verify different-login denial in the identity workstream. This test predates the resumed qualification run.

## Deferred from: code review of spec-4-26-qualify-rancher-and-the-management-migration.md (2026-10-04)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Bring the regenerated `epic-4-context.md` dependency order up to date with the approved 4.2 console closure and fresh production recovery before 4.27, and restore the dropped "native TLS where the provider supports it" requirement (G123).
  evidence: Story code review, chunk 1 (blind, edge-case and acceptance layers). `epic-4-context.md:66` orders "4.26 → 4.27 → 4.1 → 4.28 → 4.14" and says 4.2 "proceeds independently". The 2026-10-03 decisions and `eng/cluster-management/README.md:12` make accepted 4.2 closure and fresh recovery prerequisites of 4.27. `epics.md:2297` still states the native-TLS requirement, which G45/G55 do not list. Deferred because the fix edits agent context.

## Deferred from: code review of spec-4-26-qualify-rancher-and-the-management-migration.md (2026-10-05)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Make the 4.28 obligations added by the Story 4.26 corrections numbered 4.28 acceptance criteria: removing the retained `jpiquot` path, the Rancher local-admin decision with its Keycloak-unavailable test, and the named certificate's CN/O subject constraint (G135).
  evidence: Blind B105 and acceptance A16 in the review of `fad560f..6130bbe`. These items appear only in the 4.28 "Implementation handoff" prose and `eng/cluster-management/RANCHER.md`. The 4.28 acceptance-criteria list (`4-28-deploy-rancher-and-register-the-existing-cluster.md:26-33`) is unchanged, so 4.28 could be accepted without them. Deferred because the fix edits another story's epics-derived acceptance criteria.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Align 4.28 AC4 ("narrowly scoped deputy recovery access") and AC8's deputy prohibition with the 2026-10-03 Administrator decision "no deputy until one is named" (G136).
  evidence: Blind B105. The frozen 2026-10-03 decision in the 4.26 spec keeps the Administrator as sole holder of native cluster-admin and Rancher admin, with no deputy. AC4 and AC8 in `4-28-deploy-rancher-and-register-the-existing-cluster.md:29,33` still assume a deputy exists. This is pre-existing and was not introduced by the reviewed diff. The fix edits another story's acceptance criteria.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Restrict or verify direct PlatformActorRegistry mutation calls in the separate identity workstream (G137).
  evidence: The public DI facade signs any namespace-valid mutation and ignores its sourceId argument; its current production caller is the checked enrollment service, but a second in-process caller could bypass those checks.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Refuse inactive actors returned through still-active login aliases in the separate identity workstream (G138).
  evidence: The registry transition permits an inactive actor with an active alias, and ResolveLoginAsync returns that actor without inspecting Active.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Connect and verify the registered identity enrollment and login services through production callers (G139).
  evidence: The services are registered in DI but the current host has no production call to ApplyAsync or ResolveLoginAsync, leaving the intended bootstrap and login path unavailable.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Test bootstrap proof binding against a changed registry mutation in the separate identity workstream (G140).
  evidence: The production verifier hashes the mutation, but the existing enrollment test accepts any mocked scope; changing the digest calculation would evade the current test.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Bind issuer and subject to one authenticated login identity in the separate identity workstream (G143).
  evidence: ResolveLoginAsync uses ClaimsPrincipal.FindFirst for each claim, which can combine the authenticated identity's issuer with a second identity's subject in a composite principal.

## Deferred from: code review of spec-4-26-qualify-rancher-and-the-management-migration.md, chunk 2: tests (2026-10-05)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Add table-driven refusal tests for the cluster-management guards that no test reaches (G166).
  evidence: Verification-gap V52 in the chunk 2 test review. Coverage shows that the `raise` lines at `eng/cluster-management/rehearse.py:106`, `108`, `298`, `310`, `319`, `321`, `344`, `1081`, `1223-1224`, `1233`, `1766` and `rehearse_catalog.py:49` never run, and no test names their error codes. Every one of them fails closed, so this is deferred rather than patched. The catalog refusals that a passing record depends on are covered by patch G159.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Run the `eng/cluster-management` unit tests automatically in CI (G167).
  evidence: Blind B126 and verification-gap V54. The repository has no `.github/workflows`, so the 166 tests run only when someone runs the README command (`eng/cluster-management/README.md:62`) by hand. This is pre-existing repository-level infrastructure, outside Story 4.26.

- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Verify the authenticated known-content evidence behind each anonymous registry denial target (4.2 Blind 3; carried Corrected Edge 1).
  evidence: Unverified, medium if true. The signed knownExistingContent and evidenceSha256 fields can refer to protected collector operations beyond the representative consumer pull. Inspect those authenticated probe records to determine whether an anonymous target is nonexistent or was never successfully fetched.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Qualify repository-specific preservation requirements when one retained digest appears in multiple repositories (4.2 Blind 5).
  evidence: Unverified, medium if true. The local closure indexes content digests. Inspect the approved retained repository/location source, Zot retention policy and repository-specific fetch evidence to determine whether a required location could disappear while digest preservation passes.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Verify that the exact approved GC rehearsal evidence covers the intended generation, configuration and retained closure (4.2 Blind 6).
  evidence: Unverified, medium if true. Approval binds the protected rehearsal evidence hash, but those bytes were unavailable during local review. Inspect the approved record for actual source/configuration/closure identity before adding new automatic fields.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Resolve whether an additional numeric checkpoint-age or dispatch-evidence policy is needed for exposure mutations (4.2 Blind 9).
  evidence: Unverified, medium if true. The checker validates signed sequence and expiry, and the procedure separately requires a dispatch reread. An approved maximum-age policy and actual dispatch records are needed to establish a stale-operation violation.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Integrate and test the existing Custody projects in their own workstream (4.2 Blind 10).
  evidence: Both projects introduced by the separate custody commit 711a70fd94794ef1aea1b91f5524136b0d660468 are absent from the solution and the test project has no tests; neither has a caller in the exposure helper.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Qualify registry reader read scopes against the approved credential policy (4.2 Corrected Blind 2).
  evidence: Unverified, medium if true. The stated reader checks require push/delete denial and do not define an out-of-scope read field. Inspect the approved read-scope policy and protected negative probes to determine whether broader reads violate that policy.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Verify the actor and repository scope of protected least-privilege negative operations (4.2 Corrected Blind 3).
  evidence: Unverified, medium if true. Permission proofs are nested under their consumer/generation and bind protected evidence hashes; extra principal fields are not defined in that schema. Inspect the actual negative-operation audit records to establish any actor or scope mismatch.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Establish the independently approved signature-verifier and trust-root provenance for production qualification (4.2 Corrected Blind 7).
  evidence: Unverified, medium if true. Substituting /bin/true demonstrates dependence on the externally supplied retained verifier and trust root. Settle an actual unapproved substitution by checking independently approved toolchain identity and provenance; a helper-supplied hash alone cannot establish that trust.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Enforce revoked-key lifecycle behavior in the separate Custody component (4.2 Corrected Blind 10 and Corrected Edge 4).
  evidence: The pre-existing component admits Revoked metadata and its internal ComputeTag has no state guard. It has no exposure-helper consumer; its provider/caller needs explicit lifecycle enforcement and tests in that workstream.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Enforce key validity intervals at tag use in the separate Custody component (4.2 Corrected Blind 11).
  evidence: The pre-existing internal ComputeTag checks disposal but does not check the declared NotBefore/VerifyUntil interval. Its eventual provider/caller needs use-time lifetime enforcement and qualification; there is no caller in the exposure helper.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Give GitHub release workflows a dedicated least-privilege Zot writer that cannot delete retained content (4.2 live review, writer least privilege).
  evidence: The builds domain-release, eventstore and memories release workflows push with HEXALITH_ZOT_USERNAME/API_KEY, which is the Administrator's jpiquot key. Zot accessControl gives that key read/create/update/delete on `**`, so a workflow could delete retained manifests. The approved live decision left writers unchanged. Fixing it needs a new writer principal plus GitHub secret updates in three repositories.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Bring the registry `kubectl apply -k` source and the Keycloak deployment source up to date with the live 4.2 configuration (4.2 live follow-up).
  evidence: The live Zot config (anonymousPolicy [], htpasswd mount, retain-all retention; SHA-256 90805228…) and KC_HOSTNAME_ADMIN=http://localhost:38080 were applied directly to the cluster. The kustomize source the registry was applied from is not in the local repositories. Re-applying an old source would bring back anonymous read and drop the htpasswd mount or the admin hostname.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Get an independent internet-vantage measurement before 4.27 sets console-closure `externalPathIndependent` (4.2 live review, hairpin vantage).
  evidence: Medium, unverified. The 4.2 external probes ran from the LAN through the public-IP hairpin. Closure is route removal, public DNS has only the A record 82.67.127.189 with no AAAA, and no Ingress has source-range rules, so source address should not change routing. An off-network run of eng/admin-exposure/live/external_probe.py would settle it.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Revision-1 ReplicaSets in hexalith-memories reference `:0.0.0` placeholder images that are not in the registry (pre-existing).
  evidence: memories-access-telemetry-5fddff77bc and memories-access-telemetry-clock-765968cfb6 (replicas 0, created 2026-07-19) point at registry.hexalith.com/...:0.0.0, which has no tag in the authenticated closure. A rollback to revision 1 could not pull them. This predates 4.2.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Confirm the next nightly Zot GC completes for eventstore, and compare referrer-inclusive closures (4.2 live follow-up).
  evidence: The 2026-10-07 02:00 UTC GC failed for eventstore on the broken quarantine-proof index, which has since been deleted. The retain-all retention policy has therefore not yet run on eventstore. After the next run, compare with `registry_closure.py --compare` against the referrer-inclusive baseline in the 4.2 live attempt custody.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Remove the public DNS record for kube.hexalith.com, and move the hexalith-recovery Keycloak client secret into long-term secret custody (4.2 live follow-up).
  evidence: The kubesphere-console Ingress is deleted, but public DNS still resolves kube.hexalith.com to 82.67.127.189. The recovery client secret currently exists only in owner-only ~/hexalith-admin-exposure-evidence/credentials on the workstation.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Review the second Keycloak master-realm user `qdassivignon@itaneo.com` and the unused `keycloak-admin-api` service client against the sole-Administrator policy (found during 4.2 live closure).
  evidence: A read-only master-realm listing on 2026-10-07 showed users jpiquot and qdassivignon@itaneo.com, plus the service-account client keycloak-admin-api, whose credentials sit in Secret keycloak/keycloak-admin-api with no workload consumer. The approved policy names one Administrator.


## Deferred from: code review of 4-2-close-public-admin-exposure-and-anonymous-registry-reads.md (2026-10-07), split 1: local preparation

- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Carry LP-F1: verify authenticated known-content evidence behind anonymous registry targets.
  evidence: Unverified, medium if true. Blind B2 and Edge E2 repeat the existing 4.2 known-content entry: signed synthetic targets outside representative consumer pulls are accepted, but protected collector operations can supply the authenticated measurement. Inspect each target's referenced authenticated records before declaring the actual content absent or untested.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Carry LP-F2: verify the exact approved GC rehearsal generation, configuration and closure.
  evidence: Unverified, medium if true. Blind B5 repeats the existing 4.2 rehearsal entry. Additional fields are outside the local schema; approval binds the protected rehearsal bytes. Inspect their actual identities before adding new automatic fields.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Verify SHA-256 computation of transferred retained manifest/blob bytes in protected collector and audit records (LP-F3).
  evidence: Unverified, medium if true. Blind B6: the local helper compares requested/returned digests and binds audit hashes but does not inspect transferred bytes. Check whether the referenced measured records contain actual byte hashing and matching for each retained object; a new supplied assertion alone cannot independently establish it.
- source_spec: `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md`
  summary: Verify affected controller/configuration dependencies in actual exposure mutation snapshots (LP-F4).
  evidence: Unverified, medium if true. Blind B7: an Ingress/Service-only synthetic baseline passes. Inspect signed operational snapshots and effective-config collector scope to determine whether an affected mutable controller dependency is omitted from UID/resourceVersion/config drift checks; completeness is separately required by the procedure.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-1-validate-module-declarations.md`
  summary: Resolve composition-stage symbolic references
  evidence: Review BH02/BH04/BH06/BH08/EH04 confirms structural validation retains unresolved references; frozen intent reserves stage-specific rules. Composition, recovery-hook and critical-flow stories must define admissible target scopes.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-1-validate-module-declarations.md`
  summary: Enforce stage-specific uniqueness for task, hook, interface and operation names
  evidence: BH03 demonstrates duplicate task names; execution/catalog stages must apply collection-specific identity rules, which the frozen intent reserves for later stories.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-1-validate-module-declarations.md`
  summary: Decide authoritative-restore hook requirements
  evidence: BH05 is unverified: current declarations allow omitted restoreHook; the future recovery contract must decide whether native/operator hooks satisfy restoration before requiring a module-local reference.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-1-validate-module-declarations.md`
  summary: Validate resource requests against limits during composition
  evidence: BH09/EH06 confirms schema-valid requests can exceed limits; the later allocation stage must reject these inputs before provisioning.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-1-validate-module-declarations.md`
  summary: Define resource quantity conversion types and bounds
  evidence: BH10/EH05 is unverified: resource numbers remain JsonElement here, with no double conversion. A downstream conversion/provisioning test and chosen quantity types would establish required bounds.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-1-validate-module-declarations.md`
  summary: Reject invalid dependency graphs when constructing compositions
  evidence: BH11 shows self-dependencies are retained structurally; the later composition graph must reject self-links/cycles before launch.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-1-validate-module-declarations.md`
  summary: Complete v1 package qualification consumers and requalify the separately migrated EventStore 3.117.1 / FrontComposer 4.6.0 tuple before promotion.
  evidence: R3-BH01 and R3-EH04; owner commit 6f07763bd955d22ace0123798add528dc933bf51 migrated pins and synthetic fixtures, but the package control assertion and publication default still require EventStore 3.110.0. Historical retained acceptance does not qualify the migrated tuple.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-1-validate-module-declarations.md`
  summary: Define command-readiness execution and interpreter rules at the runtime execution stage.
  evidence: R3-BH08, medium unverified; local validation admits an existing readable file, but no v2 execution consumer yet establishes admissible script, DLL, interpreter or platform execution semantics. Verify against the later command launcher without launching during enrollment.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-1-validate-module-declarations.md`
  summary: Separate complete review-body and individual JSON-string budgets in the custody GitHub review transport.
  evidence: R3-BH09; authenticate_review passes the entire GitHub envelope through strict_json, whose 4096-character per-string limit rejects the body string even when the independently retained JSON body is otherwise admissible. This is unrelated custody work preserved by Story 1.1.

## Deferred from: code review of spec-1-1-validate-module-declarations.md (2026-10-08)

- source_spec: `_bmad-output/implementation-artifacts/spec-1-1-validate-module-declarations.md`
  summary: Bound startup override budgets and integer forms for typed consumers.
  evidence: R4-W1, medium. `lifecycle.startup.override.timeoutSeconds` of `1e308` or `1e-300` satisfies the frozen "positive finite" rule, but TimeSpan conversion overflows or truncates. `replicas`, `memoryMiB` and `sizeMiB` written as `1.0`/`1e3` pass JSON Schema `integer` but fail `GetInt32()`. Story 1.8 must define the representable or policy budget cap. Composition (Story 1.6) must normalize or reject non-canonical integers. Extends the resource quantity-conversion entry.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-1-validate-module-declarations.md`
  summary: Decide whether provider tenancy may be `shared`.
  evidence: R4-W2, unverified; medium if true. The v2 `tenancy.mode` enum admits `shared`, while the spine defines the field as "external providers needing per-environment tenancy" and AD-8 separates hosted state by environment. To settle it, the owner states whether any declared external provider may be shared across environments.

## Deferred from: code review of spec-plat-actor-history-1-t1-immutable-custody-contracts.md (2026-10-08)

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Reconcile the Builds R5 note with the moved Builds gitlink and the other submodule pointer updates in the dirty tree.
  evidence: BH01. The R5 verification text still describes uncommitted patches on 50b0257 while the gitlink is 0097b5a. This is other in-progress submodule work, not the actor-history contracts.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Pin whether a fresh deletion-capability signature is stored after healthy rotation.
  evidence: BH03, EH06, VG02. RetainAsync resolves trust with requireCurrent false for SignAsync. No test rotates a successful sign to non-current and non-revoked. The signing actor was already dirty before this story.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Re-check trust and the original issuance receipt when a stored Signed deletion outcome is read again.
  evidence: BH04. ResolveAsync returns a Signed outcome without consulting trust or OriginalIssuanceReceiptId, and that receipt id is not stored on the outcome. Pre-existing signing work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Treat a still-current emergency revocation as a denial of fresh deletion-capability issuance.
  evidence: BH05, VG01. IsRevoked defaults to false, and no test sets IsRevoked true while IsCurrentNonRevoked stays true. Deleting the new revocation clause leaves the current signing tests green. Pre-existing signing work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Separate the private-owner credential MAC purpose from TrustedEnvelope.
  evidence: BH06. PrivateOwnerOperationAuthenticator tags PlatformHmacPurpose.TrustedEnvelope. Separate authenticator work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Reconcile a security-spool anti-rollback anchor when the durable save is lost.
  evidence: BH07. TryWriteAsync records the next revision before TrySaveStateAsync and ignores a false or thrown save. Spool work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Keep an empty or missing security spool from reporting ready.
  evidence: BH08, EH01. A null component read becomes revision 0 with no records, and Records.All is true for that empty list when the authority attests the digest. Spool work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Define what happens when the security spool reaches its 10000-record bound.
  evidence: BH09. ObserveAsync drops the new observation at the bound, and a fully receipted spool can remain ready. Spool work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Stop security-spool drain from reporting success for a bad count or malformed state.
  evidence: BH10. ArgumentOutOfRangeException and malformed-state exceptions are caught and returned as a normal count. Spool work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Give security-spool digests a stable domain so serializer changes do not move restore anchors.
  evidence: BH12. StateDigest and IntentDigest hash default JsonSerializer output with no domain prefix. Spool work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Accept offset-equivalent UTC instants on private-owner grants and credentials.
  evidence: BH14. Those checks require Offset == TimeSpan.Zero and reject an equivalent non-zero offset. Separate authenticator work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Keep a near-max observation time from aborting the rest of a security-spool drain.
  evidence: EH02. ObservedAt plus the recovery horizon can throw, and the drain catch then returns without later records. Spool work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Reject a security-spool observation whose observed time is in the future.
  evidence: EH03. Capture accepts that time, and the horizon comparison then leaves automatic append open. Spool work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Reject a security-spool routing tenant that cannot form an aggregate identity before drain.
  evidence: EH04. SourceStream constructs AggregateIdentity, and Text does not apply that regex, so an illegal tenant throws out of the batch. Spool work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Reject an unpaired surrogate in a security-spool identity as a normal argument error.
  evidence: EH05. Text calls UTF8Encoding.GetByteCount without catching EncoderFallbackException, so IntentDigest throws. Spool work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Refuse a private-owner credential when the signing profile is missing or expired.
  evidence: VG03, pre-verified. Removing both IsValid checks leaves the authenticator tests green because they use a valid fixture profile. Separate authenticator work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Refuse a private-owner credential when the second grant read returns a different still-valid grant.
  evidence: VG04, pre-verified. The withdrawal test returns null on the second read, so deleting the grant inequality stays green. Separate authenticator work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Stop a security spool from observing after its qualification ValidUntil.
  evidence: VG05, pre-verified. Current tests keep ValidUntil two days ahead, so removing the expiry comparison stays green. Spool work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-plat-actor-history-1-t1-immutable-custody-contracts.md`
  summary: Leave security-spool state unchanged when the anti-rollback anchor refuses the next revision.
  evidence: VG06, pre-verified. No test makes RecordRevisionAsync return false for the revision about to be written. Spool work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-2-make-the-builds-catalog-the-single-version-authority.md`
  summary: Observe Aspire SDK declarations supplied only through active ordinary imports in the SDK scanner import closure.
  evidence: Review B3-1/E3-1 and parent reproduction: a consumer importing a props file containing its sole Aspire.AppHost.Sdk/99.0.0 declaration returns no pins. Source-start G6/runtime/exception scanners also inspect only the consumer XML, so this gap predates Story 1.2. Add active/inactive transitive-import controls through each actual scanner when extending that existing contract.

## Deferred from: code review of spec-1-2-make-the-builds-catalog-the-single-version-authority.md (2026-10-09)

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-2-make-the-builds-catalog-the-single-version-authority.md`
  summary: Derive or validate workflow and action CLI/runtime defaults against the catalog's HexalithDaprCliVersion, HexalithDaprRuntimeVersion and HexalithAspireAppHostSdkVersion fields.
  evidence: Review BH6+AA4. Builds `Github/workflows/domain-ci.yml` (28/33), `domain-release.yml` (22/26) and `Github/dapr-init/action.yml` (8) default Dapr CLI 1.18.0 and runtime 1.18.2 as literals. G6 `effective_tuple` reads Aspire/Dapr CLI versions from the Projects `ci.yml`, and nothing compares them with the catalog fields. This duplication predates Story 1.2 and is outside its Code Map; runtime HXR012/HXR015 still surface mismatches.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-2-make-the-builds-catalog-the-single-version-authority.md`
  summary: Match Aspire.AppHost.Sdk case-insensitively in the Python G6/runtime SDK scanners.
  evidence: Review BH2+EH2. `evaluated_catalog.has_apphost_sdk`/`transform` and the pre-change regexes compare `Aspire.AppHost.Sdk` case-sensitively, while NuGet SDK resolution is case-insensitive, so `Sdk="aspire.apphost.sdk/x"` is not observed. This predates Story 1.2 in the Python scanners. The PowerShell exceptions-validator path is closed for literal declarations by the story's non-Aspire SDK-pin patch. Loop-5 review (2026-10-09, EH5+VG-O2) found one gap there: it still skips a property-versioned `Import Sdk` whose ID is not exactly `Aspire.AppHost.Sdk` (e.g. `Sdk="aspire.apphost.sdk" Version="$(HexalithAspireAppHostSdkVersion)"`). Close that path together with the Python fix.
- source_spec: `spec-1-2-make-the-builds-catalog-the-single-version-authority.md`
  summary: Add a workspace-wide CI gate for the package-version exception allowlist across initialized module owners.
  evidence: Builds CI and release invoke the existing inventory-only validator without WorkspaceRoot; a standalone Builds checkout cannot scan the other module owners represented in the inventory, while fixture tests cover the scanner itself.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-2-make-the-builds-catalog-the-single-version-authority.md`
  summary: Evaluate PackageReference and PackageVersion item conditions in the runtime consumer inventory.
  evidence: Review BH7-9. `runtime_toolchain_v2.inventory` iterates raw XML and compares every controlled package pin even when its item or parent condition is false. The same loop exists at the preserved Builds baseline; an inactive changed pin can create false drift.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-2-make-the-builds-catalog-the-single-version-authority.md`
  summary: Require each successful VSTest shard to produce its expected TRX report.
  evidence: Review BH7-12. `domain-ci.yml` marks a zero-exit `dotnet test` PASS before checking that the expected TRX exists. The shard change predates this story's source-start revision and is separate from catalog authority.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-3-resolve-one-active-root-source-mapping.md`
  summary: Verify ownership of a retained directory-form nested submodule when its former gitlink history is absent from a shallow checkout.
  evidence: A shallow direct checkout may make `git log --all` return no older gitlink, but a fixture with a retained nested `.git` directory and truncated parent history is needed to establish whether the current preflight misclassifies it.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-3-resolve-one-active-root-source-mapping.md`
  summary: Validate actor principal ULID syntax in the Identity receipt verifier.
  evidence: The Identity contract requires `platform:actor:<ULID>`, while `P1ReceiptVerifier.ValidPrincipal` accepts any nonempty suffix; this is outside the Builds source-mapping intent.

## Deferred from: code review of spec-1-3-resolve-one-active-root-source-mapping.md (2026-10-10)

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-3-resolve-one-active-root-source-mapping.md`
  summary: Run the real EventStore and Commons production-candidate tests in a Platform-root CI check.
  evidence: `WorkspaceMsBuildTests.cs:240` and `:278` skip in standalone Builds CI, which does not fetch Platform gitlinks. Carried from review iterations 11 and 13 (V1).
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-3-resolve-one-active-root-source-mapping.md`
  summary: Exercise the case-insensitive `FilesystemPathRules.Comparison` branch on a Windows or macOS CI leg.
  evidence: Every Builds workflow runs on `ubuntu-latest`. Replacing the method body with `return StringComparison.Ordinal;` leaves CI green, while containment and the host-exclusion condition depend on it on case-insensitive filesystems.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-3-resolve-one-active-root-source-mapping.md`
  summary: Record the selected `--mode` in run evidence and its canonical command.
  evidence: `ModuleRunEvidenceFactory.CreateCommand` and the readiness validator's rebuilt command omit `--mode`, so evidence from a source/Debug run looks the same as the package/Release run CI must use. This changes evidence contracts, which story 1.3 forbids. It belongs with Story 1.4 package-mode identity.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-3-resolve-one-active-root-source-mapping.md`
  summary: Derive a standalone active root's module identity from a declared source rather than its checkout directory name.
  evidence: `WorkspaceRootResolver.cs:64,112` uses `Path.GetFileName` of the root. A worktree such as `Hexalith.Parties-wt2` loses HXW005 duplicate detection for `Hexalith.Parties.*`, and a directory named `Hexalith` flags every `Hexalith.*` package. User decision 2026-10-10: wait for the next-version Platform declaration to supply a module identity (Stories 1.5/1.12), and document the directory-name rule meanwhile.

## Deferred from: code review of spec-1-3-resolve-one-active-root-source-mapping.md, iteration 16 (2026-10-10)

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-3-resolve-one-active-root-source-mapping.md`
  summary: Confirm whether the final-import marker guard rejects tool-launched projects that do not import `Microsoft.NET.Sdk.targets`.
  evidence: `SourceMappingMaterializer.cs:352` (`GuardFinalImportMarker`) runs in every mapped project through the `Directory.Build.targets` hook, but only `Microsoft.NET.Sdk.targets` imports the file that sets `_HXWFinalTargetsImported`. A Traversal, NoTargets or legacy project in a launched build graph would fail HXW006. No such project exists in any root-declared reference, so this is unverified (medium if real). One such tool-launched build would settle it; the likely fix is to condition the guard on `'$(UsingMicrosoftNETSdk)' == 'true'`.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-3-resolve-one-active-root-source-mapping.md`
  summary: Correct Identity's incident classification for a prior observation from another receipt URI.
  evidence: `P1ReceiptHttpAdapter.RetrieveAsync` returns `ImmutableIdIncident` for mismatched retrieval URI input; that is not evidence that the same immutable ID changed and is unrelated to Builds mapping.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-3-resolve-one-active-root-source-mapping.md`
  summary: Decide whether Identity receipt history must survive adapter recreation within a process.
  evidence: The adapter creates a new `P1ReceiptHttpHistory` by default, allowing a changed frame after recreation; an explicit adapter lifecycle contract or a real recreating consumer would settle whether this violates the intended history scope.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-3-resolve-one-active-root-source-mapping.md`
  summary: Require a resolved installed-agent roster before party creation collision checks.
  evidence: Party creation guidance checks the `codes` map but ignores `installed_agents_resolved`; a failed lookup can omit an installed code and permit an unintended override.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-3-resolve-one-active-root-source-mapping.md`
  summary: Align CIS and TEA skill configuration with central BMad configuration resolution.
  evidence: Changed BMM skills use `resolve_config.py` while CIS and TEA skills still read generated per-module YAML, so central overrides can yield different settings across skills.

## Deferred from: code review of spec-1-4-enforce-the-platform-identity-and-support-package-mode.md (2026-10-11)

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-4-enforce-the-platform-identity-and-support-package-mode.md`
  summary: Realign the module manifest EventStore pin (3.119.0) with the Builds catalog and G-4 acceptance pin (3.120.0).
  evidence: The pre-existing catalog bump `9e6b6ca` set `HexalithEventStoreVersion` to 3.120.0. Story 1.4 keeps `ModuleManifestLoader.ModuleManifestEventStoreVersion` at 3.119.0 on purpose (Code Map, R2-BH9), while `G4P0AcceptanceValidator.cs:99` still checks `SupportedPlatformPins.EventStoreVersion`. So package mode enforces 3.120.0 EventStore packages for manifests that must declare 3.119.0. A manifest pin migration story must realign the schema const, the fixtures and the validator.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-5-declare-eventstore-memories-and-tenants-for-the-reference-composition.md`
  summary: Add the two clock verifier environment bindings to the Kubernetes clock deployment as part of the successor Access Telemetry profile requalification.
  evidence: Story 1.5 clock `/ready` verifies attestations against `AccessTelemetryLifecycle:AttestationVerificationKey` (secret `access-telemetry-clock-key`/`verification-public-key`) and `AccessTelemetryLifecycle:ClockSignerKeyEpoch`. The local AppHost supplies both. Adding them to `deploy/kubernetes/base/access-telemetry-deployments.yaml` changed the adopted PG-ONPREM-2 bytes (candidate hash `aba898858bd7e82eb042b6ee50a76cd16821b154a65a7ab83f8e79ef45b835b6`) and broke the Memories lifecycle CI suite, so the 2026-10-12 code-review decision reverted them. Both overlays keep the clock at `replicas: 0`. Until the successor candidate adds the bindings, a qualification run using a Story 1.5 clock image stays not-ready.

## Deferred from: code review of spec-1-5-declare-eventstore-memories-and-tenants-for-the-reference-composition.md (2026-10-11)

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-5-declare-eventstore-memories-and-tenants-for-the-reference-composition.md`
  summary: Allow Memories to reach EventStore in production, or define how it does.
  evidence: Memories defaults to Dapr `invoke/eventstore/method/` (`MemoriesServerServiceCollectionExtensions.cs:89-97`, with no `CommandGateway:BaseAddress` override in `server-deployment.yaml`). Neither `Hexalith.EventStore/deploy/dapr/accesscontrol.yaml` nor the Tenants copy allows `memories`. This was already true at baseline `25f063e9`. Memories production runs in namespace `hexalith-memories`. Once a rule exists, `inspect_acl`'s exact caller set must include it.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-5-declare-eventstore-memories-and-tenants-for-the-reference-composition.md`
  summary: Decide whether Tenants' shared configuration-input `reference` identifiers collapse distinct values. Medium if true; unverified.
  evidence: Tenants uses `authority` for `Oidc:Authority`, `EventStore:Authentication:Authority` and `Authentication:JwtBearer:Authority`. It uses `audience` for `EventStore:Authentication:Audience`, `Authentication:JwtBearer:Audience` and `Authentication:Workload:Audience`, and reuses `issuer` and `requirehttpsmetadata` twice each. EventStore and Memories use unique prefixed references. This can be settled once Story 1.6 defines whether composition binds inputs by `(source, reference)`.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-5-declare-eventstore-memories-and-tenants-for-the-reference-composition.md`
  summary: The retention disable control does not stop Dapr TTL expiry of access-telemetry records.
  evidence: `AccessTelemetryLifecycleProcessor.cs:120-144` writes records with `ttlInSeconds` equal to the retention expiry, so the state store still expires them while `AccessTelemetryLifecycle:Enabled=false` suppresses the actor purge. This is a pre-existing TTL design. Decide whether the disable contract covers store-level expiry.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-5-declare-eventstore-memories-and-tenants-for-the-reference-composition.md`
  summary: Correct the stale Tenants Dapr README description of the domain receiver ACL.
  evidence: `Hexalith.Tenants/deploy/dapr/README.md:60` (and troubleshooting rows 83-84) says `accesscontrol.tenants.yaml` allows only `POST /process` and `/project`. The file allows `/process`, `/project`, `/query`, `/replay-state` and `/admin/operational-index-metadata` for `eventstore`. This predates Story 1.5.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-5-declare-eventstore-memories-and-tenants-for-the-reference-composition.md`
  summary: Run the Tenants API authentication tests (legacy-over-alias precedence and startup key messages) in a blocking CI lane.
  evidence: The tests live only in `Hexalith.Tenants.IntegrationTests`, which `references/Hexalith.Tenants/.github/workflows/ci.yml:35` lists solely as `aspire-test-project`. Builds `domain-ci.yml` runs that job with `continue-on-error` defaulting to true, so a precedence or message regression can merge green. This is pre-existing CI tiering found in the 2026-10-12 re-review.

## Deferred from: code review of spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md (2026-10-13)

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md`
  summary: Make an explicit `hexalith-module down --run-id` stop the named run even when the manifest changed after `run`.
  evidence: Both the v1 path (`ModuleCommandExecutionService.cs:172-209`) and the new v2 path (`:958-969`) look up runs by the current manifest hash. An explicit run ID missing from those matches returns `completed`/HXI001 with exit 0 while the run keeps running. Fix both paths together: when a valid run ID is supplied, read its state directly, and distinguish "no such run" from "already cleaned up".
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md`
  summary: Pin the Memories infrastructure images through the Builds catalog.
  evidence: The Platform model adds `redis/redis-stack:latest` and `falkordb/falkordb:latest` (`PlatformAppModel.cs:118-119`), while the run Redis is digest-pinned through `CompositionToolchainPins`. The Memories owner AppHost uses the same untagged images (`Program.cs:138,279`). Neither image has a catalog entry, and the registry preflight skips both, so runs are not reproducible.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md`
  summary: Make explicit down by run ID independent of the current manifest hash in both v1 and v2.
  evidence: Both command paths first select states by the manifest's current hash; after a manifest edit, the named running state is absent from that filtered set and the command reports completed/HXI001. The existing v1 behavior and the new v2 behavior need one shared correction.

## Deferred from: code review of spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md, fourth pass (2026-10-13)

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md`
  summary: Bound the writer-protocol cutover after the gateway first answers, and report its timeout without stopping the file AppHost (Story 1.8 deadline policy).
  evidence: `CompositionWriterProtocolCutover.ActivateAsync` starts its 2-minute bound before endpoint allocation, so the gateway's Debug `dotnet run` build falls inside it. In the file entry, `PlatformGatewayStartup` (`PlatformGatewayStartup.cs:12`) lets the `TimeoutException` fault a `BackgroundService`, whose default `StopHost` ends the AppHost on a slow cold start. The signed runner reports HXR027 under the same pre-existing bound.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md`
  summary: Realign the file-entry `Aspire.AppHost.Sdk` pin with the Builds catalog.
  evidence: On `main`, `eng/verify-platform-pins.py` exits 1: `apphost.cs:2` selects 13.6.0 and the Builds catalog at `e3be1d4` selects 13.6.1. The drift came from a Builds pointer bump after Story 1.6. Platform has no CI that runs the check, so only `eng/verify-agents-host.sh --mode LocalScaffold` shows it.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md`
  summary: Verify that package-mode container servers can reach their host-run Dapr sidecars (unverified; medium if true).
  evidence: `PlatformAppModel.cs:236` sets `DAPR_HTTP_ENDPOINT`/`DAPR_GRPC_ENDPOINT` only on project resources, and container servers use a fixed HTTP target port of 8080 (`:174`). Package mode refuses until the owners release v2 declarations, so no run has exercised this. Settle it with the first live package-mode run.

- source_spec: `_bmad-output/implementation-artifacts/spec-timesheets-staging-upgrade-handoff.md`
  summary: Bind upgrade preparation source hashes to an immutable source archive or retained canonical VCS snapshot before operational audit consumes them.
  evidence: The pre-existing recorder retains only hashes; local source replacement can make consumed preparation bytes unavailable for later reproduction. The current change binds selection to its consumed snapshot but grants no execution authority.

- source_spec: `spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md`
  summary: Reject nonpositive NuGet publication verification attempt counts and invalid retry delays.
  evidence: Builds Tools/publish-g4-tool-packages.ps1 accepts zero or negative HEXALITH_NUGET_VERIFY_ATTEMPTS, skips every remote verification iteration and returns success; introduced by later release tooling commit 01dfb16.

- source_spec: `spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md`
  summary: Correct Timesheets deployment preparation's stale claim that Development composition has no Timesheets binding.
  evidence: eng/timesheets-deployment/prepare.py hosted-composition blocker and retained plan.json contradict the later local host binding and its qualification documentation; hosted release qualification remains absent.

## Deferred from: code review of spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md, sixth pass (2026-10-14)

- source_spec: `spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md`
  summary: Mark the Timesheets deployment's hash-bound files `-text` so `autocrlf` checkouts keep their recorded SHA-256 values.
  evidence: `eng/timesheets-deployment/plan.json:52` records the sha256 of `hexalith.module-manifest.v2.json`, and `prepare.py:19,145-165` hashes working-tree bytes, including its own. `.gitattributes` covers only `src/Hexalith.Platform.Composition/Declarations/*.json`. Pre-existing in Timesheets enrollment work (`ce6f2d3`).


## Deferred from: Story 1.6 build review, seventh pass (2026-10-14)

- source_spec: `spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md`
  summary: Publish retirement preparation attempts atomically after every record and checksum is complete.
  evidence: The concurrent Story 4.27 preparer creates the final immutable attempt directory before writing its archive and records; attempt.json records preparationComplete=true before later writes and checksum verification. A write failure leaves a partial attempt and blocks retry under the same ID. This is separate retirement-preparation work.

- source_spec: `spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md`
  summary: Retain distinct preflight, postflight and storage evidence paths for each Kubernetes upgrade hop.
  evidence: eng/kubernetes-upgrade/prepare.py assigns pre-upgrade-health.json, post-upgrade-health.json and dynamic-storage-proof.json to both hop records. Fresh second-hop evidence can replace retained first-hop evidence. This predates declaration-composition corrections and belongs to the separate upgrade workflow.

- source_spec: `spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md`
  summary: Refuse retirement metadata audits when a nested directory cannot be enumerated.
  evidence: The concurrent eng/cluster-management/prepare_retirement.py uses os.walk without onerror, which silently ignores inaccessible nested subtrees; the resulting audit can omit metadata while the attempt reports preparationComplete=true. This belongs to the separate retirement-preparation workflow.

## Deferred from: code review of spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md (2026-10-14)

- source_spec: `spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md`
  summary: Non-tenants sidecars receive a null Dapr config in local (non-run) mode.
  evidence: PlatformAppModel.cs:269 sets `Config = ... : options.RunPlan?.DaprConfigPath`. Unverified medium; settle by checking whether Memories/Timesheets need access control without a run plan.

- source_spec: `spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md`
  summary: Run signing-key configuration iterates a literal six-server list.
  evidence: PlatformAppModel.cs:67-81; an extra selected server (Timesheets) gets no plan signing key. Low.

- source_spec: `spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md`
  summary: Timesheets composition lives in the Platform model beyond story 1.6's scope; carried SDK 13.6.0 drift and `latest` Memories images re-confirmed.
  evidence: Delivered by separate Timesheets specs; R5-E6 and the `latest` image deferral already in the ledger.

- source_spec: `spec-1-6-compose-environments-from-declarations-with-one-platform-aspire-model.md`
  summary: Enable package-mode composition when owners release v2 declarations.
  evidence: `ReleaseSnapshotHashes` is empty by design, so package mode refuses today. When the first owner ships a v2 declaration release, populate the Platform release snapshot hashes and add a positive package-mode test.

## Deferred from: Story 1.7 parent diff audit (2026-10-15)

- source_spec: `spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`
  summary: Qualify concrete EventStore projection notification delivery for the selected local pubsub provider.
  evidence: EventStore's established subscriber pattern is `*.*.projection-changed` at `/projections/changed`; publishers produce concrete tenant/projection topics. Pinned components-contrib Redis v1.18.2 Subscribe passes req.Topic to XGroupCreateMkStream and XReadGroup rather than expanding wildcards (https://github.com/dapr/components-contrib/blob/v1.18.2/pubsub/redis/redis.go#L127). Story 1.7 preserves the exact owner binding and local Direct transport default, and does not claim wildcard fan-out. The live fixture publishes a concrete topic (204) and observes no callback within two seconds; evidence is retained under evidence/story-1-7/audit/security/projection-observation.json. Resolve the owner/provider topic contract in follow-up messaging work without inventing a provider exception here.

- source_spec: `spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`
  summary: Verify whether declared application health plus authenticated outbound sidecar health needs an additional safe invocation-readiness check.
  evidence: Independent finding B6 is medium if true but unverified: all applications are checked before the new outbound probe, and no reachable wrong-channel or name-discovery failure was demonstrated. A controlled healthy-app/broken-permitted-invocation fixture would settle the claim; preserve explicit ACL policy without inventing startup grants.

- source_spec: `spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`
  summary: Add catalog-version preflight for the file entry when it uses ambient global Dapr tools.
  evidence: Independent finding B8 confirms the pre-existing global CLI/runtime fallback when HEXALITH_DAPR_HOME is absent; the current live commands provide pinned tools explicitly. Validate ambient selection before launch in the owning toolchain/startup work without weakening the Builds catalog.

- source_spec: `_bmad-output/implementation-artifacts/spec-ext-secrets-1-runtime-delivery.md`
  summary: Pin prepared Dapr environments to declarative subscriptions and assert the scheduler lock bind mount.
  evidence: Story 1.7 composition sets `Dapr__DeclarativeSubscriptions` and passes a scheduler data directory. The prepared-environment tests do not read that flag, and the scheduler tests do not assert a `/var/lock` bind mount of `scheduler-data`. This secrets delivery did not edit those files.

- source_spec: `_bmad-output/implementation-artifacts/spec-ext-secrets-1-runtime-delivery.md`
  summary: Reject empty or wildcard invocation verbs, unsupported dead-letter strategies, and ambiguous topic matches in local Dapr binding render.
  evidence: `PlatformDaprBindings` still copies verbs without an allow-list, uses `First` for a dead-letter topic, and drops unknown servers. Those branches belong to the pre-existing story 1.7 binding work.

- source_spec: `_bmad-output/implementation-artifacts/spec-ext-secrets-1-runtime-delivery.md`
  summary: Keep local Dapr fixture cleanup from aborting when a sidecar exits before SIGTERM.
  evidence: `eng/test-local-dapr-security.py` calls `os.killpg` after `poll()` without catching `ProcessLookupError`. The fixture predates this secrets delivery.

- source_spec: `_bmad-output/implementation-artifacts/spec-ext-secrets-1-runtime-delivery.md`
  summary: Keep the offline recovery verifier from hashing unlisted private-key names and from waiting forever on a live parser child.
  evidence: `eng/recovery-preparation` was already in the working tree. `secret_path` does not refuse `.p12` or `id_ecdsa`, and the static preflight failure path can wait on a child without the success-path deadline.

## Deferred from: code review of spec-1-7-ratify-local-dapr-conventions-and-bind-component-names (2026-10-15)

- Run-plan placement/scheduler ports ignored; fixed mTLS control-plane ports (`51005`, `51006`, Sentry `50001`) collide across concurrent runs. Already disclosed as out of scope.
- (unverified medium) Declarative-only subscription mode: owners with attribute-only subscriptions and no explicit binding lose delivery, and projection wildcard `*.*.projection-changed` delivery on Redis is unproven. Settle by diffing owner `[Topic]` attributes against rendered subscriptions plus a live wildcard assertion.
- (unverified medium) Deleted reference ACL grants for `eventstore-admin`, `sample-blazor-ui`, `sample-api`; confirm no selected topology still loads those callers.

- source_spec: `spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`
  summary: Pinned Dapr ACL concrete path rules also match descendant paths; exact-path isolation needs an owner/provider decision.
  evidence: Dapr v1.18.2 pkg/config/acl_trie.go Search returns node.Data before consuming descendants; endpoint authentication remains required. This provider behavior predates the story and existed under the earlier permissive policies.

- source_spec: `spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`
  summary: EXT-SECRETS-1 signing must require explicit absence before effect-sign after a parsed query.
  evidence: R2-B8/R2-E1: DaprSigningProviderAdapter.ResolveAsync falls through from non-Signed parsed proof to effect-sign; independent commit 26628896b4fc7b70337c22b4ee34a4b6b0edc6d8 predates resumed Story 1.7 work.

- source_spec: `spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`
  summary: EXT-SECRETS-1 revocation authentication must bind the complete requested envelope.
  evidence: R2-B9: DaprRevocationAuthenticatorAdapter accepts only subset echoes then constructs authorization for the full requested envelope; independent EXT-SECRETS-1 commit, not caused by Story 1.7.

- source_spec: `spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`
  summary: EXT-SECRETS-1 compromise receipts must bind revocation and trust-profile revisions.
  evidence: R2-B10: DaprCompromiseRegistrarAdapter omits both revisions from requests and receipt checks; independent EXT-SECRETS-1 commit.

- source_spec: `spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`
  summary: EXT-SECRETS-1 invocation responses need a small bound before buffering.
  evidence: R2-E2: DaprSecretsInvoker default buffered SendAsync precedes SecretsProof size validation, allowing excessive memory allocation up to framework buffering limits; independent adapter work.

- source_spec: `spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`
  summary: Recovery inventory must refuse id_ecdsa and .p12 private-material names before reading bytes.
  evidence: R2-E5: eng/recovery-preparation/verify_packet.py secret_path does not recognize these names, while inventory hashes every admitted regular file; independent recovery work present on activation.

- source_spec: `spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`
  summary: Verify whether recovery static preflight can block indefinitely while reaping a killed parser (medium, unverified).
  evidence: R2-E6: run_view calls child.wait without a timeout after SIGKILL. A fixed-parser/process fixture remaining unreapable after termination would settle reachability; independent recovery work.

- source_spec: `spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`
  summary: EXT-SECRETS-1 compromise receipts must accept valid durable key blocks with no affected batches.
  evidence: R3-B4: the adapter rejects affectedBatchIds=[] although EventStore's actual revocation owner persists it and the custody subscriber accepts it, preventing completion and guard mirroring. Independent adapter commit 26628896b4fc7b70337c22b4ee34a4b6b0edc6d8 predates this resumed story.

- source_spec: `spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`
  summary: EXT-SECRETS-1 transition admission and recovery must bind independently retained prospective bytes.
  evidence: R3-B5: DaprInventoryAuthorityAdapter.TransitionBody drops AnchoredStateTransition.TargetBytes although the authority contract requires exact byte retention/verification; different byte arrays generate the same request. Independent EXT-SECRETS-1 code, unchanged by this story.

- source_spec: `spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`
  summary: EXT-SECRETS-1 final decision authority checks must bind the actual approver cohort, issuance time and exact published profile.
  evidence: R3-B6: VerifyAuthorityBoundaryAsync omits those manifest/profile fields despite its coherent current authority contract; earlier separate reads cannot supply the missing final check. Independent EXT-SECRETS-1 code.

- source_spec: `spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`
  summary: EXT-SECRETS-1 inventory and decision authorization must require an affirmative fresh outcome in addition to echoed identity fields.
  evidence: R3-B7: Echoes/EchoAsync return true after field equality without checking a denial/revocation status or current validity. Matching request echoes with a rejected authority state therefore authorize calls. Independent EXT-SECRETS-1 code.

- source_spec: `spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`
  summary: EXT-SECRETS-1 replay, compromise and transition mutations must require explicit request-bound absence rather than an empty proof.
  evidence: R3-B8: CapabilityIdentity.Absent accepts proof.Count == 0 and its callers proceed to effects. Empty JSON establishes neither an outcome nor the request identity. Independent EXT-SECRETS-1 code.

- source_spec: `spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md`
  summary: Investigate intermittent signed source startup failures before claiming reliable cold startup.
  evidence: The resumed 2026-10-16 run retained HXR025 failures at eventstore Waiting/FailedToStart and tenants-api Finished; a control-plane snapshot also showed an unhealthy Sentry health check despite initialization logs. All six sequential owner builds passed, followed eventually by passing technical (44.488 s) and six-server (119.514 s) signed smokes without a startup code change. No single cause is established; a controlled cold/warm reproduction with retained resource health and bounded build exceptions would settle it. See evidence/story-1-7/resumed-20261016T074246Z/verification.md.

## Deferred from: code review of spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md (2026-10-16)

- Real Dapr ACL matching is verified only by gated live tests (`HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE=1`); no default-run check. Needs Docker + Dapr runtime in a documented release gate.
- Live fixture never asserts `callbackObserved`; declarative mode drops all programmatic `[Topic]` endpoints. Unverified medium; settle by asserting a non-wildcard projection callback.
- Live evidence bypasses Platform-composed Aspire sidecars (direct `daprd` + stubs). Unverified medium; settle with a signed Builds source smoke that asserts invocation and delivery.

- source_spec: `_bmad-output/implementation-artifacts/spec-ext-secrets-1-exact-target-runtime-qualification.md`
  summary: Verify the independently installed caller authenticator accepts the exact serialized Dapr qualification request and current isolated scope.
  evidence: Unverified medium concern from R2-B4; the source exposes HttpContext authentication but no installed owner implementation or returned credentials/identity mechanism exists. Only an independent owner enrollment and serialized transport proof can settle it. Live remains blocked; the user requested the source candidate and provisioning blockers.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Per-resource and invocation probes still fail at fixed 90-second and 45-second caps while the effective startup deadline can be longer.
  evidence: `RunReadiness` cancels each resource probe at 90 seconds and each invocation probe at 45 seconds. Those caps predate story 1.8, which replaced the engine and AppHost five-minute bounds. A resource that becomes ready after the probe cap but inside a declared override still fails.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Attaching startup-task output readers after the process starts may collapse a fast exit into HXR020.
  evidence: Unverified medium. `BeginOutputReadLine` and `BeginErrorReadLine` sit outside the start try, but the existing `exit 1` task test still names the task. A process that exits before those readers attach would settle whether `InvalidOperationException` escapes `RunAsync`.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-ext-secrets-1-isolated-runtime-provisioning.md`
  summary: The offline owner-evidence plans say a tool status of none grants permission to execute.
  evidence: PLAN.md, PLAN-v2.md, and PLAN-v3.md line 142. Those files predate this provisioning run. The closed operational gate in the same plans contradicts that sentence.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-ext-secrets-1-isolated-runtime-provisioning.md`
  summary: The owner-evidence handoff still asks for trust-root-establishment.json after the clarification said not to manufacture it.
  evidence: README.md, REVIEW.md, DOCUMENT-CHECKS.json, and PLAN-v3.md do not link OWNER-TRUST-CLARIFICATION-20261016t130702z.md. Pre-existing handoff text.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-ext-secrets-1-isolated-runtime-provisioning.md`
  summary: The sole-owner clarification does not forbid the named producer from also being the independent reviewer.
  evidence: OWNER-TRUST-CLARIFICATION-20261016t130702z.md names Jérôme Piquot as proposed producer and leaves the reviewer unidentified. Pre-existing clarification.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-ext-secrets-1-isolated-runtime-provisioning.md`
  summary: The sole-owner clarification cites historical signatures and public-material classes without identifiers or a file map.
  evidence: OWNER-TRUST-CLARIFICATION-20261016t130702z.md gives no path, fingerprint, or record id for the eight recovery signatures and does not map its four classes onto the plan's PEM and trust-root names. Pre-existing clarification.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-ext-secrets-1-isolated-runtime-provisioning.md`
  summary: The clarification review calls an agent path an independent reviewer and records no digest.
  evidence: REVIEW-OWNER-CLARIFICATION-20261016t130702z.md names `/root/plan_edge_review` and stores no content digest. It also says the review establishes no owner authority. Pre-existing review note.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-ext-secrets-1-isolated-runtime-provisioning.md`
  summary: The owner-evidence plan does not separate authorization allow from HTTP 404 for selectors 20–23.
  evidence: PLAN-v3.md requires all 23 rights decisions to pass and separately requires typed 404 responses for selectors 20–23, without saying both results are required. Pre-existing plan.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-ext-secrets-1-isolated-runtime-provisioning.md`
  summary: PLAN-v3's path-correction paragraph leaves the pronoun They without a clear subject.
  evidence: PLAN-v3.md line 571. The ENOENT sentence and REVIEW.md's rg exit 2 refer to the same missing file. Pre-existing wording.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-ext-secrets-1-isolated-runtime-provisioning.md`
  summary: Owner-evidence role codes R, M, V, and H have no producer or reviewer in the role table.
  evidence: PLAN-v3.md uses those codes on the v4 blocker crosswalk. The common role table defines T, S, C, G, L, Q, P, A, and X only. Pre-existing plan.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-ext-secrets-1-isolated-runtime-provisioning.md`
  summary: The proposed owner-evidence implementation checks omit format, lifecycle, and request-budget limits already stated elsewhere.
  evidence: PLAN-v3.md items I1, I3, and I5, and dependency steps 7–10, do not restate those limits while later measurement remains described. Pre-existing plan.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-ext-secrets-1-isolated-runtime-provisioning.md`
  summary: The owner-evidence preservation ledger and HTTP acceptance list omit the current HEAD delta and the named sanitizer and library inventory.
  evidence: PLAN-v3.md records HEAD b5e1caea in prose and tells P to include later commits, but the ledger table has no row for that delta. HTTP acceptance names sanitizer shapes and a nine-library inventory without listing them. Pre-existing plan.

## Deferred from: code review of spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics (2026-10-16)

- Startup task killed at the deadline may be reported as a task failure rather than a timeout (maybe-false, medium if true). Settle by running a task past a short deadline and reading the HXR021 text. CompositionStartupTasks.cs:160-165.
- Startup task executable is resolved against the root manifest's repository root for every module (maybe-false). Settle by declaring a relative-path task in a dependency module. CompositionStartupTasks.Select.
- Diagnostics directories (`state/diagnostics/<runId>`, `before-run/<guid>`) are created on every run and never pruned; needs a retention rule. CompositionEngine.StartAppHost.
- No test for debug-mode Package-to-Source coercion (ModuleCommandExecutionService.cs:85,968); reachable only via programmatic compositionOptions.
- Debug attach to the app process rather than the `dotnet run` launcher: build in Debug and launch the output directly (RunTopology.cs:189). Separate story; decision 2C of the story 1.8 review.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Selected startup-task termination can exceed its selected-test stopping budget.
  evidence: R2-B4: the shared Kill helper can wait 10 seconds independently of the selected execution stopping timestamp; coordinate this with the selected-test lifecycle work.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Selected-observation failure reports healthy services as unready and can suppress its real cause.
  evidence: R2-B6: RunReadiness passes accumulated healthy resources instead of CurrentUnready in the concurrent selected-observation failure branch.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Completed selected-test evidence accepts empty or unhealthy readiness observations.
  evidence: R2-B7: IsSelectedTestRun checks counts/reports/cleanup but does not require nonempty all-healthy resource observations.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Selected report cleanup can throw and leave raw report material in retained workspaces.
  evidence: R2-B8: unchecked File.Delete calls in finally can replace the intended refusal on IO/permission failure.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: OCI runtime verification omits opaque-directory whiteout semantics.
  evidence: R2-B10: an upper root .wh..wh..opq does not clear a prior daprd hash, unlike .wh.daprd.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Concurrent selected-test assertions currently block module test compilation.
  evidence: R2-VO1: final solution build reports CS0841/CS0165 in ModuleTestSelectionTests before selectedRun/options are initialized; further analyzer errors are in SelectedObservationTests.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Functional observation object keys can retain exact handoff credentials.
  evidence: R2-VO2 (high): exact APP/DAPR credential comparison applies only to JSON string values. An inert reflection repro accepted a credential key and returned its raw bytes; script /tmp/story18-review-19m2i10g/observation-key-repro.ps1.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Isolate the existing CLI version-metadata test's intermediate/reference outputs.
  evidence: `references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/Workspace/PlatformIdentityTests.cs:201` builds the real CLI project with `Version=9.9.9` and a temporary `OutputPath`, while updating its shared default `obj/Debug/net10.0/ref` assembly. After a full module suite, a subsequent `BuildProjectReferences=false` test-only rebuild compiled against CLI 9.9.9 while retaining the normal runtime DLL, producing 199 missing-assembly failures plus two consequent FIFO timeouts in `/tmp/story18-revision-final-tests.xml`. A normal complete solution build restores consistency; keep that build before verification and after the suite until the fixture also isolates intermediate outputs. Deferred as a pre-existing test-isolation issue; no source/pin change was made for it in Story 1.8.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Include nested build inputs in Works SDK producer qualification.
  evidence: R3-B3: adding src/Directory.Build.props to an inert selected SDK tree leaves runtime_source_snapshot's sourceFiles and inventory hash unchanged, although it changes compilation inputs. This is concurrent Works qualification work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Resolve authoritative selected-test declarations before applying a provisional execution timeout.
  evidence: R3-B4: ModuleCommandExecutionService restricts ModuleTestExecutionBudget from the working-tree manifest before resolving its recorded direct-reference revision; Restrict only reduces the budget, so a stale short timeout cannot be restored after refresh.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Bound selected terminal metadata reads and require regular files.
  evidence: R3-B6: CompositionEngine.CopySelectedMetadata uses synchronous File.ReadAllBytes on child-visible metadata paths; a FIFO can block terminal recording beyond the selected stopping budget.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Reject special files and bound selected gate reads during renewal and closure.
  evidence: R3-B7: CompositionSelectedTestGate.Write rejects links and files above 4096 bytes but not FIFOs, then calls synchronous File.ReadAllBytes under _sync; cancellation and disposal can wait indefinitely.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Exercise selected-test observations at the real AppHost readiness boundary.
  evidence: R3-V1: the pre-verified review found no test registering ICompositionTestObservationProvider. Real RunReadiness tests therefore miss incomplete/foreign observation refusal and accepted serialization; selected command tests bypass the boundary with fixture readiness.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Verify the v2 Dapr app-channel invocation boundary during Platform readiness.
  evidence: R4-B2: the preserved baseline already probes /v1.0/healthz/outbound and direct module health separately. That does not exercise an authenticated app-channel invocation, so an incorrect app-channel credential can survive readiness. The protocol predates Story 1.8.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Cover digest-only PostgreSQL requested image references in retention backend binding tests.
  evidence: R4-V1: pre-verified review found all four RetentionBackendBindsActualLoadedImageInsteadOfRequestedName cases use the tagged PostgresImage constant. No case exercises IsRequestedPostgres's supported digest-only branch; removing it leaves tests green and a supported backend observation incomplete.

## Deferred from: code review of spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md (2026-10-17, chunk 1a)

- Debug rewrites only `mapping.Mode`; a package-origin mapping passed programmatically may fail in `Closure.Load` (unverified medium).
- Startup deadline is enforced between steps only; probes use their own timeouts (unverified medium).
- Startup task stdout/stderr discarded; failure reason lost (unverified medium).
- Dependency-module startup tasks resolve against the root manifest repository root (unverified).
- Preserved diagnostics have no retention or cleanup policy.
- Source-mode builds always use Debug; compare with baseline 5636b7f for plain `run` (unverified).

## Deferred from: code review of spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md (2026-10-17, chunk 1b)

- Control loop is sequential; stop/start-eventstore waits during a refresh poll (unverified medium).
- Refresh waits to the deadline for health-check-less non-sidecar runnable resources (unverified).
- Infrastructure refresh test uses a Redis double, not Platform registrations (Platform test project blocked).

## Deferred from: code review of spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md (2026-10-17, group 2)

- `PlatformRetentionProfile.ObserveAsync` evidence logic is untested (Linux/Docker bound).
- `eng/verify_works_host.py` `run_verification` tamper checks are untested.
- Platform has no tests for the `CompositionDeclarationClosure` delegation (unverified medium).
- Only `memories` waits for `telemetryPostgres` (unverified).

## Deferred from: code review of spec-1-8 (2026-10-17)

- Timeout report may have no resource names when no failure/progress document exists; unsafe names dropped from the list (unverified medium).
- Engine `startup-failure.json` read skips run-id/schema/text re-validation (unverified).
- Later `StartAsync` failure paths use `CleanupPreparedWorkspaceAsync` without preserving diagnostics (unverified medium).
- Retained-log writer is never disposed, uncapped, opened before the deadline check (concurrent retention work).
- Cancelled-startup handler's `descriptorChildEntryAssemblyPath!` may be null (unverified).

- source_spec: `_bmad-output/implementation-artifacts/spec-ext-secrets-1-isolated-provider-inventory-installation.md`
  summary: Preexisting authorized inventory HTTP receiver uses GetValue<string> on unvalidated JSON field types, yielding 500 for malformed authenticated requests. It is unchanged by this continuation; fix shape validation and HTTP regressions before enabling externally enrolled callers.
  evidence: Operational continuation review OB9. This source is unchanged from the previously reviewed registry/inventory installation.

- source_spec: `_bmad-output/implementation-artifacts/spec-ext-secrets-1-isolated-provider-inventory-installation.md`
  summary: Preexisting inventory/current-trust reads hardcode a 30-second server budget rather than shorter configured timeout. Existing custody adapter still bounds/releases no material after its deadline; enforce shorter source cancellation before enabling a shorter-budget authority.
  evidence: Operational continuation review OB10. This source is unchanged from the previously reviewed registry/inventory installation.

- source_spec: `_bmad-output/implementation-artifacts/spec-ext-secrets-1-isolated-provider-inventory-installation.md`
  summary: Preexisting repair_native_runtime.py can overwrite recorded nonzero writer replica counts on restart after scale-down. Preserve/reuse its original journal before any future repair invocation; the tool is not invoked in this continuation.
  evidence: Operational continuation review OE4. This source is unchanged from the previously reviewed registry/inventory installation.


- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Hosted metadata-token issuance lacks a durable pre-issuance recovery identity.
  evidence: R5-B1: create_metadata_credential issues an orphan token before durable_write and parses its response outside the cleanup block; a lost response or malformed success can leave an unrecorded token. Independent hosted-secret provisioning.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Hosted Kubernetes resource creation needs intent journaling and identity reconciliation.
  evidence: R5-B2: Installer.create writes its resource ledger only after kube create returns, using non-atomic write_bytes. A lost create response or interrupted ledger write leaves an unrecorded resource. Independent hosted installation.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Inventory readiness must enforce the same HMAC retirement invariants as key resolution.
  evidence: R5-B3: CurrentInventoryAuthority.Map accepts Active metadata with retiredAt and Retained metadata before notBefore, while PlatformHmacKeySnapshot rejects them. InventoryReadiness calls the former. Concurrent hosted inventory work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Configure the TenantRegistry request-body limit through Kestrel options before host construction.
  evidence: R5-B5: the host resolves KestrelServerOptions directly after startup; an isolated default WebApplication reproduction returned null for that registration. The promised 1 MiB limit is not installed. Concurrent hosted service work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Verify the unique access-credential service set and bind it to selected readiness observations.
  evidence: R5-B8, unverified medium: ReadAccessSecretsAsync accepts six values without a unique service-set check. Settle reachability by substituting the private file before capture and demonstrating an actual handed-off credential escapes suppression; the native child starts only afterward. Concurrent selected-report work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Refuse unsupported retention host operating systems before resource creation.
  evidence: R5-B10: PlatformRetentionProfile.Select accepts non-Linux hosts while ObserveAsync requires /proc process/environment/image observations. The OCI artifact platform check does not establish host support. Independent retention work.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Define whether rejected pre-run startup attempts need a persistent attempt record.
  evidence: R5-E3: prerequisite refusals allocate no run identity and write no state, as baseline and existing no-artifact tests require. Allocated-run startup metadata does not record rejected attempts; any extension needs an attempt-identity policy.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
  summary: Exercise the retention provider actual process and loaded-image observation sequence.
  evidence: R5-V2 pre-verified regression gap: no test calls PlatformRetentionProfile.ObserveAsync; helper/model tests cannot detect a provider fallback that bypasses BoundBackendImage rejection. Add successful/mismatched observation integration in the Linux/Docker retention lane.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-5-4-ext-secrets-1-runtime-delivery.md`
  summary: PostgreSQL readiness loads its CA with a synchronous read that the 5-second token cannot cancel, and an unreadable path can throw instead of returning Unhealthy.
  evidence: Story 1.8 `PlatformPostgresqlHealthCheck.cs` calls `File.ReadAllText` outside the linked token, and the Unhealthy filter omits `UnauthorizedAccessException`. This EXT-SECRETS-1 delivery did not edit that check.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-5-4-ext-secrets-1-runtime-delivery.md`
  summary: A failed startup-task output drain can be reported as generic teardown instead of the verifier exit code, and no test injects that read failure.
  evidence: Pre-verified review of Builds `DrainAsync`: it catches `IOException` from the discarded stdout/stderr copy, and the existing bounded-output tests only cover a normal end of stream. This is Story 1.8 startup behavior.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-5-4-ext-secrets-1-runtime-delivery.md`
  summary: Story 1.8's spec is marked done while its sprint row still says review.
  evidence: `spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md` has `status: done`, and `sprint-status.yaml` still has `1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagno: review`. The secrets delivery did not write either field.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-9-ratify-and-pin-the-first-platform-accepted-tool-version.md`
  summary: Add a repeatable Platform check that the pinned module tool satisfies the accepted version range.
  evidence: Story 1.9 manually restored and ran 4.33.0 against Platform HEAD, including a 4.32.0 HXW010 refusal, but Builds identity tests use synthetic records and the Platform pin verifier does not compare these two files. Story 2.8 owns the blocking CI tier.
  status: resolved
  resolution: Superseded by the merged pin/range/tag entry from the 2026-10-18 second-pass review of story 1.9.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-9-ratify-and-pin-the-first-platform-accepted-tool-version.md`
  summary: Align Platform's EventStore reference declaration provenance (3.120.0) with the Builds catalog (3.124.0) so package mode can run from the Platform root.
  evidence: The accepted tool 4.33.0 `run --mode package --manifest src/Hexalith.Platform.Composition/Declarations/eventstore.json` passes identity and then exits 3 with only HXP020 (declaration 3.120.0 vs catalog 3.124.0).

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-9-ratify-and-pin-the-first-platform-accepted-tool-version.md`
  summary: Define how Platform release commits get `v<SemVer>` tags, so that module workspaces can run package mode against an accepted Platform.
  evidence: Platform has no local or remote tags, and Builds identity resolution refuses package mode in module workspaces with HXW011 when the Platform gitlink is untagged.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-9-ratify-and-pin-the-first-platform-accepted-tool-version.md`
  summary: Add an automated check that the `.config/dotnet-tools.json` module tool pin satisfies `hexalith-platform.json` `toolVersionRange`.
  evidence: Pre-verified review gap: no Platform or Builds test reads the committed range together with the pin; Story 2.8 owns the blocking CI tier.
  status: resolved
  resolution: Duplicate of the entry above; superseded by the merged pin/range/tag entry from the 2026-10-18 second-pass review of story 1.9.

## Deferred from: code review of spec-1-9-ratify-and-pin-the-first-platform-accepted-tool-version.md (2026-10-18, second pass)

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-9-ratify-and-pin-the-first-platform-accepted-tool-version.md`
  summary: Add one automated Platform check covering the module tool pin, the accepted range and the Builds release tag.
  evidence: Pre-verified review gap. No Platform or Builds test reads `hexalith-platform.json` `toolVersionRange` together with the `.config/dotnet-tools.json` pin. Builds identity tests only use synthetic ranges, none with a `-0` bound.
    - Nothing would catch a regression of the human-decided `[4.33.0, 5.0.0-0)`: back to `5.0.0)`, which re-admits 5.0 prereleases; an inclusive bound; an exclusive floor that refuses the pin; or a pin outside the range.
    - Nothing checks that the Builds gitlink equals the `v<pin>` tag commit.
    - The check must parse with `NuGet.Versioning` directly. In-process `IdentityFindings` always carry HXW010, because the source-built Tooling assembly is `1.0.0-…`.
    - Story 2.8 owns the blocking CI tier.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-9-ratify-and-pin-the-first-platform-accepted-tool-version.md`
  summary: Make `hexalith-module` compare its embedded `PlatformVersionCatalog` with the catalog at Platform's Builds gitlink, and refuse in package mode or warn in source mode on a mismatch.
  evidence: Human-resolved review decision, 2026-10-18 (keep the 4.x range).
    - Under `[4.33.0, 5.0.0-0)`, any later 4.x tool passes identity. It reads Dapr CLI/runtime, Redis image/digest and AppHost SDK from its embedded snapshot (`CompositionToolchainPins`, `SupportedPlatformPins`), while package versions come from the gitlink catalog.
    - `PlatformIdentityResolver` only checks that the gitlink commit exists (`:193-196`), so a module that pins a newer tool mixes the two catalogs, and the Redis image drifts silently.
    - The same drift admits 4.33.x after a future pin move, because no floor rule exists.
    - Deferred because 1.9 must not change the Builds identity reader; owner is Builds or Story 2.8.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-10-run-and-debug-eventstore-and-memories-from-the-platform-workspace.md`
  summary: Add the path-segment validation and root-containment check to `ResolveReferencedModuleProjectPath` in both `Hexalith.EventStore.Aspire` and `Hexalith.Memories.Aspire`, matching `GetProjectPath`.
  evidence: Review pass 1 of Story 1.10 (B6/E4). The method was rewritten but still combines `moduleDirectory` and the segments unchecked, so a rooted or `..` segment escapes the consuming root. This predates the story and is tracked as EventStore DW-207. Callers today pass only constant literals.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-10-run-and-debug-eventstore-and-memories-from-the-platform-workspace.md`
  summary: Make `hexalith-module run`/`debug` print the run id in human output, and stop stale failed-run states from making `down --manifest` refuse with HXR024.
  evidence: Review pass 1 of Story 1.10 (B11/E3). In `evidence/1-10/diagnostic-eventstore.txt:45-53`, `debug` reported only launcher ids, then `down --manifest` failed with HXR024 because earlier failed runs share the manifest hash. Only `down --run-id` worked. Run states persist as `~/.local/share/hexalith-builds/g4-runs/<runId>.json` (`CompositionRunStateStore.cs:27-28`). This is existing Builds Story 1.8 behaviour; 1.10 only documents the workaround.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-10-run-and-debug-eventstore-and-memories-from-the-platform-workspace.md`
  summary: Update `Hexalith.Tenants.Aspire` `TenantsServerProjectMetadata` remarks, which still describe the removed seven-location probing, when Tenants takes the EventStore.Aspire release from Story 1.10.
  evidence: Review pass 1 of Story 1.10 (B5b). `references/Hexalith.Tenants/src/Hexalith.Tenants.Aspire/TenantsServerProjectMetadata.cs:11,21` say the helper "tolerates every layout" in the `Directory.Build.props` order. After the update it resolves only the self `.slnx` root or `references/<Module>` and throws otherwise. Consumer repositories are not edited in 1.10 (frozen decision); belongs to Tenants module adoption (AR-50).

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-10-run-and-debug-eventstore-and-memories-from-the-platform-workspace.md`
  summary: Verify whether a stalled packaged-loader Aspire-path resolver needs its own bounded child process (medium severity, unverified).
  evidence: Review pass 2 R2-B10/R2-E2 found the new pwsh resolver is synchronous and outside Invoke-PackagedHostChild's timeout. No stall was reproduced; a deliberately delayed resolver plus a gate-duration assertion would settle the operational impact before adding timeout handling.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-5-4-ext-host-1-full-qualification-verifier.md`
  summary: Correct the Platform README claim that the default Agents host script expects a successful Release AppHost build.
  evidence: `README.md` "Verify Agents host contract" still says `./eng/verify-agents-host.sh` succeeds as a Release build. Default Full already exited at the verifier before any build, and this story's composition doc states that refusal. The README section predates the qualification pass path.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-5-4-ext-host-1-full-qualification-verifier.md`
  summary: Give the EXT-PROTECTION custody diagram an explicit wait for permanent deletion, cancellation, and missing CloudTrail evidence.
  evidence: The separate custody design already in the working tree moves `Prepared` to `WaitingForAuthority` and back to `Reserved` without a deletion wait, while its prose requires permanent deletion of an abandoned successor before another arm. This story does not own that design.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-5-4-ext-host-1-full-qualification-verifier.md`
  summary: Turn a missing `dotnet` executable into the EXT-HOST-1 refusal instead of a traceback.
  evidence: `run_suite` calls `subprocess.run` and catches only `TimeoutExpired`. `FileNotFoundError` escapes. The helper is preserved from the previous verifier and was not introduced by the pass path.

## Deferred from: code review of spec-1-10-run-and-debug-eventstore-and-memories-from-the-platform-workspace.md (2026-10-18, pass 3)

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-10-run-and-debug-eventstore-and-memories-from-the-platform-workspace.md`
  summary: Add an automated Platform check that the module tool pin, `hexalith-platform.json` `toolVersionRange` and the Builds gitlink release tag agree.
  evidence: Review pass 3 verification gap. No Platform test reads `.config/dotnet-tools.json`, and source-mode `debug` only warns (HXW010). The range floor `4.33.1` set by `cf20db9` is now part of the invariant.
  status: resolved
  resolution: Duplicate of the Story 1.9 second-pass entry "Add one automated Platform check covering the module tool pin, the accepted range and the Builds release tag" (owner Builds or Story 2.8).

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-10-run-and-debug-eventstore-and-memories-from-the-platform-workspace.md`
  summary: Give users a run-id lookup when no launcher id was reported, after a plain `run` or when `debug` reports "launcher process id is unavailable" (`CompositionEngine.cs:1068`).
  evidence: Review pass 3 (edge-case hunter). The documented recipe matches `projectProcessIds` with the reported launcher id, so it has nothing to match in these cases.
  status: resolved
  resolution: Duplicate of the Story 1.10 pass-1 entry "Make `hexalith-module run`/`debug` print the run id in human output…", which removes the need for the lookup.


## Deferred from: code review of spec-1-10-run-and-debug-eventstore-and-memories-from-the-platform-workspace.md (2026-10-18, pass 4)

- source_spec: `spec-1-10-run-and-debug-eventstore-and-memories-from-the-platform-workspace.md`
  summary: Clarify mixed-revision revocation selection for EventStore published deletion trust.
  evidence: R4-B3: revoked entries are filtered before the unique live match; the owner must specify whether a newer revoked revision supersedes an older entry and test that mixed profile. Medium impact if confirmed; unverified. Independent owner commits 39b366bca3dbe508b4cf3c5afa69db1ddadb6dd2/993be1bcc03fea06b9cd451ad04d57783a119e83.

- source_spec: `spec-1-10-run-and-debug-eventstore-and-memories-from-the-platform-workspace.md`
  summary: Capture owned SubjectPublicKeyInfo bytes in the EventStore published-trust authority.
  evidence: R4-B4/R4-E3: PublishedTrustDeletionConsumptionAuthority copies the collection but retains caller-owned byte arrays that SignatureMatches later imports; caller mutation can replace accepted key material without a metadata revision. Independent owner security work.

- source_spec: `spec-1-10-run-and-debug-eventstore-and-memories-from-the-platform-workspace.md`
  summary: Reject incompatible actor implementations registered under EventStore protection actor names.
  evidence: R4-B6/R4-E4: InteractionPayloadProtectionServiceCollectionExtensions.RegisterActor returns on ActorTypeName alone, silently preserving a different CLR actor during explicit protection opt-in. Independent owner security work.

- source_spec: `spec-1-10-run-and-debug-eventstore-and-memories-from-the-platform-workspace.md`
  summary: Define bounded recovery when an EventStore original writer task never settles.
  evidence: R4-B7: InteractionOccurrenceProtector.AwaitAsync and actor lifecycle tracking deliberately retain the original writer after cancellation; a permanently incomplete write holds the actor turn indefinitely. Preserve exclusion from unfinished mutations when designing quarantine or host recovery. Independent owner security work.

- source_spec: `spec-1-10-run-and-debug-eventstore-and-memories-from-the-platform-workspace.md`
  summary: Capture the Docker health-probe result for a recurring Platform Sentry readiness failure.
  evidence: R4-B10: exact-cf20db9 class failed Memories with Sentry Running/Unhealthy; the same source and binaries passed a focused retry. The underlying HTTP/health result was lost at teardown. Capture health-check output and reproduce before attributing a medium runtime repeatability defect; unverified. Evidence: evidence/1-10/live-tests-final.txt and live-tests-final-retry.txt.

- source_spec: `spec-1-10-run-and-debug-eventstore-and-memories-from-the-platform-workspace.md`
  summary: Isolate ES256 rejection in EventStore published-trust authority regression tests.
  evidence: R4-V1: the altered-JWS test independently fails guard equality and the malformed-SPKI test fails before VerifyData. Preserve a valid matching request/guard but replace only the anchor with another well-formed P-256 key; assert dispatch denial. Independent owner security work.

## Deferred from: code review of spec-1-10-run-and-debug-eventstore-and-memories-from-the-platform-workspace.md (2026-10-18, pass 5)

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-10-run-and-debug-eventstore-and-memories-from-the-platform-workspace.md`
  summary: Refresh the Builds package-version audit for Commons 2.30.3 so Builds `main` CI passes again.
  evidence: Review pass 5 (blind hunter, acceptance auditor). Owner commit `1dfc39e` raised `HexalithCommonsVersion` to 2.30.3 in `Props/Directory.Packages.props` without refreshing `Tools/package-version-audit.json`. `Tools/validate-package-version-audit.ps1` reports a `catalogSha256` mismatch and 12 Commons packages with no accepted audit selection. Builds CI `39144143974` (`1dfc39e`) and `39157741848` (`5c04dab`) fail at "Validate package version audit"; the next Builds release is blocked. Independent of the 1.10 Builds fix, which released from `c6f56c0`.
  status: open

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-1-10-run-and-debug-eventstore-and-memories-from-the-platform-workspace.md`
  summary: Make `ExplicitDeclarationKeepsItsWorkspaceAnchorAndValidation` independent of the test output location.
  evidence: Review pass 5 (blind hunter). The pass-3 full Composition rerun failed this absolute-path substring assertion when the test output was under `/tmp`; byte-identical output passed under `/home` (`evidence/1-10/failure-membership-final.txt`, `composition-tests-final-relocated-bin.txt`). It predates Story 1.10 and had no ledger entry. Developers running the suite from a `/tmp` worktree see a spurious failure.
  status: open

## Deferred from: code review of spec-1-11-run-and-debug-mcpcli-from-the-platform-workspace.md (2026-10-20, pass 2)

- source_spec: `spec-1-11-run-and-debug-mcpcli-from-the-platform-workspace.md`
  summary: Release Builds with the run-scoped McpCli build, pin it in Platform, move the Builds gitlink to its tag, and rerun AC1 with the pinned tool.
  evidence: Review pass 2 (BH2, AA2, AA3). `.config/dotnet-tools.json` pins `4.33.1`, which builds no McpCli. The Platform Builds gitlink is at untagged `cf97810`. The only AC1 proof used `4.34.0-local.1` at `d7c828a`, before the review fixes, so the post-review `HXI010` text (DLL plus project) has no Platform record. This is tracked by the spec's open orchestrator task "release and pin". The rerun should use the README's function-breakpoint recipe as written.
  status: resolved
  resolution: Builds 4.34.0 was released and pinned with the Builds gitlink at its release tag; see [release-current.json](evidence/1-11/release-current.json). The pinned-tool AC1 proof reached ready, hit the README's function breakpoint, verified all four first-party PDB source origins, and removed the DLL and workspace with down; see [pinned-proof.json](evidence/1-11/pinned-proof.json).

- source_spec: `spec-1-11-run-and-debug-mcpcli-from-the-platform-workspace.md`
  summary: Restore Platform's `CentralPackageTransitivePinningEnabled` in the pinned-catalog Platform model build.
  evidence: Review pass 2 (BH6). `PlatformManifestCommand.BuildModelAsync` passes `PinnedCatalogArguments`, so `-p:ImportDirectoryPackagesProps=false` skips Platform `Directory.Packages.props:2`, which enables transitive pinning. The Builds catalog sets only `ManagePackageVersionsCentrally`, so the catalog's transitive security pins (for example GHSA-334f, `Props/Directory.Packages.props:224`) do not apply to the model build. Story 1.11 fixed this for the McpCli build only. Pre-existing. Pass 5 (P5-BH6): both builds now share `PinnedCatalogArguments`, so moving `-p:CentralPackageTransitivePinningEnabled=true` into it fixes both; this needs a Builds release and a check that the Platform model restore still succeeds.
  status: open

- source_spec: `spec-1-11-run-and-debug-mcpcli-from-the-platform-workspace.md`
  summary: Add one real-process cancellation and deadline test that covers `BuildMcpCliAsync` and `BuildModelAsync`.
  evidence: Review pass 2 (VG2). Both builders pass the remaining time and the startup token to `CompositionProcess.RunAsync`, but their tests replace the whole builder (`McpCliBuildOverride`, `PlatformModelBuildOverride`). Passing `CancellationToken.None` would fail no test. A fake project whose build blocks (an `Exec` that sleeps), with a cancelled token or a short deadline, should end within seconds with `OperationCanceledException`. The model-build gap is pre-existing.
  status: open

## Deferred from: story 1.11 resumed review (2026-10-20, pass 3)

- source_spec: spec-1-11-run-and-debug-mcpcli-from-the-platform-workspace.md
  summary: Make McpCli's existing test dependency gate handle approved MSBuild property-based Include expressions without rejecting their raw forms.
  evidence: Pass-3 P3-BH2/P3-EH1. references/Hexalith.McpCli/tests/Hexalith.McpCli.Manifest.Tests/DependencyPolicyTests.cs:892-901 concatenates raw Include values with evaluated items; the checker rejects an unresolved raw expression even when its evaluated package or project is approved. Introduced by McpCli story 4.7 commit f2242b76e117c93f3bcf77320ca5d133a61e9308, before this story's declaration change.
  status: open

- source_spec: spec-1-11-run-and-debug-mcpcli-from-the-platform-workspace.md
  summary: Cover inactive conditional test dependencies declared in imported MSBuild props in McpCli's dependency gate.
  evidence: Pass-3 P3-BH3. references/Hexalith.McpCli/tests/Hexalith.McpCli.Manifest.Tests/DependencyPolicyTests.cs:892-901 reads literal declarations only from the csproj; Debug/Release evaluation also omits a forbidden imported reference whose condition is false in both configurations. Pre-existing story 4.7 behavior, outside the McpCli run-scoped build.
  status: open

- source_spec: spec-1-11-run-and-debug-mcpcli-from-the-platform-workspace.md
  summary: Refresh the cached Epic 1 stack pins from the accepted tool/catalog and SDK configuration.
  evidence: Pass-3 P3-BH8. _bmad-output/implementation-artifacts/epic-1-context.md:47-52 says SDK 10.0.401/Aspire 13.5.4 with no upgrades, while global.json and the accepted-tool evidence use 10.0.402/13.6.1. Defer edits to agent context under the build review workflow.
  status: open

- source_spec: spec-1-11-run-and-debug-mcpcli-from-the-platform-workspace.md
  summary: Restore the Works preview Development-only condition in the cached Epic 1 technical decision.
  evidence: Pass-3 P3-BH9. _bmad-output/implementation-artifacts/epic-1-context.md:69 describes only Platform:Works:Enabled, while src/Hexalith.Platform.Composition/WorksPreviewModel.cs:55 enforces Development as well. Defer edits to agent context under the build review workflow.
  status: open

- source_spec: `spec-1-11-run-and-debug-mcpcli-from-the-platform-workspace.md`
  summary: Clarify the cached Epic 1 current/previous-major compatibility rule to retain the explicit v1 enrollment exclusion.
  evidence: Review pass 4 P4-BH6. epic-1-context.md:30 accepts current and previous majors without qualifying its preceding rule that v1 is never accepted for enrollment. The context wording can mislead later planning despite the explicit runtime/schema rejection; edits to agent context are deferred by the build review workflow.
  status: open

## Deferred from: code review of spec-1-11-run-and-debug-mcpcli-from-the-platform-workspace.md (2026-10-20, pass 5)

- source_spec: `spec-1-11-run-and-debug-mcpcli-from-the-platform-workspace.md`
  summary: Add an automatic check that the `.config/dotnet-tools.json` pin is within `hexalith-platform.json`'s `toolVersionRange` and that the committed `references/Hexalith.Builds` gitlink equals the commit of tag `v<version>`.
  evidence: Review pass 5 (P5-VG3). No Platform test or CI reads the tool pin; the README's claim that only the pinned version is guaranteed to embed the catalog rests on hand-run evidence (`evidence/1-11/release-current.json`). The pin and gitlink drifted apart once during this story (pass-2 ledger entry: pin 4.33.1, gitlink untagged `cf97810`) with no test failing. Pre-existing Story 1.9 process rule; a Composition test comparing the pin with `git ls-tree HEAD references/Hexalith.Builds` and the tag would fit.
  status: open

## Deferred from: code review of spec-2-1-decide-the-candidate-and-attachment-policy.md (2026-10-20)

- source_spec: `spec-2-1-decide-the-candidate-and-attachment-policy.md`
  summary: Point downstream Epic 2/3 planning at the published `hexalith.platform.candidate-and-attachment-policy.v1` record.
  evidence: Code review of commit 8b7a1fe (Blind Hunter). epic-2-context.md says "planning does not already choose these values" with no link to `docs/contracts/candidate-and-attachment-policy-v1.md`; epics.md Story 2.2's descriptor field list lacks `policyId`, decision identity, reuse lineage, executed-suite/completion and tool-qualification bindings; Stories 3.1/3.9 still carry pre-C-01 wording ("policy from Story 3.1", reuse "under the policy's recorded reuse conditions"). Story spec authors reading planning context may miss the policy. Edits to planning/agent-context artifacts are deferred by the review workflow.
  status: open

- source_spec: `_bmad-output/implementation-artifacts/spec-1-12-adopt-parties-as-the-reference-domain-module.md`
  summary: Repair the pre-existing EventStore release snapshot/catalog provenance mismatch in package composition tests.
  evidence: Story 1.12's normal composition build passes, but eight broad-test failures use unchanged EventStore snapshot 3.120.0 against accepted catalog 3.126.0; `evidence/1-12/coordinator-broad-failure-assessment.json` records the baseline comparison and `coordinator-composition-tests.txt` the failures.
- source_spec: `_bmad-output/implementation-artifacts/spec-1-12-adopt-parties-as-the-reference-domain-module.md`
  summary: Copy declared executable test entries into the pre-existing isolated owner-manifest fixtures.
  evidence: Two composition fixture cases fail because newer owner manifests reference test-entry files absent from copied fixture roots; implicated test bodies are unchanged from the 1.12 baseline, recorded in `evidence/1-12/coordinator-broad-failure-assessment.json`.
- source_spec: `_bmad-output/implementation-artifacts/spec-1-12-adopt-parties-as-the-reference-domain-module.md`
  summary: Isolate the pre-existing gateway seed test content root during parallel composition tests.
  evidence: The broad run reports one gateway-seed DirectoryNotFound failure; all four unchanged gateway seed cases pass in isolation (`evidence/1-12/coordinator-seed-tests.txt`), so the parallel fixture/content-root interference needs investigation.
- source_spec: `_bmad-output/implementation-artifacts/spec-1-12-adopt-parties-as-the-reference-domain-module.md`
  summary: Make the historical Parties catalog receipt independent of mutable git-describe tag selection.
  evidence: The unchanged receipt assertion expects `v4.33.1-49-g5d03f1db`, while the same catalog commit now has tag `v4.34.0`; owner run passes 2004/2005, with the exact failure and unchanged-method diff in `evidence/1-12/historical-receipt-failure.txt` and `receipt-test-diff.patch`.

## Deferred from: code review of spec-1-13-publish-the-domain-module-enrollment-kit.md (2026-10-21, pass 3)

- source_spec: `spec-1-13-publish-the-domain-module-enrollment-kit.md`
  summary: Remove programmatic subscription discovery and literal Dapr component names from Parties production code before its kit enrollment.
  evidence: Released `4.35.1` `validate --adoption` on real Parties `6d9e341` fails `dapr-subscriptions` on `src/Hexalith.Parties/Program.cs:75` (`app.MapSubscribeHandler()`), `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`, `HealthChecks/PartiesHealthCheckExtensions.cs`, `src/Hexalith.Parties.Security/ErasureVerificationService.cs` and `appsettings*.json` (`evidence/1-13/post-release-patch/release/released-package/real-parties-human.stdout.txt`). The Story 1.12 README says Platform renders the declarative tenant subscription; the epic requires programmatic discovery to be disabled. This is Parties-owned code, and Story 1.13 must not adopt another module. Feed it into the Story 1.12 review and Parties' kit enrollment.
  status: open

## Deferred from: code review of spec-1-13-publish-the-domain-module-enrollment-kit.md (2026-10-21, pass 5)

- source_spec: `spec-1-13-publish-the-domain-module-enrollment-kit.md`
  summary: Gate Platform pushes on the pinned-tool ↔ Builds-gitlink identity, so that `/pushall` cannot silently advance a release-pinned submodule.
  evidence: `/pushall` checks out the default branch of every submodule and merges every local branch (`.github/skills/pushall/SKILL.md:74-86`). On 2026-10-21, Platform commit `37715f3` moved `references/Hexalith.Builds` from release tag `v4.35.1` (`f8599224`) to unreleased `359fee2`. `AcceptedToolPinEqualsSupportedRangeFloorAsync` fails at that HEAD, but Platform has no CI workflow, so nothing ran it. Fix: have `/pushall` skip advancing a submodule whose gitlink equals a pinned release tag, or add a Platform CI workflow that runs that test.
  status: open
  note: 2026-10-22: it recurred. `/pushall` commit `10c50af` moved the gitlink to `v4.35.2` before the Platform pin moved. The owner declined adding Platform CI on 2026-10-22.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-ext-protection-1-custody-guard-admission.md`
  summary: The Works proof gate can qualify a different Dapr runtime than the checked-in pin and the Works CI install.
  evidence: `eng/works_toolchain.py` hashes daprd without a timeout, `eng/test_verify_works_host.py` never fails `run_verification` on a mismatched pin, and `references/Hexalith.Works/.github/workflows/ci.yml` still installs runtime 1.18.3 while `config/works-proof-toolchain.json` pins 1.18.7. This landed outside the custody guard commit.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-ext-protection-1-custody-guard-admission.md`
  summary: Works proof rows copy later target commands, and the linked-snapshot drift error still names only Identity or Parties.
  evidence: `eng/verify_works_host.py` stores `target_commands` on every proof row and raises "Identity or Parties" at the drift check. Those edits are not part of the custody guard.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-ext-protection-1-custody-guard-admission.md`
  summary: The Story 1.13 real-module compare script drops every failing row.
  evidence: `real-module-compare.py.txt` ends the failing-row collection with `[:0]`, so `candidateFailing` is always empty. That file is Story 1.13 evidence, not the custody guard.
  status: resolved
  resolution: Only the informational `candidateFailing` field is affected. The baseline/candidate row comparison uses the `onlyBaseline`/`onlyCandidate` set differences, which are intact. Released `4.35.2` rechecked at compared Parties `61921f7c` passes `workspace` and `dependency-selectors` (`evidence/1-13/pass-5/real-module-compare-parties-recheck.json`). The pass-5 README records the correction, and the executed script stays as it ran.

## Deferred from: Story 1.13 release acceptance (2026-10-22)

- source_spec: `spec-1-13-publish-the-domain-module-enrollment-kit.md`
  summary: Parties `Directory.Packages.props` reintroduced sibling and ancestor Builds catalog fallbacks, which the enrollment conventions forbid.
  evidence: Parties commit `a8e52fdc` (2026-10-21) replaced `HexalithBuildsRoot` defaulting to `references/Hexalith.Builds` with `Exists`-guarded imports of `references/`, `../Hexalith.Builds` and an unconditional `../../Hexalith.Builds` catalog. Released `4.35.2` fails `dependency-selectors` on `Directory.Packages.props` at Parties gitlink `9cfeb6c6` (`evidence/1-13/pass-5/release/released-package/real-parties-current-gitlink-json.stdout.txt`). `4.35.1` hid this behind its asserted-probe false positive. Parties owns the fix during its kit enrollment: restore the tool-mapped `HexalithBuildsRoot` bootstrap and let release CI supply the Builds mapping. Story 1.13 must not adopt another module.
  status: open

## Deferred from: code review of spec-1-12-adopt-parties-as-the-reference-domain-module.md (2026-10-22)

- source_spec: `spec-1-12-adopt-parties-as-the-reference-domain-module.md`
  summary: Tenants' owner deploy ACL lacks the `parties GET /ready` grant that the Tenants manifest now declares.
  evidence: Story 1.12 added caller `parties` and `GET /ready` to `references/Hexalith.Tenants/module/hexalith.module-manifest.v2.json` (Tenants `9a94131`, unchanged at HEAD). The owner ACL `deploy/dapr/accesscontrol.tenants.yaml` still grants only `eventstore`. The grant exists only in Parties' legacy `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.tenants.yaml`. No check compares the Tenants manifest with the Tenants owner ACL; Platform's checker compares only `accesscontrol.eventstore.yaml`. Parties readiness under that owner ACL was already denied before this story. The Tenants owner decides whether to add the grant to the deploy ACL or record the manifest grant as local-only.
  status: open

- source_spec: `spec-1-12-adopt-parties-as-the-reference-domain-module.md`
  summary: Platform's reference-declaration checker validates with `assert`, so its checks vanish under `python -O`.
  evidence: `scripts/test-reference-module-declarations.py` has 117 `assert` statements; Story 1.12 review finding B1 fixed only the Parties renderer copy (`references/Hexalith.Parties/scripts/render-platform-declaration.py`). This predates Story 1.12. Convert the checks to explicit raises with an optimized-process regression case, as Parties did.
  status: open

- source_spec: `spec-1-12-adopt-parties-as-the-reference-domain-module.md`
  summary: Platform hardcodes each domain module's EventStore registration, so every domain enrollment needs Platform code.
  evidence: `src/Hexalith.Platform.Composition/PlatformAppModel.cs` sets `EventStore__DomainServices__Registrations__wildcard_party_v1__*`, `wildcard_tenants_v1__*` and `*|timesheets|v1__*` literally; the v2 declaration has no field for the EventStore domain name or method. The Parties project path is also duplicated in `PlatformServerBindings.ReferenceProjects` and `AgentsHostComposition.cs:20`. Owner-led Tenants/Folders/Projects enrollment through the Story 1.13 kit will need Platform edits that the enrollment guide does not mention. A fix needs a declaration-schema field.
  status: open

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-5-4-ext-secrets-1-ext-host-1-shared-delivery.md`
  summary: Custody manifest build still signs a later target whose tenant differs from the authority tenant.
  evidence: `CustodyBindingDaprCall.Build` compares only `targets[0].TenantId` with the authority. The previous `Build` also put every target on the consumption request. This story did not introduce that acceptance.

- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-5-4-ext-secrets-1-ext-host-1-shared-delivery.md`
  summary: Custody manifest enrollment records only the first identity store.
  evidence: `CustodyBindingDaprCall.Envelope` uses `manifest.Targets[0].StoreTarget` for the whole request. The previous `Build` did the same. This story did not introduce that acceptance.


- source_spec: `_bmad-output/implementation-artifacts/spec-epic-1-retro-remediation.md`
  summary: Reconcile the accepted Platform tool release with the integrated Builds gitlink before release and enrollment qualification.
  evidence: Medium, pre-existing concurrent integration issue from verification review. Platform e02fa5bf43ba9c2561dc2da940264de1c9da3242 records Builds 2d653b611c8efcffb4d27bd8715d5dbf24eaebfd while the restored/pinned 4.35.2 tool and release tag identify 4248b8c70cb35ba79fdd7e4dd63049d4c73211a0. Three enabled DomainEnrollmentKitTests fail on that exact comparison; see evidence/epic-1-retro-remediation/composition-full.txt and README.md. Accountable owners: Platform/Builds release coordination maintainers. Preserve the integrated user revisions and fail-closed checks; this build does not publish or repin a tool.

## Deferred from: Story 4.11 approved Option A (2026-10-22)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-11-define-the-check-suite-contract-and-own-the-reference-suites.md`
  summary: Resolve Tenants positive reference-suite coverage before publication and Story 4.11 completion.
  owner: Platform `@jpiquot`; review with Tenants and EventStore maintainers.
  evidence: `checks/reference/registry.json` retains `tenant-management` and `global-administrator-management` as blocked critical-flow mappings, plus blocked `tenant-read-smoke`. Existing authorization requires authority unavailable in the synthetic-client contract. `tenants.admin-denied` is supplementary denial evidence and never substitutes for positive tenant creation/read or administrator coverage.
  prerequisites: Decide and implement authorization for declared run-scoped synthetic tenant lifecycle; decide positive administrator coverage without granting production administration; admit a safe read of an existing synthetic tenant. Preserve critical-flow declarations and authorization. Resolve source-to-check mappings and obtain matching hosted evidence through the trusted executor before closing 4.11.
  publication_gate: Builds reference coverage validation must refuse publication while any mapping is blocked; Story 4.12 must call it before publication.
  status: open

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-qualify-the-aspire-to-helm-export-with-the-reference-composition.md`
  summary: Bound candidate command output while reading in the separate reference-check runner.
  evidence: BH-6: checks/reference/runner.py communicates into memory before enforcing 1 MiB; concurrent story 4.11 code, independent of Helm qualification.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-qualify-the-aspire-to-helm-export-with-the-reference-composition.md`
  summary: Derive the Parties synthetic aggregate identifier from the complete invocation identity.
  evidence: BH-10: checks/reference/parties.py truncates invocationId to 110 characters, so distinct accepted suffixes collide; separate story 4.11 work.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-qualify-the-aspire-to-helm-export-with-the-reference-composition.md`
  summary: Attempt cleanup of every concurrent live-test session even when an earlier teardown fails.
  evidence: EH-5: PlatformConcurrentRunTests.cs exits its finally loop on the first cleanup failure; concurrent story 2.3 code, unchanged by story 4.5.

- source_spec: `spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Persist external issuer-child ownership for crash-safe runner lifecycle cleanup and leftover reporting.
  evidence: Story 2.3 creates a private child under an explicitly selected external certificate parent, with normal AppHost-disposal cleanup. Forced termination bypasses disposal and workspace-only runner cleanup cannot discover that external child. Verify by terminating an owned AppHost before disposal; resolve with the runner lifecycle retention/cleanup work without touching foreign parent contents.

## Deferred from: code review of spec-2-2-define-the-test-environment-descriptor-and-result-contract.md (2026-10-22)

- source_spec: `spec-2-2-define-the-test-environment-descriptor-and-result-contract.md`
  summary: Test the HXT105 refusal for non-seekable contract input.
  evidence: `TestEnvironmentContractReader.Read` refuses `!stream.CanSeek` with HXT105, but every test reads a regular temporary file. Without that check, `stream.Length` would throw `NotSupportedException` and the input would surface as HXT106, and no test would fail. Needs a platform-specific FIFO harness; the opening FIFO hang itself was rejected as low.
  status: open

- source_spec: `spec-2-2-define-the-test-environment-descriptor-and-result-contract.md`
  summary: Run the Platform canonical/Builds mirror schema parity unittest automatically.
  evidence: `tests/contracts/test_test_environment_contract_mirrors.py` only runs manually with `-s tests/contracts`. Plain `python3 -m unittest discover -s tests` finds zero tests because there is no `__init__.py`, and Platform has no workflow or script that invokes it. A Builds-only mirror edit or a Platform-only canonical edit would diverge unnoticed. Wire it into the contract verification script once one owns `docs/contracts`.
  status: open

## Deferred from: code review of spec-4-5-qualify-the-aspire-to-helm-export-with-the-reference-composition.md (2026-10-22)

- source_spec: `spec-4-5-qualify-the-aspire-to-helm-export-with-the-reference-composition.md`
  summary: Scope configuration inputs and secrets per server instead of per module.
  evidence: `runtime.extensions[].configurationInputs` is module-scoped, so `PlatformHostedPublishing.Describe` and `PlatformLocalProfile.Bind` give every enabled server the full list. Each of the three Memories workloads gets 48 inputs, including `Conversation:ProviderApiKey` and the JWT signing key; `tenants-api` gets every Tenants domain secret. This is pre-existing declaration shape, and story 4.5 forbids altering module declarations. It needs a declaration-contract change before real staging Secrets bind, for least-privilege exposure.
  status: open

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-qualify-the-aspire-to-helm-export-with-the-reference-composition.md`
  summary: Zero every owned custody binding secret buffer on both success and exceptions.
  evidence: BindingService decodes wrap input twice; its first RootKey is never cleared and exceptions bypass success-only byte-array clearing.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-qualify-the-aspire-to-helm-export-with-the-reference-composition.md`
  summary: Implement custody delivery completion before allowing deletion closure.
  evidence: KeyExchange records deliveries, ClosureRules requires Closed deliveries, and SemanticProvider does not implement advertised complete-key-use.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-qualify-the-aspire-to-helm-export-with-the-reference-composition.md`
  summary: Persist a value-free durable completion and recovery path for custody unwrap intents.
  evidence: Unwrap leaves its intent SendCommitted while CloseNativeIntentAsync requires ResultDurable, preventing native closure.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-qualify-the-aspire-to-helm-export-with-the-reference-composition.md`
  summary: Allow custody completion writes to consume reserved completion capacity.
  evidence: AwsAuthorityStore subtracts CompletionReserveBytes from the ceiling for admissions and completions alike, so the reserve cannot be used.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-qualify-the-aspire-to-helm-export-with-the-reference-composition.md`
  summary: Compare canonical public keys when enforcing independent custody signing roles.
  evidence: ComponentProfile checks PEM string inequality, allowing the same cryptographic key under distinct whitespace or encodings.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-qualify-the-aspire-to-helm-export-with-the-reference-composition.md`
  summary: Encode custody lease and delivery-attempt identifiers unambiguously.
  evidence: Colon concatenation maps (a:b,c) and (a,b:c) to the same delivery/native-intent identity; contract text validation allows both.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-qualify-the-aspire-to-helm-export-with-the-reference-composition.md`
  summary: Retain sanitized local composition failure diagnostics without retaining generated credential material.
  evidence: PlatformAppModel writes composition-failure.txt and PlatformCompositionWorkspace preserves the entire marked directory after failure.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-qualify-the-aspire-to-helm-export-with-the-reference-composition.md`
  summary: Test the new custody Dapr binding exact-proof rejection boundary.
  evidence: Verification review found no tests of the new executable; older local-component tests do not execute SemanticProvider authorization.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-qualify-the-aspire-to-helm-export-with-the-reference-composition.md`
  summary: Test custody worker uncertainty and restart behavior across native I/O failures.
  evidence: Verification review found no RecoverableLifecycle test proving the durable SendCommitted intent survives a lost native acknowledgement and prevents resend.

## Deferred from: code review of spec-4-6-encode-release-attempt-qualification-and-administrator-records.md (2026-10-23)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-6-encode-release-attempt-qualification-and-administrator-records.md`
  summary: Define which attempt kinds may issue qualification and production-promoted records, and enforce it in `RecordTransitionValidator.ValidateIssuer`.
  evidence: `ValidateIssuer` (Builds `RecordTransitionValidator.cs:253`) accepts any `AttemptKind`, so a `rotation` or `in-place-restore` attempt can issue either record. The spine's Production profile rule legitimately lets release, environment-layer and shared-infrastructure attempts issue qualification (and renew production-promoted in production), so the allowed-kind matrix needs lifecycle design. Deferred to Story 4.7 or the lifecycle work by the Administrator decision of 2026-10-23 (review decision D2).

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Attest the selected native runtime version for direct file AppHost runs.
  evidence: R2-B4: direct entry historically selects the configured Dapr home without comparing native daprd --version to the catalog; the current local proof uses catalog-verified 1.18.7. Preserve existing source/package pins.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Reject FIFOs before opening test-environment contracts.
  evidence: R2-B7: independent Builds TestEnvironmentContractReader.Read uses File.OpenRead before CanSeek and cancellation checks, so opening a Unix FIFO can block; use the owner nonblocking-open primitive and add the refusal test.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Allow Custody lifecycle workers while their component serves its existing socket.
  evidence: R2-B8: independent infrastructure/Hexalith.Platform.Custody.Dapr/Program.cs checks socket absence before branching to --worker, rejecting that invocation whenever --serve owns its UDS. Limit listener prerequisites to serving.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Zero Custody request, decoded key and delivery buffers on exceptional paths.
  evidence: R2-B9: independent BindingService.Invoke clears arrays only after successful decode/provider/serialization; a finally path must clear every produced key-bearing buffer.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Bound independent EventStore qualification snapshots for future reviews.
  evidence: R2-B10: the all-baseline review artifact is 599335081 bytes across 9271 diff sections; repeated 20–61 MB EventStore source evidence snapshots dominate it. Preserve immutable existing evidence; consider bounded manifests for future owner receipts.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Record a cleanup outcome if releasing independent Builds resource claims fails.
  evidence: R2-E2: CompositionEngine.TearDownAsync calls Claims.ReleaseOwned outside its teardown catches; CompositionResourceClaimStore.OpenTransaction throws IOException after a lock remains held for two seconds. Introduced by independent test-environment commit db5abf96d730d82fd6c1ccfa20ffd9c2262b9838.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Verify startup acquires all persisted listener claims and teardown releases them.
  evidence: R2-G1: the verification-gap reviewer traced ReadyRunRecordsDeadlineAndDebugProcessIdsAsync and confirmed it does not assert ownership of Session.Plan.Ports; manually seeded teardown/store tests cannot detect removal of startup acquisition. Independent Builds claim lifecycle work.
  status: resolved
  resolution: Resolved by Story 2.4 round-2 ReadyRunRecordsDeadlineAndDebugProcessIdsAsync: actual primary ports are claimed after readiness and released by down; passing coverage is retained in evidence/2-4/review-round-2/builds-affected.xml and builds-coverage-union.json.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Verify parent translation and validation of child HXR031 blocking-owner failures.
  evidence: R2-G2: the verification-gap reviewer found only direct AppHostRunner serialization coverage; removing HXR031 from CompositionEngine.ReadStartupFailuresAsync passes existing tests. Add public parent startup assertions including malformed BlockingRunId to independent Builds claim lifecycle tests.
  status: resolved
  resolution: Resolved by Story 2.4 round-2 ParentStartupTranslatesExclusiveClaimFailuresAsync: public startup reads actual child HXR031 documents and checks both valid and malformed blocking owners; see evidence/2-4/review-round-2/builds-affected.xml.

## Deferred from: code review of spec-2-3-make-local-composition-resources-run-scoped.md (2026-10-23, round 3)

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Confirm the Works preview starts now that its sidecar endpoints are forced proxyless.
  evidence: Unverified (maybe-false; high if true). `PlatformDaprEndpointSubscriber.Apply` with `servers=[]` now sets `TargetPort = Port` and `IsProxied = false` on every Works sidecar http/grpc/metrics endpoint, and Works sets no Dapr ports. Aspire 13.6.1 contains both proxyless port allocation and the "needs to specify a port ... since it isn't using a proxy" refusal. Settle by starting the Works preview once (`Platform:Works:Enabled`) or running the explicit Works Platform-target lane; if refused, skip endpoints without explicit ports.
  status: resolved
  resolution: False positive (2026-10-23, round-3 follow-up). Aspire 13.6.3 `DcpExecutor.PrepareServices` calls `EnsureProxylessEndpointPort`, which allocates and assigns `Port` and `TargetPort` for a proxyless executable endpoint that has none, before the "needs to specify a port" check runs. A live AppHost on the pinned Aspire 13.6.3 and CommunityToolkit Dapr 13.6.1-beta.935 applied the same mutation to a port-less sidecar: its http/grpc/metrics endpoints were allocated distinct ports, the CLI reached Running, and daprd answered `/v1.0/healthz/outbound` with 204 on the allocated port. See evidence/2-3/proxyless-sidecar-check-20261023/.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Verify `ComposeRun` claims its secondary ports when the plan carries a claim directory.
  evidence: Story 2.4 resource-claim work. `SecondaryClaimsOnlyTheAcceptedNonOverlappingAllocation` calls `AllocateOwnedSecondaryPorts` directly; the signed isolation test's plan sets no `ResourceClaimStateDirectory`. Reverting `PlatformAppModel.cs:138` to `AllocateSecondaryPorts` passes every test. Add a `ComposeRun` assertion that a peer `Acquire` of the composed Sentry gRPC port throws `CompositionResourceClaimConflictException` with `OwnerRunId == plan.RunId`.
  status: resolved
  resolution: Resolved by Story 2.4 round-2 signed IdenticalSelectionsOwnPortsCredentialsDiscoveryAndStorageAsync: ComposeRun owns the composed Sentry port and a peer Acquire names that run; see evidence/2-4/review-round-2/platform-affected.xml.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Preserve a runner workspace when owned resource removal leaves survivors or cannot verify absence.
  evidence: R4-B2; CompositionEngine.TearDownAsync currently invokes workspace deletion after Scanner.RemoveAsync regardless of Remaining, risking bind-mounted data for surviving owned containers; lifecycle policy is excluded from Story 2.3.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Bound the reference check payload's local deadline against actual invocation time.
  evidence: R4-B5; validate_invocation bounds deadlineAt minus startedAt but Context.remaining and SIGALRM use deadlineAt against current time, so a future startedAt enlarges the payload-local budget; retain the independent executor deadline.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Require exactly one enrolled KEK for each tenant in the hosted Custody component profile.
  evidence: R4-B7; ComponentProfile.Validate permits two different protector ARNs attached to one tenant, while AwsKeyAccess.ReadTenantKekAsync later refuses the uncovered tenant; hosted Custody is excluded from Story 2.3.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Exercise the real AwsKeyAccess adapter's rejection of a mismatched KMS decrypt response ARN.
  evidence: R4-V1; the verification review found only production wiring references the adapter and key tests use FakeNative, so deleting DecryptResponse.KeyId validation would pass the existing suite; use a native API double in the hosted provider's conformance harness.

## Deferred from: code review of spec-2-4-provision-a-run-owned-environment-for-a-test-suite.md (2026-10-23, round 2)

- source_spec: `_bmad-output/implementation-artifacts/spec-2-4-provision-a-run-owned-environment-for-a-test-suite.md`
  summary: Confirm `RunEndpoints.Observe` cannot throw for a healthy resource with an unallocated endpoint.
  evidence: Unverified (maybe-false; medium if true). `RunReadiness.Observe` now calls `RunEndpoints.Observe` for every healthy resource on all run paths, including `CurrentUnready`, `ReportObservedFailureAsync` and progress writes; any unallocated endpoint throws `InvalidDataException`, which would suppress the failure document for run/debug/test. Settle by confirming whether Aspire 13.6 can report a resource `Healthy` before every endpoint is allocated (proxyless or sidecar endpoints); if it can, return only allocated endpoints from `Observe` and refuse unallocated ones only in `TestEnvironmentDescriptorWriter`.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-4-provision-a-run-owned-environment-for-a-test-suite.md`
  summary: Reject special files before opening test-environment contracts.
  evidence: R3-B7: the independently delivered Story 2.2 TestEnvironmentContractReader.Read opens File.OpenRead before CanSeek/cancellation, so a Unix FIFO can block. Reuse its owner nonblocking primitive and add refusal coverage.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-4-provision-a-run-owned-environment-for-a-test-suite.md`
  summary: Suppress native diagnostic PEM blocks across independently emitted lines.
  evidence: R3-B8: concurrent PlatformDaprDiagnosticRedactor has no multiline state; short body lines below its opaque-value threshold survive after the PEM header is redacted. Define and test block suppression in the diagnostics change.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-4-provision-a-run-owned-environment-for-a-test-suite.md`
  summary: Bound aggregate checkpoint payload memory before reconstructing custody state.
  evidence: R3-B9: independent CheckpointArchive.DecodeAsync keeps every loaded byte array after bounded concurrent reads; 21,001 individually valid payloads can exceed process memory. Add an aggregate budget and exhaustion test in the custody owner.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-4-provision-a-run-owned-environment-for-a-test-suite.md`
  summary: Verify whether native-result response streams can exceed advertised object length.
  evidence: R3-B10: unverified, medium if true. Independent AwsAuthorityStore.FindIntentResultAsync copies its response stream before comparing ContentLength. Establish whether the supported AWS transport permits oversized stream reads, then use bounded streaming if needed.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-4-provision-a-run-owned-environment-for-a-test-suite.md`
  summary: Exercise a successful package-mode suite descriptor handoff after v2 declaration publication.
  evidence: R3-V1: filed regression gap: successful provisioning fixtures use source mapping and package selection coverage stops before a ready session. Current authoritative DefaultReleaseSnapshotHashes is empty, so a valid package closure is unavailable; add the positive native-child/descriptor test when released v2 declarations and pins exist, without bypassing authority checks.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-4-provision-a-run-owned-environment-for-a-test-suite.md`
  summary: Assert retained diagnostics through the actual retention composition registration.
  evidence: R3-V2: filed regression gap in concurrent diagnostics work: dedicated tests register capture manually; the real ComposeRun retention test publishes BeforeStart but only checks arguments/environment. Observe console and final state files through production registration.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-27-recovery-execution-boundary.md`
  summary: Preserve a failed composition workspace even when its diagnostic marker cannot be written.
  evidence: Pre-existing concurrent composition change, outside Story 4.27 driver implementation. PlatformCompositionWorkspace.MarkFailed suppresses IOException/UnauthorizedAccessException, while Dispose checks only FailureMarker existence; an existing failed workspace with a failed marker write is therefore deleted. Retain failure state independently and cover a write-refused existing workspace.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-27-recovery-execution-boundary.md`
  summary: Resolve the actual Platform checkout root when a nested Works AppHost also has normal AppHost markers.
  evidence: Pre-existing concurrent composition change, outside Story 4.27 driver implementation. WorksPreviewModel.LocatePlatformRoot chooses the nearest apphost.cs/aspire.config.json ancestor; nested markers can exclude only the nested host and permit certificate storage elsewhere inside the real checkout. Resolve checkout identity independently and cover the nested-marker case.

## Deferred from: code review of spec-2-4-provision-a-run-owned-environment-for-a-test-suite.md (2026-10-23, round 3)

- source_spec: `_bmad-output/implementation-artifacts/spec-2-4-provision-a-run-owned-environment-for-a-test-suite.md`
  summary: Same open item as the round-2 `RunEndpoints.Observe` entry above (healthy resource with an unallocated endpoint); re-raised by three layers, not a new defect.
  evidence: Unverified (maybe-false; medium if true). New corroboration toward false: the Story 2.3 round-3 live check showed Aspire 13.6.3 `DcpExecutor.PrepareServices` allocates every proxyless endpoint port before resources start. Settle by observing reference-composition progress snapshots under run/debug, or by confirming no 13.6 resource type can report `Healthy` with an unallocated endpoint.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-4-provision-a-run-owned-environment-for-a-test-suite.md`
  summary: Avoid the inherited synchronous readiness.json open during terminal recording, which can hang on a child-created FIFO after stopping cancellation.
  evidence: Completion-review B1/E1; the read is present verbatim at Builds baseline 66b9136a0b223925e51177a51eb48852e2f12787. Parent reproduced cancelled=True and completed=False after 1200 ms with a 200 ms token against the current compiled assembly; see evidence/2-4/review-round-3/readiness-fifo-parent-reproduction.json. Use runner-held readiness or the existing nonblocking bounded input mechanism.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-4-provision-a-run-owned-environment-for-a-test-suite.md`
  summary: Define and enforce per-entry descendant completion for shared-environment batches without stopping shared run resources.
  evidence: Completion-review B2; CompositionProcess.RunCoreAsync is byte-equivalent to Builds baseline 66b9136a0b223925e51177a51eb48852e2f12787 and returns on a successful parent exit plus drained streams without descendant quiescence. Detached inherited children can remain while the next command runs. Final run ownership cleanup exists; stronger entry supervision needs a separate lifecycle/ownership policy and a detached-child regression.

## Deferred from: code review of spec-2-3-make-local-composition-resources-run-scoped.md (2026-10-23, final-state)

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Make `AllocateOwnedSecondaryPorts` exhaustion reachable or remove its dead throw.
  evidence: Story 2.4 resource-claim code (`1622516`). At `src/Hexalith.Platform.Composition/PlatformAppModel.cs:417` the `when (attempt < 63)` filter lets the 64th `CompositionResourceClaimConflictException` escape, so the "No independent secondary runner port allocation is available." throw is unreachable; no test exhausts the claim retries.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Remove retention diagnostics written into the real Memories checkout by the retention composition test.
  evidence: `RetentionSelectedComposeRunAdoptsGraphCallbacksReadinessAndIsolatedControlPortsAsync` publishes BeforeStart through the Story 4.27 `PlatformDaprDiagnostics` registration, which writes `references/Hexalith.Memories/artifacts/retention/<runId>/diagnostics/native-sidecars`; the test's `finally` deletes only its workspace. Nine gitignored run folders were present on 2026-10-23. Delete that run's diagnostics directory in the test cleanup.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Prove the activated retention gate refuses access after `ExecutionDeadlineUtc` while the extended certificate is still valid.
  evidence: Unverified (maybe-false; medium if true). Story 2.4 `PlatformRetentionProfile.PreStartValidityCeiling` extends `AccessTelemetryLifecycle__CapabilityEvidence__ValidUntilUtc` and the server certificate past `ExecutionDeadlineUtc`; its comment relies on the gate deadline, but no Platform test exercises gate refusal with a still-valid certificate. Settle with a gate test past the execution deadline.

## Deferred from: code review of spec-2-4-provision-a-run-owned-environment-for-a-test-suite.md (2026-10-23, round 4)

- source_spec: `_bmad-output/implementation-artifacts/spec-2-4-provision-a-run-owned-environment-for-a-test-suite.md`
  summary: Same open item as the round-2 and round-3 `RunEndpoints.Observe` entries (healthy resource with an unallocated endpoint). Re-raised again; not a new defect.
  evidence: Unverified (maybe-false; medium if true). Blind Hunter, Edge Case Hunter and Verification Gap raised it again, naming progress (`RunReadiness.cs:314,343,352`) and failure-report (`:412`) call paths. The settling evidence is unchanged: observe reference-composition progress snapshots under run/debug, or confirm that no Aspire 13.6 resource type can report `Healthy` with an unallocated endpoint.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-4-provision-a-run-owned-environment-for-a-test-suite.md`
  summary: Add a regression for a healthy resource with an unallocated endpoint once the `RunEndpoints.Observe` behaviour is settled.
  evidence: Round-4 Verification Gap finding. `AppHostRunnerValidationTests.HealthyReadinessWritesMetadataAndFinalProgressAsync` covers only allocated endpoints, and no test reaches the `RunEndpoints.Observe` throw. If `Observe` is changed to return only allocated endpoints, the suite still passes. The descriptor would then silently omit that endpoint instead of refusing handoff. Assert no-launch at whichever layer the settled behaviour places the refusal: readiness, or `TestEnvironmentDescriptorWriter`.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-4-provision-a-run-owned-environment-for-a-test-suite.md`
  summary: Same open item as the round-2 R3-V2 entry (retention `PlatformDaprDiagnostics` registration never asserted through `ComposeRun`). Re-raised; not a new defect.
  evidence: Round-4 Verification Gap finding. `PlatformAppModel.cs:364-371` (Story 4.27, `858d02c`) registers the directory, server application IDs and secret values of 8 or more characters. Deleting the block or emptying the secret list passes every composition test. Assert the registered instance after a retention `ComposeRun`.

## Deferred from: Story 2.3 current-tree workflow review (2026-10-23)

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Custody unpin authorization must bind the release revision to the attested current control revision.
  evidence: R5-B2, high: StatementVerifier.Authorize checks CurrentGuardRevision only against DispatchRevision; SemanticProvider unpin accepts ReleaseGuardRevision above the registered pin without an upper bound against that signed current state. An internally inconsistent enrolled signed claim can remove a pin before its claimed release revision exists. Independent Custody provider code, unchanged by local run isolation; add CurrentGuardRevision >= ReleaseGuardRevision and a signed refusal case in its owner work.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Determine whether native-result streams can exceed their advertised length and enforce a streaming ceiling if needed.
  evidence: R5-B3, unverified medium: AwsAuthorityStore.FindIntentResultAsync uses CopyToAsync after a ContentLength check, unlike bounded ReadObjectAsync. Settle with an oversized injected native response plus the actual native SDK stream contract; no production overrun was demonstrated. Independent Custody provider work.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Resolve hosted Dapr component version selection for accepted providers requiring versions beyond v1.
  evidence: R5-B4, unverified medium: the Helm validator permits capability-prefixed provider types but templates/dapr.yaml always emits version v1. Establish an accepted hosted provider/version fixture before deciding whether to expose a version field or constrain supported bindings. Independent Story 4.5 export, not local composition.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Assert composed retention diagnostics receive the intended retained directory and redaction inputs.
  evidence: R5-B9, medium: production PlatformAppModel.ComposeRun derives the directory and secrets, but existing composition tests do not inspect them; standalone helper tests cannot catch incorrect construction. Independent Story 4.27 diagnostics configuration.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Resolve the final secondary-port claim conflict diagnostic and unreachable exhaustion throw.
  evidence: R5-B10, low: attempt 63 escapes the attempt < 63 catch filter in PlatformAppModel.AllocateOwnedSecondaryPorts. The following generic throw cannot execute, although the concrete blocking-owner exception remains useful. Independent Story 2.4 claim lifecycle policy.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Verify production ComposeRun diagnostics emit retained native console and final capture-stopped receipts.
  evidence: R5-V1, medium, pre-verified: PlatformDaprDiagnosticsTests installs its own capture/eventing registrations, and PlatformRetentionProfileTests never asserts production capture output. Removing PlatformAppModel's production eventing registration leaves those tests passing. Independent Story 4.27 diagnostics wiring, distinct from constructor-input assertions.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Redact short PEM body lines across successive native console messages.
  evidence: R5-V3, medium: PlatformDaprDiagnosticResource.RecordLogs redacts each line separately; PlatformDaprDiagnosticRedactor is stateless and its opaque matcher starts at 40 characters. A short base64 body line after a redacted header survives. Existing coverage supplies the whole PEM block in one message. Independent Story 4.27 diagnostics redaction.

## Deferred from: code review of spec-2-3-make-local-composition-resources-run-scoped.md (2026-10-24)

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Move retention native-sidecar diagnostics out of the Memories checkout.
  evidence: Medium, pre-existing Story 4.27 code (`858d02c`). `PlatformAppModel.ComposeRun` registers `PlatformDaprDiagnostics` at `Path.GetDirectoryName(Path.GetDirectoryName(RetentionArtifactsPath(...)))/diagnostics/native-sidecars`, i.e. `references/Hexalith.Memories/artifacts/retention/<runId>/diagnostics/native-sidecars`, for every retention run (not only the test). Story 2.3 requires mutable runtime files outside checkouts and documents only the retention build-output carve-out. Distinct from the final-state test-cleanup entry.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-make-local-composition-resources-run-scoped.md`
  summary: Confirm the file-entry smoke's path-targeted `aspire wait`/`aspire stop` cannot select a same-path developer AppHost.
  evidence: Unverified (maybe-false; medium if true). `PlatformAppModelTests` starts `aspire start --apphost apphost.cs --isolated` but waits/stops with `--apphost <path>` only. Recorded smokes had no same-path Platform AppHost running. Settle by running the stop command against one non-isolated developer instance plus one isolated instance of the same `apphost.cs`; if ambiguous, target the started instance by its reported PID/identity.
