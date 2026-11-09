# D5 — recommended observations, refusals and native reconciliation

Revision `PLAT-ES416-QH-Q2-R2`. **PROPOSED; owner acceptance pending.** D1 imports these closed types. No observation, native run, fault, falsification or ledger is created here. Use existing owner control/lifecycle and inspected-report records; external references stay opaque until their original owner authenticates them.

## Closed observation types

Types/limits/U branches are D1's; every field below required unless marked `?`. All objects closed. Each event's diagnostics resolve to classified top-level references, not raw logs or external fetches. Collections have at most 4096 records; reaching the cap refuses further admission and blocks incomplete lanes, never truncates/recycles prior evidence.

| Type | Complete field set | Causality / scope |
| --- | --- | --- |
| `Barrier` | `observationId:I`, `barrierId:I`, `hostId:I`, `generationId:I`, `partition:Partition`, `originallyReadRevision:I`, `batchContentIdentity:H`, `observedAt:T`, `diagnosticIds:L<I>[0,32]` | Resolve declared barrier, exact original read/batch/host generation before release; no freshly fetched revision attached to stale state. |
| `Freshness` | `observationId:I`, `method:E(restart,cache-clear)`, `hostId:I`, `previousGenerationId:I`, `generationId:I`, `previousCacheGenerationId:I`, `cacheGenerationId:I`, `observedAt:T`, `diagnosticIds:L<I>[0,32]` | Restart: different host/cache generations and newly observed process/sidecar; cache-clear: same host generation, new cache generation. Both resolve actual host records; later loads/readback must cover current generation. |
| `Readback` | `observationId:I`, `adversaryId:I`, `batchContentIdentity:H`, `partition:Partition`, `logicalReadRouteId:I`, `readerHostId:I`, `readerGenerationId:I`, `readerCacheGenerationId:I`, `freshnessObservationId:I`, `readObservedAt:T`, `expectedLogicalOracleRef:U or P<Reference>`, `actualLogicalReadbackRef:U or P<Reference>`, `diagnosticIds:L<I>[0,32]` | Read strictly after freshness, exact batch/partition/owner route and current reader/cache; U oracle/readback blocks semantics. No retained erased body or new witness. |
| `PathCoverage` | `observationId:I`, `inventoryContentIdentity:H`, `pathId:I`, `vectorId:I`, `disposition:E(executed,excluded,not-executed)`, `nativeTestRef:U or P<NativeTestRef>`, `lawfulControlRef:U or P<NativeTestRef>`, `exclusionRef:U or P<Reference>`, `observedAt:T`, `diagnosticIds:L<I>[0,32]` | Inventory path/vector must resolve. Executed requires native test and lawful-control records; excluded requires demonstrated exclusion plus passing lawful control. Not-executed/U grants no credit. No denominator removal. |
| `NativeTestRef` | `runId:I`, `attemptId:I`, `reportRef:Reference`, `testId:I`, `executionId:I` | Exact producing native row and stable D2 ID; comparisons may reference separately authorized intact/altered runs, never repurpose another decision's result. |
| `ControlEvent` | `observationId:I`, `sequence:N+`, `requestId:I`, `controlId:I`, `scope:Scope`, `hostId:I`, `generationId:I`, `partition:Partition`, `phase` from declared Control, `action:E(apply,release,reset)`, `event` below, `observedAt:T`, `diagnosticIds:L<I>[0,32]`, `detail` below | One host per request; multi-host controls use separately admitted host requests within original max uses. Scope/control/phase/host/partition fixed, exact request binding immutable. Sequence is per attempt/control across requests, unique and strictly increasing. |

`detail` is a discriminated closed union selected by `event`, never an arbitrary map:

| `event` | Exact `detail` fields | Allowed predecessor / effect |
| --- | --- | --- |
| `requested` | `requestContentIdentity:H`, `barrierObservationId:U or P<ObservationRef>`, `batchContentIdentity:H` | First event of a new request (`action:apply`), admitted in original phase before access/admission expiry. Barrier required/populated for after-original-read controls. |
| `reserved` | `reservationId:I`, `useNumber:N+` max declared maximumUses | Requested → reserved, atomically durable in existing owner record **before mutation**. Unique useNumber per control across requests. |
| `applied` | `reservationId:I`, `applicationObservationRef:Reference` | Reserved → applied only on observed exact application; action apply. Startup failure cannot supply it. |
| `unknown` | `reservationId:I`, `reason:E(application-unknown,reset-unknown)` | Reserved/application or resetting uncertainty. Reservation remains consumed; no reapply on retry. |
| `released` | `reservationId:I`, `reachedObservationId:U or P<ObservationRef>`, `releaseObservationRef:Reference` | Applied → released, action release, names exact reached barrier/batch where applicable. Unknown application must first reconcile; no assumed applied event. |
| `reset-requested` | `reservationId:I`, `retryNumber:N+` max 32 | Applied/released/unknown/application-failed/reset-failed → bounded reset attempt, action reset by owner only; initial retryNumber 1, increments exactly once on a new bounded retry. |
| `reset` | `reservationId:I`, `resetObservationRef:Reference` | Reset-requested → observed exact reset before original reset deadline. Reset ends fault, not original outcome/retention. |
| `refused` | `reason:E(scope-mismatch,phase-mismatch,expired,uses-exhausted,conflicting-replay,unsupported-control)` | No new reservation or mutation; record only safe metadata for bound requests. Foreign caller values never echoed. Conflicting replay leaves original request intact. |
| `application-failed` | `reservationId:I`, `reason:E(application-failed)` | Reserved → observed failure; reservation retained, reset obligation still accounted. |
| `reset-failed` | `reservationId:I`, `reason:E(reset-failed,reset-deadline)` | Reset-requested → failure; remains visible; only bounded owner reset retry/observation may follow, never new apply. |
| `reconciled` | `reservationId:I`, `resolution:E(applied,not-applied,reset)`, `observationRef:Reference` | Unknown → observed resolution. Applied resolution permits release/reset; not-applied ends application but consumes use; reset resolution ends fault. Prior unknown is preserved. |

