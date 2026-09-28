# Validation Report — Hexalith Platform

- **PRD:** `_bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/prd.md`
- **Rubric:** `.agents/skills/bmad-prd/assets/prd-validation-checklist.md`
- **Run at:** 2026-09-28T09:05:23.726568+00:00
- **Grade:** Fair
- **Findings:** 0 critical, 2 high, 3 medium, 0 low.

## Overall verdict

The current PRD is strong and ready to guide architecture/spec alignment and implementation planning: its shared-environment thesis, module ownership, acceptance outcomes and operating trade-offs are coherent. Production qualification remains substantial, but the document names its owners and gates and explicitly avoids claiming deployed readiness; no substantive rubric finding requires a product-policy correction.

The recovery and access review changes the consolidated gate: two high-priority contradictions with later accepted architecture decisions need reconciliation before recovery and deployment stories are finalized. The PRD promises broader revocation preservation during disaster restoration than the accepted recovery exceptions, and requires a healthy production baseline where architecture permits an Administrator-approved attempt to repair degraded production. The architecture decision log records user acceptance of both changes; the recommended action is to synchronize the PRD and addendum with those decisions.

Three medium findings remain: CI integration evidence does not explicitly bind the exercised module artifact to the candidate source revision; accepted attachment does not define the outcome when the environment owner finishes; and the addendum still describes obsolete operations-repository writer and deputy controls. The consolidated grade is Fair because of the two high findings. All seven rubric dimensions are strong when the PRD is assessed for internal coherence.

This validation assesses requirements and their consistency with the inspected architecture snapshot. It does not establish production qualification. PRD and addendum source text is unchanged. The already modified architecture and its decision log were inspected read-only; their hashes are recorded in validate-2026-09-28/source-snapshot.json. In finding locations, ARCHITECTURE-SPINE.md and architecture .memlog.md refer to _bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/.

## Dimension verdicts

- Decision-readiness — strong
- Substance over theater — strong
- Strategic coherence — strong
- Done-ness clarity — strong
- Scope honesty — strong
- Downstream usability — strong
- Shape fit — strong

## Dimension judgments

### Decision-readiness — strong

The PRD makes consequential choices directly. It limits the MVP to seven modules while allowing incremental enrollment without weakening final acceptance (`prd.md:40–48`), distinguishes automatic and Administrator-approved promotion (`prd.md:48–56`), and names real omissions such as traffic-metric rollback, standby infrastructure and a generic mocking subsystem (`prd.md:58–64`). Those choices provide usable boundaries for scope and release planning.

The recovery commitment is candid: “Outside declared response coverage, no four-hour recovery commitment is made” (`prd.md:301`), while RPO and monitoring remain continuous. Deputy recovery authority and Administrator-only promotion resumption are explicit (`prd.md:203–208`). The addendum preserves the alternatives and costs for both decisions (`addendum.md:216–225`, `258–267`), and the memlog records their acceptance (`.memlog.md:76–79`). These are accepted trade-offs, not unresolved policy questions.

### Substance over theater — strong

The opening identifies the concrete gap in the existing Works preview and connects it to complete and minimum environments, active-checkout debugging and hosted promotion (`prd.md:20–26`). User roles drive requirements: developers need source debugging, CI needs disposable isolated environments, and the recovery owner/deputy need distinct operating permissions (`prd.md:28–36`). There are no decorative personas or unsupported differentiation claims.

The NFRs describe the actual platform risks: preserving current data/security authority during rollback, bounding ordinary-data loss and response-dependent recovery time, and preventing staging authority from reaching production (`prd.md:282–309`). Exact budgets and refusal outcomes replace generic claims that the system should be reliable or secure. The document explicitly declines to invent an uptime or throughput commitment (`prd.md:348`).

### Strategic coherence — strong

The thesis is consistent composition across local development, integration testing and hosted releases while modules retain domain behavior (`prd.md:20–26`). FR-1 through FR-5 establish the development/testing foundation; FR-6 through FR-9 carry release verification and recovery into production; FR-10 through FR-12 preserve environment access and operation boundaries. Production gates separate infrastructure deployment, user opening and automated promotion without changing the final seven-module scope (`prd.md:46–56`).

Success measures test the stated outcomes rather than activity: source changes affect the running checkout, real integrations use the declared environment, isolation refusals hold, and recovery is demonstrated (`prd.md:313–320`). Counter-metrics explicitly prevent apparent success from hidden startup delay, destructive cleanup, bypassed validation or unverified restoration (`prd.md:324–328`).

### Done-ness clarity — strong

Every FR has observable consequences. Examples include explicit failure on missing/duplicate source identities (`prd.md:88–90`), a ten-minute startup default and defined lifecycle outcomes (`prd.md:122–139`), rejection of skipped/stale/wrong-release E2E evidence (`prd.md:161–169`), bounded production verification and one recovery attempt (`prd.md:183–208`), and positive as well as negative McpCli demonstrations (`prd.md:268–276`). Recovery acceptance includes security, erasure, data-integrity and response-time checks rather than treating a successful restore command as sufficient (`prd.md:220–228`, `294–301`).

