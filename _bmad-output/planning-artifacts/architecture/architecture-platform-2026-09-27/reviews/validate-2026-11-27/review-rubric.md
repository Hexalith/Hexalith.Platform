# Rubric review · Hexalith Platform architecture

**Changes required. Three high and two medium findings.** The spine sequences the first-release, weekly and drill checks coherently. The adopted profile handoff, production observation authority and terminal admission behavior still leave incompatible implementation choices.

Reviewed the Platform spine's November 27 working-tree revision, Git blob `5a5d4d13c5ef020571e2951267de9f60c668ef80` based on `f87f1593ca07ce617df50afb745a1995fde4b1e0`, together with its paired Platform spec, PRD, epic criteria, policy v1 and repository evidence. The linter reported zero findings. The named modular composition paradigm, fifteen stable decisions and capability map cover the initiative's dependency, state, release, deployment, infrastructure and recovery boundaries. Below are the remaining inconsistencies that affect handoff.

## High findings

### Rubric 1 · The adopted profile handoff still tells Story 9.2 to wait for the drill

**High · Align the contracts · High confidence.** The spine requires Story 9.2's release-admission path with Story 6.14 before the first release in 6.23. Story 8.17 then integrates its drill caller after there is working production. The adopted v2 profile binding and its format adoption record still call the admission and drill callers prerequisites to the live Story 9.2 checker. Story 9.2 directly points its implementer to both artifacts. Following that handoff literally restores the first-release dependency cycle addressed by the November 27 proposal.

**Action.** Explicitly supersede the obsolete delivery sentence in a current linked handoff or erratum, preserving the recorded adoption digest and that historical artifact. Point the spine and both consumer stories to the current sequence. Leave the v2 schema, canonical codec, independent coverage and authorization requirements intact.

