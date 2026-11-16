# AD-26 profile and ratification preparation

Status: **draft for joint EventStore/Platform review; both ratification records are unissued**.
This package proposes the template/binding split requested by Platform 4.9 and
EventStore 3.19. It supplies review documents and their content digests. It does
not supply a qualified runtime, accepted inventory, owner authentication or
production authority. Platform 4.9 and 9.2 remain open.

## Start here

| File | What to review |
| --- | --- |
| [Profile template](profile-template.draft.json) | Proposed stable runtime, component, sidecar, ACL and resiliency contract; required inventory categories and unresolved owner inputs |
| [Release binding contract](release-binding-contract.draft.md) | Template versus provider pins, release bindings and the complete resolved EventStore inventory; distinct digest domains |
| [Target decisions](selected-target.draft.json) | Technical selections still pending, retained candidate references and the required qualification inputs |
| [Architecture role draft](architecture-ratification.draft.json) | Exact architecture subject and review documents to which a future Architecture decision would bind |
| [Platform deployment role draft](platform-deployment-ratification.draft.json) | Separate deployment-role decision over the same subject and review documents |
| [Review index](review-index.json) | Source identities, exact file hashes and the canonical ratification-subject hash calculated for this preparation |

## Decisions and dependencies

| Topic | Available repository basis | What remains required |
| --- | --- | --- |
| Template and release binding | The proposed split below preserves Platform's infrastructure-only profile identity and EventStore's complete resolved inventory | Joint agreement on this draft contract and its byte encoding; downstream implementation/validation |
| Broker and Dapr runtime | Retained candidate: Dapr 1.19.1, embedded components 1.19.0; RabbitMQ 4.3.6-management or Kafka 4.3.1. These are dated experiment inputs | A supported, exact-input candidate passing all 24 vectors and explicit owner selection; no broker is selected |
| Broker qualification | Latest credited candidate-02: 20/24 PASS, four FAIL; five historical guarantee dispositions remain REJECTED/open | Preserve every vector and adjudicate the historical failures; supported remediation and a separately accepted successor run |
| Append protection | The accepted engineering scope is mandatory Dapr-owned durable revision, create-once event identity and atomic whole-batch protection, preserving lawful erasure | A qualified SDK/runtime/component implementation and application conformance; scope approval is not AD-26 ratification |
| Restore posture | EventStore 7.22 defines required state coverage and restore invariants | An actual posture, exact digest and the required profile-specific restore proof |
| Shared/environment inventory | Platform 4.9 and the published currency policy define coverage | Actual provider pins, topology, management/workload pairing, support/advisory sources, renewal/currency owners and qualified identities |
| Issuance and assurance | AD-26 requires separate Architecture and Platform deployment records | Authenticated owner-role registry, authentication evidence, verified push-time anchor and the consuming transition's assurance evaluation |

The candidate versions above are references to retained repository experiments,
not current upstream-version recommendations. The source-owned
[broker intake](../../implementation/story-4-17-next-candidate-intake.md)
records selection as null and production eligibility as FAIL. Its accepted
engineering priority is supported RabbitMQ error propagation first, then
mandatory returns; contribution submission alone is not supported delivery.
This package starts no upstream request, release, qualification run or deployment.

The current review can accept or revise the **draft contract design**. Both
role-specific ratifications remain unissued while technical selections and
qualification are missing. A generic approval of these drafts cannot select a
broker, attest missing proof or turn either draft into an authenticated record.

## What the owner provides

After the technical inputs exist, the owner reviews the exact selections and
issues a separate authenticated record for each registered role. One human can
hold both roles. Each record names the broker, runtime pin, restore posture and
qualified append-protection path and semantics, and binds the exact AD-26 subject
and supporting content identities. Authentication and registry references remain
null here; this package invents no approved record schema or signing mechanism.

The canonical architecture requirements are in
[AD-26](../../../references/Hexalith.EventStore/_bmad-output/planning-artifacts/architecture.md#ad-26---production-runs-only-on-a-proven-fail-closed-profile-assumption).
AD-11's 24-hour separation starts at the authenticated push-activity time of the
latest push to EventStore `main` that changed the ratification subject to its
current digest. The drafts leave that anchor, earliest issuance time, actual
issuance and whole-file digest at issuance unset. A Git timestamp or this
package's preparation time is not that anchor.

Required assurance comes from the authenticated owner-role registry:
`single-maintainer-attested` for exactly one human; `independent` for two or more,
with the required independent human approval. The consuming sealed transition
checks the records and computes achieved assurance. The seal belongs to that
transition, not to either owner record. Both assurance levels remain unset here.

## Digests and updates

The review index hashes each listed package file's exact bytes with SHA-256,
excluding the index itself. Each role draft binds the exact README, template,
binding contract and target-decision bytes. Those drafts are not included in
their own reviewed-input lists. No circular digest or latest-file lookup supplies
an approval.

For the AD-26 subject, extract the AD-5 `Append race (NFR7 class (c)).` paragraph
through the line before its next empty line. Append, without a separator, the
AD-26 heading through its last nonempty line before the next `### ` or `## `
heading, preserving intervening empty lines. Remove only the heading's trailing
` [ASSUMPTION]` or ` [ADOPTED]` tag. Encode every included line as UTF-8 with
exactly one terminating LF, otherwise unnormalized, then hash those bytes.
The architecture whole-file digest is retained separately for traceability.

The index uses committed Git-blob bytes for source-file hashes and records the
full source revisions. Package hashes use the local draft bytes being reviewed.
The architecture working copy and its pinned Git blob must produce the same
ratification subject. A source change requires fresh extraction, source checks
and review; a draft change requires refreshing its dependent record/index hashes.
There is no operational validator or authority verification in this package.

The proposal's future digest encoding is described in the binding contract; it
is not selected by these preparation hashes. The eventual full resolved profile
has exactly one authorizing slot:
`Hexalith.EventStore/deploy/dapr/production-profile.yaml`, owned by EventStore
3.19's publication flow. This draft template cannot substitute for that file.

## Source contracts

- [Platform 4.9](../../../_bmad-output/planning-artifacts/epics.md#story-49-ratify-the-hosted-runtime-and-identity-event-contracts): profile inventory, catalog, secret contract, renewal owners and capture-lag decision.
- [Platform profile and binding classes](../../../_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md): infrastructure profile identity, release-owned values and controlled writers.
- [Currency policy](../shared-infrastructure-currency-policy-v1.md): component coverage, current support/advisory evidence, deadlines and approved-attempt naming.
- [EventStore 3.19 and 7.22](../../../references/Hexalith.EventStore/_bmad-output/planning-artifacts/epics.md): canonical resolved inventory and restore posture.
- [Approved append-protection scope](../../../references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-4-16-append-write-once-conformance-through-enforced-envelope.md): mandatory actor transaction and erasure semantics; delivery remains pending.
