# Platform isolated node1 Docker proposal

This is an offline setup proposal for `node1 / 192.168.1.30`, owned by Platform.
`isolated_docker.py` only writes a new local private bundle. It imports no runner,
opens no socket, invokes no host commands and installs nothing. It issues no
reservation, signature, executable calibration plan or readiness verdict.

From Platform, using an already existing private local parent:

```bash
python3 -B eng/helm-qualification/isolated_docker.py --output /private/local/fresh-bundle
PYTHONPATH=eng/helm-qualification python3 -B -m unittest -v test_isolated_docker
```

The output and every directory have mode 0700; files have mode 0600 and no execute
bits. Existing outputs (including empty directories, files and dangling symlinks)
and symlink ancestors refuse. Prior contents and permissions are preserved. The
immediate parent must be owned by the current user with no group/other access.
`receipt.json` records every other generated file's exact SHA256, length and mode;
pin the receipt's own raw SHA256 externally. Bytes are reproducible for the same
generator/recipe, independent of output path, wall clock and umask. On write
failure, the newly created partial directory stays private. Payload files and a
private pending receipt are fsynced before atomic exclusive publication of
`receipt.json`; failed receipt persistence publishes no final success marker.
Inspect partial output and use another fresh name, never overwrite it.

`@TOKENS@` and null observations are deliberately unresolved. Files ending `.in`
are not installed configuration. A custodian must bind actual observations and
review the resolved delta before any host change. The existing calibration
approval remains accepted and its single attempt unused. Host capture is consumed;
do not repeat it. Story 27.4 stays in progress, Production unproved and A41 open.

Resolved payload destinations, all fresh and privately owned, are:

| Payload | Proposed destination |
| --- | --- |
| `user/hexalith-calibration-docker.service.in` | `/home/hexalith-calibration/.config/systemd/user/hexalith-calibration-docker.service` |
| `system/user-@UID@.slice.d/50-calibration.conf.in` | `/etc/systemd/system/user-<actual-uid>.slice.d/50-calibration.conf` |
| `system/user@@UID@.service.d/50-calibration-delegate.conf.in` | `/etc/systemd/system/user@<actual-uid>.service.d/50-calibration-delegate.conf` |
| `config/daemon.json.in` | `/home/hexalith-calibration/.config/hexalith-calibration/daemon.json` |
| `config/docker-client.json` | `/home/hexalith-calibration/.docker/config.json` |

The two system drop-ins are root:root 0600 in root:root 0755 instance directories;
the account must not be able to lift its ancestor limits. Other destination files
are dedicated UID:GID mode 0600 beneath dedicated UID:GID mode 0700
private account directories. Do not copy `proposal.json` or the receipt into a
calibration plan or treat them as runtime observations.

## Proposed ownership and limits

Use a fresh dedicated `hexalith-calibration` account, locked password, no sudo or
docker-group membership, a private `/home/hexalith-calibration`, and explicit
custodian-controlled execution. Do not adopt a pre-existing account/home or change
their ownership. Actual UID/GID, at least 65536 subordinate UIDs and GIDs, and
nonoverlap with every existing account/range remain operational inputs. Do not
copy or expose credentials to make the account work.

Use already verified binaries in the private versioned path
`/home/hexalith-calibration/tools/@TOOLSET@/bin`. Record actual version, source,
hash and ownership for Docker client/daemon, `dockerd-rootless.sh`, RootlessKit,
slirp4netns, containerd, runc and required companion executables (including
containerd shims and docker-proxy). Host `newuidmap`/`newgidmap` must already be
verified with the required privilege. `/usr/bin:/bin:/usr/sbin:/sbin` supplies
recorded system utilities, including sysctl/iptables; verify their actual paths,
versions and hashes without using these paths as a fallback container runtime.
The unit binds `DOCKERD` to the private daemon and clears ambient
`_DOCKERD_ROOTLESS_CHILD` and `DOCKERD_ROOTLESS_ROOTLESSKIT_FLAGS` overrides.
`userland-proxy-path` explicitly selects the private verified docker-proxy rather
than Docker's preferred host libexec location. Inspect the retained script's actual
helper selection and option support. Missing or incompatible tools block setup;
no package-manager replacement, download/install script or dependency update is
proposed. There is no new proprietary CLI/MCP service or environment runner.

