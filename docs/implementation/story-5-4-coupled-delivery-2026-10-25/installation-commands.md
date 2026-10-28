# Non-AWS MVP installation command plan

## MVP work-scope amendment — 2026-10-28

The owner approved the [pragmatic MVP scope](../../../../../_bmad-output/planning-artifacts/sprint-change-proposal-2026-10-28-mvp-custody.md). **Advanced Luna/authority/WORM provisioning and full lifecycle installation are deferred from MVP work.** The conditional commands below remain the historical full-delivery runbook; they are not an MVP execution checklist. Existing operational service/profile/access maintenance uses its own accepted procedure and preservation prerequisites; do not replay a completed installer. No command in this runbook is executed by the amendment. Full dependency availability, independent decision authority, current Human/custody, FR-34 and qualification gates remain enforced; the amendment introduces no reduced live command. See [current owner scope](owner-decisions.json).


The owner has excluded AWS from MVP. The prior cloud resource commands and AWS-reference component packaging have been removed from this active runbook; original bytes are retained only in the [superseded snapshot index](installation-evidence/superseded-aws-proposal/snapshot-index.json).

This is a conditional runbook, not a script to execute as a whole. This correction executes **no cluster/provider/build/installation/qualification command**. Preserve host 192.168.1.30, timing, all originals/journals, OQ-5.4-1 and dependency availability. Variables require exact D1–D9 returns; private values stay with their custodians.

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

## 2. Select and package the actual non-AWS implementation — Platform infrastructure/release owners

The existing `infrastructure/Hexalith.Platform.Custody.Dapr` executable/profile, Dockerfile and image preparer instantiate/package the AWS reference. They cannot be used as a non-AWS MVP merely by changing configuration. Their historical input preflight is not the MVP provisioning gate.

D1/D2/D5 must return the actual non-AWS provider/version, resource inventory, independent custody/current-authority/archive placements and scope, implementation project/artifact, exact deployment recipe and backend-specific read-only verification argv. If no qualifying existing installation is supplied, the owning implementation must first be built behind the existing provider-neutral Dapr semantic contract. No generic secret-delete or storage-restore command can supply the missing physical guarantee.

For a selected, implemented **.NET** component, the supported SDK packaging shape is below. `MVP_COMPONENT_PROJECT` must point at the actual reviewed non-AWS project returned by its maintainer. No such non-AWS executable is claimed to exist in the current source. Keep any required accepted EventStore graph/locked package restore with the owning release evidence; never substitute sibling source success for an accepted package.

```bash
set -euo pipefail
: "${MVP_COMPONENT_PROJECT:?D5 actual reviewed non-AWS .NET project path}"
: "${INSTALLATION_STAGE:?fresh external directory outside checkouts and existing journals}"
test -f "$MVP_COMPONENT_PROJECT"
test ! -e "$INSTALLATION_STAGE"
mkdir -m 0700 "$INSTALLATION_STAGE"
dotnet publish "$MVP_COMPONENT_PROJECT" -c Release   --artifacts-path "$INSTALLATION_STAGE/artifacts" -o "$INSTALLATION_STAGE/publish" -m:1
python3 - "$INSTALLATION_STAGE/publish" "$INSTALLATION_STAGE/publish-files.json" <<'PYFILES'
from pathlib import Path
import hashlib,json,sys
root=Path(sys.argv[1]); output=Path(sys.argv[2])
files={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(root.rglob('*')) if p.is_file()}
if not files: raise SystemExit('Empty publish output')
with output.open('x') as stream: json.dump(files,stream,indent=2);stream.write('\n')
PYFILES
```

That command creates a file inventory, not an installed hash or independent approval. Release owner must supply the actual digest-pinned runtime/container recipe, actual component entrypoint/arguments and closed profile validation command; do not reuse reference `--serve`/`--worker`/`--check-profile` flags unless the selected implementation really implements them. Use the actual reviewed non-AWS package pipeline if it is not a .NET component. No AWS account/region/ARN/key policy/shared-credential profile or cloud creation command is an MVP prerequisite.

