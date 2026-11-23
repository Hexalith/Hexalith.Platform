---
title: '4.6 Encode release, attempt, qualification and Administrator records'
type: 'feature'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '6fbe5a024650382e25bac7d47ac919457e8d393c'
builds_baseline_commit: '2210aaaf54c779b8763dbf1e8767bdc1b422bc6d'
builds_implementation_commit: 'c28c5c255226f85ef9533894b722d1ad99816b5b'
story_key: '4-6-encode-release-attempt-qualification-and-administrator-recor'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Platform lacks shared signed operational records. Store access or self-declared identity must never authorize deployment decisions.

**Approach:** Implement eight versioned record contracts, strict encoding, signature verification and state-transition validation in Hexalith.Builds, following the Platform architecture's writer matrix.

## Boundaries & Constraints

**Always:** Bind schema, kind, record ID, writer identity/key ID, subject, environment where applicable, timestamps and payload in the signature. Resolve keys and writer permissions from independently supplied trust configuration; reject unknown, revoked, wrongly scoped or overlapping Administrator/non-Administrator identities and public keys. Administrator custody is a trusted enrollment assertion, never inferred from a record. Preserve all existing edits.

Release and release-available records belong to publication; attempts belong to their environment executor (production executor for in-place recovery; recovery executor for replacement-capacity DR). Qualification and production-promoted records belong to their issuing attempt's writer. Latest-working advances only through a working terminal attempt's writer, in the same environment, at a higher epoch. Executors, monitor, Administrator and deputy may set environment-bound promotion stops; clearance requires an Administrator record.

**Never:** Implement storage, locks, deployment, workflow publication, production identities or key provisioning. Do not change package pins or create another CLI. Store integration and lifecycle eligibility remain downstream responsibilities; transition checks must run within the later store's CAS operation.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Error handling |
| --- | --- | --- | --- |
| Authentic record | Valid schema, signature and entitled writer | Return verified typed record and content digest | No store-access shortcut |
| Forgery or confusion | Tampering, wrong writer/environment/key, duplicate/unknown fields, unsupported kind/version/algorithm | Reject before returning trusted data | Stable metadata-only failure |
| Terminal attempt | Previously verified terminal record; proposed changed record | Reject every mutation, including writer replacement | Exact replay may be a no-op |
| Supersession | Nonterminal predecessor plus verified takeover linked to it at the current higher CAS epoch | Only successor writer can terminalize predecessor as superseded | Missing/conflicting takeover evidence rejects |
| Administrator action | Executor, workflow, monitor or deputy key claims Administrator | Reject despite valid cryptographic signature | Fail closed on shared identity/key enrollment |

</frozen-after-approval>

## Code Map

Paths below are relative to `references/Hexalith.Builds`.

- `src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs`, `ModuleManifestLoader.cs`, `JsonDuplicatePropertyValidator.cs`: reuse strict parsing and embedded, offline JSON Schema patterns.
- `src/libraries/Hexalith.Builds.Tooling/RunEvidence/ModuleRunEvidenceWriter.cs`: deterministic UTF-8 encoding pattern; leave existing evidence contracts unchanged.
- `src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj`: existing shared assembly, central schema dependency and embedded resources; presently bundled with CLI packages, not independently published.
- `test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj`: existing xUnit/Shouldly project; no new package dependencies needed.
- `{project-root}/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md`: authoritative fields, writers and takeover semantics. Existing development JWTs grant no hosted authority.

## Tasks & Acceptance

**Execution:**
- [x] `schemas/hexalith.release-records.v1.json` — define eight closed, individually identified versioned branches and their writer/signature envelopes; embed through the existing Tooling project file.
- [x] `src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/{Release,Attempt,Qualification,ProductionPromoted,ReleaseAvailable,LatestWorkingPointer,PromotionStop,Administrator}Record.cs` — add typed payloads, one type per file. Release binds artifact/host/catalog/McpCli/profile/contract/suite identities, sidecar patch, qualified sets and classifications. Attempt binds release, kind, environment-current values, epoch, timing/deadlines/grace, recovery allowance, observed stop and outcome. Other kinds bind their issuing attempt, release/profile/qualified sets or observed action/stop as applicable.
- [x] `src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/{SignedRecord,RecordTrustPolicy,ReleaseRecordCodec}.cs` — implement bounded strict decoding, deterministic signing bytes/digests and P-256/SHA-256 signatures using platform cryptography. Cover every accepted field; reject ambiguous JSON. Private keys remain caller-held; trust configuration contains public anchors and explicit scopes.
- [x] `src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/RecordTransitionValidator.cs` — validate writer matrix, referenced verified records, immutable terminal state, linked takeover and monotonic latest-working moves. Keep verified inputs immutable; unverified DTOs cannot masquerade as verified context.
- [x] `test/Hexalith.Builds.Module.Tests/ReleaseRecords/{RecordSchema,RecordSignature,RecordTransition}Tests.cs` — test every matrix row and all eight kinds with real ephemeral keys, including key aliases, cross-environment replay, stale epochs, unauthorized stop clearance, post-verification mutation and deterministic encoding vectors.
- [x] `ReleaseRecords.md` — document schemas, fields, writer matrix, signing format, trust/custody assumptions and CAS integration examples; preserve the dirty root README.

