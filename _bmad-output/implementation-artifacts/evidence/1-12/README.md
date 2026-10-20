# Story 1.12 local implementation evidence

**Integrated upstream Parties revision: `35d891d202a87fd4f494f2cadd26c277df44b84a`.**
This includes the compatibility audit and all accepted review patches. The
isolated clone revisions and live debugger/edit/rerun records below are older
historical proof inputs; they are not labeled as the current primary candidate.


The independent Parties debugger/edit/clean-source proof passed with released
module tool **4.34.0**, accepted catalog `5d03f1db9c38db34ecac7fe6048937478ffb82cc`,
.NET SDK 10.0.402, Aspire CLI 13.6.1 and isolated Dapr home
`/home/administrator/.hexalith/dapr/1.18.2`.

**Integration complete:** the user approved the owner changes and authorized
pushing Parties before Platform. Both remote `main` heads were verified.
`upstream-integration.json` records approval, the preserved upstream commit,
the unchanged approved patch and the integrated Platform pin. Local owner integration and live Platform-workspace smoke are complete.
The smoke used clean Platform proof revision `89a20036053fb4dfbcc7478b884a8d953898de5c`
with Parties `fedbb9ecf5388bd08543be5350f72edb15794646`, reached all seven servers
healthy in 287.04 seconds, and exact-run cleanup removed its state/workspace.
See `platform-smoke-*.json` and `platform-smoke-debug.txt`/`platform-smoke-down.txt`.
Local owner revision `10de88c7ecd441b6c616096d74629aa46293819c` integrates those pins,
command fixes and mapping expectations on the externally merged owner branch;
its runtime/manifests/build files are identical to the tested adoption base.
During local implementation/proof, no remote push/fetch, remote submodule update
or nested initialization was performed. The subsequent authorized integration
fetched/pushed only Parties and Platform, without recursive submodule operations. Other sessions advanced owner repositories;
those external commits and working-tree/index changes were preserved.

## Canonical revisions and local handoff

| Input | Full revision |
| --- | --- |
| Original Platform baseline | `3d51fed1f3d99acf36ef690f150136593298bd95` |
| Platform support P1 | `17179e59843ee8cfb35892a74a2c18d3136fc9cf` |
| Committed Parties adoption base (external session) | `a2f546341bc7f455d330352c5537eed9f8d43b86` |
| Committed Tenants permission (external session) | `9a94131a26d0c5d80d5540706e267ee123d54b68` |
| Parties local pin-only commit | `e935bb59ff4e942735d1333f97c9b434611ff9c2` |
| Parties clean live-proof revision; teardown docs corrected | `7ec755a664ff1910d377cc144e0b6a482b188472` |
| Historical isolated Parties; test-only mapping expectation follow-up | `fedbb9ecf5388bd08543be5350f72edb15794646` |

The clean isolated clone is `/tmp/hexalith-story-1-12/Hexalith.Parties`.
All three local follow-ups retain P1 and the exact Tenants permission revision.
That historical test-only commit changes no runtime source, manifest, tool or build
file relative to the clean live-proof revision. `parties-local-followups.patch.gz`
contains the three commits; `parties-*-commitlint.txt` records their validation
with the owning lockfile's commitlint 21.2.2. `handoff-owner-state.json` captures
owner heads, working trees and any unmerged index entries at handoff; this agent
did not resolve external docs/mTLS merges or commit their index.

`canonical-clean-revisions.json` records every direct checkout revision and
uninitialized nested references. `environment.json` records exact tools.

## Live proof