Provider infrastructure owner returns exact installation commands for its actual backend/appliance/store **before** operational authorization. Required public command record: executable/version/hash, complete argv, target resource/identity, expected result and independent verification, side effects/cost/retention, and original outcome/recovery after uncertainty. Missing provider commands remain an explicit D2/D5 input; no device, HSM or alternative cloud is fabricated.

## 3. Render and install — independent platform/identity/storage owners, later authorization only

Keep existing operational OpenBao workloads and HMAC binding. New semantic custody currentness and physical proof are separate obligations. Actual external authority/archive/observer/enrollment targets must exist outside APP restore/admin control and have reviewed Dapr routing/trust, storage, source and role providers before installation.

`REVIEWED_INSTALL_BUNDLE` is a **future output** with actual non-AWS receiving/component/authority/role images and profiles, separate SAs/ACLs/current scopes, complete network rules/node placement, original stores and independently supplied public enrollment manifests. Its `app/`, `authority/`, `observer/`, `enrollment/` resource directories and approved `SHA256SUMS` must be reviewed before applying. Private credentials are installed privately through their owners; the bundle contains references only.

The existing templates are scaffolding. Complete provider-specific credential/UDS/signer mounts, receiving composition and current role/original providers; remove reference-backend startup/environment assumptions. `preflight.py` does not render a complete installer. Never blindly substitute and apply reference templates. Keep business admission closed and the recovery worker suspended with no retries, no signed action packet, and independently retained output ownership proven.

The following are the concrete Kubernetes application shapes after actual rendered resources/targets and separate deployment authority exist. They do not create backend hardware/stores or enroll trust by themselves:

```bash
set -euo pipefail
: "${REVIEWED_INSTALL_BUNDLE:?D5/D6 reviewed actual non-AWS resource bundle}"
: "${APP_DEPLOY_KUBECONFIG:?D4 scoped APP deploy credential reference}"
: "${APP_DEPLOY_CONTEXT:?D4 exact context for host 192.168.1.30}"
: "${AUTHORITY_DEPLOY_KUBECONFIG:?D4 independent authority cluster credential reference}"
: "${AUTHORITY_DEPLOY_CONTEXT:?D4 exact independent authority context}"
: "${OBSERVER_DEPLOY_KUBECONFIG:?D4 independent observer cluster credential reference}"
: "${OBSERVER_DEPLOY_CONTEXT:?D4 exact independent observer context}"
: "${ENROLLMENT_DEPLOY_KUBECONFIG:?D4 independent enrollment cluster credential reference}"
: "${ENROLLMENT_DEPLOY_CONTEXT:?D4 exact independent enrollment context}"
(cd "$REVIEWED_INSTALL_BUNDLE" && sha256sum --check SHA256SUMS)
kubectl --kubeconfig "$AUTHORITY_DEPLOY_KUBECONFIG" --context "$AUTHORITY_DEPLOY_CONTEXT" apply -f "$REVIEWED_INSTALL_BUNDLE/authority/"
kubectl --kubeconfig "$OBSERVER_DEPLOY_KUBECONFIG" --context "$OBSERVER_DEPLOY_CONTEXT" apply -f "$REVIEWED_INSTALL_BUNDLE/observer/"
kubectl --kubeconfig "$ENROLLMENT_DEPLOY_KUBECONFIG" --context "$ENROLLMENT_DEPLOY_CONTEXT" apply -f "$REVIEWED_INSTALL_BUNDLE/enrollment/"
kubectl --kubeconfig "$APP_DEPLOY_KUBECONFIG" --context "$APP_DEPLOY_CONTEXT" apply -f "$REVIEWED_INSTALL_BUNDLE/app/"
```

These commands require actual Kubernetes targets. If an existing independent non-AWS service/host is chosen instead, D2/D4 must return its supported installation/verification commands and accepted Dapr connectivity; do not invent a cluster/kubeconfig. Do not disable real FR-34 gates to make rollout Ready. Installation invokes no `/execute`, qualification `/enroll`, physical worker, Live/Full or cohort phase.

## 4. Read-only post-installation verification — independent observer

Use separately scoped audit contexts in each independent authority/observer/enrollment target; repeat for each actual namespace/role and retain the exact target/context. Example for the proposed executor (no business endpoint invoked):

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

## 5. Detached replacement preparation — after complete delivery, never qualification

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
