# G-1 Durable Task runtime scope decision

On 2026-11-25 the Platform Owner approved the G-1 runtime design and isolated qualification scope outside the current MVP, as requested by the Projects 7.1-P2 owner proposals.

The scope covers selecting and recording the EventStore candidate package or source, test runtime and persistence provider, lease and scheduling behavior, and a two-replica qualification topology. Exercise crash, lease takeover, lost response, and rollback or forward recovery against the same isolated store. Record the runtime inputs, operational limits, clean-checkout commands and native results for the exact candidate.

The inspected Platform source was `f7635b592b0499a1c0888b91d1e3be4b0a2d7f78`. The proposal is `Hexalith.Projects/_bmad-output/implementation-artifacts/7-1-p2-owner-scope-proposals.md` at `fdc8ca69ebe0a656f88d55f9e32c8de71b1262f6`. This decision authorizes work in the isolated qualification scope. Runtime selection and capability acceptance are pending.

## Isolated admission runtime

The candidate uses EventStore DomainService source with .NET 11.0.100, Dapr 1.19.2 and Redis 7.4 Alpine. The runner selects exact image digests. Two sidecars share the task state key prefix and listen only within the fixture network. Redis uses append-only persistence with synchronous fsync. The runner removes the containers and network it created. [Fixture runner](../../eng/durable-task-qualification/probe_state_store.py)

Run the provider checks from Platform with `run-with-temp python3 eng/durable-task-qualification/probe_state_store.py`. Concurrent first writes returned one HTTP 204 and one HTTP 409. A successful conditional update advanced the fence, the stale writer was rejected, and both replacement Dapr sidecars read the same advanced value after Redis restarted. Pass `--native path/to/Hexalith.EventStore.DomainService.Tests.dll` to run the EventStore admission cases against those sidecars on the fixture network. The runner rejects skipped native cases.

This run supplies evidence for the admission transaction and the provider's conditional writes. The worker lease clock and scheduler topology still need execution qualification, including a durable discovery path for admitted tasks. A runtime rollback exercise must fence candidate writes, retain the store and verify task IDs, checkpoints and owner receipts before reopening work. If the retained runtime cannot interpret those records, exercise forward recovery under the fence.

## Original-actor binding (2026-11-25)

The Platform Owner accepted the committed admission lookup that EventStore and Folders selected for delegated steps. Platform qualification exercises that lookup and introduces no task-bound bearer credential.