Several concrete configurations remain to be qualified, including check declarations, evidence-age limits and coverage hours. Those deferrals have named owners, required boundaries and acceptance consequences (`prd.md:334–346`); the requirements fail closed when prerequisite evidence is absent. For this upstream technical PRD, requiring those artifacts before their relevant gate is a valid definition of done and does not require inventing module flow lists or parameter values centrally.

### Scope honesty — strong

The document separates product requirements from deployed evidence in its purpose statement (`prd.md:14`), describes initial policy defaults as unmeasured (`prd.md:193`), and reiterates that final document status does not pass production gates (`prd.md:332`). Non-goals identify omissions that would otherwise be easy to infer incorrectly (`prd.md:58–64`). Whole-site recovery and out-of-coverage outages have explicit limits (`prd.md:299–303`).

The memlog confirms that capability scenarios, the ordinary-data RPO envelope, deputy authority, coverage-bound RTO and downstream qualification were deliberate decisions (`.memlog.md:59`, `76–83`). There are no unresolved assumption or PM tags in the current PRD. Remaining module declarations and implementation evidence are tracked with owners and revisit conditions rather than silently represented as completed work (`prd.md:334–346`).

### Downstream usability — strong

Stable FR/NFR/SM IDs, explicit acceptance consequences, grouped capability sections and a glossary give architecture and story authors clear extraction boundaries. The glossary distinguishes enabled from enrolled modules, working release from working baseline, and rollback from disaster recovery (`prd.md:350–385`). The success measures identify the requirements and qualification moments they validate (`prd.md:313–328`).

The downstream table assigns composition, identity, operations, release control, recovery and document alignment to named owners (`prd.md:334–346`). It explicitly says that later PRD decisions govern stale downstream wording (`prd.md:346`), so such alignment is acknowledged work rather than an ambiguity in the product requirement. The addendum holds detailed mechanisms and option comparisons while preserving the PRD's product acceptance structure. This conclusion concerns source-extractability; a separate cross-document review must assess whether external architecture/spec wording has since converged or changed.

### Shape fit — strong

The capability-first shape fits an internal developer/operator platform. The document explains why it uses acceptance scenarios instead of inventing user journeys and leaves module business journeys with their owners (`prd.md:36`). Developer, CI and recovery jobs carry the necessary role context without a consumer-style persona apparatus.

The operational detail is earned by hosting production data, sharing infrastructure and owning release/recovery behavior. Keeping comparative options and implementation mechanisms in the addendum allows the PRD to remain organized around capabilities, operating commitments and verification. The historical implementation observations are explicitly dated and are not presented as evidence that the intended platform already exists (`addendum.md:10–22`).

## Findings by severity

### Critical (0)

None.

### High (2)

**[Recovery and access · S1]** — Restoration revocation guarantees omit the later accepted exceptions (prd.md:226, :227, :296, :303, :309; addendum.md:273, :275; ARCHITECTURE-SPINE.md:118, :295, :320, :321; architecture .memlog.md:220, :237, :250, :258)

FR-9 requires "reapply post-cut revocations" and "denial of revoked principals", while its ordinary lost-window exception enumerates only deletions, erasures and legal holds. NFR-3 repeats denial after revocation during restoration. The architecture accepts loss of module-owned revocations after the recovery cut and non-admission realm access removals after the durable event-export frontier. These were accepted in architecture C-25 (:220) and G-4 (:258); D5 (:237) records exceptions before reopening and requires Administrator review before promotion resumes. VAL-08 (:250) separately preserves production admission revocations. A restore can satisfy the accepted policy while failing the PRD, or builders can implement an unselected stronger guarantee.

Fix: Synchronize FR-9, NFR-2/NFR-3 references and addendum with the accepted scopes: production admission revocations survive restoration; the named module and realm removal exceptions use their respective lost windows and reporting/review boundaries. Preserve Memories continuity, live authorization and current-security application rollback. Keep implementation mechanisms in architecture.

**[Recovery and access · S2]** — The PRD blocks the accepted Administrator-approved recovery release path (prd.md:48, :168, :185, :204, :208; addendum.md:177, :193, :207; ARCHITECTURE-SPINE.md:280, :281, :288, :309; architecture .memlog.md:249, :252, :260)

FR-7 requires a currently healthy baseline before update and FR-8 requires a verified current working release to clear the stop. The architecture additionally allows an Administrator-approved candidate attempt when the last production outcome is non-working, waiving the healthy-baseline condition and observed stop only for that attempt. A later stop still dominates and successful verification establishes the new working baseline. Architecture VAL-04 (:249), the accepted decisions summary (:252), and accepted gate corrections (:260) document this evolution. The conflict concerns approved release into degraded production, not the already documented first deployment or manual recovery.

Fix: Carry the accepted approved degraded-production use case into release modes, FR-7/FR-8, addendum and SM-5 acceptance wording. State its one-attempt authorization and observed-stop boundary, newer-stop precedence, unchanged common gates, and verified-success requirement. Preserve Administrator-only release authorization and separate deputy recovery authority.

