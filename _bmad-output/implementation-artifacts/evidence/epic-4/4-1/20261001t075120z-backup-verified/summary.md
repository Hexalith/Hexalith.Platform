---
attempt_id: '20261001t075120z-backup-verified'
recovery_id: '20261001t061620z'
backup_prerequisite: 'verified and signed'
upgrade_gate: 'closed'
mutation_authorized: false
---

# Story 4.1 signed backup prerequisite validation

The new operational attempt directly verified the Administrator signatures of the final Story 4.0 policy, manifest, three restore proofs, cleanup, backup gate and passing final validation, plus the signed observations supplement. Record and signature bytes were read back by the read-only validator from exact encrypted COMPLIANCE-locked object versions and matched the local digests. The approved trust root verified, all proofs/gate were unexpired, final validation was fresh under the signed policy, and its exact `inputs`, policy/manifest/gate SHA-256 and recovery-ID bindings matched. Story 4.0 is `done`.

The attempt's `gate-validation.json` binds these observations to `20261001t075120z-backup-verified`. It passed all 12 backup-prerequisite checks, was signed under the Administrator's explicit delegation, uploaded to `s3://hexalith-recovery-points/evidence/4-1/20261001t075120z-backup-verified/`, and its signature was cryptographically verified again on readback. The signed final Story 4.0 validation supplies independent readback of 553 manifest object versions with 648 passing automated checks, zero failures/skips and eight passing manual checks. The [hash ledger](evidence-hashes.json) pins every required signed input and this attempt's record/object versions.

**This validates the backup prerequisite only. The upgrade gate remains closed.** No signed-open per-hop gate exists. Backup proof freshness, source identity and bindings must be rechecked immediately before a hop; the current backup gate expires at `2026-10-02T06:16:52Z` (08:16:52 Europe/Paris on 2026-10-02).

The observed API is still v1.34.9. After backup/resume/cleanup, `node1` remains Ready and schedulable, all 16 running protected pods are Ready, five job pods are Succeeded, and all 17 protected PVCs are Bound. This is a readiness observation; full pre-upgrade native-health and smoke checks remain required.

The Administrator already requested the window start immediately. At this backup-only snapshot, SSH access, external-etcd/node recovery, target plans/identities, compatibility and storage probes had not yet been obtained. The [subsequent signed preparation evidence](../20261001t095344z-recovery-preparation/summary.md) records verified SSH access, isolated recovery tests, encrypted off-node bundles, target plans and both storage probes. Full compatibility and workload smoke coverage remain incomplete; approved recovery/accountable owners, window end/incident channel and workload-owner acknowledgements are still unrecorded.

Upgrade quiesce, cordon, drain, package installation, service restart for upgrade, reboot, kubeadm mutation, further minor hops and post-upgrade verification have not run. Story 4.1 stays `in-progress`. The access-controlled private attempt at `~/hexalith-upgrade-evidence/evidence/epic-4/4-1/20261001t075120z-backup-verified/` preserves the signed record, signature, pending maintenance metadata, readback ledger and checksums; no secret values, kubeconfig or PKI are included in this public projection.
