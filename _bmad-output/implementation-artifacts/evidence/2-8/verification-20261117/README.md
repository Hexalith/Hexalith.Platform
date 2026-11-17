# Story 2.8 continuation — 17 November 2026

Classification: reconciliation of original delivered sources plus a focused,
baseline-only cancellation observation. Candidate acceptance, tool acceptance,
full reference qualification and a new candidate/tool policy acceptance decision
are all false. Eventual Story 2.8 completion is a separate review decision.
Story 2.10 obligations remain separate. No root gitlink, main branch, publication
or tag is changed by this continuation.

## Original delivered source

[Source reconciliation](source-reconciliation.json) binds the final delivered
Platform source `32643bde39b4da0e4538f8388e225727f82122f6` / 2.0.0 to tuple
[43391842440](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43391842440),
catalog [43391842426](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43391842426)
and publisher [43396965519](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43396965519).
Their original exact-source outcomes are success. Current42/base42/extensions/
real-legacy/pre-extension counts are 478/97/9/349/412; all six declared SDK
catalog jobs passed. The original 19-member publication manifest is retained at
[its owning location](../../2-10/source-release-20261113/publication/file-manifest.json).

The named PA-15/PB-9/PC-15/G14 package-binding case actually executed once and
passed in the [primary original XML](original-tuple/required-5.xml), with its
eight-case group passing without errors, skips or not-run cases. The **actual
collector** was `b05616cc57c16b152a364d5a4d0382782b99332f`, .NET 10 and tool 4.47.0;
the canonical workflow source was 32643. The full test file and all five relevant
production C# files are byte-identical between those sources. This is original
collector evidence; it is not a claim that this case ran on .NET 11 or tool 5.
The [range receipt](original-tuple/range-receipt.json) preserves the targeted
ZIP-member retrieval, CRC and member hashes. It does not claim to recompute the
598 MB archive's whole digest.

Story 2.13 subsequently and explicitly retired that old-image positive test;
its owning [coverage map](../../2-13/coverage-map.md) and source retirement remain
authoritative. No deleted historical test is silently treated as current.
[Delivered byte attribution](delivered-byte-attribution.json) distinguishes the
unchanged 2.8 implementation from later source-mode project propagation changes.
Current root `1a5666edd500ecf921d4068462c24d3d48663441`'s separate tuple failure remains failed in
[its receipt](current-source-failure.json); it creates no recurring latest-main
prerequisite for the original delivered 2.8 source. The incidental four-field
owner-metadata proposal was restored to its exact pre-task bytes.

