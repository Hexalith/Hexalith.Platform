# Reference-composition Helm qualification

The selected application package is the maintained chart in `chart/`. The native
probe uses Aspire **13.6.1**, `Aspire.Hosting.Kubernetes`
**13.6.1-preview.1.26506.6**, and the catalog Dapr integration
**13.6.1-beta.935**. These are the actual tools, replacing the story's historical
13.5.4 identity. No dependency upgrade is part of this qualification.

Native publishing succeeds but drops the `WithDaprSidecar` annotation from all
seven generated Pod templates. Preserving module Dapr plumbing would require a
separate Kubernetes model, which is the AD-1 fallback trigger. `qualify.py`
repeats that measurement, requires all seven native workloads to exist, retains
the generated files and render, and records their hashes and missing annotations
in `exportDecision`. A build failure or incomplete probe fails qualification.
No generated native files are patched.

`PlatformApplicationGraph` selects the actual Parties declaration and its
EventStore, Tenants and Memories closure. `PlatformServerBindings` consumes that
same neutral metadata before applying its existing local profile.
`PlatformDaprBindings` supplies capability names, consumers, environment keys,
receiver ACL operations and subscriptions to both paths. Hosted selection never
calls `PlatformLocalProfile`, creates local providers, or generates credentials.
The chart's `reference-graph.json` is a serialized metadata input from that graph,
not a second hand-written topology. Its parity with current declarations is
checked by the C# tests and every native probe. To update it after a declaration
change, run the same probe with `Platform__Hosted__GraphPath` pointing at that
file. The maintained chart remains generic.

The application owns Deployments, Services, HTTPRoutes and Dapr binding objects
(Components, Configurations and Subscriptions). Its generic component template
binds hosted provider types to endpoint parameters and existing credential
references; scopes come exclusively from the graph. Endpoint metadata can use
`endpointFormat: authority` for host:port providers or `url` for URL providers;
hosted Redis bindings require `enableTLS: "true"`. The environment owns the
underlying data services, Gateway/listeners, namespace, identity authority,
credential Secrets, registry credential, component-file ConfigMap and persistent
infrastructure. The application chart creates none of those prerequisites.
Existing component manifest files are mounted read-only from the environment
ConfigMap on every workload, so that ConfigMap is a prerequisite for every Deployment.
The environment writer supplies its control-plane trust domain and a non-secret
Sentry endpoint parameter. Each public workload requires a hostname in
`hostnames["module/server"]`; the reference EventStore and Memories `/api/v1`
backends therefore use distinct hosts. Identical normalized host/listener/path
matches targeting different backends fail. Route templates use their literal path prefix before
the first parameter, so Gateway PathPrefix contains valid URI path characters.
The environment writer must supply provider metadata and file contents consistent with its selected bindings.

Each enabled workload requires `images["module/server"]` as a full
`repository@sha256:<64 lowercase hex>` reference, plus all declared input bindings
under `bindings["module/server"].configuration`. Secret and dynamic-secret inputs
accept `{name, key}` references only. A non-secret input can be a string, a JSON
object/array string (flattened into .NET configuration keys),
`{endpoint: "parameter-name"}`, or `{configMapKeyRef: {name, key}}`.
Nested endpoint and ConfigMap reference leaves resolve recursively, including
Clock Sources arrays. Literal HTTPS leaves use the same endpoint validation.
Structured JSON is decoded before validating keys and values; escaped credential
keys cannot bypass the reference-only requirement. Configuration/environment keys
are compared case-insensitively, matching .NET, and equivalent duplicates fail.
Structured secret leaves can be supplied as additional existing references in
`bindings["module/server"].credentials`, keyed by flattened environment name (for
example `Clock__Sources__0__AuthenticationToken`). They cannot override canonical
Dapr/security bindings or declared channel/API-token inputs. Distinct app and API
Secret references are supported and must agree with their respective declared inputs.
Credential literals inside structured configuration fail.
Endpoint parameters permit HTTPS or TLS data-service schemes and reject embedded
credentials, fragments and loopback/local endpoints. PostgreSQL requires the single
query `sslmode=verify-full`, `sslmode=require` or `sslmode=verify-ca`. Scheme-less
`host:port[,option…]` inputs require canonical non-loopback IPv4 or DNS authorities,
an explicit port in 1..65535 and `ssl=true`; `ssl=false` is rejected. Sentry and
all `endpointFormat: authority` bindings require explicit ports. Dapr token Secret
references use the `token` key and must agree with the owner's channel inputs.
No plaintext credentials belong in values. `fixture_values()` supplies synthetic
admission-test inputs; it is not a deployable staging profile.

