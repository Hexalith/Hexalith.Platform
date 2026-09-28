# Recovery and access review — Hexalith Platform PRD

## Overall verdict

**Reconciliation required before recovery and deployment stories are finalized: 0 critical, 2 high, 1 medium, 0 low findings.** The PRD defines a coherent recovery and access contract, with explicit response coverage, independent recovery resources, deputy authority and production gates. Its remaining problems are discrepancies with later user-accepted architecture decisions. Those decisions should be carried back into the requirements and supporting summary; they are not evidence of unauthorized architecture changes or failed deployed controls.

Reviewed the current PRD, addendum and PRD decision log, the supplied orientation extract, and relevant current architecture sections and decision-log entries on 2026-09-28. Architecture files are under active edits; references describe the inspected snapshot. This is a document review, with no runtime or external-source verification and no source-document edits.

Paths below are relative to the project root:

- **PRD:** `_bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/prd.md`
- **Addendum:** `_bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/addendum.md`
- **PRD log:** `_bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/.memlog.md`
- **Spine:** `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md`
- **Architecture log:** `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/.memlog.md`

All locations are 1-based line numbers.

## Findings

### S1 — High: Restoration revocation guarantees omit the later accepted exceptions

**Locations:** PRD:226, PRD:227, PRD:296, PRD:303, PRD:309; addendum:273, addendum:275; spine:118, spine:295, spine:320, spine:321; architecture log:220, architecture log:237, architecture log:250, architecture log:258.

**Evidence:** FR-9 requires recovery to “reapply post-cut revocations” and verify “denial of revoked principals” (PRD:226). Its lost-window exception enumerates “deletions, erasures or legal holds” (PRD:227). NFR-3 requires “denial after revocation” and repeats the checks after restoration (PRD:309). The addendum reinforces that its ordinary-RPO exception “does not relax ... other module functional and authorization rules” (addendum:275).

The current spine explicitly includes “module-owned authorization revocations acknowledged after the recovery cut” and “realm access removals other than admission made after the durable event-export frontier” in the accepted exceptions (spine:295). Admission itself has a stronger independent record requirement and reconciliation guarantee (spine:118, spine:320, spine:321). Architecture log C-25 records acceptance of the module exception (:220); D5 allows reopening after recording the exceptions, with Administrator review before clearing the promotion stop (:237); VAL-08 preserves admission revocation continuity (:250); G-4 explicitly accepts the narrower realm-removal loss and directs durable containment through admission revocation (:258).

**Consequence:** A restore can satisfy the accepted architecture while failing the unqualified PRD acceptance language. For example, an otherwise admitted person whose module permission was revoked after the recovery cut can regain that permission in restored data; a realm role removal not yet exported can also disappear. Conversely, builders following the PRD literally would implement stronger continuity than the user selected. The distinction affects recovery acceptance and security expectations, not merely the backup mechanism.

**Minimal fix:** Reconcile FR-9 and its NFR-2/NFR-3 and addendum references to the already accepted boundary: production admission revocations survive restoration; identified module-owned revocations and non-admission realm removals can fall into their specific lost windows and must be reported under the accepted reopening/review rules. Preserve Memories continuity, ordinary live authorization, and application-rollback protection. Describe these scopes at the requirement level without importing the record-store design. No new policy decision is necessary unless the owner wants to reverse the later accepted exceptions.

### S2 — High: The PRD blocks the accepted Administrator-approved recovery release path

**Locations:** PRD:48, PRD:168, PRD:185, PRD:204, PRD:208; addendum:177, addendum:193, addendum:207; spine:280, spine:281, spine:288, spine:309; architecture log:249, architecture log:252, architecture log:260.

**Evidence:** FR-7 says Platform “verifies that [the working baseline] is healthy now” and missing preconditions stop mutation (PRD:185). FR-8 says only an Administrator record naming a “verified current working release” clears the promotion stop (PRD:204). The addendum explicitly says an unhealthy production environment stops the update for investigation (addendum:193). Approved mode is described for pre-G3 and incompatible releases (PRD:48, PRD:168).

