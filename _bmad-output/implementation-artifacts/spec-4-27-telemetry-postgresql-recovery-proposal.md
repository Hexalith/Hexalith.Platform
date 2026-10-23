---
title: '4.27: Include telemetry PostgreSQL in the recovery proposal'
type: 'feature'
epic: 4
story: 27
created: '2026-10-23'
status: 'in-progress'
baseline_commit: '1516a6be0e3b9500cbcaeb14da1c0101f88caafa'
route: 'dispatch'
review_loop_iteration: 2
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Current Memories includes persisted access-telemetry PostgreSQL beyond the historical Redis/FalkorDB pair. The owner explicitly chose “Include telemetry PostgreSQL recovery in the proposal (Recommended)” in this session. Old paired-store completeness cannot cover this dataset.

**Approach:** Prepare a deterministic offline adapter that renders exact PostgreSQL 18 capture, isolated restore, source-resume and target-cleanup command proposals. Integrate its current inputs and hashes into the parent's single private review bundle. No recovery execution is authorized.

## Boundaries & Constraints

**Always:** Preserve historical/signed/frozen/bound bytes. Keep existing destination/principal/key/roles and maintenance decisions within scope. Bind source UID/image/container/PVC/PV, database/user, measured absolute tool hashes and proposed isolated targets. Keep raw metadata and all source/custody details private. Treat generated commands as unexecuted proposals. All missing actual policy/readback/snapshot/target/capacity/fence and runtime outcomes remain null and blocked. 4.27 remains in-progress, 4.1 closed and Timesheets 3.6 open/FAIL.

**Never:** Run a database dump, exported snapshot, capture, pause, restore, signing, upload, permission change, retirement, upgrade or deployment. Do not copy source passwords, use ambient auth, restore into the source, invent a runtime snapshot/target UID, infer coverage from old proofs or build a live dispatcher.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Current proposal | Exact source metadata, tools and owner scope | Concrete argv/templates/manifests and null run-dependent bindings | No I/O except bounded exact local input/output |
| Unsafe binding | Endpoint/tool/image/name/UID drift, source-target overlap, injection, missing table/tool | Refuse before render | Sanitized fixed failure, no secret/raw input echo |
| Capture plan | Proposed exported snapshot transaction and pg_dump | Peer-only explicit socket/user/DB/env, custom archive, exact snapshot placeholder null until separately bound | No capture command execution; snapshot holder lifetime/canary agreement remain qualification gates |
| Restore/resume/cleanup plan | New isolated PG18 target, source worker prior replicas | Exact pg_restore argv, guarded replica patch and owned-UID cleanup shape | Unknown target/fence/readback/drain keeps blocked; no source storage writes |

</frozen-after-approval>

## Code Map

- `eng/recovery-preparation/telemetry_postgres_adapter.py` and `test_telemetry_postgres_adapter.py` (new) -- offline source-bound planner and adversarial tests. Reuse sibling adapter's custody/JSON/hash primitives by compiling exact sibling source bytes; never import historical helpers.
- `eng/recovery-preparation/TELEMETRY-POSTGRES.md` (new) -- schema, proposed algorithm and actual qualification limits; no source credential locations or concrete sensitive source values in public docs.
- `/home/administrator/hexalith-retirement-investigation-evidence/20261023t085529z-s427-recovery-command-preparation/telemetry-postgresql-observation-v2/observation.json` -- actual StatefulSet/pod/configuration projection. Its preceding local observer failure is retained and made zero remote requests.
- `/home/administrator/hexalith-retirement-investigation-evidence/20261023t085529z-s427-telemetry-metadata/metadata.private.json` -- actual peer role/database, two source tables, roles/extensions and config hashes; no row content/passwords.
- `/home/administrator/hexalith-retirement-investigation-evidence/20261023t085529z-s427-native-observation/resolved-minimal.private.json` and `tool-hashes/tools.private.json` -- exact protected storage, source worker replicas and measured pg_dump/pg_restore/psql/image identities.
- PostgreSQL 18 primary references: `https://www.postgresql.org/docs/18/app-pgdump.html`, `https://www.postgresql.org/docs/18/app-pgrestore.html`, `https://www.postgresql.org/docs/18/functions-admin.html#FUNCTIONS-SNAPSHOT-SYNCHRONIZATION`.

## Tasks & Acceptance

