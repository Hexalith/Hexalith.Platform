---
title: 'Fix EXT-SECRETS-1 F1 runtime analyzer build'
type: 'bugfix'
created: '2026-10-20'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Fix the eleven Secrets.Runtime analyzer errors in the retained Agents Platform F1 failure log: nine CA1062, one CA1822, and one CA2007. Preserve public contracts, named unavailable responses, proof rejection, cancellation, instance-property serialization, and false-only production mutation permission. Keep analyzers and TreatWarningsAsErrors intact.

Run `eng/ext-secrets-1/verify_f1.py` with the existing Agents root-declared Builds and EventStore checkouts and fresh detached artifact/evidence directories; run the complete `eng/ext-secrets-1` Python suite. Keep EXT-SECRETS-1 unavailable and preserve request-size, buffer-erasure, provider and runtime-input blockers. Do not initialize nested submodules, deploy, inspect credentials, run Live qualification, or amend dependency acceptance.

</frozen-after-approval>

## Implementation Notes

- Investigation: the failure uses the same Platform base `11893ca9fd85644cab8a0414b2076bf346f108f6` as this checkout. Existing Agents root dependencies are clean: Builds `5d03f1db9c38db34ecac7fe6048937478ffb82cc`, EventStore `c9f47dd5ba72918445dfd24a674a5b2c310589bd`.
- No intent gaps or irreversible actions. Small footprint: six runtime source files plus focused regression coverage. Retain existing combined missing-input reporting for optional provider/store/verifier; make the admission gate explicit to analysis. Keep null proof/credential rejection within the existing catch boundary. Preserve `QualificationDenial.ProductionMutationAllowed` as a getter-only instance property with false default. Configure asynchronous FileStream disposal without changing its lifetime.
- Verification must retain all attempts and exact logs outside the source checkout. Python command: `python3 -m unittest discover -s eng/ext-secrets-1 -p 'test_*.py' -v`.
- The eleven Runtime errors are cleared. Attempt 1 then exposed three CA2007 errors in the enrollment/executor/observer entry points; attempt 2 reached the test project and exposed 23 CA2007 errors in async helpers and one CA1062 on a theory parameter. Correct these prerequisites without changing assertions or analyzer configuration. Updated footprint: nine production source files and five existing test files. The credential null guard returns before the existing budget/try boundary, preserving the same null result without resolving a credential.
- Independent review requested required-null-input and false-only deserialization coverage; add focused assertions alongside nullable dependency denial, null proof rejection and serialized/copied false permission coverage.
- Final verification ran in `/home/administrator/projects/hexalith/agents/references/Hexalith.Platform` with its existing root-declared sibling Builds/EventStore checkouts. No nested submodules were initialized. The exact candidate patch was applied only for verification and reversed afterward, after confirming it was the only change; that checkout is clean. The fourteen delivered C# files match the verifier's recorded source hashes exactly, and all seven reported artifact hashes were checked.
- Passed command: `python3 eng/ext-secrets-1/verify_f1.py --builds-root /home/administrator/projects/hexalith/agents/references/Hexalith.Builds --eventstore-root /home/administrator/projects/hexalith/agents/references/Hexalith.EventStore --artifacts /tmp/platform-f1-analyzer-fix-20261020-elhot_mi/artifacts-3 --output /tmp/platform-f1-analyzer-fix-20261020-elhot_mi/evidence-3`. Exit 0; Debug build: 0 warnings, 0 errors, 7.01 seconds. Runtime XML: 232 total/passed, 0 failed/errors/skipped/not-run, 2.990 seconds; 95 F1 tests and all 26 required behavioral methods covered. `DependencyAvailable=false`, `LiveReady=false`, `qualificationEffects=0`, `runtimeQualificationCompleted=false`.
- Passed full Python command in the Agents Platform checkout: `python3 -m unittest discover -s eng/ext-secrets-1 -p 'test_*.py' -v`. Exit 0; 170 tests in 50.072 seconds, OK. Exact log: `/tmp/platform-f1-analyzer-fix-20261020-elhot_mi/python-tests-final.log`. Its trailing `BlockedUnknown`/`ObservedRevival` messages are synthetic test outputs, not native execution. Earlier primary-checkout run also passed 170 tests in 34.653 seconds.
- Retained the original supplied failure log and both intermediate verification failures: `evidence/runtime-build.log` (three downstream host errors) and `evidence-2/runtime-build.log` (24 downstream test errors) beneath `/tmp/platform-f1-analyzer-fix-20261020-elhot_mi`. Each attempt used fresh separate artifacts/evidence. The final fix resolves the requested eleven Runtime diagnostics plus these 27 downstream diagnostics without suppressions, configuration changes or removed assertions.
- Remaining prerequisites are unchanged: supported whole-request transport for legal 1,000-target requests exceeding 4 MiB (4,232,931 and 24,692,931 SDK bytes); cross-process buffer-erasure/completion or accepted termination; semantic custody component and independently surviving physical/current authority with original recovery and restore proofs; all 53 runtime fields plus 25 supplemental component/proof inputs in the continuation handoff. F2/F3 resource/cost/cleanup authorization, exact accepted immutable target/configuration/234-case plan/anchors/command, and the four joint OQ prerequisites remain separate gates. EXT-SECRETS-1 stays unavailable. No deployment, credential inspection, Live qualification or acceptance amendment occurred.

## Review Triage Log

- Low, addressed: initial implementation notes described the original six-file scope and an inaccurate guard location. Appended the actual downstream analyzer findings, fourteen-file code footprint and early credential rejection behavior.
- Low, addressed: required-null-input rejection had no focused regression coverage. Added checks for parameter-named argument errors and malformed JSON rejection; existing and new dependency tests retain named 503 denials with zero effects and no provider/storage callbacks.
- Low, addressed: false-only permission coverage did not exercise attempted true deserialization. Added that assertion alongside serialization and record-copy checks; all passed.
- Follow-up independent review of the complete diff found no new actionable correctness issues. No findings deferred.
