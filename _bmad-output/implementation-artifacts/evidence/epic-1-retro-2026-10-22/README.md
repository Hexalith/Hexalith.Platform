# Epic 1 retrospective evidence — 2026-10-22

The parent retrospective is the decision record. No product fixes are applied here.

- `scope.json`, `platform-git-evidence.json`, `builds-git-evidence.json`: recorded ranges and deterministic commit/churn inventory. Counts include concurrent work. Merge churn is separate; Builds has unmeasured side-branch merges.
- `aggregate-measurements.json`: direct project edges, source sizes, exact twelve-line duplication candidates. Shared header/imports are not a defect.
- `review-*.json`: independent lens findings; overlap is preserved here and deduplicated in the retrospective. Existing ledger items are identified as carried, not newly discovered.
- `root-identity-repro-*`: disposable binding reproduction against current Release assemblies. The mapping shape changes; the project and declaration remain identical. The source resolver's basename behavior is verified separately in primary source. No distributed resources were launched by this repro.
- `composition-build.txt`, `composition-tests.txt`, `verification-summary.json`: actual current-working-copy Release build/test results. Preexisting dirty Dapr catalog values are explicit in scope.json. These are not clean released-source qualification.
- `reference-declarations.txt`: partial policy matrix, ending with the exact HXP020 fixture failure/traceback.
- `cli-checks.json`, `joint-manifests.json`: exact released-CLI joint validation command/result.
- `current-parties-adoption*`: released CLI on actual pinned Parties, with status and selected-file hash checks. Nested references were not initialized.
- `dapr-security-test.txt`: initial fixture refuses installed runtime/catalog mismatch before starting resources.
- `dapr-security-released-test.txt`, `dapr-security-probe.json`, `dapr-security-released/`: actual pinned Dapr fixture launch using the published 4.35.2 Tooling assembly, first permitted invocation timeout and successful owned cleanup. No full-system or debugger/edit pass is claimed. Generated credential directory was temporary and removed by the fixture.
- `proposed-actions.json`: six stable proposed actions used by the comment-preserving sprint-status update.

The unrelated preexisting Tenants environment and baseline containers were left running. Full raw diffs and the disposable compiler project were staged outside the repository; the primary durable evidence is retained above.
