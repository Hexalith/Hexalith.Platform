# Preparing candidate tuple sources

`eng/current-tuple-source.json` records exact owner gitlinks and canonical
committed declaration SHA-256 identities separately from released package
provenance. CI verifies each selected package against its owner release tag,
declaration Git blob and Platform snapshot. Duplicate fields and crossed source,
owner, provenance or declaration identities refuse.

After staging an intentional owner-source or package-provenance change, obtain
`git write-tree` and generate the current binding from that exact Git tree:

```bash
(
  preparation_dir=$(mktemp -d) || exit 1
  trap 'rm -rf "$preparation_dir"' EXIT
  python3 -B eng/prepare_tuple_sources.py binding --source <full-tree-sha> --output "$preparation_dir/current-tuple-source.json" &&
    cp "$preparation_dir/current-tuple-source.json" eng/current-tuple-source.json
)
```

Review and stage the resulting binding with the candidate. Preparation reads
Git objects, never owner worktree bytes, and creates no commits or refs. A
regular documentation change requires no binding refresh. A gitlink change needs
only its matching current binding; no companion commit, branch, descriptor or
reader checkout is required.

The workflow resolves that candidate with a shallow checkout, authenticates its
initialized direct owners and release identities, then runs current42, base42
and extensions on the same candidate and current NET11 collector. Explicit
The `--suite` argument selects the test inventory. All suites use the release selected by the authenticated Builds catalog. Exact positive group
counts, real runtime and output identities, package-only restore and zero skips
remain enforced. The aggregate verdict requires source resolution, host evidence
and current tuple execution to succeed. The source publisher requires all four jobs.

For local graph verification, use a new evidence directory:

```bash
python3 -B eng/run_tuple_ci.py --audit-only --suite current42 --evidence <new-evidence-directory>
```

This audit authenticates archive/catalog/output inputs but grants no hosted,
image, runtime or candidate acceptance. Actual hosted success on main remains
required after landing. Genuine old package archives and declaration documents
may be fixture data; no synthetic local history grants publication provenance.
The current private reader is exercised separately from the unchanged refusal of
unqualified current image cohorts. Archived qualification and broker tags remain
unchanged. Local `qualification/*` branches may be removed after recording targets.

## Adopting a Builds release

Builds is pinned by released version. Read the current pin from
`eng/builds-tuple.json` (`{schemaVersion, version, releaseTag}`),
`.config/dotnet-tools.json`, the `hexalith-platform.json` range `[pin, next major)`,
the generated `eng/builds-tool.lock.json` and the `references/Hexalith.Builds` gitlink.
To adopt a release:

1. Set the version and `releaseTag` in `eng/builds-tuple.json`, the tool version in
   `.config/dotnet-tools.json` and the range floor in `hexalith-platform.json`.
2. Move the Builds gitlink to the commit the release tag names.
3. Run `python3 eng/builds_tool_lock.py`. It downloads the package, runs
   `dotnet nuget verify --all`, compares its SHA-512 `contentHash` with the
   nuget.org `packageHash` and only then rewrites the lock.
4. Commit the bump (pins, gitlink and tool lock) before running the local checks.
   They read the committed gitlink from `HEAD`, and CI checks the committed tree.
5. Then run `python3 eng/owner_packages_lock.py` and commit the regenerated owner
   package lock. A new catalog can select other owner versions, and the generator
   reads the Builds and Parties gitlinks from `HEAD` (see below).

The range admits later versions of the pinned major, prereleases included, and
refuses earlier versions, the next major and its prereleases.

The diffs are the pins, the gitlink and the two locks. `resolve-sources` refuses a tag
whose commit differs from the gitlink. Preparation also refuses changed tool
bytes, a missing or stale lock entry, a release whose `hexalith-release:` marker
differs from the gitlink, and an executable `--version` other than
`<version>+<gitlink>`. The catalog is read from the gitlink blob. Composition
admits the validation AppHost/Tooling pair only when both report the same
`<version>+<commit>` inside the range. CI records each archive member's SHA-256
as the AR-12 expected identity and compares it with the built Tooling and
EventStore host outputs. Release, run and artifact IDs are evidence only. A new
major generation or a behavior-changing release may still need code.

## Adopting an owner release

Composition admits the owner packages (EventStore, Tenants, Memories, Parties and
McpCli) through the generated `eng/owner-packages.lock.json`. Each entry is
`{id, version, contentHash}`, where `contentHash` is the base64 SHA-512 of the exact
`.nupkg` bytes and equals the nuget.org `packageHash`. No owner hash, revision or
release version is compiled into `src/`. An owner release reaches Platform through
the Builds catalog, so adopting one is a version bump plus a regenerated lock:

1. Adopt the Builds release whose catalog selects the owner version, as above, and
   commit its pins, gitlink and tool lock (or commit the Parties gitlink move).
2. Run `python3 eng/owner_packages_lock.py`. It reads the Builds and Parties gitlinks
   from `HEAD`, so run it only after that commit. It restores the owner references
   of Platform's package-mode surfaces (Composition, the routing catalog reader, the
   EventStore host and the `platform-package` Parties Release build) against the
   catalog read from the Builds gitlink, under each surface's own project name. The
   Parties references are read from its project closure at the recorded Parties
   gitlink, so a Parties repin also regenerates the lock. Running it before committing
   a moved gitlink refuses or locks the previous catalog. For every owner package in
   that restored closure it runs `dotnet nuget verify --all` and compares the archive
   hash with the nuget.org `packageHash`. It rewrites the lock only when every
   package passes.
3. Commit the regenerated lock as the next commit. CI runs
   `python3 eng/owner_packages_lock.py --check`, which regenerates the lock in memory
   and refuses a committed lock that differs.