The user unit launches `dockerd-rootless.sh` with an explicit Unix socket beneath
`/run/user/@UID@/hexalith-calibration`, private exec/PID/RootlessKit paths, native
overlay2 data under `engine-scratch/docker`, and Docker-managed private containerd.
No `--containerd` endpoint is supplied. Verify actual containerd root/state/socket
and parent/process UID rather than assuming the proposed managed paths were used.
No host Docker socket, TCP/SSH API, ambient context or host containerd is selected.
The daemon unit has no automatic restart or enablement. This does not control
kind's node restart policy: pinned kind 0.31.0 requests
`--restart=on-failure:1`. Do not change that policy or native sources silently.
Actual node RestartCount, start times and lifecycle events must prove that no
restart occurred; any restart fails the zero-restart attempt. The continuous
collector required below is absent, so this remains an explicit launch blocker.
No system Docker service is
enabled, disabled or stopped. No lingering is assumed or automatically enabled.

The **entire dedicated user's** `user-@UID@.slice`, owned by the system manager,
gets `CPUQuota=1225%` (12250m), `MemoryMax=24696061952` (23GiB),
`MemorySwapMax=0` and `TasksMax=32768`. It encloses `user@@UID@.service`, all user
units, Docker systemd container scopes and runner/login siblings. The workload
stays **10250m/19GiB**, including PostgreSQL **2000m/4GiB**; the additional
**2000m/4GiB** is a configured daemon/build/unpack allowance. These are ceilings,
not guaranteed net capacity or capacity/performance qualification. A daemon-only
service cap cannot contain systemd container scopes or runner siblings.

The instance-specific user-manager drop-in delegates cpu/cpuset/io/memory/pids;
never install it globally as `user@.service.d` or use `user-.slice.d`. Every
process of the dedicated UID must be inside the capped slice. The host custodian
must verify effective limits on **every ancestor** and controller availability.
There is no supported dockerd `default-pids-limit` directive or per-container PID
ceiling claimed here; enforced whole-user `TasksMax=32768` contains all descendants.
Rootless `docker run` limits alone can be ignored without cgroup v2/systemd.
Do not set `NoNewPrivileges` where it would disable the required mapping helpers.

Reserve an **additional 25769803776 bytes (24GiB)** of bounded engine/image/build/
unpack scratch, separately from primary and local S3 staging allocations. Require
an existing suitable filesystem with an enforced dedicated project quota or a
pre-existing bounded filesystem allocation; bind actual device/filesystem/quota
identity, hard byte/inode limits, current usage, competing allocations and at
least **20% free margin**. This utility creates no loop disk, partition or mount.
A directory, `df`, free-space observation, shell ulimit or PVC request is not a
boundary. Include Docker/containerd layers, BuildKit cache, temporary files,
container logs, unpack and runner build/cache paths in that accounting. Route
`TMPDIR`, source build intermediates/output, NuGet/.NET caches and `XDG_CACHE_HOME`
there explicitly, or account their independently enforced existing allocation;
do not leak growing scratch into `/tmp`, home or the protected cluster's disks.
Evidence/primary/S3 staging retain their original distinct reservations and 20%
headroom; scratch supplies no backup independence.

The slice template requires exact backing `@IO_DEVICE@` and nonzero custodian
approved read/write BPS and IOPS values. Bind every real backing device when
storage spans multiple devices. Missing enforceable device/quota/IO controls
block activation. Docker container logs rotate at 10MiB x 3 by default inside
the scratch hard limit; do not override that with unbounded logging. Daemon
stdout/stderr are discarded to avoid unbounded host journal/file growth. Systemd
state/exit data remain observable, but no retained daemon console evidence is
claimed. If native daemon console capture is needed, prepare a separately
reviewed bounded capture under an enforced allocation before activation.

