# Update gate — 2026-09-27

**Verdict: PASS for architecture handoff after applied fixes.** Architecture approval is not runtime or production qualification.

## Scope

This update rolled in the 2026-09-27 validation, which had returned FAIL:

- 67 clusters in total.
- 24 were discuss items. They were settled with the user across five themes.
- 41 were autofixes, and 2 were defers.

The update adds three ADs: AD-13 (composed EventStore host), AD-14 (the authenticated client is the calling surface) and AD-15 (expand/contract rollback sets). It amends AD-1, AD-3, AD-7, AD-9 and AD-12 in place. AD IDs are stable.

## Gate lenses

| Lens | Initial verdict | Report |
| --- | --- | --- |
| Findings reconciliation (V-01…V-67 against the memlog) | 54 landed, 13 partial, 0 missing | review-reconcile.md |
| Rubric walker | Conditional pass: 3 high, 15 medium, 11 low | review-rubric.md |
| Pragmatism and bloat | Pass with trims: 38 findings | review-pragmatism.md |
| Reality and version (configured) | FAIL on RV-1, the GitHub Free plan | review-reality.md |
| Adversarial divergence (configured) | FAIL: 10 high | review-adversarial.md |
| Confirmation of applied fixes | 122 addressed, 5 deferred or declined, 2 missed plus 12 wording contradictions, all then fixed | review-confirm.md |

## Decisions the gate surfaced

The user accepted the recommendation in each case:

- **RV-1:** stay on GitHub Free and enforce controls at the executor or target: an Administrator-only private operations repository, executor allowlists, and OIDC or executor-held credentials. Build and attest in the public repositories. Revisit GitHub Team once there is a second writer.
- **RV-7 / R-10:** an off-site monitor runs the probe (5 minutes or less), the freshness check (15 minutes or less) and the stale-attempt check. An hourly GitHub dead-man check watches the monitor.
- **ADV-U8:** Keycloak standard token exchange for synchronous cross-module steps. For asynchronous task steps, EventStore attests the original actor.
- **Bundle:**
  - a per-environment data namespace
  - automatic and Administrator-approved release modes
  - the Kubernetes 1.34 staging window after end of life, accepted until G1
  - confidential clients for UI-only surfaces

## Declined

- **PR-01**, which proposed dropping the restated PRD thresholds. The earlier PRD reconciliation required that exact wording in the spine.
- **PR-37 and PR-38.** The reviewer recommended keeping these.

## Deferred with owners

- **RV-13:** executor runner update monitoring.
- **RV-14:** split the shared Memories OpenBao token and renew it before 2027-07-19.
- **EventStore confirmations:** AD-15 retention entries, catalog activation order, the composed-subject issuer, and renewed production-promoted records.

## Checks

- `lint_spine.py`: zero findings.
- All local Markdown links resolve.
- All three Mermaid diagrams parse with mermaid 11.
- No source, runtime, deployment, cluster or Git state was changed.
