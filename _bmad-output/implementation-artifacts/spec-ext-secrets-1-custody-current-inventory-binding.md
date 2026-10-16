---
title: EXT-SECRETS-1 custody and current-inventory binding
type: feature
created: 2026-10-16
status: in-progress
route: dispatch
baseline_commit: b2890d20575356239555df5b113e3100c1d594c1
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EXT-SECRETS-1 stays Committed. Provider, HMAC credential, tenant inventory, current-inventory authority, tenant A/B, profile, and revocation references are null. No `IDaprPlatformHmacAuthority` binding is installed.

**Approach:** Discover the tenant registry and existing OpenBao/Dapr configuration on `192.168.1.30`. Install one isolated provider binding and current inventory from those facts. Record public references only and leave the dependency unavailable.

## Boundaries & Constraints

**Always:** Reuse Dapr 1.18 and `openbao/StatefulSet/hexalith-keys`. Use operator credentials only for provisioning, then a new restricted application credential. Preserve 2026-10-08, target `f448db29493c1b30b0f18380188fe99b8b7606fe`, L/S/O/H/R = 300/30/600/86400/604800, Open export disposition, and decision keys outside Platform and Agents. Bind discovered current facts only. Preserve the unrelated dirty checkout. A missing input keeps this task incomplete and names that input, its owner, and the next action.

**Never:** Invent tenants, mounts, issuers, key versions, anchors, or Available status. Use operator or Memories credentials as application credentials. Change production workloads, keys, data, Memories, or existing OpenBao policies. Apply null proposed manifests. Edit the Agents register. Run Live or any contract case. Write credential values to git, evidence, or logs.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Required behavior | Failure |
| --- | --- | --- | --- |
| Facts present | HMAC mount, TLS name, and two distinct tenants | Install the isolated binding and fill those references | Stop before an invented field |
| Fact absent | Registry, mount, profile, or revocation missing | Leave that reference null and name input, owner, and action | No fixture authority |
| Production overlap | Existing workload, key, or Memories object | Leave it unchanged | No apply |
| Validation | Installed object or missing input | Metadata and health only | Live stays unrun |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Custody/DaprPlatformHmacBinding.cs` — reuse `IDaprPlatformHmacAuthority`. Do not change timing or secret handling.
- `src/Hexalith.Platform.Secrets.Runtime/DaprHmacAuthorityAdapter.cs` — `query-inventory` and `query-profile` client. Do not add a server.
- `src/Hexalith.Platform.Secrets.Runtime/SecretsRuntimeConfiguration.cs` — reference allowlist. Do not add fields.
- `src/Hexalith.Platform.Secrets.Runtime/SecretsDependency.cs` — owner action for each missing input.
- `eng/ext-secrets-1/runtime-reference.example.json` — schema and settled constants, not installed configuration.
- `eng/ext-secrets-1/dapr-openbao.component.proposed.yaml`, `openbao-hmac.policy.proposed.hcl`, `tenant-inventory.example.json` — shape only. Do not apply nulls.
- `eng/ext-secrets-1/provisioning/owner-input-map.json` — other role owners. `apply` stays false.
- Agents `external-dependency-register.md` EXT-SECRETS-1 row — Committed. Do not edit.
- Evidence `isolated-runtime-20261016/runtime-reference.json` — update verified fields only. Keep prior `discovery.json` and packet.
- Evidence `isolated-runtime-20261016/discovery.json` — OpenBao ready, no owner component. Re-query before binding.

## Tasks & Acceptance

**Execution:**
- [x] `custody-inventory-20261016/discovery.json` — read-only Kubernetes, Dapr, OpenBao mount/TLS, and tenant-registry names. References only.
- [ ] Namespace `ext-secrets-1-isolated` — apply a read-only policy, restricted token, and `secretstores.hashicorp.vault` component only when mount, TLS, and two tenants were discovered. Mount token and CA by reference.
- [x] `custody-inventory-20261016/tenant-inventory.json` — publish discovered tenant A/B and current opaque-key to numeric KV2 mappings, or leave it unenrolled.
- [x] `isolated-runtime-20261016/runtime-reference.json` — set provider, credential, inventory, authority, tenant A/B, issuer, audience, profile version, validity, and revocation only from facts. Set `callTimeout` to 30 only with an installed provider. Preserve host, provider, timing, and Open disposition.
- [x] `custody-inventory-20261016/blockers.json` — each remaining required null: input, owner, and next action.
- [x] `custody-inventory-20261016/health.json` — component, namespace, and OpenBao health metadata. No secret bytes and no Live command.

**Acceptance Criteria:**
- Given mount, TLS, and two distinct tenants, when installation finishes, then the new namespace holds the provider and restricted credential reference, and production objects are unchanged.
- Given a missing registry, mount, profile, or revocation source, when binding is attempted, then that reference stays null and the blocker names input, owner, and next action.
- Given any result, when evidence is written, then it has no credential values, the register stays Committed, and Live remains unrun.

## Implementation Notes

Re-queried `192.168.1.30` at `2026-10-16T15:26:34Z`. OpenBao `hexalith-keys` is unsealed at 3/3 ready and Dapr is 1.18.1. The server certificate and the existing Memories vault components name TLS `hexalith-keys.openbao.svc.cluster.local`. No KV mount is an HMAC mount: `secret/` contains `hexalith/memories` and `tls/`, and `deployment-seal-custody/` list is denied. Memories Redis has no `tenant-registry-index`. The install gate failed, so `ext-secrets-1-isolated`, a new policy, a restricted token, and the vault component were not created. `runtime-reference.json` is unchanged: host, provider, timing, and Open disposition stay as recorded, and the unbound fields stay null. `callTimeout` stays null. The dependency stays unavailable. Live was not run.

## Spec Change Log

## Review Triage Log

## Design Notes

HEAD `b2890d20575356239555df5b113e3100c1d594c1` stays dirty with unrelated files. New evidence is `custody-inventory-20261016` under the existing EXT-SECRETS-1 evidence root. Namespace is `ext-secrets-1-isolated`. No authority server is added; a missing `query-inventory` host remains a blocker. Rollback deletes only that namespace and its new policy or token.

## Verification

**Commands:**
- `kubectl get namespace,component.dapr.io -n ext-secrets-1-isolated` and `kubectl get statefulset hexalith-keys -n openbao` — expected: new objects only after a real apply; OpenBao replicas unchanged.
- Parse `runtime-reference.json` — expected: settled timing and Open disposition unchanged; new fields match discovery; missing fields stay null.
- `git status --short` — expected: the same unrelated dirty paths, plus this spec.
