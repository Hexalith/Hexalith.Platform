# Pragmatism review — Platform architecture update 4

**Verdict: PASS WITH MINOR EDITORIAL FIXES.** No critical, high or medium findings. The update adds binding distinctions needed by independently implemented runners, evidence consumers, deployment workflows and recovery executors. Three low-severity findings below improve scanning or remove exact restatement; none reopens an accepted decision or weakens a gate.

- Target: `ARCHITECTURE-SPINE.md`, 499 lines, 17,350 words; SHA-256 `62527dfbf0ce7732780a7c2a5ede7e29e162cf4386d9881e6696cbdad24a95ce`.
- Comparison: the `spine_before` snapshot recorded by `update-inputs.json`, 16,003 words; delta **+1,347 words**. The comparison used the current spine, including gate fixes, rather than the possibly older `spine.diff`.
- Scope: update-4 additions and modified rows only; the inherited document's overall size is not a finding.
- Constraint: preserve every accepted AD, product requirement and owned gate, including the chosen retained-data repair rule: valid baseline compatibility evidence or a named data restore.
- Action: review artifact only; no spine, source or memlog changes.

## PR4-P1 — Split the amended timing row into seven short rows

**Severity:** low. **Disposition:** autofix at finalization. **Evidence:** line 291, `Timing and interruption`; prior PR3-13 deferred its reflow until the next amendment, which this update has made.

The row is now 427 words. It mixes the common grace, three attempt kinds, recovery phase deadlines, lifetime precedence, resumption and interruption. A builder trying to find the recovery lifetime exception must scan the whole cell. The distinction between an absolute recovery lifetime and shorter phase deadlines is particularly easy to miss.

Replace only that table row with the following seven rows. These keep every body sentence unchanged and in its existing order. The first row keeps the canonical `Timing and interruption` name, so the Terms and FR-7/FR-8 traceability references still point to the beginning of the timing contract. There is no semantic or body-word change; the longest resulting body is 103 words.

```markdown
| Timing and interruption | Every attempt records at lock acquisition its kind, one attempt grace no longer than the check timeout plus 30 seconds, used for in-flight retries, interruption detection and takeover, and a pre-mutation bound that serves as its maximum lifetime until later deadlines replace it. |
| Release-kind timing | **Release-kind** attempts — releases, rollbacks, configuration-only changes, rotation rollouts, catalog activations, realm-contract and operator-artifact changes and baseline re-deploys — record t0 at their first mutation (catalog preparation for a release) and the rollout deadline t0 + 10 minutes. |
| Infrastructure-kind timing | **Infrastructure-kind** attempts — environment-layer, profile and shared-infrastructure changes — record at entry the maximum duration their named workflow declares for the infrastructure phase, including any custodian unseal, and each working-release re-verification records a readiness deadline of phase start plus 10 minutes; a shared-infrastructure change records one attempt per environment, each on that environment's executor. |
| Recovery-kind lifetime | **Recovery-kind** attempts — in-place data restore and DR — record entry time, any original outage timestamp and an absolute maximum-lifetime deadline: for DR when the outage began within coverage, outage time plus four hours; for DR otherwise, entry time plus the last drill's measured duration, or plus four hours before the first drill; for an approved release's named recovery, entry time plus the duration its Administrator record states; for a staging reset, entry time plus the declared staging bound. That lifetime notifies and never aborts; a re-entry after it has passed records entry plus the declared bound and keeps the original outage time. |
| Phase deadlines | Each recovery phase records its deadline when it starts: recovery-release readiness at phase start plus 10 minutes, and the verification window once every hook whose module's reopening waits has completed. Release-kind and infrastructure-kind maximum lifetimes derive from their applicable phase deadlines and grace; recovery-kind phase deadlines do not replace their recorded maximum-lifetime deadline. |
| Attempt resumption | A replacement job resuming the same attempt inherits its recorded kind, timing values, grace and consumed recovery allowance, and records the new lock epoch; any other takeover records the superseded attempt's terminal outcome as superseded and records its own values by kind. Resuming the same attempt never restarts its timers; an observation gap invalidates the window. A retry completing within the grace is the check's latest result. |
| Interruption outcome | An interruption of a release-kind attempt detected before the verification deadline plus the grace fails it, and it may still use its one recovery; an interrupted recovery-kind attempt fails without automatic recovery, and Administrator or the deputy re-enters. Later detection, record/cluster disagreement, an unknown actual release or lock owner, or an unreadable record stops for intervention without mutation. Restarts never add a recovery. |
```