| Run | Source | Result |
| --- | --- | --- |
| `d9eb8946ff63acfdbce097571bfc7280` | Preliminary source overlay | Seven servers ready in 244.97s; attached source breakpoint and original header. Superseded: its staged Platform identity was old `bb81fc9...`, despite a P1 checkout. |
| `8c07fd47fb0f4b2f97d41d45668f2655` | Local Parties `e935bb59...` plus `probe-edit.patch` | Seven servers ready in 203.81s; actual service child attached; `MvpComplianceWarningMiddleware.cs:9` hit; `/ready` returned 200 and the existing warning header contained `[platform-1-12-edit]`. |
| `dfb47581c5a8ef6f62b9c0283e574319` | Clean Parties `7ec755a6...` | Seven servers ready in 172.39s; `/ready` returned 200 and the original warning header. Probe restored byte-for-byte; working tree clean. |

Every run used the ordinary 600-second deadline. Canonical mappings select P1
for both identity and checkout and the accepted catalog; the only identity
finding is **HXW011**, P1 lacking a local release tag. This source warning was
recorded, not suppressed. The preliminary HXW008/HXW009 mismatches are confined
to the superseded run. The initial missing-Dapr-home and misnamed-clone refusals
are retained separately.

The seven servers were exactly `parties`, `eventstore`, `tenants`, `tenants-api`,
`memories`, `memories-access-telemetry-clock`, `memories-access-telemetry`.
Rich Parties search remained disabled; UI/MCP/sample hosts were not enrolled.

- `canonical-debugger-attach.txt`: loaded checkout assemblies, attached child PID,
  bound source breakpoint and backtrace, HTTP response and safe detach.
- `edit-response.txt`, `clean-response.txt`, `probe-edit.patch`,
  `clean-source-hashes.json`: reversible edit and restored source/assembly/PDB identities.
- `canonical-*-ready-state.json`, `canonical-*-debug.txt`,
  `canonical-*-source-mapping.json`: run identity, duration, readiness and mapping.
- `canonical-*-project-selection.json`: actual MSBuild source/Debug project
  selection and ordinary Release/package selection. A distracting sibling
  EventStore project was present and ignored.
- `canonical-*-down.txt`, `cleanup.json`: exact-run teardown, removed workspaces,
  no remaining owned containers, restored source and uninitialized nested refs.
  Docker snapshots/events also show concurrent Works fixtures being removed by
  other activity after this story's exact down; the four global Dapr containers
  and other unrelated persistent containers remained. No broad deletion was used.

## Verification and limits

- Owner declaration generation/check and malformed/unsafe/drift tests passed.
- Released 4.34.0 jointly validated Parties/EventStore/Tenants/Memories.
- Reference ACL matrix passed using `tools/run_reference_checks.py`: it selects
  the released validator instead of the blocked source build and copies declared
  owner test-entry files into the existing temporary fixture owner roots.
- New Parties composition tests: **3 passed**. Exact existing receiver-grant
  cases: **5 passed**, including only Parties `GET /ready` on Tenants.
- Security owner project: clean Release build and **249 passed**.
- Affected Commons HTTP CI routing class: **34 passed** in the independent clone.
- CI full project: initially 142/148; the six absent-fixture cases then **all
  passed** after their five consumer restores and UI Release build with audit
  enabled. See `ci-security-fixture-setup.txt` and `ci-security-fixture-rerun.txt`.
- Main owner project: clean Release build; **2004/2005 passed** in the independent
  snapshot. The sole failure is the unchanged historical receipt assertion
  expecting `v4.33.1-49-g5d03f1db` from `git describe`, where the same accepted
  commit now carries `v4.34.0`. `historical-receipt-failure.txt` and
  `receipt-test-diff.patch` prove that failing method/constant are unchanged
  from pre-adoption. Historical validation was not weakened.
- A concurrently added sibling-auto-discovery test contradicted tool-only
  mapping. Its two cases now pass with the corrected expectation; this is the
  historical isolated test-only follow-up and `owner-mapping-expectation.patch`.

The initial broad Platform composition Release build was blocked by unrelated
Builds adoption source/analyzer errors and existing Works/Identity EventStore
references (`composition-build.txt`). The focused compile uses the accepted
packaged Tooling assembly and existing reference outputs; its external targets
exclude only the concurrent `DomainEnrollmentKitTests.cs` that requires unreleased
adoption APIs. This focused lane does not claim the broad gate passed.

