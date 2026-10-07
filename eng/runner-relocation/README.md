# Forgejo runner relocation preparation

[Story 4.3](../../_bmad-output/implementation-artifacts/4-3-relocate-the-privileged-forgejo-runner.md) defines the required migration. This directory prepares private evidence and checks signed source checkpoints locally. It does not provision hosts, install software, contact Forgejo/Kubernetes, run jobs, revoke credentials, erase storage or delete resources. A passing checkpoint checks signatures and the internal consistency of supplied statements; it cannot establish that operational measurements occurred or authorize a mutation.

The [2026-10-07 source observation](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-3/20261007t153645z-source-observation/source-observation.json) confirms `192.168.1.30` is `node1` and the application API endpoint. The privileged `dind` runner still runs there. Token automount is disabled in its current pod template; external placement, credential absence and network isolation remain unproved. Its 10 GiB action cache and 40 GiB Docker cache use local OpenEBS PVs with `Delete` reclaim policy. Deletion policy does not prove secure erasure. This unsigned sanitized observation contains neither a verified registered runner identity nor a Forgejo job census and cannot serve as an approved cutover baseline. SSH host access was not verified.

## Prepare a private attempt

Select a new dedicated evidence root outside every Git checkout and existing recovery custody. The helper rejects shared roots, symlinks, reused attempts and enclosing `.git` directories or gitfiles, and creates directories with mode 0700 and files with mode 0600 even under a permissive umask.

```bash
python3 eng/runner-relocation/prepare.py prepare \
  --evidence-root "$RUNNER_RELOCATION_EVIDENCE_ROOT" \
  --attempt-id "$RUNNER_RELOCATION_ATTEMPT_ID" --operator "$RUNNER_RELOCATION_OPERATOR"
python3 -m unittest discover -s eng/runner-relocation -p 'test_*.py'
git diff --check
```

This creates `evidence/epic-4/4-3/<attempt-id>/` under the private root, records story/context/inventory/tool/test/runbook hashes and writes unsigned pending templates. Unknown identities, digests, window, policies and proofs remain null or empty; `accepted`, `mutationAuthorized`, `operationalAcceptance` and `complete` remain false. Preserve this preparation attempt. Create a separate operational attempt for fresh measured evidence and approvals. Never overwrite a failed or superseded record. Commit only a reviewed sanitized summary and digests, not private operational records or credentials.

Before provisioning, the Administrator supplies all five decision groups from the story: separate host/VM identity and owner with hardening/patching/encryption/backup/network policies; required egress and complete cluster denies; approved labels/capabilities and repository/job matrix; secret-store enrollment/rotation; and disposable-cache policy or an explicit scanned migration exception. Bind exact runner/runtime versions and artifact digests, named operators, source baseline and cutover window. No target or software version is selected by these templates.

## Collect the source and guard each transition

Collect the current cluster identity, namespace and Deployment UID/resourceVersion, pod-template/configuration digests, effective runner configuration digest and Forgejo registered runner ID into `source-baseline.json`. Hash full effective configuration in protected memory through the approved collector. A pod-template digest alone does not bind ConfigMaps, Secret references, labels/capabilities or runner registration. Record Secret resource identity/version and protected configuration digests without fetching or retaining Secret values in these records. Collect active jobs from Forgejo for the exact source registration; pod readiness is insufficient. Preserve every required configuration in approved custody outside the namespace before removal.

Sign measured baseline, Administrator decisions and each current checkpoint with externally held approved keys using SSH namespace `hexalith-runner-relocation`. Keep the Administrator `allowed_signers` trust file outside Git under separate protected custody; the helper never generates or approves a production signing key. Set `sourceBaselineSha256` to the baseline's exact byte SHA-256, not a reserialized JSON digest. Approval must follow collection of that baseline and precede the current reread. Each record binds `story: "4.3"`, `attemptId`, `capturedAt` and `expiresAt`; decisions additionally bind `accepted: true`, `operatorIds`, `approvedPhases`, `cutoverWindow` and a `checkpointMaxAgeSeconds` of at most 60 seconds.

Immediately before each of `disable-scheduling`, `revoke-credentials`, `delete-resources` and `delete-namespace`, collect a fresh phase checkpoint. Include `collectionStartedAt`, `capturedAt`, `activeJobsObservedAt`, a readable exact-source `activeJobIds` census and `deploymentPresent`. Run:

```bash
python3 eng/runner-relocation/prepare.py check-source \
  --baseline "$RUNNER_RELOCATION_BASELINE" \
  --decisions "$RUNNER_RELOCATION_DECISIONS" \
  --checkpoint "$RUNNER_RELOCATION_CHECKPOINT" \
  --allowed-signers "$RUNNER_RELOCATION_ALLOWED_SIGNERS" \
  --principal "$RUNNER_RELOCATION_ADMINISTRATOR" \
  --phase "$RUNNER_RELOCATION_PHASE"
```

All three JSON files and their adjacent `.sig` files must be owner-only outside Git. The CLI actually verifies all signatures with `/usr/bin/ssh-keygen -Y verify`, then checks exact source UID/resourceVersion/config/template/registration equality, window/expiry, reread chronology and the current job census. It permits active jobs when disabling new scheduling, and rejects active jobs before revocation or deletion. Any change requires re-inventory and renewed approval; never refresh a resourceVersion silently.

