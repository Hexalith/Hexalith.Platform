# Hexalith.Platform sprint change proposal for the November code audit

**Status.** Approved on 19 November 2026 after individual review of all 13 items. The Product Owner and Platform maintainers take the implementation handoff through the sprint tracker.

The tuple preflight passed on `f3f96b2e`. The submodule sync at `707cf901` reintroduced drift across all five dependency revisions. The release repair is open again, and Story 2.14 is in progress. [Tuple preflight run](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44489299741)

The two HTTP hosts and OpenBao inventory need fixes before their affected deployment. The erasure-root profile also requires a decision from the owner of the data it would protect.

## Why the sprint changes

The 18 November audit recorded a successful Release build at `cb57dd4f`. On the same revision, the workflow’s `read_selection` check stops at EventStore. A comparison against the committed Git tree confirms all five differences below. These differences describe the audited revision. Other test counts and runtime measurements in this proposal come from the audit. [Code audit](https://chatgpt.com/space/page_c03de452a6e88191a8c6d6118cd2580c) · [Qualification workflow](https://github.com/Hexalith/Hexalith.Platform/blob/cb57dd4ff7009533010edcb385dbcbf03d53e4da/.github/workflows/tuple-ci.yml#L34)

| Dependency | Declared revision | Committed submodule |
| --- | --- | --- |
| EventStore | `96363934` | `05e4f6d4` |
| Parties | `8abfd266` | `8e3507e4` |
| Tenants | `ac7b6ba9` | `cbd11618` |
| Memories | `68a78d47` | `7611baf7` |
| Builds | `7afc4981` | `68ad51d4` |

## Changes and acceptance criteria

### Release and deployment

**1. Repair the candidate before Story 2.14 · Audit 1.** The approved plan called for a repair task ahead of the version-pin migration. The release owner selects the five revisions. Platform either aligns the submodules to the approved publications or validates the intended upgrades and updates their bindings together. On one corrected commit, pass `read_selection`, the Builds publication and gitlink checks, `verify_checkout`, and the existing `current42`, `base42` and `extensions` suites with their required tests and zero skips. Link the run to that commit. **Disposition.** The source-selection and tuple jobs passed on `f3f96b2e` with all five dependency revisions reconciled. The later submodule sync at `707cf901` reintroduced all five mismatches. A local run of `read_selection` against that revision stopped at EventStore, so the tracker reopens this action. [Tuple preflight run](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44489299741)

**2. Enforce the request limit in both hosts · Audit 2.** Both hosts currently configure Kestrel after startup. The audit measured 30,000,000 bytes in TenantRegistry. The EventStore measurement was blocked by its Protection prerequisite. The Platform host maintainer configures the 1,048,576-byte global limit before `Build()`, verifies the effective value in each configured host and checks the intended route exceptions before deployment.

**3. Restore OpenBao inventory transport on .NET 11 · Audit 3 and 9.** Two proxy-sensitive tests passed after a hostname exemption. A third still failed on a private .NET constructor. The inventory maintainer configures a direct connection only for the OpenBao metadata client and replaces private reflection with an instance-level connection factory. Run the inventory suite with a proxy configured and no hostname exemption. Verify active-service routing, the original TLS name and certificate chain, and rejection of invalid certificates and denied access.

**4. Resolve the release-tooling advisory · Audit 4.** Seven high-severity development dependency entries map to one advisory across two `picomatch` versions. The release-tooling maintainer traces both dependency chains and assesses which CI inputs can reach them. Close on a compatible dependency update, a clean install, a fresh audit showing the advisory cleared and passing publication tests. If a compatible fix is unavailable, keep the task open with the remaining exposure, an owner and a review date. [Advisory GHSA-fc5q-39xc-vjpf](https://github.com/advisories/GHSA-fc5q-39xc-vjpf)

### Reliability and maintenance

**5. Measure tenant inventory reads · Audit 5.** At 10,000 events the code can require 400 page reads per successful resolution. The inventory maintainer measures requests, allocated bytes and duration for short and near-limit histories before choosing a buffering or reread change. Document behavior beyond the limit and retain refusal when the enrollment changes during verification.

**6. Propagate profile-read cancellation · Audit 6.** The HTTP path currently blocks a thread for up to 30 seconds and starts the dependency read with `CancellationToken.None`. The inventory maintainer passes the request token through an asynchronous path, retaining the synchronous adapter for existing callers. A slow-dependency test must observe cancellation and verify that an unresolved profile grants no access.

**7. Clean up temporary keys after a composition failure · Audit 7.** Aspire can retain a private telemetry key with the failed run’s diagnostics. The composition maintainer retains the required redacted diagnostics and removes temporary private material after its consumers stop. Extend the failure test to check both outcomes and another run’s files.

**8. Validate a snapshot before replacing the local Custody volume · Audit 8.** The qualification component can accept an incomplete final field. The Custody maintainer validates counts, sizes and complete reads before replacing state. A valid snapshot must restore. On rejection, the previous volume must remain intact and readable.

**9. Synchronize the Identity saturation test · Audit 10.** The audit saw 16 free slots where the test expected zero, followed by a pass on the same binaries. The Identity maintainer adds a signal at the workers’ intended wait point and advances the test clock only after all workers arrive. Verify slot retention and release across expiry. The original failure’s cause remains provisional.

**10. Split the authority receiver in small steps · Audit 11.** Schedule this work after the reliability fixes. The Secrets.Runtime maintainer first extracts signed-response encoding from `HandleCoreAsync`, then isolates an existing durable transition where coverage permits. Concurrency, repeated requests, signing, erasure and cleanup must retain their tested behavior.

**11. Make Protection tests portable across build directories · Audit 12.** Five cases passed on the same binaries once their Python script and EventStore checkout were available. The Protection maintainer supplies both paths explicitly, names a missing prerequisite in the error, and verifies all five cases with the build output outside the repository.

### Qualification and operational decisions

**12. Close outstanding qualification evidence · Audit test results.** QA reruns the 12 storage cases on ext4 and the eight native Dapr or OpenBao cases on their designated test setups. The Custody maintainer traces the missing reservation in the full suite, where one of 806 cases failed even though the three targeted variants passed. Record the candidate, environment and outcome for each group. Keep unresolved failures visible in the qualification decision.

**13. Decide whether the erasure-root profile is acceptable · Audit operations decision.** The prepared profile uses one OpenBao instance. Loss of its volume permanently loses the payloads it protects. Before deploying this profile, the data owner records whether that consequence is acceptable and the operator verifies restart with the intact volume. A declined or outstanding decision holds this profile. A backup-restore procedure would contradict its erasure policy. [Erasure-root durability policy](https://github.com/Hexalith/Hexalith.Platform/blob/cb57dd4ff7009533010edcb385dbcbf03d53e4da/deploy/erasure-root/README.md#L5)

## Impact on the existing plan

**Epic 2.** Story 2.14 links to the audit follow-up. The sprint tracker records the renewed release blocker. The approved 18 November correction already covers lifecycle proof and the version-based pin design. A repair under the current selection rules can go ahead without reopening Story 2.8 or expanding the frozen Builds implementation brief. [Story 2.14](https://github.com/Hexalith/Hexalith.Platform/blob/cb57dd4ff7009533010edcb385dbcbf03d53e4da/_bmad-output/planning-artifacts/epics.md#L1710)

**Epics 1, 3, 4 and 5.** Composition cleanup must preserve useful diagnostics. Story 3.2 consumes the EventStore host. Story 4.12 owns publication and Story 4.22 consumes the deployed services. Record the applicable audit tasks beside these handoffs and keep their acceptance criteria in this proposal. Epic 5 consumes the qualification results. Story 5.2’s image-scanning policy applies to a different artifact from the development dependency in item 4. [Epic and story definitions](https://github.com/Hexalith/Hexalith.Platform/blob/cb57dd4ff7009533010edcb385dbcbf03d53e4da/_bmad-output/planning-artifacts/epics.md)

**Product, architecture and later epics.** The seven-module MVP, release identity and existing promotion criteria already cover the intended outcomes. The repairs implement existing host, transport, authorization and durability contracts. The profile-specific owner decision applies before use of that profile. The ordinary recovery requirements and the planned work in Epics 6–12 stand. The audit introduces no user interaction requiring a UX specification. [Product requirements](https://github.com/Hexalith/Hexalith.Platform/blob/cb57dd4ff7009533010edcb385dbcbf03d53e4da/_bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/prd.md#L38)

## Sequencing and handoff

**Moderate scope.** The Product Owner sequences the work within the existing epics. The Platform release maintainer owns the candidate repair and coordinates the intended versions with the release owner. Host, inventory and release-tooling maintainers can start items 2–4 in parallel. QA and the Custody maintainer can arrange the supported test setups for the candidate that includes the remaining qualification fixes. Schedule items 5–9 and 11 in the reliability cycle, and item 10 after the higher-priority fixes. Item 13 applies whenever the erasure-root profile is first proposed for deployment.

**Effort and timing.** The host configuration and path repairs are small. The release repair is small if the recorded publications are selected and larger if the owner chooses upgrades. Transport changes and the supported-environment runs are medium effort. The inventory optimization estimate depends on measurements. Release timing depends on the renewed dependency alignment and a passing tuple preflight. Deployment and the remaining qualification work also depend on the outstanding fixes and test environments. The audit supplies no basis for a calendar estimate.

**Alternatives considered.** Reverting completed stories would discard the previous sprint’s approved work and still leave the host, transport and qualification issues. Reducing MVP scope would leave the same problems in the services selected for release. Targeted repairs preserve the plan and put each decision with the person who can make it.