Normal mode has serving replicas and declared public routes. Recovery sets every
workload to zero replicas and removes every HTTPRoute. Remove-workloads omits
Deployments, Services and routes while retaining application Dapr bindings.
Unknown modes fail. Dapr HotReload remains disabled in every mode. These modes do
not implement data restoration.

Run from the Platform root with the repository's .NET test runner:

```bash
DirectoryPackagesPropsPath="$PWD/eng/helm-qualification/source-packages.props" \
  dotnet test --project tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj \
  -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:RunAnalyzers=false
HELM_BIN=/absolute/path/to/verified/helm python3 -m unittest discover \
  -s eng/helm-qualification -p 'test_*.py' -v
python3 eng/helm-qualification/qualify.py
```

`toolchain.json` pins Helm 4.2.0, kind, the digest-qualified Kubernetes node image,
Gateway CRDs and the Builds-catalog Dapr patch. Downloaded CRDs and the Dapr chart
must match the recorded SHA256 values. The qualifier downloads Helm from its pinned
distribution URL, verifies the archive SHA256, extracts its binary into its private
directory and supplies that binary to the chart tests. Every transfer requires curl
and bounds the whole download with a deadline, including response bodies. It records
the Helm binary hash. Missing tools or failed checks return nonzero;
there are no skipped qualification checks.

