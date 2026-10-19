# Works host parity and rollback

Story 4.16 qualification is **in progress** until independent review accepts it.
The accepted 4.15 evidence remains preserved. A passing supporting build or an
individual live scenario does not accept the complete Works gate. The retained
Works AppHost and ServiceDefaults stay usable until 4.16 acceptance and the
subsequent 4.9 cutover.

Public 3.124.0 failed automatic named projection catch-up; its
[public failure record](../../works/_bmad-output/implementation-artifacts/evidence/story-4-16/root-acceptance-2026-10-16/public-3.124.0/public-r4-blocker.json)
is retained. Public EventStore 3.125.1 (tag `v3.125.1`, producer
`55432c50d4b6f1666b4bd6d4a60bfc263b2d0e0b`) contains both producer corrections.
The first canonical 3.125.1 run on 2026-10-18 is
[historical supporting evidence](../../works/_bmad-output/implementation-artifacts/evidence/story-4-16/public-3.125.1-2026-10-18/README.md)
only. Review iteration 1 found that it replaced Platform's own Works hosting
startup, ran at fixture-injected rate limits, declared per-row proofs from phase
names and probed mTLS without a server-side refusal. Only a fresh canonical run
on the corrected graph can qualify this gate.

That fresh run exited 0 on 2026-10-18 with status
`passed-disposable-synthetic-parity-and-rollback`. All eleven rows derived their
four proofs from field-bound phases, with zero skips, nine Platform starts and one
retained-host rollback start; every Platform `works` and `works-ha` host loaded
Platform's hosting startup first. The
[corrected-graph result](../../works/_bmad-output/implementation-artifacts/evidence/story-4-16/review1-corrections-2026-10-18/canonical-admission/result.json)
retains the command, ledger, logs and outcome records. Acceptance stays with
independent review.

Run from the Platform checkout with a complete Works source checkout and an exact
export of the qualified producer tag:

```bash
export SDK=/tmp/works-parity-sdk-3.125.1
mkdir "$SDK"
git -C ../works/references/Hexalith.EventStore archive v3.125.1 | tar -x -C "$SDK"
# git archive applies eol attributes; restore the one rewritten blob byte for byte.
git -C ../works/references/Hexalith.EventStore cat-file blob v3.125.1:global.json > "$SDK/global.json"
./eng/verify-works-host.sh --works-root ../works --runtime-sdk-root "$SDK" --artifacts-path /tmp/works-parity-new-run
```

`--runtime-sdk-root` is required while the Works EventStore gitlink is `v3.125.0`
(`f43b121c`): without it the verifier qualifies the gitlink source against the
3.125.1 archives and refuses the two Aspire hosting files that differ. The export
must come from the root-declared SDK checkout's object store; it never checks out
or mutates that shared checkout.

Choose a fresh, empty artifacts directory for each attempt. The verifier records
each exact command, working directory, exit result and log hash in `ledger.json`,
including the first failing command. It builds the actual `apphost.cs` file entry
and a separate Works qualification consumer. Live tests require that exact host
assembly, its SHA-256 and the selected Platform root. They cannot fall back to the
retained Works host. Only the explicit rollback phase selects that host.

`--runtime-sdk-root /absolute/producer-source` selects an explicit SDK source
directory, including an extracted official producer archive. Admission compares
every `src/**` file except `bin`/`obj` build outputs, plus the three central build
files, by path and Git blob with the official commit trees bound to the restored
public packages and the named `v<version>` tag. Props, targets, JSON and resource
inputs are qualified like C# sources. Modified, missing, extra, linked or foreign
producer source fails; a matching version label alone cannot qualify it.

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
and the existing local Dapr prerequisites checked by the harness. The two-host trust
probe also needs the host `openssl` CLI (OpenSSL 1.1.1 or later): the owned Sentry
issues Ed25519 credentials, and .NET cannot sign the probe's client certificate with
them. The control-plane
ports 50001, 51005 and 51006 must be available for this serialized topology.
Application and sidecar endpoints are isolated from unrelated running sessions.
Each live start also gives every sidecar a run-owned SQLite name-resolution
registry derived from its exact configuration. Default mDNS discovery would
round-robin app IDs such as `eventstore` with an unrelated local session's
sidecar; on 2026-10-18 that cross-talk failed every other Works invocation with
a TLS handshake error. A direct developer run of the preview outside this fixture
still uses default mDNS discovery. The fixture tears down only its owned resources;
it does not stop foreign hosts.

