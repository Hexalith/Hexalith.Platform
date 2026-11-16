# Coverage correction, 2026-11-16 code review

This record supersedes rows 29–31 of [coverage-map.md](../coverage-map.md) and rows 40–42 of
[selector-count-mapping.tsv](../selector-count-mapping.tsv). Those files stay byte-identical because earlier
audits bind their hashes and their observations keep their original subjects.

The 2026-11-15 review deleted three orphaned classes in full. The 2026-11-16 review found that some of their
cases did not depend on any historical body, revision or replay collector. Those cases are restored and selected
in the current42 profile. Only the replay-dependent cases stay retired.

| Class | Earlier record | Corrected disposition |
| --- | --- | --- |
| `PlatformPartiesMappedOwnerProofTests` | 3 retired; "fixed Parties gitlink `bb2783120c0e…`" | Only `ActualPlatformOwnedResolutionBindsTheReviewedLifecycleSourceAsync` pinned the archived gitlink, so it stays retired. `PlatformOwnedMappingBindsItsActiveOwnerAndDirectCatalog` and `PlatformOwnedMappingRejectsChangedDirectOwnerGitlink` use the fixture-owned `PlatformPartiesProofFixture(platformOwned: true)` histories. They are restored and selected as current42 group 33 (`-class`, 2). No other test covered Platform-owned direct-owner drift refusal. |
| `PublishedCatalogArtifactCohortTests` | 47 retired; "tied to the old SDK4.1/Tenants7.0.2/Memories3.0.23 loaded-assembly cohort" | The old signed-body cases stay retired. `ExactVersionPairsDetermineOneClosedAuthority` (14), `ForeignBodiesAndUnknownAuthoritiesRemainRefused` (5) and `FullVerificationRequiresAFullAuthority` (1) load no historical body. They test the production `PublishedCatalogArtifactCohort.ForVersions`/`Verify`/`VerifyFullParties` closed-authority refusals: `+metadata`, four-part, cross-pair and foreign-assembly inputs. They are restored and selected as current42 group 32 (`-class`, 20). Current-cohort tests assert only null pairs. |
| `PlatformPackageCatalogReaderTests` | 9 retired; "public-context guard returns early under the current cohort" | The deletion stands, but its reason was imprecise. `QualifiedTenantsModelForwardsDistinctReceiverTokensAsync` asserts `HasQualifiedSdkBytes()` and would fail rather than return early under the current cohort. Its tenants sidecar token scoping through production composition is now covered by `PlatformAppModelTests.ActualDaprCliListenersAndTokensRemainServerScopedAsync`, newly selected in current42 group 7. No profile had ever selected that test before, despite earlier triage rationales saying it "remains selected". |

Inventory effect: current42 goes from 463 to 486 (+1 listener, +20 cohort refusals, +2 Platform-owned mapping
facts). base42 (93) and extensions (9) are unchanged, for 588 executions in total. Measured observations are in
[package/composition-results.json](package/composition-results.json).

Hosted tuple CI no longer installs a Dapr CLI. No selected test needs one, and the transport test now asserts
that every generated `*-dapr-cli` executable uses its fixture path.
