# Timesheets Story 3.6 enrollment preparation

This directory retains a Platform-owned, offline v2 enrollment candidate and
a reproducible staging plan. **Deployment is unauthorized; activation is
unauthorized; Timesheets Story 3.6 acceptance and release remain FAIL.**
Neither schema validation nor a preparation hash supplies operational evidence.

The [2026-10-17 hosted package preparation](hosted-package-preparation.md) now
adds maintained, locally tested Helm application/environment candidates. Fresh
native reads still find no staging namespaces or Timesheets deployment. Missing
immutable image, provider and infrastructure bindings refuse rendering; these
packages do not activate the historical candidate or close deployed acceptance.

The [staging dependency handoff](staging-dependency-handoff.md) records the
2026-10-14 native findings, retirement and sequential upgrade prerequisites,
local preparation receipt, and missing Release/provider acceptance gates.

The candidate declares one Timesheets host, the EventStore/Tenants/Memories
domain minimum, public and internal surface boundaries, expected workload
claim needs, and a proposed recovery inventory. It is inert by default.
Platform has an explicit Development/source host opt-in described below. Empty
`runtime.dapr`, extensions, topics, secrets, egress and providers express an
incomplete candidate: their qualified owner contracts are missing. The
classification identifies the required operational providers. A real OIDC
authority is not an inventory, maintenance, reference-validation or state-store
tenancy binding, so this candidate invents none of those keys. Critical flows
and recovery classes require module-owner qualification before enrollment.

For the file AppHost's local source run, set `Platform:Timesheets:Enabled=true` through the
standard .NET configuration environment key:

```bash
DOTNET_ENVIRONMENT=Development Platform__Timesheets__Enabled=true \
  aspire start --apphost apphost.cs --non-interactive
```

The existing Platform model consumes this directory's unchanged candidate and
the command-selected source mapping. An enabled run includes the mapped
`Hexalith.Timesheets/src/Hexalith.Timesheets/Hexalith.Timesheets.csproj` plus the
six EventStore/Tenants/Memories reference servers. Optional persisted mapping
inputs remain `Platform__SourceMapping__Path` and
`Platform__SourceMapping__Hash`; missing Timesheets mappings or projects refuse
preparation without a nested or sibling fallback, including symbolic-link escape.
For this file entry, selecting the Timesheets candidate without the flag refuses
runtime binding. Package mode, publishing
and non-Development profiles require a qualified release/profile and are refused.
Enabling Works and Timesheets together is refused before either graph is added;
the existing Agents-unavailable refusal is preserved.

For Builds, the durable command-selected source plan supplies the opt-in. It must
select this retained candidate as its sole `PlatformDeclarationPaths` entry,
identify `PlatformActiveModule=timesheets` and include a
`PlatformDeclarationHash` matching the candidate's exact SHA-256 bytes. The model
checks the path, source mode and hash before adding resources. Missing or changed
candidate selections refuse; an ambient `Platform:Timesheets:Enabled` flag cannot
expand a different signed plan. Ordinary reference plans keep their selected
closure. This path does not depend on passing an environment flag through the
Builds launcher's sanitized child environment.

The local host declares separate `public` and `internal` HTTP listeners with
Aspire-assigned ports. Its internal endpoint is excluded from external and
default reference advertisement. Dapr selects `internal`, and
`Timesheets:InternalSurface:Port` follows that listener's actual target port;
`AllowOnAnyPort=false` is explicit. The Platform-owned local
`timesheets-readmodels` component uses composition-owned Redis DB14 and scopes
only to `timesheets`; the sidecar receives no actor state-store reference.
Local workload authentication validates the shared SDK Development JWT contract,
shares the gateway's signing material, binds its internal receiver audience to
`eventstore`,
adds Timesheets to the existing trusted caller list and registers
`*|timesheets|v1` for internal processing. The LiveDaprFixture is explicitly
disabled. Access/reference/migration guards and unavailable inventory providers
remain unchanged in owner source.

The [local qualification record](local-host-binding-qualification.md) records
executed checks and their limits. This opt-in supplies no deployment, migration,
certification or acceptance authority. The retained `plan.json` remains the
historical blocked preparation artifact.

Prepare a new local file from a clean, committed Timesheets checkout:

```bash
python3 eng/timesheets-deployment/prepare.py \
  --platform-root /home/administrator/projects/hexalith/platform \
  --source-root /home/administrator/projects/hexalith/timesheets \
  --output /tmp/timesheets-enrollment-plan.json
```