### Medium (3)

**[Adversarial review · A1]** — CI can exercise an older module package without rejecting evidence for the candidate revision (prd.md:90 (FR-2), prd.md:119-121 (FR-4 Composition and readiness), prd.md:313 (Success measures); addendum.md:34 (Workspace and build constraints))

Local integration explicitly uses the active source checkout; CI specifies Release/NuGet mode without explicitly binding the tested module artifact to the candidate revision. A correctly pinned Platform runner can test an older Parties package with real dependencies and pass the listed lifecycle/evidence requirements while never exercising the changed module. The addendum's exact Platform-tool commit binding does not establish the candidate module's artifact identity.

Fix: Require CI acceptance for a candidate to identify and exercise that module's Release artifact from the candidate revision and reject evidence for a different artifact/revision. Label intentional baseline-only runs so they cannot satisfy candidate acceptance; leave package mechanics to Platform/Builds.

**[Adversarial review · A2]** — Successful attachment does not define what happens when the environment owner finishes (prd.md:129 (FR-4 Run isolation and ownership), prd.md:133-138 (FR-4 Completion, cleanup and diagnostics); addendum.md:54 (MVP approach), addendum.md:105 (Selected behavior))

Run B may attach successfully to A after all required compatibility checks. If A succeeds or is cancelled while B is still testing, A cleans its own environment and B loses its services. The restriction on deleting another run's resources does not clearly cover this because A retains ownership. The reverse cancellation case is covered, but accepted attachment lifetime and its outcome when the owner terminates are not.

Fix: Define whether owner termination preserves the environment until attached work terminates or explicitly terminates/reports attached runs before cleanup, including failed-local retention. Cover the selected ordering in the existing SM-3 attachment/lifecycle demonstration.

**[Recovery and access · S3]** — The addendum misstates the accepted operations-writer and deputy controls (addendum.md:225, :326, :342; prd.md:346; ARCHITECTURE-SPINE.md:36, :130, :131, :478, :480, :481; architecture .memlog.md:218, :236, :237)

The addendum still calls the accepted operations repository single-writer, triggers GitHub Team review at the second writer, and says architecture lacks deputy access and notification rules. The current spine supplies independent deputy recovery without repository write, accepts Administrator plus the second organization owner as writers, and triggers review only beyond those named writers. Architecture C-09 (:218), D4 (:236) and D5 (:237) explicitly record accepted decisions. The stale summary can create an unnecessary control decision/story dependency and misstate the accepted workflow-editing trust boundary. Runtime deputy qualification remains open at G2.

Fix: Refresh the accepted-control summary to two named writers and the revised review trigger. Replace obsolete architecture follow-up with remaining implementation/qualification of deputy identity, custody, alert delivery and rehearsed recovery. Retain separate spec synchronization and evidence gates that remain open.

### Low (0)

None.

## Reviewer summaries

### Adversarial review

Ready for planning with two medium clarifications; no critical or high findings.

Source: `validate-2026-09-28/review-adversarial.md`

### Recovery and access

Reconciliation required before recovery and deployment stories are finalized: 0 critical, 2 high, 1 medium, 0 low. Later user-accepted recovery/access decisions have not been carried back from the current architecture. This is document synchronization, not unauthorized architecture weakening or failed deployed controls.

Source: `validate-2026-09-28/review-recovery-access.md`

## Mechanical notes

- Verified all 26 declared requirement/measure IDs: FR-1–FR-12, NFR-1–NFR-3, SM-1–SM-6 and SM-C1–SM-C5 are unique and contiguous. All ID references found in the PRD resolve.
- All 16 Markdown local-link file targets across the current PRD and addendum exist; their explicit local heading anchors resolve. External URLs were not revalidated for this internal-consistency review.
- No unresolved `[ASSUMPTION]`, `[NOTE FOR PM]` or TODO markers occur in the PRD; an Assumptions Index roundtrip is therefore not applicable.
- No UJ identifiers or unnamed protagonists require correction because the documented capability-spec shape deliberately omits UJs.
- No substantive glossary drift or required-section omission was identified for the agreed internal-platform scope.

## Synthesis decisions

- All five reviewer findings are retained at their original severities; none are duplicates. There are no critical findings. The two high findings are requirements synchronization issues, not claims that later architecture decisions lack authorization.
- The rubric assesses internal PRD coherence; the recovery/access review additionally checks later accepted architecture decisions. Those different scopes explain strong dimension verdicts alongside a Fair consolidated grade.
- The previous consolidated validation predates the September 27 update and is superseded. Its files are preserved as validate-2026-09-28/previous-validation-report.md and .html; previous individual reviews remain in place.
- Original brief/addendum scope, later PRD choices and accepted deferrals were extracted in validate-2026-09-28/orient-context.md. Missing implementation or production evidence was not treated as a planning defect.

## Reviewer files

- `validate-2026-09-28/review-rubric.md`
- `validate-2026-09-28/review-adversarial.md`
- `validate-2026-09-28/review-recovery-access.md`

Validation is critique only. Findings can be carried into a separate PRD Update.
