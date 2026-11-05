# Exact-run startup diagnosis

Run: `33e0140c7255820e281417198c19097a`, unmodified published Builds CLI4.44.3, released Platform1.4.1/source4afa0421, diagnostic isolated Parties candidate78238d5e. This reproduction is not publication acceptance.

## Actual blocker

The admitted Parties3.0.0 image is incompatible with the current catalog activation protocol. Its pinned image is `registry.hexalith.com/parties@sha256:25e4a03cfcc932f01fd9e66be647305c572a4c7d7e0a680db710cb9579bf783b`.

- Exact-run container `508e4644719d` started at13:41:51.618UTC and exited0 at13:41:55.691UTC, noOOM.
- Its ordinary entrypoint is `dotnet /app/Hexalith.Parties.dll`, with no extra command arguments.
- Public `.deps.json` read from that exact container identifies EventStore.Client, Contracts, DomainService and ServiceDefaults **3.130.2**. Its Contracts DLL SHA-256 `8b01258ef4928a570b4afaae451dbf2cec799407fe89195eb506bec3d3e12895` independently matches the public3.130.2 archive body. Parties tagv3.0.0 `Directory.Packages.props` explicitly selects3.130.2.
- In-memory closed classifiers found the fixed Development trust-refusal category, `RouteCatalogUnavailableException`, BackgroundService failure/StopHost and graceful shutdown. Stack symbols locate `DevelopmentRouteCatalogTrustValidator.ValidateAsync` through `RouteCatalogAuthority.PrepareAsync`, `DevelopmentRouteCatalogBootstrap.ActivateAsync`/`RetryBackendAsync` and `DevelopmentRouteCatalogBootstrapWorker.ExecuteAsync`.
- Both process environment Development checks are true; the catalog profile is Development and activation owner is the expected Development-fixture. The actual activation contains `TechnicalHttpReceivers` (one technical receiver), which requires activation bindingv2.

Published SDK3.130.2 `RouteCatalogActivationBinding.ComputeDigest` computes only bindingversion1 and omits technical HTTP receiver membership. The later SDK computes bindingversion2 when that membership is present. The trust validator explicitly compares the supplied TrustProof with the locally computed binding digest. These source/body observations explain the trust refusal and BackgroundService-driven exit0; infrastructure readiness cannot fix it.

The private coordination directory contains observations for all six ordinary hosts, including Parties. It contains ready receipts for the other five, with no Parties ready receipt. That is consistent with rejection during Parties Prepare, before Ready.

## Infrastructure and final status

The original readiness snapshot is stale at13:41:38. Direct exact-run read-only probes later returned Sentry/placement/scheduler `/healthz` HTTP200 and graph/vector RedisPING=PONG. Their containers remainedrunning. Initial Starting states are not an ongoing infrastructure failure.

Final allowlisted startup failure metadata: `hexalith.g4-startup-failure.v1`, rule`HXR025`, resource/appId`eventstore`, status`state=Running;health=Unhealthy`. EventStore unhealthy is the downstream composition symptom while the Parties participant cannot complete activation.

## Owning correction and limits

`references/Hexalith.Platform/src/Hexalith.Platform.Composition/PlatformPartiesPackageCatalogQualification.cs` still admits the old Parties3.0.0 image in the current full-owner cohort. Upgrading the private reader to SDK4.1.0 does not upgrade SDK assemblies inside that image. Requalify/adopt an actually produced compatible Parties image and measured digest, with the corresponding public-owner artifact cohort and exact version gates. Preserve catalog trust comparison, technical receiver membership and startup deadlines. Do not label the old image compatible based only on reader/Contracts tests or change the activation to omit required technical authority.

Publishing a new Parties artifact may itself depend on this package-run gate: account for that dependency explicitly using an already authorized, genuine candidate qualification/publication path. Do not invent future archive/image hashes, bypass automated checks, or claim a public release prerequisite already exists.

Safe receipts: `runtime-33e0140c-infra-metadata.json` and `runtime-33e0140c-parties-classification.json` alongside this file. Only bounded metadata, public assembly hashes/dependency identities, booleans, closed error categories, exception types/method symbols and numeric status codes were retained. No raw application/resource logs, environment values, keys, headers or payloads were retained. No process/container/repository mutations or stops were performed; parent owns teardown.
