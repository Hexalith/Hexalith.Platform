# Works host parity and rollback

Story 4.16 qualification is **in progress**. The accepted 4.15 evidence remains
preserved. A passing supporting build or an individual live scenario does not
accept the complete Works gate. The retained Works AppHost and ServiceDefaults
stay usable until 4.16 acceptance and the subsequent 4.9 cutover.

Run from the Platform checkout with a complete Works source checkout:

```bash
./eng/verify-works-host.sh --works-root ../works --artifacts-path /tmp/works-parity-new-run
```

Choose a fresh, empty artifacts directory for each attempt. The verifier records
each exact command, working directory, exit result and log hash in `ledger.json`,
including the first failing command. It builds the actual `apphost.cs` file entry
and a separate Works qualification consumer. Live tests require that exact host
assembly, its SHA-256 and the selected Platform root. They cannot fall back to the
retained Works host. Only the explicit rollback phase selects that host.

`--prepare-only` exits successfully when preparation passes, with status
`preparation-passed; parity-incomplete`. Acceptance requires the full command to
exit zero, status `passed-disposable-synthetic-parity-and-rollback`, all eleven
rows passed, complete rollback, all required persisted phases, and executed test
inventories with zero failures, skips, errors or missing methods. An incomplete
ledger remains incomplete even when some supporting checks pass.

## Scope and prerequisites

This gate uses disposable synthetic data, an owned Redis container, a dedicated
Scheduler volume and generated local mTLS certificates. It requires Docker,
the catalog-compatible .NET/Aspire/Dapr tools, restored root-declared dependencies,
and the existing local Dapr prerequisites checked by the harness. The control-plane
ports 50001, 51005 and 51006 must be available for this serialized topology.
Application and sidecar endpoints are isolated from unrelated running sessions.
The fixture tears down only its owned resources; it does not stop foreign hosts.

Debug development consumers use source references. Release consumers restore the
published EventStore 3.123.0 graph. The ledger records package archive hashes and
NuGet provenance, source consumers, host identity and the actual compatible runtime
inventory used during restore. Source-built producer executables retain their own
recorded build identity; their binaries are not relabeled as published archives.
The target qualification consumer selects the Dapr adapter from the current Builds
catalog in isolated output. It preserves the retained Works consumer's pin.
Platform.Identity remains an explicitly identified test-only source dependency.

## Required producer and consumer evidence

Every EventStore package below is qualified at public version **3.123.0**. The
executable ledger owns exact hashes, commands and outcome records; this table
describes the required evidence rather than asserting that a run has passed.

| Row | Published producer/API | Actual consumer | Required target evidence |
| --- | --- | --- | --- |
| R1 | EventStore.Aspire: `AddHexalithEventStore`, `AddEventStoreDomainModule` | Platform `WorksPreviewModel` | Actual file-host composition, durable lifecycle writes, complete process membership and receiver policies |
| R2 | EventStore.DomainService/ServiceDefaults: canonical host defaults and health | Works `WorksHost`; Platform readiness/runtime adapters | Disabled recovery remains Degraded; actual OTLP recovery alert appears and clears after convergence |
| R3 | EventStore.DomainService: protected subscription/delivery | Works domain-event adapter | Actual ingress redelivery, origin/publish refusal and unchanged accepted family |
| R4 | EventStore.Client: `SharedProjectionEpochCoordinator` | Works shared rebuild handler | All eleven source histories captured; old generation readable during Capture/Stage; fenced delivery; complete Commit and unchanged logical family/checkpoints |
| R5 | EventStore.DomainService: `IDomainQueryHandler` | Works what's-next and item-detail handlers | Own assignment and queued work visible, foreign assignment hidden, wrong actor/tenant and unavailable identity denied; restart queries remain usable |
| R6 | EventStore.DomainService: `AddEventStoreReminders` | Works recovery domain adapters | Lost firing/reissue, stale callback refusal, two distinct Works hosts/sidecars with primary loss, exactly one logical effect and durable receipt |
| R7 | EventStore.DomainService: `AddEventStoreProcesses` | Works process translator/catalog | Linked candidate pages, complete coverage, independent owner fencing and restart checkpoints |
| R8 | EventStore.DomainService: `ProcessDispositionService`; Client authorizer/audit interfaces | Works protected disposition endpoint; Platform runtime providers | Exact source/operator bindings, durable attributed audit before mutation, audit outage refusal, exported recovery alerts |
| R9 | EventStore.DomainService: `AddEventStoreDomainService`, `UseEventStoreDomainService` | Works executable/domain registrations | Both SDK modes selected, protected callbacks persist SDK state; final executable simplification belongs to 4.9 |
| R10 | EventStore.ServiceDefaults workload/channel authentication | Platform ACLs/profile; Works SDK ingress | mTLS, deny defaults and exact routes, wrong port/caller/tenant/purpose/publish refusal, incomplete or foreign production configuration refused |
| R11 | EventStore.Client: `HttpTrustedEffectSubmitter` | Works process delegation/effect policy | Distinct target receipts, replay returns the original terminal result, no repeated logical resume/cancel/expiry |

