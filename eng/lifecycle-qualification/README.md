# Local lifecycle qualification controller

The published internal success is `completed` with causal exit `Success`; its
public contract outcome is `passed`. The verifier preserves the four bounded
causal outcome fields, excludes messages and requires agreement with the public
outcome, cause and terminal time.

Independent process discovery reads birth ticks before environments. Same-user
processes proven older than the controller-issued tool cannot belong to that
fresh owner and are excluded. Kernel Z/X exited states are also excluded before
environment reads. Every live same-user process at or after that boundary
must remain readable; a protected newer process still fails verification. The
issued birth boundary, exclusion/check counts and completion flag are recorded.
Read refusals retain only bounded PID/birth/parent/session/state and read-stage fields;
they still fail closed. Each exact cleanup clears its prior verification before
running, so finalization retries an owner whose latest independent audit failed.
After successful exact down, the controller reaps its issued Popen tool child
within the existing 95-second stop allowance before independent process discovery.
A timeout keeps cleanup unverified. Expected lock refusals leave that child running.

The expiry probe retains the negative retry receipt without messages, observes
expiry through the published read-only `list` projection and preserves separate
safe raw-store snapshots and hashes. Listing must leave that file unchanged;
explicit cleanup must then persist the same expiry. These metadata observations
still supply no attached-work/access termination proof. Progress stdout contains
only controller event names, timestamps and observation ordinals for monitoring.

This controller uses the fixed committed Parties `lifecycle-package` suite and
the genuine published Builds 4.47.0 DLL. It does not build or substitute a source
tool. Run focused controls before any live invocation:

```bash
python3 -B -m unittest discover -s eng/lifecycle-qualification -v
```

After the parent finishes the hosted campaign and prepares the verified runtime
and Release/package suite, the complete local invocation is:

```bash
python3 -B eng/lifecycle-qualification/local.py \
  --checkout /tmp/hexalith-story-2-10-20261110/parties \
  --work /tmp/hexalith-story-2-10-20261110/local-campaign \
  --evidence /home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/evidence/2-10/campaign-20261110/local \
  --environment /tmp/hexalith-story-2-10-20261110/local-environment.json \
  --tool-assembly /home/administrator/.nuget/packages/hexalith.builds.module.cli/4.47.0/tools/net10.0/any/Hexalith.Builds.Module.Cli.dll \
  --tool-package /home/administrator/.nuget/packages/hexalith.builds.module.cli/4.47.0/hexalith.builds.module.cli.4.47.0.nupkg
```

`--work` must be fresh; it is private and must remain outside the retained
`--evidence` tree. The official package hash defaults to
`1eea9b282d1d247e1f6e9e2213c5969f7895df44105e6900c873b586c5c4f905`.
Every installed tool file is checked against that package before launch. Optional
repeated `--scenario` values select a subset; the record always lists all eight
required groups, and a selected pass never claims the full campaign passed.
Preflight requires that exact official package hash even if `--tool-sha256` is
provided; the argument cannot select another self-consistent package.

The private prepared environment supplies only basic runtime settings and the
producer-issued package imports. Values and raw process output are never copied
into retained evidence. The parent CLI uses an isolated campaign `XDG_DATA_HOME`
for state/workspaces. Published 4.47.0 scrubs that variable from native children,
so the genuine fixture's private control remains under the child's normal
`~/.local/share/hexalith-builds/ci-qualification/<actual-run-id>` directory. `HOME`
is preserved. No GitHub metadata is fabricated.

The prepared environment is required proof: all five imports must belong to the
same producer directory. Its actual `mapping.json` must name the fixed checkout,
package mode and the producer-issued content hash. The document/import hashes
are recorded. Every terminal mapping hash and complete durable
`selected-source-mapping.json` must agree with that original mapping. Missing
proof remains refused/incomplete; a hold transition or a declared hash alone
cannot supply it. Fixture timestamps, IDs and hashes are bounded and checked
against the issued local owner before entering retained observations.

Each launch has a durable intent and closed pipe gate before execution. Every
issued run, attempt and environment is read from actual runner state. Signals use
PIDFD handles and exact PID/birth/session checks, with owned run tags additionally
required for AppHost/native processes. First outcomes and absolute startup/hold
deadlines cannot be replaced. The startup timeout scenario pauses only the actual
owned Starting AppHost and waits for the original 600-second deadline; it does
not introduce an override. Both real metadata locks and exact cleanup retry keep
the peer's resources, native process and claims unchanged, then require its
genuine persistence recheck to pass.

All normal failures and SIGINT/SIGTERM paths attempt exact issued-owner cleanup,
including every sibling after one cleanup failure. Cleanup requires the tool's
empty verified inventory plus independent Docker/process/claim/workspace checks.
An abrupt kill of the controller itself still requires the parent to use the
durable private launch records and issued run IDs for recovery; never delete an
unknown workspace or signal an unverified process. Preserve `--work` until every
issued owner is verified clean. Only `--evidence` is safe to retain as the bounded
redacted controller record.

Published capability gaps remain explicit: attachment commands admit/end hold
metadata, but expose no attached-suite execution or attached work/access
termination producer. Those rows remain incomplete even after hold transitions.
The fixed caller also exposes no exclusive-resource selector to force a genuine
managed collision; that proof remains incomplete. A missed real cancellation
window after failure remains incomplete. Missing actual startup-duration proof
also prevents a pass. These outcomes return nonzero and grant no tool/candidate
acceptance. The focused controls execute no services; an explicit live invocation
starts real owned environments.

`prepare.py` provides resource-free preparation for a later fixed input set. Its
closed `hexalith.lifecycle-preparation.v1` record names exact caller, Builds and
Platform commits; the catalog digest; the published tool source, version and
framework; SDK/Aspire/Dapr versions; and each download archive's digest. The
preparation record is a selection, not publication authority or tool acceptance.
`check-sources` compares the real caller pins, clean checkout, installed catalog,
catalog toolchain declarations and SDK with that selection, and checks that the
selected Platform source contains the required live-port correction. Publication,
signature verification and qualification remain independent gates. It starts no
process other than read-only Git commands and allocates no services.

For the recorded, currently blocked preparation:

```bash
python3 -B eng/lifecycle-qualification/prepare.py check-sources \
  --inputs _bmad-output/implementation-artifacts/evidence/2-10/preparation-20261113/inputs.json \
  --checkout references/Hexalith.Parties --platform-repository .
```

After independently verifying downloads against official publication identities,
`populate --inputs <selection.json> --cache <private-cache> --downloads <archives>`
publishes their content-addressed cache entry. `restore --inputs <selection.json>
--cache <private-cache> --destination <fresh-private-parent/downloads>` rehashes
every consumed archive and creates independent job-local copies. Both commands
require the exact selection. Cache hits revalidate all bytes and metadata;
poisoned entries, links, extra run state and existing destinations fail. No
test result, native workspace, credential, descriptor or prior run state is
restored. The Linux no-replace publication also preserves a destination created
after the initial check. Each operation reports its own elapsed time; it supplies
no test result or performance-savings claim.

The historical `local.py` remains bound to 4.47.0. These preparation commands do
not make it a controller for a new release. A compatible committed source set,
the current producer-aware local controller, fresh owning checks and all required
live runs are still required. The [preparation record](../../_bmad-output/implementation-artifacts/evidence/2-10/preparation-20261113/README.md)
keeps the met Builds-release prerequisite separate from the open Platform source
and publication gates.
