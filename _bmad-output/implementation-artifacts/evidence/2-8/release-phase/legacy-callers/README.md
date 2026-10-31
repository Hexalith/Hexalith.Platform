The four legacy callers are prepared as normal pushed PRs. Story 2.8 remains
in progress; caller merges and global workflow activation remain unexecuted.
These compatibility lanes provide no integration or candidate acceptance.

Each jobs.ci.uses reference is pinned to the exact existing Builds workflow at
1cc94bbb932be0d08a31b4e1393fcfc450a0b1c9. Producer push CI 41226277095 completed
successfully on that exact main revision; producer-push-run.json and the retained
legacy-domain-ci.yml bind its source. checkpoint.json records full owner/base/head,
workflow blob/SHA-256, before/after identities, checks and source-preservation proof.

| Caller | PR | Head | Scope |
| --- | --- | --- | --- |
| Commons | https://github.com/Hexalith/Hexalith.Commons/pull/51 |6820f0a0365636284aa5c160b23a6d4373033b9f| One workflow reference |
| Folders | https://github.com/Hexalith/Hexalith.Folders/pull/15 |35b1228a6b7832dd92b9ca255b4c710c1b5a1430| One workflow reference |
| Conversations | https://github.com/Hexalith/Hexalith.Conversations/pull/11 |21dd4bdc7430e11e348675ca4fa5d1c34131c781| Workflow reference and its matching security-test expectation |
| Tenants | https://github.com/Hexalith/Hexalith.Tenants/pull/58 |5902a22e416f8c36f9de1365d9b9dd5b3555a22e| One workflow reference |

All four actionlint 1.7.12 checks pass without exclusions. Exact byte comparison
and parsed YAML equivalence prove every other caller input, job, permission,
timeout, test selection and package/tool pin is preserved. Every existing input
is supported by the pinned legacy interface, which requires no registry secrets.
Each owner lockfile resolves commitlint 21.2.4; exact commit/title/body/squash and
committed ranges passed that owner policy before the normal push/PR.

Conversations initially failed its existing literal @main security assertion.
The coordinator approved the matching exact-ref correction and clarified the
spec note. Its existing assertion now requires the full approved uses line; all
other assertion bytes and initialize-build@main are unchanged. The focused case
and all 28 existing release-tooling tests pass on Linux. The initial failing
control is retained. No hosted Windows result is inferred from that local run.

The observed remote state contains actual blockers. Folders cannot fetch its
unchanged root Builds gitlink 15d6a10c54660153cf8c05f24db83215824faf8e from origin
(not our ref), stopping shared and independent jobs. Conversations' unchanged
Windows checkout rejects a historical ':' cache path, and its minimal-module
metadata-selection test reports dotnet msbuild exit 1 without child output.
Both repositories' dependency review reports Dependency graph disabled.
Per-owner failure-observations.json, bounded job excerpts and full-log SHA-256
receipts retain the exact source/job links. Commons and Tenants had no completed
failures at the recorded snapshot; their complete CI results remain pending.
No job, source gitlink, repository setting or gate was changed to bypass these
failures. No unchanged workflow run was retried or another owner's run cancelled.

Original Commons/Folders/Conversations/Tenants checkout heads, branches, indexes,
workflow bytes, status and captured dirty source hashes remain unchanged. Fetched
owner mains and isolated branches preserve independent incoming owner changes.
Only the new branches/worktrees and compatibility PRs were created. Shared staged
Builds/EventStore/Parties implementation and credential preparation remain untouched.

commands.txt records reproducible focused commands. Per-owner checkpoint.json
and the combined checkpoint.json are point-in-time observations. SHA-256 inventory
files bind the retained source, messages, validation outputs and check snapshots.
