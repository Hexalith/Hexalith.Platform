---
title: '1.1 Validate module declarations against the Platform declaration schema'
type: 'feature'
epic: 1
story: 1
created: '2026-10-07'
status: 'done'
baseline_commit: 'f043a2f242762233091abdaa5bbe1ab777bd0f12'
builds_baseline_commit: '520abb5898ad44b30c0744e707b53cd94741e6b1'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Builds' v1 manifest cannot express the complete Platform declaration. Module developers need actionable validation before their declarations enter a composition.

**Approach:** Publish `hexalith.module-manifest.v2` in Builds and expose local enrollment validation through `hexalith-module validate`. Define all five declaration groups now; later stories add stage-specific rules within this major.

## Boundaries & Constraints

**Always:** Platform owns semantics; implementation belongs to `references/Hexalith.Builds`. Use strict camelCase JSON: root `schema` and nonempty `modules`; each module has `identity`, `runtime`, `surfaces`, `integration`, and `lifecycle`. Publish explicit nested properties and types, allowing empty collections for unused capabilities. Define:

- Identity: module, app/resource identities, enabled servers and dependencies.
- Runtime: logical Dapr roles, recovery classes and configuration bindings; extension packages/inputs; requests, limits, replicas defaulting to one, volumes.
- Surfaces: logical interfaces, route prefixes, exposure, surface class, authorization, callers and operations.
- Integration: topics/dead-letter policy, logical secrets/dynamic namespaces, identity needs, egress, provider tenancy, workers and disable controls.
- Lifecycle: readiness, scoped tasks/authority, recovery/fence hooks, reopening/replay behavior, startup override, critical flows/smoke surfaces, classification and recovery inventory.

Initially accept only v2: the previous-major window is bounded by the first Platform major, 2. Never count v1 as eligible enrollment. Validate all supplied files together for duplicate module, app and resource identities; on any error return diagnostics and no usable declaration set. Keep ordering deterministic and include source file, full field path and reason. Require usable readiness for required servers and positive finite startup overrides with nonblank justification. Support ten-minute default startup metadata.

**Never:** Change legacy v1 qualification behavior, run/down/test contracts, evidence fixture bytes or platform pins. Do not launch resources, compose environments, execute tasks, migrate module declarations, initialize nested submodules or publish packages. Preserve unrelated workspace edits, including story 4.3.

## I/O & Edge-Case Matrix

| Input/state | Output/behavior |
|---|---|
| Complete local v2 declarations | Validated set; CLI exits 0 |
| Unknown/missing fields, invalid types, duplicate keys or identities | File/field/reason diagnostics; no validated set; exit 1 |
| v1 or unsupported major | Explain required major and eligible list; exit 1 |
| Required server without readiness; invalid override | Local configuration failure naming offending field |
| Invalid scope or missing authority class | Reject task; allow per-start-verify, once-per-environment-creation, recovery, operator-only scopes |
| Dapr literal component name or missing role/recovery/binding | Reject; recovery class must be authoritative-restore, rebuild-only or live-authority-only |
| Enrolled host maps MCP endpoint | Reject under AD-11, regardless of exposure |

</frozen-after-approval>

## Code Map

Paths below are relative to `references/Hexalith.Builds`.

- `src/libraries/Hexalith.Builds.Tooling/Manifest/ModuleManifestLoader.cs`: v1-only loader; preserve it and its models. Runtime consumers depend on these contracts.
- `src/libraries/Hexalith.Builds.Tooling/Diagnostics/ToolDiagnostic.cs`: reuse Source/Field/Message, formatter and stable exit codes. Existing duplicate detection lacks complete paths.
- `src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs`: command registration; validation must bypass runtime execution.
- `src/libraries/Hexalith.Builds.Module.Cli/Hexalith.Builds.Module.Cli.csproj`: tool packaging currently omits schemas.
- Platform's `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md`, Consistency Conventions and AD-9/AD-11: authoritative concepts. JSON spelling is an implementation decision.

## Tasks & Acceptance

**Execution:**

- [x] `schemas/hexalith.module-manifest.v2.json` — define the complete strict Draft 2020-12 contract and defaults; avoid later structural additions.
- [x] `src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs`, `PlatformManifestValidationResult.cs`, `SupportedPlatformManifestSchemas.cs` — implement bounded parsing, schema/semantic validation, current/previous eligibility and atomic multi-file results; reuse existing path/secret safeguards where applicable.
- [x] `src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj` — embed v2 schema so validation works outside the source checkout.
- [x] `src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs` — add `validate --manifest <file>` with repeatable manifests and `--output human|json`; use existing diagnostics and cancellation contracts.
- [x] `src/libraries/Hexalith.Builds.Module.Cli/Hexalith.Builds.Module.Cli.csproj` — pack the schema under `tools/net10.0/any/schemas/`.
- [x] `test/fixtures/module/platform/valid.json`, `test/Hexalith.Builds.Module.Tests/PlatformManifestValidationTests.cs`, `PlatformManifestCommandTests.cs` — cover the matrix, nested paths, multi-error aggregation, cross-file duplicates, schema/runtime parity and no lifecycle side effects.
- [x] `Tools/test-g4-tool-package-contracts.ps1` — add packaged validate/schema probes alongside existing v1 contracts.
- [x] `README.md` — document the v2 command, complete example, diagnostic fields and v1/enrollment boundary.

**Acceptance Criteria:**

- Given the fixture exercising every group, when schema and local enrollment validation run, then both accept it and omitted replicas mean one.
- Given each invalid matrix case, when validation runs in human and JSON modes, then errors identify source and full field path and expose no usable partial set.
- Given several invalid files, when validated together, then recoverable field errors aggregate deterministically, including duplicate identities across files.
- Given existing v1 fixtures, when the qualification tests run, then their behavior and retained bytes remain unchanged.
- Given the tool's packaged dependencies, when validation runs outside Builds, then it uses the shipped schema without fetching remote schemas or starting resources.

### Review Findings