The qualifier creates a unique `h45-*` kind cluster and private kubeconfig, passes
that kubeconfig and context to every Kubernetes/Helm operation, and never reads
the ambient kubeconfig. It installs the pinned Dapr control plane and Gateway
CRDs, enforces restricted admission in the application namespace, and submits an
actual Pod for each rendered Deployment template with server dry-run. Both the
real templates and unsafe control use the observed ReplicaSet-controller service
account UID and groups, matching the Deployment admission caller. The injector
[authorizes these controllers](https://docs.dapr.io/concepts/dapr-services/sidecar-injector/);
the environment-owned Dapr injector must set `sidecarRunAsNonRoot=true`,
`sidecarRunAsUser=1654`, `sidecarReadOnlyRootFilesystem=true` and
`sidecarDropALLCapabilities=true`. These are prerequisites for restricted admission,
and the qualification fixture installs Dapr with those settings. It requires
injected `daprd`, inspects every regular/init container's security settings and
requires an unsafe Pod rejection. A Deployment dry-run does not satisfy this
check.

It installs and upgrades the packaged archive using `--server-side=true`, requires
Helm `Apply` managed fields, demonstrates refusal of a deliberately conflicting
writer without Helm force flags, then verifies recovery and workload removal.
Only the synthetic rival writer uses `--force-conflicts` to set up the conflict.
Environment fixtures must retain their UIDs and remain free of Helm ownership
through removal and uninstall. Every wait is bounded. The `finally` path deletes
only the invocation's named cluster, checks owned node absence, and removes the
private kubeconfig even after cluster deletion or inventory fails, retaining that
cleanup failure in the result. Final source verification compares both complete
file inventories and hashes, detecting newly added templates as well as edits.
Local failure-path regressions exercise those behaviors without a live cluster.

The durable result is
`_bmad-output/implementation-artifacts/evidence/4-5/qualification.json`, accompanied
by version-control-visible command logs (retained through the evidence directory
ignore override), exact source/input/tool/package hashes, native output, archived
chart, rendered manifests and admitted Pods. A failed result remains failed and
does not close the story. The evidence excludes OCI publication/attestation,
off-site restoration, staging deployment and functional application readiness
with retained production images. Those are separate downstream acceptances. The
Timesheets enrollment candidate is unchanged.

Each run clears prior numbered command logs before its first command. Source
evidence records the root revision, porcelain status and submodule revisions, so
the record identifies an uncommitted qualification tree. Commands run in private
process groups that are terminated together on timeout; MSBuild node reuse is disabled.

Source builds use the qualification-owned `source-packages.props` import boundary.
It loads each project's original nearest central package props, preserving owner
compatibility additions and updates, then collapses repeated identity/version
entries. Different-version conflicts remain errors. The qualifier sets this path
explicitly for source project evaluations, records it as an input, and hashes the
wrapper and package import files. Catalog, owner and ordinary Platform package
props remain unchanged; no dependency version is upgraded.

The wrapper is an environment setting for .NET verification because owner-build
subprocesses inherit the environment, including nested builds started by tests.
A command-line MSBuild property alone does not reach those subprocesses.
The qualifier evaluates the native composition project-reference closure and
hashes actual source/resource inputs and resolved imports, alongside all four
authoritative module manifests. It records linked sources, the Builds resolver
and catalog-generation inputs, and detects added/deleted inputs on reevaluation.
After native publishing, the resolved native and composition
`Aspire.Hosting.Kubernetes` identities must both equal `aspireKubernetes` in
`toolchain.json`; retained project-assets files, hashes and package SHA512 values
provide the resolved-package evidence. No package version is changed.

## Finite Memories campaign capacity (offline)

`campaign_capacity.py` adds an explicit `PG-FINITE-CAMPAIGN-1` candidate beside
this qualifier. The existing synthetic qualifier remains chart/admission-only;
its Scheduler-disabled fixtures are not a full Memories environment or launcher.
No toolchain pin or historical fixture allocation changes. Inspect the candidate
without creating a Qualification object, downloading tools or contacting a cluster:

```bash
python3 -B eng/helm-qualification/qualify.py --campaign-capacity
```

Let N be the number of complete C1 collections in the validated finite renewal
schedule. Each collection reserves450000 C1.9 records,150000 C1.10 records and
50000 other C1/C0 records. C2 reserves450000 writers +150000 separate backlog
+50000 other; C3 reserves375 seeds +125 newer controls; C4 reserves50000 other.
The cumulative live/dead bound is therefore R=650000*N+700500 records, with no
credit for earlier vacuum/reuse. A complete source/normalized-argument inventory
has exactly one invocation per command per round; retries earn no extra allowance.
The50000 terms are configured admission envelopes, not observed producer counts.
Missing or excess inventory refuses; producer behavior remains unchanged.

For the admitted R, raw relation/support bytes are4096*R (2048 heap/TOAST +2048
all index/actor/due/catalogue/cursor/reminder state); M=ceil(5*4096*R/4) adds25%.
Reserve one full M-byte rewrite,4GiB WAL and1GiB fixed DB overhead:
PG=round_up_4GiB(ceil(5*(2*M+5GiB)/4)). The independent recoverable backup is
round_up_4GiB(ceil(5*(M+5GiB)/4)). Both retain20% free at modeled peak. Fixed
other primary allocations are8GiB control (etcd4/OpenBao1/logs+other3), three1GiB
Scheduler volumes,4GiB app spill and18GiB host workspace:33GiB. Primary=PG+33GiB.
These configured bounds require actual scoped physical/support/WAL/rewrite and
performance measurements; no exact PG/backup allocation is selected without a
compatible measured schedule. The former24GiB PG /57GiB primary /16GiB backup
numbers are only a single-collection lower bound, superseded for the full campaign.
Aggregate compute remains16250m CPU/31GiB (PG8CPU/16GiB, apps4.5CPU/7GiB,
control2CPU/3GiB, Scheduler0.75CPU/1GiB and host1CPU/4GiB).

A compatible renewal schedule remains a missing Platform prerequisite. The
unchanged C1.15->C1.16 parent chain requires all25 descendants to be recaptured
for each root refresh. A round reserves measured source-bound gate capture,
decision and cleanup slots in parent order, at most24h capture/review lag, the
original<=72h collection and<=24h review/use windows, and<=96h technical age.
Their collection coverage must span the full C2/C3/C4 intervals inside336h.
Each actual owner plan binds `campaignOriginUtcMs` and `renewalRound`; the
consumer checks actual capture/collection times against that admitted round.
A new round cannot move the immutable profile or original C3 origin/deadline.

C1 fault/workers and whole-table maintenance can purge C3 cohorts early or
confound exact125-row seeds/newer controls and physical attribution. The schedule
therefore reserves no-overlap intervals for empty/seeding and every cohort's
expiry/purge/reclamation, with newer-control/report isolation at168h. Shorter
operation reservations require retained source-bound timing measurements;900s
purge,24h reclamation and216h overall C3 policies remain unchanged. Every round
requires exact retained zero unrelated live/dead/support-state readbacks and
unchanged C3 cohort/due/reminder inventory, plus physical-attribution proof.
C2's24h records must be proven drained/purged/reclaimed before C3's empty boundary.
The proposed phase times alone cannot prove that fit. Missing timing, cleanup,
C2 drain or C3 isolation measurements reject admission. No operational renewal
count or compatible timeline is invented; unit schedules/receipts are simulations
and supply no adopted evidence. Platform must supply a real compatible plan and
exercise its native lifecycle; this change supplies no second orchestrator.

`configure_application(values, graph)` returns values for this chart: two Memories
server and two telemetry replicas, one of every other declared workload (nine
Pods), each app400m/640Mi plus Dapr100m/128Mi; explicit ephemeral limits and
128Mi `/tmp`. Existing container names `memories`, `lifecycle` and `clock` remain
compatible with the registered producer. PG keeps8CPU/16GiB. Adding4.5CPU/7GiB
applications,2CPU/3GiB control,0.75CPU/1GiB Scheduler and1CPU/4GiB host reserve
requires16250m CPU and31GiB RAM in aggregate. Throughput, fault and latency
performance must be measured at these actual limits before use.

Complete the candidate's closed input groups (tool/toolchain, immutable runtime
and backend images, every chart file, values/render, component/application/reporter,
source/storage configuration, finite workload plan and renewalEvidence). Every Ref names canonical
absolute bytes and SHA256. The workload plan binds every C1 gate and C0/C2/C3/C4
command to source, a maximum accepted-record inventory and invocation count.
C1 command IDs are `<round-id>/C1.n`; every round has all25 exactly once.
The proposed schedule includes closed campaign/phases/rounds/protectedIntervals/
`c3OperationBoundsSeconds` fields. Each round supplies start/ready/collectionEnd/
reviewEnd seconds and all25 parent-ordered `captureBoundsSeconds`. A retained
`renewalEvidence` Ref authenticates the exact schedule/source/workload digests,
per-round source/command-bound timing receipts, zero unrelated live/dead/support
cleanup and unchanged cohort inventories, C2 drain and C3 exact-row/physical
readbacks. The actual required proof and compatible inputs do not yet exist.
For C1 argument hashing only `target_sha256` and `session_id` are normalized to
`late-bound-target` and `late-bound-session`; the actual owner authority separately
verifies those exact late bindings. Retrying the same identity earns no new record
allowance. Missing or out-of-envelope plans refuse. Supply actual scoped full-rate
heap/index/support/WAL/rewrite/compute measurements, exact retained receipts,
Linux-backed physical reservations with aggregate competing demands, and separate
backup failure-domain proof. Candidate labels, free-space readings and PVC requests
never substitute for those inputs. No such operational receipts were created here.

