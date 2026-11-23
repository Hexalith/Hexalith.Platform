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

## Implementation Notes

The reviewed Release build completed with zero warnings and errors. The credentialed suite passed all 24 cases, including the signed acquisition race and entitlement probes against the Scaleway verification prefix. The test run covered concurrent acquisition, stale epoch and linked takeover, replay and unproven writes, staging-only stop and clear, and the production reservation versus staging supersession race. [Hosted evidence](evidence/story-4-7/scaleway-staging-store.md)

The CI-mode assembly-reference build and all 23 deterministic cases passed against both a local publish of the pinned Builds Tooling source at `4adecfe2af4758aa66039a1221de20774ca56e87` and the published 6.7.0 assembly. The 6.7.0 package passed NuGet signature and SHA-512 verification, its repository metadata matched release revision `763fc0e937413cb44e533391e573c43640263b40`, and the Tooling assembly SHA-256 was `79ff793b6ec2796bc0098ea39457d1a01583b40f7b1eb11815cb127f6bc8f775`. The tuple profile selects those 23 cases. Platform's full tuple gate still requires adoption of the 6.7.0 release and its catalog. [Builds 6.7.0](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v6.7.0)

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

## Design Notes

Head `environments/{env}/head.json`; records at `environments/{env}/records/{kind}/{recordId}/{digest}.json`. A record orphaned by a lost head write is unreferenced and harmless.

```json
{"schema":"hexalith.attempt-store.head.v1","environment":"staging","revision":42,"previous":"sha256:…",
 "epoch":7,"holder":"att-…","attempts":{"att-…":{"digest":"sha256:…","terminal":false}},
 "stop":{"revision":3,"recordId":"stop-…","digest":"sha256:…"},
 "decisions":{"dec-…":{"state":"reserved","reservation":{"recordId":"…","digest":"…"}}}}
```

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Platform.AttemptStore.Tests/Hexalith.Platform.AttemptStore.Tests.csproj -c Release -m:1 -nodeReuse:false` -- zero warnings or errors.
- `dotnet exec tests/Hexalith.Platform.AttemptStore.Tests/bin/Release/net11.0/Hexalith.Platform.AttemptStore.Tests.dll` -- all pass; the live suite reports whether it ran.
- `git diff --check` -- clean.
