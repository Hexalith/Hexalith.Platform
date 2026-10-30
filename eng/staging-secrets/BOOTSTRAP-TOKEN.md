# Existing shared bootstrap token: evidence and replacement proposal

**Preparation only. No token lookup, renewal, issuance or revocation is authorized.**

The architecture's Owned work row (line 502) and Story 4.15 criteria (epics lines
2330–2336; exact source lines and hashes in `source-evidence.json`) name
`openbao-runtime-bootstrap` and assert a `2027-07-19` expiry. That is an owned
planning assertion, not independently measured token metadata. The recorded
native observation proves the presence of an existing shared OpenBao workload
at its dated cut; it cannot prove a token's effective policy, type, lifetime,
renewability, root status, compromise, custody or current validity. No root,
compromise or broad privilege classification is inferred from the word bootstrap.
The retained installation observation
`docs/implementation/story-5-4-coupled-delivery-2026-10-25/installation-evidence/infrastructure-observation.json`
at `/observations/9/selectedPublicMetadata/12` (lines 3296–3342, SHA-256
`9cf7ee66f0f1a045bd67c0f645faf262140b8ba8c4594193789e00ea17ef3e2b`)
records one Vault Component with two app scopes and a `vaultToken`
`secretKeyRef` to `openbao-runtime-bootstrap/token`. This proves shared
references only, not the credential's contents or authority.

Classification remains blocked on `bootstrapToken.inventoryEvidenceRef` and
`classificationDecisionRef`. The issuer/Administrator owner must inventory, in a
separately authorized sanitized review, issuer and token identity/accessor
reference, policies and effective capabilities, token type, issuance/expiry
metadata, renewable state and maximum lifetime, custody/access history and every
consumer pod/Component/bootstrap reference. Retain no token bytes, hashes of token
bytes, unseal shares, secret values or authentication payloads. Reconcile the
inventory with deployment references and issuer history; explicitly report gaps.
The existing shared instance's environment assignment remains unclassified.
Partition every consumer by approved environment and designated instance before
cutover. Never copy historical shared or production secrets, token credentials
or tenant keys into staging. This story prepares staging's own instance only;
other environments' replacements require their own exact authority.
Administrator then records the evidenced classification and any separately
supported incident response. The current proposal makes no incident finding.

The named cutover owner, per-app renewal owners, bounded lifetimes,
lead times, expiry monitor, issuer/revocation authority and exact deadline remain
null. Replacement must finish before the historically asserted 2027-07-19 expiry,
with an earlier owner-selected deadline and failure margin; a future current
lookup may require earlier action. If renewable, the named owner renews outside
Dapr before the approved lead-time boundary; if nonrenewable or at maximum life,
the issuer replaces it. Dapr is never the renewal owner. Prove the named renewal owner can act with least privilege, independently of
the application runtime; record any selected deputy coverage explicitly.

After exact-byte approval and accepted foundation isolation:

1. Confirm the complete consumer inventory and approved environment partition;
   replace credentials only within each app's designated instance under its own
   authority. Record a distinct per-app policy,
   single-app Component scope, identity, per-pod bootstrap delivery reference,
   renewal owner and contract generation requirements. No app inherits an
   undocumented shared grant. Keep dynamic namespace writes with their sole owner.
2. The authorized issuer creates per-app replacements and delivers them through
   the approved bootstrap path. Keep values outside Git and sanitized evidence.
3. Restart every affected pod and Dapr sidecar after delivery. Record delivery
   revision, old/new pod identity, sidecar start ordering and successful reads of
   exactly required generations, bound to staging/app/contract digest. A mounted
   token change cannot update a running sidecar's startup-cached token.
4. Platform and app owners acknowledge complete coverage across replicas and
   consumer inventory. Missing consumers, stale generations, restart failures or
   absent acknowledgment stop cutover and keep readiness refused. Renew the still
   needed old credential only under separate issuer authority; never assume it
   is renewable or leave expiry monitoring unattended.
5. Only after every consumer acknowledges replacements may the separately
   authorized issuer owner revoke the shared token and prove its old access is
   denied. Retain sanitized revocation evidence and independent alert delivery.
   Future per-app rotations use the same delivery/restart/acknowledgment/revoke
   order. Recover forward with fresh per-app credentials; do not restore an old
   shared token from a backup or rewind acknowledged generations.
