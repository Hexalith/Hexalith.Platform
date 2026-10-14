---
title: 'Prepare the supported-cluster dependency for Timesheets staging'
type: 'feature'
created: '2026-10-14'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '416878a74975b1023b3a00ab5d35aad4ce6365d9'
context:
  - /home/administrator/projects/hexalith/timesheets/AGENTS.md
  - /home/administrator/projects/hexalith/timesheets/references/Hexalith.AI.Tools/hexalith-llm-instructions.md
  - /home/administrator/projects/hexalith/timesheets/references/Hexalith.AI.Tools/hexalith-git-instructions.md
---

<frozen-after-approval reason="user authorized reversible implementation and validation; infrastructure execution requires separate approval">

## Intent

**Problem:** Timesheets has a Development/source binding but no staging enrollment. Native reads confirm v1.34.9, absent staging namespaces and no Gateway API. Staging requires retirement, supported upgrade, private Rancher/access and runner relocation. The upgrade preparer emits an unspecified single hop and omits retirement ordering.

**Approach:** Extend the existing upgrade preparer with the retained two-hop selection, separate closed gates and retirement/post-retirement recovery requirements. Retain a command proposal and local preparation receipt. Actual infrastructure and Timesheets deployed acceptance remain blocked.

## Boundaries & Constraints

**Always:** Preserve historical `MAINTENANCE.md` and Timesheets `plan.json` bytes. Outputs stay owner-only, create-only, checksummed, unsigned, outside Git and mutation unauthorized. Status/signature presence proves no acceptance. Hash target/procedure lineage; dated targets require fresh currency/compatibility checks and gates at each hop. Preserve Timesheets deny/unavailable providers; Story 3.6 stays open/FAIL pending production bindings, certified migration and deployed subscriber/shared-rebuild proof.

**Never:** Execute infrastructure mutations, sign, publish, change runtime/domain state, initialize dependencies or mutate Git. No approved successor maintenance procedure before retirement/fresh census. No invented release identities/workflow.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Normal preparation | Required local source files | Both 1.34 to 1.35.9 and 1.35 to 1.36.5 records, gates closed, no operational commands executed | Source absence refuses before publishing an attempt |
| Claimed acceptance | Stories marked done; present passing records/signature files | Retirement, recovery, currency and procedure acceptance remain unvalidated and mutation unauthorized | Never infer trust or freshness from status or presence |
| Per-hop execution evidence absent | Both selected hops are pending | Second hop explicitly requires accepted first-hop outcome and newly refreshed recovery/preflight/approval | No automatic chained upgrade |
| Existing custody | Reused ID or output inside repository/recovery bundle | Refuse and preserve inputs/previous output | Existing safety behavior and private modes remain |

</frozen-after-approval>

## Code Map

- `eng/kubernetes-upgrade/prepare.py`, `test_prepare.py`: existing recorder/custody and safety coverage; reuse and extend.
- `_bmad-output/implementation-artifacts/4-1-upgrade-the-cluster-off-kubernetes-1-34-after-verified-backu.md`: retained target/retirement decision.
- `_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261004t095137z-target-review/target-review.json`: retained target identities; hash as dated lineage.
- `eng/cluster-management/README.md`: 4.27 → 4.1 → 4.28 plus 4.2/4.3 gates.
- `eng/timesheets-deployment/README.md`, `plan.json`, `local-host-binding-qualification.md`: Development binding and missing Release identities.

## Tasks & Acceptance

**Execution:**
- [x] `eng/kubernetes-upgrade/prepare.py` -- emit both dated hops, hashed lineage, closed gates, retirement/post-retirement recovery/procedure conditions and second-hop first-outcome prerequisite.
- [x] `eng/kubernetes-upgrade/test_prepare.py` -- cover every matrix row.
- [x] `eng/kubernetes-upgrade/README.md` -- explain changed output/limits.
- [x] `eng/timesheets-deployment/staging-dependency-handoff.md` -- native findings, Release prerequisites, exact local command, proposed sequential kubeadm commands/approval gates and next retirement workflow entry; no invented Helm command.
- [x] `eng/timesheets-deployment/README.md` -- link handoff.
- [x] Run preparer; verify modes/hashes/gates; retain sanitized receipt in handoff/spec.

**Acceptance Criteria:**
- Given the retained target decision, when preparation runs, then every selected hop has its own exact target and closed gate with source hashes and refresh requirements, while actual source and final versions remain unknown.
- Given unaccepted retirement and old backups, when the plan is read, then accepted 4.27/native access, fresh post-retirement recovery and an exact approved successor maintenance procedure are explicit blockers before any hop.
- Given missing Release artifacts and hosted providers, when the handoff is read, then Timesheets deployment remains unavailable and Story 3.6 acceptance remains FAIL.

