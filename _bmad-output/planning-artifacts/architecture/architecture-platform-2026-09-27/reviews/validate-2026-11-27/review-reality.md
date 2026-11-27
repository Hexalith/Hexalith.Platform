# Repository and upstream evidence review

**27 November 2026 · Platform HEAD `f87f1593ca07ce617df50afb745a1995fde4b1e0` · Reviewed spine blob `5a5d4d13c5ef020571e2951267de9f60c668ef80`**

**Verdict.** The revised currency plan gives the first release its own admission check. An adopted companion still describes the earlier circular sequence, and the inventory prerequisite remains blocked. The exact current Platform source tuple also fails its own verification.

The review used the working-tree spine and planning sources, the committed source manifests, retained qualification outputs and public maintainer sources. The only execution was the read-only current-source selection check. Production inventory, private infrastructure, notification delivery and qualification of the currency evaluator require their own evidence.

## Findings

### High · The adopted hosted-profile handoff still places the first currency evaluator after its drill caller

The November 27 spine puts the evaluator in Story 6.14 before Story 6.23 and the drill caller in Story 8.17 after 6.23. The adopted hosted-profile binding still says the admission and drill callers are prerequisites to the live checker. An implementer consuming the 4.9 handoff can defer the evaluator until 8.17, leaving 6.23 unable to admit the first release and leaving 8.17 without working production. The November 27 change identified this exact cycle in the earlier Story 9.2 draft.

**Disposition · Autofix in an Update.** Clarify source precedence where the spine hands off 4.9 and 9.2. Add a dated correction adjacent to the format adoption that governs caller sequencing, preserving the adoption-bound proposal and canonical policy bytes. Gate full three-trigger qualification at 9.8. **Confidence · High.**

Evidence — `ARCHITECTURE-SPINE.md:380,560,567–568,572`, `docs/contracts/hosted-profile-binding-v2.md:106–115`, `docs/contracts/hosted-profile-v2-adoption.json:8–16`, `spec-9-2-run-the-shared-infrastructure-currency-check.md:18–27,63–67`, `sprint-change-proposal-2026-11-27.md:19–29`.

### High · Story 4.9 still lacks an order for obtaining its capture-lag evidence

The 4.9 implementation record requires a staging realm, exporter, durable sink and checkpoint for a conforming measurement. The 4.19 acceptance criteria require export within the bound set in 4.9. The measurement proposal records that the necessary exporter and telemetry do not exist. If 4.19 supplies the measurement inputs only when it meets its acceptance criteria, 4.9 cannot complete. The newly explicit 4.9 prerequisite brings this existing handoff onto the 6.14 critical path.

**Disposition · Discuss.** Assign an isolated measurement setup and exit criteria before 4.9 ratification. Make the 4.19 bounded-export acceptance consume the resulting recorded limit. This may be preparatory 4.19 work or an explicitly owned measurement path. The measured limit remains Administrator-owned. **Confidence · High on the outstanding dependency, medium on the remedy pending ownership.**

Evidence — `ARCHITECTURE-SPINE.md:380,525,553–554`, `spec-4-9-ratify-the-hosted-runtime-and-identity-event-contracts.md:103–109`, `docs/implementation/identity-event-capture-lag-measurement-proposal.md:9–27`, `epics.md:2368–2371,2703–2727`.

### High · The reviewed main source tuple fails AD-4's released identity requirement

All four committed owner bindings differ from the current Platform gitlinks. The existing CI source-selection function refuses this HEAD on EventStore, its first comparison. Builds also has different identities: the committed binding and tool manifest both specify 6.9.0, but the local `v6.9.0` tag resolves to `87d45477c16fef2b3db706d2531e1cf3ff98640f`, while the Platform gitlink is `120a71fa750b8194648c204d1d4ab187712bc82a`. This is an implementation and evidence blocker under the existing architecture.

| Owner | Committed binding | Current gitlink |
| --- | --- | --- |
| EventStore | `295b5a6ba584` | `5484fae6c284` |
| Parties | `2851be5c79d4` | `ca637a01829c` |
| Tenants | `e4e28904dfb41` | `ce3aadd8f2f7` |
| Memories | `45e165d88d35` | `8eeb3017c481` |
| Builds | `87d45477c16f` at local `v6.9.0` | `120a71fa750b` |

