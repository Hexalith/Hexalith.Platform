---
requested_start: '2026-10-01T06:15:14Z'
timezone: 'Europe/Paris'
recovery_id: '20261001t061620z'
classification: 'sanitized prerequisite execution; mutation gate closed'
acceptance_state: 'Awaiting Administrator policy signature before backup captures. Upgrade incomplete.'
historical_snapshot: true
---

# Story 4.1 immediate maintenance request

This snapshot records the initial unsigned-policy stage. The later explicit signing delegation and completed backup/restore result are in the [fresh final-run summary](../../4-0/20261001t061620z/summary.md); its original policy/signature flags and private-record hashes remain historical evidence.

The Administrator replied `now` to the maintenance-window question. Preparation started immediately, at approximately 08:15 Europe/Paris. The end time, incident commander, rollback owner and workload-owner acknowledgements remain unrecorded; the requested start does not establish a signed-open mutation gate.

## Fresh Story 4.0 run

Created `~/hexalith-recovery-evidence/4-0/20261001t061620z/` from the checksum-verified final-run tools. The copied tools and single-Administrator trust root verified successfully. Destination prechecks passed: versioning enabled, COMPLIANCE object lock, AES256 encryption, approved bucket-policy digest and no rejected delete markers. Prepared `policy/recovery-policy.json` and uploaded its unsigned copy to the run's encrypted, object-locked evidence prefix through the approved policy step.

Policy SHA-256: `531807640d49edb5a0ce1bbda22bd947663768125b766941b07179498935d00e`.

The policy is not signed. Story 4.0 requires the Administrator to enter their own signing-key passphrase; the implementer prepares records and never signs on anyone's behalf. The next command, run by the Administrator after reviewing the policy, is:

```bash
cd ~/hexalith-recovery-evidence/4-0/20261001t061620z/tools
./sign.sh policy/recovery-policy.json
```

The helper verifies the signature against the pinned trust root, uploads the signed record and signature, and reads them back. Captures and isolated restores remain unstarted until that signature and upload verify. Later cleanup, proof, gate and validation signatures are also required. Story 4.0 remains `in-progress`.

## Live read-only baseline

The API still serves `v1.34.9`. Downloaded a separate `v1.34.12` kubectl from the official Kubernetes release endpoint and verified its upstream SHA-256 (`90b7b9058ffeb5c10710bb1c73f541eaf426fb1b4e87df435db669de9a2564a2`); no system package was changed. Used that matching-minor client for projected resource GETs against context `jpiquot@local`.

- `node1`: Ready, schedulable, kubelet `v1.34.9`, containerd `2.3.3`.
- Protected namespaces: `keycloak`, `openbao`, `hexalith-memories`, `forgejo`, `forgejo-runner`.
- All 16 running protected pods Ready; five job pods Succeeded; all 17 protected PVCs Bound; all four resource queries succeeded. This corrects the earlier prose count from the unchanged projected baseline.
- Dynamic StorageClasses: `openebs-hostpath` and `openebs-hostpath-retain`, both `openebs.io/local` with `WaitForFirstConsumer`. Functional provision/write/read/delete probes have not run; the Retain class needs provider cleanup evidence.

The [official release page](https://kubernetes.io/releases/) checked during preparation lists `1.35.9` as the current 1.35 patch. It is the first-hop candidate, subject to exact package/image identity verification and every story gate; no hop was executed.

Private projected baseline and maintenance-request records are under `~/hexalith-upgrade-evidence/evidence/epic-4/4-1/20261001t061620z-maintenance-request/`, with owner-only modes and checksums. They contain no Secret values, kubeconfig, PKI or raw data. Pod readiness and PVC binding do not establish native database health, application smoke checks, add-on compatibility or recovery evidence.

The fresh signed backup/restore proofs, signed final validation, external-etcd isolated restore, encrypted node recovery bundle, live kubeadm preflight, compatibility scan and functional storage proofs remain outstanding. Quiesce, cordon, drain, package changes, reboot and kubeadm upgrade have not run. Story 4.1 remains `in-progress`.
