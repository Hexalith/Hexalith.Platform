# EXT-SECRETS-1 continuation — 2026-10-20

**Genuine S1–S4/v23 Live qualification is incomplete. EXT-SECRETS-1 remains Committed and unavailable.** This continuation implements the approved next source unit I1/F0 and prepares updated provisioning requests. No owner Live/cohort attempt, infrastructure deployment, credential inspection, production rotation/revocation/restart/restore or Agents register amendment was performed.

The accepted owner run at `abbec4093715c1db854433119b32d4e16df20d08` stopped with 67 missing inputs and zero effects. The [ledger](input-resolution-ledger.json) retains every original name and distinguishes 50 reference fields from 17 derived/implementation prerequisites. The current configuration requires 58 public fields: five historical references are supplied and **53 remain missing**, including the three later signer/replay destination/credential fields. Supplied strings do not prove current enrollment, token renewal, authorization, installation or health. No original gap is marked resolved by a model or proposed reference.

The adopted [Dapr-only decision](../ext-secrets-1-erasure-authority-decision-2026-10-20.md) closes the architecture choice. Its qualifying component/provider, independently surviving authority and actual physical/transport proof remain absent. The requests now reflect that approved design instead of requesting the same architecture approval again. Ordinary OpenBao restore already demonstrated key revival; its HMAC store is not an all-copy lifecycle provider.

## Verified source behavior

The internal [custody model](../../../src/Hexalith.Platform.Custody/CustodySharedCutModel.cs) reuses complete custody identities and the existing target-manifest digest. It models B0–B5, whole-tenant freeze, survivor-only preparation, retired alternate/tainted epochs, uncertain sends/holders, one shared cut, retained original bytes and independently surviving current state across application restart/restore. It models current-authority loss, stale revisions, routine rotation, conflicting pins, duplicate transport requests and permanent root-reissue exclusion. It has no provider interface, DI registration, network, credential or key material.

Exploration: **740 states, 19,116 transitions, depth 16**, with explicit bounds, 28 boundary/fault schedules and five detected unsafe controls. Separate lanes cover one target/two survivors and 1,000 targets/no survivors. The Local audit now requires this class and retains its hashed content-free report. [Model assumptions and limits](../ext-secrets-1-custody-shared-cut-model-2026-10-20.md) state exactly what is modeled: actual physical deletion, complete copy/holder closure, retained independent authority, authentic proofs and real buffer erasure are premises, not qualified mechanisms.

Source validation passed **737 custody tests and 132 runtime tests** with zero failures/errors/skips/unrun tests and warning/error-free builds. The Local run also passed 154 Python tests; the later provisioning regression adds one further test and is recorded in the detached final validation. The failed first focused model run remains retained: it exposed vacuous survivor preparation in the 1,000-target lane, corrected before all 28 focused tests passed.

Evidence and exact commands: `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/continuation-20261020/validation/`. Builds source: `b435f2e3b3892e03f69322d24de6003b0baf7a7d`; EventStore source: `dfe963ffd44c4df1d3b43891234e30231a9a28e8`. These clean detached source inputs were read without changing sibling owner working trees.

## Remaining runtime inputs and implementation

The [provisioning requests](input-resolution-provisioning-requests.json) enumerate exact names, accountable owners, dependencies, return evidence and source contracts. The [pending configuration](runtime-reference.pending.json) preserves supplied references and null unknowns; it grants no runtime authority. `INPUT-SHA256SUMS` binds these three artifacts.

| Owner / seam | Missing public fields | Concrete return required |
|---|---:|---|
| Independent profile/approval authority — S1/S2/S3 | 11 | Current profile reference, issuer/audience/version/validity; stable actor-role authority; enrolled independent decision signer account/credential reference; public trust and revocation references. Approval private keys remain outside Platform/Agents. |
| Custody/lifecycle/protection owners — S3/S4/v23 | 8 | Real all-copy lifecycle/copy inventory, nonrollback originals, restore target, principal-bound export transport, migration/protection authority and public-verifier retention references. Implement the semantic component/provider and integrate real existing custody ports. |
| Capability-signing owner — S4/v23 | 2 | Actual signer Dapr app ID and restricted credential reference, with current per-tenant signing inventory, retained public verifiers and exact original result authority. |
| Private replay owner — S2 | 3 | Actual replay app ID, restricted registrar credential reference and exact EventStore scope: reserved `system/trusted-envelope-replay`, issuer-wide issuer/nonce identity, `TrustedEnvelopeFirstSeen` only at NoStream, exact lookup and public/Workflow/target-handler denial. |
| Compromise/protection/guard owners — S4/v23 | 4 | Authenticated publisher, private subscriber credential/subscription and actual guard mirror references; block-before-mirror, exact lost-result lookup and replacement-key block-set compare remain required. |
| Independent current inventory owner — S1 | 1 | Installed current-inventory authority with authenticated tenant/purpose/version/revocation reads. Five supplied references need fresh enrollment/lease/scope evidence; retained data-token evidence expired 2026-10-18 and later renewal was not inspected. |
| Workload identity owners | 11 | Distinct restricted Dapr/app-channel/caller authentication and current exact scopes for the three qualification roles. Operator and Memories credentials cannot substitute. |
| Independent qualification owners | 13 | Real executor/observer/enrollment provider implementations, immutable role identities/versions/app IDs, three separately governed public P-256 anchors, isolated target and all 234 disjoint case scopes. Existing executables do not register these providers, durable originals, authenticators or verifiers. |

The [new component return template](../../../eng/ext-secrets-1/provisioning/custody-component.proposed.json) adds 25 explicit **supplemental component/proof references**, separate from those 53 runtime fields. It names the proposed logical binding/protocol, component artifact, independent failure/recovery domains, operation-proof authority, complete copy/holder/native-intent inventory, measured Dapr limits/retries/buffer proof, shared-cut originals, old-backup non-revival/root-reissue proof and numerical retention/admission capacity. All unknowns remain null. It supplies no Secret values, provider SDK, deployment or physical guarantees.

F1 component/transport feasibility and separately authorized F2/F3 physical proof remain open. Full qualification still needs independently authenticated persisted owner state after effect, restart, restore and exact lookup for the original signing/delivery/replay/revocation/consumption operations. A source model cannot implement or certify those installed role providers.

## Replacement target and command

The detached final `HANDOFF.md` and `acceptance/replacement-acceptance-packet.json` under `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/continuation-20261020/` bind the final canonical source target, exact full-plan command and configuration/plan/public-anchor hashes. The packet generator never accepts or installs them. The three missing anchors and other runtime prerequisites keep the packet an **IncompleteSourceCandidate**. Its generated command is for review before a separately accepted Live attempt, not authorization to run now.

The replacement must also satisfy `OQ-5.4-1.CommittedProtectionOwner`, `OQ-5.4-1.SharedPlatformTarget`, `OQ-5.4-1.AcceptedPinnedHostProfile` and `OQ-5.4-1.JointLivePhaseRunner`. A standalone source verifier cannot supply the missing joint phase runner. No complete executable joint-cohort packet exists until that implementation and accepted profile exist. The current register's target and all accepted policies remain unchanged.

Preserve all historical attempts and original operation/result/recovery journals. Unknown outcomes permit exact original lookup only; deleting journals, changing scope or replacing identities is not a retry procedure. Final runtime/profile/anchor changes invalidate candidate pins and require a fresh detached packet and exact owner acceptance.
