# Actor-history lifecycle hardening: local source verification

The bounded source change is verified: `Move` rejects a lower epoch and a lower fence within the same epoch; a higher epoch may restart at a lower nonnegative fence. `AddGeneration` accepts a new generation only in Active, and returns the exact original record for a known generation in every state. Full original unit/deadline, authority, operation, receipt and generation facts remain unchanged after rejected calls. Foreign-unit retries and additions are rejected across all lifecycle states.

The final Debug build passed with **0 warnings and 0 errors**. Both required xUnit v3 classes executed: **136 passed, 0 failed, 0 errors, 0 skipped, 0 not run** (89 policy cases, 47 cleanup cases). Before changing lifecycle source, 14 of 18 selected regression cases failed, demonstrating the missing guards. The four passing cases were unknown generation gaps already rejected by the prior ordering rule.

This is local contract evidence only. **Available = false; installed qualification = false.** No production history provider, independent nonrollback authority, host installation, provisioning, credentials or owner approval was supplied. Constructor/deserialization snapshots cannot establish currentness. An Active snapshot still requires external owner authority and exclusive deadline checks at use. Branch B, immutable tenant/Party identity, the original binding-effective instant plus 31,536,000 seconds, and exclusive expiry remain unchanged. Pre-expiry destruction receipts and Q7 erasure boundaries were not restricted. Missing production custody remains tested by `AddPlatformCustody_LeavesHistoryCustodyUnregistered` and the existing default cleanup test.

## Checkout provenance

The implementation spec's Parties baseline and observed Platform planning HEAD were both checked directly against Git before editing and matched. These are source observations, not installed or accepted targets. Every existing dirty checkout and unrelated change was preserved; no Git mutation or dependency update was performed. The relevant checkout HEADs remained unchanged during validation. See [provenance-before.json](provenance-before.json) and [provenance-after.json](provenance-after.json) for exact paths, commands, status output and Git diagnostics.

| Checkout | Full canonical HEAD |
| --- | --- |
| parties | `13a6247aff26a77c80d0308b6c69af2bc3e20279` |
| platform | `f84977132777decec2f45a199d9392de73b6cc84` |
| eventstore | `fd083a91b67a3bca10016f1f645da913c7ef526e` |
| platform-builds | `68e65f2023eca117a2aa08a022ee33d8f4c34a44` |
| eventstore-builds | `6e40ea5f6243391bea80084b8ddd695df58fb1ff` |
| eventstore-commons | `1663028fb6d9f46a0bf17919edeb21bb99be1587` |
| platform-instructions | `3f194e17174994d308ec84af9ee2b5aa68674d0d` |

## Commands and raw outcomes

Fresh build artifacts were isolated under `/tmp/ext-parties-1-hardening-20261025-1pt6kz0x`; regression and final builds used separate artifact directories. No shared repository build outputs were overwritten. [commands.json](commands.json) retains exact argument arrays, working directories, timestamps, exit codes and log hashes. [manifest.json](manifest.json) binds source/evidence hashes and built assembly hashes; [source.patch](source.patch) is scoped to the two implementation files. [source-diff.json](source-diff.json) also preserves exact patch CRLF bytes inside JSON. Original source bytes are retained under `original/`.

- `regression-build`: exit 0; [regression-build.log](regression-build.log).

```bash
cd /home/administrator/projects/hexalith/platform
dotnet build tests/Hexalith.Platform.Custody.Tests/Hexalith.Platform.Custody.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore -p:NuGetAudit=false -m:1 --artifacts-path /tmp/ext-parties-1-hardening-20261025-1pt6kz0x/regression-build
```

- `regression-before-fix`: exit 1; [regression-before-fix.log](regression-before-fix.log).

