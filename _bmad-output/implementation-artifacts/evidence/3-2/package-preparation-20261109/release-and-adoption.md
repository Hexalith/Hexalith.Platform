# Story 3.2 package preparation

These local changes prepare package consumption. Story 3.2 remains **in-progress**. None of these receipts qualifies a released package cohort, publishes an SDK, moves a dependency pin, adopts a module manifest or authorizes a new package host.

The original baseline is `f7c68398cdcb494d535e789e3d9577af9e8d5d36`; the preparation starts at Platform `27a1638e0e7d66b672708507d95a44fe31050718`. The selected direct owners remain EventStore `cab8363c2b6e2c0588efc8ca9c2e09fab0006841` and Builds `27145e52ffab5623198ba5efdab5cdb71aa42d28`. See the separate published inspection receipt for official release/archive/API observations.

## Applied Platform correction

`PlatformEventStoreExtensionBindings.Write` now removes a matching central version item and includes the selected version. This preserves the version already obtained from Builds; it also works when an enrolled extension has no live central item. It does not create a version authority or use `VersionOverride`.

The two new test rows resolve a validated synthetic mapping-bound snapshot selecting `0.0.0-story32.snapshot`, while the live catalog selects `9.8.7`. They pack a disposable package and restore/compile a consumer against the generated props, both with an absent consumer central item and a stale `9.8.7` item. Assets must resolve the selected package version and package type. A changed snapshot body without its matching hash is still refused. These component fixtures do not establish pinned release provenance.

## Unapplied owning SDK correction

[builds-consumer-snapshot.patch](builds-consumer-snapshot.patch) targets the unchanged selected Builds source. It preserves the two-argument `CatalogPackageVersion` ABI and adds a consumer-name overload. The overload checks the same snapshot identity and reads the named `ProjectVersions` view. A requested view with no snapshot or no recorded consumer is refused; neither the live catalog nor the default snapshot can silently substitute for it. Existing default-view callers retain their behavior.

Five owner controls cover default and case-insensitive host selection, missing views, a missing snapshot and tampering. A sixth disposable control restores and compiles the real candidate fixture using the host view at a version different from the default view and uses the actual Platform reference writer. The selected Builds checkout remains unchanged. The patch must go through its owner review, complete tests and release before Platform calls the new overload. The actual host view must first be materialized from the pinned tree by Builds' catalog evaluation; a caller must not synthesize it during a real run.

## Unapplied Platform adoption proposal

[platform-forward-adoption.patch](platform-forward-adoption.patch) shows the exact intended edit sites:

- Enable the extension SDK in Composition, EventStoreHost, Composition.Tests and the fixture for one exact, measured future EventStore release, retaining that release's existing catalog/scopes capabilities.
- Add Composition's direct DomainService package dependency and consume the new Builds consumer-view overload for `Hexalith.Platform.EventStoreHost`.
- Admit the composed gateway only through one finite measured cohort, exact approved extension owner/package/id/version, the Platform-owned host path, its original seed identity and the existing signed-body predicate. Merely matching a host path cannot admit it.
- Add one automatic CI group with **7** package-capable cases: generated package binding (1), real snapshot-selected restore/build (2), unpinned refusal (1), Program registration order (1), HTTP extension readiness (1) and declaration drift (1). The source host-build and source-composition cases are excluded. The synthetic pack/restore cases use Release. Existing CI groups and the legacy count remain unchanged.

The `__MEASURED_*__` and `__APPROVED_*__` values are intentionally unresolved. The proposed cohort has no entry in `SelectTestCohort` or the signed artifact registry, so this proposal cannot confer admission. Replacing strings alone is insufficient: independently measure and review that cohort's release declarations, supported APIs, package signatures/body hashes, repository commit and publication identity, producer/publisher records and container digests, then add the corresponding finite registry/selection rows and negative controls. No future release version, archive hash or cohort authority is invented here.

The proposal is a reviewable forward diff, not an apply-ready release rollout. It passes `git apply --check` against this preparation but is deliberately not applied. SDK publication, the subsequent dependency pins, the complete tool/catalog/source tuple, its signed producer/publication binding, and a refreshed same-source L/M legacy pair still require approval and qualification. Run the new CI group in the actual Release/package lane and require all seven cases, without skips or zero-case success, before qualification. Recheck the count if tests change.

## Local candidate package proof

