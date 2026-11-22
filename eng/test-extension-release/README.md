# Platform test extension publication

This narrow workflow publishes only `Hexalith.Platform.TestExtension`. It does
not publish a host, deploy resources, adopt module manifests, create a source
release, or establish Story 3.2 acceptance.

Publication has two normally qualified stages to avoid a circular prerequisite.
First, qualify the current tuple with the released EventStore extension SDK,
the Builds consumer view and the package-mode extension tests. The existing
source-release workflow tags that exact current main. The fixture stays outside
finite package-host admission at this stage. Dispatch `test-extension-release.yml`
from that same current main with its existing stable Platform source-release
version. The workflow rechecks the normal exact-source tuple and catalog jobs,
the source tag, and the executing workflow identity before packaging and again
before OIDC and publication.

The fixture restores from NuGet only. Its extension feature constant must be
enabled by the measured catalog pin. The archive must contain its extension
identity, exactly its DomainService dependency, the actual built library and
the exact Platform repository/source metadata. The actual EventStore SDK archives
must match that source revision's owner package lock and EventStore release
provenance and pass NuGet signature verification. The restored compile and
runtime files must match the signed archives. The publisher consumes only
this run's immutable artifact and compares its full package identity to the
qualified receipt. It admits the fixture DLL and XML documentation, declared
build and native-test files, NuGet metadata and the standard signature entry.
NuGet may report the same immutable version already exists. The authenticated
retry continues to the mandatory public download, repository signature
verification and comparison of every nonsignature member with the qualified
archive. Different payload bytes fail the retry. Retry with a full workflow
re-run or a new dispatch, which qualifies and packs again. Re-running only the
failed `publish` job fails, because it downloads the artifact named for its own
attempt.

Second, measure that signed public fixture archive and the owner SDK releases,
then add the finite cohort/declaration/host/seed/image/publication bindings and
negative controls in a subsequent refreshed tuple. Its actual host must load
the packaged extension and report `platform-test@1`; overall distributed
readiness remains a separate result. An unsigned candidate fixture and a
source-only fixture test cannot substitute for this second stage.

The production environment requires normal review, allows only `main` and
disables administrator bypass. The publisher uses
NuGet Trusted Publishing for account `jpiquot`, repository
`Hexalith/Hexalith.Platform`, workflow `test-extension-release.yml`, environment
`production`, and package `Hexalith.Platform.TestExtension`, with the already configured
`NUGET_TRUSTED_PUBLISHING_USER` and
`HEXALITH_TEST_EXTENSION_PUBLISH_ENABLED=true` repository variables. Updating
the account-side policy requires an authenticated NuGet account session. The
workflow refuses publication when any of these prerequisites is missing.

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
source. Runtime admission also requires the Builds-owned signed fixture pin and
the authenticated parent run for the finite cohort.

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
Tenants and Memories. The declaration sets `serviceEnrollment: false`. Signed
Builds 6.6.0 admits the exact fixture package 2.2.5 in the
`eventstore-extension-package` profile with EventStore 5.4.0, Tenants 8.0.2 and
Memories 4.1.0. The ordinary run authenticates that parent proof and checks the
exact released reader and workload identities.

Both tuple preflight and the publisher require 37 resource-free Python controls
(17 readiness, 16 archive and SDK identity, 4 execution inventory) and 14 Node
refusal controls. The Python runner checks the exact inventory and requires each
test to pass without skips. The readiness observer refuses redirects and late
responses cannot report a pass.
A successful local observer or native report from an unsigned historical
candidate grants no signed fixture, parent-plan or cohort qualification credit.

The extension tuple profile runs every required component case selected in
`eng/tuple-ci-extension-suites.json`, including the original SDK controls. It also builds the actual Platform host using the named
Builds catalog selection and starts it with a disposable Dapr state sidecar. The
component check compares the restored archive, selected assets and loaded DLL
against signed NuGet 2.2.5 and requires `platform-test@1` in the extension readiness
result. The check records the overall HTTP status. The ordinary owned MTP run
qualifies distributed readiness separately, using the authenticated released
Tenants and Memories images and the SDK-issued fixture proof.
