# Staging evidence policy v1

Policy identifier: `hexalith.platform.staging-evidence-policy.v1`.
Published: 2026-10-30. The Story 5.1 review corrections on that date preceded
any adoption. Version 1 is fixed at the Platform revision completing Story 5.1
(FR-6, AR-57). It records the decisions confirmed by the Platform Maintainer,
`@jpiquot`, the review-adopted decisions and clarifications within those decisions.
MUST and MUST NOT express requirements for downstream adoption.

Platform owns this policy. The accountable policy owner is the Platform
Maintainer, `@jpiquot`, under the [repository ownership policy](../../.github/CODEOWNERS).
Builds owns implementation in its tools, validators and reusable workflows;
Platform owns the staging executor and promotion evaluator. Modules own their
critical flows and required checks. Administrator and EventStore qualify the
reset and recovery behavior with Platform. One identity may approve in each role
it holds, provided the review record names every role in which it approves;
this applies to removal/remap approvals, including Platform approval given by the
Platform Maintainer, and to Tenants review.
Publication records the approved Platform decisions; it does not record Builds
adoption, module-owner approvals, reset qualification, runtime enforcement or
release authorization.

The governing constraints are [FR-6](../../_bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/prd.md),
[Story 5.1 and its consumers](../../_bmad-output/planning-artifacts/epics.md#story-51-decide-the-staging-evidence-policies),
and the architecture's [staging gate, staging reset, recovery and timing rules](../../_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md).
The [Story 5.1 specification](../../_bmad-output/implementation-artifacts/spec-5-1-decide-the-staging-evidence-policies.md)
records the confirmed choices. The separate [candidate evidence and attachment
policy v1](candidate-and-attachment-policy-v1.md) governs FR-4 candidate evidence
and attachment holds; it does not decide staging freshness or reset semantics.
This policy's references to the PRD, epics, architecture and Story 5.1
specification resolve at the same Platform revision that fixes version 1.

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
MUST be those qualified. Evidence MUST record declared and actually executed
suite, runtime profile and configuration digests, the module declaration and
flow-to-check mapping digests, the non-empty required check set and its individual completion
results. Evidence MUST also bind the applicable preparation branch and the
branch-determining content of any Administrator approval selecting it, as defined
in decision 2. Expected and observed identities MUST agree. The applicable production
working baseline MUST name the release and working attempt it represents;
absence of a baseline MUST be explicit rather than filled with an invented one.

Promotion admission is the production attempt's precondition 4 check, performed
while it holds the production lock and new epoch and before any mutation, as
required by architecture production preconditions 1 and 4. `admissionAt` is the
time of that check; an earlier evaluation or queued request reserves nothing.
At that check, the evaluator MUST compare these bindings with the requested
release, current checked inputs and current production baseline.
Missing, unknown or mismatched bindings refuse admission, even when an outcome
says passed. A superseded decision is a promotion decision closed under
decision 5 because a later candidate's staging attempt started without an
Administrator reservation; it is no longer adoptable and MUST be refused,
even if its earlier evidence otherwise passes. Staging and production profile
digests MUST agree, and effective infrastructure inputs MUST remain within their
qualified sets. Evidence-validated
EventStore server packages and the existing provenance requirements remain
prerequisites. No evidence grants publication or promotion authority by itself.

## Five policy decisions

### 1. Maximum evidence age: 24 hours

The maximum age is **86,400 seconds (24 hours) from staging attempt start**.
The executor MUST record the absolute UTC start when the serialized attempt
acquires its staging lock and epoch, before preparation, deployment or candidate
writes. Queue time is not attempt start. The executor MUST also record the
attempt's absolute UTC completion time when it reaches its terminal outcome.
Completion, a check retry, job resumption or report publication MUST NOT restart
the age clock.

The executor MUST record the immutable deadline as
`expiresAt = attemptStartedAt + 86400 seconds`.
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
intake validation and opens a new pending decision under decision 3, or when an
input checked by the staging gate changes for a pending candidate. Re-intake of a
release whose decision is still pending opens no decision and queues no attempt by
itself; checked-input changes still queue attempts under that decision. Checked
inputs include required suites, critical-flow declarations and mappings, runtime
profile, configuration, the applicable production working baseline and
preparation branch, including the branch-determining content of the
Administrator approval selecting that branch: the selected path and, for the
retained-data path, the named recovery's identity, acceptance checks and maximum
duration. A new per-attempt Administrator record that repeats that content is no
checked-input change. A newly established baseline is also a baseline
change. A changed input invalidates earlier evidence for the new input set and
requires a fresh complete attempt, even if module versions are unchanged.
A different retained release requires a new decision under decision 3.

For a currently pending promotion decision, the Platform Maintainer may explicitly
request a fresh attempt after evidence expires or the latest E2E attempt reaches
a non-passing terminal outcome: failed, cancelled, incomplete, interrupted or
superseded. A superseded attempt is one taken over by another attempt whose writer
recorded the earlier attempt's terminal outcome as superseded (architecture
Attempt resumption); it is non-passing and may leave its decision pending. The
request MUST record the reason. Maintainer requests for completed or superseded decisions,
or in any other state, including a fresh pass or a queued or running latest
attempt, MUST be refused with the reason recorded. Version 1 has **no periodic retry loop**:
expiry or a non-passing outcome alone does not authorize recurring reruns.
Automatic triggers apply only while the candidate's decision remains
pending; a superseded decision receives no automatic triggers, including baseline
changes. Automatic trigger requests and manual rerun requests enter the same
serialized staging queue and cannot bypass an outstanding reset or active lock.
An attempt whose decision is no longer pending when it reaches the staging lock
MUST be cancelled with the reason recorded; it never starts and supersedes nothing.

