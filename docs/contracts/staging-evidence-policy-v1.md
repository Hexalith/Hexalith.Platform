# Staging evidence policy v1

Policy identifier: `hexalith.platform.staging-evidence-policy.v1`.
Published: 2026-10-30. This immutable version records the staging decisions
confirmed by the user on 2026-10-30 for Story 5.1 (FR-6, AR-57). MUST and MUST NOT
express requirements for downstream adoption.

Platform owns this policy. The accountable policy owner is the Platform
Maintainer, `@jpiquot`, under the [repository ownership policy](../../.github/CODEOWNERS).
Builds owns implementation in its tools, validators and reusable workflows;
Platform owns the staging executor and promotion evaluator. Modules own their
critical flows and required checks. Administrator and EventStore qualify the
reset and recovery behavior with Platform. Publication records the approved
Platform decisions; it does not record Builds adoption, module-owner approvals,
reset qualification, runtime enforcement or release authorization.

The governing constraints are [FR-6](../../_bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/prd.md),
[Story 5.1 and its consumers](../../_bmad-output/planning-artifacts/epics.md#story-51-decide-the-staging-evidence-policies),
and the architecture's [staging gate, staging reset, recovery and timing rules](../../_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md).
The [Story 5.1 specification](../../_bmad-output/implementation-artifacts/spec-5-1-decide-the-staging-evidence-policies.md)
records the confirmed choices. The separate [candidate evidence and attachment
policy v1](candidate-and-attachment-policy-v1.md) governs FR-4 candidate evidence
and attachment holds; it does not decide staging freshness or reset semantics.

## Evidence and identity boundaries

Both automatic and Administrator-approved promotion MUST require complete,
passing critical-flow E2E evidence against the exact retained release deployed
in staging with real services. Every included module, including unchanged,
infrastructure and tool modules, MUST supply a non-empty critical-flow declaration
with a required E2E mapping for every flow. Missing or invalid declarations,
unresolved mappings, empty required sets and any failed, skipped, incomplete or
absent required result refuse promotion. Isolated tests, integration tests and
health checks alone cannot satisfy this gate. Administrator approval cannot
waive these requirements.

Evidence MUST bind the policy identifier, its pending promotion decision, its
producing run and attempt, staging environment and lock epoch, and the exact
retained release record and artifacts actually served. Version labels alone are
insufficient: the immutable Helm package and image content identities promoted
MUST be those qualified. Record declared and actually executed suite, runtime
profile and configuration digests, the module declaration and flow-to-check
mapping digests, the non-empty required check set and its individual completion
results. Expected and observed identities MUST agree. The applicable production
working baseline MUST name the release and working attempt it represents;
absence of a baseline MUST be explicit rather than filled with an invented one.

At promotion admission, the evaluator MUST compare these bindings with the
requested release, current checked inputs and current production baseline.
Missing, unknown or mismatched bindings refuse admission, even when an outcome
says passed. Staging and production profile digests MUST agree, and effective
infrastructure inputs MUST remain within their qualified sets. Evidence-validated
EventStore server packages and the existing provenance requirements remain
prerequisites. No evidence grants publication or promotion authority by itself.

## Five policy decisions

### 1. Maximum evidence age: 24 hours

The maximum age is **86,400 seconds (24 hours) from staging attempt start**.
The executor MUST record the absolute UTC start when the serialized attempt
acquires its staging lock and epoch, before preparation, deployment or candidate
writes. Queue time is not attempt start. Completion, a check retry, job resumption
or report publication MUST NOT restart the age clock.

Record the immutable deadline as `expiresAt = attemptStartedAt + 86400 seconds`.
At promotion admission, using the evaluator's UTC time `admissionAt`, freshness
requires `attemptStartedAt <= admissionAt < expiresAt`. The deadline is exclusive:
evidence is stale at exactly `expiresAt`. Admission MUST also require the complete
passing outcome and all identity and branch checks in this policy. A request or
approval made before expiry cannot reserve freshness for a later admission.

Missing, malformed or future start/completion timestamps, completion before
attempt start, an inconsistent expiry, stale evidence or a binding mismatch MUST be refused with
the reason recorded. Finishing within 24 hours is insufficient if admission takes
place at or after the deadline. Expiry does not erase the attempt or its recovery
point and does not abort a running restoration.

### 2. Attempt triggers: intake and checked-input changes

Staging MUST automatically queue an attempt when a retained candidate passes
intake validation, or when an input checked by the staging gate changes for a
pending candidate. Checked inputs include the retained release, required suites,
critical-flow declarations and mappings, runtime profile, configuration and the
applicable production working baseline. A newly established baseline is also a
baseline change. A changed input invalidates earlier evidence for the new input
set and requires a fresh complete attempt, even if module versions are unchanged.

The Platform Maintainer may explicitly request a fresh attempt after evidence
expires or an attempt fails; the request MUST record the reason. Version 1 has
**no periodic retry loop**: expiry or failure alone does not authorize recurring
reruns. Automatic trigger requests and manual rerun requests enter the same
serialized staging queue and cannot bypass an outstanding reset or active lock.

### 3. Rerun acceptance: latest issued attempt, complete pass

The trusted gate MUST bind each pending promotion decision to its exact retained
release and policy. Each new staging attempt has a distinct run/attempt identity
and a recorded gate-issued order; retries of that pending decision retain its
decision identity and record the rerun reason and preceding attempt references.
Changed targets or new promotion decisions require new decision identities and
fresh evidence, even when their bytes match an earlier decision.
At admission, the evaluator MUST verify the request's current authoritative state
is pending. Closed, completed or cancelled requests MUST NOT reopen under their
old identity; a reopened or new request requires its own identity and fresh full
evidence.

Only the **latest issued serialized staging evidence attempt** for that pending
decision may qualify. Issuance order, rather than completion time, determines
which attempt supersedes the others. A newer queued, running, failed, cancelled
or incomplete attempt prevents fallback to an earlier pass. A qualifying rerun
MUST rerun the full required suite for every included module and pass every
required check under the same current bindings and applicable preparation branch.
Partial passes MUST NOT be aggregated across attempts, and evidence MUST NOT be
reused across decisions, including results for unchanged modules.

Keep every earlier failed, incomplete and other terminal outcome, its original
identities, timestamps, diagnostics and rerun reason visible in the decision's
history. A later pass may qualify its own attempt; it MUST NOT overwrite earlier
outcomes or present them as passed. In-flight check retries allowed by the
architecture's grace remain within their attempt and do not authorize collecting
passes from different staging attempts. A separate reset is recovery evidence,
not a passing E2E rerun.

### 4. Critical-flow removal or remap: recorded owner and Platform approval

Before a removed or remapped flow can join the gate, the owning module and
Platform MUST approve a review record binding the before and after declaration
digests and flow-to-check mapping digests, the affected flows and checks, and the
coverage rationale. The record MUST identify each approving owner and the
approval for those exact inputs. Reviewers and the validator MUST verify the
actual transition from the authoritative accepted predecessor declarations and
mappings, including applicable production-baseline inputs, to the candidate.
A caller-selected before/after pair MUST NOT substitute for that transition or
conceal a removal. A changed reviewed input requires a review of
the changed inputs; a matching digest by itself is never proof that reduced
coverage is acceptable. Pending, missing or inapplicable approvals refuse the
change and its evidence. This publication supplies no module-owner approval.

Tenants MUST also review changes to tenant-lifecycle flows with the owning module
and Platform. Those flows remain limited to staging-only admission and run-scoped
synthetic tenants with the immutable, attested synthetic-creation marker. Leftover
synthetic tenants MUST be cleaned up or reported with their creating run. These
review requirements do not implement tenant behavior. Production smoke remains
its own non-empty suite and MUST NOT create or delete tenants; staging E2E is not
a substitute for production smoke.

### 5. Staging reset lifetime: 60 minutes, notify without aborting

When an unadopted candidate wrote state that production's working baseline cannot
read, staging MUST complete a separate in-place data-restore attempt before a
later candidate's staging attempt starts. The restore uses the recovery point cut
at the start of the unadopted candidate's attempt, before candidate writes. Keep
that point and its retained-artifact references while any pending reset or
recovery references it, regardless of evidence expiry. Additive unadopted
candidates need no reset. A later candidate's start makes an earlier unadopted
candidate no longer adoptable unless an Administrator record reserves it.

The reset is a recovery-kind attempt with its own identity, staging lock, epoch,
recovery entry time and lineage to the candidate attempt and recovery point.
Record `resetLifetimeDeadline = recoveryEntryAt + 3600 seconds`: the declared
maximum lifetime is **3,600 seconds (60 minutes) from recovery entry**, not from
the candidate attempt, queue request or a later recovery phase. The independent
off-site monitor MUST read the deadline from the durable attempt record. At or
after this deadline, if the reset remains non-terminal, it MUST notify
**Administrator and the Platform Maintainer**, even if the executor has died,
and record the exceeded bound and notification. Lifetime
expiry MUST NOT abort restoration, release the lock or permit a later candidate
to start. Recovery phase deadlines remain separate and do not replace this
maximum-lifetime deadline.

A replacement job may resume the same attempt only from the same workflow,
within its recorded grace and under the architecture's permitted takeover and
fencing conditions. It MUST retain the original entry, deadline, grace and
consumed recovery allowance, recording the new lock epoch.
It MUST NOT restart timers. An observation gap invalidates verification. An
interrupted recovery-kind attempt fails without automatic recovery; Administrator
or the deputy re-enters the data restore from the same recovery point under the
architectural authority and fencing rules. A re-entry is a distinct attempt;
after the prior lifetime has passed it records its new entry plus the declared
3,600-second bound, while preserving the original attempt, point, outage time
when applicable, and notification history. Re-entry never relabels the interrupted
attempt as successful or gives the same attempt a new deadline.

Reset verification MUST pass before later candidate attempts begin. The staging
realm is regenerated from its realm contract, and the later candidate's
environment-layer attempt removes objects declared only by the reset candidate.
Missing points, failed or incomplete restores and failed verification keep later
candidates blocked; an elapsed lifetime is not verification. Staging failures
block their candidate; reset and failure handling affect only staging's stop and
MUST NOT set or clear production's stop. A verified reset is not production
promotion evidence and cannot clear a production stop.

## Serialization and preparation branches

One serialized staging attempt MUST hold the environment lock and monotonic epoch
across preparation, deployment and evidence collection, and cut its own recovery
point before candidate writes. A reset is a separate attempt, never an overlapping
phase of the later candidate. Neither a trigger, rerun nor age limit weakens lock,
fencing, recovery-point or real-service requirements.

| Applicable state and path | Required preparation and evidence |
| --- | --- |
| No production working baseline and no retained application data | Rehearse a fresh install and every required candidate E2E flow; explicitly record no baseline and prepare no nonexistent rollback target or set |
| Administrator-approved empty or degraded production | Rehearse a fresh install plus the candidate and every required E2E flow; whenever application data remain, additionally require valid compatibility evidence or the qualified named data recovery under the existing release rules |
| Production working baseline with compatibility eligibility | Start from that exact baseline, upgrade to the exact candidate and rehearse the prepared AD-15 rollback set with HotReload off; exercise a candidate-new idempotent command and verify baseline code reads candidate state and events |
| Administrator-approved retained-data path without valid safe compatibility | Rehearse the named data recovery with retained candidate/baseline artifacts, its recovery point, exact procedure, authority, verification and lost-window treatment; prepare no automatic rollback set |

A failed rollback rehearsal is breaking compatibility evidence, not a staging
E2E failure. It refuses the automatic compatible path; an approved retained-data
path still needs full passing E2E and its separately qualified named recovery.
Fresh-install-plus-candidate proof also serves the existing Administrator-approved
empty/degraded path; it never replaces compatibility or named-recovery
qualification when application data remain. These preparation requirements do
not change release authority. Catalog, key and secret retirements MUST remain
expand-only relative to the current
working baseline. A baseline change requires fresh applicable evidence; an
Administrator approval never waives it.

## Decision accountability and revisit conditions

| Decision | Accountable owner and adoption responsibility | Rationale | Concrete revisit trigger |
| --- | --- | --- | --- |
| Evidence age: 86,400 seconds from attempt start, exclusive admission deadline | Platform Maintainer, `@jpiquot`; Builds and Platform implement and qualify the gate | Bound drift between the tested state and promotion without renewing age on completion | Observed freshness failures or qualified suite/queue costs make the bound unsuitable |
| Automatic intake/input-change queueing; explicit expiry/failure reruns; no periodic loop | Platform Maintainer, `@jpiquot`; Builds and Platform implement intake and queueing | Exercise changed release inputs while preventing uncontrolled repeated attempts | Intake or checked-input/baseline lifecycle changes leave a trigger ambiguous or missing |
| Latest issued attempt, full passing rerun, preserved history and no cross-decision reuse | Platform Maintainer, `@jpiquot`; Builds and Platform implement ordering and evidence lineage | A later partial or failed attempt cannot be hidden by an earlier pass | Qualification or operation exposes rerun ordering, history or decision-lineage ambiguity |
| Owning-module and Platform approval of removal/remap; Tenants reviews lifecycle changes | Platform Maintainer, `@jpiquot`, accountable; modules own flow rationale and approvals; Builds implements validation | Content agreement alone cannot justify lost critical-flow coverage | An owner review gap, missed lifecycle impact or accepted coverage reduction escapes review |
| Reset lifetime: 3,600 seconds from recovery entry; notify without aborting | Platform Maintainer, `@jpiquot`; Builds and Platform implement; Administrator and EventStore qualify reset/recovery behavior | Detect unexpectedly long restores while preserving restoration and serialized staging | A rehearsal or real restore exceeds the bound, or interruption/re-entry cannot preserve its lineage |

A revisit trigger requires a new explicit Platform decision, not an exception to
v1. Published identifiers and their meaning MUST remain immutable. Any changed
rule requires a new version and identifier, recorded Platform ownership, explicit
Builds and Platform consumer adoption and requalification of affected paths.
Evidence MUST remain labelled with the version under which it was produced;
consumers MUST NOT relabel it, select an unspecified latest policy, silently fall
back or reinterpret old evidence under another version. Missing, unknown or
mismatched policy identifiers refuse evidence acceptance.

## Downstream adoption and qualification

These are semantic handoff requirements, not new schema fields, CLI options or
implemented workflow support. Builds and Platform MUST record adoption against
exact tool/workflow and evaluator identities, with Platform acceptance and
applicable integration qualification, before consumers accept evidence under
this policy. A configured pin or this publication alone is insufficient.

| Consumer | Required adoption and qualification |
| --- | --- |
| Story 5.3 | Validate every included module's non-empty flows, complete required E2E mappings and separate non-empty smoke declaration; verify exact removal/remap review bindings and approvals |
| Story 5.4 | Obtain Tenants and owning-module lifecycle reviews; qualify synthetic-tenant cleanup or creating-run reports without treating this policy as approval |
| Story 5.6 | Implement intake/input-change triggers, serialization, pre-write recovery points, start/expiry recording, exact input identities, full required E2E and durable ordered attempt/rerun history |
| Story 5.7 | Qualify explicitly labelled no-baseline fresh-install proof without a rollback target |
| Story 5.8 | Qualify current-baseline compatibility and rollback rehearsal, preserving breaking rehearsal evidence |
| Story 5.9 | Qualify the approved retained-data path's named recovery, required authority and verification without an automatic rollback set |
| Story 5.10 | Qualify separate resets, referenced-point retention, verified restoration, realm regeneration, candidate-only object removal and 3,600-second notification/resumption/re-entry behavior |
| Story 5.11 | Enforce policy/decision/release/input/baseline bindings, exclusive age admission, latest-attempt full-pass acceptance, all preparation branches and both release modes; qualify refusal reasons |

Only the existing Platform publication workflow may publish an attested McpCli
candidate as stable after all applicable staging validation passes. The policy
does not change release authority, runners, schemas, workflows, tool pins,
submodules, vulnerability policy or tenant implementation. Vulnerability policy
and its scan gate remain separately owned by Stories 5.2 and 5.5.

## Acceptance and refusal examples

| Situation | Required disposition |
| --- | --- |
| Attempt starts 2026-10-30 12:00:00 UTC; admission is 2026-10-31 11:59:59 UTC | Fresh, eligible only if the latest attempt fully passed with all exact bindings and applicable branch proof |
| Same attempt; admission is 2026-10-31 12:00:00 UTC or later | Refuse as stale, including when approval or completion occurred earlier |
| Start/completion timestamp is missing, future or inconsistent, or reported expiry exceeds start plus 86,400 seconds | Refuse with the timing reason; do not infer a timestamp or extend freshness |
| Suite, profile, configuration, flow mapping, retained release or production baseline changes after a pass | Refuse the old binding and automatically queue a fresh complete serialized attempt for the changed inputs |
| Evidence expires or the attempt fails with no input change | Platform Maintainer may request a fresh attempt with its reason; preserve the prior result and do not start a periodic retry loop |
| Attempt 1 passes, then attempt 2 is issued and is queued, running, failed or incomplete | Refuse fallback to attempt 1, regardless of finish order; retain both attempts |
| Latest attempt reruns every required flow and passes after an earlier failure | Eligible for that pending decision subject to current freshness, exact bindings and branch proof; earlier failure and rerun reason remain visible |
| Two attempts pass different subsets, or an unchanged module offers another decision's pass | Refuse aggregation or reuse; require a full passing attempt for this decision |
| A flow removal/remap has matching digests but no owning-module or Platform coverage approval | Refuse; require both approvals of before/after digests, affected flows/checks and coverage rationale; lifecycle changes also need Tenants review |
| Reset enters at 12:00 UTC and remains non-terminal at 13:00 UTC | Notify Administrator and Platform Maintainer, continue restoration and keep later candidates blocked pending verified reset |
| Same reset resumes at 12:30 UTC; interrupted restore re-enters after 13:00 UTC | Resume retains its 13:00 deadline; authorized re-entry is a new attempt with entry plus 3,600 seconds, the same recovery point and preserved prior lineage |
| Restore finishes but verification fails, or pruning sees a pending reset reference | Keep later candidates blocked; retain the point and failed evidence rather than treating elapsed time or restore completion as verification |
| Administrator approves stale evidence or a retained-data path supplies only fresh-install proof | Refuse; approval cannot waive freshness or the applicable recovery qualification |

These examples are downstream qualification obligations. Scoped documentation
checks establish consistency and discoverability only; no workload, deployment,
remote publication, implemented gate, owner flow approval or live reset
qualification is claimed by this record.