The output's parent must already exist. The Linux preparer opens every parent
without symlink traversal and retains those directory descriptors through
publication. It refuses a changed ancestor, writes, flushes, fsyncs and closes a unique
owned temporary file completely, then publishes by an atomic exclusive hard link.
Existing files, symlinks and racing destinations are preserved. Write or close
failures publish no partial plan; cleanup removes only the owned temporary file.
Outputs inside the candidate source, Platform references, or either repository's
actual Git/common metadata directories (including linked worktrees) are refused.
Original source and Platform root symlink aliases are rejected before resolution.
Missing, dirty, uncommitted, oversized or changing required source
inputs produce no plan. Only bounded local Git reads run; no native cluster
command, network probe, domain command, publication, receipt issuance, migration
or runtime startup is available. The only arguments select the Platform root,
Timesheets source root and a new output file. There is no receipt, grant,
operator override, deployment or acceptance argument. Adjacent local evidence
files have no effect.

`plan.json` retains full canonical revisions, SHA-256 hashes of the consumed
source and Platform inputs, and the exact candidate declaration hash. The
generator entry binds the executing script bytes; an alternate Platform root
whose generator differs is refused. Platform hashes include both the three
canonical EventStore/Tenants/Memories snapshots and the three mapped owner
declarations used by validation and dependency selection. These are inspected
preparation bytes, not proof that Platform inputs are a released artifact. Its
`preparationSha256` hashes the sorted UTF-8 JSON object before that field is
added; indentation is two spaces and the file ends with one newline. Source
paths are repository-relative, so the same complete inputs and VCS identities
produce identical bytes from another checkout. Platform HEAD intentionally
participates in preparation identity: after a commit, a new plan may differ even
when the consumed file bytes match. Cross-commit byte equality is not required.
This identity binds preparation only. Release image
and chart digests, package identities, provenance and rollback package remain
unavailable. Proposed image repository: `registry.hexalith.com/timesheets`.

The source revision, Platform HEAD gitlink, index gitlink and observed direct
checkout revision and observed cleanliness are separate fields. A dirty direct
checkout remains an enrollment blocker even when its HEAD matches the declared
pin. Cleanliness covers own-root tracked/untracked files, excluding ignored files
and nested submodule state. The direct content hash covers only allowlisted
`SOURCE_FILES`; arbitrary dirty files are unread and uncertified. Changing known
source bytes, cleanliness, revisions or Platform inputs during preparation is
refused. Preparation never updates the direct
submodule or nested references. Any difference is an enrollment blocker; an
external checkout or commit can change the next preparation's identities.
Changes to other stories and pre-existing checkout work remain outside this
increment. Source hashes are neither published package identities nor released
image digests.

The proposed target is staging on `jpiquot@local`, Kubernetes API
`https://192.168.1.30:6443`, application namespace `hexalith-staging` and separate
data namespace `hexalith-staging-data`. `timesheets.hexalith.com` is a proposed
hostname. The expected `kube-system` namespace UID
`b5860674-1d1e-4a3b-b413-e1bb3b9a4557` comes from the Timesheets owner's dated
2026-10-13 source report. Those dated facts are pinned to the exact report SHA-256
`f3fa3e844f0e23f732371e80258945feede0866b0608e5768457433ebacb2118`,
also retained in `expectedClusterEvidence.sourceSha256` and the candidate's
consumed-input hashes. A different committed report is refused until these
source-backed facts are deliberately re-evaluated. This authenticates no live
authority. Preparation makes no fresh cluster observation;
current target identities and authorization still need native binding.

The environment-layer owner provides the data-namespace Gateway and listener
Certificates, OpenBao, staging identity realm, Dapr trust and policies, durable
broker, data instances and isolation. The application package holds HTTPRoutes
in the application namespace with an explicit parent reference to the
data-namespace Gateway. Public listener 8080 may route only these paths:

| Match | Path |
| --- | --- |
| Exact | `/api/timesheets/magic-links/confirm` |
| Exact | `/api/timesheets/magic-links/confirm/submit` |
| Exact | `/api/timesheets/magic-links/adjust` |
| Exact | `/api/timesheets/magic-links/adjust/submit` |
| PathPrefix | `/api/timesheets/magic-links/confirmation-capabilities` |
| Exact | `/metadata/timesheets` |

Never render a broad `/` public route. `/process`, `/project` (including rebuild
subpaths), `/query`, `/replay-state` and `/admin` stay private on 8081, without
a public HTTPRoute or public advertisement. Configure
`Timesheets:InternalSurface:Port=8081` and
`Timesheets:InternalSurface:AllowOnAnyPort=false`. The proposed internal Service
is `timesheets-internal.hexalith-staging.svc.cluster.local:8081`; it is not an
observed endpoint. Private networking, Dapr namespace/app-ID caller policy and
application authentication must be qualified together. Readiness `/ready`
does not establish provider availability, subscriber delivery or acceptance.