[candidate-package-probe.py](candidate-package-probe.py) packs seven accepted EventStore SDK projects and the Platform fixture at a unique `0.0.0-story32.local.20261109.<nonce>` version. It copies tracked Platform source inputs without bin/obj outputs, keeps the actual host Program byte-identical, and applies only explicit disposable package/feature overlays. Its local NuGet mapping names each candidate package exactly. It invokes the production binding writer, restores/builds the actual Platform host, checks resolved assets/deps versions and `package` types, rejects foreign project references, and compares the fixture DLL hash to its archive.

The probe starts that actual Program on a dynamic loopback Kestrel port and requests `/ready`. The **eventstore-extensions** result must be `Healthy` and report `platform-test: 1`; the loaded DLL must match the NuGet archive. HTTP 503 remains expected for overall readiness without a sidecar. The test uses the existing seed metadata fixture's pinned protection installation and removes only the startup index writer's hosted alias. It does not prove distributed readiness, real Dapr operations, signed release admission, or automatic CI qualification. Every subprocess has a bounded timeout and process-group cleanup; the factory disposes the host. No source extension output is copied beside the consumer.

Replay from the scratch Platform root:

```bash
python3 _bmad-output/implementation-artifacts/evidence/3-2/package-preparation-20261109/candidate-package-probe.py --root "$PWD"
python3 _bmad-output/implementation-artifacts/evidence/3-2/package-preparation-20261109/verify-owner-snapshot-patch.py --root "$PWD" --candidate-receipt /tmp/<candidate-work>/receipt.json
```

Archives and complete disposable build trees stay outside Git. Small receipts, build/HTTP logs, result XML and failed-attempt summaries are retained here. Source and package lane tests are separate; the prior broad gate remains red, and the Story 2.8 genuine Memories Contracts fixture failure retains its own attribution.


## Independent final verification

Official archive inspection finds the extension manifest and two-argument catalog API in Builds **4.45.0** and **4.45.1**, absent in **4.44.13**. Neither published version has the consumer-view overload. The latest EventStore **4.1.0** DomainService/Server archives lack the extension types; their repository source is `dbc6e7e08d605ba6c6c790e3e08a15dc8bbdec8a`. See [published-inspection.json](published-inspection.json) for official URLs, archive/body hashes and actual API inspection commands/results. The selected Builds 4.45.0 archive exactly matches the retained tuple's SHA-256 and source.

The accepted EventStore extension source `cab8363c2b6e2c0588efc8ca9c2e09fab0006841` currently has a failed [exact-source CI run](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/42519687843). Publication requires successful owner preflight from an approved current source containing this work. Do not dispatch a release while that prerequisite is unsatisfied. The observed runs are retained in [eventstore-release-source-observation.json](eventstore-release-source-observation.json).

After recording consumer views in the synthetic package fixtures, the parent rebuilt with zero warnings/errors and ran **47/47** source checks, including 35 extension cases, 7 gateway cases and 5 enrollment/tool-pin cases. The parent independently reran the candidate HTTP startup and configured owner probe (**6/6**). Candidate extension readiness is Healthy, while overall `/ready` returns **503** without a sidecar. This is never distributed readiness or published-cohort qualification.

[retained-comparison.json](retained-comparison.json) compares every fresh selected case with both retained inventories. All 45 previously focused source cases still pass; two restore/build cases are added. The Release/package selection has **77 passed, 5 failed**; all five failed names already fail in both retained Debug broad inventories. Mode differences prevent inferring a new broad total or claiming unrelated failures resolved. The modified-tree Story 2.8 attempt stops earlier at its unchanged-input guard; its prior genuine Memories Contracts failure stays separately attributed.

The forward adoption patch still needs measured release identities, complete finite cohort registry/selection data, successful package-CI execution and exact-source tuple qualification. Its seven selected methods pass in the source lane only. Existing source-only builds are excluded from that CI group. No publication, push, dependency pin/gitlink change or manifest adoption occurred; concurrent work in the main checkout was preserved.

## Requested scope approval

Approve extending the frozen boundary to land/release the EventStore extension API and the attached Builds consumer-view correction, then prepare the measured catalog/tool/SDK pin adoption through the existing exact-source tuple and legacy controls. Keep all owner preflight, signed-body, provenance, declaration, image and host admission checks. Publication proceeds only after those gates pass; new release versions and artifact identities must come from their actual producer outputs. Story 3.2 remains in-progress until released package startup, automatic CI qualification and its other outstanding acceptance requirements pass.