Code review R4 (2026-10-08): four layers (Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor) over the Builds story commits `305742f`, `34c63c5`, `80e7a3c`, `f717a87` on `520abb5`. Owner pin commits and Platform-side changes are excluded. Paths are relative to `references/Hexalith.Builds`.

- [x] [Review][Patch] R4-D1 Schema cannot express fields that Stories 1.5, 5.3 and 5.4 require [schemas/hexalith.module-manifest.v2.json:659]. Owner resolved on 2026-10-08 (option A): add optional, empty-allowed `runtime.providerExceptions[]` (`name` enum of the three AD-9 exceptions plus `memories-redis-coordination`, `capability`, `owner`, `surface`, `transitional`), `criticalFlows[].e2eChecks[]` and `criticalFlows[].tenantLifecycle`; stage validation stays with Stories 1.5/5.3/5.4. Medium (AA). The `provider`, `dapr`, `criticalFlow` and `operation` definitions are closed (`additionalProperties: false`) and have no field for:
  - AD-9 provider-SDK exceptions or Memories' transitional Redis coordination. Story 1.5 requires both.
  - A flow-to-E2E check mapping. Story 5.3 refuses releases whose flows have no required E2E check.
  - A tenant-lifecycle flow marker. Story 5.4 needs it.

  Either v2 grows structurally later, against "avoid later structural additions", or those stories cannot declare what they need. The auditor's agent-eligibility gap is excluded: eligibility belongs to the Contracts schema digest served by the gateway (AD-11 Availability; epics Story 3.3 route entries).
- [x] [Review][Patch] R4-D2 `surfaceClass` is an open identifier, and the "complete" example uses a class that does not exist [schemas/hexalith.module-manifest.v2.json:391]. Owner resolved on 2026-10-08 (option A): enum `[ui, agent, service]`; the fixture and README example use `agent`. Medium (AA+BH). AD-14 defines the realm-contract classes `ui`, `agent` and `service`. The schema (`schemas/hexalith.module-manifest.v2.json:391`) accepts any identifier, including `mcp`. `test/fixtures/module/platform/valid.json:70` and `README.md:336` publish `"gateway"`.
- [x] [Review][Patch] R4-P1 `classification.changeClass` uses `patch` and lacks the spine's `none` [schemas/hexalith.module-manifest.v2.json:1046]. Medium (AA). Module intake defines breaking, additive, or none; reproduced rejection of `"none"`.
- [x] [Review][Patch] R4-P2 Required-server readiness binding and the `required` gate are untested [src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs:499]. Medium (VG pre-verified, plus BH). Nothing fails if either of these regresses:
  - The `probe.server == id` condition. Cover it with a required server whose only usable probe targets another server; it must fail.
  - The `required == true` guard. Cover it with a non-required, disabled server that has no readiness; it must pass.

  Cover both cases in the validator and in both CLI formats.
- [x] [Review][Patch] R4-P3 CLI invalid-matrix assertions can pass vacuously [test/Hexalith.Builds.Module.Tests/PlatformManifestCommandTests.cs:583]. Medium (BH+AA). `ShouldContain(field)` is satisfied by message text for `schema` and `$`. `ShouldNotContain("declarations")` can never fail. JSON mode only checks that fields are nonblank. Assert the exact `field`/`source` in JSON diagnostics, a field-labelled match in human output, and the `failed` status.
- [x] [Review][Patch] R4-P4 A non-seekable manifest or unusable working directory crashes with a stack trace [src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs:127]. Medium (EH+BH). Reproduced: `--manifest <(cat valid.json)` throws an unhandled `NotSupportedException` from `FileStream.Length`. A deleted working directory also escapes the path catch at line 117. Fix: map both to structured HXP004/HXP005. This is distinct from the rejected R3-BH04 FIFO hang.
- [x] [Review][Patch] R4-P5 A scheme-relative credential URI bypasses the Platform credential detector [src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs:420]. Medium (EH). Reproduced: task argument `--endpoint=//user:pw@host.example` validates. Make the scheme optional in `CredentialUriAnywhereRegex`.
- [x] [Review][Patch] R4-P6 `routePrefix` and `mountPath` accept `//` and `.`/`..` segments [schemas/hexalith.module-manifest.v2.json:378]. Low (BH). Reproduced: `/api/../admin//x` validates, but hosted Gateway API HTTPRoute path validation rejects these forms. Mirror the `repositoryPath` lookaheads.
- [x] [Review][Patch] R4-P7 Kind-specific fields are not forbidden for other kinds [schemas/hexalith.module-manifest.v2.json:809]. Low (BH). Reproduced: an `http` readiness probe that also has `service`/`arguments` validates. `executable` on an `http` probe is still path-checked. `deadLetter` with `strategy: none` accepts `topic`. Forbid inapplicable properties in each `then` branch.
- [x] [Review][Patch] R4-P8 Placeholders and credentials in executable paths produce duplicate HXM006/HXM007 diagnostics with a runner-oriented hint [src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs:557]. Low (BH). Reproduced two HXM006 entries for `$TOOLS/run.sh`, one with the hint "Resolve placeholders before invoking the runner." `InspectValues` already covers every string, so drop those rule IDs from the path diagnostics.
- [x] [Review][Patch] R4-P9 JSON syntax errors report only `field: "$"` with no location [src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs:203]. Low (EH). Populate the existing `ToolDiagnostic.Location` from `JsonException.LineNumber`/`BytePositionInLine`.
- [x] [Review][Patch] R4-P10 The README diagnostic contract overstates `source`/`field` guarantees [README.md:495]. Low (BH+AA+EH). The actual behavior differs from the README in four cases:
  - Set-level diagnostics use `source: manifest`: file count (HXP013) and empty request (HXP015).
  - Cancellation (HXC130) and usage (HXC001) diagnostics have no `source`.
  - A redacted path becomes `[redacted manifest path]`.
  - On Windows, a manifest on another volume is reported by its absolute path.

  Document these exceptions.
