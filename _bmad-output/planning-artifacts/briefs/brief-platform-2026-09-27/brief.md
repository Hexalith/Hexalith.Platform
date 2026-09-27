---
title: "Product Brief: Hexalith Platform"
status: complete
created: 2026-09-27
updated: 2026-09-27
---

# Product Brief: Hexalith Platform

## Purpose

Hexalith Platform will provide one common solution for deploying development, testing, staging, and production environments across Hexalith modules. It will reference all Hexalith servers and components and contain the Aspire host used to start the system for testing and debugging.

The platform must also work as a Git submodule inside an individual module repository, starting only the components needed to test and debug that module. Developers run locally with Aspire; other users access staging or production on Kubernetes.

## Problem

The repository already assigns shared hosting responsibilities to Platform, but the host inspected during discovery provides only an optional Works development preview. Full-system composition and reusable startup within a module repository remain gaps. The product addresses the need to deploy every required environment through a shared platform. Repository evidence is in the [addendum](addendum.md#repository-baseline).

## MVP scope

The MVP includes **EventStore, Tenants, Parties, Folders, Projects, McpCli, and Memories**, plus the supporting components required to run them. Hexalith.McpCli provides CLI and MCP access to module operations through EventStore and must support the local Aspire and hosted Kubernetes contexts.

This initial module set must work across all the environments below, both together and as the minimum composition needed for an individual module. The broader platform scope remains support for all Hexalith servers and components.

## Intended uses

| Context | Required outcome |
| --- | --- |
| Development and debugging | Start the required Hexalith servers on the developer's own machine through the platform's Aspire host. |
| Local developer tests | Deploy the components required by a local test scenario through the common platform, including the complete system when needed. |
| Automated CI tests | Provision the environments required by automated CI tests through the same platform, using the established CI build and dependency rules. |
| Development within a module repository | Include the platform as a Git submodule and start only the minimum components needed to test and debug that module. |
| Staging | Run the staging environment on the local Kubernetes server at `192.168.1.30`, published under `hexalith.com`. |
| Production | Run the production environment on the same local Kubernetes server, published under `tache.ai`. |

## Release policy

After a release passes staging checks, Platform automatically deploys it to production. If the production deployment fails, Platform automatically rolls back to the previous working release. The staging checks, failure detection, and rollback mechanism will be specified in downstream requirements and architecture.

Staging and production use separate application data and credentials, even though they share the Kubernetes installation at `192.168.1.30`.

## MVP success criteria

The MVP is usable when the following outcomes can be demonstrated:

1. **Run the MVP locally:** a developer can start the seven-module MVP and its required supporting components on their own machine with Aspire for testing and debugging.
2. **Work in one module:** a developer can use Platform as a direct submodule to run and debug that module with only its required dependencies, following the root-only submodule policy.
3. **Test locally and in CI:** both local developer tests and automated CI tests use environments provisioned through Platform, following the appropriate build and dependency rules.
4. **Use the hosted environments:** the MVP is available through staging under `hexalith.com` and production under `tache.ai`, with separate application data and credentials.
5. **Release and recover automatically:** a release that passes staging checks deploys to production automatically; a failed production deployment rolls back to the previous working release.

## Established constraints

Only submodules declared under `references/` by the active root repository may be initialized; nested submodules remain uninitialized. When a module repository includes Platform, that module workspace supplies the required dependencies through its root declarations. When Platform is the root, its own declarations apply. See the [policy source and workspace implications](addendum.md#module-repository-integration).

The existing repository direction places shared hosting in Platform and domain behavior in the modules. Shared Hexalith instructions require project references and Debug assets for local development and testing, and NuGet package references and Release assets for CI/CD.

## Open questions for requirements and architecture

- **Minimum dependencies:** who defines the required set for each module, and must dependencies always be real services, or can a development configuration use test doubles?
- **Publishing and operations:** which staging checks permit automatic production deployment, how deployment failure is detected, and which build and release capabilities Platform uses from other Hexalith components.
- **Production expectations:** what downtime and recovery targets are acceptable, and which supporting services and capacity may be shared while keeping application data and credentials separate?

Source resolution and the deployment mechanism belong in later architecture work. CI environment lifecycle and McpCli hosted access also need specification. The server address does not establish the cluster's node count or availability characteristics.

Technical constraints, repository evidence, and external research are recorded in [addendum.md](addendum.md).
