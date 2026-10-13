# Timesheets local source host binding — 2026-10-13

The file entry's explicit `Platform:Timesheets:Enabled=true` Development/source opt-in now
binds the actual mapped Timesheets host in the existing Platform model, alongside
the six EventStore/Tenants/Memories reference servers. The Builds source path opts
in through an explicitly selected `timesheets` plan whose sole declaration is the
retained Platform candidate and whose `PlatformDeclarationHash` matches its bytes;
an ambient flag cannot expand another signed plan. Default selection remains
inert. The retained candidate and blocked preparation plan are unchanged.
**Deployment and activation remain unauthorized; Story 3.6/Epic 3 remain in
progress and release remains FAIL.**

The implementation was validated against Platform
`8c1fcbc115b21b26528fb00e47edc5105fdafc6b`, mapped Timesheets
`97b72bb4bb0ea7ade05a25fbd3f053392604dc97` and root EventStore
`93982b4e1fe312169c57e2735c90be24b49460fb`. The user-authorized spec retains its
planning baseline `ce6f2d34398803644b29d02ba57625898190ce20`; intervening
external commits and checkout/index state were preserved.

## Executed verification

Commands ran from the Platform root using SDK `10.0.401`. Builds used disabled
build servers and serialized MSBuild. Test execution used the project's individual
xUnit runner; no live-suite opt-in was enabled.

```bash
dotnet --version
dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj \
  --disable-build-servers -m:1 -p:UseSharedCompilation=false \
  -p:BuildInParallel=false -nodeReuse:false
dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll \
  -class Hexalith.Platform.Composition.Tests.TimesheetsHostBindingTests
dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll
dotnet build references/Hexalith.Timesheets/src/Hexalith.Timesheets/Hexalith.Timesheets.csproj \
  --configuration Debug --disable-build-servers -m:1 \
  -p:HexalithEventStoreRoot=/home/administrator/projects/hexalith/platform/references/Hexalith.EventStore \
  -p:UseSharedCompilation=false -p:BuildInParallel=false -p:NuGetAudit=false -nodeReuse:false
python3 -m unittest discover -s eng/timesheets-deployment -p 'test_*.py' -v
git diff --check
```

The following checks describe the initial implementation before the review fixes.
The full runner, owner host build and Python suite were not repeated in the
focused repair lane; full verification follows separately.

| Check | Initial implementation result | Temporary local diagnostic pointer |
| --- | --- | --- |
| Composition test-project build | PASS, zero warnings/errors | `/tmp/platform-timesheets-binding-build.log` |
| Timesheets binding matrix | PASS, 19/19, no skips, 0.816s | `/tmp/platform-timesheets-binding-focused.log` |
| Full composition runner | PASS, 73 passed, 3 live suites skipped, 76 total, 1.782s | `/tmp/platform-timesheets-binding-full.log` |
| Mapped Timesheets host build | PASS, zero warnings/errors, 17.71s | `/tmp/platform-timesheets-binding-host-build.log` |
| Offline preparation Python suite | PASS, 31/31, 5.303s | `/tmp/platform-timesheets-binding-python.log` |
| Whitespace | PASS | `git diff --check` |
| Preservation | PASS, seven protected file hashes and every index gitlink match the pre-implementation snapshot | `/tmp/platform-timesheets-binding-preservation-20261013.json` |

All `/tmp` log pointers and the preservation snapshot are temporary local
diagnostics. They may disappear and do not constitute a retained evidence archive,
release artifact bundle or deployed acceptance record. The executed results and
limitations are recorded in this repository document.

The host build explicitly resolved EventStore Contracts, Client, ServiceDefaults
and DomainService from Platform's existing root EventStore checkout. No nested
submodule was initialized and no owner source was edited. The three skipped
composition tests require separate Docker/Dapr runtime opt-ins: two Builds source
startup tests and the reference file-entry readiness smoke.

The matrix verifies both the file-entry and Builds-source configuration paths,
the six-server default, seven-server enabled closure, missing mapping/project/
candidate refusal, exact Timesheets executable ownership, package/production/
staging/publish refusal, incompatible Works/Timesheets refusal and retained Agents
unavailability. It checks declared `/ready`, distinct named listeners, the internal
endpoint's exclusion from external/reference advertisement, guard port expression
and `AllowOnAnyPort=false`, Dapr's `internal` endpoint selection, the DB14 component's
exact name and Timesheets-only scope against composition-owned Redis, shared local
signing, retained gateway caller slots, domain registration and app-channel token
propagation. LiveDaprFixture is explicitly false. These are preparation, graph,
configuration and build checks; they do not show a running host reaching readiness
or receiving deployed deliveries.

## Earlier attempts

The first two compilation attempts failed on new test code: nonconstant environment
values used in default/attribute arguments, then an unavailable Dapr component
annotation type. They were corrected to literal test cases and the actual
`DaprComponentResource.Options` API. Logs are
`/tmp/platform-timesheets-binding-build-attempt1.log` and
`/tmp/platform-timesheets-binding-build-attempt2.log`.

