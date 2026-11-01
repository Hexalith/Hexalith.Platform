---
title: Bind catalog idempotency policies to the actual gateway
type: bugfix
created: '2026-11-01'
status: done
route: oneshot
baseline_commit: 68b785b6a929050771e70d78f83ff10fea3daabc
review_loop_iteration: 0
---

<frozen-after-approval reason="authorized prerequisite repair">

## Intent

CatalogBuilder currently binds every command policy to the SDK's default
`eventstore` consumer, although composition already supplies the actual gateway
application ID. A suffixed run therefore has correctly bound query routes and
45 policies whose consumer is outside its eight-host activation inventory.

Require the supplied gatewayAppId before generation, preserve existing query
bindings, and pass that exact value to the published SDK's three-argument
DevelopmentIdempotencyBindings.Bind API. Missing, blank or malformed input must
refuse before any catalog or receipt is written. Keep all declaration, policy,
digest, activation and trust validation in the existing owner SDK.

Prove the actual executable with public SDK3.130.1 and genuine compiled owner
declarations. Cover suffixed identities, all45 policies and every existing query binding
in the copied Root cohort, missing input and independently rejected default or
foreign policy consumers. These are unactivated Development test inputs; no
runtime authority, Production trust, live operation or release is introduced.

</frozen-after-approval>

## Implementation Notes

- Work only in an isolated worktree from the recorded baseline. Preserve newer
  owner main, all nested submodules and every original producer output.
- Reuse the published SDK factory and activation-binding validator. Do not
  rewrite the generated catalog or infer a gateway from logical module names.
- File Scope:
  - `src/Hexalith.Platform.CatalogBuilder/Program.cs`
  - `src/Hexalith.Platform.CatalogBuilder/Hexalith.Platform.CatalogBuilder.csproj`
  - `tests/catalog-builder/CatalogActivationProbe.cs`
  - `tests/catalog-builder/verify_catalog_builder.py`
  - `.github/workflows/catalog-builder.yml`
  - `_bmad-output/implementation-artifacts/spec-bind-run-scoped-catalog-consumer.md`
- The focused public fixture uses the existing published Tenants6 Contracts and
  Server artifacts. The full Root fixture uses hash-verified, read-only copies of
  its actual three owner DLL closures; it does not rebuild those producers.
- Preserve the existing manual tuple workflow and the broader composition tests.
  Their unrelated analyzer debt is outside this repair. No warning suppression.
- The focused workflow executes the real tool and the same public fixture; it
  does not substitute a source-text assertion for executable qualification.

- Source correction confirmed by the parent: seven is the Sample's TOTAL route
  inventory, not seven ProjectionActor queries. SDKsource1de Counter declarations
  contain the sole counter-status-query ProjectionActor target; Parties and
  Tenants Query helpers declare domain-service targets. Keep one actual actor
  query, all seven Sample routes and every other real query classification.
- Build original68 and patched actual executables on identical complete requests.
  Compare all declarations, bindings, operations and non-consumer policy fields
  structurally; only policy consumer/digest and the idempotency facet may change.
- Default compatibility: published SDK3.129.0 has only the two-argument API.
  A typed finite compile capability selects the real three-argument overload for
  qualified stable3.130.0/3.130.1 selections and same-binary metadata variants.
  Older selections may use their existing factory ONLY when the actual required
  request field is exactly eventstore; scoped identities refuse before output.
  No default SDK or other Platform dependency is upgraded. Verify the genuine
  default129 build, explicit-eventstore control and scoped-consumer refusal.

- Final focused proof: original68 executable reproduces genuine SDK activation
  refusal with default consumers. PublicSDK3.130.1 and3.130.0 fixtures each pass
  13 executable controls, including realSDK3.129.0 legacy bare/scoped cases.
  The full copied Root cohort passes11 controls:45 policies bound to either of
  two actual suffixed gateways,1 actual actor query,19 domain-service queries
  and all7 Sample routes. All declarations/bindings/operations and every policy
  field except consumer/content-digest equal original68 structurally.
- The actual owning tool project builds at its genuine copied defaultSDK3.129.0
  WITHOUT an SDK override, and explicitly at public3.130.1, both with0warnings
  and0errors. Eight finite-version/metadata/future/prerelease capability controls
  and actionlint pass. No composition-wide tests or warning suppressions.
- Proof directory: `/tmp/platform-catalog-consumer-ky973qv0/proof`. Independent
  BlindHunter review is pending; no commit, push or original selection yet.

## Review Triage Log

The single required BlindHunter review is complete; no additional review loop.