## Implementation Notes

The user preauthorized reversible implementation/validation before deployment approval. Platform began clean on main; preserve the unrelated outer Works change.


Implemented both retained selected hops with independent closed gates, source
snapshot/hash consistency, retirement/post-retirement recovery and procedure
requirements. Review corrections reject duplicate/noncanonical/future/overflow
review input before publication, add explicit pending health/outcome bindings,
and close exact-patch test coverage. All four matrix rows ran and passed in
17 focused tests. Final local attempt `20261014t070550z-preparation` exited 0;
16 unsigned records, 17 private files, both gates closed, all checksums and 13
source hashes independently verified. Historical maintenance/plan bytes remain.

The unchanged enrollment suite exited 1 with 31 setup errors from absent
Platform-local nested Timesheets source. No nested input was initialized, no
test weakened, and no passing enrollment result is claimed. This spec closes
bounded preparation implementation only; Story 4.1 and Timesheets Story 3.6
remain open, with deployed acceptance/release FAIL. One pre-existing immutable
source-archive concern is deferred. No staging, commit, branch or push was
performed under the repository's preservation baseline.

During the run, the enclosing Timesheets repository advanced externally to `6514f38b383512c9e392f7fb20d52856a91fbff0`; that commit only records the existing Works gitlink change. Platform HEAD remains `416878a74975b1023b3a00ab5d35aad4ce6365d9`. Neither change was made by this implementation.

## Spec Change Log

## Review Triage Log

| Finding | Verdict / route | Evidence |
| --- | --- | --- |
| Blind 1: shared operational templates prevent two retained hop packets | false / reject | Both gates independently expose pending freshPrerequisiteRecordHashes; hop 2 additionally requires new recovery/preflight/approval and accepted first-hop SHA. Global operational files are explicitly not-run local placeholders, not an execution packet or a writer that replaces first-hop proof. Actual operational evidence requires separate signed records. |
| Blind 2: protected-workload health missing from refresh checks | medium / patch | The generated condition list has recovery/compatibility/storage but no explicit authenticated pre-upgrade health condition. The second gate inherits this omission; add an unrun health condition and pending record binding. |
| Blind 3: pending outcome lacks gate/procedure/postflight binding | low / patch | New per-hop outcome objects identify the target/attempt but have no explicit pending approved-gate, successor-procedure or postflight digest slots. Add null slots so the handoff records these unavailable prerequisites. |
| Blind 4: leading-zero patch accepted | low / patch | Numeric regex accepts v1.35.09; canonical exact release identity should refuse it before output. Direct regex correction and refusal cases suffice. |
| Blind 5: duplicate JSON keys silently select last value | medium / patch | json.loads accepts conflicting tag keys and emits the last tag as an exact decision. Reject duplicate review keys and verify no attempt publication. |
| Blind 6: future review date presented as historical lineage | low / patch | checkedAt has timezone validation only; 2999 date produces an apparent prior review. Compare with captured preparation time and refuse before publication. |
| Blind 7: source snapshots discarded after hashing | medium / defer | Baseline recorder already retained only source hashes, without immutable source bytes or canonical VCS identity. The new snapshot fixes selection/hash consistency but not archive availability. Retained source archive binding remains a pre-existing operational audit concern; local records stay unauthorized. |
| Edge 1: leading-zero patch accepted | low / patch | Same demonstrated regex defect as Blind 4; retain this separate finding and fix the shared cause. |
| Edge 2: UTC conversion overflows | medium / patch | timestamp catches ValueError only; 0001-01-01T00:00:00+01:00 raises OverflowError through the new review-date input and bypasses CLI controlled refusal. Catch overflow as an invalid timestamp. |
| Verification 1: exact-patch validation coverage gap | medium / patch | The reviewer loosened the numeric patch pattern in memory: all 14 tests still passed and v1.35.* was published. Add malformed correct-minor cases for both selection fields and retain no-publication assertions. |

## Verification

- `python3 -m unittest discover -s eng/kubernetes-upgrade -p 'test_*.py'` -- all relevant checks pass.
- `python3 -m unittest discover -s eng/timesheets-deployment -p 'test_*.py'` -- existing enrollment safety checks pass.
- `python3 eng/kubernetes-upgrade/prepare.py --operator 'Codex (preparation only)'` -- new private attempt; both gates closed, no operational command run; inspect modes/checksums and source lineage.
- `git diff --check` -- no whitespace errors. Recheck both historical artifact hashes and owner-runtime diff exclusions.