- [x] [Review][Patch] R4-P11 The exact cwd-relative `source` value is never asserted [Tools/test-g4-tool-package-contracts.ps1:1015]. Low (VG pre-verified). Emitting absolute paths would pass every `Contains`/`EndsWith` check. Parse the JSON probe output and assert `source -ceq 'platform-valid.json'`.
- [x] [Review][Patch] R4-P12 The published "complete" extension example declares a secret input, which AD-13 forbids [test/fixtures/module/platform/valid.json:33]. Low (AA). AD-13 forbids extension secrets outside a named AD-9 exception. Switch the extension input in the fixture and in `README.md:299` to a `configuration` source.
- [x] [Review][Patch] R4-P13 UTF-8 BOM acceptance is untested [src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs:149]. Low (BH). Removing the explicit BOM branch would break BOM-prefixed (Windows-editor) manifests without any test failing. Add a BOM-prefixed fixture case.
- [x] [Review][Defer] R4-W1 Bound startup override budgets and integer forms for typed consumers [schemas/hexalith.module-manifest.v2.json:985]. Deferred, medium (EH+BH). Reproducible inputs:
  - `timeoutSeconds` of `1e308` or `1e-300` passes the frozen "positive finite" rule, but `TimeSpan` conversion overflows or truncates.
  - `replicas`, `memoryMiB` and `sizeMiB` written as `1.0` or `1e3` pass JSON Schema `integer` but fail `GetInt32()`.

  Story 1.8 must define the representable or policy budget cap. Composition must normalize or reject non-canonical integers. This extends the existing quantity-conversion entry.
- [x] [Review][Defer] R4-W2 Provider tenancy admits `shared` [schemas/hexalith.module-manifest.v2.json:687]. Deferred, unverified; medium if true (AA). The spine defines the field as "external providers needing per-environment tenancy", and AD-8 separates hosted state by environment. To settle it, the owner states whether any declared external provider may be shared across environments.
- [x] [Review][Defer] Carried, already in the ledger with no duplicate entries:
  - Unresolved stage references (BH, EH).
  - Named-collection uniqueness (BH, EH).
  - Dependency-graph checks (BH).
  - Requests versus limits (BH, EH).
  - Quantity conversion bounds (BH, EH).
  - Authoritative-restore hook and inventory requirements (BH).

**Rejected (R4):**

- `false`: AD-11 rule redundant or MCP-over-HTTP accepted (BH, AA). Every declared MCP interface is rejected, through the schema enum plus HXP023 asserted at `PlatformManifestValidationTests.cs:236`. An HTTP interface that does not declare MCP cannot be recognized from the declaration, and `surfaceClass: mcp` is covered by R4-D2.
- `low`: FIFO manifest or executable hangs (EH ×2). The R3-BH04/R3-EH02 rejection is unchanged.
- `low`: the same file supplied twice collapses into a single-location duplicate (BH). Repeated-file duplicates are the intended contract-probe scenario, and the fix needs a new rule branch.
- `low`: two redacted paths merge under `Distinct()` (EH). This needs two credential-bearing file names in one request.
- `low`: the previous-major window has one embedded schema (BH, EH). This is unreachable while `CurrentMajor` is 2, and the v3 story must add per-major schemas, failing loudly otherwise.
- `low`: HXP001 wording when `schema` is missing (BH). Cosmetic.
- `low`: `$schema` is rejected at the root (BH). The strict root is per spec, and editors can associate the schema through workspace settings.
- `low`: rule-ID families (HXP013/HXP015/HXP020), no rule catalog, undocumented repository-root discovery and 256-module cap (BH). The README promises stable IDs, not one ID per cause.
- `low`: free-text `authorityClass`, `provider.capability`, `egress.destination`, control characters in `text`, and the `packageId` format (BH). The spine names no closed vocabulary for these, multiline text is legitimate, and restore fails loudly on a bad package ID.
- `low`: `ssh://git@host` is flagged as a credential (EH). Detection is conservative by design, because username-only userinfo can carry tokens. Omitting the userinfo is the workaround.
- `low`: `$NAME` task arguments are rejected as placeholders (EH). This is the v1 safeguard the spec requires reusing, and an escape syntax would be new surface.
- `low`: TMPDIR inside a git checkout (EH). Tests fail loudly in an environment that was not demonstrated.
- `low`: an external `-FixtureRoot` without the Platform fixture (EH). `Copy-Item` fails loudly and names the path.
- `low`: invalid Unicode or an ineligible schema stops further checks of that file (AA, EH). Atomic rejection still occurs, evaluating unreadable strings would throw, and v1 documents carry no v2 identities.
- `low`: AC5 no-fetch is not exercised at runtime (AA). It holds by construction: the schema is an embedded resource with only local `#/$defs` references.
- `low`: nested placeholder, `deadLetter: none` and README/fixture-sync tests (BH). Nested strings already go through the same recursive inspection that the R3 task-argument tests cover.
- `low`: a 1 MiB buffer is allocated per file (BH). Allocation is bounded and transient.

## Implementation Notes

- Tooling embeds the shipped Draft 2020-12 schema; an isolated registry refuses remote fetches. The tool also packs the published schema.
- Atomic results carry immutable JSON declarations with replicas/startup defaults applied. Diagnostics aggregate source, full indexed field path and reason across files.
- The standalone command preserves cancellation/output contracts. Existing v1 loader, schema, runtime command parser, pins and retained fixtures are unchanged.
- Updated the package-gate test double for the new schema and validation probes; its original cleanup/failure assertions still pass.
- Resumed review patches reject embedded URI credentials, authority-bearing readiness endpoints and control characters in executable paths. Task/readiness arguments retain empty and whitespace-only values; every diagnostic has an explicit root or property path.
- Added quantity, byte/depth-boundary and diagnostic-metadata redaction coverage in schema, atomic enrollment and public CLI formats.

## Spec Change Log

