# Shared-infrastructure currency policy v1

Policy identifier: `hexalith.platform.shared-infrastructure-currency-policy.v1`.
Published: 2026-11-16. Version 1 records the decisions confirmed on 2026-11-15 in
the [Story 9.1 specification](../../_bmad-output/implementation-artifacts/spec-9-1-decide-the-shared-infrastructure-currency-policy.md)
and is fixed at the Platform revision completing Story 9.1. MUST and MUST NOT
express requirements for downstream implementation and qualification.

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

- At **every production attempt**, in both automatic and
  Administrator-approved modes, at its existing production admission evaluation
  under that attempt's lock and epoch, before mutation.
- At **every monthly drill** under Story 8.17, bound to the drill's checked
  deployment and inventory.
- On a **weekly schedule**, independently of attempts and drills.

Each invocation MUST evaluate the deployment and upstream data for that check.
A passing weekly or drill result MUST NOT substitute for a production attempt's
own check. Every failure, including an incomplete check or unavailable input,
MUST have a recorded result.

At production admission, `checkedAt` MUST be that admission's UTC evaluation
time. The evaluator MUST use the current inventory, deployed identities,
upstream data and qualified pairing, and evaluate support and security deadlines
at that time. An earlier evaluation or queued request reserves no eligibility;
changed inputs or a deadline reached before admission MUST NOT be accepted on
the strength of an earlier result.

The weekly check MUST notify Administrator when it fails or when any checked
component's upstream end of life is **within 30 days**, including exactly
30 days away. A supported component nearing end of life can therefore pass and
still require notification. The weekly result adds no attempt block and sets no
promotion stop; later production attempts apply their own check and decision 4.

### 3. Allowed lag: zero for support; 30 days for security fixes

There is **zero support lag**. A component MUST fail from its upstream
end-of-life date, when outside its applicable upstream supported matrix, or when
the management/workload deployment falls outside the qualified pairing. A
future end-of-life date is not a failure before that boundary. Use the upstream
effective instant when published; a date without a time takes effect at the
start of that date in UTC. Unknown support status fails closed.

For each published advisory applicable to the deployed component, a release
carrying its fix on the deployed line MUST be deployed within **30 days
(2,592,000 seconds) of that release's upstream release time**. If that line has
no supported fixed release, use the earliest supported line carrying the fix.
Retain the applicable advisory, fixed release, upstream release time and the
evidence that the deployed release contains the fix. A fix's original upstream
release time determines the deadline; a later advisory observation, inventory
edit, repackaging or later fixed release does not restart it.

Let `fixReleasedAt` be that first applicable fixed release's upstream UTC release
time and `checkedAt` the evaluation time. An unfixed component is within the lag
window only while `checkedAt < fixReleasedAt + 2592000 seconds`. At exactly the
deadline or later, it MUST fail and name the advisory and fixed release.
An advisory with no published fixed release has no release-based deadline yet;
the result MUST record that state rather than invent a fixed release or date.
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
The currency check neither sets nor clears an existing promotion stop.

## Comparison and fail-closed evaluation

For each check, the evaluator MUST use the applicable ratified inventory and:

1. Enumerate the complete checked set from decision 1 and observe each exact
   deployed identity, including immutable image/chart/artifact identities where
   applicable. Compare each deployed identity with its inventory pin. Any drift
   fails, including a different supported or newer release.
2. Resolve a version for every deployed identity and pin, then compare the pins
   with authoritative upstream support, matrix, advisory and fixed-release data
   **resolved at check time**. An image digest proves content identity, not a
   supported version. A digest-only component without a resolvable version fails
   even if its digest matches its pin.
3. Compare the actual management/workload identities and versions with both the
   upstream supported matrix and the recorded qualified pairing. Independently
   supported clusters do not establish a qualified pairing.
4. Apply all support and security deadlines and record every component's result
   and reasons. The overall result passes only when the complete checked set,
   all required comparisons and the pairing pass.

Missing inventory entries or pins, missing deployed observations, unresolvable
versions, missing pairing qualification, unavailable upstream data and
incomplete or unverifiable evidence MUST fail closed. The evaluator MUST name
the affected components and the missing or failed comparison; it MUST NOT omit
them or substitute a previously passing result or cached upstream conclusion.
Retained source observations document what this check resolved and do not
provide a cached fallback for a later check. If missing inventory or observations
prevent determination of the complete failing-component set, an approved
attempt cannot establish the naming rule and MUST be refused.