| Finding | Verdict / route | Evidence and bounded resolution |
|---|---|---|
| 1. Version-only capability can misdescribe source references; default composition cannot bind scoped IDs | medium / patch; broader profiles excluded | Typed three-argument selection now requires package references plus the finite130/131 versions. Root PartiesDevelopmentCatalogTool.cs explicitly copies both files and selects public131. Default129 tool builds and accepts only explicit eventstore, refusing scoped IDs. Broader unqualified/source composition remains intentionally refused; no dependency upgrade. |
| 2. CI only tests131 | medium / patch | Focused workflow matrix now executes both real public1300 and131 builds/controls, with distinct artifacts. |
| 3. Actor/multiple-owner fixture is optional | medium / patch | Durable public fixture compiles genuine SDK Sample and Sample.Contracts sources at the immutable published SDK commit into nonpackable/nonpublishable assemblies. It uses actual existing types, verifies public Core Contracts archive commit and executed DLL bytes, and joins Tenant6:17 policies,1 actor query,7 Sample routes. Full copied Root45 policy fixture remains separately qualified. |
| 4. No predecessor/successor migration cases | false / reject for this repair | Root starts generation1 per private attempt; predecessor forwarding in Program.cs is unchanged. SDK RouteCatalogV3Tests owns SuppliedCommittedRootMustMatchTheExactHistoryBytes, RevisedAndRemovedPoliciesSurviveWithOriginalGenerationsDigestsAndKeys and TamperedCommittedHistoryIsNeverRepaired. No caller migration or SDK policy rewrite is introduced. |
| 5. Reused evidence can retain a stale pass | medium / patch | Require a fresh evidence directory. A reuse attempt preserves the previous report by content hash and replaces the current result with failed/nonaccepting state. New runs write started then failed/passed state. Real CLI controls verify reuse refusal and a new-run failure cannot expose a prior pass. |
| 6. Executed identity is not retained | medium / patch | Retain primary executed DLL/deps/runtimeconfig files, hash every output DLL, record assets SHA/resolved SDK/source identity, and independently compare executed Core Contracts DLL bytes to the verified public archive. |

## Final qualification

- Public1300 and131 profiles each pass13 executable controls with the genuine
  Sample+Tenant fixture; the legacy129 controls still use genuine Tenant6 only.
- Full copied Root131 preserves45 consumer policies,1 actor query,19 domain
  queries,5 projections and7 total Sample routes. Original68 declarations,
  bindings, operations and non-consumer policy fields remain structurally equal.
- Default129 actual tool build without SDK override and the supported public
  builds have0warnings/errors. Existing tuple workflow and broader composition
  tests remain unchanged. Actual Root startup/authority acceptance is not claimed.
- Current proof profiles are `public130-final`, `public131-final`, `root131-final`
  under the proof directory, plus default129-final-build.log and
  evidence-state-controls/controls.json. Focused exact-source remote CI remains
  required after the normal owner branch push; this local record is not that gate.


## Pending SDK 3.130.2 maintenance qualification

Prepare this finite follow-up from selected sourced9d0eb18 while the SDK owner
repairs method-specific technical reachability on its maintenance branch.
The existing d9 public130/public131 and default129 evidence remains valid;
no 3.130.2 public archive or immutable published source is verified yet.

Extend only the typed public three-argument binding capability, executable
fixture choice and focused workflow matrix with exact stable3.130.2 and the
already supported same-binary forms. Keep the default SDK, fixture default131,
all previous versions, project-reference refusal and future/prerelease
refusals unchanged. Request validation, policies, bindings and query inventory
are unchanged. No broader composition, SDK feature or trust scope is adopted.

The genuine Sample fixture source map deliberately has no guessed3.130.2 SHA.
Insert only the immutable source verified against all fourteen actual official
archives after publication; until then the new fixture choice cannot qualify.
Do not restore/build3.130.2, commit, push or enroll the draft workflow beforehand.

After that handoff, repeat actual normal public restore/build, all13 executable
public132 controls, default129 compatibility and affected finite selection
controls. Preserve the actual Root45 policies,1 actor query and7 total Sample
routes comparison where its immutable fixture remains available. Use pinned
commitlint and normal hooks; advance only the existing qualification branch.
Require its full exact-source workflow matrix. Original source selection and
any fresh producer preparation remain RootCI-coordinated.


## SDK 3.130.2 local public qualification completed

All fourteen official SDK132 archives are HTTP200 and verified against
published source28e3f260bbaf4619d1a99d656fba04c0c532387b and its repository
URL. The genuine nonpackable Sample fixture source map now pins that exact
verified commit. Initial stale-index NU1102 logs remain retained; supported
no-http-cache restore against the same public NuGet.org source succeeds,
without archive injection, a candidate feed or global-cache cleanup.

The actual public132 owning tool build has zero warnings/errors. All13 strict
public executable controls pass, including genuine Sample+Tenant owners and
legacy129 bare/scoped controls. All11 full copied Root controls also pass:
45 policies,1 actor query,19 domain queries and7 total Sample routes, with all
declarations/bindings/operations and non-consumer fields unchanged from68.
All13 actual MSBuild finite capability controls and actionlint pass. The same
default129 catalog input still builds without an SDK override or warnings.

Proof is /tmp/downstream-sdk132-drafts-wd24gzkq/proof, including public132 and
root132 result reports plus qualified-public132-provenance.json. All restored
SDK132 and executed Core DLL identities match the actual official archives.
The existing exact branch will be advanced normally after pinned lint/hooks;
its full public130/public131/public132 workflow matrix must pass. No original
source selection, dependency default, production trust or broader composition
scope is changed by this maintenance alias.