After deleting the exact source Deployment, the final namespace checkpoint must reread its absence and current namespace identity. Carry the archived source identity in `source`, set `deploymentPresent: false`, and include `sourceDeletionReceipt` with that identical `source`, measured `deletedAt`, `absenceVerified: true` and `evidenceSha256` binding the exact protected deletion/absence receipt. A fresh namespace reread and independently reviewed receipt establish lineage; an absent Deployment alone does not identify what was removed. The helper checks the signed receipt reference, not the receipt's contents or the Kubernetes absence query. Stop if lineage is ambiguous or a replacement Deployment exists.

This CLI provides a source guard only. Independent review must additionally verify the target, complete job matrix, isolation, preserved configuration, revocation and storage prerequisites below. Re-read at actual request dispatch, use native UID/resourceVersion preconditions for each explicitly inventoried resource, and require a fresh guard for each revocation/deletion operation. Do not use a blanket namespace/resource deletion or a check as a lasting permission. Stop on drift, stale evidence, unreadable jobs or API precondition conflict.

## External placement and validation

1. Prove the host/VM is neither `node1` nor another designated cluster node, and hosts no staging/production executor or module-code sandbox. For a VM, attest its underlying host and co-resident workloads. Verify approved encryption, patching, audit logs, isolation mode and named-operator access. Record attestation and effective firewall/proxy configuration digest in `target-host-attestation.json`.
2. Enumerate the configured control-plane endpoint, every DNS alias and A/AAAA answer, API/load-balancer/VIP/node IPv4 and IPv6 address, service/pod CIDRs, representative service/pod destinations, and management/bastion/tunnel plus HTTP(S)/SOCKS proxy paths. Record each route's family, alias/address, port, SNI variant and mechanism. Account for absent IPv6/proxy/tunnel paths with measured inventory evidence; an empty list does not prove absence. Deny all cluster paths, including through otherwise allowed proxies, while allowing only approved Forgejo/source/dependency/authenticated-registry egress. Record the complete inventory in `cluster-isolation-proof.json`.
3. Install only the approved pinned runner/runtime artifacts. Enroll with a new secret-store-issued registration credential through the approved private procedure. Provide no kubeconfig, ServiceAccount token, cluster certificate, cloud cluster credential or mounted Kubernetes Secret. Never copy the source registration credential or PVCs. Keep caches disposable unless the Administrator approved and scanned a specific exception.
4. Assign the external runner an exclusive non-production validation label. Keep source scheduling available on its existing labels for this validation; never give both runners the same production labels. Confirm Forgejo registration/host/runner instance identities unambiguously.
5. Run every approved repository/commit through checkout, dependency restore, build, unit tests, container build and authenticated registry push/pull by exact digest. Record job IDs, trigger, commit, host, runner instance, individual required outcomes and artifact digests in `external-runner-job-matrix.json`.
6. Inside the actual job container, probe every inventoried alias, IPv4/IPv6 endpoint, direct-IP/SNI variant, proxy/tunnel route and representative service/pod address for discovery, namespace listing and Service access. Require network refusal and absent credentials. An API 401/403 proves the API was reached and fails network isolation. DNS failure or timeout alone needs contemporaneous firewall/proxy/audit events bound to the attempt, job, destination and probe. Inspect mounts and environment/files for Kubernetes/cloud credentials through a protected collector; record only check names/outcomes/digests, never values or credential contents. Repeat after removal.

## Cutover, revocation and storage removal

After the whole validation matrix and isolation checks pass, quiesce source scheduling under a fresh identity guard, wait for existing jobs to finish, and verify no new source jobs start. Cut over production labels without overlap. Run the entire matrix again through normal Forgejo triggers and confirm it executes exclusively on the external host before removing the source.

Under a new guard, rotate/revoke the exact old registration and every source-specific registry/job credential through their authoritative issuers and secret store. Independently test that the old runner identity can neither receive jobs nor authenticate. Rotation of an enrollment token alone may not revoke an already registered runner; bind the registered identity and its actual authentication credential. Retain sanitized issuer/audit/revocation receipts only. Stop if any credential cannot be revoked. Repair the isolated runner or provision another isolated host on failure; reopening the in-cluster runner is forbidden.

Inventory every runner PVC/PV UID, binding, reclaim policy, storage class, backing volume/directory identity, workspace, VolumeSnapshot/VolumeSnapshotContent, storage-provider snapshot and backup copy. Kubernetes snapshot discovery does not inventory provider snapshots. Establish ownership and preserve required configuration outside the namespace. The source's local `Delete` PV policy is not an erasure receipt. Before namespace removal, securely erase or cryptographically destroy cache/workspace data and all retained copies under the approved backend-specific procedure. Do not destroy a shared encryption key or erase a shared directory/device. Require independent verification and provider-side residual checks for retained/released PVs, volumes, directories, snapshots and provider objects; inability to verify closes removal.

After each fresh source guard and resource-specific precondition, delete only approved source Deployment, ServiceAccount, associated namespaced/cluster RBAC, runner-only PVCs, ConfigMaps and Secrets. Record each exact identity and erase/delete receipt. Revalidate the namespace UID/resourceVersion and source deletion lineage immediately before deleting it. Stop on replacements, extra/unowned resources, namespace-only required configuration or incomplete erasure.

Finally verify namespace/resource absence, no old runner image/digest in any pod, old-credential denial, passing normal jobs on the external host, and continued refusal of every inventoried direct/proxied IPv4/IPv6 path. Record all quiescence, guards, credential revocations, identities, storage/receipt/residual checks and post-removal jobs in `source-removal.json`. Sign `runner-relocation-result.json` only after independent review accepts measured evidence for all acceptance criteria, bind the exact evidence byte hashes, and commit the sanitized summary. Pending templates, synthetic tests and a source-check result cannot complete this story.
