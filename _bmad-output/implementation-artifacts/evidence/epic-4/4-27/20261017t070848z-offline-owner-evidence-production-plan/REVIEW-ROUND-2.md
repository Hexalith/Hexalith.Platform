# Independent offline plan review — round 2

Scope: changed `PLAN-v2.md`, with round-one triage; read-only document review by the same three independent agents. No operations, tests, integrity/signature validation, owner searches or authority/activation claims.

## Original blind-hunter follow-up

1. **Unresolved — dependency cycle remains in the new stage table.** The preliminary sequence at lines 49–51 and 121 fixes the original A2/B1/B2 cycle. However, line 174 requires applicable **M** prerequisites before H0 approval, while line 194 classifies **actual runtime qualification** involving H1 as M. Lines 122 and 228 require H0 before actual H1 observations. Split preliminary runtime readiness from outcomes produced under H0, and identify which M requirements apply to each operation.
2. **Resolved — production and laboratory TLS cases are separated.** Lines 53 and 123 prohibit alternate production credentials and negative production handshakes while explicitly limiting laboratory evidence.
3. **Resolved — documentary and operational validity are separated.** Line 37 permits honestly dated post-expiry records without extending operational validity.
4. **Resolved — preservation completeness cannot pass unresolved drift.** Line 143 explicitly keeps unknown, deferred, rejected or acknowledge-only dispositions blocking.
5. **Resolved — authorization-model coverage is required.** Lines 76 and 83 require authenticated configuration, applicable decision mechanisms and privileged-identity rules, with unknown coverage stopping acceptance.
6. **Resolved — collection limits are scoped.** Line 232 confines the 64-item limit to HTTP sanitizer collections and defines separate bounded, complete journal handling without truncation.
7. **Resolved — all 31 inherited IDs have traceability rows.** Lines 162–200 supply source, responsible roles, artifacts, acceptance conditions and stages. I checked the IDs against the explicitly identified nonsecret `bindings-v4.json` blocker map; no hash validation was performed.
8. **Resolved — original signed bytes have an explicit preservation/index contract.** Lines 33 and 72 define original payload/signature storage, byte and signature mappings, delimiter treatment and complete index accounting.

The remaining material contradiction is the H0/H1/M dependency in finding 1; `REVIEW-ROUND-1.md` currently overstates its resolution.

## Original edge-case follow-up

Original H0/B2 cycle: **resolved**. PLAN-v2:51 and H0 now bind preliminary documentary records and B2’s reviewed delivery contract; actual H1/B2/H2/H3 evidence follows authorization.

Expiry and preservation boundaries: **resolved**. Line 37 permits honest documentary records after expiry without operational admissibility. Line 143 keeps unresolved/deferred drift blocking; line 230 preserves the expired candidate’s unreachable activation boundary.

One remaining stage contradiction: PLAN-v2:166 makes **M** prerequisites apply before any measurement, and line 174 places H0 after applicable M prerequisites. Yet line 194 classifies **actual runtime qualification** as M, while lines 51/228 place that qualification after H0. Split this row into preliminary M inputs and subsequent qualification outcomes, explicitly excluding those outcomes from their own H0 prerequisites.

This document review establishes no owner authority or activation approval.

## Original verification follow-up

No material verification gaps found in the four revised areas. All 31 blocker IDs match the explicit nonsecret source map; original-byte preservation, bounded indexing and preliminary-versus-actual acceptance are specified.

This was documentation review only. No tests, integrity validation, signer authority or operational readiness were established.

## Individual triage before grouping

| ID | Verdict | Route | Evidence and disposition |
| --- | --- | --- | --- |
| Blind follow-up 1 | medium | document correction | Verified: v2's new M label generalized all measurement prerequisites while the source-tool row combined preliminary inventory and actual runtime qualification. This contradicted H0-before-actual-H1. v3 distinguishes M0 documentary input/MQ authorized outcome, revises runtime/provider/SSH rows, treats authorization as a boundary rather than its own prerequisite, and enumerates the exact native-qualification M0 set and lane applicability. |
| Edge follow-up 1 | medium | document correction | Independently verifies the same v2 M/H0/H1 cycle; classified separately before grouping. Corrected by the same explicit phase split and native/provider/SSH/Docker lane boundaries in v3. |

The two surviving findings share one stage-classification cause and are grouped only after individual classification. Other round-one corrections are independently confirmed; no finding is dismissed/deferred. Round-one's claimed dependency resolution is superseded by this truthful follow-up; the original review and v1/v2 remain unchanged. No new operational readiness is claimed. Final targeted review of v3 covers this correction only; unchanged validation is not repeated.