Debug development consumers use source references. Release consumers restore the
published EventStore graph selected by the Platform Builds catalog, currently
3.125.1. The verifier reads that single stable selection. It refuses a Works
consumer whose default pin in either `Directory.Build.props` or
`Directory.Packages.props` differs, and a file host whose `#:package`
`Hexalith.EventStore.*` directives in `apphost.cs` differ. It binds the explicit producer source to the
official `v<version>` release tag as well as the commit and trees named by the
restored archives. The ledger records package archive hashes and
NuGet provenance, source consumers, host identity and the actual compatible runtime
inventory used during restore. Source-built producer executables retain their own
recorded build identity; their binaries are not relabeled as published archives.
The target qualification consumer selects the Dapr adapter from the current Builds
catalog in isolated output. It preserves the retained Works consumer's pin.
Platform.Identity remains an explicitly identified test-only source dependency.

## Required producer and consumer evidence

Every EventStore package below is qualified at public version **3.125.1**
(tag `v3.125.1`, producer `55432c50d4b6f1666b4bd6d4a60bfc263b2d0e0b`). The
executable ledger owns exact hashes, commands and outcome records; this table
describes the required evidence rather than asserting that a run has passed.

| Row | Published producer/API | Actual consumer | Required target evidence |
| --- | --- | --- | --- |
| R1 | EventStore.Aspire: `AddHexalithEventStore`, `AddEventStoreDomainModule` | Platform `WorksPreviewModel` | Actual file-host composition, durable lifecycle writes, complete process membership and receiver policies |
| R2 | EventStore.DomainService/ServiceDefaults: canonical host defaults and health | Works `WorksHost`; Platform readiness/runtime adapters | Disabled recovery remains Degraded; actual OTLP recovery alert appears and clears after convergence |
| R3 | EventStore.DomainService: protected subscription/delivery | Works domain-event adapter | Actual ingress redelivery, origin/publish refusal and unchanged accepted family |
| R4 | EventStore.Client: `SharedProjectionEpochCoordinator` | Works shared rebuild handler | All eleven source histories captured; old generation readable during Capture/Stage; fenced delivery; complete Commit and unchanged logical family/checkpoints; the focused two-rebuild automatic catch-up fact that failed on public 3.124.0 |
| R5 | EventStore.DomainService: `IDomainQueryHandler` | Works what's-next and item-detail handlers | Own assignment and queued work visible, foreign assignment hidden, wrong actor/tenant and unavailable identity denied; restart queries remain usable |
| R6 | EventStore.DomainService: `AddEventStoreReminders` | Works recovery domain adapters | Lost firing/reissue, stale callback refusal, two distinct Works hosts/sidecars with primary loss, exactly one logical effect and durable receipt |
| R7 | EventStore.DomainService: `AddEventStoreProcesses` | Works process translator/catalog | Linked candidate pages, complete coverage, independent owner fencing and restart checkpoints |
| R8 | EventStore.DomainService: `ProcessDispositionService`; Client authorizer/audit interfaces | Works protected disposition endpoint; Platform runtime providers | Exact source/operator bindings, durable attributed audit before mutation, audit outage refusal, exported recovery alerts |
| R9 | EventStore.DomainService: `AddEventStoreDomainService`, `UseEventStoreDomainService` | Works executable/domain registrations | Both SDK modes selected, protected callbacks persist SDK state; final executable simplification belongs to 4.9 |
| R10 | EventStore.ServiceDefaults workload/channel authentication | Platform ACLs/profile; Works SDK ingress | mTLS, deny defaults and exact routes, wrong port/caller/tenant/purpose/publish refusal, incomplete or foreign production configuration refused |
| R11 | EventStore.Client: `HttpTrustedEffectSubmitter` | Works process delegation/effect policy | Distinct target receipts, replay returns the original terminal result, no repeated logical resume/cancel/expiry |

