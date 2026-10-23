# Test environment and terminal result v1

Platform owns these schemas and semantics:

- [`hexalith.test-environment.v1`](schemas/hexalith.test-environment.v1.json) describes the runner environment handed to module tests.
- [`hexalith.test-result.v1`](schemas/hexalith.test-result.v1.json) records the first terminal outcome and its environment snapshot.

Both require the exact `policyId`
`hexalith.platform.candidate-and-attachment-policy.v1`, defined by the immutable
[candidate and attachment policy](candidate-and-attachment-policy-v1.md).
Builds owns the offline consumer reader and ships byte-identical schema mirrors
with `hexalith-module`. Publication, successful parsing and the illustrative
[samples](https://github.com/Hexalith/Hexalith.Builds/tree/main/test/fixtures/module/test-environment-v1)
confer no runner adoption, runtime qualification, attachment permission or
candidate-evidence acceptance. No tool or catalog pin changes here.

## Shape, versions and population

Each document is a closed JSON object containing `schema`, `policyId`, `bindings`
and `diagnostics`. Results additionally require `terminal`, `termination`,
`retention` and `cleanup`. All nested objects are closed: unknown properties and
incorrect casing fail. Each schema is self-contained Draft 2020-12; its `$ref`
values reference only its own `$defs`. Validators must assert formats, including
`uri` and `date-time`, as well as schema types and bounds.

Readers select a document kind explicitly. Descriptor reads support only
`hexalith.test-environment.v1`; result reads support only
`hexalith.test-result.v1`. Missing, unsupported, wrongly typed or other-kind
versions fail and name the supported versions for the requested kind. Missing,
unknown or mismatched policy identifiers fail closed. There is no latest-version
selection, legacy-status translation or policy fallback. Changed semantics need
a new published version and explicit consumer adoption.

Every reserved binding is required, even when its producer is unavailable. It
uses exactly `{"state":"not-yet-populated"}` until that producer can populate
its defined typed shape. A populated binding uses `"state":"populated"` and
all fields required by that branch. Composition and catalog identities retain
their direct known-value objects (`id`/`contentIdentity` and `commit`/`instanceId`),
and also accept the same explicit `not-yet-populated` object while unavailable.
Do not use null, an omitted binding, an empty
object, fabricated identities, catalog identities substituted for candidate
identities, or a configured tool pin substituted for the executing tool.
`execution` deadlines also permit `{"state":"not-declared"}` when the test
configuration declares no deadline. That branch may also carry the existing
`origin`, `originAt` and `actualDurationSeconds` observations after execution
starts, without inventing `effectiveSeconds` or `expiresAt`. This is distinct
from an unavailable value.
`not-overridden` identifies the default startup budget.

Identities contain 1–2,048 characters with no leading or trailing whitespace,
and without C0/C1 control characters at any
position (U+0000–U+001F and U+007F–U+009F); committed revisions are complete 40- or
64-digit hexadecimal SHAs. Content identities are `sha256:` followed by 64
lowercase hexadecimal digits, not version labels. UTC timestamps use
`YYYY-MM-DDTHH:mm:ss[.fffffff]Z`, with valid calendar/time values and at most seven
fractional digits and seconds from 00 through 59; leap seconds, numeric offsets,
local times and null timestamps fail.
Durations are finite seconds. Counts, attempt sequences, CI workflow attempts
and hold seconds use plain JSON Int32 integers (maximum 2,147,483,647, with the
stricter field-specific bounds still applying). The reader rejects decimal and
exponent spellings in these integer fields, including `300.0` and `3e2`, so a
validated document can be consumed with `JsonElement.GetInt32`. Counts are
nonnegative; sequences and attempts are positive. Numeric values must be finite.
The reader accepts strict UTF-8 JSON of at most 1 MiB, including
an optional UTF-8 BOM, and at most 64 nesting levels. It rejects duplicate keys
at any depth, comments, trailing commas, malformed UTF-8/JSON and excess size or
depth before returning a document.

## Shared bindings

The descriptor and result carry the same `bindings` structure. The result is the
runner's snapshot for that same run/attempt, not a new identity. A
`hexalith.test-result.v1` document always snapshots `hexalith.test-environment.v1`
bindings, so neither records the other's version; a new descriptor version
requires a new result version. Immutable fields
must agree across both documents; readiness, startup tasks, observed loaded
artifacts, completion, actual durations and hold end records can advance as their
producers report observations. A later result cannot relabel a decision, reset an
attachment expiry, replace a prior terminal outcome or fabricate missing proof.
The runner and eventual evidence/attachment consumers must verify this agreement
and authenticate references; the offline reader validates one document at a time.

| Binding in `bindings` | Defined representation and responsibility |
| --- | --- |
| `run` | Required `runId`, distinct `attemptId`, issuer-ordered positive `attemptSequence`, `ownerRunId`, `environmentId` and `role` (`owner` or `attached`). Owners require `runId == ownerRunId`; attached runs require `runId == attachedRunId` and differ from the owner. `attachedRunId` is the attached role's explicit alias of the producing run, not a second run. The runner issues identities and preserves producing attempts. |
| `environment.composition`, `mode` | Composition `id` and `contentIdentity`, or explicit `not-yet-populated`; `source` or `package` artifact mode. The runner must resolve known identities against the actual composed resources. |
| `environment.executionContext` | `kind: local`, or `kind: ci` with repository, exact `workflowRunId`, positive `workflowAttempt`, `jobId`, `runnerKind`, `operatingSystem` and `disposable`. Declaring CI does not authenticate the job. Candidate acceptance requires disposable Linux GitHub-hosted package-mode CI; attachment must remain in the owner's exact job. |
| `environment.catalog` | Exact catalog `commit` and run-specific `instanceId`, or explicit `not-yet-populated`. The runner binds known values to the selected catalog instance. |
| `environment.isolation` | Reserved `syntheticTenantId`, `dataIsolationId`, `resourceScopeId` and `catalogScopeId`. The runner supplies synthetic, run-scoped data isolation; none identify hosted customer data. |
| `environment.resources` | Per-resource `resourceId`, owning `moduleId` and `endpoints` with `name`, `protocol` and absolute `uri`. The reader additionally requires every endpoint to be representable as an absolute `System.Uri`, refusing invalid hosts or ports atomically as `HXT104`; HTTP, IPv6, gRPC and custom schemes remain supported. Endpoint URIs exclude userinfo, query strings and fragments; credentials travel through a separate channel and must never enter retained snapshots. Resource IDs must be unique within the environment, and endpoint names must be unique within their resource (ordinal, case-sensitive comparisons). The same endpoint name may appear on different resources. Tests consume these endpoints and never start AppHosts. Empty endpoint lists can describe non-network resources. |
| `environment.readiness` | Reserved overall `status` (`pending`, `ready`, `failed`), `observedAt` and per-resource status/time/`diagnosticIds`. Each resource ID occurs at most once and resolves to `environment.resources`. The runner observes readiness, rather than inferring it from a successful descriptor read. |
| `environment.startupTasks` | Reserved task list with unique `taskId`, `required`, status (`pending`, `running`, `passed`, `failed`, `cancelled`), optional actual `startedAt`/`finishedAt`, and diagnostics. Attachment and evidence consumers require every required task to pass. |
| `tool` | Reserved actual executing `name`, `version`, `contentIdentity` and three independently reserved qualifications: `buildsAdoption`, `platformAcceptance`, `integrationQualification`. Each qualification requires a `referenceId`, exact tool version/content identity and exact policy. Builds owns adoption; Platform owns acceptance/qualification. A reference's authenticity and applicability are checked downstream. |
| `candidate` | Reserved classification: `baseline-only` requires a diagnostic `reason`; `candidate` requires repository, `moduleId`, exact `committedRevision` and optional structured `pr`. PR merge evidence must populate `pr.id`, exact `headCommit`, `baseCommit`, `mergeResultCommit`, with the merge result equal to the candidate revision. Epic 3 supplies and verifies this proof; an unpopulated classification grants none. |
| `decision` | Reserved trusted `requestId` and issuer `id`/`authenticationReference`, repository/module/policy, `kind` (`pr-merge` or `release`), exact `targetCommit`, `requestState` (`pending`, `completed`, `closed`), producing run/attempt and ordered attempt sequence. `pr-merge` requires structured PR/head/base/merge-result identity. The gate authenticates immutable bindings, selects only the latest pending attempt, and issues a fresh identity for reopened/new decisions. |
| `reuseLineage` | Reserved producing run and decision identities; `producingDecisionId` references `decision.requestId`. Requires `reuseAllowed: false` and explicitly empty `sourceRunIds` and `sourceDecisionIds`. No cross-decision evidence reuse is representable in v1. Missing producer values remain unpopulated and cannot qualify. |
| `artifacts.expected` | Reserved `items`, each identifying artifact/module/kind, version, repository, committed revision, `configuration` (`Debug` or `Release`) and content identity. Candidate artifacts must come from that revision's own Release build; other modules remain catalog-pinned. |
| `artifacts.loaded` | Reserved observed `items`, each binding artifact/module/content identity to `consumerKind` (`composed-host`, `mcpcli`, `tests`) and `consumerId`. Epic 3 must supply all consumed identities and verify expected/loaded content matches; matching versions alone never suffice. |
| `suite.declared` | Reserved `suiteId`, suite/configuration content identities, required-test-set content identity and unique `requiredTestIds`. The declaration producer records the exact intended suite/configuration. |
| `suite.completion` | Reserved executed suite/configuration and required-test-set content identities, `counts` for `required`, `executed`, `completed`, `passed`, `failed`, `skipped`, plus unique individual test IDs/outcomes. The execution/evidence consumer must match declared identities, reconcile all counts with individual results and a nonempty required set, and refuse skipped or incomplete required coverage. |
| `attachments` | Reserved `holds` list with unique `originalAttachmentRequestId` and unique `attachedRunId`, covering accepted attachments to this environment, including the current run when attached and any holds an owner must await. Every hold matches `run.environmentId` and `run.ownerRunId`, and its attached run differs from the owner. When populated for an attached run, the list includes its own `attachedRunId`. The runner accounts for every live hold, resolves reconnect lineage and verifies owner/environment/run/job compatibility before admission. |
| `deadlines` | Independent reserved `startup` and `execution` budgets described below. Neither changes a hold. |

Every populated candidate/decision/artifact/qualification/suite object is only a
representation of its producer's claim. The reader deliberately does not accept
candidate evidence, authenticate gate requests, reconcile suite results, compare
artifact content, contact an endpoint or admit an attachment. A `passed` result
is necessary but insufficient for candidate acceptance; all local and
`baseline-only` runs remain diagnostic. Downstream consumers must refuse missing,
mismatched or inapplicable proof, including declarations of CI or qualification
that do not match actual execution.

## Attachment holds and independent deadlines

Each accepted hold requires `originalAttachmentRequestId`, `environmentId`,
`ownerRunId`, `attachedRunId`, `acceptedAt`, `effectiveHoldSeconds`, `expiresAt`
and `end`. Only `requestedHoldSeconds` is optional:

| Original request | Required representation |
| --- | --- |
| Omitted | Omit `requestedHoldSeconds`; effective hold is exactly 1,800 seconds. |
| Explicit whole seconds 1–14,400 inclusive | Preserve `requestedHoldSeconds`; effective hold equals it exactly. An explicit 1,800 remains distinguishable from omission. |
| Null, zero, negative, fractional, string/empty, malformed, non-finite or over 14,400 | Refuse; never default or clamp an invalid request. |

The original runner acceptance establishes immutable `acceptedAt` and
`expiresAt = acceptedAt + effectiveHoldSeconds`. Schema validation enforces the
request's type/range and the omitted default; the reader additionally verifies
explicit request/effective equality and timestamp arithmetic. `end` is either
`{"state":"active"}` or `{"state":"ended","endedAt":...,"reason":...}`.
Actual end cannot precede acceptance and never moves the original expiry. An
`expired` end cannot precede `expiresAt`; every other end reason must precede
`expiresAt`, since expiry wins once that instant is reached. An own terminal
outcome can end the hold at acceptance itself. End
reasons cover own terminal outcome, expiry and the policy's owner/developer/CI
withdrawal causes. Expiry is exclusive: the runner's UTC clock reaching
`expiresAt` ends active access, regardless of a process's later success report.

The runner records and authenticates the original request lineage, bound to
owner/environment, and recognizes every retry/reconnect presenting it or its
issued attached-run identity. Such retries preserve the first acceptance/expiry;
unknown identities or expired lineage fail. A fresh request gets a new identity
and finite hold only after new admission checks. Single-document validation
cannot establish historical immutability, compatibility or owner state.

`deadlines.startup` when populated records `origin: environment-requested`,
`originAt`, `effectiveSeconds`, `expiresAt`, optional `actualDurationSeconds` and
`override`. The default is 600 seconds from the environment request, excluding
execution. `override` is `not-overridden`, or `declared` with finite positive
`requestedSeconds` and nonblank `justification`; effective seconds must equal the
override. A populated `deadlines.execution` records
`origin: test-execution-started`, its own origin/time/effective seconds/expiry,
optional actual duration and the configuration content identity that supplied
it. A `not-declared` execution can retain its origin/time and actual duration
without any timeout or expiry. The reader verifies both absolute deadline
calculations independently using exact integral 100-nanosecond ticks. Positive
budgets and startup overrides must be exact multiples of 0.0000001 seconds and
fit within signed 64-bit ticks; smaller, fractional-tick, underflowing or
unrepresentable budgets are refused without rounding. Decimal and exponent JSON
spellings representing the same supported budget compare exactly.
Neither execution retries nor startup/execution deadlines extend attachment
expiry. A hold may end before execution's independent deadline.

## Terminal results, termination and resource disposition

`terminal.outcome` permits precisely `passed`, `failed`, `cancelled`, `withdrawn`.
`terminal.recordedAt` is the UTC time of the first recorded terminal outcome;
`cause` records tests completing/failing, startup failure/timeout, execution
timeout, cancellation or the policy withdrawal cause. `diagnosticIds` bind its
classified diagnostics. The schema couples each outcome to its valid causes:

| Outcome | Allowed causes |
| --- | --- |
| `passed` | `tests-completed` |
| `failed` | `test-failure`, `startup-failure`, `startup-timeout`, `execution-timeout` |
| `cancelled` | `run-cancelled` |
| `withdrawn` (attached role only) | `hold-expired`, `owner-cancelled`, `developer-stop`, `cleanup`, `ci-job-completed`, `ci-job-cancelled` |

The runner must durably record the first outcome
immediately, including an owner with live attachments. Later cleanup, failure
retention, cancellation or withdrawal must never replace it. Expiry or environment
withdrawal turns an active attached run into `withdrawn`; no late pass can undo
that outcome. An attached run's own cancellation affects only that run.

For an attached result with populated `attachments`, the reader checks the
outcome against the run's own hold. Because expiry is exclusive and the runner's
record decides it, `cause` is `hold-expired` exactly when `recordedAt` is at or
after that hold's `expiresAt`; no other outcome or withdrawal cause can be
recorded then. An ended hold's `reason` must match the outcome: `terminal-outcome`
for `passed`, `failed` or `cancelled`, `expired` for `hold-expired`, and each
withdrawal reason for the same withdrawal cause. A still-`active` hold record may
lag the outcome. Contradictions refuse as `HXT107`; owner outcomes are
independent of the holds of other runs.

The following result objects are required reserved fields, so a terminal outcome
can be recorded before later stopping and cleanup finish:

| Field | Defined representation and responsibility |
| --- | --- |
| `termination` | `ownedWork` and `environmentAccess` item lists with ID, `status` (`pending`, `terminated`, `failed`, `not-applicable`), observation time and diagnostic IDs; explicit `leftoverWorkIds` and `failures`. Item IDs are unique across both lists; every failure ID resolves to an item in those lists. The runner must stop owned work/end access on expiry, withdrawal or cancellation, preserve other runs, report failures honestly and retry without extending holds. |
| `retention` | `disposition` (`not-retained`, `failure-retained`, `released`), `failureRunIds`, `retainedResourceIds`, reason and UTC record time. Local owner/attached test/startup failures retain surviving resources through later cancellation/expiry until explicit cleanup; retained resources grant no continued attachment access. CI never applies local failure retention. |
| `cleanup` | `disposition` (`not-requested`, `pending`, `in-progress`, `incomplete`, `complete`), cleanup attempt ID (optional for `not-requested`, required for a represented `pending`, `in-progress`, `incomplete` or `complete` attempt), observation time, owned/leftover resource IDs, failures and diagnostic IDs. Every failure ID resolves to `ownedResourceIds` in that cleanup object; explanations use the separate classified `diagnosticIds`. The runner performs idempotent, retryable, ownership-limited deletion and must never claim incomplete deletion is complete. |
| `diagnostics` | References with ID, classification (`public`, `internal`, `restricted`), `redacted: true`, reference and content identity. Producers classify/redact content and preserve results/diagnostics after resource deletion; a declared redaction flag alone does not sanitize content. Top-level diagnostic IDs must be unique, and every `diagnosticIds` entry anywhere in the document must resolve to one of these classified diagnostics. References are opaque and their contents are never fetched by the reader. |

Terminal outcomes are independent of cleanup disposition. Owner success waits
for all holds before automatic local cleanup. CI completion/cancellation withdraws
attachments before cleanup even when holds remain; CI cleans after all outcomes
and partial startup. Ready local failure-retained environments are the policy's
explicit exception for new attachments after owner completion/cancellation;
explicit stop/cleanup ends that exception. These lifecycle and evidence checks
belong to later runner stories, not schema validity.

## Offline Builds consumption and refusals

Use `TestEnvironmentContractReader.ReadDescriptor(path)` or
`ReadResult(path)` from `Hexalith.Builds.Tooling.TestEnvironment`.
`TestEnvironmentContractReadResult` returns a detached `JsonElement` only after
all parsing, version, policy, shape and timing checks pass. Any refusal returns
`Document: null` and stable metadata-only diagnostics; no partial values escape.
Cancellation propagates as cancellation. Schemas are embedded in Tooling and
packaged under `tools/net10.0/any/schemas/`; remote schema retrieval is disabled.
No source checkout is needed by the installed reader. Tooling is not published as
a standalone package: the reader runs inside the installed `hexalith-module` tool,
and module fixtures that validate descriptors themselves use the published schemas.

| Diagnostic | Meaning |
| --- | --- |
| `HXT101` | Missing/unsupported/wrong-kind contract version; names supported versions. |
| `HXT102` | Missing/unknown/wrongly typed policy; names the exact supported policy. |
| `HXT103` | Invalid strict JSON/UTF-8, duplicate keys, non-finite numbers, excessive depth or hold/deadline/attached-outcome timestamps that cannot be represented by the reader. |
| `HXT104` | Closed schema shape/type/value violation, non-plain/out-of-range Int32, inconsistent run/hold identities, duplicate scoped identities or unresolved document-local resource/item/diagnostic references. |
| `HXT105` | Input exceeds the byte bound or is not a seekable file. |
| `HXT106` | Invalid/unreadable file. |
| `HXT107` | Inconsistent request/effective hold, acceptance/expiry/end or deadline/default/override bindings, including unsupported deadline tick precision, or an attached run's terminal outcome contradicting its own hold expiry or end. |

Diagnostic source is the requested contract identity, field is a fixed metadata
location, and messages never echo input values, unknown keys, file paths,
exception messages or contents. Refusal is atomic; diagnostic enumeration and
messages do not depend on embedded secrets in invalid input.

Stories 2.3–2.10 own isolation, provisioning, attachment admission, withdrawal,
retention, cleanup, CI, fixture adoption and lifecycle qualification. Epic 3 owns
candidate selection, gate authentication, latest-attempt acceptance, no-reuse
checks, catalog/composition identity and loaded-artifact proof. Existing native
environment-variable handoff and internal run records remain separate transports;
this contract does not migrate them or the executable module descriptor ABI.

## Runner handoff

For declared v2 suites, `hexalith-module test --profile <suite>` now provisions
a fresh owned environment, observes readiness and successful required startup
tasks, and writes a private validated descriptor before launching tests.
Repeated `--profile` values from one manifest share that environment only when
mode, composition and profile-specific resource/runtime requirements agree;
execution is sequential. Multi-entry batches refuse legacy `--evidence` export
before provisioning because that format represents only one timing/report/count
set; normal batch execution retains each inspected diagnostic report. Tests
consume absolute `HEXALITH_TEST_ENVIRONMENT_PATH` and the observed endpoint
list. Credentials remain in the existing separate private channel and never
enter this descriptor. Every resource needs explicit ownership from an actual
declaration. Shared runner infrastructure belongs to the active selected module's
declaration, falling back to the selected suite's declaring module when no active
module is set. Servers and Memories stores retain their own declaring modules.
EventStore, Tenants and Memories closures therefore carry ownership without a
separate module named `platform`; unavailable declaration ownership still refuses
handoff.

The startup clock begins at the environment request (600 seconds by default,
with justified finite declaration overrides). A non-600 caller fallback requires
an existing justified declaration override before provisioning. Unsupported or
incomplete actual CI metadata also refuses before services start; this handoff
supports local context and complete actual GitHub Actions context without
inventing metadata. Actual execution timing activates and is persisted after
readiness, separately for each entry; a compatible batch also has a finite
combined limit covering execution and each entry's stopping/processing
allowance. Immutable source selection and legacy evidence bindings are
preserved. Stopping and cleanup use the active entry's declared bound;
descriptor/report archives use bounded validated snapshots within that same
original budget. A missing verified catalog identity, source drift, invalid
endpoint observation or descriptor validation failure prevents child launch.
Managed exclusive-resource conflicts identify the blocking owner; independent
fresh runs can coexist. No implicit attachment is performed.

The source qualification fixture is
`eng/test-environment-provisioning/manifest.v2.json`. Its gated Platform test
runs one declared suite in two concurrent fresh environments, consumes each
validated descriptor, persists identical keys through each owned Redis endpoint,
verifies its own value and absence of the peer's unique key, and cleans only its
recorded run identities. This local proof supplies
no package-mode CI, candidate or tool acceptance credit.