**Acceptance Criteria:**
- Given each of the eight record kinds, when encoded and verified, then its versioned schema identifies its writer and only an independently authorized writer signature is accepted.
- Given an existing terminal attempt or a proposed supersession, when transition validation runs, then immutability and successor-writer authority are enforced against verified prior state.
- Given an Administrator record, when verified, then only an independently enrolled Administrator identity with exclusive declared custody is accepted.

### Review Findings

Code review 2026-10-23 of Builds commit `43210de` (same story content as `c28c5c25`). Paths are relative to `references/Hexalith.Builds`; `M-nn` names a mutation that all 130 story tests survive (Verification Gap harness).

- [x] [Review][Decision] Administrator actions have no per-action required-field rules (medium) — resolved 2026-10-23: patch (minimum per-action rules; see the D1 patch below) — Only `clear-promotion-stop` is shape-checked, and only for `ObservedStop` and its revision. Verified: a clear passes with `Attempt: null`, even though the spine's Promotion stop rule requires the "verified current working attempt"; when an attempt is supplied, its outcome is never checked. `start-recovery` passes without `NamedRecovery`, and `approve-release` and `authorize-takeover` pass without `Attempt`, although the spine says the empty/degraded approval "names one attempt". `Target` is never related to the linked attempt's subject. This is ambiguous: staging has no alternate clearance path when no working attempt exists, and target semantics differ per action (see earlier BH-03/BH-04). [`src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/RecordTransitionValidator.cs:186`] (acceptance-auditor+blind-hunter+edge-case-hunter)
- [x] [Review][Decision] Transition validators never check `AttemptKind` (medium) — resolved 2026-10-23: patch for release-available; issuer-kind matrix deferred — `ValidateReleaseAvailable` accepts any working staging attempt (`rotation`, `configuration`, `in-place-restore`) as post-staging evidence. `ValidateIssuer` lets any kind issue qualification and production-promoted records. Per the spine, release-available follows staging validation of the release; qualification comes from release attempts and from environment-layer/shared attempts, which also renew production-promoted. An allowed-kind matrix is needed. [`src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/RecordTransitionValidator.cs:131`] (acceptance-auditor+blind-hunter)
- [x] [Review][Decision] Attempts don't record the digest of the environment-layer/shared-infrastructure definitions (medium) — resolved 2026-10-23: patch (add `OperationsDefinitionsDigest`) — Spine Release tiers: "each attempt records their digest" for the operations-repository definitions (pinned charts/manifests with values, rendered environment-layer objects, profile inventory, realm-contract instance, secret contract). `AttemptRecord`/`EnvironmentCurrent` carry `ActiveProfileDigest`, `SecretContractDigest` and `RealmContractVersion`, but nothing for the rendered environment-layer objects or the pinned charts. A fix changes the v1 schema and the public records. [`src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/EnvironmentCurrent.cs`] (acceptance-auditor)
- [x] [Review][Decision] Administrator records must name staging or production (low) — resolved 2026-10-23: patch (keep the schema; document the convention) — The schema's administrator `environment` excludes null. `grant-management`, `revoke-management` and `approve-infrastructure` (Rancher and shared infrastructure, which span environments) must therefore be pinned to an arbitrary environment, and the Administrator anchor must be scoped to it. [`schemas/hexalith.release-records.v1.json`, administrator branch] (acceptance-auditor)
- [x] [Review][Patch] D1: enforce minimum Administrator action shapes. `clear-promotion-stop` requires `Attempt`, and validation requires that attempt to be `working` in the same environment; `start-recovery` requires `NamedRecovery`; `approve-release` and `authorize-takeover` require `Attempt`. `Target` semantics are unchanged. [`src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/ReleaseRecordCodec.cs:258`, `RecordTransitionValidator.cs:210`]
- [x] [Review][Patch] D2: `ValidateReleaseAvailable` requires the staging attempt's `AttemptKind` to be `release` [`src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/RecordTransitionValidator.cs:137`]
- [x] [Review][Patch] D3: add `OperationsDefinitionsDigest` (`sha256:` digest of the operations-repository definitions) to `EnvironmentCurrent`, the schema and the README [`src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/EnvironmentCurrent.cs`]
- [x] [Review][Patch] D4: document in `ReleaseRecords.md` that management grants name `production` and infrastructure approvals use one record per affected environment [`ReleaseRecords.md`]
- [x] [Review][Defer] Restrict which attempt kinds may issue qualification and production-promoted records [`src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/RecordTransitionValidator.cs:253`] — deferred: the spine legitimately lets release, environment-layer and shared-infrastructure attempts issue these records, so the matrix needs lifecycle design in Story 4.7 (Administrator decision D2)
- [x] [Review][Patch] A `default` `ImmutableArray` payload escapes `Sign`/`Encode`/`GetSigningBytes` as `InvalidOperationException` instead of `RecordValidationException` (low; probe-confirmed; contradicts the README failure contract) [`src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/ReleaseRecordCodec.cs:218`] (blind-hunter+edge-case-hunter)
- [x] [Review][Patch] `EnsureP256` dereferences a null `Curve.Oid` for explicit-curve keys, so the `RecordTrustPolicy` constructor and `Sign` throw `NullReferenceException` (low; probe-confirmed) [`src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/RecordTrustPolicy.cs:103`] (edge-case-hunter)
- [x] [Review][Patch] The public-key-only enrollment check is bypassable and untested (medium) — An `EC PRIVATE KEY` wrapped in `PUBLIC KEY` framing imports and is kept in `PublicKeyPem` (probe-confirmed). M18 (prefix check) and M26 (revoked anchors skip the custody-overlap checks) both survive; check label and SPKI explicitly, then add both tests. [`src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/RecordTrustPolicy.cs:76`] (edge-case-hunter+verification-gap)
- [x] [Review][Patch] `ExpectedStopRevision` set without `ObservedStop` is silently ignored for non-clear Administrator actions (low) [`src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/RecordTransitionValidator.cs:216`] (blind-hunter+edge-case-hunter)
- [x] [Review][Patch] `causeDispositions` values are free text in the schema, but the spine allows only `resolved` or `accepted` (low) [`schemas/hexalith.release-records.v1.json:1360`] (blind-hunter)
- [x] [Review][Patch] No test rejects a writer change on an in-progress attempt revision (medium; M01 survives) [`test/Hexalith.Builds.Module.Tests/ReleaseRecords/RecordTransitionTests.cs:264`] (verification-gap)
- [x] [Review][Patch] The supersession `wrong-environment` case is masked by a writer mismatch; add a dual-environment anchor (medium; M02 survives) [`test/Hexalith.Builds.Module.Tests/ReleaseRecords/RecordTransitionTests.cs:206`] (verification-gap)
- [x] [Review][Patch] Untested supersession cases: same epoch, wrong `SupersededBy`, altered predecessor payload, superseded first record (medium; M03/M04/M05/M25 survive) [`test/Hexalith.Builds.Module.Tests/ReleaseRecords/RecordTransitionTests.cs:206`] (verification-gap)
- [x] [Review][Patch] The recovery-kind fixed-lifetime rule is never exercised through a phase start (medium; M06 survives) [`test/Hexalith.Builds.Module.Tests/ReleaseRecords/RecordTransitionTests.cs:279`] (verification-gap)
- [x] [Review][Patch] Untested attempt-revision invariants: fixed `FirstMutationUtc`, identity and recorded-time order, attempt subject, no new deadlines on an epoch-raising revision (medium; M07/M08/M09/M29 survive) [`test/Hexalith.Builds.Module.Tests/ReleaseRecords/RecordTransitionTests.cs:89`] (verification-gap)
- [x] [Review][Patch] Qualification and production-promoted negatives cover only writer and profile; references, issuer outcome, subject and qualification binding are untested (medium; M10/M11 survive) [`test/Hexalith.Builds.Module.Tests/ReleaseRecords/RecordTransitionTests.cs:294`] (verification-gap)
- [x] [Review][Patch] The latest-working environment case is masked; terminal-attempt epoch, pointer identity and attempt subject are untested (medium; M15a–c/M28 survive) [`test/Hexalith.Builds.Module.Tests/ReleaseRecords/RecordTransitionTests.cs:376`] (verification-gap)
- [x] [Review][Patch] Promotion-stop `PreviousStop` lineage and environment are never challenged (medium; M12 survives) [`test/Hexalith.Builds.Module.Tests/ReleaseRecords/RecordTransitionTests.cs:414`] (verification-gap)
- [x] [Review][Patch] The Administrator lineage test never changes target or authority family; attempt links and non-clear observed-stop links are untested (medium; M13/M14/M14b survive) [`test/Hexalith.Builds.Module.Tests/ReleaseRecords/RecordTransitionTests.cs:455`] (verification-gap)
- [x] [Review][Patch] Untested trust-policy environment authority: production-promoted in staging, recovery-executor in staging (medium; M16/M17 survive) [`test/Hexalith.Builds.Module.Tests/ReleaseRecords/RecordSignatureTests.cs:143`] (verification-gap+acceptance-auditor)
- [x] [Review][Patch] Untested codec envelope checks: `invalid-subject`, `noncanonical-value`, `invalid-timing`, `invalid-stop-clearance`, BOM and malformed UTF-8 inside a string; custody-overlap tests don't assert their code (medium; M19/M20/M21 survive) [`test/Hexalith.Builds.Module.Tests/ReleaseRecords/RecordSchemaTests.cs:102`] (verification-gap+blind-hunter)
- [x] [Review][Patch] 8 codec attempt-invariant clauses are untested (grace bound, recovery allowance and consumption, entry and first-mutation bounds, supersession pairing) (medium; M22/M23/M30 survive) [`test/Hexalith.Builds.Module.Tests/ReleaseRecords/RecordSignatureTests.cs:88`] (verification-gap+blind-hunter)
- [x] [Review][Patch] The `EnforcesEncodingBounds` depth case passes without `MaxDepth`; assert failure codes (low; M24 survives) [`test/Hexalith.Builds.Module.Tests/ReleaseRecords/RecordSchemaTests.cs:110`] (verification-gap+blind-hunter)
- [x] [Review][Patch] `TamperingNeverReturnsTrustedData` does not prove that `writerId`, `keyId`, `subject` and `environment` are signed: those mutations fail as `untrusted-writer`, `invalid-subject` or `schema-rejected`, and the test asserts only null (medium) [`test/Hexalith.Builds.Module.Tests/ReleaseRecords/RecordSignatureTests.cs:34`] (blind-hunter)
- [x] [Review][Patch] The canonical form is pinned only by one ASCII signing-bytes vector; there is no encoded-output, digest or non-ASCII escaping vector (low) [`test/Hexalith.Builds.Module.Tests/ReleaseRecords/RecordSignatureTests.cs:299`] (blind-hunter+acceptance-auditor)

