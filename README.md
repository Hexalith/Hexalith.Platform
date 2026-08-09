# Hexalith.Platform

Platform-owned Aspire host for Hexalith domain modules.

This repository is the **EXT-HOST-1** delivery target for Hexalith Agents: it owns
production-like composition of the Agents DomainService and FrontComposer UI with
EventStore, Conversations, Parties, Tenants, Provider and safety adapters, Dapr
Workflow, secrets, health, identity, telemetry, and evidence ingress.

Domain modules such as [Hexalith.Agents](https://github.com/Hexalith/Hexalith.Agents)
must **not** ship module-owned `AppHost`, `Aspire`, or `ServiceDefaults` projects.
Those hosting concerns live here.

## Status

| Milestone | State |
| --- | --- |
| Repository + empty Aspire AppHost scaffold | Current |
| Agents DomainService / UI composition wiring | Planned (Agents Story 5.6) |
| Live Level 4/5 compatibility evidence | Planned (`Available` gate) |

## Requirements

- .NET SDK `10.0.302` or later (`latestPatch` roll-forward)
- Aspire CLI `13.4.6` or later

## Verify Agents host contract (clean checkout)

From a clean clone of this repository:

```bash
./eng/verify-agents-host.sh
```

Expected: Release build of the platform AppHost succeeds, and the repository
declares itself as the Agents platform host without requiring any Agents-owned
hosting projects.

## Local run

```bash
aspire run
```

## Owner

Platform Maintainer (Hexalith).
