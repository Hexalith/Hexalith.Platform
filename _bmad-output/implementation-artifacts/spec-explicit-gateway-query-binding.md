---
title: Bind declared projection queries to the actual gateway
status: done
created: 2026-10-31
baseline_commit: 60761c60b662e592e89fd0b84a3a1280ab2b7db4
---

# Explicit gateway query binding

The existing catalog executable binds every declaration to its owner HTTP app.
The actual SDK Sample owner declares GetCounterStatusQuery with the canonical
projection-actor/ProjectionActor transport; the SDK validator requires QueryAsync
and QueryRouter requires the gateway's actual local application identity. Keep
owner declarations, operation schemas, intent policies and public SDK builder
unchanged. Supply gatewayAppId from the existing selected EventStore server.
Only the exact canonical workload projection-query transport may use that binding;
refuse missing gateway identity and unsupported non-domain transports. Existing
domain-service bindings continue to use actual owner ReceiverMethods.

## File Scope

Allowed files for this story:

- `src/Hexalith.Platform.CatalogBuilder/Program.cs` - map the canonical actor query to explicit gateway identity
- `src/Hexalith.Platform.Composition/PlatformRoutingCatalog.cs` - pass the actual selected gateway application identity
- `_bmad-output/implementation-artifacts/spec-explicit-gateway-query-binding.md` - record bounded mapping proof
- `tests/Hexalith.Platform.Composition.Tests/PlatformEventStorePackageCatalogReaderTests.cs` - protect composition forwarding of the selected gateway AppId

## Verification

Use an isolated copy of this actual executable template, actual compiled native
Sample/Contracts/adapters and published SDK3.129.0. Generate schema3 with all seven
actual routes/operations; require the Counter query target to be
actual-gateway/QueryAsync and preserve the six sample HTTP targets. Remove the
explicit gateway input and require refusal before catalog/receipt output exists.
No nested submodules, producer checkout writes, policy copy or invented authority.

Actual isolated tool proof passes using the genuine current Sample/Contracts/
adapter source compiled against published SDK3.129.0: schema3 with seven routes
and seven operations, exactly one Counter QueryAsync target at eventstore-proof
and six unchanged sample-proof HTTP targets. Removing gatewayAppId refuses before
catalog.json or receipt.json exists. Build has zero warnings/errors. Logs:
`/tmp/parties-real-catalog-tool-build.log`,
`/tmp/parties-real-catalog-tool-generation.log`,
`/tmp/parties-real-catalog-tool-negative.log`. Workspace:
`/tmp/parties-real-catalog-tool-probe-zk9krrqh`. This is genuine candidate/tool
qualification, not activated runtime authority or a claim about unpublishedSDK1.

Three independent combined SDK/root/Platform review layers completed. Their only proposed Platform issue (missing live actor binding verification) is disproved by the existing unchanged parent PartiesTopologyIsolationTests: actual command submissions and two distinct persisted Counter query results execute through the generated catalog and real Gateway projection actor. That complete E2E remains mandatory after the new SDK publication. No surviving finding concerns these two Platform source changes; no runtime authority, eligibility, default profile or production trust policy changes here.

The actual7route/7operation catalog-builder positive and missing-gateway-before-output negative proofs above remain green. Exact full commit candidate passed this owner's pinned commitlint. Parent will bind the committed source and execute final actual persisted topology before its release; this spec does not claim pending live activation or unpublished packages.


## Review Triage Log

Review resumed 2026-11-09 against baseline `60761c60b662e592e89fd0b84a3a1280ab2b7db4` and current source revision `337f5b9a3d2d9b47b0e2b9c051f00d3de8c57361`. Original implementation: `5b47d4ce733a8de645bfa7df776d5cd0cf06c744`. The complete staged baseline diff includes later unrelated changes; reviewers used that original source diff for attribution. All three independent reviewers completed before triage. Edge-case hunter returned no findings.

