# Signed Platform extension fixture and component startup

Platform source `0aacf13244fa6b1d9e08d627149d72cf12229167` uses signed Builds 6.5.1 and EventStore 5.4.0. The exact hosted tuple passed all 380 current, 67 base and 9 extension component cases without skips or failures. Independently verified artifact `14706791008` matches the retained GitHub digest and all three test receipts. The catalog and source-build gates were green. [Hosted tuple](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44898998036) · [Catalog](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44898997995) · [Tuple receipt](platform651-hosted-tuple-verification.json)

The normal source publisher tagged exact main as v2.2.5. The dedicated fixture publisher used qualified artifact `14706727068` and the ordinary `production` package-publishing environment review. The scoped `test-extension-release.yml` NuGet exchange and publication succeeded. The public signed archive equals publication artifact `14706703046`, matches NuGet's registered SHA-512, passes `dotnet nuget verify --all` and retains every nonsignature member from qualification. The nuspec, declaration, producer and native payload agree with the immutable source. [Platform v2.2.5](https://github.com/Hexalith/Hexalith.Platform/releases/tag/v2.2.5) · [Fixture publisher](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44902658431) · [NuGet fixture](https://www.nuget.org/packages/Hexalith.Platform.TestExtension/2.2.5) · [Signed publication receipt](fixture225-signed-publication.json)

The exact Platform host and generated binding writer built in a disposable workspace using the verified published Builds tool. Its assets and dependency manifest both selected the fixture and seven locked EventStore 5.4.0 libraries as packages. The host process loaded `Hexalith.Platform.TestExtension.dll` with SHA-256 `650d37d4aa1ae6de6c4633c1ecafa763349ed8c29f1c7e24d257ec0853d3f004`, matching the signed package. A real pinned Dapr 1.19.2 sidecar with a private in-memory state component let the unmodified Program start. The extension was Healthy and reported `platform-test@1`; overall `/ready` returned HTTP 503. The no-sidecar startup refusal and first observer timeout are retained. The host and sidecar exited before workspace cleanup. [Component startup receipt](fixture225-signed-host-component.json)

Full Story 3.2 acceptance still requires the normally released Builds named-consumer fixture pin and producer proof, authenticated finite Platform reader and host admission, an owned Healthy distributed environment exercising the native MTP test, and its mandatory CI inventory. The original baseline and frozen spec remain intact. **Status remains in-progress.**

The isolated finite receiver uses the measured fixture cohort. Its package reader
authenticated the released Tenants and Memories owner assemblies and generated 30
routes and 19 policies. It recorded both facet digests. Six released Linux/amd64
workload manifests and image configuration digests were independently verified
through the existing local read-only registry client. [Reader measurement](fixture225-reader-identities.json) · [Image identities](fixture225-image-identities.json)

The candidate host observer also passed against the real signed fixture. The
extension CI profile preserves its nine SDK cases, adds nine fixture admission
cases and requires a component startup with the public pinned Dapr sidecar. This
component uses public signed packages and requires no private workload pulls.
The ordinary owned native startup uses the authenticated released images. The
current organization registry grants exclude Platform, so the hosted component
preserves that access boundary while the full distributed qualification remains
a separate local owned run after the signed Builds adoption. [Builds qualification PR](https://github.com/Hexalith/Hexalith.Builds/pull/198)

The owning Builds PR passed its exact hosted workflow. Its normal merge
`d532b1e9b1f595e2c9867bfa9780dccd78c9a2d9` retains the reviewed tree, including the
catalog audit. Exact-main CI and the owner release started from that commit.
The Platform candidate passed 18 extension executions, 33 tuple controls and
three source topology controls in disposable local jobs. [Builds PR CI](https://github.com/Hexalith/Hexalith.Builds/actions/runs/44907019166) · [Builds main CI](https://github.com/Hexalith/Hexalith.Builds/actions/runs/44908984505) · [Builds release](https://github.com/Hexalith/Hexalith.Builds/actions/runs/44909014255)

A second component probe restored those same signed packages and started the
same pinned Dapr sidecar, with the protection test installation omitted. The
unmodified released host refused before listening with
`DependencyNotAvailable:EXT-PROTECTION-1:ExplicitProtectionInstallationRequired`.
The ordinary Builds child uses a fixed environment allowlist, and the Platform
model supplies no authenticated protection installation. The component test
installation exercised startup validation for the isolated HTTP proof. The
owned distributed MTP run still requires a supported authenticated disposable
installation. [Prerequisite refusal](fixture225-protection-prerequisite.json)