```bash
python3 -B eng/helm-qualification/qualify.py --campaign-capacity \
  --campaign-profile /private/approved/finite-profile.json \
  --approved-campaign-sha256 <outside-approved-raw-file-sha256> \
  --current-capacity-receipt /private/approved/current-capacity.json \
  --approved-capacity-sha256 <outside-pinned-raw-receipt-sha256>
```

This route performs offline bind/admit checks only. An absent schedule emits
blocked sizing with null PostgreSQL/backup volume requests and a labelled
single-collection diagnostic lower bound; it cannot authorize launch.

Initial measurement freshness cannot be renewed by a fresh capacity receipt.
A first use needs<=24h measurements. Before C3 dispatch, the private outside-approved
owner plan must additionally bind `initialCapacityAdmission` (exact Ref digest),
`capacityAdmissionIssuer`, `capacityAdmissionKeyPath` and
`capacityAdmissionKeySha256`. Platform's small initial-admission artifact signs
the exact immutable profile after strict fresh validation. Its issuer advances an
authenticated independent UTC sample with same-boot CLOCK_BOOTTIME (sample<=60s)
and stamps actual admission time; there is no user backdate argument. Verification
uses only outside-approved issuer/key/receipt bytes, rechecks initial freshness at
that authenticated original stamp, and preserves original identity/C3 time through
multi-day continuation. The immutable initial300s compute/storage observations do
not start expiring again during a valid continuation; separate current capacity
receipts still require<=300s age before every launch. They may renew only observed
available/reserved/competing quantities and receipt metadata over the original
poolId/failureDomain/Linux backing/allocation ownership; backing changes need new
adoption. The real issuer, key custody and trusted-UTC service remain absent
operational prerequisites. No operational key, timestamp or receipt was created.