Commands, fixture setup and exact exceptions are in `commands.md` and the logs.
`dotnet test` encountered the repository's MTP handshake failure; projects were
built individually and their xUnit assemblies invoked directly.

## Evidence storage

Text files preserve concise command results. Large build/test outputs and the
canonical debugger transcript are curated with original SHA-256 identifiers;
full local `.log` files remain in this directory but are ignored by Git.
The complete canonical debugger transcript and preliminary/follow-up patches
are retained as gzip archives. `owner-mapping-expectation.patch` is a semantic
diff that omits an accidental line-ending conversion; the integrated owner test
preserves its original CRLF bytes. No runtime source changed during integration.

## Coordinator audit

The normal Debug composition build now passes with zero warnings/errors and no
source/test exclusion. The broad default run has 210 cases, 11 failures and seven
opt-in skips. The EventStore release snapshot and lookup code plus implicated
test bodies are unchanged from this story's baseline: stale 3.120.0 provenance
conflicts with catalog 3.126.0, and copied owner fixtures omit executable entries.
The gateway seed's one broad-run content-root failure passes when its four cases
run alone. See `coordinator-broad-failure-assessment.json` and the exact test log.
These broad failures were not hidden or changed to pass.

Current focused results: three adoption cases, five exact receiver cases, four
seed subscription cases, one onboarding case and all 250 security cases pass.
After restoring compatible constructor overloads, the six security binding cases
also pass through the DI container with default/custom component names. Existing
CLR constructor signatures are covered explicitly. The audit follow-up is
`ae00820347b87483cc90538012c27464375ad9a1`; its only runtime edits restore those old signatures, preserving
the configured primary-constructor path and all record formats/keys.


## Final review and verification

All three review layers completed. Each finding and its verdict is recorded in
the spec's Review Triage Log; all accepted patches are applied. The owner review
follow-up keeps ACL checks active under Python optimization, validates all four
declarations in CI, and verifies configured stores and readiness through the
production registrations/host. Exact subscription structure and portable cache
discovery are covered too.

Coordinator reruns passed: 250 security cases, 15 production-registration/health
cases, three adoption cases, five renderer cases, three cache-discovery cases,
the existing reference ACL matrix, and released-tool joint validation. No covering
case was skipped. `review-check-results.json` records exact commands and exits;
`review-*.txt` preserves curated results and full-output hashes. The normal owner
and composition builds passed with zero warnings/errors; copied build summaries
retain their full-output hashes. The four pre-existing broad-test problems remain
explicitly deferred in the shared ledger; neither broad test gate is claimed green.

The independent live proof uses the older revisions listed above. Subsequent
runtime changes only restore the original constructor signatures; the review
follow-up changes tests, rendering, CI and documentation, and leaves the live
manifest and host configuration unchanged. The later explicit user approval authorized owner acceptance and remote
integration. Both are now complete and the implementation spec is marked done;
sprint tracking remains at review as required by the build workflow.

## Approved upstream integration

Parties rebased its four approved commits onto upstream projection improvement
`32fa72e2cb223d7f8554d3a5439fa6e15212ec26`. The approved diff remained byte-identical. Published Parties
`35d891d202a87fd4f494f2cadd26c277df44b84a` retains P1 and the reviewed Tenants pin. Platform adoption
`e9a142dc5c56cdb3c187f5eec0d3b3a7178a0058` pins that upstream Parties commit; its unpublished predecessor
was amended so published Platform history does not introduce an unavailable
Parties pin. Post-rebase builds, 15 host/configuration cases, 14 upstream
projection cases, five renderer cases, three adoption cases and joint declaration
validation all pass. Exact commands/results are in `integration-check-results.json`.
The prior live proof is retained at its recorded source revisions.