**Rejected**

- false — `Verify` returns qualification and production-promoted records signed by a non-issuing executor (AA): Task 4 assigns issuer-writer binding to `RecordTransitionValidator`. `Verify` cannot resolve the issuing attempt, and the README requires both steps.
- false — Takeover lacks the spine's Administrator/recovery-entry authority (AA): the spec's Never section keeps lifecycle eligibility downstream, the README defers takeover eligibility to the store, and the I/O supersession row is implemented.
- false — The schema does not name a writer role per kind (AA): the envelope identifies the writer through `writerId`/`keyId`, and the writer matrix is enforced in `Resolve`.
- false — No test has a non-Administrator sign `clear-promotion-stop` (AA): `Resolve` gates the `administrator` kind regardless of action, so `AdministratorCannotBeSelfDeclared` covers the same gate.
- false — Same-writer resumption cannot identify the lock holder (BH): under the documented atomic successor-plus-predecessor commit, every in-progress attempt other than the lock holder is already terminal.
- false — `MaximumLifetimeUtc` has no upper bound (BH): no policy maximum is specified, and the trusted executor sets the phase deadlines.
- false — The README calls the nonexistent `RequireVerified` (BH): the block is labeled as illustrative pseudocode.
- false — `OutageUtc`/`ObservedStop` cannot be set after creation (EC): the spine records the outage time at entry, and a later stop must not alter the observed stop.
- low — `InternalsVisibleTo("Hexalith.Builds.Evidence.Cli")` can construct `VerifiedRecord` (AA): it needs deliberate first-party code, and a fix adds token or factory complexity.
- low — Case variants of an identity escape the custody-overlap check (AA): enrollment requires canonical principal identities as documented; case folding alone would not canonicalize them.
- low — A first attempt record may be terminal or carry a dangling `Supersedes` (BH): the trusted executor gains no authority from it, the link is checked when the predecessor is terminalized, and a fix adds guards.
- low — Optional `previous` parameters let a store skip history checks (BH): needs a store bug against the documented examples; the fix is an API redesign.
- low — Missing chronology checks: `FirstMutation` ≤ `PreMutationDeadline` and `PreMutationDeadline` ≤ `MaximumLifetime` (BH+EC): unlikely from the trusted executor, and a fix adds guards that could block recording a real late mutation.
- low — Redundant schema evaluation, serialization and PEM import in `Verify` (BH): records are small and low-throughput, and the fix is a refactor.
- low — `format: date-time` is not asserted; hostnames use the generic identifier pattern (BH): the codec's canonical comparison already rejects alternative timestamp spellings.
- low — The schema duplicates its patterns about 70 times, and per-kind `$id`s don't resolve (BH): maintainability only; the fix is a refactor.
- low — No `IsSuccess` or public constants for codes, kinds and roles (BH): ergonomics, and a fix adds public surface.
- low — Trust policy accepts unusable entries (whitespace IDs, publication with environments, roles that don't match kinds) (BH): these fail closed at `Resolve`, and a fix adds guards.
- low — Exact replay of a latest-working pointer fails after the epoch moves (BH): it fails loudly and is unlikely; a fix adds a branch.
- low — A lone surrogate becomes U+FFFD, so distinct signer-side IDs collide (EC): unlikely input, and a fix adds a guard.
- low — Identifier endpoints admit U+00A0/U+2028/U+3000/U+200B/U+FEFF/U+202E (EC): unlikely from trusted writers, and a fix needs a chosen invisible-character set.
- low — `maxLength` counts grapheme clusters, so identifiers can exceed 512 UTF-16 characters (EC): records are already bounded at 1 MiB, writers are trusted, and a fix adds a guard.

### Review Findings — delta review of Builds `4adecfe2` (2026-11-23)

Scope: `b80f3e85..4adecfe2`, limited to the story paths (19 files, +2106/−120). That covers the resumed v1 patches and the v2 staging-evidence amendment. Paths are relative to `references/Hexalith.Builds`. Layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor; none failed. The verification-gap layer mutated an isolated `git archive` copy: each gap below leaves all 254 ReleaseRecords cases green.

- [x] [Review][Decision] v2 reservation closure only accepts the admitted attempt's own uninterrupted lineage (medium) — resolved 2026-11-23: document the constraint now and defer the closure extension to Story 4.7 — `complete` and `release-reservation` require `SameIdentity(attempt, admittedAttempt)` and the admitted attempt's writer. The trust policy also requires the `executor` role on the closing key. `MatchesDecisionAttempt` requires the terminal attempt's epoch to equal the current epoch. Two cases follow. After an authorized fenced takeover, the successor has a new record ID; in the DR case its writer is `recovery-executor` only. After a terminal revision, a lock re-acquisition raises the epoch, and terminal attempts cannot be re-signed. In both cases no signed action can complete or release the reservation, and the decision stays reserved permanently. Policy v2 item 4 expects takeover or terminal reconciliation to resolve it. Separately, `ValidateAttempt` only requires a v2 successor to use the v2 schema, not to carry the predecessor's `stagingEvidence`. A takeover can therefore rebind the lineage to an unrelated decision, and which decision a DR successor binds is undefined. Safety holds (fails closed); liveness does not. [`src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/RecordTransitionValidator.cs:147`, `:54`, `:327`; `RecordTrustPolicy.cs:165`] (blind-hunter+edge-case-hunter+acceptance-auditor)
- [x] [Review][Patch] Supersession trigger eligibility is untested. Add refusals for a working, post-mutation or `rollback` trigger. Add a reset bound to another decision, and a reset pointing at another digest of the release record. Add a cross-release v1 attempt with no binding, and a production attempt by a dual-scoped writer. Reuse the verification-gap probes. [`test/Hexalith.Builds.Module.Tests/ReleaseRecords/PromotionDecisionTests.cs:139`] (verification-gap+blind-hunter)
- [x] [Review][Patch] The action's own signed release, subject and `lockEpoch` are never refused in isolation. Add three cases: `reserve-administrator` with another release digest, a subject other than the release ID, and a stale signed epoch against a current attempt. [`src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/RecordTransitionValidator.cs:321`] (verification-gap)
- [x] [Review][Patch] Closure lineage and reservation-shape checks are unpinned. Add four refusals: a terminal revision by another production executor, a terminal revision recorded before the admitted one, a reservation signed for another decision, and a non-admission action supplied as the reservation. [`src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/RecordTransitionValidator.cs:144`] (verification-gap)
- [x] [Review][Patch] The only barrier keeping `reserve-administrator` in production is an untested schema rule. Add a staging `reserve-administrator` case that expects `schema-rejected`. [`schemas/hexalith.release-records.v2.json:513`] (verification-gap)
- [x] [Review][Patch] The Blind 7 wire-shape patch was only partly applied. `V2ActionRequiresOnlyItsSchemaFields` has five rows and none for `supersede`. The validator never reads `payload.Reservation` or `payload.WorkingPointer` for admission or supersession, so the schema is the only barrier. Add the missing and forbidden rows for `supersede`, `reserve-admission`, `release-reservation` and `reserve-administrator`. [`test/Hexalith.Builds.Module.Tests/ReleaseRecords/PromotionDecisionTests.cs:485`] (blind-hunter+acceptance-auditor)
- [x] [Review][Patch] The `SchemaFor` guards for unsupported versions are untested (low). Assert `unsupported-record` for `SchemaFor<PromotionDecisionRecord>()` and `SchemaFor<ReleaseRecord>(V2)`. [`src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/ReleaseRecordCodec.cs:52`] (verification-gap)
- [x] [Review][Patch] The v1 migration note is incomplete (low). The `*.v1` identifiers shipped since Builds v4.36.0 now also reject several records: non-`resolved`/`accepted` cause dispositions, `approve-release`/`authorize-takeover` without `attempt`, `start-recovery` without `namedRecovery`, stop clearance without a working attempt, and `release-available` from a non-`release` attempt. The note mentions only `operationsDefinitionsDigest`. State that pre-review v1 records are superseded. [`ReleaseRecords.md:24`] (blind-hunter+edge-case-hunter+acceptance-auditor)
- [x] [Review][Patch] The v2 CAS handoff omits policy item 2's exclusion checks (low). Supersession must also confirm that there is no current admission reservation and that no Administrator reservation applies. Admission must confirm that no supersession has committed. List these as Story 4.7 obligations. [`ReleaseRecords.md:101`] (blind-hunter)
- [x] [Review][Patch] D1: document that closure accepts only the admitted attempt's own lineage at its terminal epoch, so Story 4.7 must commit the terminal revision, working pointer and closure in one compare-and-set. [`ReleaseRecords.md:103`]
- [x] [Review][Defer] Closure after a cross-writer or DR takeover, or after the epoch moves past the terminal revision, and the v2 successor's decision binding. [`src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/RecordTransitionValidator.cs:147`] — deferred: Story 4.7 designs takeover closure together with its cross-attempt takeover reconciliation. V2 cannot activate before 4.7, and closure fails closed until then.

**Rejected**

- false — The C# `EnvironmentCurrent` change breaks API consumers under a `feat` commit (BH): `Hexalith.Builds.Tooling` is `IsPackable=false` and ships only inside first-party CLI packages, so no external caller compiles against it.
- false — A mixed v1/v2 store cannot be read, and the CAS snippet omits a version (BH): the policy forbids version fallback, explicit selection fails closed, and the snippet is labeled illustrative pseudocode.
- false — The observed-stop/expected-revision pairing is enforced only in transition validation (BH): same claim and refutation as R2-B9.
- low — Management grants that name `staging` still verify (BH): this implements the owner's D4 decision (document the convention only). Only the exclusive Administrator signs these records, and enforcing the convention adds a guard.
- low — Same as the previous item (EC): same D4 disposition.
- low — The validator has no previous-pointer or initiating-release parameters (BH): the v2 review (Blind 2, Blind 6) chose documented 4.7 obligations, and the fix adds public parameters.
- low — No test guards v1/v2 attempt-schema drift, and no v2 attempt vector exists (BH): both versions are immutable, the 31 source hashes pin them, and round trips refuse an omitted binding.
- false — Stop clearance accepts any same-environment working attempt (BH): the action must reference the exact supplied attempt, and the documented flow supplies `current.WorkingAttemptBytes`; this matches the owner's D1 rule.
- low — The `operationsDefinitionsDigest` computation is undefined (BH): like `secretContractDigest`, it is a producer-supplied digest. The operations-definitions producer owns its canonical computation, and 4.6 has no producer.
- false — The documented failure-code list is incomplete (BH): `ReleaseRecords.md` says "Typical codes include", so the list is not exhaustive.

The patches landed as Builds `93da9a5fa18a2f92a899d570619035262981a10c` on `main`, a fast-forward from `872693e8` (pushed 2026-11-23). It is a test and documentation change, so the Platform gitlink stays at `4adecfe2`. On that revision, the Debug `Hexalith.Builds.Module.Tests` build finished with zero warnings and errors. All 281 ReleaseRecords cases passed, 254 existing and 27 new, with no skips. Each of the 17 verification-gap mutations, re-applied to a scratch copy, now fails exactly its intended new case; before the patch, all of them survived. `git diff --check` is clean, and the commit message passed Builds' pinned commitlint. The full module suite was not rerun, because only tests and documentation changed. [Per-case results](evidence/story-4-6-delta-review/release-records-tests.xml) · [Build log](evidence/story-4-6-delta-review/module-build.log) · [Commitlint](evidence/story-4-6-delta-review/commitlint-builds.log)

## Implementation Notes

- Implemented the eight immutable payloads, embedded schema, canonical ES256 codec, independent trust policy and verified-only transition API in Builds; documented integration in `ReleaseRecords.md`.
- Parent inspected the complete story diff, including untracked source/tests, at `/tmp/story46-task-39luvk74.diff`. Full concurrent workspace diff retained separately at `/tmp/story46-full-8w40d008.diff`; unrelated changes are preserved.
- Matrix audit: all five rows have executed passing tests in `/tmp/story46-focused-tests.xml` (78 passed; zero failures, skips or unrun cases). Focused harness imports Builds warning gates and pinned package configuration and compiles original source/schema paths in separate library/test projects; build has zero warnings/errors.
- Regression coverage includes DR-only takeover writers and exact canonical terminal replay including the signature. Verification commands and source hashes are in `/tmp/story46-verification-commands.txt` and `/tmp/story46-source-sha256.txt`.
- Normal module-project build is currently blocked by concurrent `CheckSuites/CheckSuiteContract.cs` SA1143 diagnostics at lines 191 and 232; full existing-suite verification remains outstanding. Exact output: `/tmp/story46-normal-build-final.log`. No warning gate was weakened.

- Final verification after review fixes: focused build is clean and 130/130 cases pass; the ordinary Debug module-project build has zero warnings/errors and the complete module suite passes 1789/1789, with zero failures or skips. The earlier concurrent blockers are resolved; one missing blank line was added to the renderer test without changing its statements.
- All 13 accepted review findings were patched across 11 root-cause entries, with no public API expansion. Parent inspected the patch delta and audited executed coverage again. Preserved initial phase progress, single automatic recovery (including resumption), fixed recovery notification lifetime and staging in-place restore.
- Retained evidence: [verification packet](evidence/story-4-6/verification.json), [focused per-case XML](evidence/story-4-6/focused-tests.xml), [module suite output](evidence/story-4-6/module-tests.log), and source hashes with the exact focused fallback harness. Hash audit confirms the 26 captured source/configuration inputs still match.

- Local Builds implementation commit: `c28c5c255226f85ef9533894b722d1ad99816b5b`. Only story-owned files and its schema-resource addition were staged; concurrent shared-file work was preserved. Both exact commit messages passed each repository’s pinned commitlint configuration. Full module verification used the combined working tree recorded in the packet; no remote operation was performed.

- Resumed against Builds revision `2d9ef8b454ff897daca0149f3549844f584b634a`. Applied the open review patches and verified all five approved matrix scenarios in 201 focused cases with zero failures, skips or unrun cases. The Debug build completed with zero warnings and errors. [Focused results](../../artifacts/story-4-6-review-patches/focused-tests.xml) · [Build log](../../artifacts/story-4-6-review-patches/module-build.log)
- The amended epic adds signed staging-evidence policy v2 decisions. This criterion needs its own approved specification and evidence. Story 4.7 owns the atomic store integration. [Current Story 4.6](../planning-artifacts/epics.md#story-46-encode-release-attempt-qualification-and-administrator-records) · [Staging evidence policy v2](../../docs/contracts/staging-evidence-policy-v2.md)
- The approved v2 amendment is implemented and verified in Builds revision `4adecfe2af4758aa66039a1221de20774ca56e87`, which also contains the reviewed v1 changes above. [V2 implementation and evidence](spec-4-6-sign-staging-evidence-v2-decisions.md)

## Spec Change Log

## Review Triage Log

| ID | Verdict | Evidence and route |
| --- | --- | --- |
| BH-01 | medium | The fixed-fields comparison masks rollout, verification and maximum-lifetime updates; the same-epoch branch has no established-deadline guard. Patch local transition checks and regression tests, preserving legitimate new phase deadlines and absolute recovery lifetime. |
| BH-02 | medium | Codec timing checks omit phase-before-entry and future-outage rejection, and ordinary phase bounds can exceed the declared lifetime. Patch elementary chronology; recovery lifetime remains a notification bound and must not become an abort limit. |
| BH-03 | false | Administrator actions always bind a signed action and target; an optional attempt reference supplies existing context. No operation is executed here, and the documented downstream entry/eligibility checks must bind newly entered recovery/takeover operations. The probe shows representable signed metadata, not an unauthorized operation or entitlement bypass. |
| BH-04 | false | The signed envelope subject must equal the action target. A target can name a release, environment or recovery rather than the referenced attempt ID, so forcing equality would conflate distinct fields; no caller executing an unrelated operation was demonstrated. |
| BH-05 | medium | Clearance checks current stop identity/revision but never reads CauseDispositions. Patch rejection of a missing or invalid disposition for the supplied current cause; downstream store retains responsibility for all outstanding cause history. |
| BH-06 | low | Staging in-place restore is required by Stories 4.23/5.10, so the code correctly admits its staging executor. The writer-table wording is ambiguous: directly clarify target-environment ownership, with production ownership when the target is production. |
| BH-07 | medium | The dollar end anchors admit a final newline in identifiers, digest strings and signature text; base64 decoding then accepts it. Patch whole-string matching and add signed-input regressions. |
| BH-08 | medium | NamedRecovery.MaximumDurationSeconds is Int32 while its schema maximum is 9007199254740991. Patch the schema maximum to Int32.MaxValue and verify the published/typed boundary; grouped with EC-03. |
| BH-09 | medium | Sign calls EnsureP256/SignData outside any expected-failure translation, contradicting its documented metadata-only RecordValidationException contract. Patch known invalid signing-key failures without changing null-argument behavior or exposing crypto exception content. |
| BH-10 | medium | Digest and enum mutations use schema-invalid values, so null verification alone does not establish signature coverage. Patch schema-valid independent mutations and assert invalid-signature; grouped with VG-01. |
| EC-01 | medium | The exact-envelope early return precedes currentEpoch validation for in-progress attempts. Patch active replay to require the current epoch while retaining terminal exact-replay no-ops. |
| EC-02 | medium | The identifier regex first/last non-whitespace classes allow NUL even though the interior class excludes controls. Patch the endpoint classes and test leading/trailing control characters; distinct from the end-anchor defect. |
| EC-03 | medium | The schema permits a named duration above Int32.MaxValue but typed decoding cannot represent it. Same verified contract mismatch as BH-08; one schema-bound patch. |
| VG-01 | medium | Pre-verified broken-verification gap: applicationPackageDigest tampering is currently schema-invalid, masking a missing signature binding. Patch the signature test with valid digest replacements and the exact invalid-signature result; grouped with BH-10. |
| VG-02 | medium | Pre-verified regression gap: ValidateReleaseAvailable has only a valid working-staging invocation. Add independent negative cases for outcome, environment, exact references and composed image, asserting invalid-transition. |


### Review of the resumed implementation

The current review examined the release-record changes against the historical Builds baseline and distinguished the resumed patch from earlier implementation and planning work. The Debug build uses the current Builds checkout at `2d9ef8b454ff897daca0149f3549844f584b634a`.

| ID | Verdict | Evidence and route |
| --- | --- | --- |
| R2-B1 | false | The scoped review diff includes project-file history. The complete tree at the current revision supplies the SDK and resource prerequisites used in verification. |
| R2-B2 | medium | Defer to intake integration. The existing release enum uses `compatible` and `unchanged` where module-manifest v2 uses `additive` and `none`. Decide the wire vocabulary and legacy handling with the Story 4.8 producer. |
| R2-B3 | medium | Defer with the existing issuer-eligibility work. Signature and issuer checks currently admit a staging qualification from an in-progress attempt. The staging consumer needs the passing outcome while production has a pre-rollout issuance path. |
| R2-B4 | medium | Defer with attempt lifecycle timing. Existing encoding does not compare an ordinary attempt's initial lifetime to its pre-mutation bound. |
| R2-B5 | medium | Defer with attempt lifecycle timing. The existing phase-transition formula runs when the lifetime changes and misses an unchanged lifetime that differs from the new deadline plus grace. |
| R2-B6 | medium | Defer with DR entry timing. The existing common lifetime check would refuse a first covered recovery entry already beyond its outage-derived notification deadline. |
| R2-B7 | low | Reject the standalone-successor claim as a deployment defect. The documented store flow commits the successor and verified predecessor terminalization together. Validating a first envelope alone supplies no takeover authority. |
| R2-B8 | medium | Defer to the production admission grant contract. Storing a signed G1 grant expiry requires a new field or a separately versioned admission record and policy for G2 expiry. |
| R2-B9 | false | An expected revision without the observed stop is rejected by `ValidateAdministrator`. The documented pairing rule is part of CAS transition validation. `Verify` authenticates the signed input and supplies no clearance authority. |
| R2-B10 | low | Carried from the previous review. A single malformed UTF-16 character can be normalized by the serializer. Reject the proposed general identifier guard under the earlier disposition. The distinct duplicate-object case is R2-E3. |
| R2-B11 | low | Patch with R2-E1. An already disposed key otherwise escapes the documented signing failure code. |
| R2-B12 | medium | The amended epic's v2 criterion is open. The frozen eight-contract specification predates it. Resolve the encoding amendment before story acceptance. |
| R2-B13 | low | Patch the canonical vector test using an independently generated P-256/SHA-256 P1363 signature over the fixed UTF-8 bytes. A paired codec shift must fail verification. |
| R2-E1 | low | Patch with R2-B11. Key operations can throw `ObjectDisposedException`, which must map to `invalid-signing-key`. |
| R2-E2 | low | Reject the added post-sign serialization for this edge. The signature escape expansion can cross the exact encoding limit. `Encode` refuses that envelope, and the case requires a record within a few escaped signature characters of one MiB. |
| R2-E3 | medium | Patch. Distinct malformed UTF-16 dictionary names can normalize to duplicate decoded JSON names. The typed encoding path must reject the same ambiguity as the verifier. |
| R2-E4 | medium | Defer with R2-B6. A covered DR outage may already be beyond its four-hour notification bound when recovery first enters. |
| R2-E5 | low | Defer with attempt lifecycle timing. The phase deadline can precede the recorded first mutation. Lifecycle checks must distinguish first rollout deadlines from later separately recorded recovery phases. |
| R2-E6 | medium | Defer with R2-B5. Recording a first ordinary phase with an unchanged lifetime can bypass the existing deadline-plus-grace check. |
| R2-V1 | medium | Patch. Isolate a signed active-attempt revision changing only the newly bound operations definitions digest. |
| R2-V2 | medium | Patch. Exercise disaster-recovery phase progress with its fixed notification deadline and refuse a revision extending it. |
| R2-V3 | medium | Patch. Exercise each exact latest-working reference independently while its writer, outcome and epoch are valid. |

The resumed review added stable disposed-key and duplicate-property failures and strengthened the signature and transition regression cases. Final Debug compilation completed with zero warnings or errors. All 208 release-record cases passed, including every approved matrix scenario. [Final record tests](../../artifacts/story-4-6-review-patches/final-record-tests.xml) · [Final build](../../artifacts/story-4-6-review-patches/final-build.log)

The full module suite finished with 3,239 passed, zero failed and one skipped. The skipped case requires the controlled catalog SDK interception harness. The run used a rebuilt Platform composition and a local MSBuild version override of `6.6.2` within the committed Platform support range. [Module results](../../artifacts/story-4-6-review-patches/final-module-tests.xml) · [Module log](../../artifacts/story-4-6-review-patches/final-module-tests.log)

## Design Notes

One schema resource contains eight branches with distinct versioned schema identifiers. Keep Platform records separate from Builds package-release receipts. Reuse Tooling without extending the two-package publication pipeline. Signature and transition checks alone cannot establish a committed store write.

## Verification

From the owning Builds repository:

- `dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1` — build with existing warning gates.
- `dotnet test test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug --no-build` — existing and new cases pass; use built-assembly xUnit class filters for focused triage.
- `git diff --check -- schemas src/libraries/Hexalith.Builds.Tooling/ReleaseRecords src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj test/Hexalith.Builds.Module.Tests/ReleaseRecords ReleaseRecords.md` — changed files have no whitespace errors.