The verifier requires fresh persisted phase records from live recovery, historical
and two-host qualification facts. Supporting in-process SDK/domain suites do not
substitute for those target-host records. Host-start records distinguish
`platform-target` from `retained-rollback`; both bind their actual binary and owned
Scheduler volume. Each row also binds the completed restore observation.

## Readiness, telemetry and privileged operations

Platform preserves the SDK's Healthy, Degraded and Unhealthy readiness meanings
for both its default text writer and Development JSON writer. HTTP 200 alone
cannot promote Degraded to healthy. Operations can start while Works recovery is
degraded, so operators retain diagnostic access.

The Platform-owned runtime publishes cached SDK process-health values on the
existing `Hexalith.EventStore.Domain.work` meter. Instruments use prefix
`eventstore.domain.recovery.`: unresolved, pending disposition/publication/
transition/observation counts, retained candidates, completeness, capacity,
availability, readiness and `alert`. `alert=1` denotes degraded, unavailable or
stale recovery; healthy fresh coverage emits zero. Samples older than fifteen
seconds alert. Reason labels come from a fixed allowlist; unknown reasons collapse
to `other`. Tenant IDs, item IDs, payloads and credentials are not metric labels.
The disposable loopback collector verifies actual OTLP HTTP/protobuf exports,
retains only these gauges and bounded reason labels, and rejects oversized or
malformed requests.

Ordinary Platform runtime injection installs telemetry only. Synthetic disposition
authority additionally requires Development, `WORKS_SDK_PROOF=1`,
`WORKS_PLATFORM_PARITY=1` and 1..64 explicit canonical source grants. There is no
discovered or wildcard authority. Protected HTTP admission independently requires
the receiver's sidecar channel and a fresh signed operator assertion. The provider
then requires the named operator workload, `process:dispose`, exact tenant/domain/
aggregate/purpose bindings and an attributed actor. The ordinary endpoint policy
also requires `domain-service:process`; Dapr grants the operator only the exact
disposition route.

Audit uses the SDK's durable CAS store, with an attributed attempt written before
the SDK prepares any disposition mutation. Audit failure or exhausted bounded
capacity refuses mutation. These disposable providers and the authenticated
synthetic Parties identity service do not qualify production identity, custody,
delegation or audit providers. Do not copy their generated credentials into an
operational deployment.

## Complete fleet rollback

The drill first performs Platform writes and verifies terminal receipts, query
results and complete recovery coverage. It stops the owned topology before
capturing every Redis key/value and original expiry, offline Scheduler contents,
component/receiver/resiliency configuration, certificate/key material and the
compatible runtime deployment inventory. Generated authority and digest-key bytes
remain in the protected transient fixture snapshot; committed evidence contains
only safe hashes and synthetic outcomes.

Restore reproduces the full owned key inventory and values, removes a
post-snapshot sentinel, preserves expiry semantics, and verifies Scheduler,
configuration, credential and runtime bytes against their captured inventories.
The retained Works host then starts with that restored compatible fleet. Protected
queries must remain usable, source replay must converge, and reissued trusted
effects must replay their original receipts without another logical effect.
Projection-only restore, a changed receiver policy, or a mixed binary/state fleet
cannot qualify this drill.

This synthetic exercise does not admit real data or new catalogs. AD-28 restore
and approval gates still apply before that admission. Production Works preview
composition refuses even a structurally valid profile until the Maintainer's
time-bounded exception and qualified provider bindings exist.

## Agents and cutover boundaries

The user-approved 4.16 contract preserves the default Agents Full refusal:

```text
./eng/verify-agents-host.sh
exit 1: DependencyNotAvailable: EXT-HOST-1
```

`--mode LocalScaffold` must pass. Full Agents qualification remains unavailable
and is not a prerequisite for this independent Works gate. Its refusal is never
recorded as a successful Full qualification. See the
[Agents owner contract](ext-host-1-agents-composition.md).

After independent review accepts every required Works row and rollback, synchronize
the [4.16 story](../../works/_bmad-output/implementation-artifacts/spec-4-16-prove-platform-works-parity-and-rollback.md)
and sprint status. Only then resume
[4.9](../../works/_bmad-output/implementation-artifacts/spec-4-9-migrate-works-hosting-to-the-platform-boundary.md),
which reruns capture-through-Commit, restart, redelivery and security checks at
cutover before removing Works-owned hosting projects.
