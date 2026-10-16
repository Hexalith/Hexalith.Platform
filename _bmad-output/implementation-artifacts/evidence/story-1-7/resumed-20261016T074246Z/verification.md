# Story 1.7 resumed verification — 2026-10-16

This run resumed the existing in-review implementation and pending patches without resetting the working tree. Three independent review layers examined the complete baseline diff, including owner changes and untracked files. Every finding has a separate verdict in the spec's final resumed triage. The one additional Story 1.7 patch extends the real Memories host's subscription-discovery HTTP test; the five new independent EXT-SECRETS-1 defects are recorded in the deferred ledger, and previously deferred findings retain their routes.

The pending production fixes cover Timesheets receiver/admin and gateway invocation declarations, case-insensitive ACL path/verb merging, descendant-rule ordering, missing secret-store credential metadata and process-disappearance cleanup. Tests additionally exercise graph-level recovery conflicts. The renderer uses the pinned provider's path matching semantics; the previously deferred exact-path limitation remains. See the [Dapr 1.18.2 trie](https://raw.githubusercontent.com/dapr/dapr/v1.18.2/pkg/config/acl_trie.go) and [ACL parser/evaluator](https://raw.githubusercontent.com/dapr/dapr/v1.18.2/pkg/acl/acl.go).

| Check | Observed result | Evidence |
| --- | --- | --- |
| Platform solution Debug build | Passed; zero errors, one transient MSB3026 copy retry | [build](build.txt) |
| Full Platform executable suite | 148 total: 143 passed, zero failed, five gated skips; 28.718 s | [suite](suite.txt) |
| Live security/subscription fixture | One passed, zero skipped; 7.642 s | [test](security-live.txt), [command](security-command.json), [fixture result](security/result.log) |
| Builds manifest/workspace regressions | 145 passed, zero failed/skipped | [tests](builds-tests.txt), [build/commands](regression-commands.json) |
| EventStore assigned Aspire bindings | 12 passed, zero failed/skipped | [tests](eventstore-aspire-tests.txt) |
| EventStore projection metadata/options and assigned-store rate limits | 19 passed, zero failed/skipped | [tests](eventstore-server-tests.txt) |
| Memories actual HTTP host middleware/subscription checks | Seven passed, zero failed/skipped, with a source-mapped Debug build | [tests](memories-final-http-tests.txt), [build](memories-final-build.txt), [commands](memories-final-commands.json), [source environment](memories-source-environment.json) |
| Technical EventStore signed source run | One passed, zero skipped; 44.488 s; readiness, teardown and workspace removal asserted | [test](builds-technical-diagnostic-live.txt), [command](builds-technical-diagnostic-command.json) |
| Final six-server signed source run | One passed, zero skipped; 119.514 s test / 119.771 s command; readiness, teardown and workspace removal asserted | [test](builds-final-live.txt), [command](builds-final-command.json), [filtered exception/build errors](final-error-observations.json) |
| File-entry LocalScaffold | Passed | [scaffold](local-scaffold.txt) |
| Fixture process-disappearance cleanup | Two passed | `python3 eng/test_local_dapr_security_cleanup.py` |
| Reference declarations | 41 PASS checks | `python3 scripts/test-reference-module-declarations.py` |
| Canonical hosting helper gate | Six passed | `python3 eng/test_verify_platform_pins.py` |
| Architecture lint | Passed, zero findings | `python3 .agents/skills/bmad-architecture/scripts/lint_spine.py --workspace _bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27` |

The live fixture uses pinned `daprd` 1.18.2 directly, with the retained fixture-only Sentry port adaptation. It proves allowed invocation, eight denied caller/verb/path cases, all five Timesheets receiver POSTs, all five established Timesheets gateway calls, scoped Memories delivery and physical dead-letter forwarding. It reports zero programmatic registrations and complete owned cleanup. [Effective commands](security/runtime-commands.json) and [transport adaptation](security/transport-adaptation.json) distinguish this from CLI launch qualification. Concrete Redis projection wildcard delivery remains unproven: its callback observation is false, and it remains deferred. Released package images and concurrent production runs are not qualified.

The initial Memories true-mode assertion used an anonymous request and received the host's existing fallback authentication challenge (401). The retained [initial test output](memories-http-tests.txt) records that failed assertion. The final test authenticates only the declarative-mode request so it can prove the route is absent (404); default and explicit false still exercise anonymous subscription discovery and retain all topic/route assertions. No authentication or server behavior changed. [Initial mapping/build commands](memories-commands.json) show the source-mapping utility used to build current owner projects without initializing nested references.

