# Shared-infrastructure currency policy v1

Policy identifier: `hexalith.platform.shared-infrastructure-currency-policy.v1`.
Version 1 records the decisions confirmed on 2026-11-15 and the Administrator's
2026-11-25 decision on fixed-release timing. The Platform revision closing final
Story 9.1 review fixes the canonical v1 text. The AD-26 snapshot is historical
review evidence. MUST and MUST NOT express requirements for downstream
implementation and qualification.
[Story 9.1 specification](../../_bmad-output/implementation-artifacts/spec-9-1-decide-the-shared-infrastructure-currency-policy.md)

Platform owns this policy with Administrator. The accountable policy owner is
the Platform Maintainer, `@jpiquot`, under the
[repository ownership policy](../../.github/CODEOWNERS). One direct recorded
approval of the identified decision suffices; separate approvals are not needed
when the same person holds the Platform and Administrator roles.

This record governs FR-10 and AR-61 under the architecture's
[production profile, shared-infrastructure and G3 rules](../../_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md)
and the [release-mode sequencing](../../_bmad-output/specs/spec-platform/sequencing.md#production-entry-gates).
It publishes the policy only. It establishes neither a ratified profile
inventory, implemented enforcement, current infrastructure compliance nor G3.

## Four policy decisions

### 1. Checked inventory

The check MUST cover **every versioned component in the profile inventory's
shared facet**, including:

- Workload Kubernetes, its node OS, containerd and etcd.
- Rancher server, required agents and CRDs, the management-cluster distribution
  and its versioned constituents, and the management backup operator and CRDs.
- CNI, Traefik, Gateway API CRDs, cert-manager, the storage provisioner,
  operators and other versioned shared CRDs.
- The Dapr control plane, Keycloak server and Keycloak's database.
- Any other versioned shared-facet component, including registry and backup
  components such as Zot and Velero where present in that facet.

It MUST also cover production's **environment-layer providers: OpenBao,
PostgreSQL, FalkorDB and Redis**. Cover the actual database deployments serving
Keycloak and production's environment layer, retaining both usage scopes whether
those deployments are shared or separate. Required
versioned constituents remain covered when supplied inside a distribution or
bundle; a distribution version alone MUST NOT hide an unresolved constituent.

The list defines coverage, not installed versions, pins or topology. Story 4.9
owns creation and ratification of the profile inventory. Configuration-only
objects, whose content is covered by the profile digest, and the separately
pinned deploy toolchain are excluded from this currency check. Their existing
qualification and configuration requirements still apply.

### 2. Cadence and triggers

The check MUST run and record a result:

- At admission for **every production release attempt**, in both automatic and
  Administrator-approved modes, under that attempt's lock and current epoch,
  before mutation.
- At **every monthly drill** under Story 8.17, bound to the drill's checked
  deployment and inventory.
- On a **weekly schedule** against the active production deployment and the
  shared infrastructure that serves it, independently of attempts and drills.

Each invocation MUST evaluate the deployment and upstream data for that check.
A passing weekly or drill result MUST NOT substitute for a production release
attempt's own check. Every failure, including an incomplete check or unavailable
input, MUST have a recorded result.

At production release admission, `checkedAt` MUST be that admission's UTC
evaluation time. Bind the result to the attempt ID, release mode and current
lock epoch. Admission MUST verify all three against the current admission.
A replacement job with a new epoch needs its own check, even if the attempt ID
stays the same. The evaluator MUST use the current inventory, deployed
identities, upstream data and qualified pairing, and evaluate support and
security deadlines at that time. An earlier evaluation or queued request
reserves no eligibility. Changed inputs or a deadline reached before admission
MUST NOT be accepted on the strength of an earlier result.

The weekly check MUST notify Administrator when it fails or when any checked
component has a known upstream end-of-life instant after `checkedAt` and at most
**30 days (2,592,000 seconds)** away, using the UTC instants in decision 3. A
supported component nearing end of life can therefore pass and still require
notification. Use the architecture's existing private GitHub issue channel with
its issues-only credential. Assign the issue to Administrator and mention the
recovery deputy. Retain the issue reference when created and record the delivery
outcome. Weekly and monthly-drill results add no production-attempt block and
set no promotion stop. A monthly-drill result and its reasons are retained with
the drill evidence. Each production release attempt applies its own check and
decision 4.

### 3. Allowed lag: zero for support; 30 days for security fixes

There is **zero support lag**. A component MUST fail at or after its upstream
end-of-life instant, when outside its applicable upstream supported matrix, or
when the management/workload deployment falls outside the qualified pairing.
Unknown support status fails closed.
When an authoritative source confirms current support with no announced
end-of-life date, record both facts. The weekly end-of-life notice uses only
known future instants.

Use UTC instants for the evaluation time, upstream end-of-life boundaries and
upstream fixed-release times. Convert a published instant to UTC. For a
date-only value, use `00:00:00 UTC` at the start of that date. Apply the same
end-of-life instant to the support check and the weekly notification window.

For each published advisory applicable to the deployed component, the fix MUST
be deployed within **30 days (2,592,000 seconds)** of `fixReleasedAt`. Use the
upstream release time of the first release carrying the fix on the deployed
line. While that line has no release carrying the fix, choose the oldest
currently supported line carrying the fix in upstream release-line order, then
use the upstream release time of its first fixed release. Re-evaluate line
eligibility at every check. Once the deployed line gets its first fix, its
release time governs. A later release on the same line, a later advisory
observation, a repackaging or an inventory edit does not restart the clock.
Retain the advisory, selected fixed release, its upstream release time and any
evidence that the deployed release contains the fix.

An unfixed component is within the lag window only while
`checkedAt < fixReleasedAt + 2592000 seconds`. At exactly the deadline or later,
it MUST fail and name the advisory and fixed release.
An advisory with no published fixed release has no release-based deadline yet;
the result MUST record that state rather than invent a fixed release or date.
If fixes are published but no eligible deadline can be established, the check
MUST fail closed and record the advisory and reason.
Unknown applicability, fix status or release timing MUST fail closed. All
applicable advisory deadlines must pass; one recently fixed advisory cannot
hide another overdue fix.

**Non-security updates have no lag limit while the deployed version remains
supported**, within the supported matrix and qualified pairing. This currency
window does not alter the separate
[image and dependency vulnerability policy](image-and-dependency-vulnerability-policy-v1.md)
or grant an exception to its scan and promotion rules.

### 4. Effect on automatic and Administrator-approved attempts

A failed check MUST **block automatic promotion with no waiver**, name every
failing component and its reasons, and set **no promotion stop**. An approval
cannot convert a failed result to a pass or authorize the automatic path.

An Administrator-approved attempt MUST still run and record the check. If the
result fails, the attempt may start only when its Administrator record
**explicitly names every failing component**. Names MUST identify the checked
inventory component instances, including their environment or cluster scope
where needed to distinguish them. A generic acknowledgment such as "all
infrastructure risks" does not name the components.

One direct recorded Administrator approval of the identified record suffices,
subject to the existing Administrator identity and record-verification rules.
The check and its approved-attempt decision MUST retain the approval reference
and the naming comparison. Any unnamed failing component refuses the attempt
until a **new approval** names every failing component. If a later check finds
another failure, the earlier approval cannot cover an unnamed component.
The currency result remains failed even when the naming rule permits the
approved attempt to start. All other production preconditions still apply.
The currency check preserves the exact record and revision of any existing
promotion stop.

## Comparison and fail-closed evaluation

For each check, the evaluator MUST use the applicable ratified inventory and:

1. Verify the recorded revision and content digest of Story 4.9's separate
   composing inventory and the path and SHA-256 of its content-bound source.
   Establish the complete set of component instances selected by decision 1
   from that source's independent enumeration. Verify that the checked profile
   has the same set of selected component IDs.
   [Hosted profile binding v2 independent coverage contract](hosted-profile-binding-v2.md#independent-coverage-and-authority-boundary)
2. Observe each selected instance's exact deployed identities, including immutable
   image/chart/artifact identities where applicable. When a component spans
   multiple nodes or replicas, observe its identity on every in-scope node or
   replica. Compare each deployed identity with its applicable inventory pin.
   Any drift or missing observation fails the component instance, including
   drift to a supported or newer release.
3. Resolve a version for every deployed identity and pin. Compare each observed
   deployed version and each pinned version with authoritative upstream support,
   matrix, advisory and fixed-release data **resolved at check time**. An image
   digest proves content identity, not a supported version. A digest-only
   component without a resolvable version fails even if its digest matches its
   pin.
4. Compare the actual management/workload identities and versions with both the
   upstream supported matrix and the recorded qualified pairing. Independently
   supported clusters do not establish a qualified pairing.
5. Apply all support and security deadlines and record every component's result
   and reasons. The overall result passes only when the complete checked set,
   all required comparisons and the pairing pass.

Missing or unverifiable independent coverage, missing inventory entries or pins,
missing deployed observations, unresolvable versions, missing pairing
qualification, unavailable upstream data and incomplete or unverifiable
evidence MUST fail closed. The evaluator MUST name
the affected components and the missing or failed comparison; it MUST NOT omit
them or substitute a previously passing result or cached upstream conclusion.
Retained source observations document what this check resolved and do not
provide a cached fallback for a later check. If incomplete coverage, inventory
or observations prevent determination of the complete failing-component set, an
approved attempt cannot establish the naming rule and MUST be refused.

The [dated Rancher planning identities](../../eng/cluster-management/RANCHER.md#exact-dated-planning-identities-2026-10-04)
and [2026-10-25 infrastructure observations](../implementation/story-5-4-coupled-delivery-2026-10-25/installation-evidence/infrastructure-observation.json)
are informative historical inputs. They are neither the Story 4.9 inventory
nor proof of support, security currency or qualification at a later check.

## Required result record

Every invocation MUST retain a result, including failures before all observations
can complete. Production release admission requires a completed, retained result
bound to that attempt and current lock epoch. If the result is missing, could
not be retained or has the wrong binding, automatic promotion is blocked and an
Administrator-approved attempt is refused. Record that failure and its reason
whenever recording is available. Story 9.2 owns the result schema and evaluator.

| Record scope | Required content |
| --- | --- |
| Check identity and context | Unique check ID, policy identifier and exact policy revision or content digest, exact evaluator revision or artifact digest, trigger (`production-attempt`, `monthly-drill` or `weekly-schedule`), UTC evaluation time (`checkedAt`), start and completion times, and target environment/cluster identities. Record the bound production attempt ID, release mode and lock epoch, or the bound drill or scheduled run identity. |
| Checked inventory | Exact profile inventory revision and content digest, active profile digest, exact composing inventory revision and content digest, its independently enumerated source path and SHA-256, and the complete set of covered component instance IDs. Identify unavailable inputs explicitly. |
| Each component | Component instance ID and currency owner. For each deployed identity, record the node or replica identifier where applicable, exact identity and resolved version, applicable inventory pin and resolved version, observation time and evidence, and identity-match result. Identify missing observations. |
| Upstream observations | Per-component authoritative support, matrix, advisory and fixed-release source references and retained observations. For every observed version and pin, record UTC observation time, release line and support status, the end-of-life instant or confirmed support with no announced date, advisory applicability, and applicable fixed releases, upstream release times and calculated deadlines. Record no published fix or the reason a published fix yields no eligible deadline when applicable. |
| Component decision | Support, matrix and security-lag outcomes, pass/fail, and every failure or unknown reason; overdue fixes name the advisory and fixed release; impending end of life includes the boundary and notification reason |
| Pairing | Actual management/workload identities and versions, upstream matrix source and observation time, exact qualified-pairing record, comparison result and reasons; attribute failure to the involved component IDs |
| Overall decision | Pass/fail, every failing component and reason, missing inputs, and the resulting automatic block or approved-attempt acceptance/refusal for a production attempt. Weekly and monthly-drill results add no attempt block. No result sets a promotion stop. |
| Approval and notification | Applicable Administrator record and named-component comparison, including unnamed failures. For weekly runs, record notification requirement, reason, private issue reference when created and delivery outcome. |

A summary pass without the underlying complete component and pairing results
is insufficient. Check records MUST preserve failed results and their reasons;
a later check records its own outcome rather than overwriting earlier evidence.

## Accountability and revisit conditions

| Decision | Accountable owner and responsibility | Revisit condition |
| --- | --- | --- |
| Complete inventory scope and exact comparisons | Platform Maintainer `@jpiquot`; Story 4.9 names each component's currency owner and upstream sources | A new shared or production-provider component, an unresolved bundled version, or an inventory/observation gap exposes missing coverage |
| Attempt, monthly and weekly cadence | Platform Maintainer `@jpiquot`, with Administrator; Platform implements recording and scheduled notifications | A missed check, notification failure, changed drill cadence or incident shows a detection gap |
| Zero support lag and 30-day fixed-release window | Platform Maintainer `@jpiquot`, with Administrator and component currency owners | An upstream support/advisory or release model changes, a matrix/pairing changes, or an incident or rehearsal challenges the window |
| Automatic refusal and approved-component naming | Platform Maintainer `@jpiquot`, with Administrator; Administrator owns each attempt approval | A rehearsal or attempted release exposes an ambiguous failure name, incomplete acknowledgment or changed release-mode behavior |

A revisit requires an explicit recorded policy decision. Version 1's identifier
and meaning MUST remain fixed at the Platform revision closing final Story 9.1
review. Changed rules require a new version and explicit downstream adoption. Evidence
MUST retain the version and exact policy text identity under which it was
evaluated. Consumers MUST NOT silently select a latest policy or reinterpret
old results. Story 9.7's suspension and affected-rehearsal requirements apply
to changes in verification, recovery or rollback-set policy.

## Downstream obligations

| Consumer | Required implementation or evidence |
| --- | --- |
| Story 4.9 | Create and ratify the actual profile inventory, with exact component pins and identities, per-component authoritative support/advisory sources and named currency owners, plus the qualified management/workload pairing. Supply the separate v2 composing inventory and its content-bound independent source. |
| Story 9.2 | Implement every trigger, complete comparison, fail-closed outcome, result field, weekly notification and approval-naming rule; bind adoption to this exact policy text and evaluator identity and qualify the acceptance/refusal cases below |
| Story 9.6 | Rehearse current, drifted, unsupported, overdue-security, unknown-data and unqualified-pairing paths. Prove automatic refusal without a waiver, approved acceptance only with all failing components named, refusal requiring new approval for any unnamed failure, retained results and that the check creates no stop and preserves any existing promotion-stop record and revision exactly. |
| Story 9.8 | Require qualified currency enforcement and the required Story 9.6 evidence before recording G3 and enabling automatic promotion. After G3, require a passing recorded check for each automatic attempt |

Stories 6.22 and 8.17 retain their existing guarded-change and drill obligations.
An urgent in-place security patch under Story 6.22 still requires its
Administrator record and existing recovery and verification controls. Every
production release attempt still requires its own currency check and follows
the automatic refusal rule. This policy changes no infrastructure, pin,
workflow, staging-evidence rule or vulnerability-policy rule.

## Acceptance and refusal cases

The attempt dispositions below apply to this currency check; every other
production precondition remains required. No row sets a promotion stop.

| Input or state at this check | Recorded currency result | Automatic promotion | Administrator-approved attempt |
| --- | --- | --- | --- |
| Every component equals its pin, is supported and within all security deadlines; matrix and qualified pairing match | Pass | Currency requirement met | Currency requirement met |
| A deployed identity differs from its pin, even if newer and supported | Fail; name the component and drift | Block; no waiver | May start only if the record names every failing component |
| One node or replica has drifted from the applicable pin while all others match | Fail. Name the component and identify the node or replica and its drift | Block without waiver | May start only if the record names every failing component |
| Version cannot be resolved, including a digest-only component matching its pin, or required upstream data is unavailable | Fail; name each affected component and unknown input; no cached fallback | Block; no waiver | May start only if the complete failing set is known and the record names every failing component |
| Check cannot determine the complete component/failure set because independent coverage, inventory or observations are incomplete | Fail. Record the missing coverage | Block without waiver | Refuse because the complete naming rule cannot be established |
| A component reaches its upstream end-of-life boundary, leaves the supported matrix, or management/workload pairing is unqualified | Fail; name affected components and support/matrix/pairing reason | Block immediately; no support grace | May start only if the record names every failing component |
| Applicable fix is not deployed; check is one second before its upstream release time plus 2,592,000 seconds; all other checks pass | Pass; retain advisory, fixed release and deadline | Currency requirement met | Currency requirement met |
| Same unfixed component at exactly that deadline or later | Fail; name component, advisory and fixed release | Block; no waiver | May start only if the record names every failing component |
| The earliest supported line carrying a fix is B, first fixed on day 0. At day 19, the deployed line A has no fix and all other checks pass | Pass. Record B's fixed release and its day-30 deadline | Currency requirement met | Currency requirement met |
| Line A gets its first fix on day 20. At day 30, the deployment remains unfixed and all other checks pass | Pass. Record A's fixed release and its day-50 deadline | Currency requirement met | Currency requirement met |
| Line A is still unfixed at day 50 | Fail. Name the component, advisory and A's first fixed release | Block without waiver | May start only if the record names every failing component |
| Deployed line A has no fix. Older supported line B first carries the fix on day 10. Newer supported line C first carries it on day 0. At day 35, all other checks pass | Pass. Choose line B in upstream release-line order and record its first fixed release and day-40 deadline | Currency requirement met | Currency requirement met |
| Deployed line A is supported but has no fix. All published fixes are on other lines that are unsupported at check time | Fail. Record the advisory and that no eligible deadline can be established | Block without waiver | May start only if the record names every failing component |
| Applicable published advisory has no published fixed release, with support and all other comparisons known and passing | Pass. Record that the advisory has no published fix and no release-based deadline | Currency requirement met | Currency requirement met |
| Only a non-security update is outstanding, and pin, support, matrix and qualified pairing checks pass | Pass; no non-security lag limit | Currency requirement met | Currency requirement met |
| Failed result and the Administrator record names every failing component | Fail remains recorded; retain approval and naming comparison | Block; approval supplies no automatic waiver | May start subject to every other precondition |
| Failed result, every failing component is named in the Administrator record and a promotion stop already exists | Fail. Preserve the exact existing stop record and revision | Block without waiver. Existing stop rules still apply | Currency naming requirement met. Existing stop rules still govern admission |
| Failed result includes any unnamed component, including a newly observed failure after approval | Fail; name the omissions and retain refusal | Block | Refuse until a new approval names every failing component |
| Weekly check passes and upstream end of life is in the future and at most 2,592,000 seconds away | Pass. Record the result and notify Administrator | Weekly run adds no block. Own attempt check required | Weekly run adds no block. Own attempt check required |
| Weekly check passes and the nearest known future end-of-life instant is 2,592,001 seconds away | Pass. Record the result with no notification due | Weekly run adds no block. Own attempt check required | Weekly run adds no block. Own attempt check required |
| Weekly check passes and authoritative sources confirm current support for every component without announcing any end-of-life date | Pass. Record known support with no announced end-of-life date and no notification due | Weekly run adds no block. Own attempt check required | Weekly run adds no block. Own attempt check required |
| Weekly check fails | Fail; retain reasons and notify Administrator | Weekly run adds no block; own attempt check controls promotion | Weekly run adds no block; own attempt check and naming rule control admission |
| Monthly-drill check fails | Fail. Retain the failing components and reasons with the drill evidence | Drill run adds no block. Own attempt check controls promotion | Drill run adds no block. Own attempt check and naming rule control admission |
| Production release admission only has a passing result for the same attempt ID under an earlier lock epoch | Fail. Record the epoch mismatch when recording is available | Block until a new result passes under the current binding | Refuse until there is a recorded result under the current binding and any applicable naming rule is met |
| This production release attempt has no completed, retained check result, even if an earlier check passed | Fail. Record the missing result as the reason when recording is available | Block. This attempt requires its own passing recorded result | Refuse until this attempt's check is run and recorded and any applicable naming rule is met |

These cases are downstream qualification obligations. Documentation verification
establishes consistency and discoverability only; enforcement, rehearsal,
inventory ratification and current compliance remain separate work.