RootlessKit explicitly uses slirp4netns/builtin, disables host-loopback and
detached-netns acceleration, and requests slirp sandbox/seccomp. Verify the actual
retained versions implement these controls; unsupported controls block setup.
Default and user-created bridge publications bind 127.0.0.1; require loopback
explicitly for kind's API and all approved publications. Prelaunch inventory must
bind **every layer**: protected host/LAN/VPN/management/Kubernetes routes and
addresses; actual RootlessKit/slirp transit subnet/gateway/DNS; Docker default
bridge and all allocation pools; kind node network; kind pod/service CIDRs; and
actual selected CNI IPAM pools/tunnels/routes. Retain source-bound settings from
the unchanged runner and pinned kind/node/CNI assets for layers not yet created.
Unresolved ranges or unintended overlaps block launch; do not assume defaults,
change protected networking or patch the existing runner to fit. Any intentional
kind/CNI shared pod range must have an exact same-purpose identity, not an
ambiguous overlapping allocation. During the sole admitted attempt, verify actual
kind/CNI realization against those bindings before application/workload proceeds;
mismatch fails that attempt without a probe cluster or retry.

Prohibit host network mode and host sockets. The exact host-bind allowlist for the
existing runner is (1) its freshly owned `paths.primary/<runId>` directory,
read-write at `/var/local-path-provisioner`, with the retained device/inode,
filesystem/quota ownership and private propagation; and (2) pinned kind's host
`/lib/modules` at node `/lib/modules`, read-only, with exact observed backing and
no additional module load/change authorized. Retain the exact pinned node's
engine-private `/var` volume identity plus runtime-created namespace mounts;
these confer no additional host-bind authority. Inspect every actual mount/type/
source/destination/option against the reviewed create manifest inside the admitted
attempt. Other host binds, protected host roots (`/`, `/etc`, `/var/lib`, `/run`,
`/proc`, `/sys`, `/dev`), host runtime sockets and protected cluster storage are
denied. The two exact binds above are exceptions only at their named source and
destination; no parent/symlink widening is allowed. Primary storage is preserved;
S3 staging/evidence are not newly mounted into kind. All actual mount identities
remain operational inputs. Rootless networking
is **not an egress security boundary**: outbound access can still reach host/LAN
services. Any stronger fencing requires a separate explicit host change, never
silent host firewall, route or sysctl edits.

## Staged host procedure — pending new host-change approval

1. Establish an existing trusted host execution transport and exact custodian
   account mapping on node1. Local kubeconfig metadata for `jpiquot@local` at
   `192.168.1.30:6443` proves neither a host account nor write authority. Earlier
   SSH failures grant no current access. Do not retry credentials, decrypt
   historical access exports, or substitute a privileged diagnostic/install Pod.
   Such a Pod would need its own exact manifest, new approval and UID cleanup;
   none is part of this proposal or the consumed capture.
2. Before installation, bind node/machine identity and record protected baseline:
   Kubernetes UID/workloads/PV/PVC identity and health; kubelet/containerd/etcd and
   management service state; their PID/start/executable/cgroup identities; routes,
   addresses, interfaces, firewall rules, namespaces and mounts; existing accounts,
   subordinate IDs, retained runs/disks and applicable reservations. Keep private
   custody and bounded allowlisted output, without credentials/environment/argv
   dumps. The old node UID `46d8d528-420d-4501-a6d8-12680d8a6ac6` and machine ID
   `af92d428c6104784a4011c389e3bc6e6` are historical bindings, not a fresh check.
3. Read prerequisites without launching Docker/containers: user namespace policy,
   verified mapping helpers and nonoverlapping subordinate ranges, native overlay
   backing/kernel compatibility, cgroup v2/systemd/delegation, AppArmor permission,
   required iptables modules, kind rootless support and adequate inotify/PID/kernel
   settings. Missing prerequisites block setup. Do not load kernel modules, alter
   AppArmor/sysctls/boot settings or restart shared services. Any necessary change
   first becomes a separately enumerated new host-change proposal.
