# Story 2.10 fixed-input preparation — 2026-11-13

The Builds-release resume prerequisite is met: published
[Builds 5.1.0](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v5.1.0)
contains the Story 2.11 runtime producers. This resource-free continuation does
not establish a compatible final qualification subject or accept a tool.

The separate Platform gate remains open. The latest source release is
[v1.11.3](https://github.com/Hexalith/Hexalith.Platform/releases/tag/v1.11.3),
source `7e49ce25f79f9228214b0e9bf167cdeb4bf77ece`. It predates the required live
EventStore endpoint correction `3f7659e2ff660212bc4f31c6e118a14a65f55da5`.
The committed Parties caller `f532145038992217f6433cd3dc1e36e61a1acc38` also
selects a Platform source without that correction,
`38402bc77ac0be3e6902002d08923c51d0e73a77`.

[prerequisites.json](prerequisites.json) preserves the actual release metadata
and investigated failures. [Source publisher 43333610884](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43333610884)
correctly refused `unsuccessful-latest-run` and skipped publication. Its
[tuple run 43333012883](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43333012883)
failed before SDK setup: the committed Builds gitlink is
`565b9e63bce9d3b5b43483d01da81ef7ceb33a20`, while `eng/builds-tuple.json`
binds `9f5d751f7ab0b7a621dcd18b589bf061fabac6ec` / tool 5.0.0. The configured
tool is 5.0.1. Both historical lanes also correctly rejected production,
control/helper and unapproved metadata differences from their recorded same-code
source commits. Refreshing those exact committed source cohorts belongs to the
final publication cut. None of these guards was relaxed.

The new [preparation utility](../../../../../eng/lifecycle-qualification/prepare.py)
adds a closed fixed-input selection, a read-only source audit and verified
download reuse. Its cache key includes exact caller/Builds/Platform sources,
catalog digest, tool version/source/framework, toolchain versions and archive
digests. Each hit and restore revalidates consumed bytes and metadata, and each
restore makes new private copies. Poisoned entries, linked inputs, extra state,
nonfresh destinations and a competing destination creation fail without deleting
another owner's data. The utility accepts download archives only; it neither
extracts them nor restores test results, workspaces or credentials.

[inputs.json](inputs.json) records a preparation subject matching the current
Parties tool pin, 5.0.0, and its exact catalog/helper
`6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b`. It is not the final campaign input
set and confers no publication or qualification authority. The source audit
returns nonzero with exactly `platform-live-port-binding-unavailable`.

[cache-verification.json](cache-verification.json) records actual reuse of the
genuine NuGet 5.0.0 package. Its archive SHA-256 equals the existing official
publication binding, and `dotnet nuget verify --all` passed its NuGet repository
signature. Population, a cache hit without the original download directory and
two independent fresh restores passed. The separate operation timings cover
only preparation. No startup, cleanup, audit or performance savings are claimed.

[tests.json](tests.json) records 45/45 Platform lifecycle controls (33 original,
12 preparation) and 183/183 canonical Parties controls, all with zero failures,
errors or skips. Bound input hashes stayed unchanged through both suites.
The initial canonical attempt remains failed: 151 tests, 74 errors and one
failure while the required nested helper was unavailable. Under the spec's
existing explicit authorization, only Parties' exact recorded nested Builds
dependency was initialized from available local Git objects, with `--no-fetch`
and no recursion. No nested dependency pin changed.

The historical `local.py` remains unchanged and bound to 4.47.0; the preparation
utility does not supply a producer-aware controller for the new tool. The
compatible published Platform source and caller adoption, current local
controller, fresh full local/hosted table, real cancellation runs and final tool
acceptance/pins remain incomplete. All old outcomes retain their original
subjects. No lifecycle service, workflow dispatch, commit, push, publication,
accepted pin or deadline/budget change was performed in this continuation.