**Execution:**
- [ ] New planner -- closed JSON schema with source cluster/ns/StatefulSet/pod/container/image/PVC/PV/tools/database/user/tables/extensions/roles, paused telemetry worker with recorded replicas, proposed target namespace and exact limits. Use actual private metadata to design it; synthetic values only in tests. Parent constructs the private actual input. CLI accepts absolute 0600 exact-hash JSON, emits neutral summary and optional create-only private plan, no backend/subprocess/socket/credential loader. Validate PostgreSQL identifier and snapshot-ID values safely; no shell interpolation or connection-string override.
- [ ] New planner -- generate explicit peer-only `env -i` psql/pg_dump/pg_restore argv using exact absolute binaries, Unix socket, user/database, PGPASSFILE/PGSERVICEFILE=/dev/null and no prompt. Future capture holds one REPEATABLE READ READ ONLY exported-snapshot transaction while same-snapshot source canaries and pg_dump custom archive complete. Dump snapshot ID is null in the proposal; provide a separate pure constructor validating an actual runtime snapshot binding before returning executable argv. No --clean/--create/source overwrite. Restore stdin must be a verified/decrypted exact immutable artifact, with separate checks for new target UID/image/PVC and empty schema. Two known source tables need schema/data-count/sequence/constraint/extension/ACL canaries; a whole-DB dump includes both schemas. Source roles/ACLs must be recorded without passwords; the target runtime role is proposed NOLOGIN to preserve isolation rather than inherit source activation authority, with that deliberate difference disclosed.
- [ ] New planner -- propose isolated PG18 resources (new namespace, default-deny NetworkPolicy, new Delete-class PVC, nonroot StatefulSet, no Service/Ingress, no host/source mounts, no service-account token). Target bootstrap credentials are fresh run-owned material, null until supplied; no source password copy. Exact image is the source digest; UID/fsGroup/storage/startup assumptions are proposed inputs, not qualified facts. Plan guarded pause/resume of only the source telemetry consumer (observed prior replicas 1), preserve the already-zero clock, and integrate with parent MCP/paired-store resume prerequisites. Clean drain, paired restore and PG restore must all pass before source resume. Cleanup binds future owned UIDs/RVs/paths and creation fence; unknowns remain null. Transfer uses approved memories/ prefix and independent validator exact versions; no writer upload/abort implementation is claimed.
- [ ] Tests -- cover all matrix rows plus command injection/connection string/ref alias, wrong image/tool hash, source-target storage overlap, missing required tables, snapshot binding/no snapshot execution, target missing UID/empty-schema gate, safe restore flags, no password env/file reads, patch preconditions/prior counts, null fence/artifact/run outcomes and CLI refusal before live access. No actual PG process/cluster/provider call.
- [ ] Docs -- distinguish offline construction/tests, current read-only peer metadata success and all actual snapshot/dump/stdin/restore/capacity/fence/signature/acceptance qualifications. Full orchestration and proof-consumer updates remain explicitly required before an operational bundle; never imply this planner implements those drivers.

**Acceptance Criteria:**
- Given the explicit owner scope choice, when the final private bundle is assembled, then telemetry PostgreSQL is included with exact actual inputs/tool hashes and proposed recovery procedures, without broadening current execution authorization.
- Given synthetic or actual local proposal input, when the planner renders, then it makes zero backend calls, preserves missing runtime values as null, and cannot produce a source-overwriting restore command.

## Implementation Notes

This is the sequential second implementation dispatch, after the common adapter review revision finishes. Parent owns private current input construction, packet integration and tracking; implementation dispatch edits only the three new telemetry files. Original Story 4.27 intent/status and all other work remain untouched.

## Spec Change Log

2026-10-23 integrated review — after the common loop2 dispatch finishes, re-derive telemetry files from `/home/administrator/hexalith-retirement-investigation-evidence/20261023t085529z-s427-recovery-command-preparation/revision-2/source-telemetry_postgres_adapter.py`, `source-test_telemetry_postgres_adapter.py`, and `source-TELEMETRY-POSTGRES.md`. Update the exact sibling common-source hash to its new reviewed bytes. KEEP all existing 21-test behaviors, no live/backend calls, exact source pins, native identity, peer-only argv, exported-snapshot consistency, source census binding, plaintext/ciphertext distinction, isolated target resource guards, deliberate role/DB activation differences and null actual qualifications. Parent owns actual-input rebinding and the new final packet.

