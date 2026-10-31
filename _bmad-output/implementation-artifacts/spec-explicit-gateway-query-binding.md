---
title: Bind declared projection queries to the actual gateway
status: in-review
created: 2026-10-31
baseline_commit: 60761c60
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
