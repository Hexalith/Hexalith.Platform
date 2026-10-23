# Approved Aspire 13.6.3 alignment — 2026-10-23

The user approved upgrading the root file AppHost to the catalog's existing Aspire 13.6.3 selections. The SDK, Docker, Kubernetes, Redis and Keycloak directives now match those selections. CommunityToolkit Dapr and EventStore package pins remain unchanged; no catalog version was changed.

The direct file-entry smoke passed: one test, zero failed/skipped, 152.089 seconds. It starts the actual file AppHost, waits for all six declared servers to reach readiness, and asserts successful owned shutdown. The [result](result.json) and [full test output](file-entry.log) establish the passing gate. The original AppHosts match exactly in [before](applications-before.json) and [after](applications-after.json) inventories, and the [source hashes](source-before.json) stayed stable through the test ([after](source-after.json)).

The [exact command](commands.txt) uses the private Aspire CLI 13.6.3 installation and Dapr 1.18.7 tool home prepared during the preceding qualification. Global tools were preserved. The previously passing ordinary suites, occupied-port proof and strengthened concurrent proof are recorded in the [final verification index](../README.md). No verification check was suppressed, foreign application stopped or gitlink staged. Nothing was pushed by this workflow.

The Works harness correction is recorded locally as `3fcaa32f44bc71d4d077e031252e77fd29390277`; its tested file hashes match the preceding verification. The root gitlink is preserved under the story's explicit constraint.