The first matrix run had 12 passes and two failures (14 total, 0.787s). One failure
exposed that the generic mapping membership check allowed a Timesheets project
override to select another mapped owner. The binding now requires the exact
Timesheets host under its selected owner root. The other was a test assertion
assuming absolute manifest diagnostics; Builds returns a relative source path,
which the assertion now resolves. Its log is
`/tmp/platform-timesheets-binding-focused-attempt1.log`.

The first full run had 67 passes, one failure and three skips (71 total, 1.423s).
The existing workspace-cleanup test snapshots the shared temporary directory;
the new class composed another workspace concurrently. The new collection now
disables parallel execution, leaving the existing test unchanged. Its log is
`/tmp/platform-timesheets-binding-full-attempt1.log`. The final results above
include the additional Builds, authentication/profile and entry-route cases.

## Review fixes and focused verification

The Builds opt-in now comes from the selected source plan, without manually
setting an ambient flag. Its sole declaration must be the canonical retained
candidate, the selected module must be Timesheets, and the plan hash must match
the current candidate bytes. Wrong, missing, additional or byte-drifted candidate
selections and ambient expansion of reference plans are refused before resources.
The automatic candidate is canonicalized before deduplication.

The enabled gateway's actual `Authentication:DaprInternal` options now accept the
`eventstore` audience used by Timesheets assertions. The shared SDK Development
JWT validator rejects short/blank keys, blank issuer/audiences and incompatible
algorithms before resources are added. Existing callers and default-deny providers
are preserved. The tests bind the host and gateway workload options and validate
their JWT contracts; generated gateway configuration is bound into
`DomainServiceOptions` to check `AppId=timesheets`, `MethodName=process`,
`TenantId=*`, `Domain=timesheets` and `Version=v1`.

Timesheets project containment is physically checked under its selected owner
root through Builds' existing `RepositoryPathResolver`, including a symbolic link
into another mapped owner and a canonical host absent from a mapped temporary
root. The existing four-project Works fixture now exercises the positive
`ComposeEntry` route with Timesheets disabled.

Only the Timesheets matrix and the edited positive Works test run in this repair
lane after the serialized test-project build:

```bash
dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll \
  -class Hexalith.Platform.Composition.Tests.TimesheetsHostBindingTests
dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll \
  -method Hexalith.Platform.Composition.Tests.PlatformAppModelTests.WorksPreviewComposesItsPreservedGraphWithoutDeclarationServers
```

| Focused repair check | Result | Temporary local diagnostic pointer |
| --- | --- | --- |
| Serialized test-project build | PASS, zero warnings/errors, 30.18s | `/tmp/platform-timesheets-binding-review-fixes-build.log` |
| Timesheets binding matrix | PASS, 35/35, no skips, 1.229s | `/tmp/platform-timesheets-binding-review-fixes-focused.log` |
| Positive Works entry route | PASS, 1/1, no skips, 0.291s | `/tmp/platform-timesheets-binding-review-fixes-works.log` |

No wider test suite, owner build or live startup was rerun in this repair lane.

## Final verification after review fixes

The parent session independently inspected the implementation and repair diffs,
audited the executed matrix, and completed the full verification after the
focused repair lane. All accepted review findings were fixed; no findings were
deferred. A live smoke was not performed, and the limitation above remains.

| Final check | Result | Temporary local diagnostic pointer |
| --- | --- | --- |
| Composition build after fixes | PASS, zero warnings/errors, 30.18s | `/tmp/platform-timesheets-binding-review-fixes-build.log` |
| Focused Timesheets matrix | PASS, 35/35, no skips, 1.229s | `/tmp/platform-timesheets-binding-review-fixes-focused.log` |
| Full composition runner | PASS, 89 passed, 3 expected live-suite skips, 92 total, 3.788s | `/tmp/platform-timesheets-binding-final-full.log` |
| Mapped Timesheets host build | PASS, zero warnings/errors, 3.17s | `/tmp/platform-timesheets-binding-final-host-build.log` |
| Offline preparation Python suite | PASS, 31/31, 7.975s | `/tmp/platform-timesheets-binding-final-python.log` |
| Preservation and whitespace | PASS, seven protected hashes and all index gitlinks unchanged | `/tmp/platform-timesheets-binding-preservation-20261013.json` |

## Preserved qualification limits

Default-deny Timesheets access/reference/migration registrations and unavailable
inventory providers remain in owner source. Local symmetric workload authentication
is Development material and supplies no production resource-binding issuer or
qualified access/reference providers. Certified cross-tenant inventory, enforced
maintenance and migration authority, source/pending-delivery continuity, deployed
subscriber/shared-rebuild evidence and retained release artifacts remain absent.

The candidate SHA-256 remains
`b86836a73c5b4c1c6bccdd45f3fc908c467a0f9ccfb2aa02c71025bb7e808618`;
the retained plan remains
`04b24f38d1910390bb075b8fe27e8f159ba202b6aeacf35f517bcc272a8043dc`.
Platform and owner tracking, the owner resume checkpoint and pinned qualification
report match the preservation snapshot. Its report SHA-256 remains
`f3fa3e844f0e23f732371e80258945feede0866b0608e5768457433ebacb2118`.

No AppHost was started, cluster deployment performed, domain command issued,
legacy state initialized or migrated, fixture grant enabled, release artifact
published, or commit/push performed. Local composition/build/test success grants
no deployed acceptance or release authority.