```bash
cd /home/administrator/projects/hexalith/platform
dotnet /tmp/ext-parties-1-hardening-20261025-1pt6kz0x/regression-build/bin/Hexalith.Platform.Custody.Tests/debug/Hexalith.Platform.Custody.Tests.dll -method '*Move_RejectsRegressingAuthorityWithoutChangingOriginalFacts' -method '*AddGeneration_RejectsUnknownGenerationOutsideActive' -result-xml /tmp/ext-parties-1-hardening-20261025-1pt6kz0x/regression-before-fix.xml
```

- `final-focused-tests`: exit 1; [final-focused-tests.log](final-focused-tests.log).

```bash
cd /home/administrator/projects/hexalith/platform
dotnet /tmp/ext-parties-1-hardening-20261025-1pt6kz0x/final-build/bin/Hexalith.Platform.Custody.Tests/debug/Hexalith.Platform.Custody.Tests.dll -class '*IdentityHistoryCustodyPolicyTests' -class '*IdentityHistoryCleanupTests' -result-xml /tmp/ext-parties-1-hardening-20261025-1pt6kz0x/final-focused-tests.xml
```

- `final-build`: exit 0; [final-build.log](final-build.log).

```bash
cd /home/administrator/projects/hexalith/platform
dotnet build tests/Hexalith.Platform.Custody.Tests/Hexalith.Platform.Custody.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore -p:NuGetAudit=false -m:1 --artifacts-path /tmp/ext-parties-1-hardening-20261025-1pt6kz0x/final-build
```

- `final-focused-tests-complete`: exit 0; [final-focused-tests-complete.log](final-focused-tests-complete.log).

```bash
cd /home/administrator/projects/hexalith/platform
dotnet /tmp/ext-parties-1-hardening-20261025-1pt6kz0x/final-build/bin/Hexalith.Platform.Custody.Tests/debug/Hexalith.Platform.Custody.Tests.dll -class '*IdentityHistoryCustodyPolicyTests' -class '*IdentityHistoryCleanupTests' -result-xml /tmp/ext-parties-1-hardening-20261025-1pt6kz0x/final-focused-tests-complete.xml
```

- `scoped-diff-check`: exit 0; [scoped-diff-check.log](scoped-diff-check.log).

```bash
cd /home/administrator/projects/hexalith/platform
git -c core.whitespace=cr-at-eol diff --check -- src/Hexalith.Platform.Custody/IdentityHistoryLifecycleRecord.cs tests/Hexalith.Platform.Custody.Tests/IdentityHistoryCustodyPolicyTests.cs
```

## Recorded failures and limits

`regression-before-fix` intentionally failed against unchanged lifecycle source: 18 executed, 14 failed, 4 passed. The raw log and XML retain the counterexamples; these do not represent final-source failures.

One `final-focused-tests` runner invocation was issued before the build had produced the assembly. No tests ran. Its verbatim failure follows; the exact command is retained above. The build then completed successfully, and `final-focused-tests-complete` executed and passed all 136 selected tests.

```text
Could not execute because the specified command or file was not found.
Possible reasons for this include:
  * You misspelled a built-in dotnet command.
  * You intended to execute a .NET program, but dotnet-/tmp/ext-parties-1-hardening-20261025-1pt6kz0x/final-build/bin/Hexalith.Platform.Custody.Tests/debug/Hexalith.Platform.Custody.Tests.dll does not exist.
  * You intended to run a global tool, but a dotnet-prefixed executable with this name could not be found on the PATH.

```

The ordinary unconfigured `git diff --check` treated preserved CRLF line endings as trailing whitespace. The recorded successful scoped check uses the per-command `core.whitespace=cr-at-eol` setting; both edited C# files retain their original CRLF encoding. No repository configuration was changed.

No environment-blocked build or test remains. The focused project build and direct assembly execution are the baseline's first two validation lanes; a Release fallback was unnecessary. Broader tests were not required because the focused evidence exposed no relevant regression. Production custody, independent current authority, owner decisions Q1-Q7, prerequisite infrastructure, installed all-copy/restart/restore evidence, F1 and complete F2/P-01-P-10 qualification remain outstanding.
