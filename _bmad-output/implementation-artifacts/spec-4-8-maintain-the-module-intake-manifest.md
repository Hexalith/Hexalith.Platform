---
title: '4.8 Maintain the module intake manifest'
type: 'feature'
created: '2026-11-24'
status: 'in-progress'
baseline_commit: 'c6a573f95589a9c07d1838e2692e493696c470db'
route: 'dispatch'
review_loop_iteration: 0
story_key: '4-8-maintain-the-module-intake-manifest'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Platform has no record of which module releases a candidate contains, nothing verifies an owner image's attestation before publication, and no code computes a candidate's change classification. Story 4.12 cannot write a release record without all three.

**Approach:** Add a versioned intake manifest and a stdlib Python tool. The manifest pins each module release's package and image digests, expected attestation identity, release-evidence references and declared change class. The tool validates the manifest, admits images only through verified attestations, and computes the effective classification over the chain from a named working baseline.

## Boundaries & Constraints

**Always:**
- Fail closed. A missing `gh`, an unreachable API or registry, a timeout, no attestation, or any identity field that differs from the manifest refuses admission. A passing vulnerability scan never substitutes.
- Admission checks the attestation subject digest, the issuer `https://token.actions.githubusercontent.com`, a GitHub-hosted runner, and these certificate fields: source repository, Build Config URI (module workflow), source ref (protected ref) and Build Signer URI and digest (pinned Builds workflow). It re-checks the `gh --format json` certificate summary itself rather than trusting flags alone.
- Breaking has one definition (spine Module intake). A release's class is its owner's v2 declaration `lifecycle.classification.changeClass`, recorded with the declaration digest. Effective class is `breaking > additive > none` over every release after the baseline through the candidate. The manifest records the chain and names the baseline. With no working baseline, the chain starts at each module's first entry and the baseline reads `none`.
- Entries per module are append-only and version-ordered. `--check` refuses a manifest whose computed chain or effective class differs from the stored one.

**Decisions (Administrator, 2026-11-24):** Platform slice only. Seed EventStore 5.4.0, Tenants 8.0.4, Memories 4.1.0 and Parties 4.0.1 with real package and image digests and `attestation: null`, so live admission refuses them. Prove the verifier on a real public attestation. Builds image attestation and owner adoption are deferred, and 4.8 stays in progress until a real attested module image is admitted. Map classifications for the release record in the contract (`none→unchanged`, `additive→compatible`, `breaking→breaking`). Builds is not changed. Keep the spec whole.

**Never:** Build the publication workflow or write release records (4.12). Scan images (5.5). Read the attempt store. Change Builds or owner repositories. Hardcode identities outside the manifest.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Error handling |
| --- | --- | --- | --- |
| Admit attested image | Attestation names the recorded repository, workflow, ref and Builds ref | Admitted; JSON receipt with subject digest and certificate fields | — |
| Missing attestation | No bundle for the digest | Refused before any record | `attestation-missing` |
| Mismatched identity | Other repository, workflow, ref, Builds SHA, or self-hosted runner | Refused | `attestation-mismatch` naming the field |
| Tool failure | `gh` absent, too old, timeout, non-JSON | Refused | `attestation-unverified` |
| Chain | Baseline EventStore 5.4.0; entries 5.5.0 additive, 5.6.0 breaking | Effective `breaking`; chain lists both | — |
| Out-of-order or edited entry | Version lower than its predecessor, or chain drift | Validation fails | `--check` names the entry |

</frozen-after-approval>

## Code Map