Composition then applies one rule before any run effect. Every declared owner's
contract and adapter packages must be locked, and every locked package of that
owner's family must name exactly the catalog-selected version. A missing entry or
another version refuses, naming the package and version. Composition checks each
owner assembly it loads, and the routing catalog reader checks its whole owner
closure. Each check maps the assembly to its package through `deps.json` or
`project.assets.json`, verifies the SHA-512 of the `.nupkg` in the package folder
actually used, and compares the SHA-256 of the archive's `lib/<tfm>/<name>.dll`
member with the loaded DLL. A different archive or DLL refuses, naming the package
or assembly. The reader receipt records the lock entry, the expected member
SHA-256 and the loaded SHA-256 of every owner body (AR-12). Run-built Parties bodies
of the `platform-package` profile keep their run-bound authority and are not locked.
The compile-time SDK constants in the project files are version-range rules, so a
later release needs no project edit.

## Historical preparation observations

The records below retain the identities and findings of their original subjects.
References to replay/reader companions and 349/412 execution are past obligations,
retired by Story 2.13. They do not prescribe current candidate preparation.

## Historical producer renewal on 2026-11-14

The current tuple selects the official signed Builds6.0.0 publication from
`1ebc5b84ff2c8afd21a87d80ef3d9c9048eaf5e5`. Its original successful release
run43591439615, release445269620 and artifact14113221849 bind the two published
CLI packages and an empty container inventory. The handoff, NuGet signatures,
archive and Tooling hashes are recorded in `eng/builds-tuple.json`. This supplies
publication identity; that preparation still required349/412 and then-current
qualification. Generation4/5 bindings and archived companion refs retain their
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
native acceptance. That preparation required fresh349/412 and then-current qualification.

The current build in all three explicit SDK4.2 suites (current42/base42/extensions)
declares its existing complete public family:
SDK4.2.0, Tenants7.0.7, Memories3.0.27, Parties3.1.1 and Mcp2.0.1. The published Builds6.0.0
defaults are SDK5.1/Memories4.0.0; selecting only SDK4.2 against Memories4
produced the actual NU1109 downgrade through
`Hexalith.Memories.EventStoreExtensions4.0.0` requiring DomainService5.0.0.
The collector records these explicit properties and verifies its restored public
package identities before building. Missing, source, mixed and neighboring
package inputs refuse. Those frozen349/412 and705/25/310 reader inputs belonged to that historical preparation.

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

That file AppHost source was shared by the companions. Its SDK and direct
integration directives were checked against that main tuple's authenticated
Builds catalog. Historical CI exported the exact Git blob without changing the
frozen model catalog or checkout; direct vendor pins had to match exactly.

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

## Historical synchronized owner source renewal on 2026-11-14

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
preserved. That preparation required immutable349/412/package-reader companions containing
the same source and audience feature, with only authenticated frozen metadata and
owner selections substituted. Prepared source alignment grants no
execution credit; fresh hosted tuple and catalog gates remain required.


The 6.0.3 renewal bound official source `99f7c86072ca7b35e3fbbaee4606a559a847805a`,
raw LF catalog SHA256 `01219d064fc1c375879312f543aa649c6aaef344be054cf4c80571825ca59b41` and
blob `1968f12adb9151062915ab32cb76c5b9dbbc2a36`. Its genuine publication metadata was
then recorded in the schema 1 `eng/builds-tuple.json`; the Builds gitlink, exact local tool pin and
supported range floor selected that publication atomically. Exact 6.0.2/source60ce
admission remains explicit. Neither binding admits crossed source/range pairs
or neighboring 6.0.4. Current owner observations retain Tenants7.0.7 for each
named SDK4.2 Parties context and independently require generic Tenants8.0.0.
The current SDK4.2 collector and all frozen public provenance remain unchanged.
That historical source required a fresh binding and three new immutable companions;
prior immutable refs remain historical evidence only.

## Historical source and package context renewal on 2026-11-15

The normal Builds6.0.5 publication binds source
`e2bbbd0893f586e4d447ef8358ff747ea3d34435`, catalog blob
`42441eb4cefcff2f9c582fcda630d9ad15887df2` and raw LF SHA256
`d098dab50977041a536508f6df7e934f95678a4a0abedd80b28e9bae19ee0637`.
Its exact-main CI43717004492 and release43719900160 passed. Both actual public
NuGet archives pass repository-signature verification and preserve every
authenticated original payload body; the executable names that exact version
and source. The complete measured handoff was then recorded in the schema 1 `eng/builds-tuple.json`.

This exact publication preserves the finite SDK4.2 package view and its
Tenants7.0.7/Memories3.0.27/Parties3.1.1/Mcp2.0.1 family. The existing33 named
Platform and six Parties contexts retain those package inputs; unknown contexts
keep SDK5.1/Tenants8/Memories4/Mcp3, including unknown contexts explicitly choosing
SDK4.2. Independent observations require the actual MCP package pin as well as
its property. Current source contexts select the owning SDK5.1/OpenAPI11 view;
verified model common props establish mapping-derived mode and owner origins
before the first pinned-catalog import. Package-purpose snapshots retain their
existing semantics.

Only this exact6.0.5 source/catalog/range is added. Earlier exact6 profiles and
all4/5 rules remain unchanged; the unreleased reviewed/audit commits,6.0.4,
neighbors and crossed sources do not acquire authority. MCP3 remains outside
the qualified SDK4.2 runtime cohorts. At that preparation stage, identical-code349/412/reader companions and complete
then-current qualification were required before landing;
component measurements grant no signed-fixture or native acceptance.
