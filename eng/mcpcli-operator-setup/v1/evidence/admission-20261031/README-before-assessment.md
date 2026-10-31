# Three-service operator preparation

Execution update (2026-10-31): the current packet was approved and all owner phases through handoff passed, including genuine readiness and persisted tenant/member. The environment is retained for human client checks. Operator token expiry is **2026-10-31 09:54:53 UTC**. See the [current live handoff](../../../../mcpcli/_bmad-output/implementation-artifacts/evidence/story-4-17/three-service-preparation-20261031/operator-handoff.md). The prepared procedure and snapshot below are retained as pre-execution evidence; do not repeat completed phases. Actual client/timing acceptance and later cleanup remain pending.

## Retained preparation and execution procedure

The current review candidate is [packet-20261031-gateway-api-review/review.json](packet-20261031-gateway-api-review/review.json), SHA-256 `063bac572c44e85441169aa4410b4001886f5ae7ed6e212f3c5138c607e86be4`. It targets context `jpiquot@local`, API `https://192.168.1.30:6443`, namespace/tenant `mcpcli-417-20261031-155f9b6d`. It expires **2026-11-01 07:37:50 UTC**. This changed packet needs fresh human review before application. Three earlier exact packets were approved and attempted, then cleaned up by recorded UID. The first failed direct-Pod injection; the second failed node-local DNS. The third passed actual DNS, sidecar readiness and persisted administrator lookup, then activated writer protocol v2 once, but Gateway /ready remained HTTP503 because its unchanged raw actor-placement metadata probe received401. An authenticated diagnostic GET proved actual actorRuntime.hostReady=true; the raw GET returned401. Seed/handoff never ran. Preserve the original activation marker, uncertain writer journal, backups and all three failed/cleaned namespaces. No activation is to be retried in the retired store, and no prior approval authorizes this changed packet. Recovery preparation applied no resources and called no application APIs.

