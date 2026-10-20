# Candidate evidence and attachment policy v1

Policy identifier: `hexalith.platform.candidate-and-attachment-policy.v1`.
Published: 2026-10-20. This immutable version records the Platform decisions
confirmed by the user for Story 2.1 (AR-16). MUST and MUST NOT express requirements
for downstream adoption.

Platform owns this policy; Hexalith.Builds owns its adoption in `hexalith-module`
and reusable CI workflows. The accountable policy owner is the Platform Maintainer
(Hexalith), `@jpiquot`, under the [repository ownership policy](../../.github/CODEOWNERS).
Publication records Platform policy, not Builds approval or implemented runner
support. Builds adoption, a Platform-accepted tool version and integration
qualification remain prerequisites to accepting evidence or attachments under it.
This record changes no runner, schema, tool pin, publication authority or staging
gate.

The governing constraints are [FR-4](../../_bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/prd.md)
and [AD-4, AD-5 and AD-10](../../_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md).
The [Story 2.1 specification](../../_bmad-output/implementation-artifacts/spec-2-1-decide-the-candidate-and-attachment-policy.md)
records the approved choices. Stories 2.2, 2.6, 2.7 and 3.1 MUST adopt this same
identifier and the handoff below before their consumers accept evidence or attach.

## Candidate revision and gated decision

For a **PR merge decision**, the candidate is the **committed PR merge result**,
identified by its exact commit SHA and bound to the repository, candidate module,
PR, exact PR-head SHA and exact base SHA from which it was produced. A branch
name, PR number, mutable merge ref, version label or workflow event alone is
insufficient. If no committed merge result for that pair can be established,
candidate acceptance MUST be refused.

Evidence gates merging **that exact PR-head/base pair**. Acceptance MUST recheck
the pair and candidate identity at the merge decision. A changed head or base
requires a new merge result and a new run, even if only an unchanged module is
being assessed. PR-head-only evidence cannot qualify the merge-result candidate.
For a **release decision**, existing release authority selects the exact committed
release revision as its candidate; it need not equal the PR merge-result SHA.
That decision requires a fresh run and its own exact-revision evidence, even when
the revision is identical to a PR candidate. PR merge evidence grants no release
authorization. Existing authenticated exact-source CI, publication approvals and
staging gates remain separate and mandatory where applicable.

The trusted merge or release gate MUST issue each original decision request's
identity and authenticate its issuer. That identity immutably binds repository,
candidate module, policy identifier, decision kind and target committed revision
(including PR identity and head/base pair for a merge). Consumers MUST verify
these bindings against the gate's request; callers cannot invent or relabel a
decision identity. Retries of the same pending request retain that identity but
produce distinct run and attempt identities. A changed target, reopened PR, or new
merge/release request is a new decision requiring a fresh run, even if its target
bytes match a previous decision. A completed or closed request cannot be reopened
under its previous identity to reuse evidence.

Candidate acceptance MUST preserve the AD-4 mapping: every consumed artifact of
the candidate module comes from that committed revision's own Release build,
including Contracts, server and extension packages, services and tools. Libraries
cross NuGet package boundaries; other modules remain pinned to the Builds catalog.
The descriptor and result MUST bind expected content identities and identities
actually loaded by the composed host, run-scoped McpCli and tests. Equal version
labels do not establish equal content. Missing identities, mismatches, substituted
or cached builds from another revision, and uncommitted inputs MUST be refused.

Local Debug/source runs and explicitly labelled `baseline-only` runs can remain
useful diagnostics, but MUST NOT satisfy candidate acceptance. Required candidate
or loaded-identity fields reserved but not yet populated by a producer do not
qualify as evidence.

## Evidence reuse and lineage

Version 1 **prohibits evidence reuse across decisions**, including evidence for
unchanged modules and decisions with identical artifact bytes. Every new decision
requires a new run. An unchanged-module result from another PR, head/base pair,
release decision, policy version or run MUST NOT be imported to satisfy it.
Catalog-pinned dependency artifacts may still be consumed through AD-4; their
presence is not permission to reuse a prior test result.

Every decision's evidence MUST name its own producing run and decision. Its reuse
lineage MUST explicitly declare reuse disallowed and contain no reused source
runs or source decisions. Refuse a lineage that points at another decision's
evidence, or omits the producing run/decision. Reading the same immutable result
again for its original decision does not make it evidence for a new decision.
`baseline-only`, missing-identity, `cancelled` and `withdrawn` results cannot be
promoted into qualifying evidence. A `passed` outcome is necessary but insufficient
without all candidate, identity, policy and integration qualification checks.

