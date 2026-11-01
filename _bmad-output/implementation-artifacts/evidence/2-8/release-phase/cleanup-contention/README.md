# Story 2.8 concurrent cleanup correction

PR: https://github.com/Hexalith/Hexalith.Builds/pull/149
Separate fix commit: 46365cd94f8501f513f1cc413916ac24737d33e2
Parent: 26ba857a74d75b184a0340a07f280dca3bb08874
Worktree: /tmp/hexalith-story28-cleanup-contention

Explicit down published cleanup-request.json and then refused an immediately busy
outcome.lock. Startup cancellation can briefly own that lock, so cleanup could return
HXR037/HXT008 despite remaining stopping time. The deterministic executing control
holds the real metadata lock across down for both prelaunch and running AppHost
paths. Against the unchanged runtime, all four original cases passed and both added
contention cases failed. See reproduction-source.json and reproduction-tests.xml.

CompositionEngine.cs now lets the initial cleanup outcome commit wait only when the
owner can admit attachments, using the existing stopping token and existing bounded
lock implementation. Withdrawal still commits before resource teardown. No CTS,
production timeout, schema, policy, retention or first-outcome rule changes. Legacy
owners whose context cannot admit attachments preserve immediate metadata refusal
and resource stopping. The waitForLock XML documents the actual allowance.

LocalRunLifecycleTests.cs retains all four original executing cases and adds two
controlled-lock cases. Small helpers keep those controls readable. The new persistent
lock case proves bounded HXR037/HXT008 refusal, untouched workspace/claims/active
attachment and no withdrawal report before cleanup. Exact same-owner retry and a
second idempotent cleanup leave zero owned resources, preserve the first outcome,
status, cause and time, and preserve the attachment's original expiry while recording
withdrawal and developer-stop.

Final verification uses one freshly built Debug graph and the complete simulated
GitHub environment retained in final-test-command.json:
- LocalRunLifecycleTests: 59 passed.
- CiRunLifecycleTests: 16 passed, including executing withdrawal-before-stop,
  partial startup, first outcomes, no CI retention and original stopping-token cases.
- Existing FailedBookkeepingStillStopsOwnedProcessAsync: all three legacy cases pass.
- Final total: 78 passed, zero failed/skipped/errors; build zero warnings/errors.
- Diff checks and exact lockfile-pinned commitlint 21.2.4 commit/title/squash/range pass.

The final build command was:
```sh
dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj --configuration Debug --no-restore -m:1 -p:NuGetAudit=false
```
The first fresh graph build restored without --no-restore. NuGetAudit=false is a local
build setting only; normal hosted audit/gates remain unchanged. Exact serialized
assembly commands, environment, source hashes and binary hashes are retained in
final-test-command.json. The binary was compiled while checkout HEAD was 26ba plus
the two recorded source hashes; those bytes normalize exactly to the committed 46365cd
blobs. No artifact copying or no-reference-rebuild lane was used. External technical
dependencies remain catalog packages. This local Debug proof is not exact committed
Release/package acceptance, hosted reference qualification or candidate acceptance.

Earlier diagnostics remain distinct:
- Incomplete simulated context lacked RUNNER_ENVIRONMENT; no reproduction credit.
  Only that owned local test process was interrupted. No remote run was cancelled.
- Initial test/comment analyzer failures are retained in their build outputs and fixed.
- The first unconditional-wait attempt passed 360/362 broader cases. Its two failures
  exposed legacy no-context cleanup losing its stop allowance and a fixture declaring
  four seconds while expecting a one-second refusal. Conditional waiting preserves
  the legacy behavior; the final fixture explicitly declares its intended one second.
  Its refusal assertions were preserved and strengthened. See unconditional-wait-*
  evidence; this failed broader invocation is not a passing final-source baseline.
- Only the two affected supported-owner classes and the actual legacy control were
  rerun after those corrections. Full normal exact-head hosted CI remains mandatory.

final-source.json records all 17 PR committed-source hashes, both raw CRLF compile
inputs and normalized committed hashes for the two changed C# files, tested binary
hashes, clean working state and original shared-source preservation. The other 15 v2
producer files are unchanged from 26ba; legacy domain-ci.yml and its dedicated docs
remain byte-identical to 57255b5. The original shared Builds HEAD, index and captured
dirty files remain untouched. No reference caller, accepted pin or frozen intent was
changed. The PR title/body now describe the complete final scope.

Checks are a timestamped snapshot in checkpoint.json and checks-checkpoint.json.
Source and evidence inventories from earlier phases remain historical. This stage
performed a normal commit, fast-forward, non-force push and normal PR metadata update;
no merge, publication, caller activation, registry provisioning, gate bypass or
unchanged workflow retry occurred. Story remains in progress. Compatible SDK/tool
releases and real hosted reference cancellation/partial-startup qualification remain
pending; the separate SDK historical compatibility failure remains a publication hold.