- 2026-10-08: Third review resumed on Builds commit 6f07763bd955d22ace0123798add528dc933bf51, whose owner-authored pin/fixture migration independently resolves the old consistency blocker. Corrected the existing v2 field and diagnostic contracts and added the R3 regression coverage without editing the frozen intent or any legacy paths. This run preserves v1 bytes from that migrated checkout; it does not claim preservation against the older pre-migration baseline or grant migrated-tuple acceptance.

- 2026-10-08: User requested "use eventstore 3.117.1". Applied that selection to the shared catalog EventStore property (13 package rows) and the active Platform AppHost integration. Its SDK, Docker, Redis and Keycloak references were aligned to the existing catalog to satisfy the new integration dependencies. This supersedes the version freeze for the requested active EventStore selection and required AppHost dependency alignment; retained legacy runner pins and byte-bound qualification fixtures remain unchanged. Migration of that legacy qualification set and its FrontComposer alignment were presented as a separate scope choice.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH01 | medium | patch: server IDs | Two module-local api IDs with distinct app/resource IDs enroll and share readiness; the required-server check needs unique IDs. |
| BH02 | medium | defer: stage references | Resource/interface/readiness target resolution is absent. The frozen intent explicitly reserves stage-specific rules for later stories; composition binding belongs there. |
| BH03 | medium | defer: named collections | Task/interface/hook/operation names can collide; execution and catalog stages will need uniqueness. Current intent reserves their stage-specific rules. |
| BH04 | medium | defer: stage references | Hook references are retained without local resolution; the later recovery-hook contract owns which targets and authorities are admissible. |
| BH05 | maybe-false | defer: restore policy | The schema permits authoritative-restore without a local restoreHook. Whether a module-local hook is mandatory requires the future recovery contract; native/operator restore may supply it. |
| BH06 | medium | defer: stage references | Critical/smoke references are not resolved; the frozen intent reserves stage rules and story 5.3 explicitly owns critical-flow and smoke validation. |
| BH07 | maybe-false | defer: stage references | A secret reference is preserved without binding resolution. The current input lacks environment bindings, so local-only resolution is not established; later composition must settle it. |
| BH08 | medium | defer: stage references | Topic role resolution/capability matching is absent and belongs to the explicitly deferred Dapr binding/composition stage. |
| BH09 | medium | defer: allocation policy | Requests above limits pass the structural schema. The frozen intent reserves stage-specific scheduling rules; later composition must reject unprovisionable allocations. |
| BH10 | maybe-false | defer: quantity conversion | An exponent such as 1e999 passes a JSON number field, but this validator retains JsonElement and never converts resources to double. Downstream target types/bounds must be settled at composition. |
| BH11 | medium | defer: dependency graph | Self-dependencies pass identifier validation. Graph construction and cycle rules belong to the later composition stage explicitly reserved by the intent. |
| BH12 | medium | patch: readiness syntax | The built CLI accepts /health with an embedded newline as usable HTTP readiness; reject control characters in the v2 endpoint schema. |
| EH01 | high | patch: Unicode | Reproduced an escaped unpaired surrogate causing InvalidOperationException, empty CLI output and abandonment of a later invalid file. Return structured diagnostics and continue the set. |
| EH02 | medium | patch: anchored strings | Reproduced both moduleId and configurationKey trailing newline acceptance. Their format patterns must match the entire string. |
| EH03 | medium | patch: server IDs | Reproduced the same duplicate-server-ID readiness ambiguity as BH01; group under the identical root cause. |
| EH04 | medium | defer: stage references | The implementation preserves unresolved symbolic references; binding/flow/recovery target rules are explicitly reserved for later stages, as in BH02/BH04/BH06/BH08. |
| EH05 | maybe-false | defer: quantity conversion | The reported infinity requires downstream GetDouble, which this code never calls for resource quantities. Future composition must define representable target types and limits. |
| EH06 | medium | defer: allocation policy | Requests above limits are accepted structurally, as in BH09; resource scheduling validation is a reserved later-stage rule. |
| EH07 | low | patch: empty field path | Reproduced an unknown empty root key yielding field=""; provide an explicit nonblank escaped property path. |
| VG01 | medium | patch: enablement coverage | Pre-verified gap: removing required:true/enabled:false rejection would leave existing tests passing. Add assertions in both CLI formats. |
| VG02 | medium | patch: command readiness coverage | Pre-verified gap: removing readiness executable existence checks would leave tests passing. Cover a canonical nonexistent command probe in both formats. |

| R2-BH01 | medium | defer: stage references | carried from BH02: server references are preserved; the frozen intent reserves composition binding rules for later stories. |
| R2-BH02 | maybe-false | defer: stage references | carried from BH07: secret references require environment binding policy that this local declaration input does not supply. |
| R2-BH03 | medium | defer: stage references | carried from BH08: topic/Dapr role resolution and capability matching belong to the reserved Dapr composition stage. |
| R2-BH04 | medium | defer: stage references | carried from BH04: hook target resolution is unchanged and belongs to the future recovery contract. |
| R2-BH05 | medium | defer: stage references | carried from BH06: critical-flow and smoke references remain deferred to story 5.3. |
| R2-BH06 | medium | defer: named collections | carried from BH03: collection-name uniqueness beyond server IDs is reserved for execution and catalog stages. |
| R2-BH07 | maybe-false | defer: quantity conversion | carried from BH10: the validator retains JsonElement quantities; representable target types and conversion bounds remain a downstream policy question. |
| R2-BH08 | medium | defer: allocation policy | carried from BH09: requests-versus-limits checks belong to the reserved scheduling stage. |
| R2-BH09 | maybe-false | reject: reserved recovery policy | Empty inventory and unresolved restore-hook metadata are accepted, but the frozen intent permits empty collections and reserves recovery-stage rules. The future recovery contract must establish which volume inventory entries and local hooks are mandatory. |
| R2-BH10 | medium | patch: path controls | Reproduced CLI success for a route prefix with a newline and a mount path with NUL. Reject controls in both v2 path patterns, using the existing readiness restriction. |
| R2-BH11 | medium | patch: URI credentials | Reproduced a usable declaration containing HTTPS userinfo credentials. Extend secret detection privately within Platform validation so the legacy v1 detector and behavior remain unchanged. |
| R2-BH12 | medium | patch: property paths | Reproduced numeric keys yielding both object and array paths, and dotted keys yielding fictitious nesting. Resolve pointer segments using their actual container and escape unusual property names. |
| R2-EH01 | medium | defer: stage references | carried from BH02/EH04: undeclared server-reference checks are reserved for composition binding. |
| R2-EH02 | medium | defer: stage references | carried from BH04/EH04: hook-to-task resolution remains reserved for the recovery contract. |
| R2-EH03 | maybe-false | defer: quantity conversion | carried from BH10/EH05: CPU conversion can overflow or underflow downstream, but this validator never converts resource quantities to double. |
| R2-EH04 | medium | defer: allocation policy | carried from BH09/EH06: request/limit scheduling checks are explicitly reserved for later stages. |
| R2-EH05 | medium | patch: path controls | Confirmed the same route-control acceptance as R2-BH10; one schema correction addresses this shared defect. |
| R2-EH06 | medium | patch: property paths | Confirmed the same numeric and punctuation path ambiguity as R2-BH12; one path-formatting correction addresses this shared defect. |
| R2-VG01 | medium | patch: readiness coverage | Pre-verified gap: rejecting valid gRPC or command readiness leaves existing tests passing. Cover successful required-server enrollment and empty diagnostics in both CLI formats. |
| R2-VG02 | medium | patch: file-bound coverage | Pre-verified gap: increasing or removing the 256-file bound leaves existing tests passing. Cover acceptance at 256, atomic rejection at 257 and the CLI overflow diagnostic. |