### 3. Rerun acceptance: latest issued attempt, complete pass

When a retained candidate passes intake, the trusted gate MUST open a pending
promotion decision bound to that exact retained release and policy unless that
release already has a pending one. At most one decision per retained release may be pending
at a time; re-intake while one is pending opens no new decision and queues no
attempt by itself. Intake of the release that production's latest-working pointer
currently names opens no decision and queues no attempt. Checked-input
and baseline changes queue fresh attempts under that decision. Only a different
retained release is a changed target; switching to the Administrator-approved
retained-data or empty/degraded path keeps the same decision. Each new staging
attempt has a distinct run/attempt identity and a recorded gate-issued order;
retries of that pending decision retain its decision identity and record the rerun reason and
preceding attempt references. A changed target or new promotion decision requires
a new decision identity and fresh evidence, even when its bytes match an earlier
decision.

A decision closes only as completed, when its release becomes production's
working baseline, or as superseded. The trusted gate MUST record completed closure
when production's latest-working pointer names the decision's release, before that
baseline change queues any automatic trigger. A non-working production release outcome leaves
the decision pending, and every production release attempt for that decision MUST
repeat promotion admission. Production recovery attempts follow the architecture's
separate recovery rules.
Version 1 defines no other closure. At admission, the evaluator MUST verify the
decision's current authoritative state is pending. Completed or superseded
decisions MUST NOT reopen under their old identity; a reopened or new decision
requires its own identity and fresh full evidence. A release whose decision was
completed or superseded may return only under a new decision with fresh evidence,
and only when the latest-working pointer does not name it.

Only the **latest issued serialized staging evidence attempt** for that pending
decision may qualify. Issuance order, rather than completion time, selects the
latest attempt; that selection does not change any earlier attempt's recorded
outcome or label it superseded. A newer queued, running, failed, cancelled,
incomplete, interrupted or superseded attempt prevents fallback to an earlier pass. A
qualifying rerun MUST rerun the full required suite for every included module and
pass every required check under the same current bindings and applicable
preparation branch.
Partial passes MUST NOT be aggregated across attempts, and evidence MUST NOT be
reused across decisions, including results for unchanged modules.

