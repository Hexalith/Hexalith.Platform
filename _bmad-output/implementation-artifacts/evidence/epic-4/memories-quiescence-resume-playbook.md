---
playbook: 'hexalith-memories-quiescence-resume'
version: '1-draft'
drafted_at: '2026-09-29'
deployment: 'namespace hexalith-memories on context jpiquot@local'
status: 'DRAFT: not approved and must not be used until the Administrator approves it'
resume_owner: 'jpiquot@itaneo.com (Administrator, incident command)'
contract: 'references/Hexalith.Memories/docs/operations/backup-restore.md'
---

# Memories intake quiescence and resume playbook (Story 4.0)

This is the deployment-owned playbook required by the Memories backup/restore contract. It covers only namespace `hexalith-memories`. It is a draft until the Administrator approves this exact version. The approval records its path and SHA-256, and those values go in the `playbook` field of the quiescence evidence.

## Facts this draft relies on

These come from a read-only census on 2026-09-29. The evidence file is `memories/census.json` in the Story 4.0 evidence bundle.

- Redis `redis-stack-0` holds 3 keys: the `memories-events` stream (length 0, one consumer group, 0 pending), `ingestion-workflow:in-flight:initialized` and `retentionSeconds`. There is no `tenant-registry-index` and no Dapr workflow or actor state. The Dapr `statestore` and `pubsub` components both use this Redis (`redis:6379`, db0).
- FalkorDB `falkordb-0` has 0 keys and 0 graphs.
- `deploy/memories` and `deploy/memories-mcp` each run 2/2. Both access-telemetry Deployments are at 0. No pod with Dapr app-id `eventstore` exists, so nothing publishes to `memories-events`.
- No Ingress or HTTPRoute reaches Memories. Every API request needs the `dapr-api-token` header (Secret `app-api-token`). The Dapr access policy `memories-config` admits only app-id `memories-mcp`.
- The application has no pause flag or admin endpoint. Intake is paused by removing its callers. `memories` itself keeps running so that any workflow can finish.

## Intake controls

| Id | Path | Pause action | Verify paused | Resume action |
| --- | --- | --- | --- | --- |
| C1 | MCP `ingest_content` and every other MCP-driven API call | Record `.spec.replicas` of `deploy/memories-mcp`, then `kubectl -n hexalith-memories scale deploy/memories-mcp --replicas=0` | `kubectl -n hexalith-memories get deploy memories-mcp` shows 0/0, and no `memories-mcp` pods remain | Scale back to the recorded count and wait for rollout |
| C2 | Pub/sub topic `memories-events` | No change: the only publisher (`eventstore`) is absent | `kubectl get pods -A -l dapr.io/app-id=eventstore` returns none; `XLEN memories-events` does not grow; `XINFO GROUPS memories-events` shows pending 0 | Nothing to undo. If a publisher appears before resume, stop and escalate |
| C3 | Direct operator/automation API calls (port-forward plus `app-api-token`) | The Administrator announces the freeze; no operator token is issued during the window | Operator attestation in the evidence | End of freeze announcement |
| C4 | Background retry services inside `memories` (NL embedding retry every 60 s, actor timers) | Not paused. With no backlog they start no work | The NL-embedding backlog and the in-flight registry are both empty (see Drain proof) | None |

Scaling `deploy/memories` to 0 is not a pause control. It would stop workflows from completing and could strand in-flight work.

## Drain proof: every tracked workflow terminal

Run this after C1 to C3 are in place. It is read-only. Secrets stay inside the pod through `REDISCLI_AUTH`; never print the environment.

```bash
kubectl -n hexalith-memories exec redis-stack-0 -- sh -ec '
  export REDISCLI_AUTH="$REDIS_PASSWORD"
  echo "inflight $(redis-cli --no-auth-warning ZCARD ingestion-workflow:in-flight)"
  echo "stream_len $(redis-cli --no-auth-warning XLEN memories-events)"
  redis-cli --no-auth-warning XINFO GROUPS memories-events | tr -d "\r" | paste -sd" "
  echo "workflow_state_keys $(redis-cli --no-auth-warning --scan --pattern "*dapr.internal*workflow*" | wc -l)"
  echo "actor_reminder_keys $(redis-cli --no-auth-warning --scan --pattern "*reminders*" | wc -l)"'
```

The drain passes only when all of these hold:

- `inflight` is 0.
- `stream_len` is unchanged, and the consumer group shows pending 0 and lag 0.
- `workflow_state_keys` is 0. If it is not 0, list the Dapr workflow instances with the Dapr workflow API or CLI. Every instance must be Completed, Failed, Canceled or Terminated. Record each instance ID with its status.
- Two samples taken at least 60 s apart give identical results.

