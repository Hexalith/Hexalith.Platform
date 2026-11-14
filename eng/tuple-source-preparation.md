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
