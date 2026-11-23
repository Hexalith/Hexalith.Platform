---
title: '4.8 Maintain the module intake manifest'
type: 'feature'
created: '2026-11-24'
status: 'ready-for-dev'
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
- [ ] `eng/module_intake.py` -- validate, `add` (resolve digests, read the declaration class), `classify --baseline`, `admit` (via `gh attestation verify oci://…`), `--check`.
- [ ] `eng/module-intake.json` -- schema `hexalith.platform.module-intake.v1`, seeded with the four current releases and their declared classes.
- [ ] `eng/test_module_intake.py` -- every matrix row with a stubbed `gh`, plus schema and ordering refusals.
- [ ] `docs/contracts/module-intake-v1.md` -- fields, identity rule, classification rule, refusal codes, the 4.12 handoff and the vocabulary decision.
- [ ] `.github/workflows/tuple-ci.yml` -- run the tests and `--check`.
- [ ] `_bmad-output/implementation-artifacts/evidence/story-4-8/` -- a short summary of the live admission runs.
- [ ] `_bmad-output/implementation-artifacts/deferred-work.md` -- resolve the 4.6 vocabulary deferral with the mapping. Add owner entries: Builds image attestation, then adoption by EventStore, Memories, Parties and Tenants.

**Acceptance Criteria:**
- Given the seeded manifest, when `admit` runs live, then each unattested module image is refused `attestation-missing`, and a real attestation passes only with its exact identity.

## Implementation Notes

## Spec Change Log

## Review Triage Log

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
- `python3 -B eng/test_module_intake.py` -- expected: all pass.
- `python3 -B eng/module_intake.py --check` -- expected: exit 0.
- `git diff --check` -- expected: clean.