The decision history MUST keep every earlier failed, incomplete, superseded and other
terminal outcome, its original identities, timestamps, diagnostics and rerun
reason visible. A later pass may qualify its own attempt; it MUST NOT overwrite
earlier outcomes or present them as passed. In-flight check retries allowed by the
architecture's grace remain within their attempt and do not authorize collecting
passes from different staging attempts. Separate resets and named-recovery
qualifications are recovery evidence: neither counts as nor supersedes the latest
E2E evidence attempt, and neither is a passing E2E rerun.

### 4. Critical-flow removal or remap: recorded owner and Platform approval

Removal means omitting a previously declared critical flow or making its required
coverage optional. Remap means changing a flow's identity or its required
flow-to-check associations, including a rename, split or check replacement.
If unchanged coverage cannot be established, the transition MUST be treated as a
removal or remap requiring review. Adding a new flow, or adding a required check
to a flow while keeping all its existing required associations, is neither a
removal nor a remap; Tenants review of every lifecycle flow still applies.

Before a candidate declaration containing a removal or remap can join the gate,
the owning module and Platform MUST approve a review record binding the before
and after declaration digests and flow-to-check mapping digests, the affected
flows and checks, and the coverage rationale. Removal or remap of a tenant-lifecycle
flow MUST additionally obtain Tenants approval in that record. The record MUST
identify each approving owner and the approval for those exact inputs. Reviewers and the
validator MUST compare the candidate against both the latest authoritative
accepted declarations/mappings and, when present, the production-baseline
declarations/mappings. Each predecessor transition that removes or remaps a flow
requires exact before/after review bindings and approvals; approval covering
only one transition does not cover the other. When the latest-accepted and
production-baseline declaration and mapping digests are identical, one review
record binding those identical before digests and the candidate after digests
covers both transitions. A caller-selected predecessor MUST NOT substitute for
either comparison or conceal a removal. A changed reviewed input
requires review of the changed inputs; a matching digest by itself is never
proof that reduced coverage is acceptable. Pending, missing or inapplicable
approvals refuse the change and its evidence. This publication supplies no
module-owner approval.

Any flow that creates or deletes tenants is a tenant-lifecycle flow. Tenants MUST
review every tenant-lifecycle flow with the owning module before enrollment and
before any changed declaration joins the gate. E2E tenant creation or deletion
outside a reviewed tenant-lifecycle flow MUST be refused.
Every such review MUST record the Tenants reviewer identity and the
exact declaration and flow-to-check mapping digests; a change to either input
requires renewed review for the changed inputs before gate enrollment.
Those flows remain limited to staging-only admission and run-scoped
synthetic tenants with the immutable, attested synthetic-creation marker. Leftover
synthetic tenants MUST be cleaned up or reported with their creating run. These
review requirements do not implement tenant behavior. Production smoke remains
its own non-empty suite and MUST NOT create or delete tenants; staging E2E is not
a substitute for production smoke.

### 5. Staging reset lifetime: 60 minutes, notify without aborting

When an unadopted candidate wrote state that production's working baseline cannot
read, staging MUST complete a separate in-place data-restore attempt before a
later candidate's staging attempt starts. Without a production working baseline,
or when its readability of the candidate's writes is unproven, those writes MUST
be treated as unreadable and require the same reset. A same-candidate rerun after breaking
or unproven-readability writes MUST also wait for a verified reset first.
Staging MUST issue the required initial reset automatically ahead of the blocked
later-candidate or same-candidate attempt; automatic issuance covers only that
initial reset.

The restore uses the earliest recovery point cut for that candidate since the
last verified reset, before any of that candidate's writes, rather than a later
rerun's point that already contains its writes. That point and its retained
artifacts MUST be kept while the unadopted candidate can still require a reset,
or while an open recovery, a pending reset or a pending named recovery references
them, regardless of evidence expiry. Closed lineage references in attempt history
do not by themselves require retention. Additive unadopted candidates whose
writes are proven baseline-readable need no reset. A later candidate's start makes an earlier
unadopted candidate no longer adoptable unless an Administrator record reserves
it. Without that reservation the trusted gate MUST close the earlier decision as
superseded, refuse its promotion admission and stop all automatic triggers for it.