The [dated Rancher planning identities](../../eng/cluster-management/RANCHER.md#exact-dated-planning-identities-2026-10-04)
and [2026-10-25 infrastructure observations](../implementation/story-5-4-coupled-delivery-2026-10-25/installation-evidence/infrastructure-observation.json)
are informative historical inputs. They are neither the Story 4.9 inventory
nor proof of support, security currency or qualification at a later check.

## Required result record

Every invocation MUST retain a result, including failures before all observations
can complete. These are semantic requirements for Story 9.2, not a new schema or
an implemented command.

| Record scope | Required content |
| --- | --- |
| Check identity and context | Unique check ID; policy identifier and exact policy revision or content digest; exact evaluator revision or artifact digest; trigger (`production-attempt`, `monthly-drill` or `weekly-schedule`); UTC evaluation time (`checkedAt`), start and completion times; target environment/cluster identities; bound production attempt and mode, drill or scheduled run identity |
| Checked inventory | Exact inventory revision and content digest, active profile digest, and the complete set of covered component instance IDs; explicitly record an unavailable input rather than inventing one |
| Each component | Component instance ID and currency owner; exact deployed identity and resolved version; inventory pin and resolved version; observation time and evidence; identity-match result |
| Upstream observations | Per-component authoritative support, matrix, advisory and fixed-release source references and retained observations; UTC observation time; supported line and end-of-life boundary; advisory applicability; applicable fixed releases, upstream release times and calculated deadlines, or an explicit no-published-fix state |
| Component decision | Support, matrix and security-lag outcomes, pass/fail, and every failure or unknown reason; overdue fixes name the advisory and fixed release; impending end of life includes the boundary and notification reason |
| Pairing | Actual management/workload identities and versions, upstream matrix source and observation time, exact qualified-pairing record, comparison result and reasons; attribute failure to the involved component IDs |
| Overall decision | Complete pass/fail, every failing component and reason, missing inputs, and the resulting automatic block or approved-attempt acceptance/refusal; no promotion stop set by this check |
| Approval and notification | Applicable Administrator record and named-component comparison, including unnamed failures; for weekly runs, notification requirement, reason and delivery outcome |

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
and meaning MUST remain fixed at the Platform revision completing Story 9.1;
changed rules require a new version and explicit downstream adoption. Evidence
MUST retain the version and exact policy text identity under which it was
evaluated. Consumers MUST NOT silently select a latest policy or reinterpret
old results. Later changes affecting automatic-promotion safety follow Story
9.7's suspension and affected-rehearsal requirements.

## Downstream obligations

| Consumer | Required implementation or evidence |
| --- | --- |
| Story 4.9 | Create and ratify the actual profile inventory, with exact component pins and identities, per-component authoritative support/advisory sources and named currency owners, plus the qualified management/workload pairing. Its separately pinned deploy toolchain remains outside this check |
| Story 9.2 | Implement every trigger, complete comparison, fail-closed outcome, result field, weekly notification and approval-naming rule; bind adoption to this exact policy text and evaluator identity and qualify the acceptance/refusal cases below |
| Story 9.6 | Rehearse current, drifted, unsupported, overdue-security, unknown-data and unqualified-pairing paths; prove automatic refusal without a waiver, approved acceptance only with all failing components named, refusal requiring new approval for any unnamed failure, retained results and no stop set by the currency check |
| Story 9.8 | Require implemented and qualified currency enforcement, its current passing automatic-attempt result and the required Story 9.6 evidence before recording G3 and enabling automatic promotion; policy publication alone satisfies none of those enforcement or G3 proofs |

Stories 6.22 and 8.17 retain their existing guarded-change and drill obligations.
An urgent in-place security patch under Story 6.22 still requires its
Administrator record and existing recovery and verification controls. That
procedure neither waives automatic currency refusal nor removes the per-attempt
check. This policy changes no infrastructure, pin, workflow, staging-evidence
rule or vulnerability-policy rule.

## Acceptance and refusal cases

The attempt dispositions below apply to this currency check; every other
production precondition remains required. No row sets a promotion stop.

| Input or state at this check | Recorded currency result | Automatic promotion | Administrator-approved attempt |
| --- | --- | --- | --- |
| Every component equals its pin, is supported and within all security deadlines; matrix and qualified pairing match | Pass | Currency requirement met | Currency requirement met |
| A deployed identity differs from its pin, even if newer and supported | Fail; name the component and drift | Block; no waiver | May start only if the record names every failing component |
| Version cannot be resolved, including a digest-only component matching its pin, or required upstream data is unavailable | Fail; name each affected component and unknown input; no cached fallback | Block; no waiver | May start only if the complete failing set is known and the record names every failing component |
| Check cannot determine the complete component/failure set because the inventory or observations are incomplete | Fail; record the missing coverage | Block; no waiver | Refuse: the complete naming rule cannot be established |
| A component reaches its upstream end-of-life boundary, leaves the supported matrix, or management/workload pairing is unqualified | Fail; name affected components and support/matrix/pairing reason | Block immediately; no support grace | May start only if the record names every failing component |
| Applicable fix is not deployed; check is one second before its upstream release time plus 2,592,000 seconds; all other checks pass | Pass; retain advisory, fixed release and deadline | Currency requirement met | Currency requirement met |
| Same unfixed component at exactly that deadline or later | Fail; name component, advisory and fixed release | Block; no waiver | May start only if the record names every failing component |
| Applicable published advisory has no published fixed release, with support and all other comparisons known and passing | Pass for this release-based lag rule; record no published fix | Currency requirement met, subject to the independent vulnerability gate | Same independent gates apply |
| Only a non-security update is outstanding, and pin, support, matrix and qualified pairing checks pass | Pass; no non-security lag limit | Currency requirement met | Currency requirement met |
| Failed result and the Administrator record names every failing component | Fail remains recorded; retain approval and naming comparison | Block; approval supplies no automatic waiver | May start subject to every other precondition |
| Failed result includes any unnamed component, including a newly observed failure after approval | Fail; name the omissions and retain refusal | Block | Refuse until a new approval names every failing component |
| Weekly check passes, but upstream end of life is exactly 30 days away or nearer | Pass; record and notify Administrator | No weekly block; own attempt check required | No weekly block; own attempt check required |
| Weekly check fails | Fail; retain reasons and notify Administrator | Weekly run adds no block; own attempt check controls promotion | Weekly run adds no block; own attempt check and naming rule control admission |
| Earlier weekly, drill or production check passed, but this production attempt has no completed check result | Missing check; record failure rather than reuse the earlier pass | Block | Refuse until this attempt's check is run and recorded and its applicable naming rule is met |

These cases are downstream qualification obligations. Documentation verification
establishes consistency and discoverability only; enforcement, rehearsal,
inventory ratification and current compliance remain separate work.
