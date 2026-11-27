# Hexalith Platform architecture validation

27 November 2026 · Platform initiative spine · Reviewer gate

**Changes required.** The November 27 plan puts the currency evaluator on the first-release path. An adopted handoff still requires the later drill caller, and the admission, profile and attempt-record contracts leave implementation teams with incompatible rules.

## High findings

### H1 · The adopted profile handoff still makes the first release wait for its own drill

**Align the handoff before implementing Stories 6.14 and 9.2.** The spine requires the currency evaluator at the first approved production admission. Story 8.17 can use it only after Story 6.23 creates working production. The hosted-profile v2 contract and adoption record still put both admission and drill callers before the live evaluator. A team following that handoff cannot deliver the evaluator in time for the first release.

Publish a dated, explicit supersession for the delivery order and cross-reference it from the spine and Stories 4.9, 6.14, 8.17 and 9.2. Retain the byte-bound v2 adoption record and its structural format and authority requirements. Qualify admission first, schedule the weekly caller when production is working, and add the drill caller with 8.17. [Spine L380](../../ARCHITECTURE-SPINE.md#L380) · [Adopted profile handoff L106](../../../../../../docs/contracts/hosted-profile-binding-v2.md#L106) · [Adoption record L48](../../../../../../docs/contracts/hosted-profile-v2-adoption.json#L48) · [Story 8.17 L4636](../../../../epics.md#L4636)

### H2 · Story 6.14 can refuse the first approved production installation

**Correct the admission acceptance criteria.** The spine makes two narrow allowances for an eligible approved empty or degraded attempt. An approval can cover the exact observed stop revision, and an approved first empty installation has no working baseline to smoke-test. Story 6.14 lists a clear stop and passing working-baseline smokes for every release attempt. Story 6.23 then delegates its first empty admission to 6.14.

Apply the spine's conditions to both preconditions in Story 6.14 and state when the retained-data requirement applies. Rehearse an approved first installation without a baseline, an otherwise eligible repair at its observed stop revision, and refusal when a newer stop arrives before start. Retain the common admission gates, including the attempt's own currency result. [Spine L368](../../ARCHITECTURE-SPINE.md#L368) · [Story 6.14 L3614](../../../../epics.md#L3614) · [Story 6.23 L3918](../../../../epics.md#L3918)

### H3 · Currency inputs and results have no complete authority contract

**Agree on the read and write authorities across the three callers.** Policy v1 requires current authenticated observations across the management and workload clusters, actual component instances and their nodes or replicas. AD-7 assigns namespace-scoped deployment authority and reserves cluster-scoped change authority for named infrastructure workflows. The production executor, drill and scheduler have no common read-only observation path or defined currency-result writer. For admission, an earlier evaluation and the final evaluation may also share the same attempt ID, mode and epoch.

Define the scoped read-only observation path and authenticated result writer, verifier and retention authority for each trigger. Define how the signed admission evidence identifies the exact final check and approval disposition used. Rehearse multiple retained evaluations for one attempt and refusal when the authoritative final result is unavailable. [AD-7 L139](../../ARCHITECTURE-SPINE.md#L139) · [Record writers L249](../../ARCHITECTURE-SPINE.md#L249) · [Policy comparisons L189](../../../../../../docs/contracts/shared-infrastructure-currency-policy-v1.md#L189) · [Required result record L234](../../../../../../docs/contracts/shared-infrastructure-currency-policy-v1.md#L234) · [Story 9.2 evidence constraints L29](../../../../../implementation-artifacts/spec-9-2-run-the-shared-infrastructure-currency-check.md#L29)

### H4 · A currency refusal lacks a terminal outcome that preserves deployed state

**Resolve the attempt outcome contract with Builds.** A currency refusal occurs under the lock and preserves the exact promotion stop. The spine and stop monitor treat a non-working terminal outcome as a stop cause, and the production baseline classification also uses that outcome. The current attempt codec provides working, non-working and superseded terminal outcomes. The ordinary attempt-store revision releases the lock on a terminal outcome and excludes supersession. The admission writer and the stop monitor therefore need a common way to recognize a refused pre-mutation attempt.

Define a distinguishable durable refusal outcome or phase in the shared record contract. Qualify lock release, an unchanged working pointer and stop revision, and a later fresh admission. Keep a genuine failed baseline health check subject to its own stop and degradation rules. [Spine L343](../../ARCHITECTURE-SPINE.md#L343) · [Promotion stop L356](../../ARCHITECTURE-SPINE.md#L356) · [Attempt outcome L31](../../../../../../references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/AttemptRecord.cs#L31) · [Attempt store L29](../../../../../../src/Hexalith.Platform.AttemptStore/AttemptStore.Attempts.cs#L29) · [Story 6.10 L3496](../../../../epics.md#L3496)

### H5 · Admission uses a profile digest without identifying its domain

**Name both digest domains and their consumers.** The spine describes the infrastructure digest when it defines what the profile digest covers. It uses the generic term for staging-to-production equality, active profile evidence and qualification. Hosted-profile v2 also defines a resolved digest over the full environment-specific document. EventStore's promotion authority binds both. Two teams can choose different valid profile hashes for the same check and mishandle environments whose invariant pins agree but whose declared local bindings differ.

Use the infrastructure digest for comparing invariant staging and production profiles. Require the target's exact resolved digest where the owner contract binds the deployed profile and specify which identities each qualification, attempt and currency record retains. Exercise equal infrastructure with different valid resolved bindings and refusal on wrong target bindings. [Spine L254](../../ARCHITECTURE-SPINE.md#L254) · [Precondition 6 L375](../../ARCHITECTURE-SPINE.md#L375) · [Digest domains L84](../../../../../../docs/contracts/hosted-profile-binding-v2.md#L84) · [EventStore promotion bindings L185](../../../../../../references/Hexalith.EventStore/docs/architecture/publication-authority-v1.md#L185) · [Policy inventory evidence L247](../../../../../../docs/contracts/shared-infrastructure-currency-policy-v1.md#L247)

### H6 · The executable checklist omits the working baseline's refreshed qualification

**Make precondition 6 enforce both parts of the profile rule.** The Production profile row requires an affected working production baseline to have both its renewed qualification and production-promoted records after an infrastructure or environment-layer change. The numbered checklist and Story 6.14 check the candidate's qualifying sets and applied environment. A new release can meet that checklist while one of the working baseline's required records is missing or stale.

Require both renewed baseline records when the working baseline was affected. Keep the candidate's production records issued at rollout, including at first installation. Rehearse refusal when either baseline record is absent and first installation when no baseline exists. [Production profile L254](../../ARCHITECTURE-SPINE.md#L254) · [Precondition 6 L375](../../ARCHITECTURE-SPINE.md#L375) · [Story 6.14 L3622](../../../../epics.md#L3622)

## Other findings

### M1 · G3 can be audited without finding a missing admission result

**Medium · Complete the rehearsal and audit criteria.** Story 9.6 describes a generic failed currency check. Policy v1 also assigns it current, drifted, unsupported, overdue, unknown-data and unqualified-pairing cases, plus both release modes, approval naming and exact stop preservation. Story 9.8 and SM-C1 count compatibility and G3 in the promotion history. Add the required rehearsal cases and reconcile each automatic promotion with its retained passing admission result. [Spine G3 handoff L572](../../ARCHITECTURE-SPINE.md#L572) · [Policy obligations L276](../../../../../../docs/contracts/shared-infrastructure-currency-policy-v1.md#L276) · [Story 9.6 L4893](../../../../epics.md#L4893) · [Story 9.8 L4948](../../../../epics.md#L4948) · [SM-C1 L404](../../../../prds/prd-platform-2026-09-27/prd.md#L404)

### M2 · The drill does not identify which deployment supplies currency evidence

**Medium · Decide the target and collection boundary before Story 8.17.** The restored workloads run with egress denied and the drill allows substitutions. The caller must observe exact deployed versions and obtain current upstream evidence. Identify the deployment and inventory the check evaluates, where it accesses upstream, and how substitutes and unavailable observations are recorded. Keep the evaluator outside restored application workloads and preserve the drill's isolation and the check's advisory effect. [DR evidence L361](../../ARCHITECTURE-SPINE.md#L361) · [Drill currency L568](../../ARCHITECTURE-SPINE.md#L568) · [Story 8.17 L4645](../../../../epics.md#L4645) · [Policy triggers L56](../../../../../../docs/contracts/shared-infrastructure-currency-policy-v1.md#L56)

### M3 · Lost-window applicability needs evidence from the recovery history

**Medium · Define the evidence for both AR-44 branches.** The spine and stories now agree that a missing required report blocks stop clearance or approval. They leave the consumer to determine whether qualifying recovery occurred, including an interrupted restore whose report was never completed. Bind the recovery history used for that determination to the relevant attempt and stop context. Rehearse incomplete or unavailable history alongside a proven no-recovery case. [Spine L356](../../ARCHITECTURE-SPINE.md#L356) · [Story 6.10 L3516](../../../../epics.md#L3516) · [AR-44 change L38](../../../../sprint-change-proposal-2026-11-27.md#L38)

### M4 · Profile acceptance still depends on an unmeasured identity-event bound

**Medium · Resolve the existing dependency before Story 4.9 acceptance.** The 4.9 readiness record identifies a loop between measuring the event capture lag on the staging export path and qualifying that path in 4.19 against the approved bound. Story 9.2 now depends on 4.9 on the first-release path. Assign a measured, isolated pre-qualification slice to produce the evidence for the bound, then qualify the actual staging path against it. The existing measurement proposal describes the required observations. [Story 4.9 handoff L108](../../../../../implementation-artifacts/spec-4-9-ratify-the-hosted-runtime-and-identity-event-contracts.md#L108) · [Story 4.19 L2725](../../../../epics.md#L2725) · [Measurement proposal L100](../../../../../../docs/implementation/identity-event-capture-lag-measurement-proposal.md#L100)

### M5 · The executor's Helm pin needs a separate compatibility check

**Medium · Put support and version skew into the toolchain gate.** The spine carries a Helm 4.2.0 floor and Story 4.5 qualified that exact version on its test setup. The currency policy excludes deployment tools. Helm's published compatibility range for 4.2.x ends at Kubernetes 1.36, so satisfying the floor alone cannot qualify the pin for a newer cluster. Story 4.9 should require the same supported Helm release on each executor and qualify it against the exact Kubernetes target. [Spine stack L413](../../ARCHITECTURE-SPINE.md#L413) · [Story 4.9 L2348](../../../../epics.md#L2348) · [Policy scope L50](../../../../../../docs/contracts/shared-infrastructure-currency-policy-v1.md#L50) · [Helm support and skew](https://helm.sh/docs/topics/version_skew/)

## Qualification status

The checkout source-selection function refuses the current tuple at EventStore. The four mapped owner revisions differ from the committed gitlinks. The local Builds `v6.9.0` tag also peels to a different commit from the Builds gitlink. Reconciliation and qualification belong to Story 2.14. This is an existing implementation blocker separate from the architecture findings. [Reality review](review-reality.md) · [CI source selector](../../../../../../eng/current_tuple_source.py)

Kubernetes upstream confirms the spine's October 27, 2026 end-of-life date for 1.34. Repository evidence for Dapr 1.19.2 covers the accepted Story 4.5 chart qualification at exact pinned inputs. The policy v1 bytes match the revision and SHA-256 frozen by Story 9.2. The ratified Story 4.9 inventory, live currency callers and production compliance remain delivery evidence. [Kubernetes 1.34 release status](https://kubernetes.io/releases/1.34/) · [Story 4.9 qualification handoff L80](../../../../../implementation-artifacts/spec-4-9-ratify-the-hosted-runtime-and-identity-event-contracts.md#L80) · [Story 9.2 frozen policy identity L20](../../../../../implementation-artifacts/spec-9-2-run-the-shared-infrastructure-currency-check.md#L20)

## Validation evidence

- BMad mechanical lint returned zero findings. AD-1 through AD-15 and the full PRD FR/NFR map were present. All four Mermaid diagrams parsed and rendered. [Lint results](lint.json) · [Reviewed spine](../../ARCHITECTURE-SPINE.md)
- The three independent reviewers assessed the architecture rubric and source coverage, implementation and upstream currency, and cross-team compatibility. The synthesis prioritizes admission, state transitions and evidence that can change delivery or release outcomes. [Rubric](review-rubric.md) · [Reality](review-reality.md) · [Adversarial](review-adversarial.md)
- Review basis was the November 27 working tree at Platform base `f87f1593ca07ce617df50afb745a1995fde4b1e0`, including pending planning edits. Spine SHA-256 was `25bf177226ad19e04a3c15c3522d668f62c9289af682a240f02d701f5bb13a5f`. The spine and its decision log were preserved byte-for-byte. Live admission, deployments, drills and the integration suite were outside this document validation.

[HTML report](validation-report.html)
