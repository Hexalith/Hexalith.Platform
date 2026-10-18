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

<<<<<<< HEAD
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
