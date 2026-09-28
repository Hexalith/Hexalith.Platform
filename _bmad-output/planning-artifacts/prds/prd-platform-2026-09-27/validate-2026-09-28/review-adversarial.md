# Adversarial PRD review

Reviewed: 2026-09-28  
Artifact: `../prd.md`, with `../addendum.md` and `../.memlog.md`  
Verdict: **Ready for planning with two medium clarifications.** No critical or high findings.

The current requirements explicitly close the material release-gate and McpCli bypasses examined: all enrolled modules contribute checks, declarations and check changes require corresponding evidence, both release modes retain the staging gate, automatic promotion requires current-baseline compatibility, and McpCli acceptance requires positive execution as well as server-enforced refusals. Missing implementation or qualification evidence is not counted as a PRD defect. The two remaining findings concern local/CI evidence and shared test-environment lifecycle.

## A1 — Medium — CI can exercise an older module package without rejecting evidence for the candidate revision

**Location:** PRD, “FR-2: Debug the active module checkout,” line 90; “FR-4: Provision real-service integration tests locally and in CI / Composition and readiness,” lines 119–121; “Success measures,” line 313. Addendum, “Workspace and build constraints,” line 34.

**Short quote:** “Local integration-test environments run through Aspire using the module configuration and the active source checkout.” The following bullet specifies CI “Release/NuGet rules.”

**Finding:** The active source checkout is explicitly bound to the local environment. CI must use Release/NuGet artifacts, but no equivalent requirement binds the module package actually exercised in CI to the module revision for which the integration result is being claimed. Recording a release or source revision with the demonstration does not establish that relationship. The addendum explicitly binds the pinned Platform CI tool to the Platform submodule commit, which resolves Platform-tool identity but leaves the tested module's candidate identity unstated.

**Demonstrated consequence:** A Parties change is built and its isolated tests pass. CI starts the correctly pinned Platform runner and the declared real dependencies, but selects the previously published Parties package from the package catalog. Its real-service tests pass against that older server. The composition, artifact mode, readiness, cleanup and result-recording requirements can all pass while the changed Parties behavior was never exercised. A result that names the actual older package is still not explicitly rejected as evidence for this candidate.

**Minimal suggested fix:** State that CI integration acceptance for a candidate must identify and exercise the tested module's Release artifact from that candidate revision, and must reject results for a different module artifact/revision. Keep dependency-version selection and package-production mechanisms with the existing Platform/Builds owners. If an intentional baseline-only CI run is supported, label its evidence accordingly so it cannot satisfy candidate integration acceptance.

## A2 — Medium — Successful attachment does not define what happens when the environment owner finishes

**Location:** PRD, “FR-4 / Run isolation and ownership,” line 129; “FR-4 / Completion, cleanup and diagnostics,” lines 133–138. Addendum, “Testing options for module dependencies / MVP approach,” line 54, and “Test readiness and local cancellation rationale / Selected behavior,” line 105.

**Short quote:** “Explicit attachment requires compatible composition, artifact mode, readiness and data isolation” and “A successful local run cleans up automatically.”

**Finding:** The document checks attachment compatibility and prevents the attached test's cancellation from destroying someone else's environment. It does not state how the owning run's completion, cancellation or cleanup interacts with an attached run that is still executing. Ownership protection alone does not settle this, because the resources still belong to the run that is now entitled or required to clean them up.

**Demonstrated consequence:** Local run A creates an environment. Run B explicitly attaches and passes every listed attachment check. A succeeds while B is still testing, so A automatically cleans up its own resources. B loses its services even though its attachment was accepted and no resource owned by another run was deleted. The same unresolved ordering appears if A is cancelled. This creates nondeterministic integration failures in an explicitly supported execution path and leaves no stated expected outcome for the attachment acceptance scenario.

**Minimal suggested fix:** Define the lifecycle consequence of an accepted attachment when its owner finishes: either keep the required environment available until attached work terminates, or explicitly terminate/report dependent runs before cleanup. Define how that outcome respects failed-local retention. Add that ordering to the already required SM-3 attachment/lifecycle demonstration; no specific coordination mechanism is needed in the PRD.

## Scope notes

- No request is made for new operational features, arbitrary timing limits, broader module scope or central ownership of module business behavior.
- Explicit owner-and-gate deferrals for declaration review, evidence age, reruns, runtime integration and McpCli qualification are valid downstream handoffs.
- Recovery and access controls were checked only for cross-cutting contradictions; the dedicated recovery/access review covers their detailed feasibility and failure paths.
- Source documents were not changed. No runtime capability or passed production gate is inferred.