**Disposition · Defer to existing implementation gates.** Reconcile the exact release/tag and owner source mappings and regenerate the associated evidence before accepting candidate or runner evidence. The checked-in gate is doing its job. The report uses the locally installed tag and the committed tree and makes no claim about remote tag state. **Confidence · High.**

Evidence — `ARCHITECTURE-SPINE.md:105–117,538–540`, `eng/current-tuple-source.json:3–21`, `eng/current_tuple_source.py:50–69`, `eng/builds-tuple.json:2–4`, `.config/dotnet-tools.json:4–9`, `git ls-tree HEAD references/Hexalith.{Builds,EventStore,Parties,Tenants,Memories}`.

### Medium · The spine's profile digest terminology omits the adopted resolved identity

The template-and-pins definition is consistent with the v2 infrastructure digest. The production currency result must also bind the exact checked inventory and independent composing inventory, and Story 4.9 requires the distinct complete resolved-profile identity. The spine uses a singular `profile digest` in admission, qualification and retention rules. It never points builders at the adopted digest contract or explicitly says which identity stays stable under scope and current-binding changes. Two otherwise compliant implementations could key the same output to different identities.

**Disposition · Autofix in an Update.** Link the Story 4.9 adopted v2 contract in the production-profile row. Identify the template-and-pins digest as the infrastructure digest and require the distinct checked inventory and resolved identities where their owner contracts call for them. Keep the same codec and schema. **Confidence · High.**

Evidence — `ARCHITECTURE-SPINE.md:249,254,304,380,529`, `docs/contracts/hosted-profile-binding-v2.md:75–104`, `docs/contracts/shared-infrastructure-currency-policy-v1.md:244–251`, `spec-4-9-ratify-the-hosted-runtime-and-identity-event-contracts.md:28–32,72–76`.

### Medium · The Helm floor needs a separate current-support and skew criterion before selecting the executor pin

The spine's floor is 4.2.0. Story 4.5's passing isolated evidence used that exact pin. Helm's published matrix lists 4.2.x through Kubernetes 1.36 and 4.3.x through 1.37. The project maintains the latest minor's release branch, and its current stable release is 4.3.2. Policy v1 excludes the deploy toolchain. An inventory author could read the 4.5 evidence and floor as sufficient to choose 4.2.0 even when it is outside the supported skew for the eventual Kubernetes pin.

**Disposition · Discuss with the 4.9 and executor owners.** Keep the floor and the dated proof as recorded. Bind the separately qualified executor toolchain to upstream support, skew and the actual Kubernetes/SSA evidence before use. Keep the management-installation and application-executor toolchain evidence distinct. **Confidence · High on upstream compatibility ranges and retained inputs.**

