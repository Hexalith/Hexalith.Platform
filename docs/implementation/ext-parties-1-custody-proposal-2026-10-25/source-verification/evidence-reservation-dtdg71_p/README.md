# Verifier evidence reservation review fixes

Both verifier entry points now reject existing evidence directories and atomically create the permanent `.ext-parties-1-attempt` file with `FileMode.CreateNew` before writing evidence. The parent delegates LiveReadiness before reserving; its helper owns exactly one reservation, before input checks or dependency lookups. The helper's final directory creation with `-Force` was removed. No bypass parameter was added.

The new `ExtPartiesVerifierEvidenceTests` class exercises the real scripts in bounded PowerShell child processes with cleared inherited environments except runtime discovery paths and explicit synthetic inputs. Nine cases cover changed-input Live/Local/LiveReadiness retries after an original Live refusal; direct readiness rejection before inputs/lookups; single direct/delegated startup reservations; and deterministic synchronized competing reservations for both entry points. The test-only directory-creation double lets both children pass the existing-directory check and report successful directory creation, so the marker must exclude one. Exactly one owner proceeds; the Live race records exactly one evidence writer. Byte-preservation, lookup and writer-count assertions remain intact.

Final verification: **9 passed; 0 failures, errors, skips or unrun cases**. CI Debug build: **0 warnings, 0 errors**. Only the new class ran; unchanged lifecycle tests and wider verification were left to the parent. **Available = false; installed qualification = false; no production qualification is supplied.**

## Exact verified source bytes

| Parties file | SHA-256 |
| --- | --- |
| `eng/verify-ext-parties-1.ps1` | `4dd903a4ee4f2667aee6908da9706493ce0783348675cbfa8fdc01de428ada00` |
| `eng/verify-ext-parties-history-readiness.ps1` | `77778b35dd7f419540498f2a3ca93e65b93855f636035893838351ac1eb727cd` |
| `tests/Hexalith.Parties.Ci.Tests/ExtPartiesVerifierEvidenceTests.cs` | `911430df06ebaa129623ced67815c6439032892fc4cefb092c14113da047acce` |

[manifest.json](manifest.json) records the same digests and full canonical checkout HEADs. Exact tested source bytes are copied under `verified-source/`. [review-fixes.patch](review-fixes.patch) scopes this pass to the two scripts and new class relative to the preserved pre-review working tree; its exact CRLF bytes are also retained inside [review-diff.json](review-diff.json).

## Commands and results

All build outputs use fresh directories under `/tmp/ext-parties-1-evidence-reservation-dtdg71_p`. [commands.json](commands.json) retains exact argument arrays, working directories, timestamps, exit codes and raw-log hashes. Final passing XML: [verifier-evidence-tests-fixed.xml](verifier-evidence-tests-fixed.xml).

`ci-build` — exit 0 ([raw log](ci-build.log)):

```bash
cd /home/administrator/projects/hexalith/parties
dotnet build tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj --configuration Debug -p:NuGetAudit=false -m:1 --artifacts-path /tmp/ext-parties-1-evidence-reservation-dtdg71_p/build
```

`verifier-evidence-tests` — exit 1 ([raw log](verifier-evidence-tests.log)):

```bash
cd /home/administrator/projects/hexalith/parties
env HEXALITH_PARTIES_SOURCE_ROOT=/home/administrator/projects/hexalith/parties dotnet /tmp/ext-parties-1-evidence-reservation-dtdg71_p/build/bin/Hexalith.Parties.Ci.Tests/debug/Hexalith.Parties.Ci.Tests.dll -class '*ExtPartiesVerifierEvidenceTests' -result-xml /tmp/ext-parties-1-evidence-reservation-dtdg71_p/verifier-evidence-tests.xml
```

`ci-build-fixed` — exit 0 ([raw log](ci-build-fixed.log)):

```bash
cd /home/administrator/projects/hexalith/parties
dotnet build tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj --configuration Debug -p:NuGetAudit=false -m:1 --artifacts-path /tmp/ext-parties-1-evidence-reservation-dtdg71_p/build-fixed
```

`verifier-evidence-tests-fixed` — exit 0 ([raw log](verifier-evidence-tests-fixed.log)):

```bash
cd /home/administrator/projects/hexalith/parties
env HEXALITH_PARTIES_SOURCE_ROOT=/home/administrator/projects/hexalith/parties dotnet /tmp/ext-parties-1-evidence-reservation-dtdg71_p/build-fixed/bin/Hexalith.Parties.Ci.Tests/debug/Hexalith.Parties.Ci.Tests.dll -class '*ExtPartiesVerifierEvidenceTests' -result-xml /tmp/ext-parties-1-evidence-reservation-dtdg71_p/verifier-evidence-tests-fixed.xml
```

`scoped-diff-check` — exit 0 ([raw log](scoped-diff-check.log)):

```bash
cd /home/administrator/projects/hexalith/parties
git -c core.whitespace=cr-at-eol diff --check -- eng/verify-ext-parties-1.ps1 eng/verify-ext-parties-history-readiness.ps1 tests/Hexalith.Parties.Ci.Tests/ExtPartiesVerifierEvidenceTests.cs
```

## Retained initial failures and process artifacts

The initial nine-case run had seven harness failures: nested script exit codes were not forwarded and a readiness error phrase was display-wrapped. The corrected wrapper explicitly propagates exit codes and emits caught exception messages plainly; delegated readiness checks use its stable refusal phrase. The original log/XML and all initial fixtures remain preserved. The final run executed every case successfully.

[process-fixtures.json](process-fixtures.json) maps all 18 initial/final fixture directories to test outcomes and hashes. Raw child commands, sanitized environments, stdout/stderr, exit codes, synchronization barriers, evidence markers, writer logs and retained evidence are copied under `process-fixtures/`; transient PowerShell module caches are excluded from the copies. Original temporary fixture directories were not deleted or reused. Earlier source-verification evidence and proposal files were not overwritten. Existing unrelated repository work was preserved.
