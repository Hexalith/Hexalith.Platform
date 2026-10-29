---
title: 'Story 4.15: Offline staging OpenBao and tenant-key preparation'
type: 'feature'
created: '2026-10-29'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'c4d68ed739977c5e24e5c91e4bfd601e2a44dcd8'
context: []
---

<frozen-after-approval reason="human-owned intent — explicit offline preparation instruction">

## Intent

Prepare Story 4.15 offline: reviewable definitions and a blocked approval package for a staging-specific OpenBao and separate tenant-key store, excluding tenant keys from ordinary OpenBao snapshots. Define per-app Dapr token delivery, rotation with pod restart, renewal ownership, required-generation readiness, and an evidence-only classification/replacement plan for the existing shared bootstrap token.

## Boundaries & Constraints

**Always:** Work only in this scratch worktree. Foundation artifacts must be module-agnostic. Record unsupported operational inputs as null with exact owner questions and evidence for every resolved value. Distinguish preparation from live acceptance. Preserve all existing evidence, statuses, edits and gitlinks. The parent integrates a local commit by fast-forward, without pushing. Story 4.14 remains in-progress.

**Never:** Network or cluster calls, API dry-runs, Helm execution, installation, credential issuance, key generation, unsealing, secret writes, recovery operations, secret material, guessed versions/digests/ports/identities/custodians/storage/operational settings, or consumer-specific definitions/gates. No executable operational dispatcher.

## I/O & Edge-Case Matrix

| Scenario | Input | Expected behavior | Error handling |
|---|---|---|---|
| Current resolution | Cited facts plus null choices | Enumerate every missing input; no deployable output | Exit 2, blocked |
| Invalid input | Wrong type, unknown fields, placeholder, contradictory store boundary | Explain invalid fields | Nonzero, no output artifacts |
| Backup boundary | Tenant store shares ordinary snapshot state | Refuse logical-path-only exclusions | Nonzero |
| Readiness | Required generation absent/stale, wrong environment/app/contract, duplicate or malformed evidence | Refuse readiness | Nonzero |
| Complete synthetic fixture | Explicit test-only inputs and matching generation observations | Deterministic offline assessment, still unapproved | Never live acceptance |

</frozen-after-approval>

## Code Map

- `eng/staging-foundation/{APPROVAL-PACKAGE.md,inputs.resolved.json,MISSING-INPUTS.md,DEPENDENCY-DIRECTION.md}`: prerequisite evidence, inherited missing inputs and one-way dependency. Read first; keep unchanged.
- `_bmad-output/planning-artifacts/epics.md:2311`: Story 4.15 criteria. Architecture at `architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md` under planning-artifacts: AD-7/8 identities, AD-12 independent custody/manual unseal; Secrets, Release tiers, recovery and Owned work rows. Existing pins/observations are not staging selections.
- `eng/staging-foundation/render.py`: stdlib/offline style only; do not reuse its operational plan as authorization.
- `evidence/epic-4/4-14/live-apply-approval-2026-10-29/native-observation.json` under implementation-artifacts: sanitized recorded observations, no new collection.
- `deploy/dapr/openbao-secret-contract.yaml` is cited by architecture but absent: record the missing contract instance, do not invent app IDs or generations.

## Tasks & Acceptance

**Execution:**
- [x] `eng/staging-secrets/inputs.schema.json`, `inputs.resolved.json`, `MISSING-INPUTS.md`: typed, closed input schema, cited resolution, owner role and exact question for every null including nested app requirements; aggregate topology choices may be structured objects with documented required members. No secret values accepted. Cover provider/tool pins, storage/resources/replicas/security compatibility, TLS endpoints/selectors/ports and DNS/network dependencies, environment identity/RBAC, custody/unseal/coverage/monitoring, backup classes/access/retention/erasure, app identities/scopes/policy/mounts/token lifetimes/renewal/restart and secret contract generations, existing token inventory/cutover/revocation, verification/recovery authority. Resolve only what sources establish.
- [x] `eng/staging-secrets/definitions.json`: value-free parameterized OpenBao/store boundary, snapshot membership, per-app `secretstores.hashicorp.vault` Component and pod mount contract (`vaultTokenMountPath`, `dapr.io/volume-mounts`), ownership and rotation sequence. Nulls are non-deployable definitions. Separate physical storage/snapshot membership; a KV path exclusion cannot filter a whole-instance snapshot. Do not select an unsupported backend or fabricate Kubernetes resources.
- [x] `eng/staging-secrets/prepare.py`: stdlib-only offline assessor and metadata readiness checker; enumerate missing/invalid inputs, validate typed structures and boundary invariants. No subprocess/network imports or live adapter. Never emit approval or mutation commands, even with complete fixtures. Readiness binds environment, app, contract digest and exact required generations; observations contain no secret bytes. Explicitly describe this as a proposed contract, not deployed app readiness.
- [x] `eng/staging-secrets/{APPROVAL-PACKAGE.md,INPUT-EVIDENCE.md,BOOTSTRAP-TOKEN.md,VERIFICATION-RECOVERY.md}`: blocked package with source paths/lines or JSON pointers and hashes, limits of historical observations, no unproven policy/renewability/root/compromise claims. Plan per-app replacement before historical expiry, owner renewal outside Dapr, rotation delivery then pod/sidecar restart and acknowledgment before old-token revocation. Describe native TLS/CA verification, restricted pod qualification, least privilege and complete isolation checks. Backup/restore procedures distinguish ordinary secrets and separate key custody, preserve erasure/lineage, forbid credential rewind and erased-key resurrection. No operation authorized now. Enumerate the sequence after accepted 4.14 live apply, exact-byte owner approval and future verification evidence required for 4.15 acceptance.
- [x] `eng/staging-secrets/test_prepare.py`: meaningful offline tests for every matrix row, full missing question coverage, provenance hashes/pointers, generation failures, token and snapshot invariants, all new foundation files module independent and no process/network effects. Fixtures clearly synthetic and not adopted defaults.
- [x] `_bmad-output/implementation-artifacts/evidence/epic-4/4-15/offline-preparation-2026-10-29/`: retain offline reports/test receipts, prepared-versus-live flags and source identity. Parent records integration preservation.

