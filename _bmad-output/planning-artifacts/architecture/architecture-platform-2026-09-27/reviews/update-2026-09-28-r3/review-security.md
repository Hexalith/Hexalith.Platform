# Security architecture review: update run 3

**Verdict: needs revision (targeted).** SEC-1 is closed. SEC-2 is closed for Keycloak group membership. One high residual remains: the enforcement copy that the gateway and asynchronous re-checks actually read, EventStore's admission projection, has no recovery rule. Several medium gaps also remain around admission-record integrity, the recovery-hook principal and takeover revocation. This review checks the target contract only. It makes no claim about deployed code.

Scope: I read the complete `ARCHITECTURE-SPINE.md` (485 lines, draft, 2026-09-28), this run's `spine.diff`, the prior `review-security.md` and `validation-report.md`, and the memlog from "Update run 3 started" onward, together with D6 and D8. I checked the PRD's FR-11, FR-12, NFR-3 and DR clauses. I did not report accepted risks: the single node, the two named writers, plaintext Redis and FalkorDB in the data namespace, module-owned lost-RPO revocations, and GitHub Free. I also did not report implementation that Owned work already gates.

## Closure of prior findings

| Prior | Status | Evidence |
| --- | --- | --- |
| SEC-1 / VAL-07: audience accepted as surface | **Closed** | `:202` removes the audience-only alternative ("Neither the target audience nor caller-set headers or parameters establish the surface") and requires audience **and** `azp`, plus the attested originating surface on chains. `:206` adds the negative test with the correct confidential target audience from an `agent` or `service` origin. A scope residual is raised separately as SEC3-8. |
| SEC-2 / VAL-08: lost admission revocation resurrected by DR | **Closed for Keycloak membership** | `:118` makes every production human and synthetic grant or revocation, including the G1 grant, a signed Administrator record written before the Keycloak change, and makes these records the recovery authority. `:119` and `:319` reconcile restored membership to the records. `:251` covers a whole-server restore. `:476` qualifies "a revocation lost before event export". The residuals are SEC3-1 (the EventStore projection), SEC3-2 (record completeness) and SEC3-6 (non-admission realm revocations). |

## Findings

### SEC3-1: The admission projection that enforces admission has no recovery rule and is consulted before reconciliation

**Severity:** high. **Action:** autofix. **Confidence:** medium.

