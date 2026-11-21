# Story 3.2 fixture publisher checkpoint

The isolated worktree `../platform-story-3-2-20261121` starts at Platform
`25887836c2c7a63edbff7a4e0426e078a188b98c`. That current tuple selects
Builds 6.5.0 at `e1380b813b230022db0b8656ac3a0480e57c07e8` and EventStore
5.4.0 published from `2ec21f631a2e292fba8c5537fe665ab275ed1081`. The
previous 6.4.4 tuple failed at the missing Builds release declaration
script and the catalog verifier independently refused the EventStore pin.
[Builds release](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v6.5.0),
[EventStore release](https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v5.4.0),
[previous tuple](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44889570994),
[previous catalog](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44889571061).

The fixture publisher had a second prerequisite failure. Its verifier hardcoded
EventStore 4.2.0 and that release's source. It now reads the EventStore release
from the exact Platform commit's validated tuple provenance and checks the
fixture's dependency declaration against that version. For every restored
EventStore package it verifies the committed owner lock, NuGet signature,
package source, and extracted compile/runtime bytes. DomainService must have
an actual selected compile assembly. The verifier inspects the same archive
bytes used for the signed check, with a private copy for the signature command.

The local Release native test project compiled with zero warnings and errors.
The real pack used package references, a fresh NuGet cache and the current
catalog. The four restored EventStore SDK archives (Client, Contracts,
DomainService and ServiceDefaults) passed the verifier. The resulting
`0.0.0-story32.local.20261121` fixture is an unsigned local candidate. The SDK
signature receipt and candidate identity are retained without archives or
binaries. [Pack and build log](local-candidate-pack.log),
[signed SDK identities](signed-sdk-identity.json),
[local candidate identity](local-candidate-package-identity.json).

The verification used `run-with-temp`, a fresh `NUGET_PACKAGES` beneath
`$TMPDIR`, and the current root-declared Builds checkout. It evaluated the
fixture's `DefineConstants` with `dotnet msbuild -p:Configuration=Release
-p:UseHexalithProjectReferences=false -getProperty:DefineConstants`, then used
`dotnet build` for the native project and `dotnet pack` for the fixture with
the publication workflow's Release, package-mode, repository/source and
warnings-as-errors options. The local job used `--artifacts-path` beneath
`$TMPDIR` and passed the actual outputs and assets to
`python3 -B eng/test-extension-release/verify-package.py`. All child
commands exited before cleanup.

All 36 Python publisher controls and 14 Node release-gate controls passed
without failures or skips. The additional Python cases require the proper
dependency and restore version, locked bytes, source, signature, compiler
assembly, and verified file bodies. [Publisher control log](publisher-controls.log).

A separate disposable copy of the exact 25887836 source and all its root
gitlinks used the published Builds tool and the selectors from
`eng/tuple-ci-extension-suites.json`. All nine Release package-mode
component cases passed on .NET 11 with no failures, skips, or unexecuted
cases. The baseline composition build reported ten existing `CS0618`
deprecated Aspire overload warnings and zero errors. This component
selection includes extension readiness without distributed resources.

The exact-source catalog job passed for 25887836. At the checkpoint, the tuple
job was running its package-only Release verification. The source callback
finished its verification job and skipped publication while that tuple was
pending. The latest observed stable Platform release was v2.2.4 on
`a74abe1794db0bc79d6ca3ba45722ca02d05951c`.
[Current catalog](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44893084594),
[current tuple](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44893084604),
[source callback](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44893480900).

The official fixture feed returned HTTP 404. The last real publisher remains
attempt 1 on the historical source, where OIDC returned HTTP 401 because the
policy expected `release.yml`. The NuGet Trusted Publishing account page
redirected to sign-in. The presence of the dedicated `test-extension-release.yml`
policy could not be verified through the available session. The old attempt's
source differs from current main. Signed fixture publication, ownership of the
actual fixture pin, finite producer and host admission, and signed package host
and native startup remain pending. The story remains in progress.
[Historical publisher](https://github.com/Hexalith/Hexalith.Platform/actions/runs/42716926527),
[NuGet account controls](https://www.nuget.org/account/TrustedPublishing).

## Current tuple preflight correction

Main 25887836 subsequently failed its tuple in `file-host-pin-controls`
before xUnit. The failed test output reports that the file AppHost declares
Dapr integration `17.0.0-beta.1078` while the bound Builds 6.5.0 catalog
selects `17.0.0-preview.1.261120-0454`. The apphost directive now uses
that catalog pin. With the pin aligned, existing controls revealed two further
refusals that failed to fire on the unified catalog: an invalid producer binding
and a mismatched catalog digest. These checks now apply when the caller supplies
authentication metadata, independently of whether the catalog has a historical
named view.

The unchanged `python3 -B eng/test_verify_platform_pins.py` passes all 18
controls under `run-with-temp`. The direct verifier also accepts the real
producer binding and the SHA-256 of the catalog read from its exact Builds
gitlink. The two fixes are a prerequisite for the ordinary source-release
gate. [Failed tuple](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44893084604).