**Acceptance Criteria:**
- Given incomplete operations evidence, when assessed, then all unknowns remain blocked with exact questions and no live capability is claimed.
- Given the definitions, when inspected, then the store is outside ordinary snapshot membership and app tokens have external renewal/restart/readiness contracts.
- Given offline verification, when run, then matrix tests pass without contacting any external system.

## Implementation Notes

No intent gaps or irreversible effects in preparation. Unknown live choices are requested outputs, not implementation assumptions. Preserve sprint statuses under the user's explicit instruction; only this scoped preparation spec may finish. Parent handles integration and review. User authorization to complete the offline work supplies the preparation checkpoint; no live approval is inferred.

## Spec Change Log

## Review Triage Log

All three independent review layers completed before triage. Findings concern offline metadata consistency or clarity; they authorize no operation. Each requested correction is bounded to existing assessment inputs, definitions or tests. Replica coverage and selector/policy evaluation stay explicit future live-verifier requirements, with exact owner questions, rather than being claimed by supplied metadata.

| Finding | Verdict | Route | Evidence / required correction |
| --- | --- | --- | --- |
| B1 synthetic-source provenance laundering | medium | patch | Recorded mode accepted pointers into an explicitly synthetic fixture. Reject those sources; do not claim provenance authenticity. |
| B2 replica coverage and reused pod identity | medium | patch | One app record cannot prove every replica restarted; duplicate app IDs reject other pod records, while shared podUid passed. Reject pod reuse and clearly scope checker to one supplied observation per app; complete inventory/coverage remains an owner-evidenced future live check. |
| B3 omitted TLS port | medium | patch | Only explicit URL ports were compared. Require explicit matching owner-supplied endpoint port. |
| B4 identical store service destination | medium | patch | Different state IDs did not reject identical TLS endpoint/namespace/selector. Reject that direct boundary contradiction. |
| B5 administrative wildcard paths | medium | patch | sys* and auth* bypassed slash-only guard. Reject those wildcard roots without claiming a full policy evaluator. |
| B6 provenance-key diagnostic disclosure | medium | patch | Dynamic source keys entered schema diagnostic paths. Withhold those entry names. |
| B7 impossible calendar dates | medium | patch | Regex and lexical comparison admitted 2027-02-31. Parse calendar dates. |
| B8 contradictory resource quantities | medium | patch | CPU request 10/limit 1 and memory analogues passed. Validate supported quantities and request/limit ordering; document limits of the checker. |
| B9 CA/server-name delivery bindings | medium | patch | Proposal pointed to qualification evidence without explicit metadata wiring. Bind TLS server name, verification and exclusive CA delivery to owner-supplied evidence; retain unknown mode and paths. |
| B10 dependency reference and coverage claims | medium | patch | String identities do not parse selectors or prove complete coverage. Define reviewed-definition references and exact owner coverage/non-applicability evidence; state limits of offline assessment. |
| B11 historical Secret/key alias | medium | patch | Historical-name/token bypassed raw-name exclusion. Canonical Secret-name format plus separate sourceKey closes the demonstrated alias. |
| B12 invalid volume/key names | medium | patch | bad:name volume and bad/key source key passed. Validate their supported Kubernetes grammar. |
| E1 dynamic provenance-key disclosure | medium | patch | Same reproduced diagnostic disclosure as B6; retained separately as required by workflow. |
| E2 omitted endpoint port | medium | patch | Same reproduced endpoint/listener mismatch as B3. |
| E3 invalid calendar deadline | medium | patch | Same impossible-date reproduction as B7. |
| V1 readiness CLI route untested | medium | patch | Existing tests called readiness directly; add matching/stale CLI observations to guard routing and empty stdout on refusal. |
| V2 negative line provenance absent | medium | patch | Existing negative cases covered pointer provenance only. Add changed value and nonmatching valid line ranges. |
| V3 omitted endpoint port | medium | patch | Same reproduced endpoint/listener mismatch as B3. |

## Verification

- `python3 -B -m unittest discover -s eng/staging-secrets -v`
- `python3 -B -m unittest discover -s eng/staging-foundation -v`
- `python3 -B eng/staging-secrets/prepare.py assess --inputs eng/staging-secrets/inputs.resolved.json` must refuse with exit 2.
- `git diff --check`; preserve all prior status/evidence/gitlinks, no push.

## Preparation audit

The user authorized complete offline preparation, so unknown live choices are outputs with owner questions. No planning or operational approval is inferred from this scoped spec. The three independent review reports produced 18 entries, grouped into bounded corrections; no existing issue or live operation was deferred by this preparation. The future live-verifier requirements remain blocked by design, and are not acceptance achieved here.

The initial scoped suite passed 37 tests and the unchanged foundation suite passed 34. Final receipts supersede the initial scoped results after review corrections. All previously tracked files, including sprint status, existing evidence, foundation definitions and gitlinks, remain outside the change set. Parent integration checks the latest main state because unrelated security work continues concurrently.

Final verification: 48 staging-secrets tests pass; all 34 previously passing foundation tests retain identical source bytes. Current resolution refuses with 43 exact null questions, empty stdout and exit 2. Synthetic readiness remains unapproved. All 18 review entries were corrected or clarified within the proposed metadata contract; no live acceptance is claimed.
