---
title: 'Recover retained routing catalog work'
type: 'feature'
created: '2026-11-01'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '03799ed553a16c8a1528068ba4d95bb5509ea75e'
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Six retained worktrees contain useful unfinished routing-catalog work mixed with superseded drafts. Missing source is recoverable from saved checkpoints.

**Approach:** Preserve staged, working and untracked versions; selectively reconcile useful edits with current main; validate, deliver owner commits before Platform pointers, and retire obsolete originals. The user approved this recovery plan with “apply” on 2026-11-01.

## Boundaries & Constraints

**Always:** Preserve backups, newer main security/custody/cancellation/gateway behavior, and concurrent edits. Use fresh worktrees, source/package separation, commitlint and normal delivery policies.

**Never:** Restore obsolete trees wholesale, initialize nested submodules, force-push defaults, bypass checks, remove package refusal, or claim runtime/package acceptance from structural tests.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Expected behavior | Handling |
|---|---|---|---|
| Superseded work | Main already implements stronger behavior | Keep main and verified archive | Record disposition |
| Missing source | Trusted checkpoint has exact bytes | Recover before integration | Stop affected port if unavailable |
| Invalid evidence | Missing, malformed, inconsistent or secret-bearing metadata | Reject before native child launch | Negative regression cases |
| Private Admin | Selected private profile has an allow-default | Change to deny; retain named grants and original input | Configuration regression |
| Concurrent edits | Original changes after snapshot | Preserve newer edits | Do not retire that original |

</frozen-after-approval>

## Code Map

- Backup: `/home/administrator/.local/share/hexalith/recovery/routing-catalog-20261101T101540Z`. Manifests, staged/unstaged patches and `working/` preserve versions. `pause-checkpoint/{Hexalith.Builds-final,Hexalith.Platform-final}/untracked-source.tar.gz` recovers missing helpers/tests; `prior-checkpoints/` preserves complete older drafts.
- Originals: `/tmp/hexalith-story33-release-0_wjbwkj/{Hexalith.Builds-final,Hexalith.Parties-final,Hexalith.Tenants-disposal,Hexalith.EventStore,Hexalith.EventStore-integrated,Hexalith.Platform-final}`. Do not alter originals during implementation. Release EventStore registration/index are backed up separately.
- Canonical owners: `{project-root}/references/Hexalith.{Builds,Parties,EventStore,Tenants,Memories}`. Create fresh recovery worktrees under the backup root from local main. No implementation-phase remote operations.
- Builds: port `src/libraries/Hexalith.Builds.Tooling/Runtime/` proof capture/archival and tests, including `SelectedRoutingCatalogSnapshot` and recovered `SelectedMetadataSafety`. Preserve `src/hosts/Hexalith.Builds.Module.AppHost/RunReadiness.cs` cancellation while adding observation propagation. Seed host uses enabled technical receiver with finite fallback. Omit integrated PowerShell churn.
- Platform: port `src/Hexalith.Platform.Composition/` metadata/proof/preparation and tests; recover missing metadata helpers. Preserve CatalogBuilder gateway validation, three-argument policy binding, finite SDK conditions and older request shapes. Add provenance compatibly. Keep the unqualified `eng/Hexalith.Platform.CatalogProof` prototype in the verified archive and omit it from delivery. Validate declaration revision/hash pins.
- Parties: port `src/Hexalith.Parties.AppHost/PartiesDevelopmentCatalogConfiguration.cs` plus `tests/Hexalith.Parties.IntegrationTests/Topology/DevelopmentCatalogConfigurationTests.cs`.
- EventStore drafts would remove newer protections; Tenants main already has ordered disposal with configured awaits. Both are superseded, requiring no source commit.

## Tasks & Acceptance

**Execution:**
- [x] Back up all six recoverable worktrees and prior archives; classify EventStore/Tenants as superseded.
- [x] Port Builds proof capture/archival, recover safety helper and tests, preserve cancellation, and check matching declaration revisions/hashes.
- [x] Reconcile the Parties private Admin draft: superseded by main’s explicit deny-default private profile and regression that refuses native allow-default input; no Parties source change required.
- [x] Port Platform metadata/proof/preparation changes against reconciled Builds/current SDK, preserving gateway compatibility, default builds and package refusal.
- [x] Run focused owner and coupled checks, record exact commands/results and residual limitations, and prepare the combined diff for independent review.

**Acceptance Criteria:**
- Given retained drafts, when local recovery finishes, then every snapshotted version is recoverable and substantive differences have integrated or superseded dispositions.
- Given selected native evidence, when readiness/archive runs, then expected identities and bounded safe metadata are checked before child execution and retained with results.
- Given supported source callers/default builds, when recovered code runs, then gateway binding, cancellation, finite fallback and package admission behavior retain passing regression coverage.

