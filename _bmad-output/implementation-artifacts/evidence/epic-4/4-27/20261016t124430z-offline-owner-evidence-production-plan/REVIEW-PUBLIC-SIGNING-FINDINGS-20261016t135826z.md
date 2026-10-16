# Independent review of public signing findings

Final handoff:
[PUBLIC-SIGNING-FINDINGS-20261016t135826z-v2.md](PUBLIC-SIGNING-FINDINGS-20261016t135826z-v2.md).
Exact reviewed final document: 20,982 bytes, SHA-256
`501c30d4f3dc33a0a1440af6a5926e3df8db99414d660c6dcb95eeec2df0510b`.
This digest identifies document bytes; it establishes no signer or owner authority.
The [initial findings](PUBLIC-SIGNING-FINDINGS-20261016t135826z.md) remain
unchanged as drafting/review history. Neither version replaces PLAN-v3.md or
any earlier handoff, review, packet or evidence original.

**Independent documentation review: PASS after corrections.** All five initial
findings were addressed in create-only v2, and the same independent reviewer
confirmed no concrete contradiction introduced by the amendments. No open
concrete document finding or deferred review item remains. External trust,
current private-key availability/custody, format policy, witness authority,
revocation/freshness and production-tooling prerequisites remain unresolved.
This result authenticates no human, key mandate, grant, certificate, readiness
or operation.

## Method and triage

The bmad-build workflow was rendered exactly once successfully and its relevant
clarify, investigation, template and one-shot implementation/review instructions
were read. The user explicitly scoped the result to new findings and independent
review, so no equivalent general plan/spec, source implementation, existing-file
append, state promotion, operational checkpoint, staging or commit was made.
The documentation goal had no unresolved intent gap or irreversible action.
The only active configured review layer was Blind Hunter; no configured layer
was skipped. Runtime/test/historical-validation layers were inapplicable to
this new documentation-only result and were not run.

Reviewer `/root/public_signing_findings_review` was started context-free at the
parent session's model capability. It read the new draft directly and permitted
handoff/runbook excerpts, with the original public-inspection observations as
source-comparison evidence. It neither reopened private material nor repeated
key/signature parsing, historical verification, old evidence searches or tests.
Its first review arithmetic was
`N = min(floor(sqrt(18,502 / 1000) + 1), 10) = 5` and it returned five findings.
The parent evaluated every finding before patching; none was silently dropped.

| ID | Verified finding / harm | Verdict and route | Create-only correction and independent disposition |
| --- | --- | --- | --- |
| B1 | Witness, independent verifier and relying verifier were not fully distinguished in the decision sequence, risking acceptance of a merely nominated witness. | medium / patch | v2 defines W as witness/independent verifier and R as relying human reviewer; W may equal R, requiring no extra person. Otherwise R must already authenticate/accept W's basis and channel before the encounter. First decision and next step now preserve that order. Reviewer confirms addressed, v2 lines 182–202 and 240–255. |
| B2 | The observation's retained fields did not explicitly include the exact observed key bytes and fingerprint, risking an ambiguous later key binding. | medium / patch | v2 expressly requires both in the observation itself alongside purpose, project, method, identities and independence. Reviewer confirms addressed, lines 204–214. |
| B3 | A separate possession challenge under an unspecified purpose conflicted with the narrow format-contract signing designation. | medium / patch | v2 places a future fresh witness challenge inside the independently reviewed exact format-contract payload. Its original contract signature can prove control after external designation and separate signing authorization; arbitrary challenge signatures are not authorized. No new purpose or signing action was created. Reviewer confirms addressed, lines 216–229. |
| B4 | Unqualified wording about needing no new keypair could imply the private counterpart was currently available and suitable. | low / patch | v2 limits the claim to assessing/designating the public candidate. Future reuse is conditional; availability, custody, backup/recovery and compromise status are unassessed. No private inspection is requested. Reviewer confirms addressed, lines 164–172. |
| B5 | Plain source filenames made the conclusions harder to trace. | low / patch | v2 links all four source handoffs, the runbook's relevant sections and PLAN-v3's I1 section. Simple documentation correction. Reviewer confirms addressed, lines 36–49 and 146. |

The follow-up reviewed the amended v2 content and dispositions rather than
replaying unchanged validation. Its exact conclusion was that all five findings
were addressed and no concrete contradiction introduced by the amendments was
found. Both reviews are agent documentation reviews; they are not the independent
human trust designation/review required by the proposed bootstrap.

## New-document checks and boundaries

Only the new v2 was checked for whitespace/final newline and source-link target
resolution: PASS. The companion review link was intentionally pending this
create-only record at that check; this file now supplies that target. The final
digest above was computed once for the new v2 and is not a trust attestation.
No test, old preservation check, historical signature verification, preflight or
verifier was run. An initial local read command failed because `python` was
unavailable; it performed no reads or writes, and the intended reads were made
with installed `python3`. No dependency installation/update was attempted.

Final parent clock-tool observation: **2026-10-16T14:05:04Z**.
Reviewer observations: **2026-10-16T14:00:54Z** for the first review and
**2026-10-16T14:04:34Z** for the correction review. All are strictly before the
unchanged **2026-10-16T16:27:56Z** ceiling. These are document/review observations,
not replacement preparation, capture, acceptance or approval dates. Preparation
remains **2026-10-15T16:27:56Z**, RID `20261015t162756z`. At or after the ceiling,
all candidate-dependent work stops; only historical assessment may continue
with that stop recorded. No replacement recovery was allocated and no old
expired recovery was revived.

The exact verifier's historical **exit 1** at
`2026-10-16T11:25:36.164697Z`, reason
`offline-revision-integrity-or-custody-refusal`, remains unchanged and unrerun.
Final sealed SHA-256 remains
`c71f38517e02ea5bc7fa176cfb1abe8f12b43b499fbe6f74fbdc339d91f0b23d`.
The sealed concurrent exception remains unchanged, including its recorded
`f6c25046d9db40c069cb183cf87f8fef19a0a952b601c87dbe690d0b0aff7e43`
binding, and the additional Story 1.8 observation remains
`c94f8fedcc4d9e362985c3056c2a2b3e6628ef74b525ed377b92f7cf920a12c5`.
No preservation disposition or new baseline is created.

Both preparation directories, immutable packets, signed originals, original
modes, expired recovery and deadline, failures, concurrent changes, index and
gitlinks received no writes from this task. No complete preservation audit or
revalidation of historical acceptance is claimed. No authentication,
network/API/provider/IAM request, SSH-agent access, credential/private-key read,
key generation, signing, publication, infrastructure/operational action,
staging, commit, push or dependency update occurred. Scripts in
NEXT-OPERATOR-ACTION.md remained unexecuted documentation. The write footprint
is the new findings drafts and this review record, plus the local workflow
renderer cache.

The first necessary owner decision is the actual independent W/R assignment
and pre-existing identity/project-ownership basis/channel that R can accept.
The concrete next task is that conditional offline comparison and narrow
existing-key designation, not supplying a personal certificate, reconstructing
public-key history, unlocking a key or accepting an agent's authority. No
operational approval question is emitted.

**Spec-23 draft; Story 3.6/Epic 3/Platform 4.27 in progress;
4.1 operational gate closed; release FAIL.** Existing state files are untouched.
