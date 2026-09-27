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
| Works development topology | Opt-in preview; AD-20 parity gate open |
| Agents DomainService / UI composition wiring | Planned (Agents Story 5.6) |
| Live Level 4/5 compatibility evidence | Planned (`Available` gate) |

## Requirements

- .NET SDK `10.0.401` or later (`latestPatch` roll-forward)
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

The Works migration topology is available when the sibling `works` checkout is
present. Enable it explicitly for local development:

```bash
Platform__Works__Enabled=true aspire run
```

The Works topology is a development preview while the AD-20 R1–R11 parity and
rollback gate remains open. The existing Works AppHost remains the supported
rollback composition until that gate passes.

## Owner

Platform Maintainer (Hexalith).