Builds merge CI
[43019233527](https://github.com/Hexalith/Hexalith.Builds/actions/runs/43019233527)
remains **cancelled** at `213aab34be1ce97783a51ebeaf3d2d0c5dab6dd7`.
The later genuine source `3c2f5944aaa28dc7854a00a8a9df171aa810b0c7` contains both
`f8dba23982fb1599a37c543b41439dce52c0dd19` and
`b74b467ac5ce5d9f9fc440f43e279b7093b539de`, and its own
[43237014144](https://github.com/Hexalith/Hexalith.Builds/actions/runs/43237014144)
passed. Later delivered and published descendants are separately identified;
they do not turn the cancelled merge run into success or replay delivery.

## Focused patched-release input changes

- [44051836412](https://github.com/Hexalith/Hexalith.Parties/actions/runs/44051836412),
  exact frozen caller `5f94d7827f5b1d315856bb37b64f700e3ac56910`:
  failed Build before allocation because generated
  net10 consumer projects could not consume net11 Parties packages. Qualification
  skipped; zero cancellation requests. Exact existing owner correction
  `4aea34e048a4c6f2617e4c4f4972d2fbf184164c`
  supplied the dynamic consumer TFM selection and its controls.
- [44053412339](https://github.com/Hexalith/Hexalith.Parties/actions/runs/44053412339),
  caller `6a9208daca7ff93d116159e5b71d9c2e94254bb9`:
  Build succeeded, then seven isolated cases failed. Its original
  [primary failure output](isolated-primary-failures.json) is retained. Qualification
  skipped; zero cancellation requests. The failures were fixture stubs, stale
  docs, a real OpenAPI ABI incompatibility in the frozen 5.1 selection and a
  Debug child graph exit. The empty Debug diagnostic is not relabelled a timeout.
- [44062720071](https://github.com/Hexalith/Hexalith.Parties/actions/runs/44062720071),
  caller **68606e51aad58f4c220d516723eba792ed664a50**: the single finite coherent
  selection based on existing Parties `4eca9e8fbad451a602a9ec7d2d6384345b6b0e4e`,
  already-published Builds **5.2.2 / `02ed624aa19781cac75d2c9112940e9bd830441a`**
  and Platform **2.1.0 / `b76e0c7c70162653bbff6a8263ebfc4500c1631b`**. Publication receipts are in
  [publication-522](publication-522/). The ordinary 4eca build's actual HXW012
  failure is preserved there; it was not assumed green. This run then failed one
  exact workflow-reference assertion: the manual lifecycle test still required
  9ebc although the authorized focused caller now selected 02ed. Its
  [primary failure](run-3/run-3-primary-failures.json) remains failed; qualification
  skipped and zero cancellations were requested.
- [44066243616](https://github.com/Hexalith/Hexalith.Parties/actions/runs/44066243616),
  caller **83916016e0fb64b5fbde79f05c2cf317d0900f99**: only the exact manual-workflow
  assertion changes from 68606e51. The [one-line delta](workflow-contract-correction/incremental.diff)
  retains the ordinary CI 9ebc reference and every other lane/identity assertion.
  The complete owning workflow class passed 71/71 with zero skips/failures; its
  [receipt](workflow-contract-correction/caller.json) binds the primary XML hash.
  The hosted run subsequently failed one Memories compilation-policy child query
  with exit 1 and empty streams; its [primary failure](run-4/run-4-primary-failures.json)
  remains failed. Hosted worker PIDs were not retained, so its exact process
  attribution remains limited. Qualification skipped; zero cancellations.
- [44073347507](https://github.com/Hexalith/Hexalith.Parties/actions/runs/44073347507),
  caller **bccab615f9e3d1a92e3bbf648ee89e78a925670b**: the validated central
  bounded-worker fix supplies two child environment flags, retaining Python
  carrier mapping semantics and every cleanup budget. All 15 isolated projects
  passed, with [6,626 passes and 15 contextual not-executed cases](run-5-isolated-counts.json).
  Runner-owned integration then failed `HXP020` before native execution or hold
  issuance because the actual declaration was ineligible for the ordinary
  Platform package profile. Its [primary observation](run-5/run-5-integration-observation.json)
  preserves the startup failure and verifies both exact-owner cleanup attempts
  with empty process/container/workspace/resource/claim inventories and no
  discovery failures. Qualification skipped; zero cancellation requests. The
  [task-owned ref](run-5-ref-disposition.json) was verified at its exact source
  and deleted successfully.

The first coherent caller adds only the existing five-path private Admin UI boundary fix
from **53844114ffcedc5af96fbf1cb15e126adaae02a9**, the previously controlled observer
and hooks, and two path-specific CRLF attributes. The exact
[projection](proof-caller/projection.diff), [caller identity](proof-caller/caller.json)
and prerequisite-only [Git bundle](proof-caller/caller.bundle) are retained.
All 15 isolated projects and all 20 native suite/configuration file bytes remain
as declared. Startup 600 s, suite execution 300 s, stop 90 s, original hold 240 s,
ready wait 120 s, exact-finalize 150 s and workflow 60/90 minute budgets remain
unchanged. The fourth caller preserves every native suite/helper/observer byte
and every budget from the third. Its canonical frozen input digest is
`991197b95ed0a1172dbc3b3fd82f1948e2d2c6712c3e79a034416fb5f160c2b1`;
the preceding `06171bcf592d356e14a957f01dacb0af689db51191c3be6ee6a9e529811f50c0`
input and failed result remain immutable.

The named Parties/Platform catalog view deliberately retains **SDK 4.2.0,
ASP.NET OpenAPI 10.0.15 and Microsoft.OpenApi 2.13.0**. The ordinary external
native owner has **SDK 5.0.0, OpenAPI 11.0.0 and 3.11.0**. The private real native
UI artifact is independently compiled and qualified at exact SDK 4.2.0; its
complete source/import closure, official package archives, outer context and
actual consumed DLL are bound. The ordinary HXW012 guard remains effective.

## Executed local prerequisite controls

All disposable verification used `run-with-temp`, with repository copies and
build outputs beneath its private TMPDIR. There was no local real AppHost,
DCP/Dapr/Docker, real listener or service probe. The OpenAPI cases use the read
WebApplicationFactory/TestServer factory and in-memory substitutions.

| Primary control | Result |
| --- | --- |
| Full consumer Python module | 8 passed |
| Full runtime receipt Python module | 44 passed |
| Actual native profile tests | 5 passed |
| Actual Commons fixtures | 39 passed |
| Documentation fitness cases | 2 passed |
| Actual Debug/Release graph and refusal cases | 15 passed |
| In-memory OpenAPI/documentation cases | 9 passed |
| Complete workflow contract class after exact-ref correction | 71 passed |
| Genuine prepared private native UI host build | zero warnings/errors |
| Wrong named SDK and original native boundary | HXW012 refusal in both |
| Pinned owning commitlint and actionlint | passed |
| Bounded helper/supervisor controls after worker isolation | 32 passed |
| Full Memories compilation-policy class after worker isolation | 24 passed |
| Full Commons fixture class after worker isolation | 39 passed |
| Separate older holds-only controller after executor-order correction | 97 passed |
| Actual released 4.48.2 integration helper | 75 passed |
| Controlled observer cases | 3 passed |
| Older subject's complete owning workflow class | 70 passed |

[Primary result audit](local-controls/primary-results-audit.json) gives each TRX
hash and outcome. The initial native-profile invocation guessed minimum seven
although its source contains one Fact and four theory rows: all five passed,
but the harness returned policy code 9. That first result remains in
[results.json](local-controls/results.json). A corrected five-count invocation
had already started when the parent instructed a mechanical inventory audit;
it also passed, and no further repetition occurred. No case or assertion was
removed, weakened or skipped. The first CRLF-only diff-check refusal is a
mechanical harness issue; the final staged check passes with the two existing
owner attributes.

The [bounded worker mechanism](msbuild-ownership-probe/results.json) was reproduced
through the actual supervisor/process boundary: two default clients shared one
real SDK worker, and the first supervisor's cleanup killed it while the second
client still needed it. Isolated workers survived that peer cleanup. The valid
two-row direct/Python regression failed before the fix and passed after it;
initial invalid harness prototypes remain separately retained. This local
mechanism proof does not invent the unrecorded hosted PID attribution. The
[clean two-path owner proposal](owner-ready-fix/owned-msbuild-workers.patch) is
rebased onto `8abfd266eecb5a636ee5498f944304669c18aa41`, preserving its incoming
regressions. Its [receipt](owner-ready-fix/proposal.json) records apply-check
success and pending owner review/normal source delivery; current-owner test
qualification is not claimed from an apply check.

## Remaining hosted gate and producer decision

All five dispatched attempts are terminal failures with zero cancellation
requests. The latest exact-owner cleanup is verified; the released-tool
lifecycle cancellation and three-timestamp ordering gate remain open. The ready
artifact must bind the exact source, run, attempt, job, owner and active original
hold/owner-native execution, with trace setup code 0 and a fresh live observer.
Only then is one normal cancellation authorized. Final credit requires actual
durable admission-close and withdrawal before the first composed stop, kernel
signal-origin evidence, exact finalizer success and empty inventories. Empty
inventories alone do not prove ordering. The original 2.8 holds-only scope does
not require the later attached native-suite execution.

The one finite older eligible subject passed real
[metadata-only ordinary handoff/cohort guards](ordinary-runtime-guards-3/guard-result.json)
with the released 4.48.2 tool. Its prepared
[nine-path projection](ordinary-proof-4/projection.diff) retains every actual
native suite/declaration byte and its original 60/45-minute budgets. The separate
executor uses authenticated Aspire 13.6.5 and Dapr CLI/runtime 1.19.1 while the
caller catalog stays unchanged. [Prepared inputs](prepared-holds-only-inputs.json)
are explicitly uncommitted and undispatched.

The actual frozen test-project build is now blocked by `NU1903` for SignalR
Redis 10.0.14. The concrete one-pin patch to 10.0.15 correctly reaches `HXW009`
because its matching Platform catalog identity has not been delivered. The
[producer-boundary proposal](producer-boundary.md) records exact primary
failures, the reviewed patch, maintenance/publication consequences and the
recommended bounded owner decision. No audit suppression, new publication,
main merge, accepted pin change, new candidate search or further dispatch is
queued. The documentation case remains unexecuted behind this actual build
failure.

[Final state](final-continuation-state.json) records the unrelated incoming root
commit `4f88df0ec98885425db5ddb20a3a3bb34a93d046`, all 18 preserved gitlinks and
zero active task jobs or monitors. Earlier checks are not relabelled as executed
at that incoming head. The [finite task/row audit](finite-task-and-row-audit.json)
keeps all original six rows and their original result identities; no new
candidate/tool or Story 2.10 acceptance is inferred.

The [parent independent read audit](parent-read-audit.json) authenticates the
historical manifests, original source/collector attribution, full new projection
and native five-case XML. Its finalHostedProofVerified value remains false;
it is not final hosted acceptance and does not request recursive audits.
