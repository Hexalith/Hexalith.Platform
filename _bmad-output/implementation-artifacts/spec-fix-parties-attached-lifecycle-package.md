---
title: 'Fix the Parties attached lifecycle package build'
type: 'bugfix'
created: '2026-11-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** In Parties package mode, the pinned catalog resolves EventStore.Testing 4.2.0 for the attached lifecycle suite. That SDK rejects attached descriptors and lacks the lineage and expiry API the suite requires, blocking Story 2.10 in preparation.

**Approach:** Give the attached suite its own project identity on the pinned catalog's current default and point the declared profile, solutions and test controller at it. Keep the existing build path as a forwarding project for the legacy workflow and Platform preparation. Verify Release package builds with the latest released CLI and the committed catalog. Keep the fixture's existing access, expiry, and behavior checks.

</frozen-after-approval>

## Implementation Notes

- The real suite project uses the pinned catalog's current default by project identity. The attached profile, both solution entries, and the test inventory refer to that project. The old path forwards Restore, Build, Rebuild, and Clean for its existing callers.
- The lifecycle controller hashes the suite entry selected by the manifest before launch. The fixture still uses the shared SDK for authenticated service reads, hold expiry, and cancellation.
- In a disposable Parties checkout, Builds CLI `6.4.4+fe68718b6c074a6b500a7191e3554a1d39e17688` prepared the package imports against catalog `dc225a6969473c21c3dad6bda8f1a18ad0aaae0d`. The new suite, owner suite, and legacy path built in Release. The attached suite resolved `Hexalith.EventStore.Testing/5.4.0` and discovered its lifecycle test. Legacy Clean and Rebuild and the PowerShell project inventory passed.
- Seven declaration tests and 187 lifecycle controller tests passed with the committed Builds helper. `Hexalith.Parties.Tests` built in Release package mode, and `SourceAndTestProjectInventoryIsDocumentedExactly` passed. Story 2.10 runtime qualification remains open.

## Review Triage Log

- Patched the legacy forwarder to support Clean and Rebuild after reproducing `MSB4057` for both commands.
- Updated the project inventory check to distinguish the forwarding project from the attached suite, and to verify the suite stays in the runner-owned lane.
- Removed the duplicate controller project path so journaled entry hashes track the manifest before launch.
- Corrected the SDK prerequisite and solution build instructions for the opt-in suites.