`ObservationRef` is exactly `{observationId:I}`. Requested requestContentIdentity hashes the exact immutable request bytes produced under D2 encoding: closed object with `requestId`, `controlId`, `scope`, `hostId`, `generationId`, `partition`, `phase`, `action:"apply"`, `barrierObservationId` and `batchContentIdentity`. It excludes itself, sequence/time observations and credential values. Hashing isn't authentication. A reset/release references the same request, not another apply request.

## Advancement and replay rules

Across snapshots, U may advance to populated; populated entries form an identical ordered prefix, never replaced/removed. Executing tool and executing Dapr CLI are write-once per producing runner scope. Capability negotiation and verifier readiness append attributed records; after a host generation or qualified receiver transition, new records must cover current generations/inputs. Prior failed records remain visible; no scalar rewrite upgrades a failure. Observation times never regress; causally later records may share a clock tick, except fresh read must be strictly later than its freshness observation. Sequence orders same-tick control events. Reject references to another batch/partition/generation or contradictory transition/time atomically. Result immutable declarations match descriptor, original deadlines/outcome/retention remain governed by v1.

An identical retry resolves by immutable request bytes in the existing durable owner record, returns its existing reservation/status and performs **zero** additional mutations/uses. Different bytes for an existing request ID refuse. Unknown consumes the reserved use and keeps lane blocked until observed reconciliation; it never releases the use or licenses another apply. A new request needs another available original use and fresh admission, including current generation/phase/access. Recheck original phase/access/control expiry immediately before mutation; an expired reserved request performs no mutation and its reservation remains consumed. Only bounded internal owner release/reset may act after module access ends. Reset retry remains within original deadline, maximum 32 attempts and existing stop/cleanup allowance. No counter rollover or revived terminal/access is allowed. Physical handlers/durable crash proof remain later Q4/Q5; synthetic transition checks establish only Q2 representation.

Complete inventory coverage means each required inventory path/vector has one unambiguous executed or demonstrated-excluded record and passing applicable lawful control, with exact native/oracle references. Missing/skipped/unavailable/excluded-without-proof stays non-passing. AC8 binding/OQ8 and AC9 migration/rollback/restore/package parity remain pending in the full AC1–AC11 ledger even when a candidate subset is declared.

## Fixed metadata-only refusal mapping

Preserve existing v1 HXT101–107 schema/file/timing meanings, Builds HXT001–006 native-loader meanings and EventStore HXT108–110 consumer/client/private meanings. Recommend new **HXQ** codes, avoiding HXT110 collision. These are proposed codes, not emitted implementation behavior. Each exposes only constant source `hexalith.qualification.v2`, constant field and the fixed message below; never caller strings, paths/unknown keys, values, exception text or report content. Cancellation propagates as cancellation. Earliest phase wins; within phase use table order after inherited parser/version/policy checks. Return no partial document/client or permissive fallback.

| Code | Constant field | Fixed message / category |
| --- | --- | --- |
| `HXQ201` | `selection` | Qualification selection or consumer support is unavailable or incompatible. |
| `HXQ202` | `artifacts` | Qualification artifact bytes or references are invalid. |
| `HXQ203` | `identity` | Qualification identity or owned references disagree. |
| `HXQ204` | `observations` | Qualification observations are contradictory or unattributed. |
| `HXQ205` | `controls` | Qualification control admission, replay or transition is invalid. |
| `HXQ206` | `credentials` | Qualification private credentials or client scope are invalid. |
| `HXQ207` | `nativeIdentity` | Qualification native test identities cannot be reconciled. |
| `HXQ208` | `completion` | Qualification native outcomes or counts cannot receive suite credit. |
| `HXQ209` | `prerequisites` | A required qualification prerequisite or observation is unavailable. |

A shape/type/duplicate-key/file/timing issue still uses its inherited HXT category; HXQ codes cover additional v2 semantics. A missing later observation blocks its lane rather than preventing truthful first-terminal/cleanup recording. Rejecting a leaking report occurs before retention using existing inspected-report handling; raw secret bytes never become diagnostic text.

## Native identity and count rules

