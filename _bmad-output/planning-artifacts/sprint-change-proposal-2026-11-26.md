# Sprint Change Proposal: currency-check trigger wording (2026-11-26)

**Scope:** Minor, wording only. **Mode:** batch, direct edit approved by Administrator in chat (sole owner). **Route:** applied directly; no backlog change.

## 1. Issue

On 2026-11-26 Administrator decided that the `production-attempt` currency trigger means admission of a production release promotion, automatic or Administrator-approved. Separately admitted rollbacks, configuration-only changes, rotations, catalog activations outside a release, baseline re-deploys, and infrastructure-kind or recovery-kind attempts are outside it. The check runs at release admission, at every monthly drill and on a weekly schedule. Only a failed release-admission check blocks automatic promotion. Drill and weekly results are recorded but add no attempt block and set no promotion stop. A weekly failure, or a known end of life within 30 days, notifies Administrator.

[Policy v1](../../docs/contracts/shared-infrastructure-currency-policy-v1.md#2-cadence-and-triggers) decision 2 records this decision and is frozen at Platform `8c79c18874a3a767d60be63c2ce4020a73991f96`. The planning sources still said "each/every production attempt" and gave drill and weekly results no exemption from the block. The 9.1 code review deferred this wording fix (`deferred-work.md`).

## 2. Impact

- **Epic 9 only.** Story 9.1 is done; only its AC wording changes. Story 9.2 (blocked, draft) gets its statement and ACs aligned with its draft spec. Other epics, the story count, the numbering, the sequencing order and sprint-status are unchanged.
- **Artifacts:** PRD, architecture spine, epics, sequencing. There are no UX, code, CI or infrastructure changes. Policy v1 is not edited.
- **Reviewed, no change:** generic statements such as "a failed currency check blocks automatic promotion" (PRD:182, 309, 398, 474; addendum:199, 212; spine:286; epics:105, 202; SPEC.md, glossary, acceptance-criteria, success-measures). They name no cadence and read as the promotion's own admission check, so they stay as written.
- **Stale:** `epic-9-context.md` is left for the next bmad-build to recompile.

## 3. Approach

Direct adjustment: low effort, low risk, no timeline impact. Rollback and MVP review are not applicable.

## 4. Changes

| # | Location | Old | New |
|---|---|---|---|
| 1 | `prd.md` Remaining work row (line 428) | "Check at each production attempt, at the monthly drill and weekly." / "Failure blocks automatic promotion." | "Check at admission of each production release promotion (automatic or Administrator-approved), at every monthly drill and weekly; policy v1 decision 2 lists the attempts outside this trigger." / "A failed release-admission check blocks automatic promotion. Monthly-drill and weekly results add no attempt block and set no promotion stop; a weekly failure, or a known end of life within 30 days, notifies Administrator." |
| 2 | Spine G3 row (line 564) | "Check each production attempt, monthly drill and weekly schedule. A failure blocks automatic promotion." | "Check at admission of each production release promotion, automatic or Administrator-approved, at every monthly drill and on a weekly schedule; other attempt kinds are outside the trigger (policy v1 decision 2). Only a failed release-admission check blocks automatic promotion. Monthly-drill and weekly results are recorded, add no attempt block and set no promotion stop; a weekly failure, or a known end of life within 30 days, notifies Administrator." |
| 3 | Spine Production profile row (line 252). Added during the sweep: this row is the source that AR-31 copies. | "checked at each production attempt and monthly drill" | "checked at production release admission, at every monthly drill and weekly (G3)" |
| 4 | `epics.md` AR-31 (line 448) | "checked at each production attempt and monthly drill" | "checked at production release admission, at every monthly drill and weekly" |
| 5 | Story 9.1 AC | "the check still runs at every production attempt and every monthly drill" | "the check still runs at admission of every production release attempt, at every monthly drill and on a weekly schedule" |
| 6 | Story 9.2 statement | "at every production attempt and every drill" | "at every production release admission, every monthly drill and on a weekly schedule" |
| 7 | Story 9.2 AC 1 | "Given a production attempt or a monthly drill … each deployed shared component, including Rancher, required agents, management-cluster distribution and backup operator, with …" | "Given admission of a production release attempt, a monthly drill or the weekly schedule … each deployed shared component, including Rancher, required agents, the management-cluster distribution and the backup operator, and each environment-layer provider with …". The wording comes from the 9.2 draft spec. Environment-layer providers are already in the policy v1 decision 1 inventory. |
| 8 | Story 9.2 AC 2 | "Given a failed check / When an automatic promotion is evaluated … And approved attempts follow the policy from Story 9.1" | "Given a failed check at release admission / When automatic promotion is evaluated … And an approved attempt proceeds only if its Administrator record names every failing component" |
| 9 | Story 9.2 AC 3 (from the 9.2 draft spec) | none | "Given a failed monthly-drill or weekly check / When the result is recorded / Then it adds no attempt block and sets no promotion stop / And a weekly failure, or a known end of life within 30 days, notifies Administrator" |
| 10 | `sequencing.md` line 79 | "checks at every production attempt, every monthly drill and on a weekly schedule" | "checks at admission of every production release attempt, every monthly drill and on a weekly schedule" |

## 5. Handoff

- Minor scope; applied in this session. Sprint-status is not refreshed because no keys or statuses change. The 2-11/2-12 keys and the 4-28 status stay as they are.
- The `deferred-work.md` entry from the 9.1 code review (2026-11-26) is marked `status: resolved`.
- Success criterion: none of the edited planning sources still uses "each/every production attempt" as the currency trigger, and every block statement that names a cadence is limited to release admission.
