# Gate summary — update run 2 (2026-09-28)

This run rolled the r2 validation (`reviews/validate-2026-09-27-r2`, verdict FAIL, 64 clusters) into the spine. It then passed the spine through a six-lens reviewer gate and one confirmation pass. Decisions and evidence are recorded in `.memlog.md`, in the entries after "(event) Update run 2 started 2026-09-28".

## Inputs settled

- Sprint change proposal 2026-09-27 (McpCli owns Hexalith module-operation CLI/MCP access): recorded as the authority for the AD-11 amendment. The proposal's scope limit is applied.
- r2 clusters:
  - 24 discuss clusters were settled in six coached batches. The user accepted every recommendation, including the parent's C-18 divergence (staging HTTP-01, no separate DNS zone).
  - C-23 was reframed after a live check showed that Traefik, not the retired ingress-nginx, serves the `nginx-public` class. It now adopts Gateway API on Traefik.
  - 38 autofix clusters were applied. C-63 is deferred and C-64 ignored.

## Reviewer gate

| Lens | Verdict | Findings | File |
| --- | --- | --- | --- |
| Rubric walker | Conditional pass | 0 critical / 5 high / 14 medium / 8 low | review-rubric.md |
| Reality and version | Conditional pass | 0 / 2 / 7 / 6 | review-reality.md |
| Adversarial | Fail | 0 / 9 / 10 / 2 | review-adversarial.md |
| r2 reconciliation | Conditional pass | 51 landed, 10 partial, 1 contradicted, 13 regressions | review-reconcile-r2.md |
| Input reconciliation | Conditional pass | 0 / 2 / 10 / 8; no PRD threshold weakened | review-reconcile-inputs.md |
| Pragmatism | Pass with trims | 32 findings; growth judged earned | review-pragmatism.md |
| Confirmation | Pass with fixes | 135 findings checked; 20 wording conflicts, all applied | review-confirm.md |

## User decisions at the gate (D1–D10)

- **D1:** Dapr skew is defined within a minor, with qualification records per environment that cover environment-layer and shared pins.
- **D2:** CI package mode needs no new artifact.
  - The Platform composition builds from the submodule at a release tag.
  - Dependency services run from released images.
  - The identity pins the Builds catalog and a tool-version range.
  - In the Platform repository, HEAD is the identity.
- **D3:** Node volumes are encrypted. TLS is used where the provider supports it natively, and plaintext Redis/FalkorDB is an accepted risk.
- **D4:** The user chose option 3. The second organization owner is a named writer of the operations and notification repositories, and the org base permission is set to read.
- **D5:** In-place recovery can be started by Administrator or the deputy on the production executor. The lost-window review moves to promotion-stop clearance.
- **D6:** EventStore provides the admission predicate and projection.
- **D7:** McpCli follow-ups: a run-scoped local build, Abstractions stays McpCli-published, and the first increment ships as prerelease.
- **D8:** The tenant-creation ban applies to production smoke only. Tenants owns the synthetic tenant.
- **D9:** The storage provisioner may create PersistentVolumes through an environment-specific StorageClass.
- **D10:** Every surviving authority has a named fence-and-reissue owner.

## Declined or deferred

- **Declined:**
  - PR-217: the secret-equivalence sentence is kept as C-20 decision content.
  - PR-228, PR-229.
  - PR-230, PR-231, PR-232: optional moves.
- **Deferred:**
  - RB-27: GitHub assignees. The mention-based wording is applied.
  - RV-15: Keycloak admin path detail, handled under the G1 row.
  - RI-20: folded into tool ratification.

## Result

- Status is `final`, updated 2026-09-28.
- All 15 ADs keep their IDs, and no new AD number was needed.
- The spine is 481 lines and about 14,300 words.
- `lint_spine.py` reports zero findings, local links resolve, and all four Mermaid diagrams parse with mermaid 11.
- The review was documentation-only. No runtime, cluster or repository settings were changed, and no Git commit was made.