Owned cleanup authenticates the original outside-approved profile, physical API/CA/
cluster/namespace/runtime, session and exact Lease independently of changed capacity
inputs. Actual disable-only and parent fallback retain this authority and preserve
the original failure-packet profile. Foreign targets/sessions/Leases still refuse.
Historical source closure is authenticated from the committed verifier's actual
imports: authentic old C0/C2-C4 archives retain their original exact source sets;
current exports require every actual import, including the finite-profile loader.
Historical source cannot earn finite-profile credit.

For C3/archival admission, append these outside-approved initial-admission anchors:

```bash
  --original-admission /private/approved/initial-admission.json \
  --approved-original-admission-sha256 <outside-approved-raw-receipt-sha256> \
  --admission-issuer <outside-approved-issuer> \
  --admission-key /private/approved/capacity-admission.key \
  --approved-admission-key-sha256 <outside-approved-key-sha256>
```

The held key must have mode0600 under private0700 custody. Real Memories consumers
obtain these anchors from their outside-approved private owner plan, never from
the proposed capacity profile. No operational issuer implementation/custody service
is supplied; Platform must establish that trusted service before an actual run.

The immutable chart references environment ConfigMap
`access-telemetry-campaign-identity/profileSha256`. It supplies the lifecycle
component hash and Qualification's approved profile hash, with explicit profile ID
and Qualification environment. `runtime_binding(profile, canonical_memories_hash)`
produces its exact late-bound descriptor **after** Memories computes the canonical
identity. The descriptor is excluded from its own hashed inputs; C0 and every
producer read back its exact profile/capacity identity before mutation. Actual
owner authority binds the target/runtime receipt. Do not replace the named
reference with a literal self-hash inside the values/render being hashed.

The capacity-only slice above supplies no lifecycle or qualification credit. The
supported full-environment lifecycle is implemented below; real independent UTC/
monitoring sources, measurements, reservations and operational inputs remain required.


## Supported full-campaign lifecycle

The additive `--campaign` mode extends `Qualification` and its bounded process,
explicit transport, cancellation and disposal path. `campaign_environment.py`
supplies the real environment layer; `campaign_lifecycle.py` persists exact run
ownership and integrates `campaign_capacity.py`. It does not run or replace
Memories assertions. Builds' source/Debug CLI validates descriptors using its
existing contract reader through `validate --environment`. No dependency pin or
historical fixture was changed.

From this repository, the supported command surface is:

```bash
python3 -B eng/helm-qualification/qualify.py --campaign prepare --campaign-state "$STATE" --campaign-plan "$PLAN" --approved-plan-sha256 "$PLAN_SHA256"
python3 -B eng/helm-qualification/qualify.py --campaign inspect --campaign-state "$STATE"
python3 -B eng/helm-qualification/qualify.py --campaign launch --campaign-state "$STATE" --approved-plan-sha256 "$PLAN_SHA256"
python3 -B eng/helm-qualification/qualify.py --campaign monitor --campaign-state "$STATE" --approved-plan-sha256 "$PLAN_SHA256"
python3 -B eng/helm-qualification/qualify.py --campaign stop --campaign-state "$STATE"
python3 -B eng/helm-qualification/qualify.py --campaign remove --campaign-state "$STATE"
```