The read-model component name `timesheets-readmodels` is hardcoded by the
module. There is no honored rename key. Platform must qualify that exact
component scoped to the staging Timesheets sidecar, including module-owned
rebuild semantics and durable shared-rebuild transport/coordination continuity.
The proposed rebuild inventory cannot certify complete legacy issuance or
recoverable authoritative state.

Jérôme Piquot is the proposed Administrator/operator. Existing native context
access grants no Timesheets migration principal, domain permission or production
admission. Production-grade asymmetric OIDC and signed per-request
`eventstore:bound-tenant`, `eventstore:bound-domain` and
`eventstore:bound-actor` assertions need owner-qualified providers accepted by
the EventStore gateway. Development symmetric signing and fixture guards cannot
replace them. Normal operations also require real Timesheets authorization,
tenant access and Projects/Works/Parties reference-validation providers; the
source's default-deny registrations remain in place.

The plan records these missing prerequisites with their owners:

| Gate | Required evidence or implementation |
| --- | --- |
| Infrastructure and hosting | Approved, currently bound target; qualified namespaces, Gateway, OpenBao, Dapr and durable broker; unfinished 4.14–4.17 and 4.22 |
| Retained release | Exact digest-pinned Release image, owner packages, catalog identity, provenance, retained application chart and rollback package |
| Production providers | Resource-binding issuer; real access/reference guards; qualified owner-complete runtime/provider declaration |
| Certified legacy inventory | Complete cross-tenant coverage, immutable source cut, verifier and status/retrieval; not a Kubernetes census or generic P1 receipt |
| Maintenance and migration | Enforced writer/delivery quiescence and qualified server-side operator authority; default-deny authority remains |
| Continuity | Owner-source continuity and retained pending-delivery identities/recovery for the exact cut |
| Deployed delivery | Installed subscriber, shared-rebuild and persisted end-state proof for the retained release and target |
| Acceptance | Timesheets-owned deployed magic-link critical flow; isolated local evidence stays separately retained |

Review this concrete preparation before requesting infrastructure approval.
Approval must name the exact target, namespace/endpoints, infrastructure scope,
operator identities and recovery boundary. It does not close the provider,
release or Timesheets acceptance gates. This tool cannot consume or turn an
approval into execution authority.

After separate approval, the future operator sequence is: bind current target
identities; complete and qualify hosted composition, environment-layer and
owner providers; resolve enrollment drift under separate source authorization;
retain exact Release artifacts; validate and dry-run the retained application
package; verify public denial/private caller boundaries; deploy through the
approved Platform release procedure with baseline and recovery point retained;
then obtain module-owned deployed acceptance before reopening its gate. Nothing
in this directory executes those steps or migrates legacy state.

A failed future attempt closes user ingress and retains diagnostics. Qualified
compatible rollback restores the retained application package once, then checks
readiness and owner smokes. Application rollback never rewinds business data,
legacy issuance, event history, credential rotations, revocations or index state.
Incompatible retained-data recovery needs the separately approved named owner
recovery path. Never clear legacy data to make enrollment pass.

Verify preparation offline:

```bash
python3 -m unittest discover -s eng/timesheets-deployment -p 'test_*.py' -v
dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj --configuration Debug
dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -class Hexalith.Platform.Composition.Tests.TimesheetsEnrollmentTests
python3 eng/timesheets-deployment/prepare.py --source-root /home/administrator/projects/hexalith/timesheets --output /tmp/timesheets-enrollment-reproduced.json
cmp eng/timesheets-deployment/plan.json /tmp/timesheets-enrollment-reproduced.json
git diff --check
```

Use the repository-pinned SDK 10.0.402. The C# checks invoke the existing Builds
embedded v2 schema validator and dependency selection without composing or
starting resources. Python adds candidate policy and safe-file checks; it does
not claim to replace that schema validator. Reproduction requires unchanged
source revisions, Platform HEAD, direct checkout/index pins and cleanliness,
executing-generator bytes, and hashed Platform input bytes.

The retained snapshot was regenerated after the review fixes with this exact
generation procedure, selecting the clean sibling Timesheets source explicitly:

```bash
timesheets_plan_dir=$(mktemp -d /tmp/timesheets-enrollment-reviewed.XXXXXXXX)
python3 eng/timesheets-deployment/prepare.py \
  --platform-root /home/administrator/projects/hexalith/platform \
  --source-root /home/administrator/projects/hexalith/timesheets \
  --output "$timesheets_plan_dir/plan.json"
cp -- "$timesheets_plan_dir/plan.json" eng/timesheets-deployment/plan.json
```

The final copy updates only the reviewable local retained artifact; the generator
itself has no overwrite mode. The plan's full `platform.revision`,
`candidate.revision`, `directSubmodule` observations and `generator.sha256` are
the provenance of that snapshot. It remains valid as a record of those inspected
inputs when a later checkout or commit changes current identities. It is neither
a current live observation nor deployment, activation or acceptance authority.