## Implementation Notes

Runtime baseline: Aspire reported no running apps. The isolated Platform file AppHost started; core resources reached Running while telemetry dependents remained Waiting. Scoped Aspire shutdown succeeded. The generic Builds seed-host baseline failed before launch with MSB4166; source validation used serialized Debug builds. This is baseline evidence only.

Delivery and cleanup follow local implementation/review: publish owner changes normally, update Platform pointers, verify remote ancestry, then retire only originals unchanged since verified snapshots and safely merged branches. Do not expand this task into completing historical package/hosted qualification gaps.


Coordinator baseline update: the isolated Platform AppHost started successfully through Aspire CLI on 2026-11-01, after the generic seed-host attempt failed with MSB4166. Runtime state inspection and scoped shutdown are owned by the coordinator.

Prior mapped-source command receipt: `/tmp/story33-platform-final-local-verification.json`. Its successful command builds `tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -m:1 --disable-build-servers -p:NuGetAudit=false -p:UseHexalithProjectReferences=true` with explicit `HexalithEventStoreRoot`, `HexalithBuildsRoot`, `HexalithTenantsRoot`, `HexalithMemoriesRoot`, and `HexalithPartiesRoot`. Adapt to fresh recovery Builds/Parties and canonical current SDK/Tenants/Memories. Propagate `HexalithSourceMappingMode=source` globally; do not replace source mapping with private package versions. Generate a supported local mapping through the existing CLI if the current build contract requires one.

Implementation disposition: the retained Parties Admin normalization would accept the native allow-default configuration and remove main’s explicit-private-profile regression. Current main already selects `accesscontrol.eventstore-admin.catalog.yaml`, keeps named grants, and refuses native allow-default input. Preserve this newer security boundary; retain the draft in the verified archive and make no Parties source commit.

Review disposition: the historical optional CatalogProof executable stays preserved in the verified archives rather than being published in this recovery; its package producer, cryptographic provenance and runtime identity qualification remain unfinished. Source-run planning provenance remains descriptive and gains no runtime acceptance claim.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| BH01 | medium | patch | Required v2 preparation reads activation.json, but the held input list omits it; cleanup therefore loses the exact validated activation bytes. |
| BH02 | medium | patch | The newly added standalone checker can be invoked on held bundles but checks no attempt/binding equality; its passed ordinary-package result is not justified. Preserve this unfinished prototype in backup instead of delivering it. |
| BH03 | false | reject | The Builds reader is a bounded custody layer, not the activation authority. Supported callers publish readiness only after PlatformNativeTestPreparation validates canonical catalog/readbacks and observes actual HTTP metadata; the offline empty-object executor control returns HXT002, never a passing runtime result. |
| BH04 | high | patch | ReadCore checks labels/patterns but never compares decoded fields with the actual signing, endpoint and access secrets already gathered by ExecuteSelectedAsync before launch; opaque known credentials can reach persistent archives. |
| BH05 | high | patch | ContainsCredentialField inspects only string values; nonempty arrays/objects under credential names bypass it. Preserve genuine request/result JSON Schema declarations while refusing credential containers. |
| BH06 | medium | patch | Complete activation checks only require a nonempty technical receiver list. A foreign receiver can satisfy that check without the independently resolved EventStore gateway membership; fixtures currently demonstrate this mismatch. |
| BH07 | medium | patch | The new complete-activation input gate rejects only final-file links, so a linked parent can supply foreign input to this newly validated evidence path. Check containment/parent links through the owned run workspace. |
| BH08 | medium | patch | The new optional standalone checker trusts an informational-version suffix rather than held published parser bytes. Its unqualified prototype will remain archived outside the delivered source change. |
| BH09 | medium | patch | Presence of .signature.p7s is not cryptographic verification, and no verifying producer is connected to the added checker. Archive the unqualified checker rather than ship a misleading pass result. |
| BH10 | medium | patch | The optional checker treats planned tags/project hashes as runtime identity. Preserve those fields as descriptive source-run metadata, but do not deliver the checker that promotes them to package proof. |
| BH11 | medium | patch | Current runtime admission refuses package catalog cohorts, so the optional package-only checker has no supported producer in this change. Archive it pending the separately tracked qualification work. |
| BH12 | medium | patch | The newly added checker lacks a valid-path automated gate. Removing that unqualified executable from delivery is the smallest correction; all original prototype bytes stay in verified archives. |
| EH01 | high | patch | Memories functional-retention is supported, but only Parties registers the required proof producer. Existing catalog directories with required=false currently demand Parties-only files and refuse supported children with HXT009. |
| EH02 | high | patch | Nonempty numeric/string arrays under credential names pass current retention checks; same concrete container-policy defect as BH05. |
| EH03 | medium | patch | The exact activation validated before readiness is absent from durable held snapshots; same retained-evidence omission as BH01. |
| VG01 | medium | patch | Preverified caller gap: executor tests manually set the requirement flag; readiness publication tests do not assert it survives producer serialization and reading. Extend the actual readiness test. |
| VG02 | medium | patch | Preverified caller gap: archive tests inject RoutingCatalogSnapshot independently. Exercise ExecuteAsync and prove pre-child routing bytes are forwarded to its actual archive destinations despite child mutation. |
| VG03 | medium | patch | Preverified caller gap: helper tests do not prove PrepareAsync invokes activation validation. Mutate an otherwise valid attempt/binding and assert refusal before source-validated or credential creation. |
| VG04 | medium | patch | Preverified caller gap: bootstrap environment tests omit BindingVersion and ReceiverProfile assertions. Extend the actual evaluated-environment control for all participants and the gateway. |