Evidence MUST bind both the declared and actually executed suite and configuration
content identities, the declared required-test set and its completion counts.
Acceptance MUST verify the executed identities match the declared ones and all
required tests completed and passed, with counts reconciled to a nonempty required
set. Wrong suites/configurations, empty suites, skipped required tests, incomplete
execution or missing completion evidence MUST NOT qualify, regardless of `passed`.

## Attachment holds

An attachment request MUST select a compatible, isolated, runner-owned local or
CI environment. Before acceptance the runner MUST verify composition identity,
artifact mode, expected/loaded content identities, readiness, successful required
startup tasks and data isolation. Hosted targets and separately developer-started
Aspire environments are ineligible. CI attachment MUST remain within the owner's
same CI job, with exact workflow-run, attempt and job identity bound in the handoff.

| Request | Effective hold from acceptance |
| --- | --- |
| Omitted | 30 minutes (1,800 seconds) |
| Explicit positive finite duration up to 4 hours | Exactly the requested duration; shorter requests are allowed |
| Exactly 4 hours (14,400 seconds) | 14,400 seconds |
| Zero, negative, malformed, non-finite or over 14,400 seconds | Reject before attachment; do not clamp or substitute the default |

For an accepted attachment, record `acceptedAt`, `effectiveHoldSeconds` and
`expiresAt = acceptedAt + effectiveHoldSeconds` as an absolute UTC timestamp.
Record the original requested duration separately; omission MUST remain
distinguishable from an explicit request. The hold starts at attachment
acceptance, not request submission, environment creation, readiness completion or
test execution. Validate the request before recording an accepted attachment.

The runner MUST issue an `originalAttachmentRequestId` for each original explicit
attachment request, bound to its environment and owner. This lineage identifier
is the comparison identity for renewal: retries, reconnects and replacement run
IDs MUST inherit it regardless of suite/configuration labels. The runner MUST
resolve it from the original request record, not trust a caller-selected fresh ID.
Its first accepted `acceptedAt` and `expiresAt` are immutable across that lineage;
no heartbeat, renewal or detach/reattach may reset them. Continued work beyond
that expiry requires a fresh run-owned environment and run. A separate original
request receives a distinct runner-issued lineage and its own finite hold only
after all compatibility and owner-state checks; it MUST NOT disguise a retry or
reconnect of an existing request. Every live hold must be accounted for before
automatic local-success cleanup.

The hold ends at the earliest of the attachment's own terminal outcome, expiry,
environment withdrawal, or CI job completion/cancellation. Earlier termination
does not move the recorded expiry: record the actual end time and reason as well.
Expiry or environment withdrawal ends an active attachment as `withdrawn`, neither
pass nor failure and never valid integration evidence. A process reporting success
after its attachment expired cannot turn that withdrawn run into a pass.

Hold, startup and execution deadlines are separate. The default startup deadline
remains ten minutes from the environment-start request, excluding test execution;
only a justified finite declaration override changes it. The test configuration
supplies any execution deadline independently. Neither deadline nor an execution
retry extends an attachment hold, and a shorter hold may withdraw a still-running
test before its execution deadline. The result MUST preserve each effective
deadline and its timing origin.

## Ownership, withdrawal and cleanup

Record each run's first terminal outcome immediately, including the owner while
attachments are active. Owner success waits for all accepted holds to end before
automatic local cleanup. Owner completion refuses new attachments, except to a
local environment retained after owner or attached-run test/startup failure that
still passes all compatibility and readiness checks. Failure retention does not
renew a hold or permit attachment to a withdrawn or explicitly stopped environment.

An attached run's cancellation ends only that run and its hold; it does not stop
the owner or other attachments. Owner cancellation or explicit developer stop
withdraws active attachments and reports them before cleanup. Withdrawal produces
no evidence. Previously recorded terminal outcomes MUST NOT be overwritten:
later cancellation or withdrawal cannot erase a prior local failure or its
diagnostics.