The reset is a recovery-kind attempt with its own identity, staging lock, epoch,
recovery entry time and lineage to the candidate attempt and recovery point.
For the initial entry, the executor MUST record
`resetLifetimeDeadline = recoveryEntryAt + 3600 seconds`;
re-entry follows the deadline inheritance rule below. The declared maximum
lifetime is **3,600 seconds (60 minutes) from recovery entry**, not from
the candidate attempt, queue request or a later recovery phase. The independent
off-site monitor MUST read the deadline from the durable attempt record. At or
after this deadline, if the reset remains non-terminal, it MUST notify
**Administrator, the recovery deputy and the Platform Maintainer**, even if the
executor has died, and record the exceeded bound and notification in that
notification issue. It MUST use the architecture's
single accepted notification path: a GitHub issue in the private notification
repository with the operations repository's writer set, raised through an
issues-only credential, assigned to Administrator and mentioning the recovery
deputy and the Platform Maintainer. The notification MUST carry the
environment, release identity, outcome status and opaque references to
access-controlled evidence. Lifetime expiry MUST NOT abort restoration, release
the lock or permit a later candidate
to start. Recovery phase deadlines remain separate and do not replace this
maximum-lifetime deadline.

A staging reset MUST NOT resume in a replacement job. Executor loss interrupts
the recovery-kind attempt without automatic recovery: the lock stays held and the
attempt record stays non-terminal, so the maximum-lifetime notification still
applies, until an Administrator or recovery-deputy re-entry takes it over and
records its terminal outcome as superseded (architecture Attempt ownership,
Attempt resumption, and Binding classes and records). That re-entry runs the data
restore from the same recovery point under the architectural authority and
fencing rules. An observation gap, a failed or incomplete restore or failed
verification leaves the reset unverified and likewise requires Administrator or
the deputy to re-enter from the same recovery point under the deadline
inheritance rule below.

A re-entry is a distinct attempt. Before the prior maximum-lifetime deadline,
it inherits that deadline; at or after the prior deadline it records its new
entry plus the declared 3,600-second bound. Re-entry MUST preserve the original
attempt, recovery point, outage time when applicable and notification history.
Re-entry never relabels the interrupted or unverified attempt as successful or
gives the same attempt a new deadline. Architectural grace and recovery-phase timers remain
separate and MUST follow their existing timing rules.

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

Compatibility eligibility follows architecture production precondition 9:
effective change classification together with the staging rehearsal against the
recorded production working baseline.

| Applicable state and path | Required preparation and evidence |
| --- | --- |
| No production working baseline and no retained application data | Rehearse a fresh install and every required candidate E2E flow; explicitly record no baseline and prepare no nonexistent rollback target or set |
| Administrator-approved empty or degraded production | Rehearse a fresh install plus the candidate and every required E2E flow; whenever application data remain, additionally require valid compatibility evidence or the qualified named data recovery under the existing release rules |
| Production working baseline with compatibility eligibility | Start from that exact baseline, upgrade to the exact candidate and rehearse the prepared AD-15 rollback set with HotReload off; exercise a candidate-new idempotent command and verify baseline code reads candidate state and events |
| Administrator-approved retained-data path without valid safe compatibility | Rehearse the named data recovery with retained candidate/baseline artifacts, its recovery point, exact procedure, authority, verification and lost-window treatment; prepare no automatic rollback set |

If no preparation row applies, or proof required by an applicable row is missing,
promotion admission MUST be refused. For example, a baseline without valid
compatibility evidence and without Administrator approval of the retained-data
path cannot qualify.

A failed rollback rehearsal is breaking compatibility evidence, not a staging
E2E failure. It refuses the automatic compatible path; an approved retained-data
path still needs full passing E2E and its separately qualified named recovery.
Fresh-install-plus-candidate proof also serves the existing Administrator-approved
empty/degraded path; it never replaces compatibility or named-recovery
qualification when application data remain. These preparation requirements do
not change release authority. Staging catalog, key and secret retirements MUST
follow AD-15's expand-only rule relative to production's working baseline. A
baseline change requires fresh applicable evidence; an Administrator approval
never waives it.

