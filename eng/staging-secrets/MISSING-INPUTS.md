# Missing operational inputs

**BLOCKED_NOT_FOR_EXECUTION. Preparation only; no operation is authorized.**

There are **43 current null inputs**. Every null remains unknown. Supply metadata and evidence references only, never credentials, key shares or secret values. Aggregate objects require every documented member when supplied.

Inherited prerequisites remain the unchanged `../staging-foundation/MISSING-INPUTS.md` and `../staging-foundation/APPROVAL-PACKAGE.md`: all 35 missing foundation inputs, missing positive/negative RBAC and complete storage/DNS/CNI isolation, and unaccepted dependency evidence still block live work. Their answers are not inferred here.

## Exact current null set

| Current null path | Owner role | Exact question |
| --- | --- | --- |
| `/providers/openbao` | Platform maintainer | Which complete name, version, digest and compatibility evidence are approved for OpenBao provider? |
| `/providers/tenantStore` | Platform maintainer | Which complete name, version, digest and compatibility evidence are approved for separate tenant-key provider? |
| `/providers/dapr` | Platform maintainer | Which complete name, version, digest and compatibility evidence are approved for Dapr runtime and Vault component compatibility set? |
| `/providers/installationTool` | Platform maintainer | Which complete name, version, digest and compatibility evidence are approved for future owner-controlled installation tool? |
| `/providers/verificationTool` | Platform maintainer | Which complete name, version, digest and compatibility evidence are approved for future isolated verification tool? |
| `/ordinaryStore` | Administrator | What complete instance, backend, physical storage, snapshot domain, topology, resources and native-TLS contract is selected for ordinary staging OpenBao? |
| `/tenantStore` | Administrator | What complete instance, backend, physical storage, snapshot domain, topology, resources and native-TLS contract is selected for separate staging tenant-key store? |
| `/snapshotBoundary/ordinaryStateMembers` | Backup and key-custody owners | Which exact physical state IDs belong to ordinary whole-instance OpenBao snapshots? |
| `/snapshotBoundary/tenantKeyStateMembers` | Backup and key-custody owners | Which exact physical state IDs belong only to separate tenant-key custody snapshots? |
| `/snapshotBoundary/separationEvidenceRef` | Backup and key-custody owners | Which backend/topology evidence proves distinct persisted state and whole-instance snapshot membership, beyond a KV-path exclusion? |
| `/network/resolverDatapathEvidenceRef` | Network owner | Which accepted evidence proves the real pod resolver path including NodeLocal DNS under default deny? |
| `/network/policyEvidenceRef` | Network owner | Which evidence reference reviews exact dependency definitions and proves each DNS, ordinary-store, tenant-key-store, replication, monitoring and custody flow or explicitly records its non-applicability, under default deny with no production access? |
| `/network/dependencies` | Network owner | What complete metadata inventory references the exact reviewed dependency definitions and per-flow evidence, with coverage or explicit non-applicability recorded in policyEvidenceRef? |
| `/authority/environmentLayerPrincipal` | Administrator | Which exact environment-layer principal owns stores, Components, policies and named bootstrap Secrets? |
| `/authority/sharedInfrastructurePrincipal` | Administrator | Which distinct accepted shared-infrastructure principal owns any required cluster resources? |
| `/authority/jobPrincipals` | Administrator | Which exact per-job principals must be denied data namespace and environment-layer writes? |
| `/authority/rbacEvidenceRef` | Administrator | Which complete positive and negative RBAC evidence proves AD-7/AD-8 separation, least privilege and absence of bind/escalate/impersonate? |
| `/custody/administrator` | Administrator and recovery deputy | Who is the named Administrator with independent manual unseal and recovery access? |
| `/custody/deputy` | Administrator and recovery deputy | Who is the distinct named recovery deputy with independently usable custody? |
| `/custody/mechanismRef` | Administrator and recovery deputy | Which secret-free custody mechanism reference separates key access from ciphertext backup access? |
| `/custody/manualUnsealProcedureRef` | Administrator and recovery deputy | Which reviewed manual unseal and recovery-key procedure covers each selected store without disclosing shares? |
| `/custody/coverage` | Administrator and recovery deputy | What published time zone, primary/deputy schedule and acknowledgment bound cover manual unseal? |
| `/custody/monitoringEvidenceRef` | Administrator and recovery deputy | Which qualification proves seal, renewal, backup/coverage, custodian use and root-material alerts reach Administrator and deputy? |
| `/backup/ordinary` | Backup and key-custody owners | What complete ordinary-secrets backup class, independent destination, access and decryption custody are approved? |
| `/backup/tenantKeys` | Backup and key-custody owners | What complete separate tenant-key backup class, destination, access and decryption custody are approved? |
| `/backup/retentionPolicyRef` | Backup and key-custody owners | Which approved complete-chain cadence/retention policy implements architectural floors and retains non-erased keys at least as long as referenced data? |
| `/backup/erasureLineageAuthorityRef` | Backup and key-custody owners | Which surviving tombstone authority and complete lineage proof prohibit erased-key restoration and invalidate older erased-tenant backups? |
| `/backup/fenceReissueProcedureRef` | Backup and key-custody owners | Which independently held authority and procedure fence old backup/custody credentials and reissue replacements without credential rewind? |
| `/backup/restoreQualificationRef` | Backup and key-custody owners | Which isolated native backup/restore qualification proves integrity, independent access, erasure, key coverage and quarantined restore? |
| `/secretContract/path` | Platform maintainer | Which value-free environment/app secret-contract instance is approved? |
| `/secretContract/digest` | Platform maintainer | What exact sha256 digest binds the selected staging secret-contract bytes? |
| `/secretContract/generationOwner` | Platform maintainer | Who approves required generations and acknowledged rotations in that contract? |
| `/secretContract/apps` | Platform maintainer | What complete enrolled app inventory supplies distinct identities, scopes, policies, per-pod token delivery, renewal and exact required generations? |
| `/bootstrapToken/inventoryEvidenceRef` | Administrator | Which sanitized current token lookup, complete consumer inventory partitioned by approved environment and instance, and issuer history establish policies, type, TTL, renewable state, access and custody without token bytes? |
| `/bootstrapToken/classificationDecisionRef` | Administrator | Which signed owner classification distinguishes proven shared bootstrap use from unproven root, privilege, compromise and renewal claims? |
| `/bootstrapToken/cutoverOwner` | Administrator | Who owns per-app replacement, renewal coverage, pod restart acknowledgment and old-token revocation before the historical expiry? |
| `/bootstrapToken/cutoverDeadline` | Administrator | What exact owner-approved replacement deadline before the historical expiry is selected? |
| `/bootstrapToken/revocationAuthorityRef` | Administrator | Which independently authorized issuer owner and approved procedure can revoke the old token after complete per-app acknowledgment? |
| `/verificationRecovery/foundationAcceptanceRef` | Administrator | Which accepted 4.14 live-apply and complete isolation evidence permit the future 4.15 operation? |
| `/verificationRecovery/verificationOwner` | Administrator | Who owns native TLS/CA, restricted pod, least privilege, snapshot membership, renewal/rotation and complete AD-8 isolation verification? |
| `/verificationRecovery/verificationPlanRef` | Platform maintainer | Which exact-byte reviewed native verification plan and evidence format covers every 4.15 criterion? |
| `/verificationRecovery/recoveryAuthorityRef` | Administrator and recovery deputy | Which independently usable Administrator/deputy authority and exact source/destination procedure can restore into quarantine without credential rewind or erased-key resurrection? |
| `/verificationRecovery/approvalRecordRef` | Administrator | Which exact-byte Administrator approval record authorizes a later named target, definitions, toolchain, identities, effects, verification and forward recovery? |

