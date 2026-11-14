# Refreshing historical tuple sources

Current source gitlinks and package release provenance have separate identities.
`eng/current-tuple-source.json` records synchronized owner Git revisions and their
committed declaration hashes. Its package-provenance hash binds the selected
release metadata. CI separately checks every selected package against its actual
owner release tag, declaration Git blob and Platform declaration snapshot. A
self-authored hash does not establish publication.

Historical companions carry the current production, tests, workflows, helpers,
and current-source binding unchanged. Only the five frozen tuple metadata files
and three frozen owner gitlinks differ. The original 349/412 profiles, published
tool identities, provenance bodies and owner revisions remain authenticated.

After staging the final current source, obtain `git write-tree` and run:

```bash
python3 -B eng/prepare_tuple_sources.py binding --source <full-tree-sha> --output /tmp/current-tuple-source.json
```

Copy that JSON to `eng/current-tuple-source.json`, stage it, and create the current
source commit after the repository's commit-message validation. Then prepare:

```bash
python3 -B eng/prepare_tuple_sources.py trees --source <full-current-commit-sha> --output /tmp/tuple-companion-trees.json
```

The tool reads committed objects only and uses a temporary Git index. It creates
no commits, refs or remote writes and does not replace an existing output file.
The third `package-reader` tree preserves the qualified SDK4.2/Builds4.47,
Tenants705/Memories25 and PartiesEBE reference inputs. Record that companion SHA
in the optional bounded `packageReaderSourceRevision` field of
`eng/pre-extension-tuple-source.json`; current reader execution requires it
explicitly and never substitutes another source.

For each returned tree, validate the exact companion commit message with the
owning repository's pinned commitlint, then create a commit with the current
source as parent. Publish those immutable companion commits on new qualification
refs before recording their full SHAs in the two `*-tuple-source.json` descriptor
files. Commit and publish the descriptor change. Never update existing companion
refs or claim a prepared tree ran tests.

Every later production, test, workflow, helper, build, documentation or other
non-tuple change requires fresh companion trees and descriptor SHAs. The strict
source comparison deliberately refuses stale companions.


For a clean committed current source with all three published companion refs,
prepare the retained reader workspace with:

```bash
python3 -B eng/prepare_tuple_sources.py reader-workspace --source <full-current-commit-sha> --workspace /tmp/new-isolated-reader --output /tmp/new-reader-receipt.json
```

This clones the actual committed root and only its declared direct owner
repositories into a new directory using shared object stores, then detaches each
clone at its exact recorded revision. It does not update submodules, initialize
nested owners, create synthetic commits, or change any source checkout. Source
comparison permits only authenticated frozen tuple metadata/gitlinks and bounded
source descriptors; stale production or test code refuses.

`eng/run_tuple_ci.py --suite current42 --eventstore-sdk 4.2.0` and the `base42`
lane prepare this workspace automatically. They first build current production
with its actual current catalog. They build the collecting test closure using
that same current catalog and explicit frozen Tenants705/Memories25/Parties310
family selections, keeping patched build dependencies and vulnerability checks.
The real prepared reader workspace retains its exact historical catalog. Both
catalog hashes and the collecting package assets are recorded, and
`PlatformPackageReaderWorkspaceRoot` selects the authenticated historical inputs.
The nine additive artifact controls still authenticate actual707/26/311 public
archives and current Parties source; the independent production closure test
exports the real current producer catalog and proves genuine Server707 output.
No retained image identity is relabeled as current native acceptance.

## Current producer renewal on 2026-11-14

The current tuple selects the official signed Builds6.0.0 publication from
`1ebc5b84ff2c8afd21a87d80ef3d9c9048eaf5e5`. Its original successful release
run43591439615, release445269620 and artifact14113221849 bind the two published
CLI packages and an empty container inventory. The handoff, NuGet signatures,
archive and Tooling hashes are recorded in `eng/builds-tuple.json`. This supplies
publication identity; fresh 349/412/current qualification remains independently
required. Generation4/5 bindings and immutable companion refs retain their
historical meaning. Unknown future generations remain refused.

Upstream Platform `70a016a98c97c19656a4c279a93b88e255d41b07` selected EventStore
`3c3a8c70f83cc8e0ddde9f5d8eb0a1ff860eea6f`. The official commit API
`GET /repos/Hexalith/Hexalith.EventStore/commits/3c3a8c70f83cc8e0ddde9f5d8eb0a1ff860eea6f`
returned HTTP422, `No commit found for SHA: 3c3a8c70f83cc8e0ddde9f5d8eb0a1ff860eea6f`.
An authenticated `git fetch --no-recurse-submodules
https://github.com/Hexalith/Hexalith.EventStore.git
3c3a8c70f83cc8e0ddde9f5d8eb0a1ff860eea6f` into a private bare object store
exited128: `fatal: remote error: upload-pack: not our ref
3c3a8c70f83cc8e0ddde9f5d8eb0a1ff860eea6f`.

