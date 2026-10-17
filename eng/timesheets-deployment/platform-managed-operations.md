# Platform-managed Timesheets deployment handoff

Hexalith.Platform manages **both staging and production**. The owner clarified
this responsibility in the Timesheets Story 3.6 continuation on 2026-10-17.
It agrees with the Platform specification's [ownership and single-composition
constraints](../../_bmad-output/specs/spec-platform/SPEC.md#constraints).

| Responsibility | Owning module |
| --- | --- |
| Environment provisioning, namespaces, networking, Gateway/TLS and data services | Hexalith.Platform |
| Environment configuration, secret delivery and identity/issuer enrollment | Hexalith.Platform, consuming the owning technical/domain contracts |
| Retained release composition, staging deployment, production promotion and recovery | Hexalith.Platform |
| Generic resource-binding issuer/client implementation | Hexalith.EventStore |
| Timesheets domain behavior, readiness semantics, critical flows, smoke tests and authoritative-state inventory | Hexalith.Timesheets |
| Running the module's acceptance suite against the exact managed release | Hexalith.Platform, with Timesheets defining and assessing its assertions |

The [current blocked staging proposal](platform-managed-operations.json) belongs
to Platform. Its future environment/application values paths are inside this
Platform repository. They are uncreated proposals: no production settings,
credentials, grants, historical inventory or observation is fabricated.
Both package tiers are managed by the same Platform release lifecycle. The
component-level argv cannot bypass the complete retained Platform release,
single release writer or promotion/recovery gates.

The earlier proposal in the Timesheets evidence packet is preserved as historical
preparation. Its bytes and packet manifest remain unchanged. The current proposal
supersedes its operational ownership and future values-file locations only;
it changes no target facts, artifact hashes, recovery times, evidence outcomes
or authorization. Production remains Platform-managed, with no production
deployment command or target newly selected by this clarification.

Timesheets consumes Platform's installed release identity, endpoint, issuer,
delivery, shared-rebuild and recovery evidence, then assesses its domain
acceptance criteria. Missing environment resources are Platform implementation
and operational dependencies. They are not a request for the owner to reconstruct
environment configuration in the Timesheets repository.

The owner's original live-mutation boundary still applies. Deployment, recovery,
migration, permission changes and other live mutations require the exact
Platform-managed commands, target, expected effects and recovery procedure to be
fully prepared for approval. This handoff grants no such approval and executes
no command. Story 3.6 remains in progress until deployed acceptance passes.
