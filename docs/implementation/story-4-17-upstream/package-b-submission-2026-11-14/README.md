# RabbitMQ Package B submission proposal

This is a local proposal for the reviewed [Package B contribution](../package-b-local-2026-11-14/README.md).
It is ready for an owner decision about publication. It has not been pushed
or submitted, and does not authorize a remote write.

The proposed operation is one upstream PR targeting
`dapr/components-contrib:main`, from the existing fork
`jpiquot/components-contrib` on the new branch
`fix/rabbitmq-mandatory-returns-4765`. Preserve Package A's existing commit
and add exactly one DCO-signed Package B commit with the approved message.
Do not alter #4767 or comment on, close or label any existing issue or PR.

## Exact approval material

| Item | Reviewable bytes |
|---|---|
| PR title | [pr-title.txt](pr-title.txt), final newline omitted when sending |
| PR body | [pr-body.md](pr-body.md), exact UTF-8 contents |
| New commit message | [commit-message.txt](commit-message.txt), including signoff and original source coauthor |
| Package B-only source | [package-b.patch](package-b.patch), identical to the reviewed contribution |
| Package B-only statistics | [package-b.stat.txt](package-b.stat.txt) |
| Complete upstream PR source diff | [upstream-main.diff](upstream-main.diff), includes unchanged A and new B |
| Complete upstream statistics | [upstream-main.stat.txt](upstream-main.stat.txt) |
| Hashes, identities and destination | [proposal.json](proposal.json) |
| Source and retained-receipt verification | [source-verification.json](source-verification.json), [receipt-revalidation.txt](receipt-revalidation.txt) |
| Reproducible verification procedure | [verify.py](verify.py) |
| Verification with retained command outputs and assertions | [reverification/verification.json](reverification/verification.json) |
| Dated upstream observations | [upstream-observation.json](upstream-observation.json) |

## Dependency choice

Package A [#4767](https://github.com/dapr/components-contrib/pull/4767) is still
open at `96c3259b7c12a476edd208848153d3cf72a8c4f0`, while upstream main is
`440b15349565bb15f7a88636477864e5a9f8cc72`. Six workflows require maintainer
approval (`action_required`), and the discussion has no comments. This is a
dated observation, not an acceptance or prediction of merge.
The connector query and [direct API readback](workflow-head-readback.json)
bind all six workflows to A's exact head; each is a `pull_request` run.

**Proposed publication:** submit the dependent A+B branch to upstream main
with the dependency disclosed prominently. GitHub's upstream PR diff will
include both packages. Maintainers should resolve A first; B then needs a
rebase before merging. Publishing a PR into the fork's A branch would show
only B, but would not submit it to upstream. This proposal does not claim
GitHub can target an upstream PR at a branch existing only in another fork.

**Alternative:** wait for A to merge or for maintainer direction. If the
dependency or base changes, prepare and approve a fresh exact diff/message
packet before publication. The present proposal is bound to its recorded
main, A and tree identities; it is not permission for an arbitrary rebase.

## Evidence and limits

The preparation job used `run-with-temp` to fetch the official base by full
SHA, load the retained A bundle, apply B to a private index and check the
resulting tree against `50f8ef2d3d3a240595d6088917f9d51afe04ec3c`.
Both retained diffs independently reconstruct that exact tree. No source
change or new test execution occurred. The original 15 check receipts and
18 negative controls were revalidated in a disposable copy; its recomputed
summary was byte-identical to the historical summary. Historical logs and
receipts were not overwritten. [workspace-cleanup.json](workspace-cleanup.json)
records that the job returned and both disposable directories were removed.

To repeat this verification without overwriting this packet, from the project
root run `run-with-temp python3 -B docs/implementation/story-4-17-upstream/package-b-submission-2026-11-14/verify.py --output <new-results-directory>`.
Choose a new results directory outside the temporary job workspace. The
script records command output hashes, tree outputs, assertions and the
receipt-revalidation command; it rejects an existing results directory.

The proposed public commit object has not been created. Its identity must
be captured later, with parent A, the reviewed tree, exact message/signoff,
author and committer `Jérôme Piquot <jpiquot@itaneo.com>`
and no additional source changes verified before pushing. The old test
commit's SHA is evidence of the tested subject, not the future public head.

Before any remote write, re-read the owner-approved packet; recheck main,
A, #4765, competing changes, the authenticated account's fork access and
branch/PR absence. Create the new branch atomically with a create-ref operation
that fails if it already exists, or a Git push with an explicit lease requiring
the destination ref to be absent. A preflight absence check alone is insufficient;
never update a concurrently created ref. Any base, patch, text or tree drift requires a new owner
decision. If a write partially succeeds, retain its identity before the
next action. Read back the created head, tree, parent, exact commit message,
PR destination/title/body and dependency after publication. No automatic
retries with changed content are authorized by this proposal.

No broker or Dapr runtime execution, upstream acceptance or delivery gate
is claimed. Both parent stories remain in-progress; technical qualification
is FAIL (20/24), five guarantees REJECTED/open, selection null and production
eligibility FAIL. Kafka stop rules, packet custody and the December 13
Administrator review remain unchanged. Re-entry still requires an official
supported Dapr release embedding the fixes, EventStore exact-input admission,
Builds alignment and a separately accepted Platform full-matrix proposal.