Expiry, withdrawal or cancellation MUST also terminate that attached run's owned
processes/tasks and end its environment access. Stopping an attachment MUST
preserve the owner and other attachments; owner withdrawal still ends all active
attachments as required above. Record stopping/access-termination failures and
leftover work, retry their cleanup, and never report them as successfully stopped.
Such failures grant no evidence or extension of the original expiry; retaining
local failure resources does not authorize attached work to continue accessing them.

Any local owner or attached-run test/startup failure retains surviving resources
for debugging, even after owner success, attachment expiry or later cancellation.
Expiry releases a hold, not failure retention. Cancellation preserves already
retained local failure resources; explicit developer cleanup can remove them.
Retained resources are a debugging disposition, not a live attachment or evidence
of a pass. Listings MUST show owner, age, failed/attached runs and absolute hold
deadlines, including pending cleanup.

CI job completion or cancellation withdraws active attachments before cleanup,
regardless of unexpired holds. CI always cleans its owned resources after success,
failure, cancellation and partial startup; local failure retention does not apply.
Cleanup MUST be retryable and limited to that environment's owned resources,
report leftovers, and never claim incomplete cleanup is complete. Preserve
classified, redacted results and diagnostics after resource deletion. Integration
CI stays on disposable Linux GitHub-hosted runners, runs isolated tests first and
then blocking real-service package-mode integration, and receives no staging or
production provider credentials; retained-registry access remains read-only pull.

## Required descriptor and result handoff

The following are semantic requirements for Story 2.2's versioned descriptor and
result contracts, not a schema or CLI implementation. Both descriptor and result
MUST carry the exact `policyId` above and agree on their shared bindings. Unknown,
missing or mismatched policy identifiers MUST fail closed before attachment or
candidate-evidence acceptance. Baseline-only diagnostics must be labelled rather
than silently filling absent candidate fields with catalog identities.

| Binding | Required meaning |
| --- | --- |
| Policy and run | Exact `policyId`, descriptor/result contract versions, producing run/attempt, owner run and environment identities; attached run identity when relevant; CI run/attempt/job identity for CI |
| Executing tool | Actual executing tool version and content identity, with applicable Builds adoption, Platform acceptance and integration-qualification references binding that tool and policy; a configured pin alone is insufficient, and missing or inapplicable qualification refuses attachment/candidate acceptance |
| Candidate | Candidate or explicit `baseline-only` classification; candidate repository/module and exact committed revision; for PR merge evidence, PR identity, exact head SHA, base SHA and merge-result SHA |
| Gated decision | Trusted gate-issued original request identity and authenticated issuer, immutable repository/module/policy/kind/target bindings, request state and distinct producing run/attempt identity; retries retain the pending decision identity, while reopened/new requests require new decisions |
| Reuse lineage | Own producing run/decision, reuse disallowed, empty reused-source run/decision lineage in v1; never an absent or inferred reuse declaration |
| Environment and artifacts | Composition identity, mode, catalog commit and instance, synthetic tenant/data-isolation binding, readiness, required startup-task results, expected and loaded content identities from AD-4/AD-5 |
| Suite and completion | Declared and actually executed suite/configuration content identities; required-test set identity and counts for required, executed, completed, passed, failed and skipped tests, reconciled with individual results and nonempty required coverage |
| Attachment | Runner-issued `originalAttachmentRequestId` bound to environment/owner and inherited across retries/reconnects/replacement run IDs; requested hold or explicit omission, effective seconds, original acceptance time/absolute UTC expiry and actual end time/reason |
| Deadlines and result | Separate startup/execution deadlines and origins, actual durations and justified overrides; first terminal outcome/time (`passed`, `failed`, `cancelled`, `withdrawn`), withdrawal cause, owned-work/access termination results, retention/cleanup disposition, stopping failures/leftovers and diagnostic references |

Story 2.2 MUST reserve candidate and loaded-identity fields even while producers
are unavailable; missing values cannot qualify evidence. Story 2.6 MUST apply
compatibility, request/expiry and owner-state checks before accepting attachment.
Story 2.7 MUST implement withdrawal ordering, first outcomes and local failure
retention without extending expiry. Story 3.1 MUST apply candidate selection,
decision binding and no-reuse checks. Builds must record its runner/workflow
adoption against an exact tool version, with Platform acceptance and integration
qualification; this publication supplies none of those implementation claims.

## Decision accountability and revisit conditions