4. The custodian must account competing Kubernetes, OS, management and retained
   allocations and approve net enforceable **12250m/23GiB**, zero swap, PIDs,
   scratch hard quota/free margin and exact device IO rates. Do not change kubelet
   reservations. If enforceable non-disruptive headroom cannot be established,
   refuse node1 activation; gross totals/current load are insufficient.
5. Resolve and hash every template, tool and proposed destination. The exact new
   host-change decision must enumerate account/UID/GID/sub-ID entries/private
   paths; existing quota facility and exact quota/IO changes; the two dedicated
   system drop-ins; private user unit/configs; daemon-reload, dedicated user
   manager creation/start and **one empty-engine start**; its bounded rollback
   and protected before/after reads. No global template edit, lingering, automatic
   boot enablement or existing object replacement is included. Account creation
   and user-manager startup are host changes, even before Docker starts. Rendered
   tokens/nulls are not authority to perform them. Do not re-request existing
   calibration approval or repeat completed S3 review.
6. Only after that exact approval, the host custodian creates the newly owned
   account/paths, applies the resolved private configs and instance-specific caps,
   validates them with installed `systemd-analyze verify` and verified
   `dockerd --validate --config-file=...` (neither starts a daemon), and starts
   only the dedicated user manager/unit. Ensure the capped ancestor exists before
   any dedicated user workload starts. Do not run a setuptool that changes
   contexts/enables services or stops shared Docker. Reconcile the protected
   baseline immediately; unexplained drift blocks further use.

The following are **unexecuted parameterized host argv**, for the eventual exact
host-change decision. `<uid>`, `<gid>`, both inclusive subordinate range ends,
resolved source paths/toolset, quota/IO operations and trusted transport are still
missing. An existing account/group/home/destination refuses rather than being
adopted. The custodian first verifies every pathname component is a real owned
directory, no concurrent installer exists, and every to-be-created path is absent.
Use the installed verified system tools; paths below do not claim their hashes.

```text
/usr/sbin/groupadd --gid <gid> hexalith-calibration
/usr/sbin/useradd --uid <uid> --gid <gid> --no-user-group --no-create-home --home-dir /home/hexalith-calibration --shell /usr/sbin/nologin --password ! --no-log-init --key SUB_UID_COUNT=0 --key SUB_GID_COUNT=0 --key CREATE_MAIL_SPOOL=no hexalith-calibration
/usr/sbin/usermod --add-subuids <subuid-start>-<subuid-end> --add-subgids <subgid-start>-<subgid-end> hexalith-calibration
/usr/bin/mkdir --mode=0700 -- /home/hexalith-calibration
/usr/bin/chown <uid>:<gid> /home/hexalith-calibration
```

Create only the newly inventoried account subdirectories, parent first, with
`/usr/bin/mkdir --mode=0700 -- <fresh-directory>` and
`/usr/bin/chown <uid>:<gid> <fresh-directory>`. The list is `.config`,
`.config/systemd`, `.config/systemd/user`, `.config/hexalith-calibration`, `.docker`,
`tools`, `tools/<toolset>`, `tools/<toolset>/bin`, `engine-scratch`, and its
`docker`, `tmp`, `cache`, `build` subdirectories. Each is below the new home.
The quota facility must already exist; the custodian must supply the exact
facility/device-specific quota/IO argv and hard-limit readback before using
scratch. No guessed quota command, filesystem enablement or loopback device is
part of this proposal. Create only these two root-owned directories with
`/usr/bin/mkdir --mode=0755 -- <fresh-directory>`:

```text
/etc/systemd/system/user-<uid>.slice.d
/etc/systemd/system/user@<uid>.service.d
```

After resolving tokens and hashing source bytes, a local exclusive install can
use the following parameterized `python3 -c` argv for **one file** at a time.
`<mode>` is `0600` for configuration or `0755` for a verified executable; `<owner>`
and `<group>` are `0 0` for the system drop-ins, or the dedicated UID/GID for the
account's files. The source/destination parent inventory above is a precondition.
The exclusive open refuses existing files/symlinks; errors leave only the newly
created partial destination for explicit disposition. Nothing is sourced as shell.

