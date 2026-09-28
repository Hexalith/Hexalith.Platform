# Security architecture validation

**Verdict: needs revision.** Two high-severity contract gaps remain: a bearer-token audience can substitute for authenticated surface identity, and disaster recovery lacks a rule for admission revocations lost inside the identity-event export window. This is validation of the target contract, not a claim that either exploit exists in deployed code.

Reviewed the complete current `ARCHITECTURE-SPINE.md` (481 lines, updated 2026-09-28). Findings below concern independent implementations that the present wording permits. No source artifact, runtime, credential, or infrastructure setting was changed.

## SEC-1 — Confidential target audience is not proof of the calling surface

**Severity:** high. **Action:** autofix. **Confidence:** high.

**Evidence:** `ARCHITECTURE-SPINE.md:199`, `:200`, `:201`, `:203`, `:204`, `:438`.

AD-14's surface rule gives bearer-token hosts two alternatives: derive the surface from authenticated `azp`, or accept tokens audienced to their own confidential client. The second alternative does not bind the caller to the `ui` class or preserve an attested agent origin. A confidential client's identity as a resource audience says nothing about which client obtained the token.

**Independent-unit failure:** the realm team permits a service requester to target a confidential module client for an agent-eligible read. The module host team implements the allowed audience-only alternative and treats its own confidential-client endpoint as a UI surface. A token with `aud=module-ui`, `azp=module-service`, and an agent originating context can consequently reach a UI-only operation unless that host independently invents the missing origin check. The allowed audience mapping is not itself excessive: it is needed for the legitimate read. The failure is using that same audience as sufficient evidence of UI origin.

Keycloak's official standard-exchange documentation shows this distinction directly: the requester remains in `azp` while the target appears in `aud`. The chain and negative-test clauses express the desired denial, but the explicit alternative in the surface rule permits an incompatible host implementation. [Keycloak token exchange documentation](https://www.keycloak.org/securing-apps/token-exchange).

**Impact:** the API and realm teams can implement different interpretations of the same contract, weakening the intended protection for UI-only and confirmation-required operations.

**Recommendation:** require audience validation **and** authenticated client-to-surface validation for every bearer endpoint. For exchanged or asynchronous requests, also require the attested effective originating surface and confirmation proof where applicable. A confidential UI can use a server-side authenticated session, but its resource audience alone must never establish the caller's surface. Add a negative case with the correct target audience and an agent/service origin so the alternative cannot survive as a permissive implementation.

## SEC-2 — Recovery can resurrect admission revoked inside the event-export lag

**Severity:** high. **Action:** discuss. **Confidence:** high.

**Evidence:** `ARCHITECTURE-SPINE.md:117`, `:203`, `:247`, `:286`, `:290`, `:312`, `:315`, `:316`, `:437`, `:481`.

Admin and user events are exported off-cluster within a declared bound, and recovery-point validity accepts export lag within that bound. Recovery then restores the older Keycloak database and replays recorded revocations. The accepted lost-window exception covers module-owned authorization revocations; it does not authorize losing Keycloak production-admission revocations. No rule specifies what recovery must do when acknowledged identity changes occurred after the durable export frontier and their primary record is lost.

**Independent-unit failure:** a user belongs to production admission at the recovery cut. Administrator subsequently removes that membership; the primary server fails before the bounded exporter persists the event. The restore team correctly restores the database and replays every surviving event, while the admission-projection team correctly rebuilds its projection from that restored realm. The user is admitted again, and neither team has a record identifying the missing revocation. Rotating signing keys or credentials does not repair this authorization state: an already admitted asynchronous task can resume against the newly rebuilt but resurrected admission projection without the user logging in again.

**Impact:** DR can reopen with a previously revoked actor admitted, contrary to AD-6's no-new-admission recovery boundary and the required denial of revoked principals. Checking the exporter against its allowed lag or testing all *known* revoked principals does not detect the missing event.

**Recommendation:** bind one recovery-safe authority for admission revocations. Options include acknowledging changes only after an independent durable revocation record, or keeping restored human admission fail-closed until Administrator reconciles and explicitly reauthorizes it when the export frontier may be incomplete. If a bounded loss of identity revocations is intended instead, it needs an explicit accepted-risk decision and corresponding recovery/denial semantics; it cannot be inferred from the existing module-owned exception. Exercise a failure immediately after membership revocation and before event export in the recovery qualification.

## Coverage and exclusions

The review covered authenticated surface and actor attribution; declaration-derived routes and authority; environment, namespace, Dapr, secret and executor trust boundaries; retained artifact provenance and signing authorities; operator and deputy permissions; backup, key and revocation continuity; and quarantine/reopening rules.

The spine already assigns meaningful enforcement boundaries for module declarations, provenance writers, immutable retained digests, tenant-key custody, secret rotation, private executor authority and recovery fencing. Their explicitly assigned implementation/qualification gates are not reported as absent architecture. In particular, the accepted two-writer operations repository, single node, plaintext Redis/FalkorDB inside the data namespace, and stated module-owned lost-RPO exceptions are respected. No additional finding is raised merely because current deployed infrastructure has not passed those gates.

The epoch takeover, promotion-stop clear race, synthetic tenant lifecycle mismatch and executor reachability for in-place recovery are intentionally omitted because other independent lenses cover them.

**Counts:** 0 critical, 2 high, 0 medium, 0 low. No additional speculative findings.