## Decision accountability and revisit conditions

| Decision | Accountable owner and adoption responsibility | Rationale | Concrete revisit trigger |
| --- | --- | --- | --- |
| Evidence age: 86,400 seconds from attempt start, exclusive admission deadline | Platform Maintainer, `@jpiquot`; Builds and Platform implement and qualify the gate | Bound drift between the tested state and promotion without renewing age on completion | Observed freshness failures or qualified suite/queue costs make the bound unsuitable |
| Automatic intake/input-change queueing; explicit reruns after expiry or a non-passing terminal outcome; no periodic loop | Platform Maintainer, `@jpiquot`; Builds and Platform implement intake and queueing | Exercise changed release inputs while preventing uncontrolled repeated attempts | Intake or checked-input/baseline lifecycle changes, or a trigger is ambiguous or missing |
| Latest issued attempt, full passing rerun, preserved history and no cross-decision reuse | Platform Maintainer, `@jpiquot`; Builds and Platform implement ordering and evidence lineage | A later partial or failed attempt cannot be hidden by an earlier pass | Qualification or operation exposes rerun ordering, history or decision-lineage ambiguity |
| Owning-module and Platform approval of removal/remap; Tenants reviews every lifecycle flow before enrollment and before any changed declaration joins the gate | Platform Maintainer, `@jpiquot`, accountable; modules own flow rationale and approvals; Tenants reviews every lifecycle flow and approves lifecycle-flow removal/remap; Builds implements validation | Content agreement alone cannot justify lost critical-flow coverage | An owner review gap, missed lifecycle impact or accepted coverage reduction escapes review |
| Reset lifetime: 3,600 seconds from recovery entry; notify without aborting | Platform Maintainer, `@jpiquot`; Builds and Platform implement; Administrator and EventStore qualify reset/recovery behavior | Detect unexpectedly long restores while preserving restoration and serialized staging | A rehearsal or real restore exceeds the bound, or interruption/re-entry cannot preserve its lineage |

A revisit trigger requires a new explicit Platform decision, not an exception to
v1. From the Platform revision completing Story 5.1, this published identifier
and its meaning MUST remain immutable. Any changed rule requires a new version
and identifier, recorded Platform ownership, explicit Builds and Platform
consumer adoption and requalification of affected paths.
Evidence MUST remain labelled with the version under which it was produced;
consumers MUST NOT relabel it, select an unspecified latest policy, silently fall
back or reinterpret old evidence under another version. Missing, unknown or
mismatched policy identifiers refuse evidence acceptance.

## Downstream adoption and qualification

These are semantic handoff requirements, not new schema fields, CLI options or
implemented workflow support. Builds and Platform MUST record adoption against
the exact Platform revision of this policy text and exact tool/workflow and
evaluator identities, with Platform acceptance and applicable integration
qualification, before consumers accept evidence under
this policy. A configured pin or this publication alone is insufficient.

