---
title: "Hexalith Platform: Supporting Context"
status: complete
created: 2026-09-27
updated: 2026-09-27
---

# Hexalith Platform: Supporting Context

## User-supplied constraints

- The primary purpose is one common solution to deploy environments needed for development, testing, staging, and production.
- Testing covers both local developer tests and automated CI test environments.
- The MVP includes EventStore, Tenants, Parties, Folders, Projects, McpCli, and Memories, along with required supporting components.
- The platform contains an Aspire host for starting servers during testing and debugging.
- Developers run the environment on their own machines using Aspire; other users use staging or production on Kubernetes. This includes the intended usage contexts for McpCli.
- The platform references all Hexalith servers and components.
- A module repository can include the platform as a Git submodule to run the minimum components needed to test and debug that module.
- Staging and production use the local Kubernetes server at `192.168.1.30`.
- Staging is published under `hexalith.com`; production is published under `tache.ai`.
- Production deployment happens automatically after the release passes staging checks.
- A failed production deployment triggers automatic rollback to the previous working release.
- Staging and production have separate application data and credentials despite sharing the Kubernetes installation.

These are requirements supplied by the user, not assertions that the environments have been deployed or verified.

## Repository baseline

The following observations describe the working tree inspected on 2026-09-27, including existing uncommitted work.

| Evidence | What it establishes |
| --- | --- |
| [Root README](../../../../README.md), lines 3–21 | Platform is already described as the Aspire host for domain modules and the owner of shared hosting concerns. Agents composition remains planned. |
| [Root AppHost](../../../../apphost.cs), lines 24–45 | The current host has an opt-in Works preview. It resolves a sibling `../works` checkout and nested EventStore source projects and rejects running that preview outside Development. |
| [Root AppHost](../../../../apphost.cs), lines 95–165 | The Works preview composes EventStore, its admin host, Works, and EventStore Operations with Dapr resources. This establishes one composition, not general module selection. |
| [Root README](../../../../README.md), line 53 onward | The Works AppHost remains the supported rollback composition until the migration parity gate passes. Consolidation is already underway. |
| [Agents composition plan](../../../../docs/ext-host-1-agents-composition.md), line 24 onward | Complete Agents wiring and live evidence are assigned to Agents Story 5.6. |
| [Root submodule declarations](../../../../.gitmodules) | Sixteen module/component repositories are referenced. Repository references alone do not establish runtime integration. |
| [Shared Hexalith instructions](../../../../references/Hexalith.AI.Tools/hexalith-llm-instructions.md), lines 123–134 and 242 onward | Shared guidance places hosting outside domain modules and calls for source references and Debug assets during local development. |

The declared references are AI.Tools, Agents, Builds, ChatBot, Commons, Conversations, EventStore, Folders, FrontComposer, Memories, Parties, PolymorphicSerializations, Projects, Tenants, Timesheets, and Works. This is the current inventory, not a permanent limit on platform coverage or a claim that each reference runs as a server.

No Platform reference was found in the immediate module `.gitmodules` files inspected. No root implementation of the requested Kubernetes server and domain mapping was found. These remain target capabilities.

## McpCli context

The user-specified `mcpcli` is the sibling **Hexalith.McpCli** repository at `../mcpcli`. It is not present in Platform's root `.gitmodules` inventory inspected for this brief.

The [McpCli README](../../../../../mcpcli/README.md), lines 1 and 40 onward, describes a shared CLI and MCP server for decorated Contracts libraries. Both interfaces send commands and queries through the EventStore gateway, which routes them to module servers. The [executable project](../../../../../mcpcli/src/Hexalith.McpCli/Hexalith.McpCli.csproj) defines the `hexalith` .NET tool. The README currently documents MCP over stdio, with HTTP transport reserved for later work.

The user established the usage contexts: developers run locally with Aspire; other users use staging or production on Kubernetes. McpCli must support these contexts as part of the MVP. The transport, process placement, and exposure of hosted MCP access remain architecture work. The current stdio-only documentation does not establish that hosted integration is already available.

The README also reports an empty catalog pending module enrollment, including Projects and Folders among the pending modules. This is a documentation finding; runtime readiness and the complete enrollment status of the MVP modules have not been verified.

## Module repository integration

The user identified the shared instructions as the source of the existing submodule policy. [hexalith-llm-instructions.md](../../../../references/Hexalith.AI.Tools/hexalith-llm-instructions.md), lines 29–31 and 299–303, delegates Git rules to [hexalith-git-instructions.md](../../../../references/Hexalith.AI.Tools/hexalith-git-instructions.md). Its submodule section, lines 39–44, permits initializing or updating only root-declared `references/` submodules, prohibits nested initialization and recursive or remote updates, and requires accidentally initialized nested submodules to be deinitialized.

