# Story 2.8 approved release phase

Status: in progress. Necessary prerequisite release/PR work and hosted qualification
are approved. Normal source, review, provenance and publication gates remain mandatory.
No candidate acceptance or completed hosted reference qualification is claimed.
Original local-phase records and superseded source checks remain historical.

- [Builds foundation PR148](https://github.com/Hexalith/Hexalith.Builds/pull/148)
  normally merged as `57255b502fdd546bb09851d04fc1830175d897f3` after every required
  check and full PR CI passed. Its main push then exposed one concurrent-down test
  assumption. Incoming normal PR150 corrected that test according to the existing
  HXR037 same-owner retry contract; main then became
  `f6834a33967f8ec905145db14e1a13ffb5468d69`. The failed main result and provisional
  wait implementation remain historical evidence; the provisional runtime change
  is withdrawn from the final producer candidate.
- [Builds PR149](https://github.com/Hexalith/Hexalith.Builds/pull/149) now has source
  `fb949ec9c41587279b1e153a3f9d00c7ad3cfc37`, a normal branch merge preserving
  current main and its retry tests. Its final 16-file diff adds strict
  `domain-ci-v2.yml`, separate identity and direct-helper closure binding, dual
  validators, HTTPS restrictions and documentation, plus one 49-line active-hold
  refusal/retry case. Production `src`, legacy workflow/docs and incoming spec are
  identical to main. Local verification passes 59 local, 16 CI and 3 legacy cases,
  with no failures/skips/errors; fresh Debug build has zero warnings/errors.
  The 15 v2 producer files are unchanged from reviewed 26ba source (411 legacy,
  254 strict, 95 provenance, 56 governed workflow, 11 preparation and 24 unchanged
  helper controls). All exact current-base hosted checks passed. PR149 normally
  squash-merged as `d8d0d70f26d79c95b544a64a74e329149711cb7d`; reviewed tree/message
  and source preservation were verified. Actual hosted module result is
  2,666 passed/zero failed/five existing skips. Exact-main CI41246834980 is running
  before tool publication. See
  [reconciliation evidence](current-main-reconciliation/README.md) and
  [independent tree/source checks](cleanup-contention/coordinator-reconciled-source-fb949.json).
- [SDK helper PR378](https://github.com/Hexalith/Hexalith.EventStore/pull/378)
  normally merged as `1de42b706f52af0da6513f0b7663002606d17156`. All seven required
  current-base contexts passed; real live-sidecar result was 304 passed, zero failed,
  four existing skips, plus serialization 1/1. The exact reviewed tree and linted
  message match [the merge record](eventstore-sdk/normal-merge-result.json).
  Exact new-main CI41244694290 and commitlint41244694281 passed; actual CI has
  14,863 passes/zero failures/49 existing skips. Helper prerequisite Release
  [41246774822](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/41246774822)
  completed as official **3.130.1** at exact1de after successful source preflight
  and normal protected production review. Tag/source, OCI content identities,
  provenance labels and both platform smoke/cleanup results are verified in
  [the release receipt](eventstore-sdk/helper-release-completed-31301.json). All
  fourteen [official NuGet archives](owner-packages/eventstore-3.130.1/inventory.json)
  now match ID/version/source and have valid signatures; the testing assembly
  contains the stream helper. Initial feed propagation failure remains recorded.
  No caller pin has changed.
  The separate historical 3.128.2 matrix
  job reports green while its real result is 49 passed/1 failed; its tee pipeline
  masks the exit. [Actual failed evidence](eventstore-sdk/published-compatibility-actual-result-2ed.json)
  remains unqualified; no expectation or gate was changed.
- The prior waiting SDK release 41238371177 completed externally as 3.130.0 at
  `484f4bd82fd2ef4b6c2653fdde57feeeb6d85a3c`. All 14 official family archives match
  identity/version/source and have valid signatures; see
  [the inventory](owner-packages/eventstore-3.130.0/inventory.json). This version
  predates the newly merged stream helper. Separate owner catalog
  [PR380](https://github.com/Hexalith/Hexalith.EventStore/pull/380) remains active;
  source/cohort coordination and actual normal release proof are still required.
- Already-published Tenants 6.0.0 and Memories 3.0.13 retain 15 verified signed archives
  in [the family inventory](owner-packages/inventory.json), with exact released
  declaration bytes/tags in [the snapshot inventory](owner-snapshots/inventory.json).
  Package signatures do not establish combined runtime/image compatibility.
- Commons51, Folders15, Conversations11 and Tenants58 compatibility-pin PRs were
  closed unmerged after the versioned replacement was prepared. Existing callers
  retain their legacy contract and supply no Story 2.8 acceptance.
- [Parties preflight](parties-preflight/README.md) confirms the real service and
  EventStore adapter build in supported Release/package mode with zero warnings/errors
  against official SDK3.130.0, Tenants6.0.0 and Memories3.0.13, without source fallback.
  Seven already-recorded gitlinks need actual owner ratification; [the exact proposal](parties-preflight/owner-selection-proposal.md)
  was explicitly ratified by the user. Its ledger correction is queued after registry
  preparation. Full current-source CI and normal release gates
  remain necessary. The new adapter remains unpublished.
- [Registry preparation](registry-preparation.json) retains explicit pull-only
  secret wiring and locally verified private Docker config removal. The user selected
  the dedicated ci-reader account. A reviewable exact provisioning artifact is being
  prepared; existing workload/publisher credentials are not reused. No registry
  account/configuration/credential provisioning or real authenticated pull/write-
  denial qualification has occurred. Reference caller adoption remains pending.

Current machine-readable checkpoint: [coordinator.json](coordinator.json).
Compatible helper/tool/owner releases, exact reference adoption, image provenance,
real cancellation/partial-startup and verified withdrawal/zero-leftover observations
remain necessary before story completion. The helper prerequisite release is now
dispatched as recorded above. No registry mutation or accepted consumer pin change
has been performed by this phase.
