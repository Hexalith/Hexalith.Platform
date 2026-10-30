# Review round 6 — human escalation

The build remains incomplete. Fourteen individual verdicts were recorded before grouping: six bad_spec entries, six lower patch entries, one carried false/reject and one low/reject. There are no deferrals or unresolved intent ambiguities. Every surviving entry is separate; proximity in a file is not used to merge defects.

The bmad-build skill instructs: "On success, read and follow the one absolute workflow.md instruction printed to stdout." Its already-rendered review workflow explicitly says: "If it exceeds 5, HALT and escalate to the human." The counter advances from 5 to 6, so no sixth correction iteration or lower patch is started. This stop follows the rendered rule, not an inferred approval requirement.

- [Skill manifest](../../../../../.agents/skills/bmad-build/SKILL.md)
- [Exact rendered review rule](../../../../../_bmad/render/bmad-build/platform-fcb6e4841134/df57414dae3b284c0fb7/step-04-review.md)
- [Spec and individual triage rows](../../../../../_bmad-output/implementation-artifacts/spec-ext-protection-1-production-engine.md)
- [Machine-readable verdicts, grouping and routes](triage.json)

The six entries requiring re-derivation are:

1. R6-BH-4: erased identities, manifest/lifecycle history and guard pointer indexes still grow inside the bounded authority head and eventually prevent tenant protection and destruction.
2. R6-BH-7: graceful host shutdown can discard the only process-owned completion evidence for already-cleared holders, leaving destruction permanently blocked.
3. R6-BH-8: large independent source originals use 4 MiB raw chunks that exceed the actual Dapr transport default after JSON/base64 framing; compatibility, framing and real transport verification need coherent treatment.
4. R6-EC-1: whole-snapshot replay metadata retains arbitrary confidential sibling fields outside encryption, so root erasure cannot remove them.
5. R6-EC-2: supported opaque whole snapshots have no typed-Erased replay representation after manifest destruction or permanent custody loss.
6. R6-VG-1: successful shipped bootstrap apply is not validated through the real occurrence/deletion/lifecycle anchored-owner consumer. Its filed patch disposition was considered; the required cross-language integration is nontrivial, so the workflow routes it to bad_spec.

The lower patch entries remain unexecuted: event-type validation before permanent-loss replay; complete mixed-snapshot metadata equality; bootstrap string identity validation; archive chunk bounds before retention; bounded pinned-file acquisition; and the newly exposed full Secrets test-project CS0436 collision. That collision is caused/exposed by making the Server helper public; the earlier pre-existing attribution is corrected. The retained exact failed command and full log establish five errors, while the separately selected 58 existing adapter/failure tests pass.

Dapr documents a default 4 MiB request/response limit for HTTP and gRPC in [Handle larger body requests](https://docs.dapr.io/operations/configuration/increase-request-size/) and [arguments/annotations](https://docs.dapr.io/reference/arguments-annotations-overview/). The 5,592,410-byte full-chunk JSON size is a calculation from the shown byte-array serialization; the failure under default transport limits is an inference from the implementation and documented limits. No unverified claim about a specific SDK receive-limit override is needed. The delivered graph establishes no larger sidecar configuration.

The withdrawn pair is Platform b1e5bbefa129ca205e6618e01451ff77710ae6fc and EventStore 6a26fa86be77eb9745b3b839dab871acead8651a. Both local refs remain intact as refs/ext-protection-1/source-20261030-loop5-review. The unaccepted 2026-11-02 date is withdrawn with the pair. Accepted profile/target/date/availability remain unset; these objects must not be selected for owner acceptance as currently implemented.

Preserve the current implementation and every positive result for any later authorized correction: native successor/trim recovery, real actor/source bindings, exact original retention and restrictive unknown-holder behavior, restricted shipped policies, all four frozen matrix rows, strict 41-case source inventory, and all earlier KEEP constraints. No source rollback, re-derivation, staging, branch/ref movement or remote operation occurred during this review triage. The original reviewed diff is retained externally at the path and SHA-256 recorded in triage.json. Raw blind/edge/gap outputs accompany this file; pre-review parent documents and their inventory remain under pre-review/.

All 18 final selected parent checks pass, including Protection 465 executed/six explicit native-only skips, lifecycle/revocation 68, existing Secrets adapters/failure 58, EventStore 444, payload 897, offline verifier 116 and harness/bootstrap 12. Retained contracts anchors pass 14. Separately frozen native-v7 passes 5/5 with no failures/errors/skips, full 1,000 targets, actual 9 MiB state, restricted policies and verified cleanup. These checks do not cover the newly verified defects; no pass is relabelled as complete acceptance.

Concurrent shared branches advanced independently. A final check of all 268 files listed by the two candidate records finds every functional file unchanged; the sole mismatch is an unrelated appended Directory.Build.props whitespace rule in Platform .gitattributes, which is preserved. Exact heads/statuses are in final-source-check.json. Frozen intent and the external register still match their original pinned hashes.

Full physical-copy qualification and the independently installed 41-case run remain external and unestablished. No Live/OQ/Agents consumer, register edit, cluster/provisioning or production operation occurred. Production destruction remains independently gated.

Continuing requires an explicit human instruction to exceed the workflow's review-loop limit. That decision has not been inferred from the earlier resume after model-capacity interruption.
