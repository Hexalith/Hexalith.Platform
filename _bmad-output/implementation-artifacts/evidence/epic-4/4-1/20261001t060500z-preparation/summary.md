---
prepared_at: '2026-10-01T06:06:27Z'
attempt_id: '20261001t060500z-preparation'
classification: 'sanitized local-only preparation; no mutation authorization'
acceptance_state: 'Blocked before mutation. Story 4.0 is in-progress; Story 4.1 is not complete.'
---

# Story 4.1 preparation attempt

The mutation gate is **closed**. This attempt prepares the maintenance handoff and records local prerequisite gaps. No live cluster, SSH, package, service, storage or provider operation was performed. Nothing was quiesced, cordoned, drained, upgraded, rebooted or signed.

The operator is `Codex (local preparation only)`. Administrator approver, window, incident channel, incident commander, rollback owner and workload-owner acknowledgements are **unassigned/unapproved**. The historical source is `jpiquot@local`, `node1`, Kubernetes `v1.34.9`, captured `2026-09-28T12:51:19Z`; today's source/component versions and health were not queried. The first candidate hop is `1.34 → 1.35`, subject to approval and compatibility. Exact current target patch, package identities and image digests remain unset until the maintenance window. Further hops require their own selection, sequential planning and fresh signed gates.

## Local dependency result

`sprint-status.yaml` records Story 4.0 as `in-progress`. The inspected `20260929t124806z` recovery bundle is the explicitly rejected **rehearsal**, not a final-run candidate:

- `gate/backup-gate.json` and `validation/validation.json` are absent.
- All three restore-proof files are present but have no detached signatures and their recorded expiry timestamps are past. The Memories record also does not declare `verificationResult=pass`.
- No proof signature, immutable off-node object or provider read-back checksum was validated in this local-only attempt.
- All **76 rehearsal files** retained their SHA-256: the before/after aggregate manifests match. No final-run tool or recovery record was changed.

The gate now requires the Administrator-signed, passing final Story 4.0 validation, fresh under its signed policy and bound to the exact backup-gate, evidence-manifest and policy digests plus recovery ID. This addresses review finding B3 without allowing sprint status or signature-file presence to stand in for cryptographic/live validation.

## Reviewable preparation

The [local diagnostic recorder](../../../../../../eng/kubernetes-upgrade/prepare.py) creates an owner-only attempt and always leaves authorization false. The [maintenance handoff](../../../../../../eng/kubernetes-upgrade/README.md) defines required evidence, protects the concrete workload set, resolves per-hop/preflight/package sequencing, and describes stop/recovery decisions. The [local safety checks](../../../../../../eng/kubernetes-upgrade/test_prepare.py) exercise closed-gate behavior, no external/network calls, malformed/expired inputs and private-data suppression, owner-only modes/checksums, duplicate story status, unsafe/reused attempt IDs, repository-output refusal and recovery-bundle preservation.

Private evidence is under `~/hexalith-upgrade-evidence/evidence/epic-4/4-1/20261001t060500z-preparation/`, with directories `0700` and files `0600`. It contains **15 JSON records** and `SHA256SUMS`. These are sanitized preparation metadata only; no raw logs, snapshots, kubeconfig, PKI or recovery material were created. Every required operational evidence filename is reserved with `not-run`, `blocked` or `closed` state. `upgrade-result.json` says `complete=false`, has no executed hops or actual final versions, and records all four workload outcomes as `not-run`.

[Evidence hashes](evidence-hashes.json) cover all private records, the source story/context/status/tool inputs and the unchanged recovery rehearsal. `SHA256SUMS` SHA-256: `580b75e764b6afc6b34450921ea8446f12291ba97c12af6ab10c481215be652d`. Checksums detect changes; they are not Administrator signatures or approval.

## Verification

`python3 -m unittest discover -s eng/kubernetes-upgrade -p 'test_*.py' -v`: **8 tests passed**. Actual attempt record checksums and owner-only modes verified, and the 76-file rehearsal manifest was identical before/after preparation. No live preflight, health check or smoke test ran.

The [Kubernetes release page](https://kubernetes.io/releases/) checked on 2026-10-01 confirms the story's supported branches 1.35–1.37 and 1.34 end of life on 2026-10-27. Target patches must be resolved again at window start. The handoff links target-minor upgrade, skew and etcd recovery instructions; upstream documentation is preparation context, not proof of this cluster's compatibility.

## Outstanding operational prerequisites

1. Schedule and approve the window/full-workload outage; assign incident/recovery owners and collect every workload owner's acknowledgement and approved quiescence/recovery procedure.
2. Execute Story 4.0's final fresh capture/isolated-restore/cleanup run; sign the gate, three proofs and passing final validation, verify their exact bindings and independently read back immutable off-node objects. Mark Story 4.0 `done` only after its requirements pass.
3. Collect actual live component/workload/PVC/storage/add-on/API versions and health, using a client within supported skew. Run current-hop kubeadm plan/preflight and resolve exact current package/image identities and compatibility without bypassing blockers.
4. Have the external-etcd owner prove fresh snapshot integrity, encrypted off-node recovery, isolated restored member health and revision/key-count/hash/canary agreement, then sign cleanup.
5. Produce and independently read back the encrypted node-local configuration/PKI/static-pod/package/kubelet recovery bundle with owner/mode-preserving checksums and test its approved recovery procedure.
6. Clear every removed/deprecated API and CNI/DNS/proxy/storage/CRD/operator/webhook compatibility result. Prove every required dynamic StorageClass provision/write/read/delete path and provider cleanup immediately before each hop.
7. Capture concrete pre-outage health, sign a fresh per-hop gate, and require terminal quiesce/workflows, observed cordon and successful drain before kubeadm apply. Record actual sequential hop exits and component versions; gate uncordon/next hop on API, node, etcd, CNI/DNS/storage/add-on and protected-workload health.
8. Restore and smoke-test Keycloak/PostgreSQL/OIDC, OpenBao raft/unseal/canary, Memories Redis/FalkorDB/API/MCP/search and telemetry PostgreSQL, and Forgejo repository/UI/runner. Record actual final versions and every outcome; obtain Administrator signatures on the upgrade result and sanitized summary.

The story and sprint status remain `in-progress`. No operational checkbox has been marked complete on the basis of preparation evidence.
