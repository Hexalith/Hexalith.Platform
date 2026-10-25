# Conditional installation commands

These commands are a runbook for the responsible owners, **not a script to execute as a whole**. Only the metadata inspection and offline refusal identified in `installation-decision.md` were run for this packet. Provisioning, deployment, cost, enrollment writes and physical experiments remain outside the current authorization. Commands containing variables require the exact D1–D9 returns; do not infer missing values. Never set `HOME`, `CODEX_HOME` or print credential values.

## 1. Read-only local installation census — Platform infrastructure owner

Run from the Platform checkout. Preserve stdout/stderr/exit and UTC start/end under a fresh evidence path, as in the retained observations. The kubeconfig is inspection access only, never an application credential.

```bash
set -euo pipefail
kubectl --kubeconfig /home/administrator/.kube/hexalith-production --context jpiquot@local --request-timeout=20s get nodes -o wide
kubectl --kubeconfig /home/administrator/.kube/hexalith-production --context jpiquot@local --request-timeout=20s get deployments,statefulsets -A -o 'custom-columns=KIND:.kind,NS:.metadata.namespace,NAME:.metadata.name,UID:.metadata.uid,REPLICAS:.spec.replicas,READY:.status.readyReplicas,IMAGE:.spec.template.spec.containers[*].image'
kubectl --kubeconfig /home/administrator/.kube/hexalith-production --context jpiquot@local --request-timeout=20s get pods -n openbao -o 'custom-columns=NAME:.metadata.name,NODE:.spec.nodeName,IMAGEID:.status.containerStatuses[*].imageID'
kubectl --kubeconfig /home/administrator/.kube/hexalith-production --context jpiquot@local --request-timeout=20s get components -A -o 'custom-columns=NS:.metadata.namespace,NAME:.metadata.name,TYPE:.spec.type,SCOPES:.scopes'
kubectl --kubeconfig /home/administrator/.kube/hexalith-production --context jpiquot@local --request-timeout=20s get storageclasses -o wide
kubectl --kubeconfig /home/administrator/.kube/hexalith-production --context jpiquot@local --request-timeout=20s get configurations.dapr.io daprsystem -n dapr-system -o 'jsonpath={.spec.mtls}'
```

Do not dump Secrets, kubeconfig raw credentials, environment values or entire container profiles. For role/current-scope verification use metadata and owner-signed public manifests, not `kubectl exec`, logs containing credentials, or new test workloads.

## 2. Resource identity inspection — independent C/A/R owners

Run on an owner workstation with reviewed AWS CLI v2 and explicitly scoped profiles. The local workstation used to prepare this packet has no AWS CLI or configured AWS profile files. These profile names are owner-provided local references, not new administrator identities.

```bash
set -euo pipefail
: "${CUSTODY_AUDIT_PROFILE:?D2 scoped custody read-only profile}"
: "${AUTHORITY_AUDIT_PROFILE:?D2 scoped authority read-only profile}"
: "${ARCHIVE_AUDIT_PROFILE:?D2 scoped archive read-only profile}"
: "${AUTHORITY_TABLE_ARN:?D2 actual enrolled ARN}"
: "${CHECKPOINT_BUCKET:?D2 actual approved bucket}"
: "${ORIGINAL_BUCKET:?D2 actual approved bucket}"
: "${ARCHIVE_ACCOUNT_ID:?D2 actual 12-digit owner}"

aws --profile "$CUSTODY_AUDIT_PROFILE" --region eu-west-3 sts get-caller-identity
aws --profile "$AUTHORITY_AUDIT_PROFILE" --region eu-west-3 sts get-caller-identity
aws --profile "$ARCHIVE_AUDIT_PROFILE" --region eu-west-1 sts get-caller-identity
aws --profile "$AUTHORITY_AUDIT_PROFILE" --region eu-west-3 dynamodb describe-table --table-name "$AUTHORITY_TABLE_ARN"
aws --profile "$AUTHORITY_AUDIT_PROFILE" --region eu-west-3 dynamodb describe-time-to-live --table-name "$AUTHORITY_TABLE_ARN"
aws --profile "$AUTHORITY_AUDIT_PROFILE" --region eu-west-3 dynamodb describe-continuous-backups --table-name "$AUTHORITY_TABLE_ARN"

for custody_archive in "$CHECKPOINT_BUCKET" "$ORIGINAL_BUCKET"; do
  aws --profile "$ARCHIVE_AUDIT_PROFILE" --region eu-west-1 s3api get-bucket-location --bucket "$custody_archive" --expected-bucket-owner "$ARCHIVE_ACCOUNT_ID"
  aws --profile "$ARCHIVE_AUDIT_PROFILE" --region eu-west-1 s3api get-bucket-versioning --bucket "$custody_archive" --expected-bucket-owner "$ARCHIVE_ACCOUNT_ID"
  aws --profile "$ARCHIVE_AUDIT_PROFILE" --region eu-west-1 s3api get-object-lock-configuration --bucket "$custody_archive" --expected-bucket-owner "$ARCHIVE_ACCOUNT_ID"
  aws --profile "$ARCHIVE_AUDIT_PROFILE" --region eu-west-1 s3api get-public-access-block --bucket "$custody_archive" --expected-bucket-owner "$ARCHIVE_ACCOUNT_ID"
done
```