| R3-BH01 | medium | defer: separate pin migration | The package control assertion still requires EventStore 3.110.0 and the publication helper defaults to it. The owner migrated pins in commit 6f07763bd955d22ace0123798add528dc933bf51 before this run; completing that qualification migration is separate from declaration validation. |
| R3-BH02 | medium | patch: embedded URI credentials | Reproduced successful enrollment with a credential-bearing URI embedded in a task argument. Extend the private Platform detector while preserving the legacy detector. |
| R3-BH03 | medium | patch: readiness authority | Reproduced success for //evil.example/health. HTTP readiness declares a path on the required server; reject authority-bearing URI references in the endpoint pattern. |
| R3-BH04 | low | reject: uncommon special files | Reproduced a deliberately supplied FIFO blocking through SIGINT; the stream-length access can also reject nonseekable streams without a diagnostic. Ordinary local JSON files do not encounter this, and a portable cancellation-safe special-file contract requires more than a direct correction. |
| R3-BH05 | low | reject: pathological diagnostic amplification | A deliberately constructed 23,562-byte document with a 5,000-character unknown key and 500 credential strings emits 2,652,319 bytes. This unusual malformed input requires a separate diagnostic-budget policy; adding limits or truncation would complicate the full-path aggregation contract. |
| R3-BH06 | medium | patch: exact command arguments | Empty and whitespace-only arguments fail the schema, although they are ordinary argument values. Preserve string contents in both readiness and task argument arrays while retaining the existing length bound. |
| R3-BH07 | medium | patch: repository path controls | Reproduced successful command-readiness enrollment using an existing filename ending in a newline. Make the v2 repository-path pattern fully anchored and reject control characters. |
| R3-BH08 | maybe-false | defer: command execution contract | A readable README.md passes local readiness metadata validation. The later execution stage must settle interpreter, DLL, script and platform execution rules before deciding executability; this story explicitly reserves stage-specific rules and does not launch commands. |
| R3-BH09 | medium | defer: unrelated review transport | The separate custody review transport applies the 4,096-character scalar budget to its complete envelope body. A larger otherwise admissible review is rejected; the frozen intent preserves this unrelated work. |
| R3-BH10 | false | reject: already corrected externally | The current custody verifier checks the exact header plus 88-character envelope length and framing before decoding. This disproves the snapshot's oversized-signature claim at the cited allocation. |
| R3-BH11 | medium | patch: parsing boundary coverage | Existing tests cover an oversized file but do not accept exactly 1 MiB or distinguish depth 64 from depth 65. Add validator and both-format CLI boundary assertions. |
| R3-EH01 | medium | patch: embedded URI credentials | Independently reproduced the same task-argument credential bypass as R3-BH02; one private detector correction addresses both reports. |
| R3-EH02 | low | reject: uncommon special files | Confirmed the same deliberately supplied FIFO cancellation failure as R3-BH04; retain that verdict and its nontrivial portable-correction rationale. |
| R3-EH03 | low | patch: root diagnostic path | Reproduced invalid credential and placeholder root strings producing field="". Use $ for root diagnostics so every returned field is actionable. |
| R3-EH04 | medium | defer: separate pin migration | The external owner commit 6f07763bd955d22ace0123798add528dc933bf51 changed v1 pins and synthetic fixture bytes. Historical byte-preservation statements predate that migration; this run must preserve the migrated checkout and must not claim fresh tuple acceptance. |
| R3-VG01 | medium | patch: quantity boundary coverage | Pre-verified gap: removing CPU, memory or volume minimums leaves existing positive-fixture and rejection tests passing. Cover zero and negative requests, limits and volume sizes in schema, atomic enrollment and both CLI formats. |
| R3-VG02 | medium | patch: diagnostic redaction coverage | Pre-verified gap: credential-bearing filename and property-name redaction can regress while value-redaction tests pass. Assert complete output redaction and actionable failures in both CLI formats. |

| R3-BH01-follow-up | false | reject: corrected externally | Owner commit 57a3167dcf6a4855c195fc1029856b5251e16ac3 aligned both qualification consumers and their test expectation to EventStore 3.117.1. The current mismatch is resolved; fresh migrated-tuple acceptance remains a separate follow-up. |