Evidence — `ARCHITECTURE-SPINE.md:413–429,550`, `eng/helm-qualification/toolchain.json:2–8`, `epics.md:2348–2354`, `docs/contracts/shared-infrastructure-currency-policy-v1.md:50–54`. [Helm version support and skew](https://helm.sh/docs/topics/version_skew/) · [Helm v4.3.2 release](https://github.com/helm/helm/releases/tag/v4.3.2)

## Evidence that supports the decisions

- The root manifests select .NET SDK 11.0.100 and Aspire 17.0.0. The upstream support pages confirm .NET 11 and Aspire 17.0 are supported. Aspire 13.5 and 13.6 are out of support and are explicitly historical in the spine. The spine's dated November 19 tool value of 6.3.2 and the current prepared 6.9.0 manifest serve different snapshots. `global.json:2–4`, `apphost.cs:2–13`, `.config/dotnet-tools.json:4–9`, `ARCHITECTURE-SPINE.md:411–425`. [Microsoft .NET support](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) · [Aspire support policy](https://aspire.dev/support/)
- The local 6.9.0 Builds tag catalog selects Dapr runtime 1.19.2, CLI 1.19.1 and Dapr.Client 1.19.1. Dapr's official releases endpoint resolves to runtime 1.19.2. Its general documentation page still lists the previous packaged combination in its table, which illustrates why 4.9 needs component-specific support sources and why version publication, compatibility and qualification remain distinct. Story 4.5's November 23 evidence qualified the chart and Dapr inputs on its approved eight-workload graph, using Helm 4.2.0 and Builds 6.6.2. It verifies the repaired application/environment Dapr ownership boundary on that graph. `eng/helm-qualification/README.md:9–19,40–71`, `spec-4-5-qualify-the-aspire-to-helm-export-with-the-reference-composition.md:257–262`, `spec-4-9-ratify-the-hosted-runtime-and-identity-event-contracts.md:80–87`. [Dapr 1.19.2 release](https://github.com/dapr/dapr/releases/tag/v1.19.2) · [Dapr support policy](https://docs.dapr.io/operations/support/support-release-policy/)
- The Kubernetes 1.34 end-of-life date in the G1 row matches upstream. Rancher 2.15.2 and K3s 1.35.9 are historical planning identities. The version-specific SUSE matrix describes technical pairing ranges. Standalone Kubernetes can leave support even while its minor is listed in a Rancher matrix, and the previously recorded pairing remains a separate qualification. `ARCHITECTURE-SPINE.md:276,565`, `eng/cluster-management/RANCHER.md:13–34`, `docs/contracts/shared-infrastructure-currency-policy-v1.md:111–127,199–232`. [Kubernetes 1.34 end of life](https://kubernetes.io/releases/1.34/) · [Rancher 2.15.2 support matrix](https://www.suse.com/suse-rancher/support-matrix/all-supported-versions/rancher-v2-15-2/)
- Policy v1 matches the Story 9.2 frozen SHA-256, `c75a8b0b85ee1aa73615e5906dc0d3aa8a5601743fefd9de4b6eb1d3b817a4a9`, at `8c79c18874a3a767d60be63c2ce4020a73991f96`. The inventory, caller, approval and result records still require the documented authority. Story 4.9 remains backlog, the adopted v2 contract states that no independent inventory is populated, and 9.2 remains backlog. The scan provides no basis for treating dated stack or Rancher evidence as the deployed production inventory. `spec-9-2-run-the-shared-infrastructure-currency-check.md:18–33,63–81`, `docs/contracts/hosted-profile-binding-v2.md:96–112`, `sprint-status.yaml:174,287–295`.

## Decision coverage and remaining execution evidence

| Decisions | Repository evidence inspected |
| --- | --- |
| AD-1, AD-3, AD-8 · Composition, release tiers and environment separation | `apphost.cs:16–22` owns the composition entry point. `eng/helm-qualification/README.md:40–71` confirms the selected template and ownership transfer boundary. `eng/staging-foundation/README.md:3,132–134` still records the closed staging execution gate. |
| AD-2, AD-5, AD-7, AD-10 · Provenance, CI ownership, executors and lifecycle | `docs/contracts/staging-evidence-policy-v2.md`, `docs/contracts/candidate-and-attachment-policy-v1.md`, `eng/repository-controls/README.md`, and `eng/lifecycle-qualification/README.md` support exact candidate, executor and ownership requirements. No cluster, repository-rule or runner campaign was run. |
| AD-4 · Source identity | The read-only selection gate and exact tuple comparison above establish refusal of the reviewed commit. |
| AD-6, AD-9 · Realm and Dapr contracts | The capture-lag proposal and `src/Hexalith.Platform.Composition/PlatformDaprBindings.cs` support the chosen authority and consumer-binding contracts. Canonical hosted inventory and actual realm/exporter evidence are still open. |
| AD-11, AD-14 · McpCli and calling surfaces | The owner McpCli spine records the common executor, gated operation coverage and current enrollment boundary. Platform carries the upstream exceptions at `ARCHITECTURE-SPINE.md:402`. |
| AD-12, AD-15 · Recovery and rollback | `docs/contracts/recovery-hook-v1.md`, `eng/recovery-preparation/README.md` and the Story 4.5 exact application recovery renders support the contract and isolated evidence. An actual recovery drill, working-baseline compatibility, and promotion qualification still require their respective gates. |
| AD-13 · Composed EventStore runtime | `src/Hexalith.Platform.EventStoreHost/Program.cs` registers the host and `src/Hexalith.Platform.Composition/PlatformEventStoreExtensionBindings.cs` enforces extension compatibility. Exact hosted release and owner publication evidence remain separately governed. |
