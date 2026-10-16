# Story 1.7 verification — 2026-10-15

The local implementation and review workflow are complete. The spec is `done` and sprint tracking is `review`; owner review/merge/release, catalog promotion and live package-image qualification remain pending.

The final resumed run on 2026-10-16 passed 148 Platform tests total (143 passed, five gated skips), 145 Builds regressions, 12 EventStore Aspire cases, 19 EventStore Server cases and seven source-mapped Memories HTTP host cases. Fresh live security/delivery, technical signed source and final six-server signed source runs passed with owned cleanup. Earlier startup failures are retained and reliable cold startup remains unqualified. Three review layers completed; the Memories host verification gap is fixed and independent follow-ups remain deferred. See [current commands, results, failures and diagnostic boundaries](resumed-20261016T074246Z/verification.md).

The four later review patch findings were covered on 2026-10-16 with test-only changes. The full Platform suite now passes 142 total / 137 passed / five gated skips / zero failures. Solution/test-project builds, declaration checks, the helper gate, architecture lint and LocalScaffold also passed. These are model and actual generated CLI assertions; live tests and owner regressions below remain historical. See [the review-patch commands, logs and limits](review-patches-2026-10-16/verification.md).

## Final parent verification after independent review

All patch findings in the spec's review triage are fixed. The three independent code reviewers and both architecture reviewers completed; no intent/spec loopback was needed. The unverified readiness-probe question, pre-existing ambient-tool preflight gap and existing Redis projection wildcard limitation remain in the deferred ledger.

| Check | Executed result | Final evidence |
| --- | --- | --- |
| Solution and Platform test-project Debug builds | Both passed | [solution](parent-final/solution-build.txt), [test project](parent-final/platform-build.txt) |
| Full Platform executable suite | 134 total: 129 passed, zero failed, five gated skips; 31.823 s | [suite](parent-final/platform-tests.txt) |
| Builds manifest/workspace classes | 145 passed, zero failed/skipped | [tests](parent-final/builds-tests.txt) |
| EventStore assigned Aspire bindings | 12 passed | [tests](parent-final/Hexalith.EventStore.AppHost.Tests-tests.txt) |
| EventStore projection metadata/options and effective rate-limit configuration | 19 passed | [tests](parent-final/Hexalith.EventStore.Server.Tests-tests.txt) |
| Canonical-helper gate and historical Timesheets preparation | Six and 31 passed | [helper](parent-final/helper-tests.txt), [Timesheets](parent-final/timesheets-provenance.txt) |
| Reference declarations, LocalScaffold and architecture lint | 41 PASS checks; scaffold passed; zero lint findings | [declarations](parent-final/declarations.txt), [scaffold](parent-final/local-scaffold.txt), [lint](parent-final/architecture-lint.txt) |
| Final full direct-runtime security/subscription fixture | One passed; 7.516 s | [test](parent-final/security-live.txt), [result](parent-final/security/result.log), [commands](parent-final/security/runtime-commands.json) |
| Final real six-server signed Builds source run | One passed; 100.808 s test / 101.015 s command; readiness, owned teardown and workspace removal asserted | [test](parent-final/builds-tenants-live.txt), [AppHost observations](parent-final/builds-tenants-apphost.txt) |
| Python compilation and scoped source/docs/owner whitespace | Passed; root check recognizes the seed's existing CRLF convention | [exact commands and results](parent-final/results.json) |

The final full fixture proves allowed EventStore-to-Tenants and Tenants API-to-gateway POSTs return 200, all six corresponding wrong-caller/verb/path checks return 403, all four established Timesheets POST operations return 200, and the unlisted Timesheets caller returns 403. Scoped Memories ingestion and physical dead-letter forwarding succeed with zero programmatic registrations. It uses pinned `daprd` 1.18.2 directly and records the fixture-only Sentry transport adaptation; it does not qualify Dapr CLI launch. The separate final signed Builds run uses the pinned 1.18.0 CLI and Aspire 13.6.1 through actual composition.