```text
/usr/bin/python3 -c 'import os,sys; data=open(sys.argv[1],"rb").read(); fd=os.open(sys.argv[2],os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,int(sys.argv[5],8)); os.fchmod(fd,int(sys.argv[5],8)); os.fchown(fd,int(sys.argv[3]),int(sys.argv[4])); f=os.fdopen(fd,"wb"); f.write(data); f.flush(); os.fsync(f.fileno()); f.close()' <resolved-source> <fresh-destination> <owner> <group> <mode>
```

Install only the mapped configs and already verified complete toolset. Rehash
actual destination bytes and ownership/modes; do not install the `.in` bytes.
After quota/IO and ancestor caps are bound, syntax validation and activation argv
are:

```text
/usr/bin/systemd-analyze verify --man=no --generators=no user-<uid>.slice user@<uid>.service
/home/hexalith-calibration/tools/<toolset>/bin/dockerd --validate --config-file=/home/hexalith-calibration/.config/hexalith-calibration/daemon.json
/usr/bin/systemctl daemon-reload
/usr/bin/systemctl start user@<uid>.service
/usr/sbin/runuser --user hexalith-calibration -- /usr/bin/env -i HOME=/home/hexalith-calibration USER=hexalith-calibration LOGNAME=hexalith-calibration PATH=/home/hexalith-calibration/tools/<toolset>/bin:/usr/bin:/bin:/usr/sbin:/sbin XDG_RUNTIME_DIR=/run/user/<uid> DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/<uid>/bus /usr/bin/systemd-analyze --user verify --man=no /home/hexalith-calibration/.config/systemd/user/hexalith-calibration-docker.service
/usr/sbin/runuser --user hexalith-calibration -- /usr/bin/env -i HOME=/home/hexalith-calibration USER=hexalith-calibration LOGNAME=hexalith-calibration PATH=/home/hexalith-calibration/tools/<toolset>/bin:/usr/bin:/bin:/usr/sbin:/sbin XDG_RUNTIME_DIR=/run/user/<uid> DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/<uid>/bus /usr/bin/systemctl --user daemon-reload
/usr/sbin/runuser --user hexalith-calibration -- /usr/bin/env -i HOME=/home/hexalith-calibration USER=hexalith-calibration LOGNAME=hexalith-calibration PATH=/home/hexalith-calibration/tools/<toolset>/bin:/usr/bin:/bin:/usr/sbin:/sbin XDG_RUNTIME_DIR=/run/user/<uid> DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/<uid>/bus /usr/bin/systemctl --user start hexalith-calibration-docker.service
```

The first syntax check resolves the host's synthesized slice and existing vendor
user-manager template with new instance drop-ins. Bind their actual load paths
rather than creating main unit files. If installed syntax tooling cannot resolve
these instances, report that blocker instead of pretending validation passed.
No generator, host start or privileged validation is run by the offline renderer.
Verify the actual user manager is maintained for the required window without
enabling lingering or automatic boot startup; missing lifecycle evidence blocks
calibration. Before the first empty-engine start, recheck effective caps and the
protected baseline. If any step fails, stop at that step and inventory only the
newly created objects; never replay uncertain creation.

After exact supported calibration disposal and empty/identity checks, the scoped
stop argv use that same private user-bus environment, replacing the final command
with `/usr/bin/systemctl --user stop hexalith-calibration-docker.service`, followed
by `/usr/bin/systemctl stop user@<uid>.service`. Do not stop another user's manager
or broad `user.slice`. Verify runtime-dir cleanup; stop
`user-runtime-dir@<uid>.service` only if it was newly owned, is empty, and the
explicit teardown approval names it. Account/group/sub-ID removal is a later
exact inventory action, not an automatic command in this recipe.

## Empty-engine verification and existing runner routing

Verification creates **no test container or kind cluster** and consumes no
calibration attempt. On node1 under the same capped account, use only the verified
client with an explicit clean config and exact local Unix socket, for example:

```text
/home/hexalith-calibration/tools/<verified-toolset>/bin/docker --config /home/hexalith-calibration/.docker --host unix:///run/user/<actual-uid>/hexalith-calibration/docker.sock info --format '{{json .}}'
/home/hexalith-calibration/tools/<verified-toolset>/bin/docker --config /home/hexalith-calibration/.docker --host unix:///run/user/<actual-uid>/hexalith-calibration/docker.sock ps --all --quiet
/home/hexalith-calibration/tools/<verified-toolset>/bin/docker --config /home/hexalith-calibration/.docker --host unix:///run/user/<actual-uid>/hexalith-calibration/docker.sock volume ls --quiet
/home/hexalith-calibration/tools/<verified-toolset>/bin/docker --config /home/hexalith-calibration/.docker --host unix:///run/user/<actual-uid>/hexalith-calibration/docker.sock network ls --no-trunc
```

Require actual nonempty engine ID, rootless SecurityOptions, cgroup v2/systemd,
native overlay2, exact data root and no containers/volumes. Inspect each default
network, its IDs/options/CIDRs, and actual sockets/mount/network namespaces;
retain them for empty-only teardown. Verify socket ownership/mode/peer UID,
daemon/RootlessKit/slirp/private containerd PID/start/executable/UID ancestry,
private containerd roots and no shared endpoint. No TCP API may listen. Inspect
systemd ControlGroup and effective `cpu.max`, `memory.max`, `memory.swap.max`,
`pids.max`, `io.max` and delegated controllers at the slice, manager, daemon and
**every ancestor**; inspect all dedicated UID processes/scopes. Re-read actual
quota/inode limits, free margin and backing-device enforcement. A loaded unit
file or Docker-reported host capacity alone is insufficient. Absent data stays
explicitly absent; it never becomes an inferred pass.

The existing `independent_calibration.py`/`qualify.py --calibration` runner executes
locally on node1 as this same user, within this slice. Its actual plan must bind
`docker.host = unix:///run/user/<actual-uid>/hexalith-calibration/docker.sock`,
the observed engine ID and exact verified local Docker tool path/hash. Its
subprocess environment supplies the explicit Docker host, strips context overrides
and **does not retain `DOCKER_CONFIG`**. Bind its inherited `HOME` to the dedicated
private home and install the clean client config at that account's default
`~/.docker/config.json`, with no currentContext, credentials, helpers or plugins.
Inspect actual routing rather than inheriting `DOCKER_CONTEXT` or an SSH/TCP
engine. Start the runner in a dedicated user-manager scope under the same capped
slice, never a root/system scope. Keep all runner children/builds in its admitted
allocations. No new wrapper, context switching or alternate runner is required.

The sibling runner does **not** inherit the daemon unit's scratch environment.
Its child scrub retains `PATH`, `HOME`, `TMPDIR`, `DOTNET_ROOT` and
`NUGET_PACKAGES`; it drops `XDG_CACHE_HOME` and `DOTNET_CLI_HOME`. Do not rely on
those dropped variables to bound child growth. After all admission/collector
holds are satisfied, the unexecuted routing shape for the existing command is:

```text
/usr/sbin/runuser --user hexalith-calibration -- /usr/bin/env -i HOME=/home/hexalith-calibration USER=hexalith-calibration LOGNAME=hexalith-calibration PATH=/home/hexalith-calibration/tools/<toolset>/bin:/usr/bin:/bin:/usr/sbin:/sbin TMPDIR=/home/hexalith-calibration/engine-scratch/tmp NUGET_PACKAGES=/home/hexalith-calibration/engine-scratch/cache/nuget XDG_RUNTIME_DIR=/run/user/<uid> DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/<uid>/bus /usr/bin/systemd-run --user --scope --unit=hexalith-calibration-run-<runId> --property=Delegate=yes --working-directory=<bound-source-root-on-enforced-allocation> <bound-python> -B <bound-platform-source>/eng/helm-qualification/qualify.py --calibration launch --calibration-state <actual-state> --approved-plan-sha256 <actual-approved-plan-sha256>
```