## Conditional aggregate and app-member questions

These questions apply when a currently null aggregate is supplied. A wildcard describes each future inventory member and never creates an app ID. A nested null is enumerated with its exact member path by the assessor. These are member requirements, not additional current nulls.

| Conditional member path | Owner role | Exact question |
| --- | --- | --- |
| `/providers/openbao/name` | Platform maintainer | What exact OpenBao provider artifact name is selected? |
| `/providers/openbao/version` | Platform maintainer | What exact supported OpenBao provider version is approved? |
| `/providers/openbao/digest` | Platform maintainer | What exact OpenBao provider content digest is approved? |
| `/providers/openbao/compatibilityEvidenceRef` | Platform maintainer | Which recorded qualification proves OpenBao provider compatibility and support? |
| `/providers/tenantStore/name` | Platform maintainer | What exact separate tenant-key provider artifact name is selected? |
| `/providers/tenantStore/version` | Platform maintainer | What exact supported separate tenant-key provider version is approved? |
| `/providers/tenantStore/digest` | Platform maintainer | What exact separate tenant-key provider content digest is approved? |
| `/providers/tenantStore/compatibilityEvidenceRef` | Platform maintainer | Which recorded qualification proves separate tenant-key provider compatibility and support? |
| `/providers/dapr/name` | Platform maintainer | What exact Dapr runtime and Vault component compatibility set artifact name is selected? |
| `/providers/dapr/version` | Platform maintainer | What exact supported Dapr runtime and Vault component compatibility set version is approved? |
| `/providers/dapr/digest` | Platform maintainer | What exact Dapr runtime and Vault component compatibility set content digest is approved? |
| `/providers/dapr/compatibilityEvidenceRef` | Platform maintainer | Which recorded qualification proves Dapr runtime and Vault component compatibility set compatibility and support? |
| `/providers/installationTool/name` | Platform maintainer | What exact future owner-controlled installation tool artifact name is selected? |
| `/providers/installationTool/version` | Platform maintainer | What exact supported future owner-controlled installation tool version is approved? |
| `/providers/installationTool/digest` | Platform maintainer | What exact future owner-controlled installation tool content digest is approved? |
| `/providers/installationTool/compatibilityEvidenceRef` | Platform maintainer | Which recorded qualification proves future owner-controlled installation tool compatibility and support? |
| `/providers/verificationTool/name` | Platform maintainer | What exact future isolated verification tool artifact name is selected? |
| `/providers/verificationTool/version` | Platform maintainer | What exact supported future isolated verification tool version is approved? |
| `/providers/verificationTool/digest` | Platform maintainer | What exact future isolated verification tool content digest is approved? |
| `/providers/verificationTool/compatibilityEvidenceRef` | Platform maintainer | Which recorded qualification proves future isolated verification tool compatibility and support? |
| `/ordinaryStore/instanceId` | Administrator | What distinct environment-specific instance identity is selected for ordinary staging OpenBao? |
| `/ordinaryStore/backend` | Administrator | Which storage backend is approved for ordinary staging OpenBao, with qualification evidence? |
| `/ordinaryStore/storage` | Administrator | What complete physical state, volume, class, capacity and encryption allocation is approved for ordinary staging OpenBao? |
| `/ordinaryStore/storage/stateId` | Administrator | What unique physical persisted-state identity contains ordinary staging OpenBao? |
| `/ordinaryStore/storage/volumeIds` | Administrator | Which exact independent persistent volume identities contain ordinary staging OpenBao? |
| `/ordinaryStore/storage/className` | Administrator | Which accepted staging storage class is selected for ordinary staging OpenBao? |
| `/ordinaryStore/storage/capacity` | Administrator | What exact capacity quantity is approved for ordinary staging OpenBao? |
| `/ordinaryStore/storage/encryptionEvidenceRef` | Administrator and recovery deputy | Which current node-encryption and independent custody evidence covers ordinary staging OpenBao? |
| `/ordinaryStore/snapshotDomainId` | Backup and key-custody owners | What unique whole-state snapshot-domain identity contains ordinary staging OpenBao? |
| `/ordinaryStore/replicas` | Administrator | What exact replica count is qualified for ordinary staging OpenBao? |
| `/ordinaryStore/resources` | Administrator | What complete resource requests and limits are approved for ordinary staging OpenBao? |
| `/ordinaryStore/resources/cpuRequest` | Administrator | What exact cpuRequest quantity is approved for ordinary staging OpenBao? |
| `/ordinaryStore/resources/memoryRequest` | Administrator | What exact memoryRequest quantity is approved for ordinary staging OpenBao? |
| `/ordinaryStore/resources/cpuLimit` | Administrator | What exact cpuLimit quantity is approved for ordinary staging OpenBao? |
| `/ordinaryStore/resources/memoryLimit` | Administrator | What exact memoryLimit quantity is approved for ordinary staging OpenBao? |
| `/ordinaryStore/restrictedPodEvidenceRef` | Platform maintainer | Which qualification proves ordinary staging OpenBao meets restricted Pod Security including capabilities, user, seccomp and volume access? |
| `/ordinaryStore/nativeTls` | Network owner | What exact native-TLS endpoint, port, SAN, trust reference and pod selector are approved for ordinary staging OpenBao? |
| `/ordinaryStore/nativeTls/endpoint` | Network owner | What exact HTTPS endpoint terminates native TLS for ordinary staging OpenBao? |
| `/ordinaryStore/nativeTls/port` | Network owner | What exact native-TLS listener port is approved for ordinary staging OpenBao? |
| `/ordinaryStore/nativeTls/serverName` | Network owner | Which exact SAN/server name must be verified for ordinary staging OpenBao? |
| `/ordinaryStore/nativeTls/caBundleRef` | Network owner | Which evidence reference defines the exact public CA source and selected caCert mounted-file or caPem inline-public-CA delivery mode for ordinary staging OpenBao, including the sidecar directory mount/file path or inline metadata mapping and its verification qualification? |
| `/ordinaryStore/nativeTls/namespace` | Network owner | Which exact namespace contains the ordinary staging OpenBao endpoint? |
| `/ordinaryStore/nativeTls/podLabels` | Network owner | What exact pod selector is approved for ordinary staging OpenBao? |
| `/ordinaryStore/nativeTls/podLabels/key` | Network owner | Which pod-selector label key selects only ordinary staging OpenBao? |
| `/ordinaryStore/nativeTls/podLabels/value` | Network owner | Which pod-selector label value selects only ordinary staging OpenBao? |
| `/tenantStore/instanceId` | Administrator | What distinct environment-specific instance identity is selected for separate staging tenant-key store? |
| `/tenantStore/backend` | Administrator | Which storage backend is approved for separate staging tenant-key store, with qualification evidence? |
| `/tenantStore/storage` | Administrator | What complete physical state, volume, class, capacity and encryption allocation is approved for separate staging tenant-key store? |
| `/tenantStore/storage/stateId` | Administrator | What unique physical persisted-state identity contains separate staging tenant-key store? |
| `/tenantStore/storage/volumeIds` | Administrator | Which exact independent persistent volume identities contain separate staging tenant-key store? |
| `/tenantStore/storage/className` | Administrator | Which accepted staging storage class is selected for separate staging tenant-key store? |
| `/tenantStore/storage/capacity` | Administrator | What exact capacity quantity is approved for separate staging tenant-key store? |
| `/tenantStore/storage/encryptionEvidenceRef` | Administrator and recovery deputy | Which current node-encryption and independent custody evidence covers separate staging tenant-key store? |
| `/tenantStore/snapshotDomainId` | Backup and key-custody owners | What unique whole-state snapshot-domain identity contains separate staging tenant-key store? |
| `/tenantStore/replicas` | Administrator | What exact replica count is qualified for separate staging tenant-key store? |
| `/tenantStore/resources` | Administrator | What complete resource requests and limits are approved for separate staging tenant-key store? |
| `/tenantStore/resources/cpuRequest` | Administrator | What exact cpuRequest quantity is approved for separate staging tenant-key store? |
| `/tenantStore/resources/memoryRequest` | Administrator | What exact memoryRequest quantity is approved for separate staging tenant-key store? |
| `/tenantStore/resources/cpuLimit` | Administrator | What exact cpuLimit quantity is approved for separate staging tenant-key store? |
| `/tenantStore/resources/memoryLimit` | Administrator | What exact memoryLimit quantity is approved for separate staging tenant-key store? |
| `/tenantStore/restrictedPodEvidenceRef` | Platform maintainer | Which qualification proves separate staging tenant-key store meets restricted Pod Security including capabilities, user, seccomp and volume access? |
| `/tenantStore/nativeTls` | Network owner | What exact native-TLS endpoint, port, SAN, trust reference and pod selector are approved for separate staging tenant-key store? |
| `/tenantStore/nativeTls/endpoint` | Network owner | What exact HTTPS endpoint terminates native TLS for separate staging tenant-key store? |
| `/tenantStore/nativeTls/port` | Network owner | What exact native-TLS listener port is approved for separate staging tenant-key store? |
| `/tenantStore/nativeTls/serverName` | Network owner | Which exact SAN/server name must be verified for separate staging tenant-key store? |
| `/tenantStore/nativeTls/caBundleRef` | Network owner | Which evidence reference defines the exact public CA source and selected caCert mounted-file or caPem inline-public-CA delivery mode for separate staging tenant-key store, including the sidecar directory mount/file path or inline metadata mapping and its verification qualification? |
| `/tenantStore/nativeTls/namespace` | Network owner | Which exact namespace contains the separate staging tenant-key store endpoint? |
| `/tenantStore/nativeTls/podLabels` | Network owner | What exact pod selector is approved for separate staging tenant-key store? |
| `/tenantStore/nativeTls/podLabels/key` | Network owner | Which pod-selector label key selects only separate staging tenant-key store? |
| `/tenantStore/nativeTls/podLabels/value` | Network owner | Which pod-selector label value selects only separate staging tenant-key store? |
| `/network/dependencies/*/purpose` | Network owner | What specific required DNS, replication, monitoring or custody flow is allowed? |
| `/network/dependencies/*/sourceIdentity` | Network owner | Which evidence reference identifies the exact reviewed source namespace and pod-selector definition for this flow? |
| `/network/dependencies/*/destinationIdentity` | Network owner | Which evidence reference identifies the exact reviewed destination namespace/pod-selector or external-address definition for this flow? |
| `/network/dependencies/*/port` | Network owner | Which exact qualified dependency port is allowed? |
| `/network/dependencies/*/protocol` | Network owner | Which exact transport protocol is allowed? |
| `/network/dependencies/*/evidenceRef` | Network owner | Which qualification establishes this dependency and its encryption requirements? |
| `/custody/coverage/timeZone` | Administrator and recovery deputy | What exact response-coverage time zone is approved? |
| `/custody/coverage/scheduleRef` | Administrator and recovery deputy | Which published primary/deputy coverage schedule is approved? |
| `/custody/coverage/maxAcknowledgmentSeconds` | Administrator and recovery deputy | What maximum response acknowledgment time applies within that coverage? |
| `/backup/ordinary/custodyClass` | Backup and key-custody owners | What ordinary-secrets backup custody class is approved? |
| `/backup/ordinary/destinationRef` | Backup and key-custody owners | Which immutable encrypted off-site ordinary-secrets prefix is selected? |
| `/backup/ordinary/accessPrincipal` | Backup and key-custody owners | Which scoped independent principal may read ordinary-secret backups? |
| `/backup/ordinary/decryptionCustodyRef` | Backup and key-custody owners | Which independent ordinary-backup decryption custody reference is approved? |
| `/backup/tenantKeys/custodyClass` | Backup and key-custody owners | What distinct tenant-key backup custody class is approved? |
| `/backup/tenantKeys/destinationRef` | Backup and key-custody owners | Which separate off-site tenant-key backup location is selected, never with ciphertext backups? |
| `/backup/tenantKeys/accessPrincipal` | Backup and key-custody owners | Which separately scoped tenant-key custody principal is selected? |
| `/backup/tenantKeys/decryptionCustodyRef` | Backup and key-custody owners | Which separate key-backup decryption custody reference is approved? |
| `/secretContract/apps/*/appId` | Platform maintainer | What exact declared Dapr app ID is being enrolled in staging? |
| `/secretContract/apps/*/serviceAccount` | Administrator | What exact least-privilege Kubernetes service account runs this app? |
| `/secretContract/apps/*/componentName` | Platform maintainer | Which distinct environment-layer Dapr secret-store Component name is assigned to this app? |
| `/secretContract/apps/*/scopes` | Platform maintainer | Which exact single app scope is allowed on this per-app secret-store Component? |
| `/secretContract/apps/*/policyRef` | Administrator | Which value-free reviewed per-app least-privilege OpenBao policy reference is approved? |
| `/secretContract/apps/*/allowedSecretPaths` | Administrator | Which exact static or owned dynamic secret paths may this app read, with scoped dynamic namespaces justified by the policy evidence? |
| `/secretContract/apps/*/dynamicNamespaceWriterRef` | Platform maintainer | Which declaration names the sole owning writer for each dynamic namespace, or records that none are needed? |
| `/secretContract/apps/*/token` | Administrator | What complete per-app bootstrap reference, volume, mount, delivery revision, external renewal and restart contract is approved? |
| `/secretContract/apps/*/token/bootstrapSecretRef` | Administrator | Which canonical unqualified per-app bootstrap Secret name in the application namespace delivers the token without values in Git (the key belongs only in sourceKey)? |
| `/secretContract/apps/*/token/volumeName` | Platform maintainer | Which pod volume name carries only this app token? |
| `/secretContract/apps/*/token/mountDirectory` | Platform maintainer | What absolute sidecar directory mount path is approved for the per-pod token volume? |
| `/secretContract/apps/*/token/sourceKey` | Platform maintainer | Which valid bootstrap Secret data key, supplied separately from its unqualified Secret name, is projected into the per-app token volume? |
| `/secretContract/apps/*/token/projectedFileName` | Platform maintainer | Which single relative filename maps that bootstrap key into the mounted directory? |
| `/secretContract/apps/*/token/mountPath` | Platform maintainer | What full absolute token file path, equal to mountDirectory/projectedFileName, is approved for vaultTokenMountPath? |
| `/secretContract/apps/*/token/deliveryRevision` | Platform maintainer | Which required token delivery revision must each restarted pod acknowledge? |
| `/secretContract/apps/*/token/renewal` | Administrator | What complete external renewal owner, TTL, lead time, issuer qualification and monitor contract is approved for this app? |
| `/secretContract/apps/*/token/renewal/owner` | Administrator | Who is the named per-app token renewal owner outside Dapr? |
| `/secretContract/apps/*/token/renewal/ttlSeconds` | Administrator | What exact bounded per-app token TTL is approved? |
| `/secretContract/apps/*/token/renewal/renewBeforeSeconds` | Administrator | How many seconds before expiry must this app token be renewed or replaced? |
| `/secretContract/apps/*/token/renewal/renewabilityEvidenceRef` | Administrator | Which sanitized issuer metadata proves the selected token lifecycle and whether renewal or replacement is supported? |
| `/secretContract/apps/*/token/renewal/monitorRef` | Platform maintainer | Which monitor and notification evidence proves per-app expiry and renewal failure alerts reach both owners? |
| `/secretContract/apps/*/token/restartOwner` | Platform maintainer | Who owns replacement delivery, pod and sidecar restart, and acknowledgment before old-token revocation? |
| `/secretContract/apps/*/token/restartEvidenceRef` | Platform maintainer | Which future authenticated qualification binds the complete expected pod inventory and all-replica coverage to token delivery, new pod identity, sidecar restart ordering and acknowledgment? |
| `/secretContract/apps/*/requiredSecrets` | Platform maintainer | Which complete non-empty list of logical secret IDs, exact required generations and owners belongs to this app? |
| `/secretContract/apps/*/requiredSecrets/*/secretId` | Platform maintainer | What value-free logical secret ID is required by this app? |
| `/secretContract/apps/*/requiredSecrets/*/generation` | Platform maintainer | What exact positive generation of this secret is required by the staging contract? |
| `/secretContract/apps/*/requiredSecrets/*/owner` | Platform maintainer | Who owns this secret generation and its acknowledged rotation? |

## Already resolved facts

The six environment target fields, three candidate CoreDNS fields and two historical bootstrap-token fields have cited answers in `inputs.resolved.json`; no new answer is requested for them. See `INPUT-EVIDENCE.md` for source hashes, JSON pointers/lines and evidence limits.

The contract instance `deploy/dapr/openbao-secret-contract.yaml` is absent at the preparation cut. No existing app inventory, secret generation, observed image, conventional listener port or current operator identity is promoted to a staging choice.

Resource quantities use a conservative subset: nonnegative integer millicores
or decimal CPU cores with at most three fractional digits; memory uses integer
bytes with optional k/M/G/T/P/E or Ki/Mi/Gi/Ti/Pi/Ei suffixes. Signs, exponent
notation and fractional memory are refused. Matching requests may not exceed
limits. All quantities remain owner-supplied; no value is defaulted or rounded.

Network source/destination identities are evidence references to exact reviewed
namespace/selector or address definitions. `policyEvidenceRef` must cover each
DNS, ordinary-store, tenant-key-store, replication, monitoring and custody flow
or state explicit non-applicability. The assessor does not parse selectors or
prove dependency coverage; synthetic fixture completion cannot prove it.
