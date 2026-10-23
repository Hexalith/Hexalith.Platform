# Story 2.3 resumed qualification — 2026-10-23

The required local composition gate passes on the recorded production source. Both normal Debug builds passed with zero warnings/errors, and no analyzer, source-identity, readiness or mTLS check was disabled.

| Check | Result | Evidence |
| --- | --- | --- |
| Final normal Composition build | 0 warnings/errors; 21.90 seconds | [build](final-build.log) |
| Normal Builds Module build | 0 warnings/errors; 27.33 seconds | [build](builds-build.log) |
| Platform and Timesheets ordinary tests | 196 passed, zero failed, six gated skips; 202 total | [suite](patched-suite.log) |
| Builds renderer/ports/plan/paths/workspace tests | 22 passed, zero failed/skipped | [runtime tests](builds-runtime.log) |
| Final cleanup unit checks | Three passed, zero failed; live case gated separately | [tests](final-cleanup-unit.log) |
| Direct file-entry live smoke | One passed, zero skipped; 115.514 seconds | [smoke](file-entry-patched.log) |
| Occupied-port live proof | One passed, zero skipped; 1.762 seconds | [proof](occupied-port.log) |
| Final concurrent six-server proof | One passed, zero skipped; 225.417 seconds | [proof](live-concurrent-verified.log), [receipts](live-concurrent-verified/) |
| Final ownership/source audit | Both run cleanups passed, no labelled containers, 24 native listeners per run with zero overlap; original AppHost unchanged | [audit](cleanup-audit.json) |

## Diagnosed file-entry failure

The first file-entry attempt reached actual resources and returned gateway HTTP 503. The gateway reported disconnected actor placement, with writer protocol inactive. `--isolated` moved the proxied placement/scheduler host listeners but native commands and scheduler advertisements retained the allocated ports. The diagnostic retry observed actual placement port 40721 versus sidecar address localhost:28591, and scheduler port 37001 versus localhost:27633. Sentry already bound directly and retained its assigned port.

Placement and scheduler now use the same direct binding as Sentry. After the correction, the actual placement port and native address both use 17691, and scheduler uses 18910; the gateway is healthy. The file smoke then verified all six declared servers and completed its owned stop. See [before/after diagnosis](file-entry-port-diagnosis.json), [initial gateway health](file-entry-gateway-health.json), [failed smoke](file-entry.log), [AppHost failure log](file-entry-apphost.log), and [passing listener observation](file-entry-patched-observed.json).

## Review corrections and cleanup

The retention fixture now mounts AddSentry's returned private credential child for placement/scheduler and verifies read-only credential mounts. The Timesheets subscriber fixture includes the application expected by the revised subscriber and selects its sidecar explicitly. The concurrent harness tracks every returned run identity, including failed starts, while preserving session-based stdin shutdown for successful starts. Aggregate cleanup still attempts every owner before reporting errors.

The intermediate [cleanup-channel attempt](live-concurrent-final.log) passed readiness, disjoint resource checks, persisted values and survivor checks but reported HXR028 during by-ID shutdown of a successful session. Its [receipts](live-concurrent-final/) show no resources remained. The final correction restores the successful session's stdin shutdown and uses by-ID retry only when no session was returned. The final 225.417-second proof passes all assertions and both cleanup receipts. The earlier [265.071-second proof](live-concurrent.log) remains an additional pre-correction receipt.

The final proof inventories all four actual native listeners for each of six services: [run 0](live-concurrent-verified/native-listeners-0.json), [run 1](live-concurrent-verified/native-listeners-1.json). [Persisted values](live-concurrent-verified/persisted-values.txt), [survivor readiness](live-concurrent-verified/survivor-readiness.json), [peer cleanup](live-concurrent-verified/peer-cleanup.json), and final cleanup receipts establish separate state and continued survivor health.

## Scope and broader-suite result

An initial unfiltered run discovered 256 cases: 238 passed, eight failed, ten skipped; see [original complete output](ordinary.log). Four failures concern unchanged GatewaySeedSubscriptionTests test-host content-root setup; three concern unchanged DomainEnrollmentKitTests release-pin checks. The eighth was the Timesheets fixture corrected here; the affected Platform/Timesheets group subsequently passed all 196 executed cases. The unrelated release pins and test-host fixtures were not changed or represented as a broad-suite pass.

Review covered the Story 2.3 composition/Dapr paths and directly supporting Builds runtime paths. The full baseline diff also includes independent repository changes; findings about their contract, claim lifecycle, Custody and evidence handling were recorded separately in the spec triage and deferred-work ledger. Existing external-credential crash cleanup remains a carried lifecycle deferral. No hosted data, descriptor migration, Works live qualification, tool publication or repinning is claimed.

[Canonical revisions and source hashes](verified-source-snapshot.json), [final correction diff](final-source.diff), [commands](commands.txt), and [tool versions](versions.txt) bind the results to the tested source. Dapr CLI is 1.18.2, native runtime 1.18.7, Aspire 13.6.1 and SDK 10.0.402. Only task-owned resources were stopped. [Before](before-applications.json) and [after](after-applications.json) application inventories match exactly; the original running AppHost remains intact. No gitlinks were staged by this workflow.