| ID / layer | Finding | Verdict | Evidence and route |
| --- | --- | --- | --- |
| BH-1 / blind | Actor queries bypass missing or contradictory owner receiver methods. | false | The canonical actor transport executes the gateway's `QueryAsync`, not an owner HTTP method. The selected declaration must match query/workload/projection-operation/ProjectionActor, and SDK catalog validation runs before either output file. Ignoring an owner HTTP method for that transport produces the intended callable gateway target; no wrong-target outcome was demonstrated. Rejected. |
| BH-2 / blind | Duplicated predicate omits SDK QueryKind validation. | false | The SDK builder validates declarations before output and rejects a noncanonical projection query; the reviewer confirmed this guard. This private mapping cannot accept the invalid declaration described. Rejected. |
| BH-3 / blind | Unsupported-transport diagnostics omit the offending owner and route. | low | The refusal previously exposed only a generic message, preventing a developer from identifying the offending declaration in a mixed-owner inventory. Patch: include owner assembly, route ID, target kind, and actor type in the existing exception; no new branch or policy. |
| BH-4 / blind | Missing gateway internal input leaves a pending directory after compilation. | low | A direct malformed internal call can fail outside cleanup, but the production bootstrap checks gateway presence before Generate and graph resolution enforces resource identity. This is not encountered by the supported caller; adding lifecycle guards for invalid internal inputs exceeds a direct correction. Rejected under the low-finding complexity rule. |
| BH-5 / blind | Composition forwarding lacks a nondefault-gateway regression test. | medium | Executable controls write their own request and cannot catch composition replacing the selected AppId with a literal default. Patch with VG-1: execute the production generator with ResourceId `eventstore` and a distinct AppId, then assert the actual request and generated policy consumers. |
| BH-6 / blind | The mixed-owner probe does not assert every HTTP target against owner metadata. | low | The domain-service branch still indexes the actual owner's ReceiverMethods, and the executable harness compares complete route bindings with its baseline. A separate complete metadata-parity fixture would strengthen verification but no changed HTTP binding was demonstrated; it adds fixture logic beyond a direct correction. Rejected under the low-finding complexity rule. |
| BH-7 / blind | No dedicated malformed actor/target/credential/operation executable controls. | low | The unsupported-target branch and downstream SDK declaration validation refuse these declarations before output. The existing native fixture covers the accepted canonical branch; no unsupported accepted transport was demonstrated. Additional compiled negative-owner fixtures are an optional extension beyond a direct correction. Rejected under the low-finding complexity rule. |
| BH-8 / blind | Domain-host parity does not compare gateway actor-query targets. | low | CatalogActivationProbe already checks each declared actor query's actual AppId and QueryAsync target against the selected gateway. Extending the separate domain-host inventory parity routine duplicates that bounded check without demonstrating an unguarded binding outcome. Rejected under the low-finding complexity rule. |
| BH-9 / blind | No runtime query test exists for the private builder and installed actor. | false | PartiesDevelopmentCatalogTool copies and executes the actual Platform CatalogBuilder template, and the existing PartiesTopologyIsolationTests seeds two actual persisted Counter streams and queries both sessions through authenticated UI routing, including a surviving session after peer disposal. This unchanged parent runtime proof remains a release responsibility already stated in the intent; this resumption claims executable/tool qualification only. Rejected; no live acceptance claimed here. |
| BH-10 / blind | No successful predecessor-to-successor actor binding migration test. | low | Generic successor-generation coverage predates this mapping change and belongs to the existing versioned builder contract; the query binding introduces no command policy migration rule. Defer the optional positive reader-successor scenario without changing SDK or migration policy in this story. |
| VG-1 / verification-gap | Hard-coding composition's gateway ID would leave current tests green. | medium | Pre-verified regression gap: current executable tests bypass composition, and direct composition tests use the default AppId. Patch with BH-5 using a genuine private SDK 4.1.0/Tenants 7.0.5 package-test reader and a nondefault selected gateway. |

## Review Remediation

- Improve unsupported transport refusal with the actual owner, route, target kind and actor type.
- Strengthen the existing `AuthenticatedCurrentReaderRetainsTheSixExactBodiesAndCatalogAsync` package-test control with a distinct selected gateway AppId; check both the serialized request and the emitted idempotency consumers so replacing the selected identity with `eventstore` fails. Reuse the current SDK 4.1.0/Tenants 7.0.5 purpose and preserve the required CI test count.
- Preserve owner declarations, operations, SDK builder APIs, canonical actor selection, and domain-service receiver mappings.


## Verification Refresh — 2026-11-09