Recommend extension of the existing Builds loader for **explicit v2 selection only**; v1 loader behavior stays unchanged. At inspection capture immutable original TRX bytes and report hash, selected consumer/test artifact, run/attempt, actual argv, source and declared suite/config/test-set identities in existing runner records. A TRX file need not repeat these identities: the authenticated owner handoff/report record binds them; filename or process exit cannot fabricate them. Retained redacted TRX may have different bytes: record original inspected digest and retained digest as distinct existing diagnostic references, never overwrite either identity. No raw leaking original is retained. The v2 loader extension must return inspected non-passing row/count metadata for truthful failed/cancelled completion recording, separately from its pass result; existing v1 failure return behavior remains unchanged.

Require one root TestRun, one ResultSummary/Counters and complete definitions/results. Each UnitTestResult.testId joins exactly one UnitTest.id; its executionId is unique and joins that test's Execution.id; conflicting/duplicate rows/definitions, ambiguous data-driven cases, unknown test definitions or absent IDs refuse. Map exactly via D2's assembly/class/method/testName tuple to a stable required testId. Do not deduplicate reruns or pick a last/pass row. The selected v2 native run must contain exactly the declared required test set; ancillary rows need their own declared test IDs or a separate diagnostic report, never silently disappear from counts. There is one exact-case mapping:

| Native row outcome | Existing completion `outcome` | Started (`executed`) | Terminal (`completed`) | Native counter bucket |
| --- | --- | --- | --- | --- |
| `Passed` | `passed` | 1 | 1 | passed |
| `Failed` | `failed` | 1 | 1 | failed |
| `Error` | `failed` | 1 | 1 | error |
| `Timeout` | `failed` | 1 | 1 | timeout |
| `Aborted` | `cancelled` | 1 | 0 | aborted |
| `NotExecuted` | `not-executed` | 0 | 0 | notExecuted |
| `Inconclusive` | `skipped` | 0 | 0 | inconclusive |
| Missing row | **No fabricated row** | 0 | 0 | Missing, no counter bucket |

Unknown/native `Skipped` or other adapter vocabulary refuses until separately accepted with a revised exact mapping; never guess by summary. This proposed mapping of Inconclusive to non-executed/skipped is an accounting convention, not a factual runtime-start claim. Actual TRX started execution remains its own native observation.

Let N be declared required IDs, P/F/S/C/Z the counts of mapped `passed`/`failed`/`skipped`/`cancelled`/`not-executed` rows, and M the missing required IDs. All arithmetic uses checked wide intermediates; stored counts must fit plain nonnegative Int32. Recommend:

- `required = N`; `executed = P + F + C`; `completed = P + F`; `passed = P`; `failed = F`; `skipped = S`.
- `P + F + S + C + Z + M = N`. M is verifier-local missing coverage, **not** a new public count or invented test row. Before row/report inspection, completion remains U.
- Native `total = P + F + S + C + Z` and each native bucket equals its exact mapped row count. Summary `passed`/`failed` required; optional error/timeout/aborted/notExecuted/inconclusive counters mean zero only if their row bucket is empty. Duplicate/negative/overflow/noninteger/inconsistent counters refuse.
- Preserve current native-loader aggregate `failed + error + timeout + aborted` and `notExecuted + inconclusive` checks as an additional native-report prerequisite. Do not equate this aggregate with public completion.failed: Aborted is public cancelled. Record cancelled/incomplete rows honestly.
- Ordinary suite credit requires N > 0, M = Z = C = S = F = 0, P = N, executed = completed = required, exact identity/report binding, and all original loader/run/terminal/tool/candidate checks. Exit zero/green summary alone cannot pass; a failed native report remains available as inspected non-passing metadata, not a promoted report success.

A run cancelled, timed out, withdrawn or failed still records its original terminal outcome immediately. Partial counts cannot relabel it; missing report keeps completion U. Schema-shape validity does not reconcile rows or authenticate references.

## Expected-failure comparison

Use existing owner comparison/report records with the following proposed closed metadata: `falsificationId:I`, `removedRule:E(original-read-revision,create-once-identity)`, `profileContentIdentity:H`, `adversaryId:I`, `authorizationRef:U or P<Reference>`, `intactRunRef:U or P<Reference>`, `alteredRunRef:U or P<Reference>`, `lawfulControlRef:U or P<NativeTestRef>`, `expectedFailedTestRef:U or P<NativeTestRef>`, `comparisonObservation:U or P<Comparison>`. `Comparison` is exactly `observedAt:T`, `effect:E(intended-failure-observed,invalid-comparison)`, `diagnosticIds:L<I>[0,32]`. Bind both runs' exact original native/terminal references, same profile/adversary and separate authority for each removed rule; require intact prevention and lawful control pass, altered lawful control pass and exact intended prevention test failed for that rule. Unrelated failure, unavailable readback/authority or missing report invalidates comparison.

The altered ordinary result **stays failed**, its prevention row stays failed and its failed count is not reduced. A valid classified comparison grants only that authorized experiment's intended-effect observation; no ordinary suite, candidate, intact conformance, U2/U4/U5 or G-APPEND pass follows. Both physical falsifications remain unexecuted and independently gated.
