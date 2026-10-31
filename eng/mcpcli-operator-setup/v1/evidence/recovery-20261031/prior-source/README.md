# Three-service operator preparation

The current review candidate is [packet-20261031-review/review.json](packet-20261031-review/review.json), SHA-256 `79b9b6febc3c6ba8ca9753c1d8a4776f4eef07470fe25dcec3a01b3c84e5cedd`. It targets context `jpiquot@local`, API `https://192.168.1.30:6443`, namespace/tenant `mcpcli-417-20261031-aebefecb`. It expires **2026-11-01 05:40:31 UTC**. Deployment is pending human review; preparation applied no resources and called no application APIs. Earlier packets and private attempts remain retained.

This reproduces McpCli's three-app fixture: EventStore Gateway, Tenants and Parties, plus one private Redis state/pubsub provider. The packet lists every resource, immutable image, resource bound, Secret name/key, source binding and seed payload. It does not qualify Platform's full closure. Image inspection proves required static capabilities; actual Dapr 1.18.1 interoperability, injected Pods and persistence remain installation checks. The image Contracts and restored package have distinct hashes; the eight required Tenants surfaces and projection dispatch DTO comparisons report no API changes. See [evidence/images.json](evidence/images.json), [wire-bindings.json](evidence/wire-bindings.json) and [target-observation.json](evidence/target-observation.json).

Review the rendered six YAML files and `seedOperations`, `adaptations`, `bounds`, `secretReferences` and `evidenceHashes` in the review document. Private input files are under `/tmp/mcpcli-417-v1-private-20261031-attempt4/`, directory mode 0700/file mode 0600. Their manifest SHA is public; values and individual credential hashes stay private. The infrastructure phase creates a namespace-local image-pull Secret from the configured registry identity, delegated only for `registry.hexalith.com`; its upstream account scope can be broader than this task and needs explicit review. It creates fresh task signing, channel and Redis credentials. It retrieves no Kubernetes Secret resources and requires no external secret provider.

The existing injector uses non-root/read-only settings but does not drop all sidecar capabilities. This packet therefore enforces Pod Security **baseline**, with restricted warnings/auditing; app containers drop ALL capabilities. No shared injector setting changes. All Services are ClusterIP and bootstrap endpoints publish private unready addresses so genuine `/ready` gating stays intact. App Pods use restartPolicy Never. Redis has a dedicated 1Gi PVC and authenticated ACL; the namespace has CPU/memory/pod/storage quotas and network isolation. Parties' legacy Development Tenants bootstrap and Memories search are explicitly disabled.

From the owning Platform checkout, these checks are local and authorized now:

```bash
cd /home/administrator/projects/hexalith/platform
python3 -m unittest discover -v -s eng/mcpcli-operator-setup/v1 -p 'test_*.py'
python3 eng/mcpcli-operator-setup/v1/phases.py \
  --packet eng/mcpcli-operator-setup/v1/packet-20261031-review \
  --review-sha256 79b9b6febc3c6ba8ca9753c1d8a4776f4eef07470fe25dcec3a01b3c84e5cedd
```

Each execution verifies the reviewed document, exact runner/helper, fixture/evidence/manifests/private-input hashes, scope and expiry before the phase. Source or credential edits require a fresh packet and review. An expired packet must be prepared into fresh output/private paths, retaining this attempt; do not edit its expiry. Immediately before approval/application, repeat local verification and the bounded namespace-absence, context/API, Dapr/storage observations recorded in the validation evidence. Stop if target metadata has materially changed.

After explicit approval of this exact packet, run the following sequence in one Bash session. `set -e` stops at the first failure. Run each phase once and inspect its result before continuing. `--execute-reviewed` expresses an already granted approval; it is not permission to skip review.

```bash
cd /home/administrator/projects/hexalith/platform
set -e
operator_packet=eng/mcpcli-operator-setup/v1/packet-20261031-review
operator_review=79b9b6febc3c6ba8ca9753c1d8a4776f4eef07470fe25dcec3a01b3c84e5cedd
operator_phase() {
  python3 eng/mcpcli-operator-setup/v1/phases.py --packet "$operator_packet" \
    --review-sha256 "$operator_review" --execute-reviewed --phase "$1"
}
operator_phase infrastructure
operator_phase backup
operator_phase gateway
operator_phase tenants
operator_phase administrator
operator_phase writer
operator_phase parties
operator_phase seed
operator_phase handoff
```