Both final live runs completed owned cleanup. [The final inventory](parent-final/runtime-inventory-after-source.txt) contains only the pre-existing global Dapr containers. An unrelated Works AppHost stopped independently before these final live runs; no unrelated resources were terminated. The earlier full-fixture timeout alongside that AppHost is retained in [review-fixes/security-live.txt](review-fixes/security-live.txt), and its cause is not treated as a proved production defect.

The final Platform suite includes both actual gateway seeds, configured/declarative metadata, explicit-root/symlink certificate exclusion, zero-resource unsafe-path refusal, semantic binding conflicts, physical-topic filename uniqueness and malformed-template diagnostics. The affected owner classes include the newly added caller/path guards, structured timeout evidence, supplied YAML name validation, Admin.Server state binding and a configured-store sync that changes the effective tenant rate limit.

Earlier failed parent runs are retained separately: [explicit-root fixture failure](parent-initial-refusal/platform-tests.txt), [missing-key diagnostic failures](parent-builds-refusal/builds-tests.txt) and [unscoped/CRLF whitespace refusal](parent-final/whitespace-refusal/source-whitespace.txt). These are superseded by the final passing checks. Raw logs and unrelated changes were preserved. [Parent commands](parent-final/verify.py), [scoped/live continuation](parent-final/finish.py), [signed-run continuation](parent-final/signed-source.py) and [machine-readable results](results.json) identify the actual executions.

The five ordinary-suite skips are explicit gates. The full security fixture and six-server source test were rerun separately above; the narrow Timesheets fixture also passed during review corrections. The technical EventStore and sequential-prebuild file-entry startup retain their earlier passing evidence below. Cold default file startup and concrete Redis wildcard projection fan-out remain unqualified. Released package/container behavior remains unqualified; the package graph and gateway seed HTTP tests are source fixtures.

Source hashes in [changed-files.sha256](changed-files.sha256) cover the final scoped working-tree source/docs and owner changes. Audit-era results/hashes are preserved separately, and owner regressions not changed by review patches retain the historical evidence below. Unrelated secrets/recovery work is excluded from attribution and local commits.

## Parent audit verification — historical provenance

The parent audit exposed missing owner replacement subscriptions, missing Tenants API gateway business grants, and missing Subscription resource paths in the actual Aspire composition. These are corrected using owner source evidence. The package graph assertion now exercises the supported gateway project seed plus five owner containers; it does not qualify released images or add another gateway mode.

| Check | Executed result | Current evidence |
| --- | --- | --- |
| Platform test-project Debug build | Passed; zero warnings/errors | [build](audit/build.txt) |
| Platform executable suite | 118 total: 114 passed, zero failed, four gated skips; 29.201 s | [suite](audit/platform-tests.txt) |
| Test-only conflict follow-up | Twelve invalid-binding theory cases passed, including newly added configuration-key and recovery conflicts; production code unchanged | [build](audit/conflict-build.txt), [tests](audit/conflict-tests.txt) |
| Actual generated CLI subscription paths and full package graph | Two passed; 0.630 s; scoped Subscription files and actual lifecycle-generated arguments asserted | [tests](audit/cli-package-tests.txt), [CLI arguments and contents](audit/cli-subscriptions.json) |
| Builds affected manifest/workspace regressions | 138 passed; zero failed/skipped; 3.243 s | [build](audit/builds-build.txt), [tests](audit/builds-tests.txt) |
| Reference declaration matrix | 41 PASS checks | [log](audit/declarations.txt) |
| Expanded isolated security/subscription fixture | One passed; 6.370 s | [test](audit/security-live.txt), [result](audit/security/result.txt), [effective runtime commands](audit/security/runtime-commands.json) |
| Six-server Tenants through the real Builds source engine | One passed; 122.029 s; declared application/sidecar readiness, owned teardown and signed workspace/scheduler-data removal asserted | [test](audit/builds-tenants-live.txt), [runner observations](audit/builds-tenants-apphost.txt) |
| Architecture scoped rechecks and lint | Both independent reviewers passed; zero lint findings | [lint](audit/architecture-lint.txt), architecture `reviews/review-1-7-{reality,adversary}.md` |
| Python compilation and source/docs whitespace checks | Passed for scoped Story root source/docs and all four owners | [commands and exclusions](audit/commands.md) |

