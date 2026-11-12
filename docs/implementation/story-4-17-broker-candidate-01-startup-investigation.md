# Story 4.17 — candidate 01 startup-failure investigation

Investigated on 2026-11-13. The retained UTC observations were taken from
2026-11-12T23:50:04Z to 23:50:26Z, after exploratory read-only queries that started
around 23:30Z.
The subject is the single approved hosted attempt
[43097161643, attempt 1](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43097161643),
which ended in `startup_failure` with zero jobs, zero artifacts and 0/24 observed vectors.

**Classification: provider failure.** Organization-wide evidence strongly indicates it,
but it is not confirmed and the exact mechanism is unresolved. No workflow defect was
demonstrated, and no repository code changed. One new confounder was found that the
earlier receipts miss: the dispatch branch was deleted while the run was queued.

No workflow was dispatched, rerun or cancelled. Every GitHub request was an HTTP `GET`.
The original attempt's evidence is unchanged: all 79 manifest commitments (151,443 bytes)
were re-verified with the retained
[`verify-manifest.py`](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/verify-manifest.py).
New diagnostics are retained in
[candidate-01-startup-investigation](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/file-manifest.json).

## Identities

| Identity | Full value |
| --- | --- |
| Investigation baseline (Platform main) | `0c97b496f3d9a4fc9a82c656ff1c159fba54325c` |
| Original run source / head SHA | `94001fad925fdf7fa03a90b507b8f8e19074f9e3` |
| Original dispatch ref (now deleted) | `refs/heads/chore/broker-candidate-01-hosted-94001fad` |
| Workflow blob at source and baseline | `9130ed4243e3961326e984e8fd22e49b70695aa4` |
| Candidate SHA-256 (unchanged) | `472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc` |
| EventStore gitlink at source | `02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213` |
| Profile SHA-256 at that EventStore revision | `1c1da59176f78b9b60a87ba85b6076b8d215ff912840ecb52bdb7432497c587f` |
| Current main EventStore gitlink | `bb9e2b51fc8a609181077eba2ae900bcbe40745a` (main is not a valid source) |
| GitHub support request | `4ed78a69-de10-11f1-8d56-5705d8f2f5ff` |

See [source-identities.json](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/source-identities.json).

## Findings

### Provider failure window

43 runs across six Hexalith repositories, created between `20:29:41Z` and `20:52:47Z`
on 2026-11-11, are still `queued` with zero jobs. The repositories are Platform,
EventStore, Parties, Memories, Tenants and Builds. Allocation resumed for runs created
from `20:54:28Z`. Inside each repository's queued span, the only completed run is this
`startup_failure`.

In Platform, the six sibling runs are tuple-ci and catalog-builder push runs created
`20:32:49Z`–`20:44:07Z`; two of them are at `94001fad`. Catalog-builder's
`cancel-in-progress` group never cancelled its orphaned runs.

GitHub status incident [`dvy0t3mvh1lg`](https://stspg.io/jdjlq5zjyl48) (critical:
Actions degraded availability) was posted at `20:48:12Z`, mitigated at `21:07:24Z`, and
its backlog was fully processed at `21:21:32Z`.

Our run was queued from `20:37:28Z` to its terminal update at `20:41:54Z` (266,000 ms).
It carries GitHub's generic unexpected-error annotation, which names no workflow line or
policy. It is the workflow's only run and the repository's only `startup_failure`. See
[cross-repository-runs-window.json](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/cross-repository-runs-window.json)
and
[repository-runs-window.json](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/repository-runs-window.json).

### No workflow defect demonstrated

- actionlint 1.7.12 reports no findings on the execution-revision bytes or on the
  identical baseline bytes.
- The 53/53 executable controls pass. They ran from the baseline test file against the
  identical workflow bytes; the execution revision's test file differs only in its
  js-yaml import form.
- Actions policy allows all actions, does not require SHA pinning and defaults to read
  permissions.
- The same checkout and upload-artifact pins ran in catalog-builder.

The gh CLI's hint, "likely failed because of a workflow file issue", is a client-side
suggestion. GitHub returned no validation annotation to support it. That a rejected
workflow file would fail at creation rather than after queueing is general Actions
behaviour, not something tested here, and is listed as uncertainty.