Applying that policy gives two workspace contexts:

- **Platform is the root:** required modules are initialized from Platform's own root `.gitmodules` declarations.
- **A module is the root:** Platform and the other required dependencies are direct `references/` entries of that module workspace. Platform's own nested submodules remain uninitialized.

For example, the following layout follows from the policy; it is illustrative, not a mandated Platform directory name:

```text
Module workspace/
  src/                         # Module being developed
  references/
    Hexalith.Platform/         # Shared host; nested references uninitialized
    Hexalith.EventStore/       # Example direct dependency
```

Reciprocal repository declarations therefore do not require recursive checkout. Remaining implementation work is to resolve source paths from the active workspace and select the required runtime components. Whether nested initialization is allowed has already been decided by the shared policy.

Module debugging must use the checkout being edited so source changes and breakpoints affect the running module, consistent with the shared source-reference rules. The current Works preview's fixed sibling paths do not establish a general solution for this use.

Shared instructions also require project references and Debug assets in local development/testing, and NuGet package references and Release assets in CI/CD (lines 242–250). The common platform must account for these established differences between contexts.

The minimum required components need an explicit definition for each supported module and test/debug scenario. Open questions include dependencies of dependencies, optional services, real services versus test doubles, and how dependencies are supplied under the established local and CI/CD reference rules. Architecture will choose a manifest format, selection mechanism, and source-resolution strategy within those constraints.

## Assumptions and downstream decisions

The following assumptions and design questions remain for requirements and architecture work:

- Developers using local Aspire environments and other users accessing hosted staging/production are confirmed user contexts. `[ASSUMPTION]` Integration testers and environment operators are additional platform personas whose responsibilities need to be described.
- `[ASSUMPTION]` Centralizing deployment should reduce repeated hosting work and configuration drift. Current effort and impact have not been quantified.
- Candidate measures are startup time, developer setup effort, resource consumption, and deployment recovery time. No targets have been selected.
- The lifecycle and hosting location of automated CI test environments remain to be defined within the confirmed testing scope.
- Automatic production deployment after staging checks pass and automatic rollback to the previous working release on deployment failure are established. The checks, failure detection, operating responsibilities, and production service expectations remain to be defined.

## Hosted environment constraints

| Environment | User-specified location | User-specified publication domain |
| --- | --- | --- |
| Staging | Local Kubernetes server `192.168.1.30` | `hexalith.com` |
| Production | Local Kubernetes server `192.168.1.30` | `tache.ai` |

The publication domains do not yet specify per-service hostnames, paths, DNS configuration, certificates, or ingress routing. The server address does not reveal the cluster topology. Existing capacity, storage, credentials, backup arrangements, and recovery expectations have not been assessed.

Staging and production must have separate application data and credentials. Configuration management and the permitted sharing of supporting services and capacity remain architecture work within that requirement.

Production deployment is automatic after staging checks pass. A failed production deployment triggers automatic rollback to the previous working release. The staging checks, failure detection, rollback mechanism, and recovery responsibilities remain to be specified, including the relationship to Hexalith.Builds. This defines the intended release behavior; no release pipeline has been implemented or executed as part of this brief.

## External research for later decisions

These sources inform the remaining design questions. Compatibility with the project's installed versions and the local cluster has not been verified.

- Aspire describes project resources and explicit resource references. **Inference for this platform:** the brief needs an explicit responsibility for choosing a module's minimum dependencies; those docs do not establish automatic discovery of the complete set. [Aspire project resources](https://aspire.dev/integrations/dotnet/project-resources/)
- Aspire documents Kubernetes artifact publishing and deployment to an existing cluster. This makes deployment ownership a relevant product decision; the brief leaves the implementation choice open. [Aspire Kubernetes deployment](https://aspire.dev/deployment/kubernetes/)
- Aspire documents deployment contexts for different environments. **Inference for this platform:** shared composition can be a basis for staging and production while environment-specific configuration and release policies are specified separately. [How Aspire deployment works](https://aspire.dev/deployment/deploy-with-aspire/)
- Kubernetes namespaces scope resources, but network and resource isolation require additional controls. **Inference for this platform:** separate environment names alone do not define the isolation outcome. [Kubernetes namespaces](https://kubernetes.io/docs/concepts/overview/working-with-objects/namespaces/), [Kubernetes multi-tenancy](https://kubernetes.io/docs/concepts/security/multi-tenancy/)
- Kubernetes production guidance discusses availability and operational requirements. **Inference for this platform:** availability and recovery targets depend on the actual cluster topology; the supplied IP address is insufficient to assess them. [Kubernetes production considerations](https://kubernetes.io/docs/setup/production-environment/)