Pre-create the private `cache/nuget` below the enforced scratch boundary. Bind
source build `bin/obj` and other default relative output to that same scratch
`build` boundary or the existing independently enforced host-workspace allocation.
The dedicated home stays the Docker client home: default `.dotnet`, `.cache`,
MSBuild, tool/telemetry caches and any other default-home growth require an actual
separately enforced byte/inode allocation bound to the existing host-workspace
reservation. Record its exact limit/usage and competing bytes, without inventing
additional capacity or counting scratch twice. Missing enforceable home/source
growth custody blocks launch. Retained `TMPDIR`/`NUGET_PACKAGES` do not establish
that separate boundary. Verify actual parent/child environments and cgroups with
allowlisted fields, never secret dumps; use this same bound context for supported
prepare/inspect/stop/remove as applicable. No native file is edited to accomplish
routing; all ten existing native source hashes must remain unchanged.

Only after these observations and unchanged protected-baseline readback may the
coordinator bind real inputs into the existing 37-null S3 draft. This bundle
neither edits that draft nor signs anything. Preserve all ten native source
bindings, runtime/source/Debug identity, signed net reservations, S3 principal/
project/bucket/credential observations, trusted UTC/current action authority,
absolute window, monitoring and existing admission gates. Actual rootless kind
compatibility is tested **inside the existing approved one calibration attempt**,
after admission, with original 600s startup/50400s execution/900s cumulative
disposal bounds, zero retries/restarts and unchanged workload/assertions. No
compatibility probe cluster is created first.

## Ongoing observation — operational collector hold

For the bounded empty-engine setup, the approved custodian checks identity,
caps, quota, protected state and elapsed time before each verification operation
and during its separately bounded setup rollback. No calibration or test container
runs in that step. The dedicated unit has 120s start and stop timeouts; bind the
whole setup/verification/rollback deadline and stop on failure or unknown ownership.

Before calibration launch, bind a real bounded collector, its reviewed source/tool
hashes, private evidence allocation, exact engine/UID/PID/start/cgroup/quota/device/
network selectors and original execution/stop deadlines. It must continue through
calibration execution and disposal, sampling at least
every **5 seconds**, bounding each complete sample to **5 seconds**, and refusing
sample age over **10 seconds** on both trusted UTC and original monotonic time.
Keep sequence/time/source identity and explicit gaps, without secret values.
Bound capture files within a separately enforced existing evidence allocation.
The current allocation monitor does **not** cover this added 24GiB scratch,
default-home growth, daemon/user-slice ancestors or all these identities.
This renderer implements no such collector. Its actual implementation, binding,
continuous failure-to-stop path and offline verification remain an explicit
**calibration launch hold**; prose or one-time observations cannot discharge it.

Each sample must recheck engine/socket/UID and all runtime process identities;
every effective ancestor CPU/memory/swap/PID/I/O limit and delegated controller;
dedicated UID membership under the capped slice; quota byte/inode hard limits,
usage and >=20% free margin for scratch and separately bounded home/source/
evidence; original primary/staging reservation custody; and network/mount identity
against the reviewed baseline. Retain memory/PID limit-event counters and stop on
new OOM/kill or PID-limit hits, loosened/missing limits, controller/quota loss,
wrong engine/UID/ancestry, exhausted margin, unexpected mount/network/protected
drift, expired authority/time/window, missing/stale/failed samples or collector
exit. No new command may proceed using the last good sample after a gap.

For kind nodes, continuously consume exact-engine lifecycle events and inspect
the exact recorded Docker IDs, `RestartCount`, `StartedAt`, running/exit state and
restart policy every sample from first creation through confirmed removal. A
missed event interval is unverified, not proof of no restart. Any restart, changed
start time after the original start, replaced ID or unexplained exit makes the
one attempt failed/withdrawn; preserve observations before exact supported stop.
`Restart=no` only controls the daemon unit; kind's native `on-failure:1` is retained
and can restart a node before a poll. Absent implemented continuous enforcement
blocks launch; do not silently alter kind/container policy to manufacture a
zero-restart claim.