The expanded fixture launches **direct pinned daprd 1.18.2**, four small HTTP applications, Redis and Sentry. It proves `eventstore -> tenants /process` and `tenants-api -> eventstore` command/query POSTs return 200, while wrong callers, verbs and paths return 403. It proves Memories declarative ingestion, poison-message forwarding to physical target `failed.physical`, the declared dead-letter route, intended receiver scopes and zero programmatic registrations. HTTP discovery probes can receive 404; zero registrations does not imply zero probes. Owner endpoint/option regressions from the initial handoff are reused below because those implementation files did not change during this audit correction.

An unrelated AppHost initially held production Sentry port 50001. It was preserved. Only this fixture adapts copied `spec.mtls.sentryAddress` values and the matching runtime argument/Docker mapping to an allocated port. The [transport metadata](audit/security/transport-adaptation.json) records launcher, exact runtime path, host/container ports, field changes and original/adapted hashes; original renderer output and adapted copies are both retained. Production fixed-port behavior is unchanged and concurrent production runs remain unqualified. This runtime fixture **does not exercise Dapr CLI launch**. The actual Aspire-generated CLI path assertion and refreshed real signed-source smoke provide separate evidence for that path. The unrelated AppHost stopped independently before the current signed-source smoke; no unrelated resources were terminated.

The source-established EventStore pattern `*.*.projection-changed` is preserved. Publishing concrete topic `fixture-tenant.fixture-view.projection-changed` returned 204; no projection callback appeared within two seconds. The [bounded observation](audit/security/projection-observation.json) records the existing Redis wildcard limitation; concrete projection fan-out is not qualified and is deferred in the ledger. No literal-wildcard publication is used as delivery proof.

Failed attempts are retained separately: [production-port conflict](audit-port-conflict/test.txt) and [CLI transport-option refusal](audit-transport-option-refusal/test.txt). The failed-launch cleanup now tracks ownership before Docker launch, covering a container created before port binding fails. The final fixture reports no owned leftovers. The final [Docker inventory](audit/cleanup.txt) preserves the pre-existing global Dapr containers.

[Audit machine-readable results](audit-results.json), [exact focused commands](audit/commands.md) and [audit source hashes](audit-changed-files.sha256) superseded the initial broad verification at that stage. The earlier [results](pre-audit-results.json) and [hashes](pre-audit-changed-files.sha256) remain as provenance. The initial technical EventStore and sequential-prebuild file-entry smokes were not rerun after this audit; the refreshed full signed-source smoke covers subscription loading/startup at that stage. Four gated tests were skipped in that ordinary suite; two were rerun live after the audit, while the other two retain only their initial passing evidence.

Two conflict tests were added after the ordinary suite; the complete 12-case invalid-binding theory was rebuilt and passed afterward. The 118-test count is the actual earlier full-suite execution, not an invented 120-test rerun. A global whitespace check reported trailing spaces in preserved raw logs and concurrent unrelated files; its [refusal](audit/global-whitespace-check.txt) is retained. The passing root check excludes raw evidence and those unrelated edits; no evidence bytes or unrelated files were normalized.

Concurrent unrelated `Hexalith.Platform.slnx`, `eng/ext-secrets-1/`, secrets runtime/qualification projects, recovery-preparation files, Epic 4 evidence and the separate EXT-SECRETS spec are preserved and excluded from Story 1.7 source hashes/attribution. Parent staging/review is separate; this implementation agent did not stage or commit files.

## Initial handoff verification — historical provenance

The following table and remaining initial-run narrative describe the broad verification before the parent audit. Current corrected-code results and limitations are recorded above.

