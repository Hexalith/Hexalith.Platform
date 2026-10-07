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
