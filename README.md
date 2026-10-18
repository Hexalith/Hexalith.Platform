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

## Module tool

The first Platform-accepted `hexalith-module` version is `4.33.0`, published as
`hexalith.builds.module.cli`. The local tool manifest pins that exact version.
`hexalith-platform.json` accepts the range `[4.33.0, 5.0.0-0)`: later 4.x
releases, prereleases included, are accepted without a Platform edit, and 5.0
prereleases are excluded. The catalog commit is
`544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9`, matching both the Builds gitlink
and release tag `v4.33.0`. When the exact pin moves, the Builds gitlink moves
to that version's release-tag commit.

Every version below `4.33.0` is not Platform-accepted, including the published
`4.20.0–4.27.4` and `4.29.1–4.32.0` cohorts. A tool outside the range is
refused with `HXW010` in package mode and warned in source mode.

From the Platform root, restore and check the pinned tool with:

```bash
dotnet tool restore
dotnet tool run hexalith-module --version
```

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
