# Primary-source primitive and input reconciliation review — 2026-10-20

Scope: the 2026-10-20 erasure-authority decision and custody spine, read against HANDOFF.md, INPUTS.md, the 2026-10-18 provider decision, Platform custody interfaces and the inspected Agents/EventStore contracts. This review made read-only public web/feed/source reads and verified the retained negative-control checksums. It deployed nothing, read no private credential values and performed no destructive experiment.

## Verdict

**Suitable as a proposed owner decision after the clarifications below; not a qualifying production architecture under the unchanged native-transaction criterion.** The document accurately discloses that B1/B3/B5 and KMS B4 are separate boundaries. No verified product/API version error or hidden cross-service transaction claim was found. OA-1 is an explicit known blocker, not a new review finding. Cloud installation, complete copy closure and successful original-outcome recovery remain unverified as stated.

## Findings

### PR-1 — Specify the epoch switch and old-epoch write prohibition (P2)

Decision lines 70, 78–89 and 133–147; spine AD-17/AD-20/AD-22.

The decision freezes a tenant epoch, permits intervening survivor writes and requires B5 to merge them, but B3 does not explicitly change `head.currentEpoch` to the adopted successor. An independently built writer could continue creating envelopes under E(g) between inventory closure and scheduling; deleting E(g) would then destroy an uninventoried survivor. This is a missing transaction invariant, not a missing AWS operation.

Require B1 to fence all new Encrypt/GenerateDataKey/ReEncrypt-to-E(g) admissions and reconcile every admitted old-epoch call before B2 closure. Require the B3 CAS to adopt the verified successor checkpoint **and** set the current allocation/wrapping epoch to E(g+1). Survivor writes resume only after that CAS; selected-target release/copy stays fenced. No late response or stale worker can adopt an E(g) envelope. Add these cuts to F0/F3. This follows the existing design and needs no additional Product policy.

### PR-2 — State the KMS eventual-consistency limit beside the fence (P2)

Decision lines 70–93, 121–149; spine AD-18/AD-20/AD-21.

AWS documents eventual consistency for KMS resource changes and ReEncrypt. Therefore disablement, scheduled deletion, grant/policy revocation and an observed state response cannot be the current linearizable use fence. The existing independent head and holder-completion requirements are the correct construction, but the document should say this explicitly and describe the “shared cut” as common whole-key dependency erasure, conditional on the documented final deletion guarantee. Public AWS documentation does not separately prove an instantaneous linearizable cross-request physical cut.