**Preservation check:** joining the seven proposed row bodies with single spaces reproduces the current row body byte for byte. This preserves the timeout-plus-30-second grace, pre-mutation lifetime, all ten-minute deadlines, outage-versus-entry anchors, four-hour coverage rule, named/staging bounds, non-aborting notification, re-entry behavior, hook-dependent verification start, lifetime precedence, inherited timing and consumed recovery allowance, new epoch, superseded outcome, observation gaps, retry results and every interruption failure path. This is the deferred PR3-13 action fulfilled at its specified trigger.

## PR4-P2 — Point the roles summary to the reopening rule

**Severity:** low. **Disposition:** autofix. **Evidence:** line 37, Roles, repeats the ingress restriction bound in Recovery sequence step 7 (line 329).

- Replace: `Reopening restores only the ingress state recorded before the incident; before G2, user ingress stays closed.`
- With: `Reopening follows Recovery sequence step 7.`

The roles summary still states the deputy's authority to verify restoration and reopen service. The authoritative step retains the recorded pre-incident ingress restriction, the pre-G2 closure, the staging-reset variant and the requirement to keep the promotion stop set. The attempt record continues to carry pre-incident ingress state at line 231. This removes 10 words of duplicate rule text and leaves a direct pointer at the role description. Keep the preceding explicit Administrator-absence limitation: it is useful role-boundary information.

## PR4-P3 — Remove the immediate restatement of the retained-data requirement

**Severity:** low. **Disposition:** autofix. **Evidence:** line 288, Empty or degraded production.

Delete only: `Fresh-install proof never waives that retained-data requirement.`

Its preceding sentence already says: “Any retained-data repair still requires precondition 9: valid compatibility evidence or the named data-restore recovery in Release modes.” That complete rule, the first-empty-install exception immediately before it, Release modes and precondition 9 remain intact. The deleted sentence adds no further condition. Saving: 7 words.

## Content considered and retained

| Updated content | Why it belongs |
| --- | --- |
| AD-4 candidate mapping and AD-5 evidence | Distinct duties: choose the candidate's committed build for every consumed artifact, then prove which identities were actually loaded. Combining them would obscure the producer/consumer boundary. |
| AD-10 outcome, attachments and stop/cleanup | Failure retention, finite holds, withdrawn outcomes and owner cancellation are separate lifecycle decisions. The split bullets give them usable homes. No environment service or other extra mechanism is introduced. |
| Candidate/attachment policy and runner acceptance rows in Owned work | The former fixes policy before consumers implement it; the latter names executable acceptance scenarios and known brownfield constraints. Keep the owner, deadline and test cases even where they cite the AD. |
| Release modes, Empty or degraded production, precondition 9 and degraded-attempt acceptance | These serve procedure selection, exceptional execution, the pre-mutation gate and proof obligations. Keep the compatibility-or-named-restore rule visible at those boundaries. PR4-P3 removes only the adjacent duplicate sentence. |
| Diagnostics and notification versus Promotion stop | The monitor owns detection and comparison cadence; Promotion stop owns causes, revision and clearance. Keeping the mismatch requirement at both seams makes responsibilities explicit. |
| Recovery sequence confirmation fixes | In-place versus DR behavior, credential and admission reconciliation, phase start and recorded ingress reopening prevent incompatible recovery implementations. They are decisions, not removable rationale. |
| New G1, G2 and G3 Owned work rows | Each prevents a consumer from choosing a shared policy independently and supplies an owner plus an admission boundary. These gates should not move to unowned Deferred work. |
| Catalog activation exceptions and the compatible-deployment diagram caption | They prevent the normal-path diagram and generic catalog convention from imposing automatic rollback preparation on approved empty/degraded or named-recovery attempts. |

No wholesale rewrite, new mechanism, inherited-rule trim or acceptance-gate removal is recommended. The two optional text trims remove 17 words; the timing change is a reflow with unchanged rule text.