The current spine permits an approved attempt when production has no working baseline **or its last outcome was non-working**, lifting the stop and healthy-baseline condition for that one attempt at the observed stop revision (spine:280, spine:281). A successful attempt clears that revision; later stops retain precedence (spine:288). VAL-04 records the accepted exception binding (architecture log:249), the ensuing decision entry records user acceptance (:252), and the final accepted corrections expressly cover a working degraded override acting as a clear (:260).

**Consequence:** An Administrator-approved attempt to repair degraded production can be permitted by architecture and rejected by PRD acceptance tests even when staging evidence and all common gates pass. The PRD does address first deployment and manual recovery; the conflict is specifically the additional approved candidate-release path without a currently healthy baseline, including its limited stop exception.

**Minimal fix:** Carry the accepted approved-mode use case into the release-mode description and FR-7/FR-8, with corresponding addendum and SM-5 acceptance wording. State that Administrator may authorize the identified attempt despite the missing healthy baseline and existing observed stop, that this does not suppress a newer stop or waive staging/provenance/isolation/verification, and that only successful verification establishes the new working baseline. Keep independent deputy recovery and Administrator-only release authorization intact.

### S3 — Medium: The addendum misstates the accepted operations-writer and deputy controls

**Locations:** Addendum:225, addendum:326, addendum:342; PRD:346; spine:36, spine:130, spine:131, spine:478, spine:480, spine:481; architecture log:218, architecture log:236, architecture log:237.

**Evidence:** The addendum says the architecture's “current Administrator-only operations access and notification rules do not yet implement deputy recovery authority” (addendum:225). Its accepted-decisions table specifies a “single-writer operations repository” and GitHub Team review “when a second writer is introduced” (addendum:326).

The current spine names Administrator and the second organization owner as writers (spine:36, spine:130), supplies independent deputy recovery without repository write access (spine:131), and triggers the GitHub Team review only when someone **beyond those named writers** gains write or admin (spine:481). The architecture log records accepted deputy controls (:218, :237) and the user's explicit two-writer choice and revised trigger (:236). Demonstrating actual deputy access remains a G2 qualification task (spine:478).

**Consequence:** Readers of the PRD's supporting rationale receive the wrong accepted administrative trust boundary and an obsolete architecture task. This can cause an unnecessary GitHub-plan decision or story dependency while obscuring the accepted risk that either named writer can change executor workflows. It does not show that the live repository or deputy access is qualified.

**Minimal fix:** Refresh the accepted-control summary to the two named writers and revised review trigger. Replace “architecture does not yet implement” with the remaining implementation and qualification work for deputy identities, custody, alert delivery and rehearsed restoration. Mark architecture alignment complete where the current spine supplies it, while retaining the separate spec alignment and evidence gates that remain open.

## Requirements that are sufficiently defined

- **RPO/RTO and response:** PRD:296–301 defines continuous one-hour RPO, four-hour RTO for outages starting within declared coverage, continued commitment across coverage end and reporting from the original outage time. Worst-case acknowledgement, capacity and substitution time are qualification inputs. No additional round-the-clock promise is inferred.
- **Independent recovery resources:** PRD:220–228 covers complete recovery sets, dependency ownership, independently available artifacts/access/keys, replacement capacity, quarantine, fencing, restored checks and protection before reopening. Whole-site coverage is correctly conditional on an independent location.
- **Deputy limits:** PRD:34, PRD:203–207 and PRD:255 distinguish recovery/reopening from promotion clearance and admission administration. No missing Administrator availability dependency was inferred from the stronger delegated recovery wording elsewhere in the PRD.
- **Gate order:** G1 is closed deployment with monitoring; restricted synthetic qualification precedes G2; G2 requires recovery/access evidence; SM-5 enables automatic promotion at G3. The absence of completed drills or deployed proof is not a planning defect.
- **Security scopes:** Memories erasure continuity survives the ordinary RPO; rollback does not rewind current security authority; restored copies remain production data; staging users, workloads and automation are included in negative checks. S1 concerns the exact later accepted disaster-restoration exceptions.

## Mechanical notes

None. Source documents were not modified.
