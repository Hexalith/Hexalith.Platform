---
title: '4.7 Provide the staging attempt, lock and promotion-stop store'
type: 'feature'
created: '2026-11-23'
status: 'done'
baseline_commit: '689efa824ec621509aded4ddaf35ed8bcfdad645'
route: 'dispatch'
review_loop_iteration: 0
story_key: '4-7-provide-the-staging-attempt-lock-and-promotion-stop-store'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/references/Hexalith.Builds/ReleaseRecords.md'
  - '{project-root}/docs/contracts/staging-evidence-policy-v2.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Staging has no durable off-cluster place to serialize attempts. Story 4.6's signed lock, attempt and stop records cannot commit atomically, and the v2 admission-versus-supersession race has no shared conditional point.

**Approach:** Add a Platform store library over object-storage conditional writes. One compare-and-set head object per environment holds the lock, monotonic epoch, current record references, latest-working pointer, promotion stop and v2 decision state; signed envelopes are create-only objects. Each transition reads and verifies current state, calls the Builds validator, then commits with one conditional head write. Deploy and live-prove the staging instance.

## Boundaries & Constraints

**Always:**
- Store access grants no authority. Verify every record with the caller's independently built `RecordTrustPolicy`, and read and validate current state inside each commit attempt. A lost compare-and-set re-reads and re-validates; it never blind-retries.
- One head per environment. A transition mutates only its verified record's environment; no operation takes two locks.
- Acquisition and takeover commit `epoch + 1`. A takeover commits successor, predecessor terminalization and epoch in one head write. Terminal attempts are immutable. Exact replay of a committed record or action returns the committed result without writing.
- v2 decisions live in the staging head. Supersession checks the staging epoch in the same write. A production reservation or closure reads the production head at commit and binds its attempt and epoch. Reserve and supersede compete on one head, so one commits.
- After an ambiguous write, read the head: committed only if it names this transition, otherwise re-validate. Uncertain outcomes keep reservations pending.
- Heads carry a strictly increasing revision and their predecessor's digest. Every PUT is conditional (`If-Match` head, `If-None-Match: *` records). Writers cannot delete objects or versions.
- Hosted outside the cluster and every executor host; the staging writer is scoped to the staging prefix. Never print or commit key values.

**Decisions (Administrator, 2026-11-23):** Host on a new dedicated Scaleway project and bucket in `fr-par`, created with versioning and default 30-day `COMPLIANCE` object lock. Deploy and live-prove the staging instance in this spec with an organization-owner key read from a `0600` file (`~/.config/hexalith-recovery/admin.env` if still valid, otherwise a path the Administrator supplies). Keep this spec whole despite its size.

**Never:** Implement executors, fencing, job credentials, monitor checks, the production instance (6.9), admission-record chains, v2 activation, a CLI or Builds record changes. Builds' takeover-closure and issuer-matrix deferrals stay open and fail closed.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Error handling |
| --- | --- | --- | --- |
| Concurrent acquisition | Two signed in-progress attempts at `epoch + 1` | Exactly one commits | Loser refuses `lock-held`; no partial state |
| Stale mutation | Revision signed below the head epoch | Refused; head unchanged | `stale-epoch` |
| Takeover | Nonterminal holder, linked successor, successor-signed final revision | Both records and `epoch + 1` commit together | Missing or mismatched link refuses |
| Replay or lost acknowledgment | Same signed record or action again; unknown write outcome | One effect; committed result returned | Unproven commit re-validates |
| Staging stop | Staging set; Administrator clear naming the pointer's working attempt; a staging key naming production | Only the staging stop changes | Cross-environment or non-pointer attempt refuses |
| v2 race | Production reserve and staging supersede for one pending decision | First valid commit wins | Loser, stale epoch, wrong writer or conflicting action refuses before mutation |

</frozen-after-approval>

## Code Map

Builds paths are under `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/`.

