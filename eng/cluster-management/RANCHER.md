# Story 4.28: private Rancher deployment and recovery

Deploy only after accepted qualification, retirement and supported native-hop outcomes. The exact pins, VM placement/budget, private endpoint and authentication are qualification outputs, not values inferred by this document.

## Deploy and register

1. Revalidate the qualified supported/security-current manager and workload version pair, community chart/image/dependency licenses, VM capacity/OS/storage, private DNS/TLS trust chain and agent connectivity. Retain actual identities and owner-controlled definitions.
2. Provision the approved single-node K3s management VM and deploy the pinned Rancher Helm release with required certificates and backup operator. Record resource use and the single-node/shared-host limitation. Use the manager's qualified private infrastructure endpoint rather than placing it in an application release chart.
3. Verify administrative authentication/MFA and narrow Administrator/deputy role mapping before accepting UI/API access. Prove public/unauthorized denial; retain independent native kubeconfig/SSH access and custody.
4. Inspect the exact import manifest and its required authority before applying it. Register the existing kubeadm workload-cluster identity as generic/imported; verify agents and representative read/admin operations. Preserve native kubeadm/external-etcd maintenance; registration does not rebuild the cluster.
5. Inventory installed agents, namespaces, CRDs/webhooks, service accounts and RBAC. Bound any required system-namespace exception to qualified resources while preserving application restrictions. Do not enable unattended workload upgrades, Fleet application-release reconciliation or optional platform stacks.
6. Verify workload/namespace/PVC preservation against the post-hop baseline and re-run affected smoke/access checks. Staging users/workloads/executors must fail attempts to obtain global/cluster-owner authority, production proxy access or usable production credentials through UI/API/kubeconfig issuance.

## Backup and isolated restore

1. Create encrypted immutable off-node Rancher management backups. Separately retain the required encryption configuration/key material, TLS/configuration/registration recovery inputs, pinned definitions and management-cluster recovery inputs under independent Administrator/deputy custody.
2. Continue every existing native workload-data and external-etcd backup. The Rancher backup operator's management-app scope does not replace these systems or the management cluster's native recovery needs.
3. Restore the manager into a quarantined target unable to reach live source/downstream authority. Check configuration, required resource coverage, current approved roles and decryption from independently available material. Keep old manager/agent credentials fenced and prove later-revoked token/proxy credentials fail before reconnection.
4. With Rancher unreachable, demonstrate qualified native administration and a representative authorized maintenance/recovery operation. Re-register only the approved target identity after current-authority reconciliation; recovery never grants admission or expands deputy powers.
5. Record signed access/preservation/restore outcomes, actual deployed pins and renewal/currency/monitor/recovery owners. Complete initial 4.28 evidence before staging/G1. Epic 8 integrates required cadence/retention, independent monitoring and RPO/RTO accounting; Epic 9 includes management components in currency and affected policy-rehearsal checks.

Stop on drift, unsupported identities, unresolved source ownership, failed isolation/authority tests, unreadable recovery material or incomplete required evidence. Preserve failed state and diagnostics; use the qualified owner-controlled forward-revert/DR procedure rather than application Helm rollback.

## Authority source and manager-independent recovery

Use independently retained Administrator-approved management grant/revocation records, not the restored Rancher database alone, to reconcile current users/roles/token authority. Missing, conflicting or gapped current lineage fails closed before reconnection. Only Administrator changes grants; deputy recovery remains within pre-existing scoped authority. Native workload recovery and verification can proceed while manager restoration is pending; manager/agent connections stay fenced. Measure every necessary management step under the applicable integrated RTO plan.