For **each** returned epoch/protector ARN, run the following separately and retain exact argv and public metadata. Compare all seven actual ARN/IDs and native properties with the independently signed mapping; do not substitute aliases/tags for IDs.

```bash
set -euo pipefail
: "${ENROLLED_KEY_ARN:?D2 exact key ARN from retained mapping}"
aws --profile "$CUSTODY_AUDIT_PROFILE" --region eu-west-3 kms describe-key --key-id "$ENROLLED_KEY_ARN"
```

For each archive enrollment object, verify the **exact version**, current compliance retention and public enrollment bytes. `ENROLLMENT_PUBLIC_FILE` must be a fresh local file; only this public object is downloaded, no key material.

```bash
set -euo pipefail
: "${ENROLLMENT_BUCKET:?D2 enrolled checkpoint or original bucket}"
: "${ENROLLMENT_OBJECT_KEY:?D2 exact public enrollment key}"
: "${ENROLLMENT_VERSION_ID:?D2 exact immutable version}"
: "${ENROLLMENT_PUBLIC_FILE:?fresh local output for public enrollment only}"
test ! -e "$ENROLLMENT_PUBLIC_FILE"
aws --profile "$ARCHIVE_AUDIT_PROFILE" --region eu-west-1 s3api head-object --bucket "$ENROLLMENT_BUCKET" --key "$ENROLLMENT_OBJECT_KEY" --version-id "$ENROLLMENT_VERSION_ID" --expected-bucket-owner "$ARCHIVE_ACCOUNT_ID"
aws --profile "$ARCHIVE_AUDIT_PROFILE" --region eu-west-1 s3api get-object-retention --bucket "$ENROLLMENT_BUCKET" --key "$ENROLLMENT_OBJECT_KEY" --version-id "$ENROLLMENT_VERSION_ID" --expected-bucket-owner "$ARCHIVE_ACCOUNT_ID"
aws --profile "$ARCHIVE_AUDIT_PROFILE" --region eu-west-1 s3api get-object --bucket "$ENROLLMENT_BUCKET" --key "$ENROLLMENT_OBJECT_KEY" --version-id "$ENROLLMENT_VERSION_ID" --expected-bucket-owner "$ARCHIVE_ACCOUNT_ID" "$ENROLLMENT_PUBLIC_FILE"
sha256sum "$ENROLLMENT_PUBLIC_FILE"
```

A matching hash establishes bytes, not enrollment authority. E verifies its signature/current issuer/scope and independently accepted genesis. Table or archive outage fails closed. A new/restored TableId is never accepted as a repair.

## 3. Provision proposed empty resources — C/A/R owners, later authorization only

Do **not** run this section for the current task. It creates resources, spends money and sets durable retention. First obtain D1–D4, a current quote/alarm and reviewed policies binding the actual principals/accounts/resources. The cloud custodian must supply the owner-reviewed key policies, cross-account table/bucket policies, account guardrails and budget controls; they cannot be correctly prefilled without those identities. Verify caller Account/ARN against the approved record before each account's mutations. Do not automatically retry a timed-out creation: reconcile the original request/resource before proceeding.

Create **one** exact current-authority table using the schema actually consumed by `AwsAuthorityStore`, in account A:

```bash
set -euo pipefail
: "${AUTHORITY_PROVISION_PROFILE:?D2 separately approved provisioner profile}"
aws --profile "$AUTHORITY_PROVISION_PROFILE" --region eu-west-3 dynamodb create-table \
  --table-name hexalith-custody-f2f3-20261023-current \
  --attribute-definitions AttributeName=authorityTenant,AttributeType=S \
  --key-schema AttributeName=authorityTenant,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST --deletion-protection-enabled
aws --profile "$AUTHORITY_PROVISION_PROFILE" --region eu-west-3 dynamodb describe-table \
  --table-name hexalith-custody-f2f3-20261023-current
```

Retain actual ARN/TableId/status. Do not enable TTL/global-table replication or invoke restore. The table is **empty/unusable** until independently verified genesis/checkpoints are enrolled by the owning service. No generic `put-item` genesis command is supplied: the signed original format, current-authority implementation and enrollment authority are missing, and application startup must not manufacture them. The same rule applies to the separate authority-service store bundle.

In account R, only after verifying exact approved name ownership/availability and approved numeric retention, create both archives. SSE-S3 avoids wrapping the recovery archives under an erasure epoch. These commands use the recommended new placement; approve alternate names before any substitution.

```bash
set -euo pipefail
: "${ARCHIVE_PROVISION_PROFILE:?D2 separately approved archive provisioner}"
: "${ARCHIVE_ACCOUNT_ID:?D2 actual approved 12-digit archive owner}"
: "${RETENTION_DAYS:?D1 Governance accepted integer minimum and extension schedule}"
for custody_archive in hexalith-custody-f2f3-20261023-checkpoints hexalith-custody-f2f3-20261023-originals; do
  aws --profile "$ARCHIVE_PROVISION_PROFILE" --region eu-west-1 s3api create-bucket \
    --bucket "$custody_archive" --create-bucket-configuration LocationConstraint=eu-west-1 \
    --object-lock-enabled-for-bucket --object-ownership BucketOwnerEnforced
  aws --profile "$ARCHIVE_PROVISION_PROFILE" --region eu-west-1 s3api put-public-access-block \
    --bucket "$custody_archive" --expected-bucket-owner "$ARCHIVE_ACCOUNT_ID" \
    --public-access-block-configuration BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true
  aws --profile "$ARCHIVE_PROVISION_PROFILE" --region eu-west-1 s3api put-bucket-versioning \
    --bucket "$custody_archive" --expected-bucket-owner "$ARCHIVE_ACCOUNT_ID" --versioning-configuration Status=Enabled
  aws --profile "$ARCHIVE_PROVISION_PROFILE" --region eu-west-1 s3api put-bucket-encryption \
    --bucket "$custody_archive" --expected-bucket-owner "$ARCHIVE_ACCOUNT_ID" \
    --server-side-encryption-configuration '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"}}]}'
  aws --profile "$ARCHIVE_PROVISION_PROFILE" --region eu-west-1 s3api put-object-lock-configuration \
    --bucket "$custody_archive" --expected-bucket-owner "$ARCHIVE_ACCOUNT_ID" \
    --object-lock-configuration "ObjectLockEnabled=Enabled,Rule={DefaultRetention={Mode=COMPLIANCE,Days=$RETENTION_DAYS}}"
done
```

The 120-day minimum/review 30 days before expiry is a proposed starting policy from the prior packet; Governance must accept actual values and extend all live referenced originals, journals and public verifiers before expiry. Do not configure an archive lifecycle expiration or authority TTL. The component's actual `RetainUntil` must be independently enrolled and checked against every object, not inferred solely from the bucket default.

Create each of the exact **seven** named native keys once, in account C. Run this command separately for each name listed in the decision table, with a retained original creation record and a purpose-specific policy file. Do not use a shell loop that reissues all creates after uncertainty; create-key has no task-level exact-original recovery built into this runbook.