- `ReleaseRecordCodec.cs` -- `Verify<T>` (:158/:167) is the only decode path; `VerifiedRecord<T>.Reference` is the head's reference. `SameContent` is internal, so compare exact bytes and digests.
- `RecordTransitionValidator.cs` -- call `ValidateAttempt` (:22, with `takeover`), `ValidateLatestWorking` (:214), `ValidatePromotionStop` (:236), `ValidateAdministrator` (:251; clearance needs the pointer's working attempt, :275-280) and `ValidatePromotionDecision` (:102); do not modify.
- `RecordTrustPolicy.cs` -- caller-supplied enrollment; roles include executor, promotion-evaluator, monitor and administrator.
- `references/Hexalith.Builds/ReleaseRecords.md:62-105` -- store obligations and the v2 handoff.
- `src/Hexalith.Platform.Protection/IErasureRootStore.cs` -- reuse its contract shape: conflict returns false, ambiguity throws, no retry.
- `eng/kubernetes-upgrade/native-node-recovery/publication.py:130-135` -- proven Scaleway `IfNoneMatch='*'` write.
- `src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj:50-61`, `Directory.Build.targets:42-49` -- Tooling reference pattern and CI checks to extend; `Directory.Packages.props` has no S3 client yet.
- `_bmad-output/implementation-artifacts/evidence/epic-4/recovery-destination-setup.md` -- project, IAM, bucket-policy and denial-probe pattern to mirror.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Platform.AttemptStore/` -- new library in the slnx: head model, object-store port (read with ETag, create-only, replace-if-match), S3 backend, and acquire, revise, takeover, latest-working move, stop-set, Administrator-clear and v2 decision transitions.
- [x] `Directory.Packages.props`, `Directory.Build.targets` -- pin the S3 client; require the published Tooling assembly for the new project in CI.
- [x] `tests/Hexalith.Platform.AttemptStore.Tests/` -- in-memory backend covering every matrix row with real ephemeral keys, a parallel-writer race on one head, and a credential-gated live suite on the hosted verification prefix.
- [x] Hosting -- provision the project, bucket, staging-writer and reader applications and bucket policy; probe denials; and keep a short sanitized summary in `_bmad-output/implementation-artifacts/evidence/story-4-7/`.
- [x] `docs/contracts/attempt-store-v1.md` -- layout, transitions, failure codes, IAM model and consumer obligations.

**Acceptance Criteria:**
- Given the hosted store, when probed live, then it is outside the cluster and executor hosts, two concurrent acquisitions commit exactly one, and unconditional writes, deletes and the staging writer's access to another environment's prefix are denied.

### Review Findings

Code review 2026-11-23 of `689efa82..fbf42799` (Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor). Result: 2 decision-needed (resolved by the Administrator 2026-11-23), 14 patch left as action items, 4 defer, 15 rejected.

- [x] [Review][Decision] A lost head silently resets the epoch, stops and decisions. The adapter reads `404 NoSuchKey` as confirmed absence. A delete marker added by the owner or created by a lifecycle expiry returns the same response, and `CommitAsync` then bootstraps an empty revision-zero head with `If-None-Match: *`. The result is epoch 0, no active stop and no decisions, so fencing epochs can be reissued. Object lock does not prevent delete markers. Options: require an explicit one-time head initialization and refuse a missing head in every transition; or detect a delete marker in the adapter. Either option comes with a contract rule that no lifecycle rule or deletion may touch `environments/*/head.json`. [src/Hexalith.Platform.AttemptStore/AttemptStore.cs:123-140, src/Hexalith.Platform.AttemptStore/S3AttemptObjectStore.cs:70] — resolved 2026-11-23: explicit initialization (patch below).
- [x] [Review][Patch] Add explicit head initialization. `InitializeAsync(environment)` creates an empty head create-only, once at provisioning. `CommitAsync` stops bootstrapping and refuses `head-missing` when no head exists. Initialize the canonical staging head with the writer key, and have the live and in-memory tests initialize their fixture prefixes. The contract forbids any lifecycle rule or deletion on `environments/*/head.json`. [src/Hexalith.Platform.AttemptStore/AttemptStore.cs:123-140, docs/contracts/attempt-store-v1.md]
- [x] [Review][Decision] No identity or topology can perform the production decision actions. `ApplyDecisionAsync` reads `environments/production/head.json` and the production records, then writes the staging head, all through one `IAttemptObjectStore`. The provisioned staging writer is denied the production prefix and the reader cannot write. So `reserve-admission`, `complete` and `release-reservation` cannot run against the hosted bucket. The contract names neither the principal nor where the production head lives. Options: document a single-bucket topology now and defer the production-decision principal to 6.9; give the store separate staging and production object stores; or provision a production-decision principal now. [src/Hexalith.Platform.AttemptStore/AttemptStore.Decisions.cs:55, docs/contracts/attempt-store-v1.md:29] — resolved 2026-11-23: document the topology now and defer provisioning to 6.9 (patch and defer below).
- [x] [Review][Patch] Document that both heads live in one bucket under `environments/{env}/`. Decision actions need a principal that reads the production prefix and conditionally replaces the staging head, and the staging writer has neither right. [docs/contracts/attempt-store-v1.md:29]
- [x] [Review][Defer] Provision the production-decision principal and prove its denials [src/Hexalith.Platform.AttemptStore/AttemptStore.Decisions.cs:55] — deferred to 6.9: the production instance is outside 4.7's scope, and v2 cannot activate before it.
- [x] [Review][Patch] Classify every S3 PUT error other than a known refusal as uncertain. Only status 0, 408 and 5xx from `AmazonServiceException` are uncertain today, along with `HttpRequestException`, `OperationCanceledException`, `TimeoutException` and `IOException`. A non-service `AmazonClientException`, or a 2xx response that fails to unmarshal, propagates as a definite failure even though the PUT may have committed. [src/Hexalith.Platform.AttemptStore/S3AttemptObjectStore.cs:116-130]
- [x] [Review][Patch] Make the production-prefix read-denial probe conclusive. The writer GETs a production key that never exists. With list rights only on the staging prefix, S3 returns 403 for a missing key whether or not reads are allowed. Probe an owner-created production canary object, and record the result in the evidence. The PUT denial already holds. [tests/Hexalith.Platform.AttemptStore.Tests/HostedAttemptStoreTests.cs:56, _bmad-output/implementation-artifacts/evidence/story-4-7/scaleway-staging-store.md]
- [x] [Review][Patch] Add deterministic adapter tests for the confirmed conflicts. A 412 on a stale `If-Match` and on `If-None-Match: *` over an existing key, and a 409 `ConditionalRequestConflict`, must each return `false`. Only the credential-gated hosted test checks this now. [tests/Hexalith.Platform.AttemptStore.Tests/S3AttemptObjectStoreTests.cs:14]
- [x] [Review][Patch] Exercise a second stop clearance. Test a new clearance ID that disposes only of the causes raised since the last clearance, a predecessor equal to `LastClear`, and a refused foreign predecessor. The existing test stops at `record-id-conflict`. [tests/Hexalith.Platform.AttemptStore.Tests/AttemptStoreTests.cs:312]
- [x] [Review][Patch] Cover a decision introduced through a staging v2 takeover. A fresh binding should read as pending. A fresh binding while the release still has a pending decision should be refused with `decision-binding-conflict`. [tests/Hexalith.Platform.AttemptStore.Tests/PromotionDecisionTests.cs:206]
- [x] [Review][Patch] Assert the `contention` bound. Every replace should lose to an unrelated commit, and the call should report `contention` after 16 conditional attempts. [tests/Hexalith.Platform.AttemptStore.Tests/AttemptStoreTests.cs:221]
- [x] [Review][Patch] Apply the active-replay epoch check on the lost-acknowledgment path. It returns the receipt after an epoch advance, while an explicit replay of the same submission refuses `stale-epoch`. [src/Hexalith.Platform.AttemptStore/AttemptStore.cs:189]
- [x] [Review][Patch] Re-route the 4.6 deferral that names Story 4.7 as the owner of takeover closure. Per the Never rule, 4.7 kept it open and fail-closed. [_bmad-output/implementation-artifacts/deferred-work.md:2306]
- [x] [Review][Patch] Fix the contract's statement that the validator "leaves reservations pending". `AttemptStoreDecision.State` uses `pending` to mean unreserved, so this should say the reservation stays outstanding. [docs/contracts/attempt-store-v1.md:29]
- [x] [Review][Patch] Complete the failure-code table. Add `record-too-large`, `invalid-record` and the default `invalid-transition`, and state that `AttemptStoreWriteUncertainException` and provider exceptions such as `AmazonS3Exception` and `IOException` propagate. [docs/contracts/attempt-store-v1.md:35-45]
- [x] [Review][Patch] Restore CRLF line endings on the three added `Directory.Packages.props` lines. [Directory.Packages.props:11-13]
- [x] [Review][Patch] Add the missing `reason:` field to the three new Story 4.7 deferral entries. [_bmad-output/implementation-artifacts/deferred-work.md:2312-2322]
- [x] [Review][Defer] The production reservation or closure is checked only by reading the production ETag before the staging write, not atomically at commit [src/Hexalith.Platform.AttemptStore/AttemptStore.cs:161-168] — deferred: already in the ledger ("Prove production lifecycle serialization through the staging decision CAS"). A race leaves the reservation bound to the superseded attempt, which fails closed.
- [x] [Review][Defer] Tuple CI cannot compile the store on the pinned Builds 6.6.2 [eng/tuple-ci-sdk42-current-suites.json:342, eng/run_tuple_ci.py:21] — deferred: already in the ledger as the 6.7.0 adoption. Adding the project to the slnx also makes the whole tuple build fail on 6.6.2, and the first hosted run will be the first execution of the runner's non-Composition test path.
- [x] [Review][Defer] No expiry or renewal owner is recorded for the staging writer and reader application keys [_bmad-output/implementation-artifacts/evidence/story-4-7/scaleway-staging-store.md] — deferred: unverified (medium if true). Settle it by reading each key's `expires_at` from the Scaleway IAM API and naming a renewal owner if one is set.

#### Rejected

- Spec `status: done` against sprint `review` (four layers): the fix edits the spec under review. The closing status sync of this review reconciles it.
- Store not probed as outside the cluster: false. Scaleway Object Storage is a managed endpoint off the cluster and executor hosts, and the bucket policy admits only the named applications and the owner.
- Permanent wedge after a production takeover, and no release for an Administrator reservation: false as a store defect. The fail-closed outcome is required by the Never rule, and the Builds v2 contract defines no Administrator release action.
- Enforce the successor binding in `TakeoverAsync`: low. A mismatched successor already cannot close the decision (fails closed), and the fix adds a guard.
- Cancelled token turns the strong read into uncertainty: false. A cancelled call is correctly reported as unresolved.
- An uncertain archive create before the head write reports uncertainty: low. A retry resolves it, and the fix adds a branch.
- No CAS backoff: low. Transitions are rare, and 16 rereads bound the work.
- 32 MiB head limit without warning: low. Already rejected (Blind 8). It takes tens of thousands of transitions.
- Version-delete probe hit a COMPLIANCE-locked version: false. The writer's IAM has no ObjectsDelete set, and the delete-marker 403 proves the policy denial.
- Evidence lacks lifecycle, open-gaps and acceptance-state sections: low, process-only.
- Head schema undocumented, and spec Design Notes example out of date: the example is in the spec under review. No consumer parses the head outside the library.
- `AWSSDK.S3` pinned outside the Builds catalog, and the CI-mode Tooling dependency list: low and speculative. The CI-mode build passed against published 6.7.0.
- No Python test run recorded: false. The five changed `eng/test_*` modules pass (81 tests, 2026-11-23).
- Record version passed separately from the bytes: false. Builds requires explicit version selection, and a mismatch fails verification closed.
- A 409 on a concurrent archive create yields `record-content-conflict` instead of `lock-held`: low. Exactly one acquisition still commits, it needs provider 409 timing, and the fix adds a branch.

### Review Findings — delta review of `917df689` (2026-11-24)

Scope: `c3abc0d0..917df689`, the fix commit for the first review (20 files, +645/−132). Layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor; none failed. The acceptance auditor confirmed that every first-review patch item and both decisions (D1 explicit head initialization, D2 single-bucket topology with 6.9 deferral) are implemented, with nothing missing or contradicted. Result: 0 decision-needed, 5 patch, 1 defer, 24 rejected.

- [x] [Review][Patch] Test the known-refusal side of the S3 PUT classification (low). `PutAsync` lets an `AmazonS3Exception` with status 400, 401, 403, 404, 405, 411, 413 or 415 propagate as a definite failure and wraps everything else as uncertain. The existing theory covers only the uncertain side. Dropping `Forbidden` from `IsKnownRefusal`, or deleting the method, leaves every test green. Add 403 `AccessDenied` and 400 `InvalidRequest` cases where `S3Handler` refuses the head PUT. Each case asserts that `AcquireAsync` throws `AmazonS3Exception`, not `AttemptStoreWriteUncertainException`, and that `HeadVersions` stays at 1. [tests/Hexalith.Platform.AttemptStore.Tests/S3AttemptObjectStoreTests.cs:56, src/Hexalith.Platform.AttemptStore/S3AttemptObjectStore.cs:121-133] (verification-gap+acceptance-auditor)
- [x] [Review][Patch] Re-route the 4.6 issuer-matrix deferral that still names Story 4.7 (low). The entry reads "Deferred to Story 4.7 or the lifecycle work". The Never rule kept the issuer matrix open and fail-closed in 4.7, so the entry is now orphaned as 4.7 closes. Name the lifecycle work as owner and state that 4.7 kept it fail-closed, as was done for the takeover-closure entry. [_bmad-output/implementation-artifacts/deferred-work.md:1115] (acceptance-auditor)
- [x] [Review][Patch] Name the escape set in the contract's record-key rule (low). "URI-escape the ID" does not identify the encoder. The code uses `Uri.EscapeDataString`, which keeps only RFC 3986 unreserved characters and uses uppercase hex. JavaScript's `encodeURIComponent`, for example, leaves `!'()*` unescaped and would derive different keys. State that every UTF-8 byte outside the RFC 3986 unreserved set is percent-encoded with uppercase hex. [docs/contracts/attempt-store-v1.md:8] (blind-hunter)
- [x] [Review][Patch] Update the stale runner line reference in the 6.6.2 tuple deferral (low). This diff rewrote the entry's evidence but kept `eng/run_tuple_ci.py:414-416`, which now falls inside `measure_collector`. The non-Composition test path is at `:457-461`. Removing the msbuild `TargetPath` fallback is intended: an unconfigured project now fails closed. [_bmad-output/implementation-artifacts/deferred-work.md:2336] (blind-hunter)
- [x] [Review][Patch] Bring the evidence's bucket-policy record up to date with the policy change (low). The policy row replaced the original hash `7a2f5085…` with the current `1292c94d…`. The signed-acquisition paragraph still says that run "passed against the original bucket policy", which no longer has a recorded identity. The statement table also omits `VerificationCanaryRead`; only the Review verification prose mentions it. Keep both hashes, and add the canary statement to the table. [_bmad-output/implementation-artifacts/evidence/story-4-7/scaleway-staging-store.md:16-25] (blind-hunter)
- [x] [Review][Defer] The tuple runner's AttemptStore wiring has no test that reaches it [eng/run_tuple_ci.py:431-461] — deferred: the Python tests drive `run_tuple_ci.main()` only with `--audit-only`, which returns at `:360` before any collector code. That limitation predates this change. Untested branches: the AttemptStore collector, its output snapshot, its Tooling-copy identity check, and the error raised for unconfigured projects. The first non-audit execution is the hosted tuple run, which the existing Builds 6.7.0 adoption deferral blocks. When that run happens, check `buildsArtifactIdentities.attemptStore` and `attemptStoreTestOutputs.unchanged: true` in its `tuple-ci-result.json`. (verification-gap+blind-hunter)

#### Rejected (delta review)

- Status: the spec says `done` while sprint-status says `review`, and the summary still reads "14 patch left as action items" (blind-hunter, verification-gap). Rejected because the fix edits the spec under review; this review's closing status sync reconciles it.
- Low. The follow-up triage table records no provenance and has no patch checklist (blind-hunter). Rejected because the fix edits the spec under review.
- Low. The Verification section is past-tense prose with no commands (blind-hunter). Rejected because the fix edits the spec under review.
- Low. `InitializeAsync` can recreate an epoch-0 head after a delete marker (blind-hunter, edge-case-hunter, acceptance-auditor). The follow-up review triaged this (Blind 6) with a documentation fix, and that fix is present. The writer cannot create delete markers, and the contract forbids lifecycle rules and deletion on heads. A guard would add a version-listing permission and a branch.
- Low. A retried uncertain `InitializeAsync` returns `false`, the same result as "already provisioned" (blind-hunter). Initialization runs once, and the provisioner confirms the head with `ReadAsync`, as the evidence does.
- False. The failure section narrowed "Operations" to "Transitions" (blind-hunter). `InitializeAsync` returns `bool`, not `AttemptStoreCommit`, and contract line 9 documents its result, so the narrower wording is the accurate one.
- Low. `head-missing` is tested only for Acquire and SetStop (blind-hunter). Every transition goes through `CommitAsync`, which calls `ReadRequiredHeadAsync` (`AttemptStore.cs:117`). The production check at `AttemptStore.Decisions.cs:56` is a single `Require`.
- False. No one is named to create the production head (blind-hunter). Contract line 9 requires `InitializeAsync` once per environment, and the Never rule assigns the production instance to Story 6.9.
- False. After a lost acknowledgment, `stale-epoch` hides a committed write (blind-hunter). The code tells the caller it does not hold the lock, which is correct whether or not its record committed. `ReadAttemptAsync` answers the commit question. The behavior matches explicit replay and is the requested first-review patch.
- False. A lost-acknowledgment replay of `supersede` after a takeover returns a success receipt (edge-case-hunter). Explicit replay and lost-acknowledgment replay both reach `ReconcileAsync` with `activeReplayEpoch` null for decisions (`AttemptStore.cs:122-124`, `:174-176`), so the two paths agree. The supersession did commit. Contract line 11 states that a receipt records a past effect and that live authority comes from the head. Every later attempt mutation is still epoch-checked.
- Low. The PUT catch-all wraps failures that occur before the request is sent (credentials, a disposed client, MD5) as uncertain (blind-hunter, edge-case-hunter). This classification is the one the Administrator chose in the first review. It errs toward caution, and the wrapped inner exception keeps the cause. Excluding these cases would add type branches.
- Low. A 404 on an `If-Match` replace of a deleted head surfaces a raw `AmazonS3Exception` instead of `head-missing` (blind-hunter, edge-case-hunter). This needs a head deletion during a transition, which the writer cannot perform and the contract forbids. The failure is loud and leaves no partial state.
- Low. An uncertain head write followed by a reread that finds the head absent reports uncertainty instead of `head-missing` (edge-case-hunter). Same trigger as the previous item, and uncertainty is the safe report.
- Low. Two concurrent `InitializeAsync` calls: one gets a 409, then the competing create fails (edge-case-hunter). This needs concurrent one-time provisioning. Transitions then refuse `head-missing`, and a second `InitializeAsync` succeeds.
- Low. The oversized-key fallback has no exact 1,024-byte or 640-character boundary tests (blind-hunter). The code compares the final key with `<= 1024` UTF-8 bytes and the initial escape with `> 640` (`AttemptStoreHead.cs:97`, `:106`). Both boundaries are correct by inspection, and a literal `~` occurs only in a hashed segment.
- Low. The second-clearance test omits `undisposed-stop-cause`, an absent predecessor and the 128-cause bound (blind-hunter). The Builds validator owns cause disposition. The first review already triaged the optional predecessor as false (Blind 4), and the first-review patch's three requested cases are present.
- False. The follow-up gaps (the 129th cause, pointer coordination, supersession confirmation) have no ledger owner (blind-hunter). The spec assigns caller obligations to the contract's consumer obligations, and the contract states all three. The 128-cause bound is a pre-existing Builds schema limit.
- Low. New `reason:` fields name no owner story (blind-hunter). This is process-only: the patch asked for the reasons, which are present, and choosing owners is not a mechanical fix.
- False. `runtime_context["targetFramework"]` is read without a `None` guard (blind-hunter). `runtime_context` is always set outside audit-only mode (`run_tuple_ci.py:293-296`), and the collectors run only after the audit-only return.

The five patches were applied on 2026-11-24 in the working tree; they are not committed. The new `KnownRefusalOfAHeadPutPropagatesWithoutUncertainty` theory adds two adapter cases. The tuple profile's AttemptStore `requiredCount` is now 41, and the `current42` total is now 421, in `eng/tuple-ci-sdk42-current-suites.json`, `eng/run_tuple_ci.py`, `eng/test_tuple_collectors.py` and `eng/test_tuple_sdk_suites.py`. The Release build of `tests/Hexalith.Platform.AttemptStore.Tests` finished with zero warnings and errors. Under `dotnet exec`, 41 cases passed and the hosted case skipped, for 42 in total. A mutation run in a disposable worktree disabled `IsKnownRefusal`. Exactly the two new cases failed, and the other nine `S3AttemptObjectStoreTests` cases passed. All 89 Python checks in the six tuple and Builds modules passed, and `git diff --check` is clean. The CI-mode build against published Builds 6.7.0 and the credentialed hosted suite were not rerun. The change adds tests and documentation only and leaves adapter behavior unchanged.

## Implementation Notes

The reviewed Release build completed with zero warnings and errors. The credentialed suite passed all 24 cases, including the signed acquisition race and entitlement probes against the Scaleway verification prefix. The test run covered concurrent acquisition, stale epoch and linked takeover, replay and unproven writes, staging-only stop and clear, and the production reservation versus staging supersession race. [Hosted evidence](evidence/story-4-7/scaleway-staging-store.md)

The CI-mode assembly-reference build and all 23 deterministic cases passed against both a local publish of the pinned Builds Tooling source at `4adecfe2af4758aa66039a1221de20774ca56e87` and the published 6.7.0 assembly. The 6.7.0 package passed NuGet signature and SHA-512 verification, its repository metadata matched release revision `763fc0e937413cb44e533391e573c43640263b40`, and the Tooling assembly SHA-256 was `79ff793b6ec2796bc0098ea39457d1a01583b40f7b1eb11815cb127f6bc8f775`. The tuple profile selects those 23 cases. Platform's full tuple gate still requires adoption of the 6.7.0 release and its catalog. [Builds 6.7.0](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v6.7.0)

The review follow-up initialized the canonical staging head at revision and epoch zero. The hosted reader verified one retained version and no delete markers. The reader fetched the owner-created production canary, and the staging writer received `403 AccessDenied` for the same object. The policy grants the reader access to that single canary. All 33 cases passed in the credentialed Release suite. The CI-mode Release build against verified Builds 6.7.0 passed all 32 deterministic cases selected by the tuple profile. All 79 Python tuple and Builds checks passed. [Hosted verification](evidence/story-4-7/scaleway-staging-store.md#review-verification)

The final review fixed S3 key addressing for oversized escaped record IDs while keeping existing valid addresses. Tuple verification now measures the AttemptStore assembly and checks its Builds Tooling copy against the archive identity. Final verification passed 40 credentialed Release cases, 39 CI-mode cases against verified Builds 6.7.0, and 89 Python checks. [Final verification](evidence/story-4-7/scaleway-staging-store.md#final-verification)

| Acceptance matrix row | Passing test |
| --- | --- |
| Concurrent acquisition | `ParallelAcquisitionCommitsExactlyOneAndLinksHeadVersions`, `HostedWriterSerializesAcquisitionAndProviderEnforcesScope` |
| Stale mutation and takeover | `TakeoverCommitsBothRecordsAndRejectsMissingLinkAndStaleMutation` |
| Replay and lost acknowledgement | `LostAcknowledgementReconcilesFromReceiptEvenAfterASecondCommit`, `LostAcknowledgementOfActiveAttemptRefusesAfterEpochAdvances` |
| Staging stop | `ClearsOnlyStagingWithExactPointerAndAllOutstandingCauses` |
| v2 decision race | `ProductionReservationAndStagingSupersessionRaceOnOneHead` |

## Review Triage Log

| Finding | Verdict | Evidence and disposition |
| --- | --- | --- |
| Blind 1. Production changes after the final ETag read | medium | A production mutation in the last network interval can invalidate the snapshot. The contract requires the production lifecycle caller to serialize production work through the decision CAS. Defer production integration proof to the production lifecycle work. |
| Blind 2. A raw head writer can omit signed authority | medium | Conditional storage grants can change unsigned indexes. Library clients verify signed references and assume operational writes go through the store. The raw-writer integrity model requires a separate design for authenticated history or principal isolation. Defer that design. |
| Blind 3. A terminal decision blocks fresh intake | medium | The release lookup includes terminal entries. Verify the actual terminal action before allowing a later decision ID and keep exactly one pending decision per release. Patch. |
| Blind 4. Clearance without a predecessor | false | Builds requires lineage for admission and management actions. Stop clearance binds its exact current stop and revision and checks any predecessor it names. Clearing one stop supplies no authority for a newer stop. |
| Blind 5. Replay omits the retained release envelope | medium | The acquire and takeover receipts can return success after submission of a re-signed release. Verify the archived release bytes during reconciliation. Patch. |
| Blind 6. An orphaned envelope can refuse a re-signed version | low | The provider permits one exact envelope per signing-content digest. A caller retaining its exact input bytes can retry, and another content digest has an independent archive address. Reject archive migration for this case and document the envelope custody requirement. |
| Blind 7. Archive read failure loses write uncertainty | medium | The ambiguity handler wraps the head read only. Preserve uncertainty when the archive needed to prove the receipt is unavailable. Patch. |
| Blind 8. Long-lived head reaches its size limit | low | Appending enough receipts can reach 32 MiB. Reaching the bound requires sustained use over many transitions, and a rollover protocol would add a new persistence contract. Reject for this build. |
| Blind 9. Pinned published Tooling predates v2 | medium | The tuple publication gate already refuses the recorded gitlink against 6.6.2. The reviewed store passes against newly published 6.7.0. Defer adoption of its changed catalog and tuple pin. |
| Blind 10. Tuple CI omits the in-memory suite | medium | The solution compiles the project but the tuple runner executes only explicit profile selections. Add the deterministic classes and verify the measured inventory. Patch. |
| Blind 11. Live verification uses fixture paths | low | The provider puts the canonical and fixture paths in the same conditional allow statements. Record both resource lists from the policy read-back. Patch. |
| Edge 1. Production changes at the staging write | medium | The final production read and staging CAS are distinct provider operations. Defer the same production lifecycle serialization proof as Blind 1. |
| Edge 2. Delayed PUT can commit after a refusal | medium | A reread with the original ETag cannot rule out the delayed write. Preserve uncertainty if there is no receipt and the prior precondition still holds. Patch. |
| Edge 3. The final retry can miss its own receipt | low | The last conflicting call refreshes the head and exits before its receipt lookup. Reconcile the refreshed head before reporting contention. Patch. |
| Edge 4. Retained release is omitted from replay | medium | The receipt check compares primary and companion archives only. Patch with Blind 5. |
| Edge 5. A malformed decision entry can throw a null-reference error | low | Deserialization admits null dictionary entries and the new decision scans them. Reject that state when decoding. Patch. |
| Edge 6. A malformed head can omit an active holder | medium | A nonterminal attempt slot can remain without the matching holder field. Reject orphaned nonterminal slots when decoding. Patch. |
| Verification 1. Tuple CI does not execute this assembly | medium | The existing test profiles select only Composition tests. Patch with Blind 10. |
| Verification 2. Acquire and takeover with a current stop | medium | Existing fixtures set stops outside fresh acquisition and takeover. Exercise missing, stale and exact references before checking effects. Patch. |
| Verification 3. Exact active replay after an epoch advance | medium | The stale revision case uses new bytes and does not exercise the receipt lookup. Add an exact replay at the wrong epoch. Patch. |
| Verification 4. Pointer lineage on replacement | medium | Existing cases only initialize the pointer. Verify replacement refuses a new pointer identity and accepts the current identity at the later epoch. Patch. |
| Verification 5. S3 ambiguous response classification | medium | Backend uncertainty tests skip S3 exception classification. Exercise the adapter with a deterministic post-write server error. Patch. |

| Follow-up finding | Verdict | Evidence and disposition |
| --- | --- | --- |
| Blind 1. Escaped key can exceed S3's size limit | medium | Builds accepts a 512-character record ID made of tildes. Percent-encoding expands it after the existing cutoff. Hash the segment when the final key would exceed the limit and test the adapter. |
| Blind 2. Clearance can exceed the schema's cause limit | low | Builds accepts at most 128 dispositions. A backlog of 129 distinct causes requires a change to the clearance protocol. State the bound in the contract for the lifecycle integration. |
| Blind 3. Public reads omit stop history for signing after a restart | medium | The store retains the signed predecessor chain. Callers can keep exact references from receipts, and the public state exposes the active stop and last sequence number. Defer the restart read path and cause collection to stop producer and Administrator integration. |
| Blind 4. A new acquisition can win before pointer publication | medium | A terminal revision releases the holder. Builds validates the subsequent pointer against the current epoch, which an acquisition can advance. Document the required lifecycle coordination. |
| Blind 5. Revoking a signer can block transitions over its records | false | The required independently enrolled current policy rejects a revoked key. Re-verification of the holder and stop preserves that authority boundary. Recovery policy belongs to the lifecycle and signing custody work. |
| Blind 6. Initialization after a delete marker can reset state | low | The provisioning call issues create-only, and a marker can make a key appear absent to the provider. Deletion and lifecycle expiry are prohibited for heads. Clarify that provisioning applies only to a new environment and head loss requires recovery. |
| Blind 7. Published Builds pin predates the new solution projects | medium | Carried. The recorded release and gitlink differ, and Builds 6.6.2 lacks the v2 API. The existing Builds 6.7.0 adoption deferral owns this gate. |
| Blind 8. Tuple evidence omits the AttemptStore collector's loaded artifacts | medium | The non-Composition branch executes a separate assembly. The existing output and artifact checks inspect only Composition, so verify the new collector and its Tooling copy. |
| Blind 9. An archive conflict can be followed by confirmed absence | low | Carried from the first review. A conditional 409 followed by absence can produce `record-content-conflict` while the other acquisition proceeds. Reject the added retry logic for this rare provider ordering. |
| Blind 10. Hosted proof depends on a separate version listing | low | The credentialed test observes a current version and the winner but does not list the history. Assert the fixture's two versions and zero delete markers in the test. |
| Blind 11. A staging initiator can hold the lock before supersession | medium | The staging attempt exists before the decision race. Document that the lifecycle caller must confirm supersession before performing conflicting mutation. |
| Blind 12. Targeted reads conflate missing head and missing value | low | The nullable targeted reads cover both cases. Document `ReadAsync` as the presence check and keep the public behavior. |
| Edge 1. Tilde expansion can overflow the S3 key | medium | The percent-escaped length can exceed 1024 bytes for a valid signed ID. Patch with Blind 1. |
| Edge 2. A 129th distinct cause leaves no valid clearance | low | The existing Builds bound prevents a single clearance over 129 distinct causes. A defensive rejection would drop a new cause from an active stop. Record the limit with Blind 2. |
| Edge 3. Production mutation can race the final production read | medium | Carried. The ETag check is a read across two objects. The existing production lifecycle serialization deferral owns the protected phase boundary. |
| Edge 4. An archive conflict with a missing reread can falsely refuse identical bytes | low | Carried from the first review with Blind 9. The provider timing is transient and no invalid state commits. |
| Edge 5. The tuple solution build requires a newer published Tooling API | medium | Carried with Blind 7. The pinned Builds release lacks the required API. |
| Edge 6. Tuple tests can report the wrong Tooling provenance | medium | Composition's dependency check does not cover the separate AttemptStore output. Patch with Blind 8. |
| Verification 1. In-progress revision holder is untested | medium | All existing revision cases submit a terminal revision. Exercise committing signed progress and preserving the holder and epoch. |
| Verification 2. Authoritative read of a substituted signed envelope is untested | medium | Existing replay tests change input bytes, leaving the archive untouched. Exercise a valid signature with the same signed reference and different envelope bytes returned from storage. |
| Verification 3. Adapter tests omit `NoSuchBucket` on GET | low | The missing-key fixture alone cannot catch a filter that treats all 404 errors as absence. Check both error codes. |
| Verification 4. Reuse of a pending decision ID on a different release is untested | medium | Existing tests vary the decision ID or use a terminal prior decision. Exercise the pending decision's exact release binding. |
| Verification 5. Reset proof revalidation after CAS loss is untested | medium | Existing tests vary proof results between calls, and the race uses a release rather than a reset. Force a conflict and assert withdrawal prevents supersession. |
| Verification 6. Recorded tuple workflow cannot execute the new test group | medium | Carried with Blind 7. The test selectors passed against published 6.7.0 directly. The full workflow still depends on the existing pin adoption. |

## Design Notes

Head `environments/{env}/head.json`; records at `environments/{env}/records/{kind}/{recordId}/{digest}.json`. A record orphaned by a lost head write is unreferenced and harmless.

```json
{"schema":"hexalith.attempt-store.head.v1","environment":"staging","revision":42,"previous":"sha256:…",
 "epoch":7,"holder":"att-…","attempts":{"att-…":{"digest":"sha256:…","terminal":false}},
 "stop":{"revision":3,"recordId":"stop-…","digest":"sha256:…"},
 "decisions":{"dec-…":{"state":"reserved","reservation":{"recordId":"…","digest":"…"}}}}
```

## Verification

- The Release build used `dotnet build tests/Hexalith.Platform.AttemptStore.Tests/Hexalith.Platform.AttemptStore.Tests.csproj -c Release -m:1 -nodeReuse:false` with outputs redirected under the disposable workspace. Both source and published-assembly builds reported zero warnings and errors.
- The xUnit assembly ran under `dotnet exec` with the hosted credentials for 40 cases and with the current tuple selectors for 39 published-assembly cases. The XML reports confirmed every acceptance-matrix case passed.
- `PYTHONPATH=eng python3 -B -m unittest` ran `test_builds_publication`, `test_builds_tuple`, `test_current_catalog_view`, `test_tuple_collectors`, `test_tuple_sdk_suites` and `test_tuple_output_identity`. All 89 cases passed.
- `git diff --check` passed.