Re-derivation requirements:
- Return fully independent command/contract data: no returned artifact or nested object may alias caller input. Test mutation in both directions.
- Add explicit retained transfer policy fields `retentionDays=30` and `maximumProofAgeSeconds=86400` to the closed proposal input. Extend the runtime authenticated capture record with `capturedAt` and `expiresAt`, requiring capture no earlier than the bound source observation and expiry exactly capturedAt+86400 seconds. Independently read-back retention must include actual COMPLIANCE mode, retainUntil and AES256 encryption, equal to the exact immutable artifact and at least capturedAt+30days. A year2000/short/mismatched retention must refuse; never fabricate actual metadata into the proposal, whose runtime fields remain null. This constructor only checks consistency of supplied already-authenticated records; actual cryptography/provider readback stay separate null gates.
- Add deterministic full-table content SHA256 for BOTH bound tables under the same exported snapshot, not only row counts. Use canonical row JSON, stable representation settings (UTC/DateStyle/bytea/float as needed) and deterministic ordering preserving duplicates. Use PostgreSQL18 built-in hashing, no new extension/install. Bound query duration/row profile; no actual SQL/data hash runs in this implementation. New pure canary comparison logic must reject same counts with different content digests, missing/duplicate table data checks or changed metadata, and remain explicitly unqualified.
- Include direct per-sequence last_value AND is_called observations, beyond pg_sequences, matched to an explicit sequence census. Generated dynamic read-only sequence SQL must quote catalog-derived identifiers safely; the caller must retain quiescence and stable before/after sequence samples since sequence state is not MVCC-isolated. Zero-sequence census must be explicit and must not mask a missing observation. No source CHECKPOINT/nextval/setval/mutation is allowed.
- Replace ambiguous raw tuples with tagged JSON records: every result has a category and safely escaped structured payload, with explicit null/empty representation. Source/target portable schema/data/count/sequence/constraint/extension/ACL categories are compared independently from deliberate fresh bootstrap/current-user/role activation differences. Add meaningful synthetic comparison tests for separators/newlines/NULL and category spoofing/missing/duplicate records, not only SQL substring tests. The parser/consumer must remain bounded, data-only, and have no DB/client/provider access.
- Normalize the supported integer/millicore CPU and Mi/Gi memory quantities. Reject CPU memory-unit misuse, requests exceeding respective limits, malformed or unsupported quantities. Add actual impossible-request fixtures.

No new source/protected credential reads or runtime execution is needed for these fixes. Parent can add the two static transfer policy fields to a NEW actual input, preserving all revision-2 bytes.


## Review Triage Log

| Finding | Verdict | Route / evidence |
| --- | --- | --- |
| Integrated Blind5 / Edge3 | medium | Returned artifact aliases caller; copy complete returned contract and prove independence. |
| Integrated Blind6 | high | Year2000 retention accepted with no independent lock evidence; strengthen retained policy/actual runtime consistency contract. |
| Integrated Blind7 | high | Changed row contents with equal counts pass proposed checks; add deterministic content digests and comparator regression. |
| Integrated Blind8 | medium | Sequence view omits is_called/direct value; add direct stable sequence observations tied to census. |
| Integrated Blind9 | medium | Unescaped tuples make distinct metadata ambiguous; structured tagged JSON and bounded semantic comparison. |
| Integrated Blind10 | medium | CPU1000m request/500m limit renders; normalize units and enforce requests<=limits. |

These are this change's implementation/verification defects, none deferred. Missing content/sequence/framing and independent-retention contract details route to bad_spec outside the unchanged frozen owner intent; smaller corrections are retained re-derivation requirements. No actual runtime outcome is accepted.

## Verification

- `/usr/bin/python3.14 -I -S -B eng/recovery-preparation/test_telemetry_postgres_adapter.py` -- meaningful synthetic tests pass without live I/O.
- Scoped whitespace check and parent exact private packet consistency audit.

## Review 3 Re-derivation Contract

Re-derive ONLY the three telemetry code/test/docs files from exact private snapshots under `/home/administrator/hexalith-retirement-investigation-evidence/20261023t085529z-s427-recovery-command-preparation/review-3-source/source-telemetry_postgres_adapter.py`, `source-test_telemetry_postgres_adapter.py` and `source-TELEMETRY-POSTGRES.md`. Parent will dispatch after the common patch returns. Update the common-source pin to those then-current exact bytes. KEEP all34 existing tests and all prior positive source binding, independent readback, alias isolation, tagged parser, snapshot, resource, custody, CLI refusal and null-qualification behavior. Original frozen text, revision-2 and review-3 snapshots remain unchanged. Parent owns final actual input/packet rebinding.