`prepare` is offline. It requires actual complete outside-approved inputs and
renders with the pinned local Helm artifacts; it never creates a cluster. `launch`
is an explicit operational action and stays in foreground supervision. `monitor`
observes the original owner and never renews startup/execution/stopping budgets or
replays uncertain creation. `stop`/`remove` request cancellation of that exact
owner and share the first stopping deadline. Startup is600s, campaign execution is
at most336h within the approved window, stopping/disposal is900s cumulatively.
Preserve private state after failure; incomplete/foreign/unverified resources
never become a successful disposition. The source code's closed plan validation
is authoritative for input shapes.

The private plan binds run ID, capacity profile and canonical Memories hash,
initial admission, independent clock/status custody, exact window, Docker
engine/socket, primary/backup paths, all tool binaries/support files, pinned
Dapr/Gateway/CNI assets, environment configuration and source closure. The
application input binds this environment configuration into the finite profile;
the tool input binds the actual tools/assets. Environment configuration supplies
immutable Dapr/backend/application/provider/reporter images, component files,
provider/reporter resources, private Secrets/OpenBao seed, PostgreSQL conformance
template and source-bound bounded fault reservations. Missing data is a refusal,
not a generated operational default. Private files use0600 and directories0700.

The real environment retains TLS PostgreSQL with dedicated runtime/maintenance
roles and the maintained security/bootstrap template; OpenBao Raft/TLS with
private initialization custody; three persistent Placement/Scheduler members;
restricted application Pods, declared service accounts and writer gate; and a
suspended, zero-retry unprivileged reporter. Scheduler uses explicitly configured
1Gi member claims and a lower etcd quota, subject to actual measurement admission.
Placement uses three256Mi claims within the control allowance. Single-node member
replication provides no node/site HA. Provider/reporter/CNI bounds must fit the
remaining300m CPU/1Gi control envelope and actual measured performance; oversized
inputs refuse and need a newly admitted allocation. Do not change pins or relax
capacity checks to fit a candidate.

Actual signed independent UTC/status/monitoring sources, fresh source-bound
measurements, compatible finite schedule, physical reservations/backup, TLS and
scoped credentials, exact artifact adoption and live target bindings are still
operational prerequisites. No actual launch or qualification is claimed. The
one ordinary review is complete with all seven substantive findings resolved. The
final37-case lifecycle lane and18-case Memories handoff/profile/cleanup selection
pass; focused process checks, rendering and Builds validation are recorded in
Memories Spec14 and its single retention report. These are offline results.


Collector receipts distinguish `preflight` (before target creation, target and
runtime/PostgreSQL observations explicitly null) from `observed` (exact live
physical target and every required fresh series). Source/trust, declared limits
and physical allocation bindings are checked before launch. The first live poll
requires actual observations; missing/stale data stops the run. During an exact
approved PostgreSQL fault only its unavailable series can be null; allocation
and runtime observations remain required. The implementation supplies the
admission/monitor contract, not fabricated operational measurements.


The operational environment input also binds:

- `leaseHolders`: exact approved `story-27-4/owner-6775094/<32 lowercase hex>`
  session holders. Cleanup never treats a matching prefix as ownership.
- `providerNamespaces`: explicitly declared additional provider namespaces,
  including the existing consumers' `monitoring` namespace. Resources keep their
  declared namespace; namespace UIDs join the run's physical ownership record.
- `faultWindows`: exact source command/hash, resource and interval, explicit
  `replicas` and `allowRolloutRestart`, plus the exact source `scenario`,
  configuration/resource `changes` and `metricAllowances`. Normal drift
  checks resume after the interval. An allowance never grants a general exemption
  for images, security settings or unrelated configuration.
- `monitoring`: `collectorId`, source Ref, `commandSha256`, private key Ref,
  `receiptPath`, allocation bindings and explicit operational limits. Each sizing
  allocation binds an adopted `poolId` and unique owned `identity`.