On a breach after calibration state exists, the collector/supervisor must latch
failure, prevent further execution and request supported exact-state stop/remove
under the **original cumulative 900s** allowance, preserving S3/primary custody.
Cleanup observation continues within that same remaining allowance; it never
resets it. If original identity or authority cannot be established, retain unknown
resources and report unverified rather than deleting by guessed selectors. Before
any calibration state exists, use the separate setup-only branch below. Binding a
finite supported failure-to-stop mechanism is required before calibration launch; none
has been executed or claimed supplied by this offline bundle.

## Cleanup and failure custody

**Setup-only failure, before calibration state exists:** do not invent a state,
run ID or calibration stop/remove command. The new host-change approval must bind
its own finite setup/rollback deadline (including the unit's 120s stop timeout),
exact newly created account/unit and recorded PID/start/socket/data identities.
Missing bound rollback authority/deadline blocks activation. If the new engine is
positively identified and empty with no calibration resources, the custodian uses
the private user-bus `systemctl --user stop hexalith-calibration-docker.service`
argv above, then stops only newly owned `user@<uid>.service`. Verify exact process,
socket/network/mount absence and protected baseline. If daemon verification failed
before an engine ID is available, only the positively recorded newly created unit/
UID/PID lineage may be stopped under that setup authority; make no Docker API
delete/prune call. Unknown ownership, nonempty engine, failed stop or deadline
exhaustion means retained/unverified, with no automatic restart or deletion.
Preserve all fresh files/account/quota/evidence for explicit disposition. This
branch consumes neither the unused calibration attempt nor its 900s disposal
authority. A setup rollback deadline is not a new calibration budget.

**After calibration state exists:** first use supported
`qualify.py --calibration stop/remove` against the exact
original state/run/engine/UID, authentic original authority and remaining original
**cumulative 900s** disposal allowance. Preserve its actual failure/unknown
disposition, native evidence and immutable locked S3 objects. S3 uncertainty
retains primary/staging as prescribed by the existing adapter. Engine teardown
is a separate new-host-change action and cannot renew calibration cleanup time.

After supported removal, verify the bound engine still belongs to this UID,
is the same engine/socket/data/process lineage, and has zero containers/volumes
and no remaining calibration resources or in-flight ownership. Record any images
and exact default-network IDs. If anything is unknown/foreign/nonempty, retain
the engine and mark cleanup unverified; do not prune. Only the approved custodian
may stop this exact unit and newly created user manager. Confirm all recorded
UID/PID/start identities and descendants absent, Unix/managed-containerd sockets
gone, private rootless network/mount namespaces and owned interfaces absent,
no surviving UID scopes, and protected baseline unchanged. Never stop host
containerd, kubelet, etcd or management services.

Only with explicit removal authority and a fresh ownership/no-mount/no-symlink
inventory may the custodian remove newly created scratch/runtime/config/unit/
quota entries and the newly created account/sub-ID entries. Retained primary,
staging, evidence, source and locked backup objects remain outside that deletion.
No wildcard path deletion, broad label deletion, shared Docker/network prune,
pre-existing account/artifact removal, old cleanup approval or automatic restart.
Unknown/failed teardown stays retained/unverified with the exact remaining objects
and next custodian action recorded. No successful backup/restore, capacity,
qualification, Production or A41 conclusion follows from an empty engine.

References checked for this design: [Docker rootless prerequisites](https://docs.docker.com/engine/security/rootless/),
[Docker user service/cgroups](https://docs.docker.com/engine/security/rootless/tips/),
[kind rootless requirements](https://kind.sigs.k8s.io/docs/user/rootless/),
[Docker rootless script](https://github.com/moby/moby/blob/master/contrib/dockerd-rootless.sh),
[systemd resource controls](https://www.freedesktop.org/software/systemd/man/latest/systemd.resource-control.html).
Examples that disable shared services or change global host settings are not adopted.
