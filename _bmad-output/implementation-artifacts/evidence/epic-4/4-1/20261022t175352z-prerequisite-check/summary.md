---
attempt_id: '20261022t175352z-prerequisite-check'
classification: 'sanitized preparation; unsigned'
upgrade_gate: 'closed'
mutation_authorized: false
---

# Story 4.1 prerequisite checkpoint — 2026-10-22

**The upgrade remains blocked and Story 4.1 remains `in-progress`.** Story 4.0 is `done`, but its retained recovery `20261014t112353z` expired at **2026-10-15T11:40:42Z**, derived from the signed policy's preparation time and 24-hour age ceiling. Story 4.27 remains `in-progress`; accepted signed retirement/preservation and private native-access evidence are still required. The month-end maintenance window does not extend backup freshness or approve the exact successor procedure.

Local verification authenticated all **eight original Administrator signatures** against the pinned trust root. Final validation's exact input, recovery-ID, gate/manifest/policy and proof bindings still match. All eight records, their signatures and the trust file remained unchanged. Their integrity preserves historical acceptance; all three current freshness checks fail. No new independent provider readback or Administrator signature was performed. The [verification receipt](verification.json) binds exact input and diagnostic hashes; the sealed private checkpoint SHA-256 is `dd0c9713696dc100c4cb248fb84e5df1fa3b2cdb5aa5226d4f6ea4023a54e333`.

Five unauthenticated upstream downloads confirmed current [1.35](https://dl.k8s.io/release/stable-1.35.txt) and [1.36](https://dl.k8s.io/release/stable-1.36.txt) publication values **v1.35.9 → v1.36.5** and both kubeadm checksums. Retained standalone v1.35.9 kubeadm matches its newly downloaded checksum. The [release page](https://kubernetes.io/releases/) still reports Kubernetes 1.34 end of life on **2026-10-27**. This dated observation establishes no cluster compatibility or executable hop approval; refresh exact targets and all binary/image identities immediately before each hop.

The unchanged preparation/recovery suite passed **43 tests**. The new owner-only private attempt contains all 16 original checksummed preparation records, with both hop gates closed/unsigned and every upgrade action `not-run`. Record checksums and modes passed. No cluster, SSH or provider requests, credentials, captures/restores, signing, quiesce, cordon, drain, installed-binary/package changes, reboot or kubeadm upgrade occurred. Live versions and workload health were not recollected.

Resume with fresh accepted Story 4.0 recovery and independent immutable readback, then accepted Story 4.27 retirement. Require fresh post-retirement etcd/isolated-restore and matching node recovery; complete API/add-on/admission clearance, Forgejo consistency disposition and authenticated workload expectations; derive and approve the exact remaining-controller maintenance/stop/recovery/outage procedure while preserving the historical digest. Current live preflight/identities, storage/health proofs and a fresh signed gate remain mandatory for **each** sequential hop. This checkpoint completes no operational task and changes no existing approval or expiry.