Only that unavailable gitlink is reconciled to Platform's previously explicit
public source `49084f66f8d6545cae442875355a7066d5f5e73d`; all other upstream
content and the Builds1ebc selection are retained. This metadata repair does not
attribute test results to the unavailable source or replace any nested working
checkout. The umbrella's newer EventStore source remains a separate selection.

The later upstream source `966df4205994e60b1d0eb31e3c717df719e8c254` explicitly
selects public EventStore `f712c08e5cfacd0e340fe7ec571385e72fc0177f`; that selection
and its declaration binding are preserved. It does not change the published
EventStore5.1.0 source identity, which remains49084f66.

Actual Builds6 qualification refused the earlier consumer-view assumption:
the signed1ebc catalog selects SDK5.1.0/OpenAPI11.0.0/3.11.0 for current Platform
projects. Only the exact6.0.0/source1ebc/catalogSHA25645b05f6755f337baccfaa805e5e4afb4a57e91b2f70226a357c82d03b4c19bb7
binding admits this current view. Independent MSBuild evaluations separately
require all33 named current consumers and three unknown controls, then verify
the explicit SDK4.2 collecting view retains OpenAPI10.0.15/2.13.0 for only the
named consumers. Producer5 and historical4 views retain their previous rules.
The historical file-AppHost check uses the authenticated current producer binding
exported alongside its catalog; its model build keeps the frozen catalog.
These view receipts describe the actual selected SDKs and grant no runtime or
native acceptance. Full fresh349/412/current qualification remains required.

The current build in all three explicit SDK4.2 suites (current42/base42/extensions)
declares its existing complete public family:
SDK4.2.0, Tenants7.0.7, Memories3.0.27, Parties3.1.1 and Mcp2.0.1. The published Builds6.0.0
defaults are SDK5.1/Memories4.0.0; selecting only SDK4.2 against Memories4
produced the actual NU1109 downgrade through
`Hexalith.Memories.EventStoreExtensions4.0.0` requiring DomainService5.0.0.
The collector records these explicit properties and verifies its restored public
package identities before building. Missing, source, mixed and neighboring
package inputs refuse. Frozen349/412 and705/25/310 reader inputs are unchanged.

The isolated production-model test replays the admitted SDK4.2/T707/M27/P311/Mcp201
cohort and measures its assets and original signed Server707 hash. Its output
identifies that replay explicitly and grants no default SDK5.1/native acceptance.
Independent catalog observations retain the actual SDK5.1 and Memories4 defaults;
no signed catalog bytes or production admission rules are changed by this test
and CI repair.

Historical root builds retain the catalog objects while applying the current
source's four bounded security dependency patches: Aspire.Hosting13.6.4→13.6.5,
SignalR.Redis10.0.14→10.0.15, and its Options/DI.Abstractions10.0.15 dependency
closure. The overlay changes only those exact old entries and keeps package
auditing enabled. CI evaluates every frozen catalog to verify that owner
versions, unrelated packages and newer patch versions remain unchanged.

The file AppHost is current source shared by every companion. Its SDK and direct
integration directives are checked against the current main tuple's authenticated
Builds catalog. Historical CI exports that exact catalog Git blob into evidence
without changing its frozen model catalog or checkout. Every direct vendor pin
must match exactly; this does not introduce a version exception.

Historical validation transports use the original signed AppHost and Tooling
bodies. When their Aspire13.6.4 dependency cannot load the patched model, the
fixture creates a private validation-only copy and replaces exactly
Aspire.Hosting.dll with the independently signed and measured13.6.5 body. It
verifies every original file against the signed archive, records both closure
hashes and checks the private copy before and after execution. The original
archive, dependency documents and checkout stay unchanged; this check grants no
image or native runtime acceptance.

