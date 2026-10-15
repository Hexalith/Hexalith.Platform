# Focused parent-audit verification — 2026-10-15

Commands ran from the Platform root. Build/restore-sensitive commands ran sequentially. Every result in the current verification table has an executed passing log; the failed attempted driver is retained separately and is not an all-passed sequence.

```sh
dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -m:1 --nologo
HEXALITH_PLATFORM_CLI_EVIDENCE=/tmp/story1-7-cli-subscriptions.json dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -parallelMode none
dotnet build references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1 --nologo
dotnet references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -class '*PlatformManifestValidationTests' -class '*WorkspaceAppHostTests'
python3 scripts/test-reference-module-declarations.py
dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -m:1 --nologo
dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -method '*InvalidDaprBindingsReturnSourceAndFieldWithoutDeclarations'
HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE=1 HEXALITH_DAPR_HOME=/tmp/story-1-6-builds-toolchain HEXALITH_PLATFORM_SECURITY_EVIDENCE=/tmp/story1-7-audit-security-evidence dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -method '*LiveLocalDaprEnforcesInvocationAndDeclarativeDeadLetterDeliveryAsync'
HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE=1 HEXALITH_DAPR_HOME=/tmp/story-1-6-builds-toolchain HEXALITH_ASPIRE_CLI=/tmp/story1-7-aspire-cli/aspire HEXALITH_PLATFORM_BUILDS_SMOKE_LOG=/tmp/story1-7-audit-builds-tenants-apphost.log dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -method '*DomainTenantsStartsThroughTheRealBuildsEngineAsync'
python3 .agents/skills/bmad-architecture/scripts/lint_spine.py --workspace _bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27
python3 -m py_compile eng/test-local-dapr-security.py eng/verify-platform-pins.py
git diff HEAD --check -- . ':(exclude)_bmad-output/implementation-artifacts/evidence/' ':(exclude)Hexalith.Platform.slnx' ':(exclude)eng/ext-secrets-1/'
git -C references/Hexalith.Builds diff HEAD --check
git -C references/Hexalith.EventStore diff HEAD --check
git -C references/Hexalith.Memories diff HEAD --check
git -C references/Hexalith.Tenants diff HEAD --check
docker ps --format '{{.Names}}\t{{.Ports}}'
```

The CLI subscription-path and full-package fixture tests also passed in a separate two-test execution; both execute again within the 118-test ordinary suite. Their runtime processes are not launched by that assertion: it executes Aspire lifecycle callbacks and checks actual generated CLI arguments and Subscription contents. The package fixture injects test provenance hashes and an image-probe runner; it qualifies the supported graph, not released images.

After the full suite/source smoke, two test-only cases were added for conflicting configuration keys and recovery classes. The affected invalid-binding theory was rebuilt and executed in full (12 cases), asserting refusal, null declarations, source and field diagnostics. Production code did not change and source smokes were not repeated for these test-only additions.

The security fixture uses `/tmp/story-1-6-builds-toolchain/.dapr/bin/daprd` directly. Its exact arguments and copied-resource transport adaptation are in `security/runtime-commands.json` and `security/transport-adaptation.json`. Dapr CLI 1.18.0 remains checked as a prerequisite but is not its launcher. Production source smoke uses isolated Aspire CLI 13.6.1 and real toolkit-generated Dapr CLI resources, with runtime 1.18.2. Docker 29.8.2 and .NET SDK 10.0.401 are available; initial pinned-image digests and tool observations remain in `../prerequisites.txt`.

`attempted-sequence.py` and `attempted-sequence.txt` retain the first focused sequence, which stopped at the owned Sentry port-binding failure. That sequence did not reach its later source/lint checks. The passing direct-runtime fixture, signed-source smoke and final checks above were executed afterward and have their own logs. The original rendered YAML and adapted copies are retained without certificate credentials.

The initial global `git diff HEAD --check` reported trailing whitespace in preserved raw logs and concurrent unrelated slnx/ext-secrets edits. Its full output is retained in `global-whitespace-check.txt`. The passing root check above deliberately covers Story source/docs while preserving those raw bytes and unrelated files; the four owner checks cover their full tracked diffs. `final-checks.py` and `final-checks.txt` record the precise final commands/results.