### Unresolved confounder: dispatch-ref deletion

The paginated repository activity log (251 events back to 2026-11-06) shows this sequence
on 2026-11-11:

| Time (UTC) | Event |
| --- | --- |
| `20:32:47Z` | `main` moves to `94001fad` (PR #40 merge) |
| `20:32:49Z` | The PR head branch is deleted by `delete_branch_on_merge` |
| `20:37:25Z` | Dispatch branch created |
| `20:39:42Z` | Dispatch branch deleted by the repository owner account |

The deletion came 2m12s before the run's terminal update, while it was still queued.
That branch never had a pull request. The organization audit-log API returns 404, and
the account security log is not available through the API.

Platform is a submodule of 15 local superprojects, and concurrent agent sessions were
syncing them at that time. The committed `/pushall` procedure prunes every origin branch
whose tip is already merged into main, and `94001fad` is merged. A bounded local log
search did not identify the deleting process.

This run differs from its stuck siblings, which never terminated, in four ways:

- it is a `workflow_dispatch` run;
- it uses the `ubuntu-24.04` label, never allocated in this repository;
- it is the first run of a workflow registered at `20:32:47Z`;
- its ref was deleted.

Any of these may explain why only this run reached `startup_failure`. None is proven or
excluded. See
[dispatch-ref-lifecycle.json](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/dispatch-ref-lifecycle.json)
and [local-log-search.json](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/local-log-search.json).

### Remaining uncertainty

- GitHub has published no root-cause analysis. The incident's public start time is later
  than our terminal update, so the correlation rests on stuck runs that began by
  `20:29:41Z`. The latest completed run created earlier in these listings is from 17:34Z.
- The support request ID cannot be looked up through any public API.
- What happens to a queued run whose ref is deleted is undocumented.
- The deleting process is unknown.

The [classification](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/classification.json)
records each item.

## Commands and results

[commands.json](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/commands.json)
records all 47 queries. Each entry has its argv, endpoint, exit code, HTTP status,
`X-GitHub-Request-Id`, and the SHA-256 of the raw unredacted body. The queries came from
the retained
[collector](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/collect_startup_evidence.py),
which uses `gh api --include --method GET`, one paginated activity query and one public
`curl` of the githubstatus incident feed. It queried:

- the run, attempt 1, timing, jobs, artifacts and logs (404);
- the workflow, its runs and the repository's `startup_failure` runs;
- this repository's runs and their jobs for 19:30–22:30Z, plus the currently queued runs;
- runs from 17:00 to 21:30Z in six repositories;
- ref activity, branch state (404), matching refs, pull requests and the audit log (404);
- Actions permissions and rulesets `24657570`, `24657217` and `24657106`.

Logins and user IDs are replaced with a role label. The collector also writes
[workflow-trigger-survey.json](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/workflow-trigger-survey.json)
from local Git: no workflow triggers on tags at `94001fad` or at the baseline.

Offline checks ([offline-checks.json](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/offline-checks.json)):

```bash
node --test tests/source-release/broker-candidate-workflow.test.mjs   # exit 0, 53/53
actionlint -no-color .github/workflows/broker-candidate-qualification.yml   # exit 0, no findings
git show 94001fad925fdf7fa03a90b507b8f8e19074f9e3:.github/workflows/broker-candidate-qualification.yml \
  > SCRATCH/broker-candidate-qualification-94001fad.yml
actionlint -no-color SCRATCH/broker-candidate-qualification-94001fad.yml   # exit 0, no findings
sha256sum docs/implementation/story-4-17-broker-candidate-01.json   # 472bae6b…08dc
python3 -I _bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/verify-manifest.py \
  _bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/file-manifest.json   # exit 0, 79/79
git diff --check
```

actionlint ran without its shellcheck and pyflakes integrations. The executable
controls cover the embedded Bash and Python.

## Proposal for one further hosted attempt (not authorized)

[next-attempt-proposal.json](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/next-attempt-proposal.json)
is the authoritative proposal; this section summarizes it.

**Source.** The unchanged `94001fad925fdf7fa03a90b507b8f8e19074f9e3`, with the identities
above. No new commits and no inputs; the workflow is `workflow_dispatch` without inputs.

**Ref.** Recommended: a new tag, `qualification/broker-candidate-01/attempt-2-94001fad`.
- Ruleset `24657570` makes every tag non-updatable and non-deletable with no bypass.
- No workflow triggers on tags.
- So the ref cannot vanish while the run is queued, as long as that ruleset stays active.
- Creating the tag needs the owner's bypass under ruleset `24657217`.
- The tag is permanent and public.
- A re-created branch would be reversible, but brings the deletion hazard back.

**Go/no-go.** All of these must hold before dispatch:
- The owner explicitly authorizes the tag, one dispatch and the queue-timeout handling.
- githubstatus shows Actions operational with no open Actions incident.
- Rulesets `24657570` and `24657217` match the retained policy immediately before tag
  creation.
- The tag does not already exist.
- Workflow `415133929` is active, lists exactly one run and has none queued or in
  progress.
- After creation, the tag resolves to `94001fad…`, and `git show` at that revision
  reproduces the identities above.
- The tag-trigger survey still shows no tag triggers.

**Dispatch.**
`gh workflow run broker-candidate-qualification.yml --repo Hexalith/Hexalith.Platform --ref qualification/broker-candidate-01/attempt-2-94001fad`.

**Worker.**
- A standard GitHub-hosted `ubuntu-24.04` runner: Linux X64, ImageOS `ubuntu24`,
  Python ≥ 3.11 and openssl.
- A local `/var/run/docker.sock` daemon reporting linux/amd64, with no remote Docker
  selection.
- Egress to github.com releases (Dapr 1.19.1), ghcr.io/dapr, docker.io (Kafka, RabbitMQ)
  and quay.io (OpenBao 2.6.2).
- A 40-minute timeout, `contents: read` and 30-day artifact retention.

**Queue timeout.** If no job is allocated within 30 minutes:
- record the run as not-allocated;
- cancel it only if the authorization says so; otherwise leave it queued and report it;
- either way, the run consumes the single attempt.

**Expected verification.**
1. Exactly one new run: `workflow_dispatch`, head SHA `94001fad…`, `head_branch` equal to
   the tag, attempt 1, with job `candidate-01` allocated.
2. Download artifact `platform-broker-candidate-01-RUN_ID-1` to a fresh directory and
   inspect its archive paths before extraction.
3. The `outcomes.json` observations match the identities above.
4. The three independent verifier modes run with an isolated EventStore at `02a4d3b2…`
   and the original `--platform-config`.
5. Authority exits 1 with the exact pinned rejection.
6. All 24 vectors are assessed. Cleanup credit is given only with a verified receipt.
7. The tag and both rulesets are unchanged afterwards.

**Stop conditions.**
- An Actions incident is open: do not dispatch.
- The dispatch fails: no retry.
- No job allocated within 30 minutes: apply the queue timeout.
- The startup failure recurs while the provider is healthy and the tag is intact: the
  result points at a workflow- or account-side cause. Record the request IDs, contact
  GitHub Support and stop.

## Next owner decision

Choose one:
- authorize the tag, exactly one further dispatch and the queue-timeout handling
  (recommended);
- authorize a re-created branch instead;
- pursue GitHub Support with request `4ed78a69-de10-11f1-8d56-5705d8f2f5ff` before any
  attempt.

[deferred-work.md](../../_bmad-output/implementation-artifacts/deferred-work.md) tracks
three separate follow-ups:
- cancelling the orphaned queued runs. The broker workflow has no concurrency group or
  `workflow_run` dependency, so they do not block it. Their commits have no completed
  tuple-ci, catalog-builder or source-release result;
- stopping `/pushall` from pruning active qualification refs;
- the GitHub Support case.

All five [historical rejected guarantees](story-4-17-local-broker-dispositions-2026-11-10.md)
remain **REJECTED**. Broker selection is **null** and production eligibility is
**FAIL**. Platform 4.17 and EventStore 3.21 remain **in-progress**. Packet, verifier,
cleanup and artifact-delivery assessment remain incomplete.