| Check | Result | Evidence |
| --- | --- | --- |
| Platform solution and test-project Debug builds | Both passed, zero warnings/errors | [solution](solution-build.txt), [test project](build.txt) |
| Platform ordinary executable suite | 110 total: 106 passed, zero failed, four gated skips; 32.443 s | [log](platform-tests.txt) |
| Builds manifest and workspace/AppHost regressions | 138 passed, zero failed/skipped | [log](builds-tests.txt) |
| EventStore domain-event delivery endpoint/metadata | 10 passed | [log](domainservice-tests.txt) |
| EventStore Aspire isolated/assigned bindings | Nine passed | [log](aspire-owner-tests.txt) |
| EventStore projection notifier options | 14 passed | [log](server-owner-tests.txt) |
| Memories ingestion and configured topic metadata | 15 passed, including assigned pubsub name and isolated default | [log](memories-eventstore-tests.txt) |
| Memories embedding/secret/hosting regressions | 83 passed, including assigned secret-store lookup | [log](memories-server-tests.txt) |
| Tenants API readiness and gateway routing | 17 passed, including real upstream 200/503 propagation for explicit health and isolated Dapr paths | [log](tenants-owner-tests.txt) |
| Real Dapr security/subscription fixture | One passed, zero skipped; 4.375 s | [test](security-live.txt), [result](security/result.txt), [runtime commands](security/runtime-commands.json) |
| Technical EventStore through real Builds source engine | One passed; 53.778 s | [test](builds-eventstore-live.txt), [runner observations](builds-eventstore-apphost.txt) |
| Six-server Tenants through real Builds source engine | One passed; 162.532 s | [test](builds-tenants-live.txt), [runner observations](builds-tenants-apphost.txt) |
| Real file entry with explicit sequential prebuild | Six successful owner builds, then one passed startup/readiness/stop test; 168.941 s | [prebuild](file-entry-prebuild.txt), [test](file-entry-live.txt) |
| LocalScaffold | Passed | [log](local-scaffold.txt) |
| Reference declaration matrix | 41 PASS checks | [log](declarations.txt) |
| Canonical helper gate | Three recorded helpers accepted; absent canonical set and unrecorded helper refused | [five checks](helper-gate.txt) |
| Architecture lint | Zero findings; required independent architecture reviews passed | [lint](architecture-lint.txt), architecture `reviews/review-1-7-{reality,adversary}.md` |
| Python compilation and whitespace checks | Passed; root and all four touched owner repositories checked | [driver](final-driver.txt), [executed commands](final-verification.py) |

The four gated Platform tests skipped in the ordinary suite were each executed separately and passed. Counts above describe actual executions; Builds and owner rows are the selected affected classes, not their entire repository suites. [Machine-readable results](results.json) retain counts and final sequential command outcomes.

## Evidence boundaries

The ordinary suite checks the resolved project/container graph, exact selected scopes, owner options, actual generated CLI environment, signed discovery, atomic diagnostics, package fixture guards and compatibility. These are model or in-process assertions.

The live security fixture uses the current renderer, actual pinned Dapr CLI/runtime, Redis, Sentry, and three small HTTP fixture applications. It proves permitted invocation **200**, denied caller/verb/path **403**, normal subscription delivery, forwarding to the declared dead-letter topic/route, receiver scope, and teardown. The fixture applications expose no programmatic registrations: the pinned CLI may still probe `/dapr/subscribe`, receiving 404. Zero registrations does not mean zero discovery requests. The real EventStore domain endpoint tests separately execute declarative-mode removal of topic metadata while retaining the delivery endpoint. Raw fixture logs, rendered policies/subscriptions and effective `daprd` arguments are in [security/](security/). No PEM credentials are retained.

The signed source tests run the actual selected owner projects and authenticated Sentry/placement/scheduler. They check declared application health, every selected sidecar's authenticated outbound health, and complete runner teardown: no remaining owned resources and removal of the signed workspace, including its scheduler data. They do not add invocation permissions for readiness. Tenants API still checks the actual gateway `/ready`; Platform supplies its resolved HTTP health endpoint through `EventStore:GatewayReadiness:BaseAddress`. The isolated Dapr readiness path is preserved and tested.

The package/container graph uses verified fixtures and injected image probes. Unmerged owner changes, unchanged released snapshots/provenance and unavailable release qualification prevent a claim that released owner containers implement this story. The unreleased-snapshot guard remains covered before resources are added. Existing snapshots, release commit/hash records and provider exceptions were not rewritten.

## Commands and prerequisites