The historical temporary logs named above are no longer present. Their original claims are superseded by the following fresh executions; component results do not grant live activation or release acceptance.

| Execution | Result |
| --- | --- |
| Original committed Program, published SDK 3.129.0, unmodified native Sample/Contracts/adapters | PASS: schema 3, exactly seven routes and operations, `counter-status-query` at `eventstore-proof/QueryAsync`, six `sample-proof` HTTP targets, and missing gateway refusal before either output exists. All three builds: zero warnings/errors. |
| Current patched executable, SDK 4.1.0/Tenants 7.0.2 | PASS: 15 cases, two distinct gateway IDs accepted, real native Sample actor query and six preserved HTTP bindings, wrong policy consumers refused, foreign adapters refused, six missing/malformed gateway inputs refused before output, and SDK 3.129.0 default/scoped compatibility controls. Eight builds: zero warnings/errors. |
| Current composition test, SDK 4.1.0/Tenants 7.0.5, signed Builds tool 4.45.0 | PASS: one executed, zero failed/skipped; the existing required package-test control forwards a nondefault gateway AppId to the actual reader request and all 12 emitted policy consumers, retaining its 23 routes/operations/declarations and six measured bodies. Test build: zero warnings/errors. The required CI test count stays one. |
| Working-diff whitespace | PASS with `git -c core.whitespace=cr-at-eol diff --check`, recognizing the existing C# CRLF source convention. |

Reproduce current executable controls from the project root (choose a fresh evidence directory):

```bash
PYTHONDONTWRITEBYTECODE=1 python3 tests/catalog-builder/verify_catalog_builder.py --builds-root /home/administrator/projects/hexalith/platform/references/Hexalith.Builds --sdk-version 4.1.0 --tenants-version 7.0.2 --evidence /tmp/gateway-query-binding-verification-final-20261109
```

Reproduce the current composition control with its existing private package-test purpose:

```bash
dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Release -m:1 -nr:false --disable-build-servers -warnaserror -p:UseHexalithProjectReferences=false -p:HexalithBuildsRoot=/home/administrator/projects/hexalith/platform/references/Hexalith.Builds -p:HexalithBuildsPublishedToolRoot=/home/administrator/.nuget/packages/hexalith.builds.module.cli/4.45.0/tools/net10.0/any -p:HexalithEventStoreVersion=4.1.0 -p:HexalithTenantsVersion=7.0.5 -p:PlatformPackageReaderWorkspaceRoot=/home/administrator/projects/hexalith/platform
dotnet tests/Hexalith.Platform.Composition.Tests/bin/Release/net10.0/Hexalith.Platform.Composition.Tests.dll -class Hexalith.Platform.Composition.Tests.PlatformEventStorePackageCatalogReaderTests -result-xml /tmp/gateway-query-composition-test-current.xml
```

The original proof recompiled the unmodified native Sample source from `dbc6e7e08d605ba6c6c790e3e08a15dc8bbdec8a` against published SDK 3.129.0 and executed the Program from original implementation `5b47d4ce733a8de645bfa7df776d5cd0cf06c744`. It does not claim that native fixture source and package were published together. The SDK 3.129.0 package's own source revision is `c02afa0fce310ccdb696232f18829df96cba088e`.

Fresh local evidence:

- Current executable report: `/tmp/gateway-query-binding-verification-final-20261109/result.json`; SHA-256 `07b0d2d4269d648a6f7f0005ab343dd40a96f03d4bab2bad7dbd0e4d32984c18`; executed Program SHA-256 `2ab8ec69412a24483338c400ff7b0057e544b1a34028e6b10de0d6fafa49d1c4`.
- Original executable report: `/tmp/gateway-query-original-evidence-ixcdtcts/result.json`; SHA-256 `a7b2f304819c602ec446388ab1fcc72dc25498c325a3e194f013c33580c48806`; reproduction helper `/tmp/verify-original-gateway-query-binding.py`.
- Composition build/test: `/tmp/gateway-query-composition-build-current.log`, `/tmp/gateway-query-composition-test-current.log`, `/tmp/gateway-query-composition-test-current.xml`.

Three review layers are complete. Both surviving patch groups are resolved; one optional, pre-existing positive successor-generation scenario is recorded in `deferred-work.md`. No story-key/sprint synchronization applies to this freeform spec.