## Review Resolution

Three independent reviewers returned 19 findings. Triage recorded each finding before grouping; 18 were addressed in 11 patches and BH03 was rejected with caller evidence. There are no deferred findings in the delivered source scope.

- Optional native runs skip the required-proof capture even when a catalog directory exists.
- Required capture retains activation.json and rejects decoded known signing, endpoint and access credentials before launch; credential containers are rejected while schema declarations remain valid.
- Preparation requires owned input paths and the independently selected gateway technical receiver/profile.
- Readiness serialization, real execution-to-archive forwarding after child mutation, preparation-entry activation/readback refusal and actual bootstrap environment settings have regression coverage.
- The unfinished standalone package-proof executable is archive-only; original bytes and hashes remain recoverable.

Detailed receipts: backup root `review/triage-1.json`, `review-fix/manifest.json`, `review-fix/archive-only-manifest.json` and `review-fix/verification/results.json`.

## Verification

The coordinator independently passed 197 checks: Builds snapshot/executor/declaration closure 99; readiness/cancellation 6; Platform metadata/binding/preparation/proof/gateway seed 71; metadata/runtime-admission 9; mapped-owner bootstrap 1; and current Parties private-profile configuration 11. All passed without skips. Both owner Debug builds passed with zero warnings/errors. The implementation worker separately passed all 131 review-fix cases.

The unchanged public CatalogBuilder source controls previously passed 11 cases, including scoped gateways using older requests without optional provenance. Backup hashes, final file hashes, staged whitespace checks and exact owner commit-message candidates passed validation. The final source diff excludes the standalone prototype.

Builds main advanced while verification ran. The recovery commit rebased cleanly onto df13d9d; the first coupled build exposed an omitted `HexalithEventStoreFromSource` flag after the pin update. The complete root/FromSource command selects EventStore Contracts and DomainService as source projects only, retaining updated delivered pins. Corrected commands and final checks are recorded under `delivery/verification/ci-followup/results.json`. Exact commands, logs and XML results remain in the backup.

These checks establish source/structural and in-memory behavior. They do not qualify live topology, images, released packages, hosted acceptance or the historical Sample projection fixture. Earlier incomplete broad tests and discarded harness attempts remain recorded as non-authoritative. The historical routing-catalog story remains in progress.

Normal delivery requires Builds checks and a squash PR merge, followed by a separate Platform submodule-pointer commit. Platform requires one code-owner approval. No protection bypass is authorized. Delivery URLs and retirement receipts are recorded in the durable recovery manifest rather than embedding mutable PR state here.

Delivery integration checks: the CI package audit initially detected the duplicate seed compiler flag as a consumer declaration change. Reusing the existing, identically conditioned mapped-source flag removes that declaration diff, keeps the authoritative audit valid and avoids anchoring an audit to a feature commit lost by the required squash merge. Both seed project and audit bytes match main. The audit validator passes 314 packages, 154 families and one source.

Six Codacy findings prompted cohesive helper extractions; new Platform validator/test methods use the same limits. Targeted methods and new helpers meet NLOC ≤50 and cyclomatic complexity ≤15 without suppressions or policy changes. The original enumerated readback count is forwarded exactly, preserving Windows glob behavior. The stable ten-file manifest is `delivery/verification/ci-followup/source-manifest.json`; coordinator checks are recorded under `delivery/coordinator-final/`.

Upstream Builds advanced again to e9b74ab during delivery, adding production model common-props pinning and SDK cache/prevention controls. It merged cleanly into the already published recovery branch with a validated conventional message. Integration checks for the affected process/model paths are recorded under `delivery/upstream-integration/`; final delivery never force-pushes a default or bypasses required checks.
