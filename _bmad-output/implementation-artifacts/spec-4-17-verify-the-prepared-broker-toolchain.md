---
title: 'Story 4.17 slice: Verify the prepared broker toolchain'
type: 'chore'
created: '2026-11-26'
status: 'done'
route: 'oneshot'
story_key: '4-17-select-and-qualify-the-durable-production-broker'
baseline_commit: '332de5fd4fa84224dba5ffd830d38c8d38875747'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="existing approval for Dapr 1.19.2 preparation and the user request to fix hosted admission">

## Intent

Resolve the verifiable toolchain dependencies from the blocked hosted broker admission. Bind EventStore's committed Builds pin to the published package, embedded catalog, resolved SDK packages, prepared profile and measured Dapr executables. Evaluate RabbitMQ delivery, Kafka evidence and capture admission against Story 4.17's existing acceptance criteria. Preserve Candidate-02's historical 20/24 outcome and keep Story 4.17 in progress until a successor earns qualification.

</frozen-after-approval>

## Implementation Notes

- Record the current evidence in the [Platform intake](../../docs/implementation/story-4-17-next-candidate-intake.md#1192-hosted-admission-recheck--2026-11-26) and update the sprint tracker comment. The exact EventStore and Builds source revisions identify this preparation check.
- Both upstream RabbitMQ fixes are open. The prepared EventStore profile validates the exact inputs but refuses capture. The existing Kafka pressure rejection and rotation attribution requirements still apply. No hosted attempt was dispatched.
- Restored the committed EventStore tools and solution against NuGet.org in disposable storage. Matched the Builds archive and tooling assembly to the release publication record, read the exact embedded catalog with the pinned Builds `Tools/ToolingProbe.cs`, and matched all six resolved Dapr SDK package hashes. The official profile installer validated both Dapr archives and extracted binaries.
- In the EventStore archive, `python3 -B -m unittest discover -s tools/tests -p <suite>` passed `test_dapr_broker_execution_profile.py` (6), `test_dapr_broker_qualification.py` (25), `test_dapr_platform_broker_configuration.py` (32) and `test_dapr_runtime_qualification.py` (102). In Platform, `run-with-temp node --test tests/source-release/broker-candidate-workflow.test.mjs` passed 53 fixtures.
- Replayed Candidate-02 from the provider ZIP with its original verifier and configuration. All three exits and stdout commitments matched the retained run and stderr was empty. Validated the new local links, unchanged sprint status values and changed-file whitespace.

## Review Triage Log

| Finding | Verdict and resolution |
| --- | --- |
| Tracker could read the initial Builds blocker as current | Medium. Labeled the initial intake and linked the recheck next to it. |
| Final dispatch pin and SDK obligations needed explicit scope | Medium. Added EventStore gitlink, Platform catalog reconciliation and exact SDK content verification. |
| Profile refusal needed a reproducible check | Medium. Added the tested validation and refusal commands, expected diagnostic and absent-packet assertion. |
| Kafka rotation lacked the earlier-work condition | Medium. Restored completion of prior work or supported per-request discrimination on the same live publisher. |
| RabbitMQ rotation could still receive the client's generic 403 | Medium. Documented the Dapr denial evidence required on the actual deleted-user path. |
| Test counts lacked commands | Low. Added the four EventStore suites and Platform workflow fixture invocation. |
| Publication record hash was ambiguous | Low. Named the asset and linked its release API digest. |