Fix the complete review findings with a bounded supported database profile, not unverified blanket schema claims:
- Preserve native json field representations in content hashing: replace to_jsonb(record) normalization with row_to_json(record)::text under the existing stable GUCs and framing. Retain deterministic duplicate rows and full-content digest profile. Document exact representation limits and lack of actual SQL qualification. Add a focused regression ensuring the content SQL cannot use JSONB normalization; do not start a database to implement this.
- Cross-check schema namespaces/relations/columns against the two bound tables. Every bound table must have exact table relation and nonempty coherent column metadata; reject missing/orphan/duplicate references, unsupported relkinds, unknown table references and schema inconsistencies. Support only ordinary tables, indexes and sequences in this limited recovery profile. Refuse views/materialized views/foreign or partitioned tables rather than claim their definitions are checked.
- Add an explicit structured unsupported-object census for non-extension-owned functions/procedures, user triggers, RLS/policies, user-defined types or other unhandled executable schema objects. Keep supported pinned extensions (plpgsql and observed pgstattuple) as explicitly bounded extension identity/version coverage, not routine-body equality. Any nonempty unsupported census refuses comparison before agreement. Add SQL that observes those categories without mutation and representative nonempty/missing-census refusal tests. Do not execute SQL.
- Validate sequence definition numeric strings/type ranges (smallint/integer/bigint), minimum<maximum, nonzero increment and positive cache; start and observed lastValue must fit the supported type and sequence bounds. Before/after samples remain stable and source/target equal. Test each malformed/impossible case plus stable target lastValue and isCalled differences.
- Validate content statistics are feasible: nonempty rows have nonempty representation; maximum <= total <= rows*maximum and sufficient minimum bytes for remaining rows; zero-row digest is exact empty SHA256. Keep all bounds.
- Observe and verify target role authority beyond login/superuser: createdb, createrole, replication, bypassrls, inheritance and memberships. Source observations may record existing authority, but new target custom application roles must be NOLOGIN/NOSUPERUSER/NOCREATEDB/NOCREATEROLE/NOREPLICATION/NOBYPASSRLS with no activation-grant membership. Explicitly define/validate the supported source membership profile and target-local builtin/bootstrap exceptions; unsupported custom membership semantics stop rather than silently drop restoration meaning. These observations are runtime canary transcript fields, not invented current source facts in proposal input. Add authority/membership adversarial tests.
- Align telemetry cleanup with common contract: ownedTargets contains only allowed deletion roots; generated Pod is only a proposed cleanupCensus member. Complete census, target paths, bound owned UID/RV and qualified fence remain run-produced null gates. Add alignment tests preventing a Pod from becoming a destructive root.

This amends only non-frozen implementation contract. No new source reads, database startup, live SQL, provider or operational command is needed. Parent integrates the same actual proposal source facts and unchanged owner direction, updates only retained static transfer constants and tool/parent hashes in a new private input, then seals a distinct packet.

### Integrated review 3

| Finding | Verdict | Verified evidence / route |
| --- | --- | --- |
| Blind 1 | high | Content SQL uses to_jsonb(record), which normalizes native json duplicate keys/lexical representation; replace with native-preserving row JSON in existing content query. PostgreSQL runtime qualification remains separate. |
| Blind 2 | high | Parent pure reproduction accepts empty relations/columns with nonempty table census; require catalog references and table coverage. |
| Blind 3 | medium | Sequence definitions pass only string checks, so nonsensical numeric/range/cache values pass; validate supported native numeric profile and sampled range. |
| Blind 4 | medium | Parent pure reproduction accepts contentBytes16000000 for two one-byte rows; enforce count/maximum/total feasibility. |
| Blind 5 | high | Cleanup target_paths maps ownedTargets PVs, while cleanupCensus allows extra PV; reject census PV outside deletion-root inventory before request construction. |
| Blind 6 | medium | First fence observation can age across up to64 census GETs; refresh after identity binding before any deletion, while retaining per-dispatch freshness. |
| Blind 7 | high | Target role query checks only login/superuser although role SQL strips more authority; require full relevant authority flags/membership observations and explicit source/target profile. |
| Blind 8 | medium | View definitions are not observed; fail closed on unsupported views/materialized/foreign/partitioned relation profiles rather than imply their recovery verification. |
| Blind 9 | medium | User functions/triggers are not observed; explicitly census unsupported non-extension function/trigger objects and refuse them, retaining only pinned extension-owned objects as the limited profile. |
| Blind 10 | medium | Telemetry cleanup includes Pod as owned deletion root and has no separate census; align with common root/census contract and keep all future identities null. |
| Edge 1 | high | Same extra-PV path gap as Blind5, independently reproduced; small reject guard and regression. |
| Edge 2 | high | Same missing-catalog gap as Blind2, independently reproduced; schema completeness contract correction. |
| Verification 1 | medium | Pre-verified mutant removing source/target sequence equality passes34 tests; add stable target lastValue/isCalled disagreement regression. |
| Verification 2 | medium | Pre-verified mutant replacing set inventory comparison with ordered tuple passes90 tests; add reordered complete-census success regression. |

All fourteen findings are retained. Common PV/fence/order findings are patch corrections to existing demonstrated state and interfaces. Telemetry coverage/profile/authority contract corrections require re-derivation from the exact review-3-source snapshot, outside unchanged frozen intent; no finding is deferred. No real qualification or operational approval follows from this review.