- `eng/owner_packages_lock.py`, `eng/test_owner_packages_lock.py` -- closest analogue: docstring argparse, `--check`, exact key-set validation, `sys.exit(str(failure))`; reuse the style, not the module.
- `eng/owner-packages.lock.json` -- versions Platform consumes now: EventStore 5.4.0, Tenants 8.0.4, Memories 4.1.0, Parties 4.0.1.
- `src/Hexalith.Platform.Composition/Declarations/{eventstore,tenants,memories}.json` and `provenance.json` -- v2 declarations with `lifecycle.classification` and their source sha256. Parties' declaration is `references/Hexalith.Parties/module/hexalith.module-manifest.v2.json`.
- `references/Hexalith.Builds/schemas/hexalith.module-manifest.v2.json:1242-1262` -- `changeClass` enum.
- `references/Hexalith.Builds/schemas/hexalith.release-records.v1.json:258-270,344-359`, `ReleaseRecord.cs:31,42` -- `moduleArtifactDigests` (sha256 values) and `moduleChangeClassifications`, the 4.12 consumer; do not modify.
- `references/Hexalith.Builds/Github/split-publication/attestation.py:212-236` -- certificate OIDs Builds already checks; mirror the field set.
- `.github/workflows/tuple-ci.yml:125-131` -- add the tests and `--check` beside the owner-lock step.
- Images per release come from each owner's container list (EventStore `eventstore`; Memories `tools/release-containers.json`; Parties and Tenants `release.yml`). `docker buildx imagetools inspect` resolves index digests.

## Tasks & Acceptance

**Execution:**
- [x] `eng/module_intake.py` -- validate, `add` (resolve digests, read the declaration class), `classify --baseline`, `admit` (via `gh attestation verify oci://…`), `--check`.
- [x] `eng/module-intake.json` -- schema `hexalith.platform.module-intake.v1`, seeded with the four current releases and their declared classes.
- [x] `eng/test_module_intake.py` -- every matrix row with a stubbed `gh`, plus schema and ordering refusals.
- [x] `docs/contracts/module-intake-v1.md` -- fields, identity rule, classification rule, refusal codes, the 4.12 handoff and the vocabulary decision.
- [x] `.github/workflows/tuple-ci.yml` -- run the tests and `--check`.
- [x] `_bmad-output/implementation-artifacts/evidence/story-4-8/` -- a short summary of the live admission runs.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- resolve the 4.6 vocabulary deferral with the mapping. Add owner entries: Builds image attestation, then adoption by EventStore, Memories, Parties and Tenants.

**Acceptance Criteria:**
- Given the seeded manifest, when `admit` runs live, then each unattested module image is refused `attestation-missing`, and a real attestation passes only with its exact identity.

## Implementation Notes

- The last release per module selects the candidate. A named working baseline stores its exact module versions in `baselineVersions`. Modules first introduced after that baseline contribute their full chains.
- Tuple CI checks append-only history during source resolution. Pull requests use the target base SHA, pushes use the previous SHA, and manual runs use the candidate's first parent.
- Live verification admitted the public SHA-pinned reusable-workflow fixture, refused four altered expectations, and returned `attestation-missing` for all 11 seeded module indexes. The [live evidence](evidence/story-4-8/admission.md) records the results. The story remains in progress pending the first successful Hexalith module admission.

## Spec Change Log

## Review Triage Log