| R5-BH01 | medium | patch: readiness endpoint canonical form | The endpoint pattern rejects only a leading `//`. `/health/../admin` and `/health//ready` match it, and `IsUsableReadiness` accepts any `/` path, so a required server enrolls. |
| R5-EH01 | medium | patch: readiness endpoint canonical form | Same defect as R5-BH01: dot and empty segments in the readiness endpoint still enroll as usable. |
| R5-BH02 | low | reject: encoded traversal needs a decoder | `/sample/%2e%2e/admin` passes the literal lookaheads. This validator stores the path and does not decode it, so no traversal occurs here. Everyday declarations do not use percent-encoded dots, and decoding them is a new path policy rather than a direct correction. |
| R5-EH02 | low | reject: encoded traversal needs a decoder | Same claim as R5-BH02. |
| R5-BH03 | medium | patch: encoded userinfo | `Uri.TryCreate` returns false for `https://user:pw%40host.example`, and the regex requires a literal `@`, so both that value and `//user:pw%40host.example` enroll. |
| R5-BH04 | low | reject: stage rule reserved | Extension `source: secret` still validates. The frozen intent leaves stage-specific rules to later stories, and the resolved R4-P12 change was the published example, which now uses `configuration`. |
| R5-BH05 | low | patch: diagnostic id wording | The new README sentence assigns HXP013 only to the 256-file limit and HXP015 only to an empty request. The same IDs also report the 1 MiB file limit and a missing required field. |
| R5-BH06 | low | reject: location contract already met | Location is the requested 1-based line and `BytePositionInLine`. Translating nested `JsonException.Path` JSONPath into dotted fields is a separate parser, and syntax failures already identify the location. |
| R5-BH07 | medium | patch: process working directory | The deleted-cwd tests call `Directory.SetCurrentDirectory` while the assembly has no parallelization disable. Other collections can observe that process-wide directory. |
| R5-BH08 | low | patch: optional server coverage | Readiness is required only when `required` is true. No test enrolls an enabled, non-required server with an empty readiness list, so widening that gate to every enabled server stays green. |
| R5-BH09 | false | reject: fields already retained | Unknown `providerExceptions[].name` values fail the closed enum. `ApplyDefaults` re-parses the whole module and only fills omitted replicas and the default startup timeout, so `providerExceptions`, `e2eChecks`, and `tenantLifecycle` remain. Empty `e2eChecks` is the allowed stage-deferred form. |
| R5-BH10 | medium | defer: quantity conversion | carried. R4-W1 already records that `1e308` and `1e-300` satisfy the frozen positive-finite rule while `TimeSpan` cannot represent them, and that non-canonical integers remain a composition concern. The acceptance test still locks that frozen rule. |
| R5-EH04 | medium | defer: quantity conversion | carried. Same startup-budget claim as R4-W1. |
| R5-EH05 | medium | defer: quantity conversion | carried. `memoryMiB` and `sizeMiB` above `Int32` maximum are the same deferred quantity-bound claim; this validator still does not convert those quantities. |
| R5-BH11 | low | reject: spec wording | The contradictory completion sentence is text in this spec. The review route does not edit the spec to close a finding. |
| R5-BH12 | false | reject: json source is exact | The package loop runs human and JSON. The JSON iteration requires `source` to equal `platform-valid.json`, so an absolute path fails the script even though the human check uses `Contains`. |
| R5-EH03 | low | reject: uncommon special files | carried. Opening a FIFO still blocks before `CanSeek`, which is the same deliberately supplied special-file hang rejected as R3-BH04 and R3-EH02. The new check runs only after open returns. |
| R5-VG01 | medium | patch: validate outcome contract | Pre-verified. Success, failure, and cancellation JSON results do not assert `outcome` exit, phase, category, or rule id, so those fields can change while the current command and package checks stay green. |

## Verification

Run from Builds:

- `dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1` — successful build with existing analyzer gates.
- `dotnet test --project test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug --no-build` — new validation and existing module suites pass. Use the documented direct xUnit assembly fallback if the runner blocks.
- Exercise package contract probes with locally packed artifacts; record their exact command and result. Package qualification does not publish or grant Platform tool acceptance (story 1.9).

### Initial execution evidence (2026-10-08, before review fixes)

Commands run from Builds. [Verification record](../../references/Hexalith.Builds/artifacts/story-1-1-validation/verification.json) retains exact commands, source hashes and log paths.

- Required Debug build: exit 0, zero warnings/errors.
- `dotnet test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -class '*PlatformManifest*' -result-xml artifacts/story-1-1-validation/platform-tests.xml`: 55 passed, zero failed/skipped/not-run. [Executed cases](../../references/Hexalith.Builds/artifacts/story-1-1-validation/platform-tests.xml).
- Required broad test command: 268 passed, one failed, exit 2. `SupportedPlatformPinsCatalogTests.CatalogDefaultsMatchSupportedPlatformPins` also fails at untouched HEAD: catalog EventStore 3.115.0 versus legacy runner 3.110.0; FrontComposer also differs (4.6.0 versus 4.5.0). Pins remain unchanged as required. The broad gate is not green. [Broad output](../../references/Hexalith.Builds/artifacts/story-1-1-validation/dotnet-test.log).
- `pwsh -NoProfile -File Tools/test-g4-tool-package-contract-gate.ps1`: passed.
- `pwsh -NoProfile -File Tools/test-g4-tool-package-contracts.ps1 -Version 0.0.0-story11.20261008.3 -PackageDirectory artifacts/story-1-1-validation/packages-0.0.0-story11.20261008.3 -SkipSourceValidation -RetainPackageDirectory`: passed before review fixes. Installed tools validate outside Builds; schema bytes, both output formats, duplicate identities, v1 rejection and unchanged input bytes pass. No publication eligibility or Platform acceptance is granted.

### Final execution evidence (2026-10-08, after review fixes)

The parent re-ran every required check after the implementation agent applied the review patches. [Final verification record](../../references/Hexalith.Builds/artifacts/story-1-1-validation/review-final-verification.json) retains exact commands, final source hashes, protected legacy paths, executed test methods and log hashes.

