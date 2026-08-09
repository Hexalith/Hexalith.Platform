# EXT-HOST-1 — Agents platform host composition

## Required artifact

Platform-owned host composition for the Agents DomainService and UI with
EventStore, Conversations, Parties, Tenants, Provider and safety adapters,
Dapr Workflow, secrets, health, identity, and telemetry. This artifact
replaces module-owned AppHost, Aspire, and ServiceDefaults ownership in
`Hexalith.Agents`.

## Compatibility contract

Clean-checkout composition through this repository's Aspire AppHost without
any Agents-owned hosting infrastructure.

## Current verification command

```bash
./eng/verify-agents-host.sh
```

## Delivery notes

- Scaffold and ownership: this repository (`Hexalith.Platform`).
- Full Agents composition wiring and live Level 4/5 evidence: Agents Story 5.6
  and register status upgrade from `Committed` to `Available`.
