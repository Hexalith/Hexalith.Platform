---
title: 'Selected runtime diagnostics and regression coverage'
type: 'bugfix'
created: '2026-11-24'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Selected Platform runs retain ambiguous workload identity, report imprecise image and reader failures, and lack controls for arguments, digest binding, and invalid preparation inputs.

**Approach:** Close BH3-P8, P9, P10, P11, P13, P15, P16, VG3-1/VG3-2/BH3-P6 (one entry), VG3-3, and AA9+ECH5 in the Platform deferred-work ledger. Keep the existing composition and verification entry points. Verify the batch and push it to main.

</frozen-after-approval>

## Implementation Notes

- `PlatformRoutingCatalogBootstrap` records the selected OCI manifest SHA-256 in `artifactSha256` and the project-file hash in `projectFileSha256`. Composed hosts in either mapping mode retain the underlying server project.
- `PlatformReleasedImages` identifies the reference that failed verification. `PlatformRoutingCatalog` names the failing build mode and points to its existing log.
- The current-owner test composes an unstarted model with registry-shaped fixture responses and checks the native launch arguments, image annotations, and retained closure. Native preparation exercises refused files and catalog closures through its entry point. The tuple profile includes all eight new cases.

## Review Triage Log

- Medium. Separated the project-file hash from the artifact digest in the closure JSON and asserted both fields for projects and images.
- Medium. The reader diagnostic now identifies an unstarted process or a timeout directly. A failed build still gives the retained log path.
- Medium. The current-owner test now checks the reader artifacts from one actual composition, using one startup deadline.
- Medium. Added independent host-count, duplicate-resource, unknown-resource, and wrong-app closure cases.
- Low. Added malformed manifest JSON after digest selection to exercise the exception handler.
- Low. The source seed check uses the production closure serializer with a source binding. The existing source composition control already asserts that the resolved mapped project reaches that binding. The two controls cover the change without adding a second source composition fixture.

## Verification

- The Release build and 71 focused .NET cases passed with the lock-verified and repository-signed Builds 6.6.2 tool.
- The current-owner composition check passed in a clean Platform checkout with its recorded direct dependencies. Registry responses were fixture data and the composed workloads were never started.
- The 25 tuple-profile and collector tests passed. The Platform ledger moved from 543 to 533 open entries, including 40 open legacy bullets.