- Required Debug build: exit 0, zero warnings/errors. [Build output](../../references/Hexalith.Builds/artifacts/story-1-1-validation/review-final-build.log).
- Focused declaration validation and CLI tests: 85 passed, zero failed/skipped/not-run/errors. [Executed cases](../../references/Hexalith.Builds/artifacts/story-1-1-validation/review-final-tests.xml).
- Required full module suite: 298 passed, one failed, zero skipped, exit 2. The sole failure remains `SupportedPlatformPinsCatalogTests.CatalogDefaultsMatchSupportedPlatformPins`, caused by the unchanged baseline catalog/pin mismatch described above. [Full suite output](../../references/Hexalith.Builds/artifacts/story-1-1-validation/review-final-module-tests.log).
- Package-gate tests: exit 0; all 34 artifact-validator scenarios and the existing failure/cleanup checks passed. [Gate output](../../references/Hexalith.Builds/artifacts/story-1-1-validation/review-final-package-gate.log).
- Fresh local package probes: exit 0 using the recorded command with version `0.0.0-story11.20261008.4`, `-SkipSourceValidation` and `-RetainPackageDirectory`. The installed tools passed the shipped-schema, outside-checkout validation, both-format diagnostics, repeated-file duplicates, v1 rejection and unchanged-input probes. [Package output](../../references/Hexalith.Builds/artifacts/story-1-1-validation/review-final-package-contracts.log), [inventory](../../references/Hexalith.Builds/artifacts/story-1-1-validation/packages-0.0.0-story11.20261008.4/g4-tool-package-inventory.json). Publication eligibility and Platform acceptance remain false.
- Both repository diffs pass `git diff --check`. All 294 baseline-tracked schemas, fixtures, catalog, legacy loader, pins and pin-test files are unchanged.

Review patches cover invalid Unicode without aborting later files, complete string anchoring, duplicate server IDs, readiness endpoint controls, visible empty-property paths, required-disabled servers and missing command readiness. Every patched behavior has passing regression coverage; both CLI formats are exercised where applicable.

The full verification gate cannot pass within this story's frozen prohibition on pin changes. The bmad-build review step therefore halts with the spec `in-review` and sprint story `in-progress`; completion is not recorded. The baseline catalog/pin inconsistency requires a separately scoped resolution before the build workflow can complete.

### Matrix audit

All covering tests below ran and passed in the final focused XML:

| Matrix row | Covering tests |
| --- | --- |
| Complete v2 / defaults / exit 0 | `CompleteFixturePassesSchemaAndEnrollmentWithDefaults`, `DistinctValidFilesReturnAllModules`, `ValidationSucceedsWithoutExecutingLifecycleAsync` (human/json) |
| Unknown/missing fields, invalid types, duplicate keys/identities | `InvalidFieldsMatchSchemaAndReportCompletePaths`, `DuplicateKeysFailWithNestedLocation`, `CrossFileDuplicatesAndRecoverableErrorsAggregateDeterministically`, CLI matrix/repeated-manifest tests (human/json) |
| Unsupported/v1 major | `UnsupportedSchemaExplainsEligibility`, CLI matrix (human/json) |
| Readiness / finite justified override | `RequiredServerMustHaveUsableReadiness`, `NonFiniteStartupOverrideFailsLocally`, `PositiveFiniteStartupOverridesPass`, CLI matrix (human/json) |
| Task scope / authority | `SupportedLifecycleScopesPass`, invalid fields, CLI matrix (human/json) |
| Dapr role / recovery / binding | `SupportedRecoveryClassesPass`, invalid fields, CLI matrix (human/json) |
| Enrolled MCP endpoint | `EnrolledHostCannotMapMcpForAnyExposure`, `McpEndpointsFailUnderEveryExposureAsync` (all exposures, both formats) |

Parent diff audit confirms preservation of v1 schemas/loaders, existing qualification fixture bytes and pins. The confirmed baseline failure remains a verification limitation.

### Resumed review and final verification (2026-10-08)

The resumed three-layer review identified three local defects and two verification gaps. The v2 schema now rejects controls in route prefixes and mount paths; Platform validation rejects URI userinfo credentials without changing the shared legacy detector; diagnostic paths preserve numeric and punctuation-containing object keys. New tests cover both output formats, valid gRPC/command readiness without execution, and the 256-file acceptance/257-file atomic rejection boundary. All findings are recorded individually above; earlier deferred findings were carried without duplicate ledger entries.

[Resumed verification record](../../references/Hexalith.Builds/artifacts/story-1-1-validation/resumed-20261008/final-verification.json) retains exact commands, current revisions, final source/log hashes and all executed test methods. This record supersedes the earlier final evidence for the current patched source.

- Required Debug build: exit 0, zero warnings/errors.
- Focused declaration validation and CLI tests: 112 passed, zero failed/skipped/not-run/errors. [Executed cases](../../references/Hexalith.Builds/artifacts/story-1-1-validation/resumed-20261008/final-tests.xml).
- Package-gate tests: exit 0, all 34 artifact-validator scenarios and existing failure/cleanup assertions passed.
- Fresh local package contract probes: exit 0, version `0.0.0-story11.20261008.5`, with `-SkipSourceValidation -RetainPackageDirectory`; shipped-schema and outside-checkout validation probes passed. No publication eligibility or Platform acceptance is granted.
- Required complete module suite: 325 passed, one failed, zero skipped, exit 2. The sole failure remains `SupportedPlatformPinsCatalogTests.CatalogDefaultsMatchSupportedPlatformPins`. [Full suite output](../../references/Hexalith.Builds/artifacts/story-1-1-validation/resumed-20261008/final-module-tests.log).
- Both staged and unstaged diffs pass `git diff --check`. All 293 protected legacy paths other than the externally updated catalog remain unchanged from the recorded baseline.

During this run, external work advanced the Builds checkout and changed the catalog EventStore version from 3.115.0 to 3.117.0; the unrelated catalog/audit changes are preserved. The frozen legacy runner still pins EventStore 3.110.0 and FrontComposer 4.5.0, while the catalog pins FrontComposer 4.6.0. The failing consistency test and legacy pins are untouched.