If any value is non-zero, missing or cannot be explained, the drain fails. Stop and keep intake paused. Incident command then decides the next step.

## Quiescence evidence

Write the evidence to `memories/quiescence.json` in the access-controlled bundle. It must satisfy the contract gate:

```json
{
  "playbook": "<repo path>@<sha256 of approved version>",
  "intakePaused": true,
  "activeWorkflows": 0,
  "capturedAt": "<UTC>",
  "resumeOwner": "jpiquot@itaneo.com",
  "pausedControls": ["C1", "C2", "C3", "C4"],
  "mcpReplicasBefore": 2,
  "deploymentRevision": {"memories": "<rev>", "memories-mcp": "<rev>"},
  "drainSamples": ["<sample 1>", "<sample 2>"]
}
```

The maximum evidence age proposed in `recovery-policy.json` is 900 s. Capture must start inside that window, or the drain proof must be re-run.

## Physical capture while quiesced (maintenance-copy path)

The cluster has no CSI snapshot API, so the contract's only allowed physical path is a file copy that a maintenance pod reads from a quiesced, read-only volume. For a copy to count as quiesced, both store processes must be stopped:

1. Run the contract's bounded `BGSAVE` plus AOF-health commands on both stores. Record the Redis and FalkorDB `run_id` and `LASTSAVE`.
2. Record the replicas of `sts/redis-stack` and `sts/falkordb`, then scale both to 0. This stops `memories` state, actors and pub/sub, so all of Memories is down, not just intake (see decision D4).
3. Start one maintenance pod per PVC. The pod mounts `data-redis-stack-0` or `data-falkordb-0` read-only, runs non-root under restricted Pod Security, and has no ServiceAccount token. It streams `tar` of the data directory with SHA-256 to `s3://hexalith-recovery-points/memories/<recovery-id>/` through the writer credential, from the workstation.
4. Read back both objects with the validator credential. Only after both checksums match, delete the maintenance pods and scale both StatefulSets back to the recorded replicas.
5. Require both stores to report Ready, `aof_last_write_status:ok`, and the same key and graph counts as step 1. Intake stays paused.

A failed or uncertain step keeps intake paused. Never pair copies from different attempts. A retry uses a new recovery ID and repeats both copies.

## Resume and reconciliation

Resume is an explicit incident-command action by the resume owner, taken only after the Memories restore proof has passed or incident command decides to abandon it.

1. Re-run the Drain proof to confirm nothing arrived during the window.
2. Scale `deploy/memories-mcp` back to the recorded replicas and wait for rollout.
3. Reconcile. Every workflow instance recorded at quiescence must still be terminal or have reached a terminal state. The in-flight registry and consumer-group pending must both be 0, and the stream length unchanged, which proves no work is missing. The same instance IDs mean no duplicate starts.
4. Record in `memories/resume.json`: the resume time, the playbook ID, the before/after samples and a `/ready` status body from each `memories` pod.

## Decisions required before approval

- **D1. Zero tenants.** The census shows no tenant registry, so the logical export set is empty, and `verify-backup-recovery.py` needs a `--tenant` and an `--export`. Choose one:
  - accept a documented vacuous logical proof, confirmed through `GET /api/v1/tenants` (total 0); or
  - approve a synthetic canary tenant, created before capture and deleted afterwards through the tenant deletion workflow, so the export, import and verifier path is exercised.
- **D2. Identity workflow.** The tenant list and exports need the `dapr-api-token` header plus an OIDC token for `https://auth.tache.ai/realms/tache` with audience `hexalith-memories` and tenant claims. The `memories` CLI does not send `dapr-api-token`. Name the approved way to get both without printing them.
- **D3. Isolated restore topology.** A logical import re-chunks and re-embeds, which needs `memories` with a Dapr sidecar, `redis-stack` and `falkordb`, plus an embedding provider, all in a new namespace (for example `recovery-4-0-mem`). It also needs a default-deny policy that allows only the embedding endpoint. The StatefulSets must keep the names `redis-stack` and `falkordb`, because the verifier hard-codes those pod and PVC names. Approve:
  - the provider credential copy;
  - the egress exception;
  - the Dapr app-id and namespace, chosen so the restored app cannot invoke source apps.
- **D4. Store stop.** Approve the Memories read/search outage while both StatefulSets are at 0 for the copy. On 2026-09-29 this is a few minutes, because both stores are small.
- **D5. Evidence age.** Confirm or change the proposed 900 s maximum quiescence-evidence age.