| Decision | Accountable owner | Rationale | Concrete revisit trigger |
| --- | --- | --- | --- |
| Committed PR merge result gates the exact head/base merge decision | Platform Maintainer, `@jpiquot`; Builds owns runner/workflow adoption | Exercise the committed combined inputs that the merge decision concerns | A merge queue, merge strategy or workflow change cannot bind the tested merge result to the exact pair, or release revision semantics change |
| No evidence reuse, including unchanged modules | Platform Maintainer, `@jpiquot`; Builds owns evidence enforcement | Avoid treating stale composition, dependencies or identity observations as a new decision's proof | Qualified repeat-run cost prompts a proposal that proves safe decision/composition/content lineage for reuse before any consumer enables it |
| Default 1,800-second hold, maximum 14,400 seconds, shorter positive requests and immutable expiry | Platform Maintainer, `@jpiquot`; Builds owns attachment lifecycle adoption | Bound resource occupancy while allowing a shorter explicit hold or a bounded longer suite | Lifecycle qualification shows valid suites exceed the default/maximum, resource pressure requires a shorter limit, or CI job limits conflict with the chosen holds |

A trigger requires an explicit Platform decision; it does not waive v1. Published
policy identifiers and their meaning MUST remain immutable. Any policy change
requires a new version/identifier, recorded Platform ownership, explicit Builds
and consumer version adoption, and requalification of affected candidate,
attachment and lifecycle paths before acceptance. Consumers MUST NOT select
"latest", silently fall back or reinterpret evidence under the new policy. Prior
evidence stays labelled with its original policy; it cannot satisfy a new
decision by relabelling. Existing release authority and staging gates still apply.

## Acceptance and rejection examples

| Situation | Required disposition |
| --- | --- |
| Run passes against committed merge result M for head H/base B with all expected/loaded identities matching | Eligible only for its bound merge decision for H/B, subject to all other integration gates |
| Head H or base B changes after the run; an unchanged module still has the same version/content | Refuse old evidence; build the new committed merge result and run again |
| Release revision R differs from M, or a separate release decision asks to reuse M's result | Require a fresh run bound to that release decision and its exact revision; retain existing release/staging gates |
| Candidate extension identity is absent, McpCli loaded another build, or inputs contain local edits | Refuse candidate evidence, even with passing tests and matching package versions |
| A `baseline-only` result passes, or an unchanged module supplies another decision's result | Refuse candidate acceptance; no cross-decision reuse in v1 |
| Same pending gate request retries, or a PR reopens with the same head/base | Retry keeps its decision identity with a distinct run/attempt; reopened PR needs a new gate-issued decision and fresh run |
| Tool qualification is missing/inapplicable, or a passing result ran a wrong, empty, required-skipped or incomplete suite | Refuse candidate evidence; an unqualified tool also cannot accept attachment |
| Attachment accepted at 12:00 UTC with no hold request | Effective hold 1,800 seconds, absolute expiry 12:30 UTC |
| Attachment accepted at 12:00 UTC with 300 seconds or 14,400 seconds requested | Effective hold equals request; expiry 12:05 UTC or 16:00 UTC respectively |
| Request is zero, negative, non-finite, malformed or 14,401 seconds | Reject attachment; no hold, defaulting or clamping |
| An attachment reaches expiry while tests continue, or retries/reconnects under a replacement run ID or suite label | Compare original request lineage, preserve original expiry, withdraw and stop owned work/end access at expiry; report stopping failures without evidence or extension |
| Owner has succeeded with a valid attachment still running | Record owner success immediately, refuse new attachments and delay automatic local cleanup until the hold ends |
| Owner finishes after a local failure and surviving environment is ready and compatible | A new explicit finite attachment is allowed while failure-retained; prior failed evidence stays failed |
| Owner cancels or environment is explicitly stopped before expiry | Withdraw active attachments first; cancellation keeps prior local failure retention, explicit cleanup may remove it |
| Attached local failure is followed by hold expiry or owner cancellation | Preserve the first failed outcome and surviving debug resources; no later event clears retention |
| CI job ends at 12:10 UTC despite an attachment expiring at 12:30 UTC | Withdraw the active attachment at job end, clean owned resources and report leftovers; no surviving hold |
| Hosted target, another CI job, incompatible identities/isolation, unsupported policy or completed non-retained owner | Refuse attachment before acceptance |

These examples are downstream qualification obligations. Documentation checks
prove policy consistency and discoverability only; no workload was started and no
integration, cleanup, artifact-identity or runner-adoption evidence is claimed.
