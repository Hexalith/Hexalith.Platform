# Sprint change proposal for the platform architecture validation

19 November 2026 · **Approved by the owner** · Moderate scope · Combined review

Reviewed revision `54d90be3b661591048640d3585a8eab4aea0e534`

## 1. Approved decision

On 19 November 2026 the owner answered “approve” to the combined V1–V9 proposal, including the two release-policy decisions. Production admission reserves the promotion decision for the exact attempt (V5). When the exact candidate adds no idempotent command, a representative existing command can supply the rollback witness with catalog and baseline-readback evidence (V8). Both rules require explicit Builds and Platform consumer adoption and qualification under [staging evidence policy v2](../../docs/contracts/staging-evidence-policy-v2.md). [Architecture validation report](architecture/architecture-platform-2026-09-27/reviews/validate-2026-11-19/validation-report.md) · [Staging policy ownership and versioning](../../docs/contracts/staging-evidence-policy-v1.md#decision-accountability-and-revisit-conditions)

The trigger was an architecture review on November 19. The structural linter passed all 15 decisions. The saved application render nevertheless contains eight Dapr Components owned by the wrong release tier, and different readers of the production checklist can reach different promotion verdicts for identical evidence. The earlier November 19 proposals cover the code audit and PRD acceptance decisions. This proposal builds on both. [Recorded chart render](../implementation-artifacts/evidence/4-5/normal.yaml) · [Code audit proposal](sprint-change-proposal-2026-11-19.md) · [PRD acceptance proposal](sprint-change-proposal-2026-11-19-prd-acceptance.md)

## 2. Impact on the sprint

| Area | Owners and delivery impact |
| --- | --- |
| Epic 4 · Chart, intake, publication and retention | Platform corrects the selected maintained chart and its qualification fixture in 4.5. The environment writer supplies the forward-only objects. Builds, EventStore and Memories supply image attestations for 4.8 intake. Stories 4.12 and 4.13 own publication and off-site restoration evidence. |
| Epic 5 · Release evidence | Platform Maintainer owns policy decisions. Builds implements tool and workflow support. Platform owns the staging executor and promotion evaluator. Adjust criteria in 5.5, 5.6 and 5.8–5.11. Administrator and EventStore participate in reset and recovery qualification. Story 5.2 already records the adopted vulnerability decision. |
| Epic 6 · Production admission | Story 6.14 consumes the same verdict under the production lock. Story 6.16 supplies the working-baseline transition. Applicable approved retained-data attempts consume the rehearsed restore under existing eligibility rules. |
| Epics 2, 8, 9 and 10 | Story 2.14 owns the separate dependency blocker below. Retain Epic 8’s DR distinction, reuse the affected SM-5 cases in Epic 9 before automatic promotion and in the Epic 10 repeat. Epics 1, 3, 7, 11 and 12 have no new delivery work from these findings. |
| PRD, spec and UX | Preserve the product scope and the existing FR/NFR IDs. Mirror FR-6/FR-8 and NFR-1 acceptance in the spec and epic summaries. Operators get explicit release refusal reasons in existing evidence. No screen, user journey or design asset is required. |

The current sprint tracker lists 4.5, 5.1 and 2.14 in progress, 5.2 done, and the affected Epic 5 execution stories in backlog. The plan fits the existing story IDs. A historical qualification result remains evidence for its recorded inputs. Closing an amended criterion requires evidence against the correction. [Sprint tracker](../implementation-artifacts/sprint-status.yaml) · [Epic and story definitions](epics.md)

## 3. Approved approach and sequencing

Use the existing story owners and gates. An implementation rollback would discard the maintained-chart fallback that solved the measured Aspire export problem. Reducing the MVP would remove required compatibility or recovery coverage. Neither addresses the ownership conflict or defines the two missing acceptance decisions. [Chart fallback measurement](../../eng/helm-qualification/README.md) · [Product requirements](prds/prd-platform-2026-09-27/prd.md)

1. The Product Owner and Platform Architect synchronize the spine, spec and story criteria for the seven settled items. Story 4.5 can proceed independently of consumer adoption of the two approved policy choices.
2. Record the approved V5 and V8 rules under a new staging policy identifier. Bind the exact policy revision in Builds and Platform, retain the policy ID on every result, and qualify both the new cases and the existing rejection cases before consumers use the new version. Evidence produced under v1 keeps its original meaning.
3. Qualify chart ownership before hosted use, module image attestations before publication, and the scan, recovery and concurrency cases before the affected production decision. Repeat affected SM-5 cases before using automatic promotion. [Spec gate sequence](../specs/spec-platform/sequencing.md) · [Staging policy](../../docs/contracts/staging-evidence-policy-v1.md)

**Planning estimate.** Allow one to two engineering days to align the documents and get the policy wording reviewed. Chart separation and isolated qualification are a medium engineering task. Scheduling cross-repository adoption and release rehearsals depends on Builds, Platform and available qualification environments. The affected publication and promotion gates carry that schedule risk. This estimate is for planning and does not set a release date.

## 4. Approved edits and acceptance cases

### V1 · Fix Dapr Component ownership in the application chart

**Current.** AD-8 and the release-tier table assign every Dapr Component to the environment identity. The application chart emits Components, its README calls them application-owned, and its test requires one per graph capability.

**Approved change.** Add to Story 4.5: “Across normal, recovery and remove-workloads renders, the application package contains zero Dapr Components. The environment renderer derives their names, scopes and provider bindings from the existing graph and profile and applies the required baseline/candidate union before application deployment.” Keep application-owned Configurations, Subscriptions and other application objects in their assigned tier. Update the chart template, harness, README and tests together. Keep the environment’s write permissions with its identity.

**Accept when** the same reference composition produces all eight expected Components in the environment output and zero in each application mode. An isolated install, upgrade, recovery and removal with the intended identities preserves their ownership and the required bindings. Capture fresh qualification evidence alongside the historical result. [AD-8 and release tiers](architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#release-tiers) · [Application template](../../eng/helm-qualification/chart/templates/dapr.yaml) · [Current test](../../eng/helm-qualification/test_chart.py) · [Story 4.5](epics.md#story-45-qualify-the-aspire-to-helm-export-with-the-reference-composition)

### V2 · Require the adopted vulnerability decision at production admission

**Current.** The spine assigns the severity threshold as future work. Story 6.14 and the spine’s common production checklist omit explicit consumption of the scan verdict.

**Approved change.** Extend existing precondition 4 in the spine and 6.14: “At admission in either release mode, require the qualifying staging E2E result **and** an independently qualifying scan for this pending decision under the adopted vulnerability policy.” Story 5.5 produces the latest-issued, complete image-inventory scan. Story 5.11 verifies all image identities, scan age and current exception state. Mirror the handoff in the spec sequencing table and Epic 5 context. Cite the adopted policy for the threshold and exceptions.

**Accept when** an unexcepted High, Critical or unresolved Unknown result refuses both modes. A newer incomplete scan blocks fallback. Each image report at its exclusive 24-hour deadline refuses even with fresh staging evidence. A valid, exact Administrator exception for a finding still requires complete, current scans and every other admission check. [Adopted vulnerability policy](../../docs/contracts/image-and-dependency-vulnerability-policy-v1.md) · [Story 6.14](epics.md#story-614-refuse-production-attempts-that-fail-a-precondition) · [Epic 5 context](../implementation-artifacts/epic-5-context.md)

### V3 · Select the recovery rehearsal using the release path

**Current.** The spine prescribes a rollback-set rehearsal whenever a production baseline exists. Story 5.9 and the published staging policy specify a separate rehearsal for an approved retained-data attempt without valid compatibility evidence.

**Approved change.** Replace the blanket instruction in the staging gate, the spec’s CAP-6 acceptance summary and related catalog wording with the policy’s branch table. An empty first installation takes the fresh-install path. A compatible baseline takes the AD-15 rehearsal. An approved retained-data attempt without valid compatibility must supply complete candidate E2E and a separately rehearsed named data restore, with no automatic rollback set. For the empty/degraded exception, also require its fresh-install evidence and, whenever data remain, applicable compatibility or named-recovery evidence. The compatible-path diagram should keep its scope explicit. Retain production precondition 9 and the actual recovery point cut after the production lock.

**Accept when** a plan or fresh-install pass alone is refused for incompatible retained data. Staging must demonstrate restoration with the identified retained inputs, authority, checks, maximum duration and treatment of post-cut changes. Stories 5.7–5.9 supply the applicable branch, 5.11 evaluates it and 6.14 enforces it. [Staging preparation rules](../../docs/contracts/staging-evidence-policy-v1.md#serialization-and-preparation-branches) · [Story 5.9](epics.md#story-59-qualify-the-named-recovery-for-a-retained-data-approved-attempt) · [Spec acceptance criteria](../specs/spec-platform/acceptance-criteria.md)

### V4 · Reset staging before a retry can reuse incompatible data

**Current.** The spine’s reset rule and Story 5.10 describe a later candidate. The in-place recovery wording can identify a later retry’s already-contaminated recovery point.

**Approved change.** Write into the staging reset, in-place recovery, pruning and 5.10 criteria: “After unreadable or unproven writes, require a verified reset before either a same-candidate rerun or a later candidate starts. Restore the earliest point for that candidate since the last verified reset, taken before any of its writes.” Preserve the point and artifacts for open recovery references even after evidence expiry. Preserve the existing exception for writes proven baseline-readable and the existing recovery-entry rules.

**Accept when** a retry of A and a subsequent candidate B both wait for verification after A’s incompatible writes. A later snapshot containing A’s writes is rejected. Missing baseline or readability proof triggers the same reset, and a failed reset keeps the queued attempt blocked. [Policy reset cases](../../docs/contracts/staging-evidence-policy-v1.md) · [Story 5.10](epics.md#story-510-reset-staging-for-unadopted-incompatible-candidates)

### V5 · Order staging supersession and production admission

**Current.** A can pass production admission under the production lock. Staging can then start B, supersede A and begin a reset before A’s rollout updates the latest-working pointer. The trusted gate can be asked to record two incompatible outcomes.

**Approved decision.** Give the Platform promotion evaluator and staging executor one durable, conditional ordering point for successful production admission versus a staging start or reset that would supersede A. Bind the decision to the exact release, production attempt and epoch. The authorized writer records the reservation after all preconditions pass and before the first rollout effect. A queued request or a partially passed check creates no reservation. Keep the existing explicit Administrator reservation as a distinct input. Record the responsible signer and conditional writes in Stories 4.6/4.7 and 5.6. No staging worker holds both environment locks.

| Acceptance case for Stories 5.10, 5.11, 6.14 and 6.16 | Required result under policy v2 |
| --- | --- |
| B wins the ordering point while A is pending and unreserved | The supersession commits once. A’s admission loses and refuses before mutation. Apply the staging reset rule to B. |
| A wins admission first | Queue conflicting staging work. A stays pending and reserved to that production attempt. Successful verification and the working-pointer transition complete A exactly once. B reevaluates against the new baseline and obtains fresh applicable evidence. |
| A fails or its executor disappears after admission | A non-working terminal outcome releases the reservation and leaves the decision pending, subject to the existing stop and recovery rules. A retry repeats admission. An interrupted or uncertain attempt retains its reservation until fenced takeover or terminal reconciliation. Queued staging work rechecks the authoritative decision and recovery state before any reset or supersession. |

Implement the pending reservation as part of the existing decision/attempt state. Replay of the same transition must be idempotent and an old epoch must be refused. The tradeoff is a wait for conflicting staging work during production rollout. The alternative would allow that work to proceed while defining a separate, more complex cancellation and recovery path for an admitted release. [Story 5.1 deferral](../implementation-artifacts/spec-5-1-decide-the-staging-evidence-policies.md) · [Policy decision states](../../docs/contracts/staging-evidence-policy-v1.md) · [Story 6.16](epics.md#story-616-roll-out-and-verify-a-production-release)

### V6 · Require module image attestations at first intake for publication

**Current.** The spine and spec handoff date owner attestations to first staging promotion. Stories 4.8 and 4.12 already require them before the release record is published.

**Approved change.** Move the handoff to first admission for publication. Builds supplies the attested release path. EventStore, Memories and applicable module owners supply digest-bound attestations. Platform verifies the image digest, owner repository, workflow, protected ref and pinned Builds workflow identity.

**Accept when** missing or mismatched attestation refuses intake before publication can write a release record. A later passing vulnerability scan cannot substitute for the attestation. [Story 4.8](epics.md#story-48-maintain-the-module-intake-manifest) · [Publication story](epics.md#story-412-publish-retained-releases-with-provenance) · [Spec sequencing](../specs/spec-platform/sequencing.md)

### V7 · Assign each Helm proof to the stage that can produce it

**Current.** The architecture handoff combines chart rendering, OCI attestation and retained-package rollback. Story 4.5 runs before published or retained artifacts exist.

**Approved change.** Record the measured fallback to the maintained chart. Story 4.5 owns deterministic rendering, resource ownership, admission and local chart qualification, including V1. Story 4.12 owns the published package’s OCI digest and attestation. Story 4.13 owns off-site retention and retrieval of the same package by digest for recovery. Update the handoff and spec sequencing to identify all three gates.

**Accept when** 4.5 can demonstrate its amended criteria before publication. Qualification of publication and retained-package recovery each cites the artifact and evidence from its own stage. The old chart evidence cannot close V1. [Chart qualification boundary](../../eng/helm-qualification/README.md) · [Epic 4 sequence](epics.md#epic-4-publish-retained-releases-and-run-the-isolated-staging-environment) · [Story 4.13](epics.md#story-413-retain-artifacts-off-the-primary-failure-domain)

### V8 · Define the rollback witness when the candidate adds no idempotent command

**Current.** PRD NFR-1 and the spec’s NFR-1 text say “where applicable.” The spec’s CAP-6 summary, Story 5.8, Epic 5 context and staging policy require a candidate-new command unconditionally. A dependency-only release cannot satisfy that condition.

**Approved decision.** Keep the PRD’s conditional rule. Compare the exact baseline and candidate command catalogs and their contract identities. If a new idempotent command exists, the rehearsal exercises one. If the comparison proves none was introduced, the module’s suite names a representative existing idempotent command that exercises the affected write path. Record both catalog identities, the comparison, the command and the candidate-written state and events the baseline successfully reads after rollback. An unknown catalog delta, missing suitable command or failed readback refuses automatic compatibility. Full E2E and the remaining AD-15 checks still apply.

**Accept when** the new-command and dependency-only cases pass with their appropriate evidence, and each missing-proof case refuses. Align the NFR-1 and CAP-6 wording in the PRD, spec, epic requirements, spine and 5.8. Adopt the changed rule explicitly with V5. The alternative is to make every command-preserving release ineligible for automatic promotion and change the product requirement accordingly. [PRD NFR-1](prds/prd-platform-2026-09-27/prd.md#nfr-1-preserve-data-through-application-rollback) · [Spec acceptance criteria](../specs/spec-platform/acceptance-criteria.md) · [Story 5.8](epics.md#story-58-rehearse-the-rollback-set-and-record-compatibility-evidence)

### V9 · Identify the source of current tool versions

**Current.** The stack table marks the September seed as current. At the reviewed revision the checkout specifies .NET SDK 11.0.100, Aspire AppHost 17.0.0 and a prepared `hexalith-module` 6.3.2.

**Approved change.** Label the September table as historical and point builders to the version manifests and CLI instructions. Keep the Story 4.5 probe versions with their dated evidence. Refer tool acceptance to the Story 2.10 and 2.14 gates.

**Accept when** a reader following the spine reaches the version used by the checkout, can identify the version used in the historical probe, and can find the evidence still required for tool acceptance. [Current SDK](../../global.json) · [AppHost](../../apphost.cs) · [Tool manifest](../../.config/dotnet-tools.json) · [CLI requirements](../../README.md#requirements)

## 5. Handoff and completion

| Delivery owner | First action and completion evidence |
| --- | --- |
| Product Owner and Platform Architect | Amend the cited architecture sections, spec, epic summaries and existing story criteria. Record V5 and V8 as owner decisions. Repeat architecture validation and trace each of V1–V9 to the corrected text or to an owned implementation gate. |
| Platform chart and environment owners | Implement V1 and the 4.5 part of V7. Verify ownership using the same reference graph, all chart modes and the intended identities. Preserve prior runs and identify the revision of the new proof. |
| Builds, Platform and module release owners | Exercise 4.8, 4.12 and 4.13 in order for V6/V7. Qualify the adopted scan checks in both release modes for V2. |
| Platform Maintainer, Builds, Administrator and EventStore | Record and version V5/V8, obtain explicit consumer adoption and execute the V3–V5 and V8 cases at their assigned gates. Planning approval and runtime qualification are separate milestones. |
| Story 2.14 owners | Continue the existing reconciliation of Builds, EventStore, Parties, Tenants and Memories. The prior validation’s local source-selection check rejected the EventStore revision. Publishing a new candidate requires the reconciled revisions to pass qualification. [Existing audit follow-up](epics.md#story-214-pin-builds-and-owner-modules-by-released-version) |

**Implementation handoff.** The architecture, PRD, spec and story criteria incorporate V1–V9. The independent review records the acceptance evidence for each finding and the remaining release gates. [Architecture review and story handoff](architecture/architecture-platform-2026-09-27/reviews/update-2026-11-19-course-correction/gate-summary.md)

**Workflow record.** Trigger and evidence (1.1–1.3), epic impact (2.1–2.5), PRD/architecture/technical impact (3.1, 3.2, 3.4), response options (4.1–4.4), proposal and handoff design (5.1–5.5), and proposal review (6.1–6.2) are complete. UX changes (3.3) and new sprint-status entries (6.4) are inapplicable. The owner approved the full proposal (6.3). The Product Owner, architecture and chart workstreams received the approved changes (6.5). External consumer adoption, live qualification and publication remain with the named story owners.