Run from the Platform root. [final-verification.py](final-verification.py) records the exact final sequential build/test commands and class filters; [final-driver.txt](final-driver.txt) records their exit codes. Earlier affected EventStore and Tenants runs retain their build/test output. The required commands included:

```sh
dotnet build Hexalith.Platform.slnx -c Debug -m:1 --nologo
dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -m:1 --nologo
dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -parallelMode none
python3 scripts/test-reference-module-declarations.py
bash eng/verify-agents-host.sh --mode LocalScaffold --artifacts-path /tmp/story1-7-scaffold-artifacts
```

Live fixture and signed source runs set `HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE=1`, `HEXALITH_DAPR_HOME=/tmp/story-1-6-builds-toolchain` and `HEXALITH_ASPIRE_CLI=/tmp/story1-7-aspire-cli/aspire`. The fixture also sets `HEXALITH_PLATFORM_SECURITY_EVIDENCE=/tmp/story1-7-security-evidence`. The passing file-entry command was:

```sh
HEXALITH_PLATFORM_LIVE_SMOKE=1 HEXALITH_PLATFORM_PREBUILD_SOURCE=1 HEXALITH_DAPR_HOME=/tmp/story-1-6-builds-toolchain HEXALITH_ASPIRE_CLI=/tmp/story1-7-aspire-cli/aspire HEXALITH_PLATFORM_SOURCE_PREBUILD_LOG=/tmp/story1-7-file-entry-prebuild.log dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -method '*ReferenceServersReachDeclaredReadinessInTheFileEntryAsync'
```

[Prerequisite observations](prerequisites.txt): .NET SDK 10.0.401, usable Docker server 29.8.2, isolated Aspire CLI 13.6.1, Dapr CLI 1.18.0, explicit runtime 1.18.2, and available catalog Sentry/runtime images with recorded digests. The CLI's plain `--version` reports the global HOME runtime 1.18.7; live runs explicitly select the pinned runtime HOME and the retained runtime logs report 1.18.2. The global Aspire CLI 13.6.0 failed signed preflight HXR015; the isolated matching CLI resolved that prerequisite without changing global tools.

## Retained failures and limits

- Initial signed/full-file source checks exposed the denied Tenants API readiness invocation. The owner health endpoint fix preserves the dependency check without new ACL grants. [Signed refusal](builds-tenants-live-before-readiness-fix.txt), [file refusal](file-entry-live-before-readiness-fix.txt).
- Cold default file startup encountered shared owner output contention during concurrent project compilation. The owned AppHost was stopped cleanly. [Resource error](file-entry-build-lock.txt), [failed test](file-entry-concurrent-owner-build.txt). The passing file-entry result explicitly includes sequential prebuild; it does not qualify cold default startup.
- An overlapping plain owner build and source-mapped verification build caused HXW005 in one ordinary-suite run. [Failure](platform-tests-overlapping-builds.txt). All restore/build-sensitive final checks then ran sequentially and passed; the source guard was retained.
- The first trust-domain failure informed the architecture correction: local workload SPIFFE identities use `public`, while the control plane uses `localhost`. Its temporary raw fixture path was reused on rerun; the old raw failure is not retained. The corrected passing run is retained under `security/`.
- The affected EventStore AppHost test build emitted existing NU1903 warnings for `Microsoft.AspNetCore.SignalR.Common` 10.0.12. [Build log](aspire-owner-build.txt). No catalog upgrade was made for this story.
- Hosted policy, concurrent-run isolation and owner release/promotion remain outside this implementation. Full EXT-HOST-1 qualification remains unavailable, as the LocalScaffold command reports.

The final [Docker inventory](cleanup.txt) contains no Story 1.7 resources. Pre-existing global Dapr containers and concurrent unrelated `p1r-isolation-…` containers were preserved. Unrelated recovery-preparation files and Epic 4 evidence were not changed or staged. No files were staged or committed for this handoff.

The implementation sanity check read the root and all four touched owner diffs, including new source/tests, and ran each repository's `git diff --check`. [Changed-file hashes](changed-files.sha256) identify the reviewed working-tree content; parent review remains separate.
