# Platform test extension publication

This narrow workflow publishes only `Hexalith.Platform.TestExtension`. It does
not publish a host, deploy resources, adopt module manifests, create a source
release, or establish Story 3.2 acceptance.

Publication has two normally qualified stages to avoid a circular prerequisite.
First, adopt the released EventStore extension SDK and Builds consumer-view
correction through a refreshed exact-source L/N/M tuple, compile the package-mode
extension tests, and let the existing source-release workflow tag that exact
current main. The fixture stays outside finite package-host admission at this
stage. Dispatch `test-extension-release.yml` from that same current main with
its existing stable Platform source-release version. The workflow rechecks the
normal exact-source tuple and catalog jobs, the source tag, and the executing
workflow identity before packaging and again before OIDC/publication.

The fixture restores from NuGet only. Its extension feature constant must be
enabled by the measured catalog pin. The archive must contain its extension
identity, exactly its DomainService dependency, the actual built library and
the exact Platform repository/source metadata. Actual restored EventStore SDK
archives must pass NuGet signature verification. The publisher consumes only
this run's immutable artifact and compares its full package identity to the
qualified receipt. NuGet's signed public archive must retain those package
bodies. A failed late check stops publication; an already published version is
never silently skipped or replaced.

Second, measure that signed public fixture archive and the owner SDK releases,
then add the finite cohort/declaration/host/seed/image/publication bindings and
negative controls in a subsequent refreshed tuple. Its actual host must load
the packaged extension and report `platform-test@1`; overall distributed
readiness remains a separate result. An unsigned candidate fixture and a
source-only fixture test cannot substitute for this second stage.

The GitHub production environment and the two repository variables below are
configured and read back in this continuation; normal review is required, only
main is permitted, and administrator bypass is disabled. Before the normal
production job can publish, the maintainer must configure
NuGet Trusted Publishing for account `jpiquot`, repository
`Hexalith/Hexalith.Platform`, workflow `test-extension-release.yml`, environment
`production`, and package `Hexalith.Platform.TestExtension`, with the already configured
`NUGET_TRUSTED_PUBLISHING_USER` and
`HEXALITH_TEST_EXTENSION_PUBLISH_ENABLED=true` repository variables. The
account-side NuGet policy requires an authenticated NuGet account session. No credential is read or copied
from another repository, and no organization secret grant is widened here.
The workflow fails closed while those prerequisites are absent.

Local resource-free checks:

```bash
node --test eng/test-extension-release/gate.test.mjs
python3 -B eng/test-extension-release/run-controls.py
```

These checks test release refusals and archive identity validation; they do not
claim a hosted publication or signed-package startup qualification.

The fixture archive also contains the exact Platform-owned v2 declaration for
`platformtestextension` and its producer descriptor. This pure extension module
has no servers and depends only on EventStore/Tenants/Memories. The verifier reads the
approved declaration and producer directly from the immutable release Git
object, compares every archived byte, and records their hashes. It refuses a
foreign producer, missing or changed declaration, a different extension/API,
source project dependencies, another SDK version, or a different SDK release
source. These packaged inputs prepare a concrete declaration authority for
subsequent finite host admission; they grant no current composition authority.

The publication controls now run automatically in tuple CI and again before
fixture publication: 14 Node gate cases and 30 Python readiness, archive/producer and execution-inventory cases.

The fixture declaration has one `mtp` package test,
`eventstore-extension-package`, implemented in the separate
`Hexalith.Platform.TestExtension.Qualification.Tests` project. It runs one bounded
readiness observation after the normal parent supplies the owned endpoint and
selected profile. It accepts an unhealthy overall `/ready` result only when the
actual `eventstore-extensions` check is healthy and reports the declared extension
id, major and package. It does not assert distributed readiness or establish
package-load provenance by itself. The native case does not forward the parent's
bearer token.

The producer document binds the exact native project, test source and Python
observer bodies carried in the archive. The fixture keeps its original
`platformtestextension` module identity and explicitly declares EventStore,
Tenants and Memories, preserving its actual domain closure. It remains a test
fixture with `serviceEnrollment: false`; this source preparation grants no
ordinary service adoption, owner repository mapping, private parent plan or
finite host admission. Those require a normally released SDK path that proves
the signed producer identity and the expanded reader/purpose cohort.

Both tuple preflight and this publisher require exactly 30 resource-free Python
controls (17 readiness, 9 archive identity, 4 execution-inventory controls) plus the existing 14 Node refusal
controls. A missing or extra Python control fails the inventory before tests run. Execution
also requires all 30 controls to run without skips. Readiness redirects are refused,
and an observation completed after its native deadline cannot report Pass.
A successful local observer or native report from an unsigned historical
candidate grants no signed fixture, parent-plan or cohort qualification credit.