**Evidence:** `:205` (asynchronous steps re-check "EventStore's admission projection"); `:441` (projection "fed from the realm admin-event export"); memlog D6 (the gateway **and** every asynchronous re-check read it); `:118`/`:319` (reconciliation acts on Keycloak membership only); `:318` (step 4 "catch subscribers up from restored checkpoints" happens before step 5's reconciliation); `:315` (the event-export sink is fenced in step 1 and its credentials are reissued only in step 5); `:281` (in-place data restore restores EventStore data but no Keycloak); `:320` ("denial of revoked principals", with no stated population); `:313` (recovery mode).

**Concrete failure:**
1. The Administrator revokes user U after the cut, with a record, in Keycloak, and exported.
2. An in-place data restore after a failed approved release rewinds EventStore state, including the admission projection, to the cut. Keycloak stays live and correct.
3. The admin-event deliveries were already acknowledged, and in place the broker is not restored, so the rewound projection never receives the revocation again.
4. The gateway and the asynchronous re-checks read that projection and admit U.

In DR the same thing happens. Step 4's subscriber catch-up can run U's pending asynchronous steps against the cut-time projection before step 5 reconciles membership. Step 5 cannot help: the projection's export feed is fenced until step 5 reissues credentials. The AD-6 records fix Keycloak, but they do not fix the copy that enforces admission. Recovery mode's "only recovery-scope tasks mutate state" conflicts with step 4's catch-up, so the protection depends on which of the two clauses an implementer follows.

**Minimal correction (spine wording):** add this to the Admission predicate row (`:441`) or DR step 5: "EventStore's admission projection is live-authority-only. It is never restored as authority. After any data restore it reports unknown, and therefore fails closed, until it has been rebuilt from the realm after step 5's admission reconciliation (in place: from the live realm). No asynchronous step with a user actor runs before then." Reword step 6 as: "denial, at the gateway and through an asynchronous re-check, of every principal whose latest admission record is a revocation."

### SEC3-2: Signed admission records do not prove that the record set is complete

**Severity:** medium. **Action:** autofix. **Confidence:** high.

**Evidence:** `:118` (records live in the attempt, lock and stop store); `:225` (records are accepted when signed, but only attempt records are "immutable once terminal"); `:131` (the recovery executor has standing write access to the store); `:225`/`:287` (every executor and the monitor write stop records to the same store); `:319` ("removing every principal without a current grant record or with a later revocation").

**Concrete failure:** A signature proves that a record is authentic. It does not prove that no record is missing. Any store writer can delete or withhold a signed revocation record: a compromised production executor job, the recovery executor, or the monitor host. The next DR then finds U's grant record with no later revocation, keeps U in the restored group, and reopens with U admitted. Separately, nothing sets how long records are kept. If grant records are pruned on the AD-2 artifact schedule, DR removes every long-standing member. That failure is fail-closed, but it is not specified.

**Minimal correction:** add this to AD-6 Admission records: "Admission records are create-only for every store writer. Each names its predecessor's digest, and each is retained while current and for at least the backup retention period. Reconciliation stops for intervention on a chain gap." This matches the registry rule "create without update or delete" at `:231`.

### SEC3-3: Admission records and Keycloak can diverge undetected between recoveries

**Severity:** medium. **Action:** autofix. **Confidence:** high.

**Evidence:** `:118` (record first, then Keycloak, by process); `:119` (reconciliation only in recovery); `:235` (the monitor already checks event export); `:316`/`:319` (DR-scoped realm rights held by the deputy and the recovery executor); PRD FR-11 (admission granted only by Administrator through "an authenticated, auditable action").

**Concrete failure:** There are two cases.
- **An unrecorded grant.** Someone adds a user straight to the human production-admission group without a record. That someone could be a stolen Keycloak-admin session, or DR-scoped rights used after step 5. The user stays admitted until the next DR, which then removes the user silently, and nothing flags the unauthorized grant.
- **An unrecorded revocation.** The Administrator revokes in the Keycloak console but skips the record. Keycloak denies the user correctly today. After a later DR from a pre-revocation point, the grant record still stands and the user is readmitted.

**Minimal correction:** add this to Diagnostics and notification: "The off-site monitor matches every exported change to an admission group against a signed admission record, and notifies and sets the promotion stop on any unmatched change." Add this to DR step 6: "the admission groups hold exactly the principals with a current grant record."

### SEC3-4: The recovery-hook principal is not bound to recovery-kind attempts, recovery mode or an issuer-attested epoch

**Severity:** medium. **Action:** autofix. **Confidence:** medium.

**Evidence:** `:128` ("for a recovery attempt it owns, the environment's recovery-hook principal — through GitHub OIDC claim checks or executor-held credentials"); `:318` (an endpoint that "admits only that environment's recovery-hook principal at the current epoch"); `:283` (release-kind and recovery-kind attempts; automatic recovery and baseline re-deploy are also called recovery); `:138` (the deploy identity holds no data-namespace authority, while hooks purge and rotate backend principals); `:319` (restored credentials and realm signing keys are rotated only in step 5, after the hooks run).

**Concrete failure:**
- **Release jobs can hold the principal.** "Recovery attempt" is undefined. An executor can read it to cover a release attempt's automatic recovery or a baseline re-deploy, and a release job also holds the current epoch. So a buggy or compromised production release job with this principal can invoke purge or credential re-provisioning hooks on live production. Those hooks reach data-tier authority that the deploy identity deliberately lacks.
- **The caller may supply the epoch.** "At the current epoch" does not say who attests the epoch. If the caller supplies it, any holder of the standing per-environment principal passes the check. Both the production executor and the recovery executor hold that principal.
- **Compromise-driven DR.** In a compromise-driven DR, step 4 admits hook calls authenticated by restored, not-yet-rotated credentials or realm keys. Network quarantine is then the only barrier.

**Minimal correction:**
- In `:128`, write "for a recovery-kind attempt it owns, a recovery-hook credential that its issuer binds to that attempt and epoch".
- In `:318`, write "a declared internal endpoint, served only while the workload runs in recovery mode, that admits only that environment's recovery-hook principal with a credential issued for the current recovery-kind attempt and epoch; in DR the issuer is created on the replacement capacity, never from restored credentials or signing keys".

The sandbox interaction holds: module code never sees job credentials (`:129`).

### SEC3-5: Revoke-and-drain takeover does not stop the superseded job from getting new credentials

**Severity:** medium. **Action:** autofix. **Confidence:** medium.

**Evidence:** `:282` ("revokes the previous job's per-job credentials at their issuer, waits the declared maximum in-flight request duration"); `:128` (per-job credentials are obtained "through GitHub OIDC claim checks or executor-held credentials"); `:131` (the recovery executor's standing credentials).

**Concrete failure:** A stale release job and the Administrator-started in-place recovery run on the same production executor. The new owner deletes the old job's Secret-bound tokens (the memlog's example mechanism) and waits out the drain. The stale job's Kubernetes client then re-mints a token from the executor-held bootstrap credential; exec-credential plugins do this automatically. Its delayed write commits after the new owner has verified the cluster. This violates "no older-epoch mutation … may commit". The same gap applies to standing credentials that both jobs share, such as a replacement recovery-executor job's credentials for prepared capacity. Revoking those would revoke the new owner as well.

**Minimal correction:** in Attempt ownership, write "…or revokes the previous job's per-job credentials at their issuer and ensures the issuer grants that job no new ones (issuance conditioned on the current epoch, or the job terminated), waits…; a credential both jobs share never counts as revoked."

### SEC3-6: Realm revocations other than admission are outside both the records and the accepted lost window

**Severity:** medium. **Action:** discuss. **Confidence:** medium.

**Evidence:** `:118` (records cover admission only); `:119`/`:319` (re-apply "admin and user revocations recorded after the cut", which come from the export); `:290` (export lag within a bound); `:294` and `:485` (the accepted exception names *module-owned* revocations); `:115` (modules declare roles held in Keycloak); PRD line 226 ("reapply post-cut revocations … denial of revoked principals").

**Concrete failure:** The Administrator contains a compromised account by disabling the user, removing a module realm role or resetting credentials, rather than by revoking admission. The server fails inside the export lag. DR restores the pre-change realm, and the user is enabled, keeps the role and is still admitted, because the grant record stands. That realm-held removal is not an admission record, and it is arguably not a "module-owned" revocation, so neither the reconciliation nor the accepted risk covers it.

**Minimal correction:** there are two options.
- **(a) Recommended, minimal.** Add to AD-6: "A removal of a human's production access that must survive DR is made as an admission revocation record." Add "realm access removals other than admission after the durable event-export frontier" to the Lost window row and to Accepted risks.
- **(b)** Extend record-first to every production realm change that removes access.

### SEC3-7: A caller-supplied exclusion marker is honored outside the staging synthetic clause

**Severity:** low. **Action:** autofix. **Confidence:** medium.

**Evidence:** `:234` (tenants "carrying the synthetic exclusion marker"; "modules exclude that tenant from real-tenant views and aggregates"; "the marker alone never admits").

**Concrete failure:** The staging admission clause itself is sound. The marker never admits, and later operations bind to the synthetic client that EventStore attests created the tenant, so real tenants stay unreachable. The marker's *exclusion* effect, however, has no binding. An admitted production user, or an agent if tenant creation is agent-eligible, can create a tenant carrying the marker, or set the marker on an existing tenant. That hides the tenant from real-tenant views and aggregates, including any oversight views built on them.

**Minimal correction:** add to Synthetic identities: "EventStore accepts the synthetic exclusion marker only on a tenant-creation command from a synthetic client in staging or a run-owned environment, and rejects it elsewhere. The marker is immutable, and modules exclude a marked tenant only when its EventStore-attested creation names a synthetic client."

### SEC3-8: The AD-14 audience and client rule is scoped to externally reachable hosts only

**Severity:** medium. **Action:** autofix. **Confidence:** medium.

**Evidence:** `:202` ("Every externally reachable host that accepts bearer tokens validates its own audience…"); `:205` (exchange "permitted per requester and target-audience pair and downscoped to the target's declared operations"); `:233` (internal-only interfaces are not routed); Module declaration `:222` (internal-only exposure, inbound callers).

**Concrete failure:** Cross-module steps reach internal-only interfaces through Dapr service invocation, carrying exchanged bearer tokens. The team that owns an internal-only target reads `:202` literally and skips audience validation. Module A's service client is realm-permitted to exchange only for module C. It replays its C-audience token to internal module B, which Dapr ACLs allow A to call. B accepts it, and A acts at B for the user without the requester-target exchange permission. That bypasses the downscoping matrix, which is the same class of gap as SEC-1.

**Minimal correction:** in `:202`, write "Every host that accepts bearer tokens, including internal-only interfaces reached through Dapr service invocation, validates its own audience…". Add to `:206`: "…and a token exchanged for another target's audience, replayed to an internal host."

### SEC3-9: Nothing enforces that the lifecycle clause stays staging-only when the same image runs in production

**Severity:** low. **Action:** autofix. **Confidence:** medium.

**Evidence:** `:234` ("the production predicate has no such clause"); `:441` (an EventStore-owned predicate); AD-2 `:80` (production runs the same composed image and package as staging).

**Concrete failure:** The predicate ships in the one promoted `platform/eventstore` image, so "staging only" can only be an environment-bound switch. Nothing names that switch, prevents it being set in production, or tests that it is off. If staging values are copied, production synthetic clients held on the production executor can create and operate marked tenants in production. Production smoke suites are forbidden to do that, and population-creating tasks there are operator-only (`:223`).

**Minimal correction:** add to Synthetic identities: "The lifecycle clause is enabled only by a staging realm-contract instance value. The realm-contract validator rejects it in the production instance, and production SM-4 proves that a synthetic client's marker-carrying tenant creation is denied."

### SEC3-10: The G1 temporary grant has no bound and no G2 check that it was revoked

**Severity:** low. **Action:** autofix. **Confidence:** high.

**Evidence:** `:288` (a temporary grant into the *human* group; "its revocation followed by a denial check"; G2 criteria); `:118` (the grant is recorded); PRD line 56 ("the test grant is revoked after the check").

**Concrete failure:** The grant is recorded correctly, and DR covers it. But nothing time-bounds it, and G2 does not require its revocation. If it is forgotten, G2 opens ingress with a synthetic identity that still has human-group admission, now usable from any source. Its permissions are limited to the synthetic tenant.

**Minimal correction:** add to G1: "the grant record carries an expiry no later than G2." Add to G2: "the temporary grant's revocation record and denial check exist."

## What holds

- **AD-14:** the surface classes, the most-restrictive chain rule, the `service` fallback for chains that start outside EventStore admission, the ban on raw token forwarding, and the rule that `ui` tokens never leave their server all hold. Together they stop an agent origin being laundered into `ui` through an exchange.
- **Admission records:** Administrator-only signing (`:225`), record-first ordering, removal-only reconciliation that the deputy can perform, and explicit coverage of the G1 grant. A failed Keycloak apply after a revocation record is caught by the AD-8 check "revoked principals after every admission change" (`:143`).
- **Staging clause:** real tenants cannot be reached through it, because later operations bind to the EventStore-attested creating synthetic client, not to the marker. The memlog records that no run binding is an accepted choice.
- **Module-code sandbox (`:129`):** it keeps job credentials, including the recovery-hook principal, away from smoke, E2E and fence-hook code. Fence hooks receive only authority that a custodian releases for a single authority.
- **Recovery-mode quarantine:** admitting only the owning executor and the recovery workloads, with user ingress closed and external-effect workers disabled, is a sound boundary. The in-place quiesce also admits probe sources (`:281`) where recovery mode does not (`:313`). The probe is harmless, so I noted this without raising a finding.
- **Promotion-stop clears:** the revision-conditioned clear (`:287`) closes VAL-04 without adding new authority.

**Counts:** 0 critical, 1 high, 6 medium, 3 low.
