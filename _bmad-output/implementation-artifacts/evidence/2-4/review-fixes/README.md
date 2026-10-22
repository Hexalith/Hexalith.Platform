# Story 2.4 parent-review corrections

Focused checks pass: **109/109 Builds**, **3/3 engine**, **3/3 Platform ports**. Builds compiled with no warnings/errors; Platform compiled with the existing MSB3277 Redis assembly conflict warning. All invocations were serial. Exact commands and working directories are in `commands.txt`; method names and retained failure investigations are in `verification.json`. The full affected regressions and a fresh serial live proof remain parent verification work.

Changes:

- Secondary port proposals are filtered against persisted ports before only the accepted allocation is claimed; public allocator signatures remain unchanged.
- Complete claim JSON is flushed privately and atomically renamed under the existing transaction. Subprocess tests verify contention, termination of a transaction holder, OS lock release and peer preservation.
- Native execution snapshots the bounded validated descriptor before launch and archives those bytes plus each inspected native report directly into durable entry diagnostics. Cleanup no longer rereads child-writable descriptor/report archives. Existing redaction and credential refusal remain enforced.
- Archival uses the original remaining processing token, reports evidence failures, and preserves the first child outcome and original stopping timestamp. Changed-descriptor and FIFO replacement cases are covered.
- The active entry selects final stopping, graceful-stop options, evidence selection and cleanup bounds; mixed declared limits are covered.
- Non-600 unoverridden startup policies and unsupported/incomplete actual CI context refuse before service provisioning. The command consumes the original resolved startup policy; the real engine reloads and validates the authoritative closure before allocation, and the producer validates again before handoff. No justification or CI metadata is invented.
- Legacy evidence export and validation refuse multi-entry batches, and validation checks represented selection membership. Normal batch execution and durable inspected entry reports remain supported. Candidate/suite-completion proof remains unpopulated.
- Adjustable gate tests now exercise expired legacy selections with fresh actual activation, renewal capped by actual expiry, and closure at that expiry. Normal engine cleanup tests seed owner/peer claims and cover successful release and incomplete cleanup retaining blocking ownership.

The initial 36-failure focused log is preserved. Its command preflight redundantly loaded the closure before the simulated start override had prepared authoritative fixture roots. Reusing the already-resolved startup policy removed that duplication while preserving the real engine's pre-allocation closure validation. Two descriptor cases also needed selected readiness metadata. A subsequent single failure was a fixture assertion against a test name deliberately normalized by the existing redactor; distinct retained counts now identify reports without relaxing redaction. The Platform test's first run needed the factory's explicit Platform marker for a zero-module plan. Corrected runs are green.

Only the added `SecondaryClaimsOnlyTheAcceptedNonOverlappingAllocation` method and its subsequent fixture-marker correction belong to this fix in the shared `PlatformRunIsolationTests.cs`; concurrent content is preserved. Frozen spec intent, schema contracts, pins and unrelated work were not changed.