The signed collector body uses `schema: hexalith.platform.campaign-monitor/v1`,
`runId`, `planSha256`, `profileSha256`, `collectorId`, `sourceSha256`,
`commandSha256`, `observedUtcMs`, `intervalStartUtcMs`, `boottimeNs`, `bootId`,
`phase`, `target`, `allocations`, `series` and `expectedFaults`, with an HMAC
`signature`. Receipts are at most60s old by
UTC and same-boot elapsed time. Each allocation reports its original pool/identity,
`scope: owned-allocation`, `usedBytes` and `availableBytes`; whole-filesystem free
space is not an owned allocation measurement. Expected fault errors additionally
need attribution to the exact approved command/resource UID/window. Each
`expectedFaults` entry binds `faultSha256`, `resourceUid` and the attributed `series`.
Intervals must not overlap a prior consumed receipt; allowances are cumulative
within the original fault window and never reset on a fresh receipt. Restoration
returns every configuration/resource field to its admitted baseline.

Required `postgres.` series include table count, relation/live/dead/support/WAL
size/counts, connections/utilization, transaction p99 latency/errors, timed and
requested checkpoints/age, vacuum progress/stall, lock wait, oldest-due age,
physical-evidence age and recovery health. `runtime.` series cover TLS/authentication,
Dapr transaction, ETag and restart errors. The exact closed field names and bounds
are `PG_SERIES`, `RUNTIME_SERIES`, `MONITOR_LIMITS` and `monitoring_config` in
`campaign_lifecycle.py`. These operational series do not replace Memories' exact
accepted-record or reclamation assertions.

Read-only `inspect` works while the monitor owns its mutation lock and exposes an
immutable `observationPair` path/hash plus descriptor/readback references. Each pair
stays available for its full freshness interval. Memories accepts
`HEXALITH_CAMPAIGN_OBSERVATION` and
`HEXALITH_CAMPAIGN_APPROVED_OBSERVATION_SHA256` together with the outside-approved
lifecycle plan hash; separate pinned artifact inputs remain supported. A later
poll cannot rewrite a producer's held pair. Environment observations remain
`qualified: false`.


## Independent prerequisite calibration

`--calibration prepare|inspect|launch|monitor|stop|remove`, with
`--calibration-state`, optional `--calibration-plan` for prepare, and
`--approved-plan-sha256` for prepare/launch/monitor, is a separate supported mode.
It subclasses the existing `CampaignQualification` lifecycle and shares its
exclusive state lock, bounded processes, exact Docker/kind/cluster/namespace/
control ownership, failure capture and stop/removal verification. It does not
call finite campaign admission, a synthetic qualifier or a Memories C2 producer.
The schema is `hexalith.platform.independent-calibration/v1` and the explicit
application/chart profile is `PG-INDEPENDENT-CALIBRATION-1`. The qualification
endpoint additionally requires the mounted independent run and plan bindings;
Production, local-profile mixing, wrong component hashes and expired gates refuse.

For example, from this repository, after actual inputs and outside approval exist:

```bash
python3 -B eng/helm-qualification/qualify.py --calibration prepare --calibration-state "$STATE" --calibration-plan "$PLAN" --approved-plan-sha256 "$INDEPENDENT_PLAN_SHA256"
python3 -B eng/helm-qualification/qualify.py --calibration inspect --calibration-state "$STATE"
python3 -B eng/helm-qualification/qualify.py --calibration launch --calibration-state "$STATE" --approved-plan-sha256 "$INDEPENDENT_PLAN_SHA256"
# monitor is only for an original ready state whose measurement has not started;
# launch normally supervises measurements and removal before returning.
python3 -B eng/helm-qualification/qualify.py --calibration monitor --calibration-state "$STATE" --approved-plan-sha256 "$INDEPENDENT_PLAN_SHA256"
python3 -B eng/helm-qualification/qualify.py --calibration stop --calibration-state "$STATE"
python3 -B eng/helm-qualification/qualify.py --calibration remove --calibration-state "$STATE"
```

