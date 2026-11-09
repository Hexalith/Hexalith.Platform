# Platform test extension publication

This narrow workflow publishes only `Hexalith.Platform.TestExtension`. It does
not publish a host, deploy resources, adopt module manifests, create a source
release, or establish Story 3.2 acceptance.

Publication has two normally qualified stages to avoid a circular prerequisite.
First, adopt the released EventStore extension SDK and Builds consumer-view
correction through a refreshed exact-source L/M tuple, compile the package-mode
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
python3 -m unittest discover -s eng/test-extension-release -p test_package.py -v
```

These checks test release refusals and archive identity validation; they do not
claim a hosted publication or signed-package startup qualification.
