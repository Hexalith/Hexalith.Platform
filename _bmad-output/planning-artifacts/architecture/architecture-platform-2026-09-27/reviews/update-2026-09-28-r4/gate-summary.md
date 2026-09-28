# Platform architecture update 4 — 2026-09-28

**PASS after corrections.** The spine is final. AD-1–AD-15 remain stable. This establishes document readiness; runtime implementation and production qualification remain subject to the spine's owned gates.

## Decisions carried into the spine

- AD-4/AD-5 bind CI acceptance to the candidate's committed Release artifacts and the content identities actually loaded, including the composed host and McpCli. Baseline-only runs cannot pass candidate acceptance.
- AD-10 defines bounded attachment holds, withdrawn outcomes, retention after any local run failure, owner completion/cancellation, and attachment within one CI job.
- Production rules adopt the expanded degraded definition, two-way admission checks, per-cause stop clearance and recurrence, Administrator reviews, deputy limits, and post-cut grant reporting.
- The user selected valid compatibility evidence or a named data-restore recovery for degraded production with retained data. First installation without retained application data uses fresh-install evidence. Exception paths do not require running baseline hosts or automatic rollback preparation.
- Recovery wording consistently distinguishes automatic rollback, baseline re-deploy, in-place data restore and replacement-capacity recovery. Timing and takeover rules preserve recorded deadlines and authority fencing.
- Restore and backup monitoring honor keys destroyed by a proven erasure; unknown lineage or unexplained missing keys still fail closed.

## Review evidence and disposition

| Review | Result and resolution |
| --- | --- |
| [Run-3 confirmation](confirm-r3.md) | Four wording groups corrected: recovery path/owner, timer scope, takeover acceptance after fencing, and admission-drift notification. |
| [PRD reconciliation](reconcile-prd.md) | Final recheck passes P1–P5; the one architecture choice is resolved. Shared policy deferrals retain owners and consumer gates. |
| [Rubric](review-rubric.md) | Two medium findings corrected: baseline preparation applies only to eligible paths, and stop records are explicitly per environment. |
| [Reality](review-reality.md) | Pass; zero new findings. Existing version seeds are verified and new behavior is identified as target work. |
| [Adversarial](review-adversarial.md) | Two high findings corrected: first-install bootstrap and degraded retry no longer depend on a nonexistent live baseline. |
| [Security](review-security.md) | Pass; zero findings. |
| [Recovery](review-recovery.md) | One high finding corrected in both restoration and recovery-point monitoring: intentional key destruction must preserve erasure and recovery usability. |
| [Pragmatism](review-pragmatism.md) | Three low editorial fixes applied. Timing is split into seven rows with byte-for-byte preservation of the concatenated rule bodies; two duplicate statements were removed. |
| [Final confirmation](review-confirm.md) | Pass; no remaining defects in the requested scope, including the final timing reflow. |

## Handoff

The PRD and addendum now record architecture adoption and the selected compatibility/recovery rule. Their requirement IDs and numeric targets are preserved. Historical validation reports remain historical evidence.

Candidate revision selection, the decision CI evidence gates, evidence reuse and the attachment hold limit remain one shared Platform/Builds policy before consumers implement acceptance. Monitor bounds, currency details, tenant-lifecycle qualification and reduced-recovery operations retain their stated gates. No missing evidence counts as a pass.

Next: use `bmad-spec update` to synchronize `spec-platform` and its acceptance, sequencing and success-measure companions before recovery or deployment stories are finalized. Module source-document overrides retain their existing owners.

Mechanical evidence is in [document-checks.json](document-checks.json) and [lint-final.json](lint-final.json). The update-only architecture diff is [spine.diff](spine.diff); [source-alignment.diff](source-alignment.diff) records the narrow PRD/addendum synchronization.
