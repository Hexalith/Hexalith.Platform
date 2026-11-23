# R4 synthetic closure examples

All instances use fictional identities, a fake clock and inert references. The shared run is package-mode CI with two writers and one owned control. The packet includes exact bytes for the D2 artifacts, selection registry, control request and reports. Other producer references stand for external inputs. The fake support, adoption, operation-owner and comparison authority references permit review of populated branches. They confer zero execution, qualification, custody or publication credit. Use disposable in-memory inputs, fake transports and fake resource inventories for verification.

## D1 · Public serialization

[Descriptor](fixtures/descriptor.json), [failed result](fixtures/result.json) and [partial-startup result](fixtures/partial-startup-result.json) are full documents. They carry the unchanged v1 envelope with the paired v2 identifiers and required qualification binding. The failed result adds observed host and control facts while preserving the immutable descriptor scope, profile, hosts, operations and controls. The CI hold keeps its acceptance, effective seconds and absolute expiry when it ends. Partial startup uses the original issued identities and explicit unavailable producers. The failed result's missing logical readback blocks semantic credit, and its observed cleanup is incomplete.

Check these cases by changing one input at a time. Adding an unknown nested key or removing a required observation union refuses the shape. Using an endpoint on the wrong resource or a different attempt on the result refuses the binding (`HXQ203`). Replacing the reader's fresh generation with a stale one refuses the observation (`HXQ204`). Unavailable required observations or only empty arrays provide no observation credit (`HXQ209` at prerequisite evaluation). The historical R2 fragment and metadata files remain examples of inputs that refuse as full documents.

## D2 · Immutable bytes

The full [configuration](fixtures/configuration.json), [required test set](fixtures/required-test-set.json) and [writer inventory](fixtures/writer-inventory.json) feed the [profile](fixtures/profile.json). Each has compact UTF-8, ordinal member order and exactly one trailing LF. The profile binds the three exact digests. Configuration and inventory bind the same opaque operation contract, and the descriptor and selection bind the resulting exact profile digest. The test-set order is part of its identity. Each native tuple includes the assembly filename, class, method and exact test name, including two data cases sharing a method. The fictional inventory is complete for its fixture vectors only. EventStore's full AC1–AC11 denominator still applies to qualification.

Delete the trailing LF or add a space to the configuration and retain the original reference. The changed bytes refuse (`HXQ202`). Reordering the two native data cases changes the artifact bytes and also violates their declared ordering. The fake environment-request instant is 00:00:00Z, the execution deadline is 00:01:42Z, control admission ends at 00:01:00Z, and the original owner stop/cleanup bound is 00:02:12Z. The reset deadline is 00:01:20Z. An attach expiry, when applicable, can further bound admission. Retry uses those original inputs.

## D3 · Selection and plan

The [complete registry](fixtures/selection-registry.json) holds a designation and one closed entry. The [internal plan example](fixtures/run-plan.json) carries that exact `QualificationSelection` and the inherited `SelectedTest` and `SelectedTests` metadata. The plan paths and image are illustrative. Artifact resolution and consumer support use digest-bound input from the owner, and the v2 example binds the selected pair and private channel explicitly. Preflight checks them before allocation and rechecks source and accepted consumer bytes before launch.

| Synthetic preflight | Expected disposition |
| --- | --- |
| Ordinary undesignated suite and v1 support | Use the existing v1 pair and its existing credential rule. |
| Registry entry, exact artifact and consumer references, v2-capable readers | Select v2/v2 with private v2 and the explicit plan. Actual readiness and credential preparation are separate later checks. |
| Designated key with its matching entry removed, copied twice, or with one manifest/entry digest changed | Refuse `HXQ201` before allocation. |
| Same entry, consumer v1-only, wrong kind, wrong content or missing adoption reference | Refuse before allocation or client construction. |
| v2 descriptor with v1 result, v2 with private v1/absent, or v1 with private v2 | Refuse. Detect declared mismatches during preflight. |
| V1-only reader receives the example run plan | Refuse unsupported plan version. |
| Compatible whole batch of v2 selections / batch with an ordinary v1 entry or a different v2 topology or consumer | Preflight all entries, then use separate suite/test-set/attempt/report scopes for compatible v2. Refuse the whole incompatible batch before any allocation. |

## D4 · Private credentials

The [complete canary file](fixtures/private-credential-canaries.json) includes each of the three proposed credential kinds. The values are inert strings for exact-value scanning. Model the file as the descriptor's fixed sibling `native-endpoint-credentials.v2.json` with owner-only permissions. At 00:00:12Z, the three grants match the declared resource, module, endpoint, exact URI, protocol and scope. The HTTP and gRPC grants bind only the declared actor, host, partition and selected logical routes. The control grant allows request, observe and release, with a bound of one use. Credential expiry is exclusive at 00:00:55Z. LC-06 closes access earlier by cancellation.

Change the control grant to include `reset`, remove a value, change the public resource/URI binding, or present the unchanged file at or after expiry. Expected result is credential refusal before client construction (`HXQ206` for semantically wrong scope or access). With fake HTTP/gRPC clients, a redirect, alternate authority or unlisted route sends no credential. Append a canary in memory to a synthetic report or diagnostic candidate. Retain only the existing inspected/redacted form when the exact canary is absent, with distinct original and retained identities if the bytes changed. If the canary survives, refuse retention. Owner reset uses the original internal deadline and never consumes or reopens a module grant.

## D5 · Native rows, observations and comparisons

