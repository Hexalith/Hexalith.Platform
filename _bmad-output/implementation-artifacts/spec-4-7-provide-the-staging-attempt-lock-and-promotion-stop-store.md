---
title: '4.7 Provide the staging attempt, lock and promotion-stop store'
type: 'feature'
created: '2026-11-23'
status: 'ready-for-dev'
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
- [ ] `src/Hexalith.Platform.AttemptStore/` -- new library in the slnx: head model, object-store port (read with ETag, create-only, replace-if-match), S3 backend, and acquire, revise, takeover, latest-working move, stop-set, Administrator-clear and v2 decision transitions.
- [ ] `Directory.Packages.props`, `Directory.Build.targets` -- pin the S3 client; require the published Tooling assembly for the new project in CI.
- [ ] `tests/Hexalith.Platform.AttemptStore.Tests/` -- in-memory backend covering every matrix row with real ephemeral keys, a parallel-writer race on one head, and a credential-gated live suite on the hosted verification prefix.
- [ ] Hosting -- provision the project, bucket, staging-writer and reader applications and bucket policy; probe denials; and keep a short sanitized summary in `_bmad-output/implementation-artifacts/evidence/story-4-7/`.
- [ ] `docs/contracts/attempt-store-v1.md` -- layout, transitions, failure codes, IAM model and consumer obligations.

**Acceptance Criteria:**
- Given the hosted store, when probed live, then it is outside the cluster and executor hosts, two concurrent acquisitions commit exactly one, and unconditional writes, deletes and the staging writer's access to another environment's prefix are denied.

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