The verifier requires fresh persisted phase records from live recovery, historical,
two-host and focused projection-rebuild qualification facts. Supporting in-process SDK/domain suites do not
substitute for those target-host records. Host-start records distinguish
`platform-target` from `retained-rollback`; both bind their actual binary and owned
Scheduler volume. One target start must bind the independent `works-ha` replica
module.

Each row's `persisted`, `negative`, `restart` and `rollback` proof maps to named
phases and the state fields each phase must carry with qualifying values, for
example a `404` legacy-route status or `foreignCaClientRefusedByServer=true`. The
verifier derives `requiredProofs` from that map and fails a row whose phase is
missing, duplicated, lacks a field or carries a non-qualifying value. Cross-phase
checks also bind the restart family digest to the persisted one, and the restored
fleet, runtime binaries and replayed receipt to the Platform-written snapshot.

- **R9 negative.** The live Works host answers a fully credentialed request to the
  retired `/project` route with the canonical SDK's named-mode `404`. An
  uncredentialed request gets `401`. Neither changes the accepted family.
- **R10 negative.** The trust probe connects to the Works sidecar's internal mTLS
  gRPC endpoint. A client chained to the owned trust anchors completes HTTP/2
  setup. The sidecar itself refuses a client without a certificate and a client
  certificate from a foreign CA. The probe validates the server's exact SPIFFE
  identity under the configured root, and it rejects the genuine server when it
  expects another application's identity.

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

`WorksPreviewModel` composes `WORKS_PLATFORM_RUNTIME=1`, the Platform runtime's
`DOTNET_STARTUP_HOOKS` path and `ASPNETCORE_HOSTINGSTARTUPASSEMBLIES=Hexalith.Platform.Works.Runtime`
on its `works` host. Live parity appends the synthetic proof assembly after those
values with each variable's separator (`:` and `;`); it never replaces them. So
`WorksRecoveryHostingStartup`, behind its `WORKS_PLATFORM_RUNTIME` gate, registers
the recovery telemetry before the proof runs, and the proof's later registration
adds nothing. Each proof host records which hosting startups loaded, whether
Platform's registered telemetry first, and the registration count. The verifier
refuses a `platform-target` start whose `works` or `works-ha` host lacks Platform's
hosting startup, runs it after the proof, or registers the telemetry more than
once. The HA replica copies Platform's composed `works` values; the fixture never
re-sets Platform-composed workload callers.

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

## Development/test rate limits

EventStore's default limiter (1,000 requests per minute per tenant and 100 per
second per consumer) rejects the preview's own internal actor, projection and
recovery traffic with `429`. A swallowed projection update then leaves the
pending-date index empty, which is an open EventStore producer defect tracked in
the Works deferred-work ledger. `WorksPreviewModel` therefore composes explicit
development/test limits on its `eventstore` host:

| Setting | Value |
| --- | --- |
| `EventStore__RateLimiting__PermitLimit` | 8192 |
| `EventStore__RateLimiting__ConsumerPermitLimit` | 8192 |
| `EventStore__RateLimiting__TenantPermitLimits__anonymous` | 8192 |
| `EventStore__RateLimiting__ConsumerPermitLimits__anonymous` | 8192 |

These values carry the eleven-source parity load and keep both SDK limiters
finite. Parity runs at exactly these Platform-composed limits; the fixture injects
none. The retained Works AppHost keeps its own proof-topology limits (100000).
Production admission stays refused and must size its own limits after the
EventStore defect is fixed.

## Production profile policy bindings

`WorksProductionProfile` requires `brokerPolicySha256` and `networkPolicySha256`
but only checks that each is a 64-character hexadecimal value. No broker or
network policy file exists for these hashes to match yet. The owner deferred that
binding to the AD-28 production gate on 2026-10-18; that gate must supply the
committed policy files. Until then the hashes are format-checked only. Production
admission stays refused regardless, and development values for `trustDomain` and
`namespace` are refused in any letter case.

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
The Scheduler inventory binds every entry's type, mode, owner and link target,
plus every regular file's content hash.
The original Redis serialization supplies restoration bytes. Complete binary
hash fields/values and set members use stable ordering for verification; hash-field
absolute deadlines are included. Other values retain strict serialization checks,
including stream consumer groups and pending deliveries. Startup reuses captured
component files so retained templates cannot overwrite restored configuration.
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
