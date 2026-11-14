# Package B submitted and verified

The owner answered **“yes”** to submitting the exact proposed dependent A+B
upstream PR. [PR #4768](https://github.com/dapr/components-contrib/pull/4768)
is **OPEN**, targeting `dapr/components-contrib:main` from
`jpiquot:fix/rabbitmq-mandatory-returns-4765`.

The public B commit is `c6ba13793f181461afb3c74c9300f5876879861f`, with
sole parent A `96c3259b7c12a476edd208848153d3cf72a8c4f0` and reviewed tree
`50f8ef2d3d3a240595d6088917f9d51afe04ec3c`. Its raw commit, exact message,
author, committer and incremental source diff were fetched from the remote
and matched locally. The exact approved PR title/body, base/head identities,
non-draft/open state and complete A+B scope matched the GitHub readback:
two commits, eight files, 2786 insertions and 72 deletions.

The [submission receipt](publication/submission-receipt.json) retains approval,
preflights, partial results and verification. The branch was created with an
absence lease, preventing updates to any pre-existing ref. The GitHub connector
could not create the PR (403, resource not accessible by integration); a direct
check confirmed no PR had been created, then the authenticated `jpiquot` GitHub
CLI submitted the same approved text successfully. There were no other remote
writes. Both disposable jobs finished and their workspaces were removed.

The [public commit bundle](publication/package-b-public.bundle) retains B with
A as its prerequisite. The [original proposal](proposal.json) and its 17 bound
artifacts remain unchanged as the approved historical packet; this submission
record supersedes their pending-publication wording.

Package A [#4767](https://github.com/dapr/components-contrib/pull/4767) remains
the dependency. The upstream PR currently includes A+B. Dependency resolution
and any later rebase require a newly checked exact diff and approval before
updating the branch. No existing issue, PR, comment, label or A branch was edited.

Submission is not upstream acceptance or supported Dapr delivery. Platform
4.17 and EventStore 3.21 remain in-progress, technical qualification FAIL (20/24),
five guarantees REJECTED/open, selection null and production eligibility FAIL.
No broker, Dapr runtime or qualification was run. Official supported delivery,
EventStore admission, Builds alignment and a separately accepted full-matrix
proposal remain required. Kafka stop rules, packet custody and the December 13
review are unchanged. Platform and EventStore were not committed or pushed.
