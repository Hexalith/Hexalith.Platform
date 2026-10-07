# Story 4.3 local preparation — 2026-10-07

Private pending evidence was created in a dedicated owner-only store outside Git. Its 12 unsigned JSON templates and checksum manifest were verified: directory mode 0700, file modes 0600, matching source and file digests. Missing operational identities, policies, job/isolation results and cutover approval remain empty. Private templates were not copied into Git.

The local helper verifies real SSH signatures for source checkpoints and rejects source UID/resourceVersion/configuration/registration drift, stale or pre-approval checkpoints, unreadable or active jobs before revocation/deletion, and missing exact source deletion lineage. All 15 focused tests passed, including real disposable signing fixtures, tampering/wrong-principal failures and custody protections. `git diff --check` passed. These tests establish tooling behavior; they provide no operational relocation proof.

The [fresh source observation](../20261007t153645z-source-observation/source-observation.json) confirms that 192.168.1.30 is node1, where privileged dind remains. Token automount is disabled. Both local cache PVs use Delete reclaim policy; erasure and provider residual checks remain unverified. That observation and this summary are unsigned.

The separate destination, Administrator decisions, fresh signed registration/job baseline, external hardening/enrollment, complete network/job isolation matrix, production label cutover, credential revocation, secure erasure, source removal and signed final result remain incomplete. No live mutation occurred. Story 4.3 remains in-progress and no operational checkbox was completed.