Published Builds6.0.1 closes the finite Platform default view at source
`c91beec60ce5eaa8cd62bf055d07fa6ca3890e89`, catalog blob
`e17e81e703da4cd1805644134e62ec1122cafb47` and SHA256
`ef691a7a3caa7a92e524f766a770574cd21ef5e32fccd996d1d2899b9d7bddd7`.
Only its exact authenticated publication binding admits the automatic SDK4.2.0
and Memories3.0.27 view for the same33 named Platform consumers. All five
Memories package pins are verified independently. Unknown and general consumers
retain SDK5.1.0/Memories4.0.0; an explicit SDK5.1 selection also retains
Memories4.0.0. The SDK4.2 collector still records and verifies its complete
measured public cohort. The earlier NU1109 control retains the unmodified6.0.0
catalog. Both exact6 profiles and the historical4/5 rules remain distinct;
these catalog and collection receipts grant no candidate or runtime acceptance.

The complete 6.0.1 current collector measured 27 passes and one refusal: the
private Parties producer selected Memories.Client.Rest/Contracts 4.0.0 under
SDK 4.2.0, while the admitted Platform snapshot required 3.0.27. Its exact public
package guard refused those actual assets. Published Builds6.0.2 corrects the producer view at source
`60ce7967607dc8b703fff11f4fc13cabb09c090a`, catalog blob
`86e9b0d6fd49551d80047d92f3aaed93b91ca5a3` and SHA256
`1291e32272127e2409e337fb6a974bc4c5f76a2907da0ba0d5528cc5016fe44e`
selects Memories3.0.27 for the existing finite Parties, Platform and packaged
Builds host roles only when SDK4.2 is selected and no Memories override exists.
Unknown/general SDK5.1 consumers and explicit selections retain their previous
behavior. Exact 6.0.0/6.0.1 catalog fixtures and failure evidence remain unchanged.
Only the genuine6.0.2 publication binding authenticates this exact source and
catalog for selection; catalog component observations grant no candidate or
runtime acceptance. The private owner package guard and fresh qualification requirements
remain in force.


The production generation6 consumer-context guard is additionally limited to
exact6.0.2, range `[6.0.2, 7.0.0-0)` and signed source60ce. The newer tests-only
Builds checkout12475 cannot substitute for that publication source. Existing4/5
selection semantics remain unchanged. Broad/exclusive/metadata/neighbor/future
ranges and missing/foreign/newer source identities refuse independently.
The current default assertion retains producer5's SDK5.0 and binds SDK5.1 only
to the exact generation6 producer; no catalog tamper refusal is removed.

In addition to the full five-Memories-pin checks and upstream collecting-graph
controls, current-source qualification observes all six actual Parties source
project names and an unknown sibling. Their complete SDK/OpenAPI/Tenants/
Memories/Parties/Mcp family must match the selected role. Unknown contexts keep
the generic SDK5.1/Memories4 view. These observations grant no runtime authority.
The earlier private explicit3.0.27 input experiment against c91 failed HXW012
because its actual Parties project view required4.0.0; that experiment was
removed without changing the snapshot or asset/body guards.

## Synchronized owner source renewal on 2026-11-14

Upstream Platform50fb selected the newer Builds12475 tests-only source while its
authenticated producer remained the signed6.0.2/source60ce publication. The strict
producer check refused that mismatch before dependency initialization. Restore
only the governed Builds gitlink to60ce; the umbrella's latest Builds12475 source
remains a separate selection. The publication, archive, Tooling and catalog1291
bindings are unchanged.

The synchronized EventStore749d and Tenantsd89f gitlinks retain their exact public
Git manifest identities in the renewed current-source binding. Their manifest
hashes and all package-release provenance remain unchanged. All upstream audience
feature code, contribution evidence and six other synchronized gitlinks are
preserved. Fresh immutable349/412/package-reader companions must contain this
same source, including the audience feature, with only their authenticated frozen
metadata and owner selections substituted. Prepared source alignment grants no
execution credit; fresh hosted tuple and catalog gates remain required.


The current 6.0.3 renewal binds official source `99f7c86072ca7b35e3fbbaee4606a559a847805a`,
raw LF catalog SHA256 `01219d064fc1c375879312f543aa649c6aaef344be054cf4c80571825ca59b41` and
blob `1968f12adb9151062915ab32cb76c5b9dbbc2a36`. Its genuine publication metadata is
recorded in `eng/builds-tuple.json`; the Builds gitlink, exact local tool pin and
supported range floor select that publication atomically. Exact 6.0.2/source60ce
admission remains explicit. Neither binding admits crossed source/range pairs
or neighboring 6.0.4. Current owner observations retain Tenants7.0.7 for each
named SDK4.2 Parties context and independently require generic Tenants8.0.0.
The current SDK4.2 collector and all frozen public provenance remain unchanged.
Fresh source binding and three new immutable companions are required for this source;
prior immutable refs remain historical evidence only.