Infrastructure creates only the task namespace, private Secrets, provider and supporting resources. Backup requires Redis to be the only Pod, observes DBSIZE zero, runs provider-native SAVE and retains the actual RDB (REDIS header/SHA), namespace/Pod/PVC UIDs and pre-application receipt. No backup or UID is fabricated in this prepared packet. Gateway and Tenants start afterward; administrator observes persisted `mcpcli-live-admin` via the owner query. Writer submits exactly one real activation using the Gateway source commit and actual backup reference, then observes `/ready`. Parties starts afterward and all three apps must become Ready. Seed submits exactly one CreateTenant and one AddUserToTenant, observes committed command status, then queries the persisted tenant and Contributor membership. Administrator identity is separate from the client identity. Only observations poll, within deadlines.

Every attempted phase leaves a private journal in `/tmp/mcpcli-417-v1-private-20261031-attempt4/execution/`. An uncertain POST, terminal command failure or deadline failure stops subsequent phases. Never remove/edit the journal, rerun a mutation, restart an uncertain bootstrap or reuse its namespace as a new attempt. Retain evidence and investigate; after reviewed cleanup, a new attempt uses new private paths/namespace and fresh approval. Cleanup can inspect the recorded namespace UID even if infrastructure failed after namespace creation.

Handoff is created only after persisted seed observations. It writes the private operator profile at `/tmp/mcpcli-417-v1-private-20261031-attempt4/operator-home/.eventstore/mcpcli.json`, using subject `mcpcli-417-operator-20261031-aebefecb`, TenantContributor, `global_admin=false`, tenants `system` plus the task tenant, and domains `tenants`/`party`. No bootstrap administrator token goes to clients. The client token expires two hours after handoff, capped by review expiry; the exact UTC expiry is in `execution/handoff.json`. Schedule the three client sessions and independent attempt inside that window. Expiration is a recorded setup outcome; there is no automatic refresh or mutation replay. Stop before expiry or obtain a newly reviewed attempt.

Keep this explicit forward running in a separate terminal in the **same native/WSL network environment as the clients**:

```bash
kubectl --context=jpiquot@local -n mcpcli-417-20261031-aebefecb \
  port-forward --address=127.0.0.1 pod/eventstore 18170:8080
```

Use the exact accepted staged 1.2.0 installation and client registrations in [McpCli README](../../../../mcpcli/README.md#operator-setup-with-the-staged-120-tool) and its [session checklist](../../../../mcpcli/docs/release/operator-setup-checklist.md). For ordinary client sessions, `MCPCLI_HOME` may point to the private `operator-home` above; remove inherited EVENTSTORE settings. A native Windows install needs its own protected profile and a reachable native forward, or the documented explicit WSL bridge. Do not put tokens in arguments/client JSON/evidence. Retain actual client/OS versions, initialization, discovery, approved operations, persisted Party evidence and every timing/assistance attempt; these checks are still unexecuted.

The independent README-only attempt requires a fresh installation directory and fresh `MCPCLI_HOME`. Before timing, the owner privately supplies the approved endpoint, tenant, actor, token and expiry as prerequisites. The independent operator starts timing immediately before installation, then performs installation, profile creation/configuration and client registration using only the McpCli README. Reusing this prepared profile or an existing installation cannot establish the full timed setup result. Record any help, preconfiguration, failure or token expiry in the attempt; retain the measured outcome against 900 seconds.

After the approved sessions, stop the forward and use the same reviewed packet to remove only its owned namespace (PVC reclaim policy Delete). The runner confirms ownership and the original namespace UID before deletion:

```bash
python3 eng/mcpcli-operator-setup/v1/phases.py \
  --packet eng/mcpcli-operator-setup/v1/packet-20261031-review \
  --review-sha256 79b9b6febc3c6ba8ca9753c1d8a4776f4eef07470fe25dcec3a01b3c84e5cedd \
  --execute-reviewed --phase cleanup
```

Cleanup also requires an unexpired review; arrange it within the review window. If the window expires or identity differs, stop and obtain a reviewed recovery decision. Preserve private backup/journal evidence and revoke task credentials through that recovery process; no automated broad cleanup is provided. No package/tag/release publication is part of this packet.