This reproduces McpCli's three-app fixture: EventStore Gateway, Tenants and Parties, plus one private Redis state/pubsub provider. The packet lists every resource, immutable image, resource bound, Secret name/key, source binding and seed payload. It does not qualify Platform's full closure. Image inspection proves required static capabilities; actual Dapr 1.18.1 interoperability, injected Pods and persistence remain installation checks. The image Contracts and restored package have distinct hashes; the eight required Tenants surfaces and projection dispatch DTO comparisons report no API changes. Kubernetes target 1.34.9 exposes the required Job controls; its controller manager uses service-account credentials and kube-system/job-controller exists. The pinned [Dapr 1.18.1 injector source](https://raw.githubusercontent.com/dapr/dapr/v1.18.1/pkg/injector/service/injector.go) allows this controller by default, and [Kubernetes 1.34 Job policy documentation](https://v1-34.docs.kubernetes.io/docs/concepts/workloads/controllers/job/) explains the selected controls. No identity impersonation, shared injector modification or application source/image/pin change is involved. Actual admission/injection remains an execution gate. See [controller-support.json](evidence/controller-support.json), [startup-order.json](evidence/startup-order.json), [evidence/images.json](evidence/images.json), [wire-bindings.json](evidence/wire-bindings.json) and [target-observation.json](evidence/target-observation.json).

Review the rendered six YAML files and `seedOperations`, `adaptations`, `bounds`, `secretReferences` and `evidenceHashes` in the review document. Private input files are under `/tmp/mcpcli-417-v1-private-20261031-attempt8/`, directory mode 0700/file mode 0600. Their manifest SHA is public; values and individual credential hashes stay private. The infrastructure phase creates a namespace-local image-pull Secret from the configured registry identity, delegated only for `registry.hexalith.com`; its upstream account scope can be broader than this task and needs explicit review. It creates fresh task signing, channel and Redis credentials. It retrieves no Kubernetes Secret resources and requires no external secret provider.

The existing injector uses non-root/read-only settings but does not drop all sidecar capabilities. This packet therefore enforces Pod Security **baseline**, with restricted warnings/auditing; app containers drop ALL capabilities. No shared injector setting changes. All Services are ClusterIP and bootstrap endpoints publish private unready addresses so genuine `/ready` gating stays intact. Applications are batch/v1 Jobs owned by the existing controller: one completion, one parallel Pod, restartPolicy Never, backoffLimit zero, podReplacementPolicy Failed and FailJob for disruptions/nonzero exits. Redis remains a plain Pod. The runner captures the created Job UID, resolves a single Pod by task/app labels plus controlling owner UID, binds its Pod UID/name and requires actual daprd injection before Running/liveness/forwarding. It rejects duplicate, replaced, terminal or restarted application Pods. The observed injector uses ordinary sidecars; valid native-init daprd sidecars are also checked when present. These policies disable normal retries/replacements on failure; Kubernetes Jobs are not a transactional exactly-once guarantee, and no bootstrap restart/recreation is authorized. Redis has a dedicated 1Gi PVC and authenticated ACL; the namespace has CPU/memory/pod/storage quotas and network isolation. Parties' legacy Development Tenants bootstrap and Memories search are explicitly disabled.

The Gateway-only local API adaptation is explicit: the optional `DAPR_API_TOKEN` env, `dapr.io/api-token-secret` annotation and unused `mcpcli-eventstore-api` Secret are removed. The resulting **eight Secrets** are exactly those in `secretReferences`. Its HTTP3500/gRPC50001 APIs remain strictly `127.0.0.1` inside the Pod and are omitted from the Gateway Dapr ClusterIP Service; internal invocation50002 and metrics9090 remain. Trusted processes within that Pod can use these local APIs. Gateway `APP_API_TOKEN` app-channel protection, public HTTP Gateway JWT authentication, Dapr mTLS, namespace network isolation and Tenants/Parties API tokens remain required and guarded. No health check, app `/ready`, image, accepted tool, verified authority boundary or dependency pin is replaced or disabled. [gateway-api-mode.json](evidence/gateway-api-mode.json) retains actual compiled probe calls, fixture token distinction, prior real401/200 observations and the bootstrap-authority assessment.

The metadata phase rechecks actual injected Pod loopback/API/app-token/JWT/mTLS configuration and all bound runtime identities, then opens its own temporary `127.0.0.1:18171` forward to the owned Gateway sidecar3500. It performs only unauthenticated GET `/v1.0/metadata` with no Authorization or dapr-api-token header, matching the unchanged real health probe. The response must identify `eventstore` and contain actual boolean `actorRuntime.hostReady=true`. Read-only startup failures/unready/missing data can poll within60 seconds;401/403 and identity mismatch stop immediately. The owned forward closes on success or failure. No placement success is fabricated during preparation, and no query/writer/seed follows a failed gate. Genuine Gateway `/ready` and all app readiness remain mandatory after the one real writer activation.

The initial `WireAdminAssertionRemoved` bootstrap warning is not treated as a proven seed defect: subsequent persisted administrator lookup succeeded. Static inspection of the actual Tenants image shows its read-model-backed verifier and domain verifier calls; the newer checkout differs. Seeding still uses the now-persisted `mcpcli-live-admin` subject through the real verified boundary and the exact two reviewed CreateTenant/AddUser payloads. No sample command was submitted and no signed/admin check weakened.

From the owning Platform checkout, these checks are local and authorized now:

```bash
cd /home/administrator/projects/hexalith/platform
python3 -m unittest discover -v -s eng/mcpcli-operator-setup/v1 -p 'test_*.py'
python3 eng/mcpcli-operator-setup/v1/phases.py \
  --packet eng/mcpcli-operator-setup/v1/packet-20261031-gateway-api-review \
  --review-sha256 063bac572c44e85441169aa4410b4001886f5ae7ed6e212f3c5138c607e86be4
```

Each execution verifies the reviewed document, exact runner/helper, fixture/evidence/manifests/private-input hashes, scope and expiry before the phase. Source or credential edits require a fresh packet and review. An expired packet must be prepared into fresh output/private paths, retaining this attempt; do not edit its expiry. Immediately before approval/application, repeat local verification and the bounded namespace-absence, context/API, Dapr/storage observations recorded in the validation evidence. Stop if target metadata has materially changed.

After explicit approval of this exact packet, run the following sequence in one Bash session. `set -e` stops at the first failure. Run each phase once and inspect its result before continuing. `--execute-reviewed` expresses an already granted approval; it is not permission to skip review.

```bash
cd /home/administrator/projects/hexalith/platform
set -e
operator_packet=eng/mcpcli-operator-setup/v1/packet-20261031-gateway-api-review
operator_review=063bac572c44e85441169aa4410b4001886f5ae7ed6e212f3c5138c607e86be4
operator_phase() {
  python3 eng/mcpcli-operator-setup/v1/phases.py --packet "$operator_packet" \
    --review-sha256 "$operator_review" --execute-reviewed --phase "$1"
}
operator_phase infrastructure
operator_phase dns
operator_phase backup
operator_phase gateway
operator_phase tenants
operator_phase parties
operator_phase startup
operator_phase sidecars
operator_phase metadata
operator_phase administrator
operator_phase writer
operator_phase seed
operator_phase handoff
```

Infrastructure creates only the task namespace, private Secrets, provider and supporting resources. Backup requires Redis to be the only Pod, observes DBSIZE zero, runs provider-native SAVE and retains the actual RDB (REDIS header/SHA), namespace/Pod/PVC UIDs and pre-application receipt. No backup or UID is fabricated in this prepared packet. The dns phase first checks the actual provider resolver and policy, then performs bounded DNS reads as detailed below. Backup follows successful DNS verification. The gateway, tenants and parties phases create their three Jobs after the backup, capturing Job/Pod identities and injection. They do not wait for application HTTP liveness/readiness between creates. This follows the qualified fixture and avoids Gateway startup metadata calls waiting for deliberately absent domain services. Startup then rechecks all owned Pods/injection and observes Gateway `/alive`. The sidecars phase separately waits at most 120 seconds for actual daprd container readiness in each bound Pod (ordinary or native-init status), verifies running/not-restarted/not-terminated state and binds its runtime container ID. It does not require application `/ready` at that point. Missing/unready status can recover within that bounded observation; terminal or changed identity stops execution. Metadata then checks the real raw Gateway sidecar endpoint and actor host readiness before administrator/writer, as described below. Administrator observes persisted `mcpcli-live-admin` via the owner query. Writer submits exactly one real activation using the Gateway source commit and actual backup reference, then observes `/ready` and all three Pods Ready before seed. Readiness is not bypassed. Seed submits exactly one CreateTenant and one AddUserToTenant, observes committed command status, then queries the persisted tenant and Contributor membership. Administrator identity is separate from the client identity. Only observations poll, within deadlines. GET connection refusal/disconnect/timeout is retained as a sanitized transport observation and can recover within that existing poll deadline; an exhausted deadline still fails. Activation and domain POSTs are never retried. Private `execution/observations.jsonl` retains HTTP status/transport error types without credentials or payloads.

The DNS adaptation is exact: [dns-target.json](evidence/dns-target.json) binds the observed `nodelocaldns` hostNetwork daemonset and prior real Pod resolver `169.254.25.10`. Existing Platform NetworkPolicy source already uses that address. The rendered policy adds only `169.254.25.10/32` UDP/TCP 53 alongside legitimate kube-system DNS, with other isolation rules preserved. No broad egress, resolver override, shared DNS change, image change or extra probe workload is introduced.

After infrastructure establishes the pinned Redis Pod, dns verifies its recorded Pod/PVC identity, requires the installed NetworkPolicy spec to match the reviewed manifest and requires `/etc/resolv.conf` nameservers to equal the reviewed address list. It resolves the task Redis FQDN and the actual Sentry, operator, placement and scheduler FQDNs in `review.json`. The exact pinned Alpine Redis image contains getent, cat and BusyBox timeout. Their real extracted binary/argument evidence is retained in `evidence/dns-recovery-20261031/provider-image-tools.json`; a local isolated CLI capability check is not DNS proof. DNS uses `/usr/bin/timeout -s TERM -k 1 5 /usr/bin/getent ahostsv4 NAME`, with a 45-second observation deadline and bounded in-flight commands. It needs no packages installed into a Pod and no temporary workload. No DNS success receipt is invented during preparation.

Private `execution/dns-observations.jsonl` retains hostname, exit code, bounded stdout/stderr and parsed IPv4 answers; sidecar observations retain safe status/identity metadata. A DNS/policy/resolver failure prevents backup/application Jobs. A sidecar readiness or metadata/actor-placement gate failure prevents persisted administrator queries and all later mutations. Bootstrap and access forwards/handoff recheck actual sidecar readiness and the bound runtime IDs; a changed or lost runtime stops further operations. These checks preserve the real writer/application readiness and persisted membership gates.

Every attempted phase leaves a private journal in `/tmp/mcpcli-417-v1-private-20261031-attempt8/execution/`. An uncertain POST, terminal command failure or deadline failure stops subsequent phases. Never remove/edit the journal, rerun a mutation, restart an uncertain bootstrap or reuse its namespace as a new attempt. Retain evidence and investigate; after reviewed cleanup, a new attempt uses new private paths/namespace and fresh approval. Cleanup can inspect the recorded namespace UID even if infrastructure failed after namespace creation.

Handoff is created only after persisted seed observations. It writes the private operator profile at `/tmp/mcpcli-417-v1-private-20261031-attempt8/operator-home/.eventstore/mcpcli.json`, using subject `mcpcli-417-operator-20261031-155f9b6d`, TenantContributor, `global_admin=false`, tenants `system` plus the task tenant, and domains `tenants`/`party`. No bootstrap administrator token goes to clients. The client token expires two hours after handoff, capped by review expiry; the exact UTC expiry is in `execution/handoff.json`. Schedule the three client sessions and independent attempt inside that window. Expiration is a recorded setup outcome; there is no automatic refresh or mutation replay. Stop before expiry or obtain a newly reviewed attempt.

After handoff, keep this reviewed, ownership-checked forward running in a separate terminal in the **same native/WSL network environment as the clients**:

```bash
cd /home/administrator/projects/hexalith/platform
python3 eng/mcpcli-operator-setup/v1/phases.py \
  --packet eng/mcpcli-operator-setup/v1/packet-20261031-gateway-api-review \
  --review-sha256 063bac572c44e85441169aa4410b4001886f5ae7ed6e212f3c5138c607e86be4 \
  --forward
```

The forward resolves the Pod from the recorded Job/Pod UIDs rather than assuming `pod/eventstore` or selecting an arbitrary Pod. It requires completed handoff and re-verifies the packet, scope, expiry and all three application/sidecar identities, injection and runtime readiness; Ctrl-C stops only its owned forward process.

Use the exact accepted staged 1.2.0 installation and client registrations in [McpCli README](../../../../mcpcli/README.md#operator-setup-with-the-staged-120-tool) and its [session checklist](../../../../mcpcli/docs/release/operator-setup-checklist.md). For ordinary client sessions, `MCPCLI_HOME` may point to the private `operator-home` above; remove inherited EVENTSTORE settings. A native Windows install needs its own protected profile and a reachable native forward, or the documented explicit WSL bridge. Do not put tokens in arguments/client JSON/evidence. Retain actual client/OS versions, initialization, discovery, approved operations, persisted Party evidence and every timing/assistance attempt; these checks are still unexecuted.

The independent README-only attempt requires a fresh installation directory and fresh `MCPCLI_HOME`. Before timing, the owner privately supplies the approved endpoint, tenant, actor, token and expiry as prerequisites. The independent operator starts timing immediately before installation, then performs installation, profile creation/configuration and client registration using only the McpCli README. Reusing this prepared profile or an existing installation cannot establish the full timed setup result. Record any help, preconfiguration, failure or token expiry in the attempt; retain the measured outcome against 900 seconds.

After the approved sessions, stop the forward and use the same reviewed packet to remove only its owned namespace (PVC reclaim policy Delete). The runner confirms ownership and the original namespace UID before deletion:

```bash
python3 eng/mcpcli-operator-setup/v1/phases.py \
  --packet eng/mcpcli-operator-setup/v1/packet-20261031-gateway-api-review \
  --review-sha256 063bac572c44e85441169aa4410b4001886f5ae7ed6e212f3c5138c607e86be4 \
  --execute-reviewed --phase cleanup
```

Cleanup also requires an unexpired review; arrange it within the review window. If the window expires or identity differs, stop and obtain a reviewed recovery decision. Preserve private backup/journal evidence and revoke task credentials through that recovery process; no automated broad cleanup is provided. No package/tag/release publication is part of this packet.
