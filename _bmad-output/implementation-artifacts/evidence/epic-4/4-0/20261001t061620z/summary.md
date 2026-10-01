---
recovery_id: '20261001t061620z'
status: 'complete at verification time; expiry must be rechecked before a hop'
classification: 'sanitized operational backup/restore evidence'
upgrade_mutation_authorized: false
---

# Fresh signed backup and isolated-restore run

The Administrator requested the maintenance start immediately, then explicitly delegated signing and supplied the key unlock input interactively. The implementer used the pinned Administrator key through terminal prompts. No unlock input was written to workspace files, environment variables or execution logs. This delegation supersedes the earlier requirement that the Administrator personally enter the prompt for this run.

All three fresh recovery points were restored into isolated namespaces on new volumes, verified, and cleaned up. The source PVC identities remain unchanged. The signed full records are in `~/hexalith-recovery-evidence/4-0/20261001t061620z/` and under `s3://hexalith-recovery-points/evidence/4-0/20261001t061620z/`. Those records and detached signatures are AES256-encrypted and COMPLIANCE-locked; Git contains these projections and hashes only.

| System | Measured RPO | Restore result | Proof expiry (UTC) |
| --- | --- | --- | --- |
| keycloak | 14 s | pass | 2026-10-02T07:17:08Z |
| openbao | 42 s | pass | 2026-10-02T07:21:38Z |
| memories | 89 s | pass | 2026-10-02T07:30:43Z |

The read-only validator re-read all **553 manifest object versions** and checked signatures, encryption, object lock/retention, source incarnations, RPO, expiry, cleanup and exact gate/manifest/policy bindings: **648 passed, zero failures, zero skipped**. The eight manual checks also passed: both Memories run IDs matched the signed post-copy identities, and every writer IAM policy/group and bucket Allow statement granted only the required project-scoped read/write actions without delete, retention or legal-hold rights. Final validation includes the observed manual results and the execution delegation.

The policy, cleanup, manifest, three proofs, backup gate and final validation are signed, uploaded and independently read back with matching SHA-256. A ninth signed observations supplement records two retained-tool details. The [hash ledger](evidence-hashes.json) pins their exact local and object-version identities. The backup gate expires at **2026-10-02T06:16:52Z** (08:16:52 Europe/Paris on 2026-10-02); fresh source identity and proof validation remain required immediately before any upgrade.

Keycloak database inventory/schema and isolated OIDC checks passed. OpenBao restored three unsealed voters, one leader, the matching source cluster identity, inventory/canary digests and an applied index reaching the snapshot. Memories restored paired physical copies with matching durable key/graph/stream/group state and healthy AOF. Its stores were stopped from `2026-10-01T07:29:26Z` to `2026-10-01T07:30:50Z`; intake resumed at `2026-10-01T07:33:22Z` after a clean drain recheck, with healthy API readiness, zero missing work and zero duplicate work. There are zero tenants, so the approved D1/D3 physical-only scope applies and **no tenant verifier pass is claimed**.

Cleanup passed all 17 underlying absence checks and all 11 signed-record checks. Restore namespaces, recorded PVCs/PVs, workloads, RBAC/network resources and temporary seal-key copies are absent; CNPG watch namespaces are restored. Host-directory deletion is inferred from OpenEBS provisioner PV deletion, as explicitly disclosed in the signed cleanup record, and was not verified through node shell access.

The checksum-verified retained tools were unchanged. The implementer supplied the missing fresh `memories/census.json` input from the source-pre census. The full OpenBao proof also contains a static rehearsal-era `abortedAttempt` note: **no target was aborted in this fresh run**. The signed `validation/final-run-observations.json` supplement clarifies that descriptive note and binds the original proof, final validation, gate and manifest; original signed records were preserved.

API-resource requests from restore targets were denied. Memories' API Service VIP remains reachable at TCP level under kube-proxy IPVS, the same disclosed isolation limitation as the rehearsal. Provider-managed encryption keys, single-Administrator signing and workstation-held writer/validator credentials retain the previously recorded custody risks.

Story 4.0 acceptance is complete under the approved decisions. This backup result supplies one prerequisite for Story 4.1. Its external-etcd restore, node recovery bundle, kubeadm preflight, API/add-on compatibility, dynamic-storage probes and incident/rollback ownership gates remain open; node drain, package changes, reboot and kubeadm upgrade have not run.