[Spine L380 and L560](../../ARCHITECTURE-SPINE.md#L380) · [Story 8.17 L4636](../../../../epics.md#L4636) · [Story 9.2 draft L55 and L65–67](../../../../../implementation-artifacts/spec-9-2-run-the-shared-infrastructure-currency-check.md#L55) · [Adopted v2 binding L106–115](../../../../../../docs/contracts/hosted-profile-binding-v2.md#L106) · [Adoption record L48–66](../../../../../../docs/contracts/hosted-profile-v2-adoption.json#L48)

### Rubric 2 · No current observation authority is assigned across the shared fleet

**High · Decide the cross-team boundary before admission implementation · High confidence in the gap.** Current policy evaluation requires an authoritative read of actual deployed identity on every in-scope replica or node and of both Kubernetes clusters and the Rancher pairing. AD-7 gives ordinary production jobs environment-scoped deployment and smoke credentials, and gives the production executor a cluster-scoped identity only for named shared-infrastructure workflows. The first-release, weekly and drill handoffs do not assign a read-only management/workload identity or a trusted independent observation issuer. An implementer can preserve AD-7 and fail every live admission on missing evidence. Another can widen the deployment identity to obtain observations. The drill's isolated copies and upstream observations require their own declared source boundary.

**Action.** Under AD-7 assign ownership for a purpose-scoped, authenticated, read-only observation path used by the three callers. Make the target, authority and current-evidence binding explicit, including whether the drill checks its isolated deployment or uses observations collected outside that environment. Qualify refusal when an observation or independently bound coverage is unavailable and prove that the observer has no deployment or admission write authority.

[AD-7 L144–150](../../ARCHITECTURE-SPINE.md#L144) · [Release admission L380 and weekly/drill handoff L567–568](../../ARCHITECTURE-SPINE.md#L380) · [Policy v1 observation and comparison L191–227](../../../../../../docs/contracts/shared-infrastructure-currency-policy-v1.md#L191) · [Story 8.17 drill isolation L4650–4654](../../../../epics.md#L4650)

### Rubric 3 · A currency refusal needs a terminal state that preserves the working baseline

**High · Decide the attempt outcome contract · High confidence.** Admission takes the lock and current epoch before checking currency, and a currency refusal must preserve the exact stop state and revision. The spine separately sets a stop for every non-working terminal outcome and treats the last non-working outcome as degradation. The spec repeats both rules. Builds attempt records offer in-progress, working, non-working and superseded. The Platform store's ordinary revision releases the lock on a terminal outcome and disallows superseded through that transition. Under the current text a currency-only refusal can either leave an in-progress attempt holding the lock, or terminate as non-working and make a healthy production baseline appear degraded. Skipping a stop write alone does not resolve classification.

**Action.** Bind the no-mutation refusal to a distinct outcome or validated phase/refusal reason recognized by lock-release, stop and health readers. Require its terminal record and release of the lock with the working pointer and stop unchanged. Retain the independent failed-health-check stop rule. Qualify a refusal over a healthy baseline and a subsequent eligible retry using a new epoch.

[Spine L343 and L356](../../ARCHITECTURE-SPINE.md#L343) · [Spine release-admission rule L380](../../ARCHITECTURE-SPINE.md#L380) · [Platform acceptance criteria L158 and L166](../../../../../specs/spec-platform/acceptance-criteria.md#L158) · [Policy v1 L164–187](../../../../../../docs/contracts/shared-infrastructure-currency-policy-v1.md#L164) · [Builds attempt outcomes L31–59](../../../../../../references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/AttemptRecord.cs#L31) · [Platform attempt revision L29–53](../../../../../../src/Hexalith.Platform.AttemptStore/AttemptStore.Attempts.cs#L29)

## Medium findings

### Rubric 4 · The final admission decision does not identify the selected currency result

**Medium · Specify the signed evidence reference · High confidence.** Policy v1 gives each result a unique identity, retains earlier results and requires a current check. The spine permits re-evaluation before final admission if inputs or deadlines change. Multiple retained results can therefore legitimately have the same attempt ID, mode and epoch. The signed reservation and attempt-record rules do not state which exact completed result and approval disposition the evaluator accepted. A later auditor can join the signed decision to an earlier positive result, although the final evaluation failed.

**Action.** Bind the selected result's exact immutable identity and the approved-mode disposition, where used, in signed admission evidence referenced by the attempt or reservation. Retain all other invocations. Demonstrate an early pass followed by a re-evaluation at a changed input or deadline, with the final evidence selecting the governing result.

[Spine record classes L249](../../ARCHITECTURE-SPINE.md#L249) · [Spine reservation L340 and re-evaluation L380](../../ARCHITECTURE-SPINE.md#L340) · [Policy v1 L87–95 and L234–257](../../../../../../docs/contracts/shared-infrastructure-currency-policy-v1.md#L234)

### Rubric 5 · The G3 handoff can accept incomplete rehearsal and audit evidence

**Medium · Align epic qualification and success measures · High confidence.** The spine's G3 handoff requires Story 9.6 currency rehearsals. Policy v1 gives 9.6 six decision classes and requires both mode dispositions, named and unnamed failure handling, retained results and exact stop preservation. The Epic 9 rehearsal criterion only checks that a failed result blocks automatic promotion. Story 9.8's live automatic behavior requires a pass, but its retrospective audit and the PRD and spec SM-C1 measure enumerate compatibility and G3 without requiring evidence of the current attempt's currency pass. Completing the named story criteria can leave the policy's rehearsal evidence and historical proof incomplete.

**Action.** In Story 9.6 reference the policy's exact required cases and outcomes, and in Story 9.8 make those records an explicit G3 input. Include the exact current-attempt currency proof in the SM-C1 history audit. Check approved-mode dispositions against the bound approval and retained result.

[Spine Owned work L572](../../ARCHITECTURE-SPINE.md#L572) · [Policy downstream requirements L280–283](../../../../../../docs/contracts/shared-infrastructure-currency-policy-v1.md#L280) · [9.6 L4893–4895 and 9.8 L4935–4950](../../../../epics.md#L4893) · [PRD SM-C1 L404](../../../../prds/prd-platform-2026-09-27/prd.md#L404) · [Platform spec success measures L27](../../../../../specs/spec-platform/success-measures.md#L27)

## Prior findings checked

The scan gate now appears in common precondition 4. The named-recovery and same-candidate reset rules match the staging policy in the spine. The November decision reservation and command witness are defined in staging policy v2, with consumer adoption and qualification still explicit. Owner image attestation precedes publication and chart/OCI/off-site recovery proof are separated. The formerly failing Component ownership boundary matches the maintained chart source and has fresh isolated proof for the reviewed Story 4.5 fixture. The Stack table now dates its historical seed. These conclusions concern the recorded architecture and scope of the cited evidence.

[Spine release acceptance L310–380 and work L546–559](../../ARCHITECTURE-SPINE.md#L310) · [Chart template](../../../../../../eng/helm-qualification/chart/templates/dapr.yaml) · [Story 4.5 isolation and ownership evidence L269–271](../../../../../implementation-artifacts/spec-4-5-qualify-the-aspire-to-helm-export-with-the-reference-composition.md#L269)
