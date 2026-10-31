# Story 2.8 versioned workflow checkpoint

PR: https://github.com/Hexalith/Hexalith.Builds/pull/149
Base: 57255b502fdd546bb09851d04fc1830175d897f3
Current source: 26ba857a74d75b184a0340a07f280dca3bb08874
Worktree: /tmp/hexalith-story28-domain-ci-v2

The 15-file producer change adds the strict domain-ci-v2.yml entry point, a distinct
exact workflow identity and direct integration-helper closure binding. Both legacy
and v2 validator modes run in producer CI and release. V2 requires declared suites,
isolated success, dedicated pull-only secrets, exact owner finalization and bounded
diagnostics. It rejects obsolete direct integration/AppHost input paths and removes
16 silently unused legacy-only inputs. Initial downloads and redirects require HTTPS.
No legacy fallback authorizes a v2 reference. The existing C# candidate consumer is
unchanged and unenrolled.

The legacy workflow and its dedicated documentation are byte-identical to the base.
Legacy workflow SHA-256:
510b6e8d868c659846f921ed531d9c582468e718ad2c1caae9f506abd6817d14
final-source.json records every committed changed-source SHA-256, clean/normalized
working-byte parity, original shared dirty-source preservation and source limits.
source-before.json and validation-source.json remain historical observations. The
original shared Builds checkout was not reset, staged, committed or overwritten.

The scoped .gitattributes exception preserves authored CRLF in the existing publisher
workflow test file (1298 original lines; 1314 current lines), avoiding a file-wide
line-ending conversion. Existing provenance source attributes already do the same.

Local verification:
- Legacy validator: 411 assertions; unchanged by the HTTPS follow-up.
- V2 validator: 254 assertions on final source.
- Provenance/policy/source controls: 95 tests on final source.
- Affected governed publisher workflow controls: 56 tests on final source.
- Preparation controls: 11 tests; source unchanged by the HTTPS follow-up.
- Integration helper controls: 24 tests; helper source unchanged throughout this phase.
- Earlier broad publisher baseline: 159 tests before final test-fixture extraction.
- actionlint 1.7.12: pass on both entry points and producer CI/release workflows.
- Diff whitespace checks: pass. Changed Python lines have no selected style findings;
  seven pre-existing findings remain recorded in python-style.json.
- Real curl invocation refuses HTTP with exit 1 before network access.

The existing narrowly scoped actionlint job.workflow_* compatibility rules also apply
to v2. Current GitHub Cloud supports these properties; the local linter schema is
older. No workflow identity or authorization check was relaxed:
https://docs.github.com/en/actions/reference/workflows-and-actions/contexts#job-context

Final follow-up commands (worktree above; outputs retained as *-https.txt):
```sh
pwsh -NoProfile -File Tools/test-domain-workflow-test-platforms.ps1 -Contract v2
python3 -m unittest discover -s Github/governed-provenance/tests -p 'test_*.py'
python3 -m unittest discover -s Github/publish-containers/tests -p 'test_governed_release_workflow.py'
/home/administrator/.local/bin/actionlint .github/workflows/domain-ci.yml .github/workflows/domain-ci-v2.yml .github/workflows/ci.yml .github/workflows/build-release.yml
git diff --check
curl --fail --location --proto '=https' --proto-redir '=https' --retry 3 http://127.0.0.1:1
```
The last command must return 1, with HTTP disabled. The command/exit receipt is
https-focused-commands.json. Both commits, the exact title and final squash text
passed owning lockfile-pinned commitlint 21.2.4; evidence is retained. Both pushes
were normal non-force pushes. PR metadata uses pr-body-final.md.

Superseded compatibility proposals were verified at their prepared heads, explained
and closed unmerged: Commons51, Folders15, Conversations11 and Tenants58. See
superseded-pr-head-check.json and superseded-pr-closures.json. No caller source,
settings, protections or dependency pins changed; branches remain available.

PR checks are a timestamped snapshot in checkpoint.json, checks-final.json and
pr-final.json. The initial d11 Sonar findings and the resulting HTTPS/test-call fix
are retained separately; pending checks are not reported as successful. No workflow
run was cancelled, bypassed or retried unchanged.

Story status remains in progress. This checkpoint performs no merge, publication,
registry provisioning, E/P adoption or hosted reference/candidate acceptance. Compatible
SDK/tool/image releases and actual cancellation/partial-startup reference qualification
remain pending. The exact-main cleanup contention failure and SDK historical-matrix
failure are separate recorded prerequisite tasks; no runtime/test fix was folded into
this producer PR. No new .NET rebuild or package archive was claimed in this phase.
