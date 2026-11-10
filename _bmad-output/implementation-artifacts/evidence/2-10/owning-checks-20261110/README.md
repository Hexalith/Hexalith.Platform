# Canonical owning checks — 2026-11-10

The user explicitly authorized Parties' nested Builds initialization and local /
hosted qualification. This worker initialized only that exact nested dependency
and completed the canonical owning checks. It ran no lifecycle campaign or hosted
workflow and grants no tool or candidate acceptance.

Parties source `71dd8a4377dcab3d5a30a416be96263e5aa3a2f0` now has its exact nested
Builds `5ec1779040606be89306129a7d2b245258c9acd2`, cloned from existing local Git
objects with `--no-fetch` and no recursion. [Canonical discovery](parties-canonical.txt)
passed **106/106**, including the formerly failing linked-owner-directory control.
No Parties source or dependency pin changed. Earlier discovery errors/failures
retain their original results.

The Builds model test's old six-server assertion reproduced. The selected
declarations actually define seven servers: one gateway, two Tenants and four
Memories servers. The [one-file correction](model-test.patch.txt) compares exact
resource names, app IDs and readiness endpoints with the selected declarations,
retaining the existing gateway, administrative-resource and HTTP readiness checks.
The Docker discovery control passed normally; its historical post-cancellation
`TargetException` did not reproduce, and no speculative production fix was made.

Concurrent external commits changed main during setup. Verification therefore
uses the clean detached checkout
`artifacts/story-2-10-builds-da4e896b` at exact source
`da4e896b164f35c0b830a32b975203bb922280f9`. That external commit contains the
one-file correction. This worker made no commit or push. Cache-only restore and
the isolated Debug build passed with zero warnings/errors.

- [Correction and Docker controls](correction-focused.txt): **2/2 passed**, no skips.
- [Actual stdin regression classes](stdin-controls.txt): **21/21 passed**, no skips.
- [Full owning Module.Tests project](builds-owner.redacted.json): **2,941 total,
  2,939 passed, zero failures/errors, two skips, zero not-run**, exit 0. It finished
  in 531.984 seconds within the existing 1,800-second owner-job budget.
- The two explicitly skipped controls are
  `CompositionSdkProcessTests.QualificationCatalogProbeInterceptionIsEstablishedAsync`
  (requires its qualification-only SDK harness) and
  `CompositionNativeProcessDiscoveryTests.NativeTaggedChildRemovalPreservesIndependentPeerAsync`
  (Windows/macOS inspection). They are recorded separately and grant no proof.

[Inputs](inputs.json) bind mapped repository revisions/dirty-patch digests,
declaration content, the exact resolved package SHA-512 identities and catalog /
assets, plus available test/model binary and configuration SHA-256 identities.
The selected catalog supplies EventStore.Aspire 4.2.0; the runner is xUnit 4.0.3
on .NET 10.0.14 with SDK 10.0.403. [The stability check](input-stability.json)
confirms every bound catalog, declaration and test/model artifact remained
unchanged through execution and the detached source stayed clean.

[Verification](verification.json) preserves the initial focused failure, the
concurrent-source compilation failure, each subsequent build and the separate
fresh results. [Per-case records](test-cases.redacted.json) omit input values,
exception messages and raw output. Original raw-output/XML hashes remain bound;
the raw owner reports are excluded from this evidence directory.

The bounded-diagnostics implementation and owning checks are complete. All eight
fresh local and eight fresh CI lifecycle rows, fixed qualification input selection,
full matrix/cancellation campaigns and accepted-tool enrollment/pins remain open.
No production deadline changed, no setup savings are claimed and no prior result
was relabelled as fresh qualification.
