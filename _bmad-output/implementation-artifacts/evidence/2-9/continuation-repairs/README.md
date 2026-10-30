# Story 2.9 continuation: isolated and fixture repairs

This phase makes no live, package, attachment, expiry-enforcement or candidate qualification claim.
No files were staged or committed by this implementation agent. Source identities and exact changed
paths are in `source-snapshot.json`; Builds changed externally during this phase.

Passing complete project runs: EventStore Testing 190/190 (zero skips), Testing.Integration 52/52
(zero skips), Parties Aspire 9/9, Contracts 226/226, Client 345/345 and CI 472/472. The final main
Parties project executes all 2,811 tests with zero failures and four pre-existing optional skips
(2,807 passed). The private EventStore integration project builds, and its writer-protocol
cutover/restore classes pass 16/16; this is focused evidence, not qualification of its entire live lane.
Declaration renderer tests pass 7/7. All builds are Debug, individual and serialized.

The tests now preserve HTTP authentication while using explicit isolated protocol seams; migrated
fixtures use exact resource/module/endpoint clients, and streamed response reads retain fixture
cancellation and their finite request budget. The new loopback streaming test first failed because
an outstanding body read outlived fixture disposal; both failed attempts and the passing 190-test
run are retained under `streaming/`.

Tool-selected source roots are explicit test assembly metadata, including the owning repository
root for external artifacts directories. Committed selection observations are separate from mapped
HEAD/dirty observations and explicitly report `immutable_consumption=false`. Mismatched/dirty mapped
checkouts still exercise and fail the strict immutable gate. Diagnostic hashes have distinct purpose
prefixes; strict receipt hashing and all malformed fixtures retain their prior checks. The dated
2026-10-30 source observation adds no historical approval or consumption proof. Historical rows
remain intact. Local package probes use Debug; the CI Release counterpart remains selected by its
own test build configuration, and inherited source mapping is removed from independent probes.

`counts.json` lists complete run counts. Round directories preserve exact commands, exit codes and
compressed logs, including every intermediate broad failure. `archives.json` verifies compressed
and original log bytes. The full task patch archives (including new files), tracked patch archives and source hashes bind this phase before the
subsequent runtime prerequisite work. They include no runner private credentials or live snapshots.