The closed plan schema in `independent_calibration.load_plan` is authoritative.
It binds exact host/isolated engine, source/tool/image/value/component/credential
inputs, a separate approved calibration allocation basis, independent Linux
primary/backup backing and signed current reservations. Each reservation requires its issuer, reservation ID, owner, valid-from/end and
active non-revoked disposition. It binds original host/engine/pool/filesystem/
failure-domain/path identities, fresh available
minus competing capacity and the reserved amount. Changed backing, unsigned gross
observations, missing independent backup or missing inputs refuse before preparation
or infrastructure mutation. The HMAC issuer/key references, authenticated UTC and
short action-status custody must be outside-approved with the complete plan.
No campaign measurement, renewal count or single-collection allocation is borrowed.

The native `calibration_workload.py` child rechecks that original authority and
budget before each authenticated fixed-workload call. It executes a separate
1200-segment/150000-record lane and then two distinct Pod writers with 1801
125-record segments each. Actual target receipts must account for every record,
measure at least1800s common overlap and show at least900000 state-component
operations from stable per-Pod metric counters. No retries or restarts occur.
`launch` stays in foreground supervision; `monitor` cannot replay a consumed or
uncertain measurement. Each child inherits the original supervised process group.

Fresh complete operational allocation receipts, metrics-API per-container CPU/
memory, target-side latency, and physical SQL snapshots cover database, heap/TOAST,
all access-telemetry/support/index relations, WAL and the quiesced full-table rewrite.
Allocation headroom remains at least 20%; the 4GiB WAL ceiling is enforced.
Measurement quiescence is bounded within original execution and never starts or
resets the separate stop/remove allowance.
Missing metrics/provider/readbacks fail; no value is filled with a configured bound.
The original startup is <=600s, execution <=50400s including startup, cumulative
stop/remove <=900s, within the outside-approved <=51900s window. The exact signed
plan's `limits` selects either the unchanged `independent_calibration.LIMITS`
or the explicit `independent_calibration.SMALL_LIMITS` candidate:

| Compute budget | Legacy independent | Small independent candidate |
| --- | --- | --- |
| PostgreSQL ceiling | 8000m / 16GiB | 2000m / 4GiB |
| Other application, sidecar, control, Scheduler and host budgets | 8250m / 15GiB | 8250m / 15GiB (unchanged) |
| Aggregate net reservation and observed ceiling | 16250m / 31GiB | 10250m / 19GiB |
| PostgreSQL requests | 4000m / 8GiB | 2000m / 4GiB |
| PostgreSQL `shared_buffers` / `effective_cache_size` | 4GB / 12GB | 1GB / 3GB |

These are configured budgets pending actual measurements, not proven minima or
performance results. Selection changes only the four compute values in `limits`;
all other workload, timing, retry, storage, latency/error and security requirements
remain exact. Mixed or modified selections refuse. Selecting small requires its
own plan/profile identity and current signed net reservations; legacy authority
and larger-target measurements cannot be relabelled. The independently rendered
PostgreSQL object receives both small requests and limits plus the two memory
settings; TLS/SCRAM, durability, probes, credentials and all other object fields
remain unchanged. Full campaign configuration and admission retain their defaults.

Persisted preparation and prelaunch comparisons, metrics checks, inspection and
results use the same selected plan limits. Missing/stale readbacks or excess stop
the attempt; no cap increases or retries are automatic. Historical readbacks with
no selection field are compatible only with the legacy plan. Terminal inspection
uses retained immutable evidence even after operational allocation paths disappear.
Small measurements cannot supply same-input evidence for the larger target.
The source-bound result and retained
segment proofs say qualified/C1/C2/A41=false. Separate backlog occupancy is not an
expired-backlog drain, C2 replacement/fault, C3 retention/24h reclamation or renewal
proof. Existing full campaign admission and all its measurements/security checks
remain unchanged and refuse this independent schema.

Actual host access, reservations/allocations, complete images/provider/reporter/CNI
and scoped credentials, trusted clock/status/monitor sources, adopted source and
outside approval remain operational prerequisites. No independent calibration or
infrastructure was executed during implementation. Missing data is retained as null
in Memories' existing preparation index; no owner packet is fabricated.