| Consumer | Required adoption and qualification |
| --- | --- |
| Story 5.3 | Validate every included module's non-empty flows, complete required E2E mappings and separate non-empty smoke declaration; verify removal/remap review bindings and owning-module, Platform and applicable Tenants approvals for each latest-accepted and applicable production-baseline transition |
| Story 5.4 | Obtain Tenants and owning-module review of every lifecycle flow before enrollment and before any changed declaration joins the gate; refuse E2E tenant creation/deletion outside reviewed lifecycle flows and qualify synthetic-tenant cleanup or creating-run reports without treating this policy as approval |
| Story 5.6 | Implement decision creation and intake/input-change triggers only for pending decisions, with no attempt queued by re-intake alone and no decision or attempt for intake of the release the latest-working pointer currently names; cancellation with a recorded reason of any attempt whose decision is no longer pending when it reaches the staging lock, so it never starts and supersedes nothing; completed closure when production's latest-working pointer names the release, before that baseline change queues triggers; manual request authorization only for currently pending decisions in the permitted expiry/non-passing states (including superseded E2E attempts), reason recording and refusal for completed/superseded decisions or other states, serialization, pre-write recovery points, start/completion/expiry recording, exact input identities including the preparation branch and its selecting approval's branch-determining content, full required E2E and durable ordered attempt/rerun history; completed-closure qualification requires Story 6.16's latest-working pointer move |
| Story 5.7 | Qualify explicitly labelled no-baseline fresh-install proof without a rollback target |
| Story 5.8 | Qualify current-baseline compatibility and rollback rehearsal, preserving breaking rehearsal evidence |
| Story 5.9 | Qualify the approved retained-data path's named recovery, required authority and verification without an automatic rollback set |
| Story 5.10 | Qualify separate resets for unreadable or unproven-readability writes, including same-candidate reruns; retain the earliest pre-write point since the last verified reset, verify restoration, regenerate the realm, remove candidate-only objects, close superseded decisions and qualify 3,600-second notification/interruption/re-entry behavior; notification qualification requires Story 6.4's off-site monitor, deputy re-entry qualification requires Story 6.3's recovery authority, and re-entry fencing qualification requires Story 6.15's takeover fencing |
| Story 5.11 | Enforce policy/decision/release/input/baseline bindings and current pending/adoptable state, refusing superseded decisions; qualify exclusive age admission, latest E2E-attempt full-pass acceptance, recovery-evidence separation, all preparation branches including default refusal, both release modes and refusal reasons; qualify that only the Platform publication workflow publishes an attested McpCli candidate as stable after all applicable staging evidence passes, and that failed, stale or mismatched evidence publishes no stable version; admission qualification requires Story 6.14's precondition 4 check under the production lock and new epoch |
| Story 6.4 | For staging resets, the off-site monitor notifies at the recorded maximum-lifetime deadline through the accepted notification path and mentions the Platform Maintainer as well as the deputy |

Only the architecture's Platform publication workflow may publish an attested McpCli
candidate as stable after all applicable staging validation passes. The policy
does not change release authority, runners, schemas, workflows, tool pins,
submodules, vulnerability policy or tenant implementation. Vulnerability policy
and its scan gate remain separately owned by Stories 5.2 and 5.5.

## Acceptance and refusal examples

