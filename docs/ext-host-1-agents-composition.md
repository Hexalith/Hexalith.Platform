# EXT-HOST-1 — Agents platform host composition

The expanded owner artifact is Committed at immutable Platform target `f448db29493c1b30b0f18380188fe99b8b7606fe` with the Full command pinned in the Agents external dependency register. The register's exact-target Full command still exits at the closed gate. A successful build of the default empty scaffold does not prove Agents composition or Levels 4/5 qualification. Earlier Uncommitted statements in dated source evidence describe historical state.

The complete requirement is H1–H4: Platform-owned Agents DomainService/UI composition with accepted EventStore, Parties, Conversations, Tenants, Provider/Safety, Workflow, custody, identity and health/telemetry; a replicated durable security-audit spool and private exact-target replay/recorder/worker credentials; real payload protection and persisted FR-34 startup/every-readiness attestation; migration/fences/guard protocols, all-or-none manifest destruction, v21/v23 compromise/dispatch/reserve/activation/completion and restore qualification. The target, date and command are accepted; installed bindings and passing qualification remain required. Local signing fixtures do not supply S3/S4 or these host protocols.

Default full verification without `--profile` runs the verifier and then exits 1:

```bash
./eng/verify-agents-host.sh
./eng/verify-agents-host.sh --mode Full
```

That command exits 1 with `DependencyNotAvailable: EXT-HOST-1` and records no live effect.

Component qualification pins a complete deployment profile:

```bash
./eng/verify-agents-host.sh --mode Full --profile /path/to/agents-deployment-profile.json
```

The default artifacts directory keeps one complete attempt. A later commit or profile needs a fresh `--artifacts-path`.

It exits 0 only when every required custody and guard cell passed. That result is component qualification, and `dependencyAvailable` stays false. Live qualification and cohort `OQ-5.4-1` remain gated on `EXT-PROTECTION-1`, trust anchors, and the cohort prerequisites.

Explicit local scaffold verification builds Debug into isolated artifacts:

```bash
./eng/verify-agents-host.sh --mode LocalScaffold --artifacts-path /tmp/hexalith-agents54-host-artifacts
```

It proves only the platform scaffold build and ownership checks. It cannot mark EXT-HOST-1 Available, and a fake accepted manifest cannot unlock undelivered code. Unknown arguments, or `--profile` without a path, fail before pin tests and before `dotnet`. The register accepts the Full runner as the compatibility command. Component qualification leaves the live gate closed.

Setting `Platform:Agents:Enabled=true` composes Agents only from a valid deployment profile. Without that profile, enablement refuses before any Agents resource is added. The default scaffold and the explicitly enabled Works/Identity lane retain their existing wiring. No fake custody/protection/spool or new database is installed.

[Local owner evidence](implementation/ext-host-1-local-gates-2026-10-06.md) records the exact tested source, modes and runtime limits. The full owner plan and external dependency register in Agents remain authoritative.