```bash
set -euo pipefail
: "${CUSTODY_PROVISION_PROFILE:?D2 approved native key provisioner}"
: "${APPROVED_KEY_NAME:?one exact approved seven-key name}"
: "${APPROVED_KEY_POLICY_FILE:?D2 owner-reviewed exact purpose/principal policy file}"
aws --profile "$CUSTODY_PROVISION_PROFILE" --region eu-west-3 kms create-key \
  --key-spec SYMMETRIC_DEFAULT --key-usage ENCRYPT_DECRYPT --origin AWS_KMS --no-multi-region \
  --description "$APPROVED_KEY_NAME" --tags "TagKey=Name,TagValue=$APPROVED_KEY_NAME" \
  --policy "file://$APPROVED_KEY_POLICY_FILE"
```

Return all actual disjoint ARN/IDs. No schedule-delete, cancel-delete, key encryption probe, worker action, native physical experiment or Live/Full command is included here. Key generation and public trust enrollment are separate owners/actions; these seven native keys do not create qualification or decision-signing anchors.

AWS CLI syntax references: [create-table](https://docs.aws.amazon.com/cli/latest/reference/dynamodb/create-table.html), [create-bucket](https://docs.aws.amazon.com/cli/latest/reference/s3api/create-bucket.html), [create-key](https://docs.aws.amazon.com/cli/latest/reference/kms/create-key.html).

## 4. Offline source packaging — Platform release owner

After missing services/regional wiring and accepted EventStore source/package integration are complete, use a fresh external output and exact reviewed source root. These are the current supported component packaging commands. No image is built/pushed by `prepare-image.py`; the reviewed inventory must include every published file and the immutable ASP.NET image pin. Do not reuse the old 41-file inventory as though it has that pin.

```bash
set -euo pipefail
: "${REVIEWED_EVENTSTORE_ROOT:?D7 absolute reviewed accepted source checkout}"
: "${INSTALLATION_STAGE:?fresh external directory outside source checkouts and existing journals}"
: "${PUBLISH_INVENTORY:?D6 closed runtimeImage/files inventory of actual publish output}"
: "${PUBLISH_INVENTORY_SHA256:?D6 independently reviewed actual inventory hash}"
: "${ASPNET_IMAGE:?D6 reviewed registry/image@sha256 digest}"
test ! -e "$INSTALLATION_STAGE"
mkdir -m 0700 "$INSTALLATION_STAGE"
dotnet publish infrastructure/Hexalith.Platform.Custody.Dapr/Hexalith.Platform.Custody.Dapr.csproj \
  -c Release -p:UseHexalithProjectReferences=true "-p:HexalithEventStoreRoot=$REVIEWED_EVENTSTORE_ROOT" \
  --artifacts-path "$INSTALLATION_STAGE/artifacts" -o "$INSTALLATION_STAGE/publish" -m:1
python3 eng/ext-secrets-1/custody-deployment/prepare-image.py \
  --publish "$INSTALLATION_STAGE/publish" --inventory "$PUBLISH_INVENTORY" \
  --inventory-sha256 "$PUBLISH_INVENTORY_SHA256" --runtime-image "$ASPNET_IMAGE" \
  --output "$INSTALLATION_STAGE/component-image-context"
```

This source-reference publish is component packaging evidence. Release owner must also retain an actual successful locked **package** lane against the accepted EventStore package; sibling source success cannot replace it. Application/authorizer/control/observer/role images require their real owning implementations/composition and complete inventories, not this component's Dockerfile.

Do not run the following until E returns the **real** independently enrolled component profile; `--check-profile` only parses/validates the supplied profile and prints its computed digest. It does not contact native services, enroll or qualify anything:

```bash
set -euo pipefail
: "${ENROLLED_COMPONENT_PROFILE:?D2/D4 exact independently enrolled file}"
dotnet "$INSTALLATION_STAGE/publish/Hexalith.Platform.Custody.Dapr.dll" --check-profile "$ENROLLED_COMPONENT_PROFILE"
```

## 5. Rendering and installation — Kubernetes/identity owners, later authorization only

There is no complete installer in the repository. `preflight.py` deliberately does not render manifests. Use the reviewed templates as source, with an **explicit** variable allow-list and all referenced artifacts/identities returned first. Platform maintainer must finish receiving services, original stores and private DI; infrastructure maintainer must add explicit signer-key mounts, node placement, complete network rules, correct per-cluster Dapr identity/discovery and regional/native profile wiring. Do not blindly `envsubst` and apply the current templates.

The installation output contract is a new external reviewed directory `REVIEWED_INSTALL_BUNDLE` with:

- `SHA256SUMS` approved through the independent release/trust process.
- `app/`, `authority/`, `observer/`, `enrollment/`: fully rendered Kubernetes resources, explicit node/cluster placement, appIds/SAs/ACLs/network policies, actual digest-pinned images and closed production admission.
- Public profiles/trust/enrollment/placement manifests and public original-store resource references; private credential Secret **references** only. Credential owners install sealed credentials privately before deployment, with no key values in this directory.
- `suspended-worker.yaml`: `suspend:true`, no retries, exact retained output store/group/ownership. No signed worker packet or command is launched by installation.

This directory is a **required future build output**, not an existing artifact. Its rendering/credential provisioning cannot be completed until D1–D7 are supplied. After owners review exact files and separately authorize installation, the following are concrete commands; `apply` itself does not grant qualification permission:

```bash
set -euo pipefail
: "${REVIEWED_INSTALL_BUNDLE:?D5/D6 reviewed actual rendered resource bundle}"
: "${APP_DEPLOY_KUBECONFIG:?new scoped APP deploy credential reference; never app runtime config}"
: "${APP_DEPLOY_CONTEXT:?D4 approved APP context for 192.168.1.30}"
: "${AUTHORITY_DEPLOY_KUBECONFIG:?D4 independent authority cluster credential reference}"
: "${AUTHORITY_DEPLOY_CONTEXT:?D4 exact authority context}"
: "${OBSERVER_DEPLOY_KUBECONFIG:?D4 independent observer cluster credential reference}"
: "${OBSERVER_DEPLOY_CONTEXT:?D4 exact observer context}"
: "${ENROLLMENT_DEPLOY_KUBECONFIG:?D4 independent enrollment cluster credential reference}"
: "${ENROLLMENT_DEPLOY_CONTEXT:?D4 exact enrollment context}"
(cd "$REVIEWED_INSTALL_BUNDLE" && sha256sum --check SHA256SUMS)
kubectl --kubeconfig "$AUTHORITY_DEPLOY_KUBECONFIG" --context "$AUTHORITY_DEPLOY_CONTEXT" apply -f "$REVIEWED_INSTALL_BUNDLE/authority/"
kubectl --kubeconfig "$OBSERVER_DEPLOY_KUBECONFIG" --context "$OBSERVER_DEPLOY_CONTEXT" apply -f "$REVIEWED_INSTALL_BUNDLE/observer/"
kubectl --kubeconfig "$ENROLLMENT_DEPLOY_KUBECONFIG" --context "$ENROLLMENT_DEPLOY_CONTEXT" apply -f "$REVIEWED_INSTALL_BUNDLE/enrollment/"
kubectl --kubeconfig "$APP_DEPLOY_KUBECONFIG" --context "$APP_DEPLOY_CONTEXT" apply -f "$REVIEWED_INSTALL_BUNDLE/app/"
```

Server-side dry-run is also a later cluster operation; the present task did not submit dry-runs, execute admission webhooks or install anything. Install the suspended worker only after its actual retained output/credential/approval controls are verified; never unsuspend it here. Fail-closed startup must remain real: do not inject a successful canary or bypass FR-34 to make a rollout Ready.

## 6. Read-only post-installation verification — independent observer

Use separately scoped audit contexts in each A/R/E cluster; repeat for each actual namespace/role and retain the exact target/context. Example for the proposed executor (no business endpoint invoked):

```bash
set -euo pipefail
: "${AUTHORITY_AUDIT_KUBECONFIG:?D4 read-only independent authority access}"
: "${AUTHORITY_AUDIT_CONTEXT:?D4 exact independent authority context}"
kubectl --kubeconfig "$AUTHORITY_AUDIT_KUBECONFIG" --context "$AUTHORITY_AUDIT_CONTEXT" --request-timeout=20s get deployment,serviceaccount,role,rolebinding,networkpolicy,service -n ext-secrets-1-executor
kubectl --kubeconfig "$AUTHORITY_AUDIT_KUBECONFIG" --context "$AUTHORITY_AUDIT_CONTEXT" --request-timeout=20s get pods -n ext-secrets-1-executor -o 'custom-columns=NAME:.metadata.name,UID:.metadata.uid,SA:.spec.serviceAccountName,NODE:.spec.nodeName,IMAGEID:.status.containerStatuses[*].imageID'
kubectl --kubeconfig "$AUTHORITY_AUDIT_KUBECONFIG" --context "$AUTHORITY_AUDIT_CONTEXT" --request-timeout=20s get configuration.dapr.io -n ext-secrets-1-executor -o 'custom-columns=NAME:.metadata.name,TRUST:.spec.accessControl.trustDomain,DEFAULT:.spec.accessControl.defaultAction'
```

Compare actual imageIDs to approved manifest digests and signed installed file inventories. Kubernetes image metadata alone is not an engine binary hash or proof of production DI. Storage/OS/security owners must provide independent measurements of actual mounts/ownership, swap/core/memory capture/hibernation, component/sidecar/SDK/UDS/gRPC/TLS copy admissions/completions, and restoration exclusions. Metadata cannot prove those facts. Verify actual current public trust/revocation and ACL permission sets; focused later negative scope tests must use approved read-only/zero-effect owner fixtures and cannot call the qualification executor/enrollment endpoints during installation.

For public role anchors on the independently supplied evidence workstation, repeat for all roles and compare against E's signed manifest (and ensure decoded keys are distinct):

```bash
set -euo pipefail
: "${PUBLIC_ROLE_ANCHOR:?D4 actual public-only P-256 PEM file}"
sha256sum "$PUBLIC_ROLE_ANCHOR"
openssl pkey -pubin -in "$PUBLIC_ROLE_ANCHOR" -pubout -outform DER | sha256sum
openssl pkey -pubin -in "$PUBLIC_ROLE_ANCHOR" -text -noout
```

This checks bytes/curve; E's separate provenance/current authorization checks remain mandatory. Never generate replacement keys merely to fill missing paths.

## 7. Detached replacement preparation — after complete delivery, never qualification

Current scripts still contain literal missed-date/implementation blockers. Complete the planned accepted date/hash and joint-protocol changes first; these are the existing CLI argument shapes, not a claim that the present source can emit complete packets. Supply one clean canonical Platform checkout and fresh external destinations; retain plan/configuration serialization rules from each verifier. Hash raw profile files separately where required. No emitted `exact-command.sh` is executed here.

```bash
set -euo pipefail
: "${CLEAN_PLATFORM_ROOT:?D9 exact complete clean canonical checkout}"
: "${INSTALLED_RUNTIME_REFERENCE:?D4/D5 complete public installed runtime reference}"
: "${INSTALLED_HOST_PROFILE:?D8 complete actual host profile}"
: "${DETACHED_SECRETS_PACKET:?fresh external output directory}"
: "${DETACHED_HOST_PACKET:?fresh external output directory}"
: "${FUTURE_SECRETS_ARTIFACTS:?fresh future qualification artifacts path; do not create here}"
: "${FUTURE_SECRETS_EVIDENCE:?future accepted evidence root preserving original identity; do not create here}"
python3 "$CLEAN_PLATFORM_ROOT/eng/ext-secrets-1/prepare_acceptance.py" \
  --root "$CLEAN_PLATFORM_ROOT" --configuration "$INSTALLED_RUNTIME_REFERENCE" \
  --output "$DETACHED_SECRETS_PACKET" --eventstore-argument ../eventstore \
  --artifacts-argument "$FUTURE_SECRETS_ARTIFACTS" --evidence-argument "$FUTURE_SECRETS_EVIDENCE" \
  --register /home/administrator/projects/hexalith/agents/_bmad-output/planning-artifacts/external-dependency-register.md
python3 "$CLEAN_PLATFORM_ROOT/eng/ext-host-1/prepare_acceptance.py" \
  --root "$CLEAN_PLATFORM_ROOT" --profile "$INSTALLED_HOST_PROFILE" --output "$DETACHED_HOST_PACKET"
```

Check both nine-field packets pin the same full Platform commit, explicit owner-confirmed dates and the same installed acceptance set, with exact profile/config/plan/public-anchor/image/binary hashes. EventStore's full installed profile and immutable package/source target are a prerequisite. Current preparations keep availability false and candidate acceptance unset; no accepted register row or historical packet is overwritten. The future joint runner's CLI is not yet implemented, so no invented invocation or reservation command is supplied.
