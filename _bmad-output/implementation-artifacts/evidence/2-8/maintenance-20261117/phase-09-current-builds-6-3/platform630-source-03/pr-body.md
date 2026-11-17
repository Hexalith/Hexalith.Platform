Bind the supported Platform 2.x line to the signed Builds 6.3.0 release: exact tool pin, range floor, publication handoff, catalog and Builds gitlink. Update the current preparation documentation.

Normal recorded artifact preparation now acquires a private checkout of the bound producer, preserving exact committed catalog bytes despite the producer’s normal CRLF checkout. The final guard rechecks that same producer, and the temporary checkout is removed on exit. Register the real Git regression in the tuple gate.

Validation: tuple/publication/bound-producer, collector/runtime/source controls and source-release tests passed; npm signature audit and commitlint passed. Real public release metadata, NuGet signature, package/executable identity, unchanged owner/source and scratch cleanup passed through the normal artifact-preparation command. The other 17 direct dependency pins are unchanged. Hosted tuple and six SDK gates must pass normally.

Story 2.8 cancellation-order and cleanup qualification remains open; these artifact and source checks grant no native acceptance.
