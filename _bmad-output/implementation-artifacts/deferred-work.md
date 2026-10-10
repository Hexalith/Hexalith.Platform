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
