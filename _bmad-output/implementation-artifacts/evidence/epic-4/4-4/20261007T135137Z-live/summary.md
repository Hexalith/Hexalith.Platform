# Story 4.4 authorized live evidence

Story 4.4 remains **in-progress**. The public repository controls and organization
read default are applied and measured. The private repository and credential
boundary has unresolved dependencies, so operational acceptance is false.

The user authorized this phase with **"proceed"**, then requested **"provide it
yourself"** for evidence collection. The initial local-only preparation and its
blocked historical checks remain separate in `../20261007T111755Z-local-preparation/`.
The approved baseline commit stays `bed37580a4213edb3e4698525e530b85aa4a80ea`.

## Applied and inspected

| Control | Live result | Evidence |
| --- | --- | --- |
| CODEOWNERS | Published on `main`; exact policy audit passes; GitHub returns no errors | `codeowners-publication-receipt.json`, `discovery-after-controls.json` |
| Organization default | `read`; Free plan and both pinned owners preserved | `organization-read-default-readback.json` |
| Platform main | Active ruleset `24657106`: PR, one approving code-owner review, no force pushes/deletion, only jpiquot bypass | `platform-main-reviewed-changes-readback.json` |
| All Platform tag creation | Active ruleset `24657217`: only jpiquot bypasses creation denial | `platform-tag-creation-authorization-readback.json` |
| All Platform tag update/deletion | Active ruleset `24657570`: update/deletion denied, empty bypass list | `platform-immutable-tags-readback.json` |
| Builds main | Ruleset `19196298` now bypasses only jpiquot; entire writable model otherwise matches the reviewed pre-change model | `builds-administrator-bypass-readback.json` |

CODEOWNERS publication used [PR #3](https://github.com/Hexalith/Hexalith.Platform/pull/3),
operator diff review and the user's authorization. It merged as
`87588681bc229c19f58fc11d6d1f3db2ba6ce04c`, from full branch revision
`5f945450d4140fd190594a30b60a603521ec4e30`. GitHub review submissions were empty;
this report does not claim a formal approving GitHub review.

Each settings mutation has its complete fresh pre-change GET discovery,
regenerated proposal, scoped preflight, review hash binding, HTTP receipt and
immediate readback. Its method, endpoint and payload matched the previously
reviewed action. The main ruleset was enabled only after CODEOWNERS publication
and successful GitHub resolution-error readback. No ruleset was weakened to work
around a rejected field. These are configuration inspections; no disposable ref
mutation probes or complete operational signoff are claimed.

## Evidence gathered without owner statements

`connector-app-observations.json` records the connected App installation
`77847816` (chatgpt-codex-connector), still selecting **all** repositories. Its
authenticated connector returned 67 repository IDs, matching the full owner CLI
organization inventory, followed by an empty offset-100 page. This supplements
the classic OAuth CLI's failed coverage query; it does not replace failed CLI
observations or establish other installations' selections.

Travis installation `31615326` selects repositories, but both CLI and connector
coverage queries returned 403. Its exact selected repository set remains unproved.
The other seven installed Apps select all repositories. The connected tools expose
no installation selection-mode mutation. The existing Chrome profile reached the
GitHub sign-in page for the organization settings URL, so no authenticated
owner-visible settings collection or selection change was possible.

`custody-observations.json` records authenticated account identity, credential-store
file permissions and the browser access limitation. Credential file contents and
values were not collected. These observations do not establish ownership of this
device, complete credential inventory or custody on either owner's other devices.
No signed/authenticated custody statement from jpiquot or tinouit was available.
Existing backup/certificate custody artifacts do not cover this credential boundary.

## Outstanding dependencies

- Obtain complete owner-visible Travis coverage and convert the seven all-repository
  Apps to selected repositories, preserving their exact existing access; measure
  all eight selections and exclude the new private repositories.
- Resolve unavailable fine-grained PAT grants/requests inventory. The current
  classic OAuth credential's 404 responses are retained as unavailable observations,
  not evidence of an empty inventory. GitHub documents these APIs for
  [GitHub App credentials](https://docs.github.com/en/rest/orgs/personal-access-tokens).
- After App prerequisites pass, create and configure private Operations and
  Notifications repositories, measure effective writers and workflow permissions,
  inspect every branch/tag tree and deploy-key inventory.
- Establish authenticated evidence for both owners' six credential inventory kinds
  and their device custody, bound to the actual private repository IDs and fresh
  final discovery. An agent-generated statement cannot substitute for these facts.

No private repository was created while its App prerequisites were unproved.
Ownership, billing, integration permissions, unrelated credentials, Builds content,
submodules and cluster resources were unchanged by this operational phase.

## Verification

All **46 tests pass**, including new regression cases that unavailable App coverage
or an incomplete preservation baseline reports **unproved preservation**, without
alleging that existing repository access changed. The earlier diagnostic output is
retained as `verification-before-diagnostic-fix.json`.

`verification-after-controls.json` returns **blocked** (exit 1),
`complete: false` and `operational_acceptance: false`. No remaining issue concerns
the inspected main/tag/CODEOWNERS settings, organization read default or Builds
preservation. Its remaining issues concern App coverage/selection, PAT inventory,
the absent private repositories and custody. See `local-validation.json` for the
test command and source hashes, and `SHA256SUMS` for retained evidence hashes.

The build workflow is still at step-03 acceptance verification. The private
boundary criterion is unmet, so the completed-review/done workflow has not run.