Keep Unknown until exact final DeleteKey evidence and closure; recheck/reconcile admitted native calls and authenticated holders after current-authority fencing. Include delayed native visibility and late native responses in F3. [AWS KMS access and eventual consistency](https://docs.aws.amazon.com/kms/latest/developerguide/accessing-kms.html), [ReEncrypt](https://docs.aws.amazon.com/kms/latest/APIReference/API_ReEncrypt.html).

### PR-3 — Separate independent export keys from same-root occurrence copies (P2)

Decision lines 110–127; spine AD-23/AD-24; Agents EXT-EXPORT-STORE-1 and its phase-pinned lifecycle policy.

The copy table groups exports with same-original-interaction-root occurrences. The existing export contract separately owns artifact/manifest/index/wrapped-export-key lifecycle, and its policy/implementation remain Open. A plaintext-equivalent export encrypted under a separate surviving export key is another decrypting path; deleting E(g) does not erase it. An implementer must not silently reinterpret an export key as the interaction DEK.

Clarify that each such export path needs its own accepted phase-pinned closure/disposition proof before an all-copy B5 receipt. Unresolved export policy or unqualified independent export custody blocks that target’s complete-copy result. A genuinely export-free branch can proceed through its other gates. This preserves the accepted Open disposition and does not invent a new export policy. The existing “Open policy does not authorize omission” caution is correct but should be made operational in the table and acceptance criteria.

## Verified supported primitives and versions

| Binding | Primary-source check and practical limit |
| --- | --- |
| Standard generated single-Region symmetric customer-managed KMS keys | AWS documents final whole-key deletion as irreversible. Scheduling uses 7–30 days and may finish later; cancellation is possible before actual deletion. Custom-store backup and replica/import exclusions are appropriately conservative. The selected scheme remains conditional on vendor assurance and complete plaintext/data-key closure. [Deletion](https://docs.aws.amazon.com/kms/latest/developerguide/deleting-keys.html). |
| KMS ReEncrypt | Decrypts and reencrypts KMS ciphertext inside KMS; applicable to Encrypt/GenerateDataKey ciphertext, not arbitrary AWS Encryption SDK envelopes. The proposed 32-byte X protected with direct KMS Encrypt/GenerateDataKey fits. Require exact source/destination ARNs and matching authenticated contexts. [API](https://docs.aws.amazon.com/kms/latest/APIReference/API_ReEncrypt.html). |
| Final DeleteKey evidence | DeleteKey is a real CloudTrail service event after scheduled deletion; it is distinct from ScheduleKeyDeletion. It can bind the affected key ARN. An event is provider evidence, not independent vendor-media inspection, exactly as the decision states. [DeleteKey](https://docs.aws.amazon.com/kms/latest/developerguide/ct-delete-key.html). |
| DynamoDB transactions | Up to 100 actions, one account/Region, 4 MB aggregate and 400 KB items; idempotency token lifetime is ten minutes. A small conditional head transaction may reference a complete immutable external vector. It does not transact S3 or KMS. [TransactWriteItems](https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_TransactWriteItems.html). |
| DynamoDB restore/current reads | Restore creates a new table; current authoritative reads need strong table reads. TableId is a real returned identifier. Restored tables require explicit independent re-enrollment, as proposed. No native permanent append-only/nonrollback guarantee is claimed. [Restore](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/pointintimerecovery_restores.html), [TableDescription](https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_TableDescription.html). |
| S3 Object Lock | Retention applies to a particular object version; newer versions/deletion markers are possible. Exact version/hash selection and prepublication retention verification in the design are necessary. Compliance retention supplies immutability for retained versions, not ordering/current authority. [Object Lock](https://docs.aws.amazon.com/AmazonS3/latest/userguide/object-lock.html). |
| CloudTrail validation/public keys | Log/digest integrity validation and ListPublicKeys are real supported primitives. Keeping original logs, digests, paths, signature metadata and verification keys is appropriate for long-lived independent checking. They do not prove absence of undeclared copies. [Validation](https://docs.aws.amazon.com/awscloudtrail/latest/userguide/cloudtrail-log-file-validation-intro.html), [ListPublicKeys](https://docs.aws.amazon.com/awscloudtrail/latest/APIReference/API_ListPublicKeys.html). |
| .NET SDK/runtime | Microsoft currently publishes SDK 10.0.402 with ASP.NET Core/.NET runtime 10.0.13, released 2026-10-13. Local runtime output is consistent. Build compatibility/image native dependencies remain explicitly untested. [Download/release inventory](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). |
| AWS SDK pins | Independently fetched all four public NuGet flat-container indexes. Each exact pin exists and was the latest listed: KeyManagementService 4.0.100.19, DynamoDBv2 4.0.108, S3 4.0.104.5, CloudTrail 4.0.101.7. Publisher metadata establishes existence, not the unimplemented graph’s successful build. [KMS feed](https://api.nuget.org/v3-flatcontainer/awssdk.keymanagementservice/index.json), [DynamoDB feed](https://api.nuget.org/v3-flatcontainer/awssdk.dynamodbv2/index.json), [S3 feed](https://api.nuget.org/v3-flatcontainer/awssdk.s3/index.json), [CloudTrail feed](https://api.nuget.org/v3-flatcontainer/awssdk.cloudtrail/index.json). |
| Existing OpenBao/Dapr/DB operators | OpenBao v2.6.2, Dapr v1.18.1, PostgreSQL 18.6, CloudNativePG 1.30.0 and Barman 0.15.0 are real published versions. The document labels them selected installed observations, not latest-release or health qualification. Dapr and CloudNativePG have newer patches; later acceptance must bind the actual tested dependency versions. [OpenBao](https://github.com/openbao/openbao/releases/tag/v2.6.2), [Dapr](https://github.com/dapr/dapr/releases/tag/v1.18.1), [PostgreSQL](https://www.postgresql.org/docs/release/18.6/), [CloudNativePG](https://cloudnative-pg.io/docs/current/release_notes/v1.30/), [Barman](https://github.com/cloudnative-pg/plugin-barman-cloud/releases/tag/v0.15.0). |

The managed service API dates in the proposed bill of materials correspond to their published service APIs. Binding current account/resource identities and testing quotas in Paris remain provisioning work, not established entitlements. Existing image digests were checked against retained selected infrastructure metadata; this review did not independently re-pull images or qualify host health.

## Input reconciliation

- HANDOFF’s reviewed Platform candidate, Committed/unavailable status, absent production authority, absence of installed qualification roles, missing three anchors and OQ-5.4-1 dependencies are preserved. The recommendation supplies no speculative runtime strings and calls no owner command.
- INPUTS’s timing 300/30/600/86400/604800, 2026-10-08 date, 192.168.1.30 host, Open export disposition, independent approval issuer and still-missing lifecycle/public references are retained. A proposed AWS installation is explicitly new and unapproved.
- The 2026-10-18 ordinary-Raft revival evidence was verified with its SHA256SUMS: both retained native result/command match. The new decision rejects ordinary recovery and does not reinterpret a failed negative control as qualification.
- CustodyKeyWrapCore’s exact original 32-byte DEK/KEK, AES-GCM lengths, KekVersion and complete `candidate-custody-key-wrap-v1` AAD are preserved inside C0. The extra erasure gate is private. A historical ungated C0 remains a bypass, and the decision correctly refuses to claim migration closure.
- ICustodyKeyLifecycleProvider lookup remains read-only; outcomes require independent physical/restore evidence; inherited anchored transition authority remains mandatory. EventStore’s unchanged all-or-none manifest/original vector semantics remain an OA-1 adoption blocker rather than silently relaxed code.
- Candidate EventStore gitlink and inspected sibling HEAD are explicitly distinct. No inspected sibling revision is inferred accepted. The input binding’s pre-update spine hash was initially ambiguous; the author corrected it to an explicit immutable git-blob baseline during review. Final rendered artifacts must receive separate output hashes after finalization.

## Remaining acceptance limits

This review verifies documentation/source fit only. It establishes no cloud resource, IAM denial proof, survivor checkpoint, plaintext-zeroing mechanism, 1,000-target result, root-reissue defense, failure-atomic native erasure or Available dependency. OA-1–OA-6 and F0–F3 remain real gates. With the three clarifications applied, the construction can be presented honestly for owner approval/rejection; literal native atomicity remains unsatisfied.
