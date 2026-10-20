# Owner approval and Dapr constraint — 2026-10-20

The owner replied **“I approve”**, then clarified **“we must keep infrastructure abstraction given by DAPR, any infrastructure dependency should be avoided.”** This record applies the later instruction to the approval. The original presented decision/spine remain immutable in [approval-basis](approval-basis/), bound by [binding.json](approval-basis/binding.json); they are historical proposal bytes, not the current adopted boundary.

## Current disposition

- **OA-1 approved:** shared physical all-or-none cut plus recoverable exact-original publication is accepted instead of requiring one native transaction across key/storage services. Physical target/survivor/hold/original proof is still mandatory; no cross-service ACID claim.
- **OA-2 amended by explicit owner constraint:** retain independent custody/evidence recovery classes, accessed exclusively through Dapr. Direct AWS/native adapter, mandatory cloud accounts/Region and Platform AD-9 exception are not adopted. Parent AD-9 was not changed. Physical provider internals belong only to a replaceable infrastructure component, never application dependencies. No provisioning/spending/deployment authorized.
- **OA-3 approved conditionally:** one physical cycle/tenant, whole-tenant preparation pause/O(N) sweep, existing actor limits and fail-closed authority loss; the reference profile's seven-plus-day pending interval is acceptable. Actual provider bounds, rates/budget/capacity and measured admission/completion headroom remain required.
- **OA-4 approved assurance boundary:** registered system copies, independent authority and documented vendor/trusted-runtime assurance; actual copy lineage and all Dapr/SDK/component/transport/buffer completion/termination proof remain required. Approval is not evidence and waives none of those checks.
- **OA-5 approved as amended:** hierarchy, original identity, guard/native-intent/key-use protocols stay behind provider-neutral Dapr contracts. Select a semantic custody output binding; complete component/provider implementation/qualification is absent. No thin HTTP wrapper or generic secret/state/crypto calls count as the physical provider.
- **OA-6 approved policy:** finite retention/extension/minimization covers every referenced batch/outcome/backup, allocation exclusion and public verifier. Actual numerical schedules/lock settings remain inputs; no TTL is invented and no referenced original expires early.

Custody AD-16–AD-24 are adopted/amended; AD-25 records the mandatory Dapr boundary. Approval closes those architecture choices, not physical feasibility, deployment, provider installation or availability. The AWS construction is reference feasibility research inside a possible infrastructure component, not a mandated provider or Platform dependency.

## Unchanged scope

No infrastructure deployment, destructive qualification, owner Live/cohort command, Agents register amendment or Available declaration. F2/F3 require separate explicit authorization of exact synthetic resources, monetary ceiling, ownership/cleanup and evidence retention. This task records adoption and creates the [implementation spec](../../../_bmad-output/specs/spec-ext-secrets-1-interaction-custody/SPEC.md); it executes no feasibility experiment.

A failed physical/component/transport proof rejects the candidate. The ordinary OpenBao/Raft erasure-first stop remains. A restored deny flag, Dapr facade, successful SDK call or approved design cannot qualify erasure.