The [report bindings](fixtures/report-bindings.json) give each exact original and retained report digest, the producing run and attempt, scope, source, consumer and fixed synthetic argv, and its first terminal record. The samples retain the same report bytes because the base reports contain no private value. The fixture terminal references hash each exact compact terminal object with one final LF for review. The production design retains the existing owner's terminal/report record format.

| TRX input | Required / executed / completed / passed / failed / skipped | Expected |
| --- | --- | --- |
| [Intact](fixtures/intact.trx) | 8 / 8 / 8 / 8 / 0 / 0 | Native accounting passes for the fixture. Eligibility still requires the other gates. |
| [Mixed](fixtures/mixed.trx) | 8 / 5 / 4 / 1 / 3 / 1 | Exit 0 gives no credit. Native Timeout/Error/Failed map to failed, Aborted to cancelled, Inconclusive to skipped, and NotExecuted to not-executed. The eighth required ID has no row. Native failed aggregate is 4, public failed count is 3. |
| [Original-read revision removed](fixtures/revision-removed.trx) | 8 / 8 / 8 / 7 / 1 / 0 | The exact revision prevention row fails and the lawful control passes. The ordinary result remains failed. |
| [Create-once identity removed](fixtures/create-once-removed.trx) | 8 / 8 / 8 / 7 / 1 / 0 | The exact create-once prevention row fails and the lawful control passes. The ordinary result remains failed. |

The [two comparison records](fixtures/comparisons.json) bind the same fictional intact profile and adversary, distinct simulated authority records, and the exact altered prevention and lawful-control rows. Substitute the other failed test, mismatch profile or adversary, or remove authority and the comparison is invalid with zero credit. Duplicate a native result row or execution ID for identity refusal (`HXQ207`), or change a counter without changing rows for completion refusal (`HXQ208`). Missing or truncated native material supplies no completed report.

The [canonical request](fixtures/control-request.json) binds the original barrier, host generation, partition and batch before use is reserved. The failed result shows a requested → reserved → unknown → reconciled → released sequence and a failed reset followed by a bounded retry. Replay the exact request bytes while unknown to return the existing reservation with zero additional use. Reuse its ID with changed bytes or apply after expiry to refuse (`HXQ205`). The fresh read follows a new reader process, sidecar and cache generation with attributable loaded evidence. The required actual oracle is explicitly unavailable.

## Revised CI lifecycle · LC-01–LC-06

The [ordered lifecycle fixtures](fixtures/ci-lifecycle.json) pair fake execution, owner receipt, actual-job status, terminal, hold, public cleanup and helper evidence inputs with their expected dispositions. Apply the [R3 lifecycle rules](../spec-plat-es416-qh-q2-contract-implementation-r3/ci-lifecycle-inheritance.md) and the [published v1 contract](../../../docs/contracts/test-environment-v1.md#ci-ownership-recovery-and-finalization).

- **LC-01 and LC-02.** A cooperative helper timeout can set the first owner terminal to `cancelled` and withdraw an active hold as `ci-job-cancelled`. The helper records `timedOut: true` and returns 124. Preserve an earlier terminal and any already-ended hold. For remaining active holds, the independent finalizer uses actual job status and the recorded terminal. A failed terminal in a failed job means `ci-job-completed`. Native Timeout and local failure retention use their own rules.
- **LC-03 and LC-04.** Escalation and stdin loss may preempt withdrawal. Preserve missing observations. Stop the recorded tool, read the external receipt, and when absent stop the rest of the recorded session and read it again. Recover a late receipt through verified exact `down` with actual `HEXALITH_CI_JOB_STATUS`. Only a persistent absence after successful recorded-session shutdown permits `not-allocated`. Determine that a receipt belongs to the exact run, attempt, environment and CI job before cleanup.
- **LC-05 and LC-06.** An unavailable tool leaves operational cleanup unverified and retains only declared, classified and redacted diagnostics. Unknown discovery and owned leftovers stay incomplete. Verified empty discovery on exact retry can complete cleanup and preserves the earlier terminal, hold facts and peer data. Access closes immediately. The internal owner can attempt an originally bounded reset, while expired retries and renewed module access refuse.

## Decisions requiring review

| Decision | Scope still to approve |
| --- | --- |
| D1 | Platform, Builds and EventStore need to approve the closed public types, names, bounds, availability rules and exact reader/writer agreement. |
| D2 | Platform, Builds and EventStore need to approve the canonical original-byte formats, deadline arithmetic, stable native joins and producer assignments. Name the EventStore Test and 3.17 owners and identify the owning artifact deliveries. |
| D3 | Builds and Platform need to approve the compiled registry, explicit plan and whole-batch boundary. EventStore needs to approve the exact version support and owning adoption work. Name the intended compatible tool release and consumer revision through the separate delivery gates. |
| D4 | Platform, Builds, EventStore and the applicable Security/7.6 authorities need to approve finite kinds, lifetimes, exact transport scope, canary handling and separate reset. Qualified receiver/custody and actual credential authority remain separately pending. |
| D5 | Platform needs to approve observation, reset and cleanup truth. Builds needs to approve the TRX join, native outcomes/counts and metadata-only refusals. EventStore needs to approve provider-free logical routes, oracle evidence, coverage and separate comparison roles. |

R2 contains the proposed D1–D5 formats and RF-00–RF-12 product checks. Fixture choices such as exact synthetic IDs, ports, native case names and stub references are R4 illustration choices. Any owner-required change to the bound design requires a subsequent revision and its matching receipts. The offline [fixture checker](verify_fixtures.py) checks the published v1 projections and the fixture byte, identity, deadline, TRX and lifecycle joins. RF and LC product checks, independent release reviews and Q3–Q5 hosted qualification remain pending.
