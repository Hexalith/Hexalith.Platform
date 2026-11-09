# Automatic qualified source releases

`source-release.yml` responds to completed main-push runs of **Platform tuple CI
preflight** and **Catalog builder consumer binding**. Its read-only job requires
both latest exact-source runs, latest attempts and all four tuple
orchestration/execution jobs and five SDK jobs to pass. Missing or pending counterparts defer. Failures, cancellations,
unknown jobs, foreign/PR inputs, stale callbacks and changed main refuse.

The read-only verifier runs the reviewed default-branch workflow SHA, so an old
callback cannot hide behind the absence of newer helper files. Exact workflow ref,
workflow SHA and checkout roles are bound. The publisher checks out the qualified
callback source explicitly; `workflow_run`'s default `GITHUB_SHA` is not treated
as that source. A serialized publisher rechecks the
same qualification immediately before semantic-release creates its tag and again
before its GitHub release. An intervening main/attempt change refuses. GitHub has
no atomic branch-and-tag transaction; a change between these operations can leave
a source tag without a release. The failed run and bounded receipt remain visible;
no tag update/deletion or stale-source retry is attempted. If the tag was pushed
but GitHub release creation failed, a later standard semantic-release callback can
see that tag and report no-release. It does not repair the missing release. The
maintainer must assess the exact first failure and existing tag under the normal
authority; this change provides no recovery framework or tag rewrite.

Node24 and the exact locked semantic-release25 ecosystem use the conventional
preset: fix gives patch, feat gives minor, breaking changes give major. A build,
docs or other no-release commit does not force a version. Duplicate callbacks let
semantic-release find no unreleased commits. Only commit analysis, release notes,
the small qualification hook and the GitHub source-release plugin are enabled.
There is no npm/container publisher, version/changelog commit or main rewrite.
The GitHub plugin's pinned success/failure conditions disable issue discovery,
comments and labels; controls exercise its actual hooks against an in-memory API.

Release notes pin Conventional Commits preset9.3.1 with notes-generator14.1.1
and its writer8.4.0. Preset10 requires writer9 and cannot render with this
generator. A direct generator regression renders controlled feature, fix and
breaking descriptions plus repository, compare and commit links.

Only the publisher job has contents-write; both jobs have actions-read. It uses
the job's ordinary GitHub token, with no NuGet, registry or provider credentials.
The approved numeric-version tag creation exception permits the ordinary Actions
token to publish. Tag updates and deletion remain protected. This compatibility
change edits no rule or credential.

Receipts retain source, qualifying workflow/run/attempt URLs and required job IDs,
computed version once known, outcome and explicit native/candidate/Story false.
They are retained as workflow artifacts; source/run links and those limitations
also appear in the public release notes. Standard semantic-release Git notes are
release metadata; source files and main remain unchanged. No local semantic-release
CLI or dry run is required: dry runs themselves probe remote push permission.
Run `npm ci --ignore-scripts` then `npm run test:source-release` for effect-free
controls. The same controls execute in normal PR/main tuple CI before provisioning.
Original Story2.8 public Parties, native/manual, cleanup and acceptance gates remain.

References: [GitHub workflow_run context](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#workflow_run),
[standard semantic-release configuration](https://semantic-release.org/usage/configuration/),
and [the disabled-comment issue](https://github.com/semantic-release/github/issues/1083).
The exact pinned12.0.10 hook implementation and its executable API controls, rather
than a comment-setting assumption, establish the source-only permission boundary.