| Situation | Required disposition |
| --- | --- |
| Attempt starts 2026-10-30 12:00:00 UTC; admission is 2026-10-31 11:59:59 UTC | Fresh, eligible only if the latest attempt fully passed with all exact bindings and applicable branch proof |
| Same attempt; admission is 2026-10-31 12:00:00 UTC or later | Refuse as stale, including when approval or completion occurred earlier |
| Start/completion timestamp is missing, future or inconsistent, or reported expiry differs from start plus 86,400 seconds | Refuse with the timing reason; do not infer a timestamp or extend freshness |
| Suite, profile, configuration, flow mapping, production baseline, applicable preparation branch or the branch-determining content of its selecting Administrator approval changes for a pending decision after a pass | Refuse the old binding and automatically queue a fresh complete serialized attempt under the same decision for the changed inputs |
| A different retained release passes intake, or the pending release switches to the approved retained-data or empty/degraded path | A different release gets a new decision and fresh evidence; a path switch automatically queues a fresh complete attempt under the same pending decision for full applicable branch proof |
| The same retained release passes intake again while its decision is pending | Open no additional decision and queue no attempt from the re-intake itself; at most one decision for that release is pending, and checked-input changes still queue attempts under it |
| The release that production's latest-working pointer currently names passes intake again | Open no decision and queue no attempt; a completed or superseded release returns under a new decision only when the pointer does not name it |
| A new per-attempt Administrator record repeats the selected path and, for the retained-data path, the named recovery's identity, acceptance checks and maximum duration | No checked-input change; queue no attempt from the new record alone |
| A production release attempt for a decision ends non-working, or its release becomes production's working baseline | A non-working release outcome leaves the decision pending and the next production release attempt for that decision repeats admission; when the latest-working pointer names the release, close the decision as completed before that baseline change queues any automatic trigger, so the release's own baseline change queues no attempt for it |
| For a pending decision, evidence expires or the latest E2E attempt ends failed, cancelled, incomplete, interrupted or superseded with no input change | Platform Maintainer may request a fresh attempt with its reason; preserve the prior result and do not start a periodic retry loop |
| Maintainer requests a manual rerun for a completed or superseded decision, or while the latest attempt is queued, running or a fresh pass | Refuse the request and record the reason |
| Attempt 1 passes, then attempt 2 is issued and is queued, running, failed, cancelled, incomplete, interrupted or superseded | Refuse fallback to attempt 1, regardless of finish order; retain both attempts |
| Latest attempt reruns every required flow and passes after an earlier failure | Eligible for that pending decision subject to current freshness, exact bindings and branch proof; earlier failure and rerun reason remain visible |
| Two attempts pass different subsets, or an unchanged module offers another decision's pass | Refuse aggregation or reuse; require a full passing attempt for this decision |
| A flow removal/remap has recorded and declared digests that agree but no owning-module or Platform coverage approval for either applicable predecessor transition | Refuse; require both approvals of each transition's before/after digests, affected flows/checks and coverage rationale; every lifecycle flow also needs Tenants review before enrollment and before any changed declaration joins the gate, including Tenants approval of lifecycle removal/remap |
| A removal/remap review record binds each applicable predecessor transition, or one record covers identical predecessors, with owning-module, Platform and, for a lifecycle flow, Tenants approvals | Accept the declaration into the gate; its evidence still needs a full passing attempt under current bindings |
| A new tenant-lifecycle flow has no Tenants review | Refuse its enrollment until Tenants and the owning module review its exact declaration and mapping digests |
| An E2E flow creates or deletes tenants without being declared and reviewed as a tenant-lifecycle flow | Refuse; tenant creation/deletion makes it a lifecycle flow regardless of its label |
| Candidate A's later rerun writes incompatible state, or readability is unproven without a production baseline | Reset before a same-candidate rerun or later candidate, using A's earliest pre-write recovery point since the last verified reset |
| Candidate B starts without an Administrator reservation of unadopted candidate A; then the production baseline changes | Close A's decision as superseded, refuse A at admission and issue no automatic baseline-change trigger for A |
| Reset enters at 12:00 UTC and remains non-terminal at 13:00 UTC | Off-site monitor notifies Administrator, recovery deputy and Platform Maintainer through the accepted private-issue path, continues restoration and keeps later candidates blocked pending verified reset |
| Reset executor dies or has an observation gap at 12:30 UTC | Executor loss interrupts the reset and leaves it non-terminal, so the 13:00 lifetime notification still applies until re-entry records it superseded; an observation gap leaves it unverified. No replacement job resumes it; Administrator or the deputy re-enters from the same point |
| Reset entered at 12:00 UTC; authorized re-entry occurs at 12:30 UTC | New attempt inherits the 13:00 deadline and preserves the same recovery point and prior lineage |
| Same interrupted restore re-enters at or after 13:00 UTC | Authorized re-entry is a new attempt with entry plus 3,600 seconds, the same recovery point and preserved prior lineage |
| Restore fails or is incomplete, or finishes but verification fails | The reset is unverified and is not reissued automatically; Administrator or the deputy re-enters from the same recovery point under the deadline inheritance rule. Keep later candidates blocked and retain the point and failed evidence; elapsed time or restore completion is not verification |
| Pruning sees a pending reset reference | Retain the point |
| A reset or named-recovery qualification follows a full passing E2E attempt | Recovery evidence does not count as or supersede the latest E2E evidence attempt; admission still checks all current bindings and applicable branch proof |
| A baseline lacks valid compatibility evidence and no Administrator-approved retained-data path applies, or applicable preparation proof is missing | Refuse admission; no unmatched state or missing branch proof defaults to acceptance |
| Administrator approves stale evidence or a retained-data path supplies only fresh-install proof | Refuse; approval cannot waive freshness or the applicable recovery qualification |

These examples are downstream qualification obligations. Scoped documentation
checks establish consistency and discoverability only; no workload, deployment,
remote publication, implemented gate, owner flow approval or live reset
qualification is claimed by this record.
