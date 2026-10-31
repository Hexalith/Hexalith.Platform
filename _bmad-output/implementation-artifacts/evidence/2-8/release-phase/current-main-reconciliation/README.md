# Story 2.8 current-main reconciliation

PR: https://github.com/Hexalith/Hexalith.Builds/pull/149
Head: fb949ec9c41587279b1e153a3f9d00c7ad3cfc37
Merge parents: 46365cd94f8501f513f1cc413916ac24737d33e2 and
f6834a33967f8ec905145db14e1a13ffb5468d69.

The updated Story 2.8 spec supersedes the provisional production wait with the
current-main test-only refusal/retry contract. A normal branch merge preserves
PR150's incoming specification and every incoming test/control byte. The Engine
and entire src tree are identical to current main. No stopping allowance,
production cleanup/withdrawal/admission/retention rule or first outcome changes.
The two transient-wait cases and their helper extraction are removed.

The only test delta to current main is the additional active-attachment case,
renamed OutcomeLockImmediatelyRefusesCleanupUntilExactRetryAsync. It has no elapsed
or artificial-deadline expectation. It reuses the incoming exact HXR037/HXT008/hint
assertion and preserves the active attachment, workspace/claims and lack of a
withdrawal report while metadata is locked. Same-owner retry and idempotent cleanup
preserve the first outcome/status/cause/time and original attachment expiry.

The final PR diff to main contains 16 files: the 15 reviewed v2 producer files,
unchanged from 26ba857, plus 49 added lines in LocalRunLifecycleTests.cs. Legacy
workflow/docs and incoming specification are byte-identical to main; removing our
single added case from the merged test source reproduces the incoming file exactly.
final-source.json records every PR blob hash and these preservation checks.

Fresh same-source verification:
- Complete Debug project graph build: zero warnings/errors; no prior test binary.
- LocalRunLifecycleTests: 59 passed, including the four original startup cases,
  both incoming live-process run-id/session refusal/retry controls and our new case.
- CiRunLifecycleTests: 16 passed, including withdrawal-before-stop, partial startup,
  first outcomes, no CI retention and unchanged stopping-token controls.
- Existing legacy bookkeeping controls: all three passed on the same binary.
- Total 78 passed; zero failed/skipped/errors. No broader local suite was rerun.

Build command:
```sh
dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj --configuration Debug -m:1 -p:NuGetAudit=false
```
The local audit setting does not change normal hosted audit/gates. test-command.json
retains exact serialized xUnit commands, complete simulated GitHub environment,
raw compile-source and binary hashes. Direct xUnit uses -parallelMode none and
-result-xml. The resolved merge-index tree compiled into the binary exactly equals
the committed fb949ec9 tree; binary metadata may still carry the checkout's prior
46365cd identity. No copied binary or no-reference-rebuild lane was used. Raw CRLF
compile-source and normalized Git hashes are distinct and recorded. External
technical dependencies remain catalog packages. This is local Debug source proof,
not hosted reference, Release/package or candidate acceptance.

The exact full merge message, final PR title/body/squash candidate and commit range
passed owning lockfile-pinned commitlint 21.2.4. The normal merge, non-force push and
normal PR metadata update are retained. Current checks are timestamped separately
in checkpoint.json/checks-checkpoint.json; complete current-base hosted gates remain
mandatory before a parent PR merge. No gate or protection was bypassed.

Earlier red/failed controls, provisional production source and 78-case evidence in
../cleanup-contention remain historical and unchanged. Original shared Builds HEAD,
index and captured dirty-source hashes remain unchanged. Reviewed v2 source and
frozen intent are preserved; no caller, accepted dependency pin or registry account
was activated. No PR merge or publication was performed in this handoff.

Story remains in progress. Compatible released helper/tool/reference inputs,
registry provisioning and actual hosted reference cancellation/partial-startup
qualification remain pending. Coordinator separately reports SDK 3.130.0 publication
from 484f4bd and normal PR 378 merge 1de42b7, with new-main qualification running;
this Builds reconciliation performed no EventStore operation or release dispatch.
