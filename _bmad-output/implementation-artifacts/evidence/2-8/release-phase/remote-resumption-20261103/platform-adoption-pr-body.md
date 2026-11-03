Package-mode composition still records older declaration snapshots while the selected catalog now uses EventStore 4.0.4, Tenants 6.0.0 and Memories 3.0.18. Import the exact official EventStore/Tenants declaration bytes and update all three immutable source/hash records. Memories declaration bytes are unchanged.

The owner-refusal regression now sets an explicitly unqualified version in its isolated fixture, preserving refusal before any resource allocation as real snapshots advance.

Depends on Hexalith/Hexalith.Builds#153 (release declaration bindings) and Hexalith/Hexalith.Builds#154 (authenticated dependency test metadata). The local checks below use the unpublished compiled correction and do not qualify the accepted published tuple, runtime cohort or hosted lifecycle.

Validation:
- Release/package Composition.Tests project build: 0 warnings/errors.
- 18 focused tests passed, 0 failed/skipped: invalid provenance refusal, catalog images, participant binding and native preparation.
- Adopted snapshot bytes and provenance match the inspected official release archives.
- Accepted tuple and tool pins are unchanged. The existing CI tuple preflight still refuses recorded Builds gitlink fc743715 versus approved producer 6585f586; normal released-tool/tuple adoption and hosted acceptance remain pending.