| Layer and finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| Blind review, dispatch history comparison | high | patch | Dispatch checked the selected candidate against its own HEAD. Compare with its first parent. |
| Blind review, Builds prerequisite | medium | patch | The documented Builds tag mismatch blocks the tuple job before the intake step. Intake uses the Platform checkout alone and can run in source resolution before the Builds check. |
| Blind review, baseline version binding | high | patch | The same baseline ID with different versions could remove breaking entries after reclassification. Apply the current-baseline binding during history checks. |
| Blind review, release input binding during add | medium | defer | Selection of the owner release commit, published package IDs and image tag is a reviewed input. The tool resolves identities at those coordinates. Automation for authenticating their joint owner publication belongs to owner release evidence and the publication workflow. |
| Blind review, declaration authenticity during check | false | reject | The reported acceptance is for an append-only schema and chain check. The frozen scope assigns owner declaration retrieval to `add` and image provenance verification to `admit`. An appended entry passing schema validation gains no admission or publication authority. |
| Blind review, live issuer, repository and subject diagnostics | medium | patch | GitHub CLI can report verified issuer and repository policy failures before producing JSON. Map its explicit policy diagnostics and retain unverified when a subject failure prevents cryptographic verification. |
| Blind review, attestation endpoint access | medium | patch | Public metadata alone could not confirm what the authenticated attestation lookup did. Confirm absence with the public attestation endpoint too. |
| Blind review, default bundle search limit | low | reject | GitHub CLI fetches up to 30 by default. An older exact match could be omitted after 30 same-digest attestations. Raising a finite cap shifts the limit and a separate paginated verification path adds complexity for uncommon repeated attestations. |
| Blind review, protected ref and workflow authority | false | reject | The frozen identity rule compares against the approved canonical manifest. The system verifies the certificate, while Platform review establishes which protected ref and pinned workflow the manifest authorizes. The contract now makes that responsibility explicit. |
| Blind review, intake handoff keys and snapshot | medium | patch | The release-record schema leaves digest-map keys to the producer and supplies an evidence digest map. Specify stable keys and the retained intake snapshot digest in the contract. |
| Edge-case review, reused baseline ID | high | patch | The stored chain could drop a breaking entry after rebinding the same ID. The history check needs the same guard as `classify`. |
| Edge-case review, dispatch history comparison | high | patch | The fallback passed the checked-out candidate as both sides of the comparison. Use its first parent for dispatch. |
| Edge-case review, canonical symlink | medium | patch | Resolving the manifest before testing the default path can turn a symlinked canonical file into a standalone path. Determine the canonical argument before resolving symlinks. |
| Edge-case review, default bundle search limit | low | reject | The lookup cap can omit an older match after repeated attestations for one digest. Full pagination would need a separate verification path and any finite cap has the same boundary. |
| Edge-case review, issuer and repository prefilter | medium | patch | GitHub CLI emits explicit certificate policy diagnostics for these failures without a JSON result. Preserve the corresponding mismatch field. |
| Edge-case review, malformed later record | medium | patch | An early receipt could bypass malformed trailing records. Validate the full result list before returning a receipt. |
| Edge-case review, protected tag containing an at sign | low | reject | The accepted owner refs use simple branch and version-tag syntax. Supporting the rest of Git's ref grammar would require more validation than widening the regex. |
| Verification-gap review, explicit history revision | medium | patch | A mutation ignoring `--against` passed the original suite. Exercise the CLI against an earlier commit when HEAD already contains the edit. |
| Verification-gap review, exact release and image selection | high | patch | Two mutations that selected the first release or image both passed. Test selection among distinct releases and images. |
| Verification-gap review, candidate-only module classification | high | patch | Dropping modules absent from a named baseline passed. Exercise a breaking release from a new module. |
| Verification-gap review, package version and image tag | medium | patch | Using the image tag for the NuGet request passed. Check distinct coordinates in the artifact test. |
| Verification-gap review, OCI index constraint | medium | patch | Accepting single-platform manifests passed. Test index media-type refusal and an unchanged manifest. |
| Verification-gap review, candidate-only CI test regression | high | patch | The existing source-release suite rejects general Git fetch commands. Allow only the guarded exact-SHA comparison fetch and preserve the shallow candidate-only checkouts. |

## Design Notes

```json
{"schema":"hexalith.platform.module-intake.v1",
 "modules":{"eventstore":{"repository":"Hexalith/Hexalith.EventStore","releases":[{"version":"5.4.0","commit":"…",
   "packages":[{"id":"Hexalith.EventStore.Server","sha256":"…"}],"images":[{"name":"registry.hexalith.com/eventstore","digest":"sha256:…"}],
   "attestation":{"workflow":".github/workflows/release.yml","ref":"refs/heads/main","buildsWorkflow":"Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml","buildsSha":"…"},
   "evidence":["https://github.com/…/releases/tag/v5.4.0"],"classification":{"changeClass":"additive","declarationSha256":"…"}}]}},
 "classification":{"workingBaseline":"none","chain":[{"module":"eventstore","version":"5.4.0","changeClass":"additive"}],"effective":"additive"}}
```

## Verification

**Commands:**
- `run-with-temp python3 -B eng/test_module_intake.py` -- 25 passed.
- `python3 -B eng/module_intake.py --check` and `validate` -- both passed.
- `run-with-temp npm run test:source-release` -- 85 passed.
- `actionlint .github/workflows/tuple-ci.yml` and `git diff --check` -- both passed.