The review step requires a halt when required verification cannot be fixed within scope. Story 1.1 remains `in-review`, and its sprint entry remains `in-progress`. A separate catalog/legacy-pin resolution is required before completion; the failure is neither skipped nor treated as a passing gate.

### User-selected EventStore 3.117.1 (2026-10-08)

The shared `references/Hexalith.Builds/Props/Directory.Packages.props` catalog now selects EventStore `3.117.1` for all 13 family packages. Central catalog validation passed for 304 entries, package-authority checks passed, and the EventStore host restored its EventStore dependencies at `3.117.1` and built in Debug with zero warnings/errors. [Verification record](../../references/Hexalith.Builds/artifacts/eventstore-3.117.1/verification.json).

The targeted legacy consistency test still fails: its retained runner pin is `3.110.0`, while the catalog now selects `3.117.1`. Retained qualification fixtures and the FrontComposer legacy pin remain unchanged. Story status remains `in-review`; this catalog change does not close the legacy qualification mismatch.

Audit refresh was attempted with `pwsh -NoProfile -File Tools/audit-central-package-versions.ps1 -PriorAuditPath Tools/package-version-audit.json -Family hexalith-eventstore -OutputPath artifacts/eventstore-3.117.1/catalog-audit.json`. It exited 1 because the edited catalog is dirty relative to the recorded committed revision. The existing audit was preserved; refresh must follow a commit of the selected catalog bytes. [Exact audit output](../../references/Hexalith.Builds/artifacts/eventstore-3.117.1/audit-refresh.log).

The active Platform `apphost.cs` now uses `Hexalith.EventStore.Aspire@3.117.1`. Its Aspire SDK, Docker and Redis references are `13.6.1`, and Keycloak is `13.6.1-preview.1.26506.6`, matching the existing catalog and avoiding the reproduced NU1605 transitive downgrades. `dotnet build apphost.cs -c Debug` passed with the existing CLI-bundle configuration warning `ASPIRE010`; no runtime resources were launched. The docs API lookup completed without results for the Hexalith extension; unchanged API calls were verified by compilation.

### Completed review and current verification (2026-10-08)

This record supersedes the earlier blocked verification for the current checkout. The owner independently migrated the legacy pin/fixture tuple in `6f07763bd955d22ace0123798add528dc933bf51`, aligned the qualification consumers in `57a3167dcf6a4855c195fc1029856b5251e16ac3`, and committed this run's four validation patch files in `f717a87c26a8266bdde95d18f998ef2ab366d43a`. This build preserved all 289 protected legacy, catalog and fixture paths against the initial migrated owner checkout.

[Completion verification record](../../references/Hexalith.Builds/artifacts/story-1-1-validation/closure-20261008-287o7ox5/final-verification.json) retains exact commands, revisions, source/log hashes, all 141 focused executed cases and the package inventory.

- Required Debug build: exit 0, zero warnings/errors.
- Focused declaration/schema/CLI cases: 141 passed, zero failed/skipped/not-run/errors. All new parsing, quantity, credential, path and exact-argument regressions ran.
- Required full module suite: 355 passed, zero failed/skipped, exit 0. The catalog consistency test passes.
- Package gate: exit 0, all 34 artifact-validator scenarios and existing failure/cleanup assertions pass.
- Fresh installed-tool contracts: exit 0 for `0.0.0-story11.20261008.9`. Both output formats, multi-file identity rejection, legacy-major rejection, shipped schema bytes and validation outside the checkout pass. The official package script ran through an ephemeral dotnet wrapper adding `-m:1` to MSBuild operations; a serialized solution restore independently passed in 5.76 seconds after the parallel attempts stalled. The executed expanded commands and wrapper are retained.
- Package source-validation and qualification-control lanes remain outside this verification invocation (`-SkipSourceValidation`, no `-RequireControls`). The recorded inventory correctly sets `releaseEligible: false`; no fresh tuple acceptance, publication eligibility or Platform tool acceptance is granted.

All three review layers completed and every finding is triaged above. Eight distinct declaration/verification corrections were applied. Follow-ups cover migrated-tuple qualification, later command execution semantics and the unrelated custody review transport; the consumer-pin mismatch was independently corrected. The implementation spec is `done`, with Story 1.1 moved to sprint `review` for human review.

### R4 patch verification (2026-10-08)

Builds commit `50b0257001fe91f14bf16c7ea877d3e92bdfdf08` applies the open R4 patch findings. Deferred findings R4-W1, R4-W2, and the carried stage-rule items stay deferred. Parent verification re-ran the gates after that commit.

- Required Debug build: `dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1` exited 0 with zero warnings and errors.
- Focused declaration and CLI tests: `dotnet test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -class '*PlatformManifest*'` — 175 passed, zero failed, skipped, or not run.
- Required full module suite: the same assembly with no class filter — 389 passed, zero failed, skipped, or not run.
- Fresh installed-tool contracts: `pwsh -NoProfile -File Tools/test-g4-tool-package-contracts.ps1 -Version 0.0.0-story11.20261008.11 -PackageDirectory artifacts/story-1-1-validation/packages-0.0.0-story11.20261008.11 -SkipSourceValidation -RetainPackageDirectory` exited 0. MSBuild operations used `-m:1`. The JSON duplicate probe asserts `source` equals `platform-valid.json`. No publication eligibility or Platform tool acceptance is granted.

The Windows other-volume path exception is documented and was not exercised on this machine.

### R5 patch verification (2026-10-08)

Review patches remain uncommitted in Builds on top of `50b0257001fe91f14bf16c7ea877d3e92bdfdf08`.

- Required Debug build exited 0 with zero warnings and errors.
- Full module suite: 395 passed, zero failed, skipped, or not run.
- Fresh installed-tool contracts for `0.0.0-story11.20261008.12` exited 0, including exact JSON `outcome` checks for success, duplicate rejection, and legacy rejection. MSBuild operations used `-m:1`. No publication eligibility or Platform tool acceptance is granted.