The first solution build overlapped the declaration script's CLI build and logged one resolved file-copy retry. All later build-sensitive checks ran sequentially. The passing suite and affected owner builds did not repeat that failure; the original warning remains in the raw build log.

The full Platform suite and live fixture cover the current production patches. The later Memories edit changes only its test; its seven focused host checks cover that edit. Earlier file-entry startup, other owner classes and package-fixture evidence remain historical in [the story record](../verification.md). Review, merge, owner releases and catalog promotion remain separate prerequisites.

The initial root build and suite commands were:

```bash
dotnet build Hexalith.Platform.slnx -c Debug -m:1 --nologo
dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -parallelMode none
```

Python compilation and scoped source/spec/ledger whitespace checks also passed, as did `git diff --check` in both changed owner repositories. [Source state](source-state.json) retains the full canonical baseline, tested root/owner HEAD revisions and SHA-256 values of the changed source/test/manifest files. The workflow's final step records the verified owner changes and Platform changes in separate local commits after commitlint validation; no push or release is performed.

## Signed source startup observations

The technical EventStore signed source smoke passed one test in 44.488 s, asserting real application/sidecar readiness, owned teardown and workspace removal: [test](builds-technical-diagnostic-live.txt), [command](builds-technical-diagnostic-command.json). This is a real CLI/source run, distinct from the direct-runtime Python fixture. Expected transient gateway health errors during initialization are retained in [filtered observations](technical-error-observations.json); the existing fresh-store writer-protocol activation completed without disabling health checks or changing ACL grants.

The final six-server retry also passed, in 119.514 s, with all six declared applications and sidecars healthy and complete owned teardown/workspace removal. It followed six successful sequential owner prebuilds and the passing technical smoke. Its filtered exception/build-error observations are empty. No startup implementation was changed between the failed attempts and passing retries. Reliable cold startup is not established by these results.

[Final AppHost inventory](final-apphosts.json) is empty; [final Docker inventory](final-containers.txt) contains only the four pre-existing global Dapr containers. [Owner local commit identifiers](owner-commits.json) record the unchanged verified source bytes after owner commits. The repository-pinned [commitlint versions](commitlint-versions.json) validated each exact candidate before commit, and both owner commits were checked afterward: [Platform candidate](platform-commitlint.txt), [EventStore candidate](eventstore-commitlint.txt)/[commit](eventstore-post-commitlint.txt), [Memories candidate](memories-commitlint.txt)/[commit](memories-post-commitlint.txt). The root commit records their pointers separately.

Earlier failed attempts remain evidence, rather than being overwritten:

| Attempt | Result | Evidence |
| --- | --- | --- |
| Six-server signed run | HXR025, eventstore Waiting; 103.050 s | [test](builds-tenants-live.txt), [command](builds-live-command.json) |
| Six-server diagnostic retry | HXR025, tenants-api Finished; 130.881 s | [test](builds-diagnostic-live.txt), [command](builds-diagnostic-command.json) |
| Six-server retry after sequential owner prebuild | HXR025, eventstore Waiting; 102.757 s | [test](builds-prebuilt-live.txt), [command](builds-prebuilt-command.json), [six successful prebuilds](prebuild-commands.json) |
| Initial technical EventStore retry | HXR025, eventstore FailedToStart; 17.708 s | [test](builds-technical-live.txt), [command](builds-technical-command.json) |

During inspection, one six-server snapshot showed healthy Sentry/placement/scheduler and an initializing gateway; a later attempt showed an unhealthy Sentry health check while it had logged successful initialization. These observations do not establish a single underlying cause. Sequential prebuilds all passed, but prebuilding alone did not establish reliable startup. The passing technical retry does not qualify cold six-server startup.

An initial `aspire describe --format Json` snapshot included environment credentials and was inadvertently printed in tool output. The saved [resource snapshot](diagnostic-resources.json) was immediately rewritten with a strict allowlist containing only resource identities, state, dependencies, URLs and health reports. It contains no environment values or private credentials. Subsequent state inspection used table output, and log capture excludes credential-bearing lines. Local development credentials exposed in tool output should be rotated separately; this run did not rotate shared local trust material.
