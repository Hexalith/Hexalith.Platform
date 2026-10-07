# Story 4.4 repository controls

The initial preparation run followed the build restriction **"No push. No remote
ops."** It made no GitHub requests or mutations. The user subsequently authorized
live discovery, CODEOWNERS publication and application of the prepared Story 4.4
controls by replying "proceed". Live evidence is recorded separately from the
initial local validation. Story 4.4 remains `in-progress` until measured controls
and owner custody evidence satisfy all acceptance criteria.

`desired-state.json` pins Hexalith, Platform, Builds ruleset `19196298`,
Administrator `jpiquot` (6775094), and second owner `tinouit` (183023925). The only
private writers are those two existing owners. Ownership and billing stay
unchanged; GitHub Free remains required. No publication identity is granted a
bypass: Story 4.12 owns that identity and its qualification.

## Local commands

Use Python 3 and its standard library. `propose`, `preflight` and `verify` only
read local files. There is no apply command. `discover` requires explicit
`--live-read-only` and uses an already authenticated `gh` installation; do not
invoke it without an explicit live authorization recorded with the evidence.

```sh
python3 -m unittest discover -s eng/repository-controls -p 'test_*.py'
python3 eng/repository-controls/controls.py propose \
  --discovery _bmad-output/implementation-artifacts/evidence/epic-4/4-4/20261007T101349Z/discovery.json \
  --output /owner-only/path/proposal.json
python3 eng/repository-controls/controls.py preflight \
  --baseline /owner-only/path/reviewed-discovery.json \
  --proposal /owner-only/path/reviewed-proposal.json \
  --discovery /owner-only/path/fresh-discovery.json \
  --output /owner-only/path/pre-change-check.json
python3 eng/repository-controls/controls.py verify \
  --baseline /owner-only/path/reviewed-discovery.json \
  --discovery /owner-only/path/fresh-readback.json \
  --custody /owner-only/path/owner-custody.json \
  --output /owner-only/path/verification.json
```

`verify` and `preflight` return exit code 1 for blocked evidence, 2 for invalid
input, and 0 for passing local consistency. Proposal generation returns 0 even
when it reports observation gaps: it creates material for review, not authority
to execute. Every report keeps `mutation_authorized` and `operational_acceptance`
false. The verifier also keeps `complete` false. An operator must establish the
authenticity of supplied observations and owner statements and record live
acceptance independently before changing story status.

The retained `20261007T101349Z` snapshot has no pagination proof, contains failed
PAT queries and repository lookup 404s, and lacks owner custody evidence. It
supports draft payloads only. Do not add completeness claims to it, replace its
timestamp, or treat candidate lookup 404s as authoritative absence.

## Separately authorized operational sequence

1. Establish Administrator access and recovery on an owner-controlled device.
   Confirm the pinned actor/organization IDs, both existing owners and the Free
   plan. Read authorization and mutation authorization are separate from this
   build. Authenticate through `gh`'s credential store, never CLI token arguments
   or committed environment files.
2. Capture a new, complete read-only discovery in an owner-controlled directory:
   `python3 eng/repository-controls/controls.py discover --live-read-only --output
   /owner-only/path/discovery.json`. The collector follows every pagination link,
   rejects duplicate/partial results and truncated trees, and preserves errors.
   Review API availability and request payload contracts before applying them.
   An unsupported field, HTTP error, unknown actor type, or rejected rule blocks
   the affected action. Retain that evidence and resolve the issue; do not drop
   an unknown Builds rule, buy Team, or broaden a bypass to get past an error.
3. Generate and independently review the proposal against that snapshot. Record
   hashes of the policy, discovery and reviewed proposal. Before each action,
   capture another observation and run `preflight` with the reviewed baseline
   and proposal. Every precondition must be complete and no older than the
   policy's 900-second window. Drift stops the affected action. Regenerate and
   review a proposal for the new state; do not edit a reviewed payload in place.
   Payloads, endpoints, methods and baseline bindings are checked together.
   Every action binds fresh, complete GET observations of the Administrator,
   organization identity/Free plan and the two owners. The Platform main action
   also binds successful published CODEOWNERS and an empty resolution-error
   inventory; missing, changed or stale publication evidence blocks that action.
4. Publish this change's `.github/CODEOWNERS` on Platform `main` through a reviewed
   PR. Have a different author, for example `tinouit`, obtain Administrator's
   approval: GitHub does not allow the PR author to approve their own PR.
   Read back the three ownership patterns and GitHub CODEOWNERS resolution errors
   before enabling the code-owner review requirement. A local file has no effect
   on GitHub ownership. No root workflow is introduced here.
5. Apply the organization read default and Platform rulesets using reviewed
   payloads, fresh checkpoints and immediate readbacks. Main requires a PR,
   one approving code-owner review, denies force-push/deletion and has only the
   pinned Administrator bypass. Tags have two active `~ALL` rulesets with no
   exclusions: creation authorization with Administrator bypass, and update/
   deletion denial with **no bypass**. Test creation and existing-tag update/
   deletion denial using disposable, expressly authorized refs in the live run;
   configuration readback alone is not a mutation probe or operational signoff.
6. Apply Builds' reviewed PUT payload, changing only `bypass_actors`. Retain and
   compare its entire previous and returned writable model. Codacy, SonarCloud,
   commitlint, PR parameters, conditions, enforcement and all other rules must
   match the reviewed baseline. Do not touch Builds source, workflows or its
   submodule. Unknown API fields produce a blocked capture, never a replacement
   PUT that would silently remove a rule.
7. Before creating the private repositories, convert each all-repository App
   installation to selected repositories in GitHub's installation settings.
   Snapshot every current repository ID with complete owner-visible pagination,
   then preserve that exact set. For existing selected installations, preserve
   their exact existing selection. The retained discovery shows eight Apps,
   seven selecting all repositories; it does not establish the selected App's
   coverage. The proposal records manual selection values rather than inventing
   a REST endpoint for changing installation selection mode. Read back every
   installation after each change. Do not uninstall an App or reduce unrelated
   permissions/access. Any new repository or installation discovered during the
   sequence requires rediscovery and review. Inaccessible installation coverage
   remains blocked even when the UI appears plausible.
8. Rediscover and review a creation checkpoint after base permission is read and
   every App selection is complete and selected. Complete organization inventory
   plus confirmed owner identity establishes whether each name is absent; a
   404 does not. Create private `Hexalith.Operations` and
   `Hexalith.Notifications` with `auto_init: false`. Do not add executable content.
   Confirm both new IDs are excluded from **every** App, including selected Apps,
   before content is added. Retain creation responses and App readbacks.
9. Generate a new post-creation proposal/checkpoint for each repository's token
   settings: `default_workflow_permissions: read` and
   `can_approve_pull_request_reviews: false`. A pre-creation snapshot cannot
   qualify a token-setting change. Do not add bots, executor grants, additional
   collaborators, teams, deployment credentials or notification delivery.
10. Measure effective access using all-affiliation collaborators, all organization
    owners/members/outside collaborators, team members and each user's effective
    repository permission endpoint. Inspect repository team grants and push/
    maintain/admin flags, including custom roles. Only the two named owners may
    write. Direct collaborator lists alone cannot establish the boundary.
11. Once exclusions and token settings are measured, a named writer may add a
    non-executable README in each new repository. This enables usable Git trees.
    An empty repository can return API errors for refs/trees and is explicitly
    blocked for workflow inventory; neither size zero nor 404/409 proves absence.
    Discover all branches and tags, peel annotated tags (including nested tags)
    to their commit/tree object and inspect complete recursive trees. Missing tag
    objects, cycles and truncated trees remain blocked. Workflow content is
    audited in memory and retained as hashes/results; source is not copied into
    evidence where it might contain a credential.
12. Gather both owners' custody evidence, capture a fresh full readback and run
    `verify`. Keep inaccessible PAT inventories as unresolved dependencies. An
    owner statement cannot turn an unavailable API inventory into an empty one.
    Human review must authenticate the statements and inspect the referenced
    inventory evidence before live acceptance. Retain sanitized measured evidence
    and its hashes, record explicit operational acceptance, then synchronize
    Story 4.4. Until then keep its status `in-progress`.

Previously applied settings will naturally change later preconditions. Use a
fresh, reviewed checkpoint for each phase. Existing named Platform rulesets are
proposed as PUTs; ambiguous duplicate/inherited names produce no executable
payload. Private creation proposals stop if the repository already exists.
Readback preservation comparisons use the separately retained pre-change baseline.

## Workflow permissions and credential custody

Read-only default workflow tokens do **not** cap workflow `permissions` overrides.
The checker inspects every local workflow on every retained branch/tag. It rejects
write scopes, `write-all`, secret credential references and ambiguous YAML forms
such as aliases or escaped mapping keys. It accepts only a conservative subset
with read/none overrides, `{}`, `read-all` or inherited read defaults. More complex
syntax requires review and a supported parser before it can qualify; do not
weaken or forge the audit result. This check does not prove remote reusable
workflow internals or runtime custody. No execution credentials or executors are
introduced by this story.

Store credential values, private keys and raw owner-device inventories outside
Git, on the named writers' devices. Record references/hashes in sanitized owner
statements. The custody input has this shape (placeholders do not qualify):

```json
{
  "captured_at_utc": "<fresh UTC timestamp>",
  "discovery_sha256": "<canonical digest of exact readback>",
  "private_repository_ids": ["<Operations ID>", "<Notifications ID>"],
  "no_writable_credentials_outside_named_writer_devices": true,
  "owners": [
    {
      "id": 6775094,
      "login": "jpiquot",
      "inventories": [
        {
          "kind": "classic-pat",
          "complete": true,
          "writable_credentials_on_named_writer_devices_only": true,
          "evidence_reference": "<owner-retained inventory reference>",
          "evidence_sha256": "<SHA-256 of independently reviewed evidence>"
        }
      ]
    }
  ]
}
```

Supply **both** owners (`jpiquot` and `tinouit`) and all six inventory kinds for
each: `classic-pat`, `fine-grained-pat`, `app-credentials`, `deploy-keys`,
`cached-credentials`, and `workflow-and-admin-credentials`. Include CLI/browser
caches, SSH signing/administration credentials, App private keys/installation
tokens, owner-created tokens, repository workflow/admin credentials and any
remote runners/secret stores that could hold writable authority. Bind the actual
private repository IDs and the canonical discovery digest, obtainable via
`controls.digest(controls.load(path))`. The human reviewer verifies identity,
coverage, storage location, signatures/authenticated owner provenance and evidence
hashes. Boolean assertions and synthetic fixtures alone cannot establish custody.

Successful PAT API inventories must include permission extent and writable
grant/request repository coverage. Unauthorized writable owners are rejected;
missing permission or scope data stays blocked. Writable deploy keys are rejected
because their custody is not tied to the named owners by the repository API. App
exclusion is required even for read-only installed Apps.

## Rollback and Administrator recovery

Retain pre-change JSON, exact reviewed payloads, fresh checkpoints, change results
and readbacks with SHA-256 hashes before each mutation. Keep the Administrator's
authenticated device and independent owner recovery access available throughout.
Organization owners retain the power to edit rulesets; there is no immutable
protection against owner policy edits.

If an API rejects a payload or readback differs, stop that action and retain its
error. Prefer repairing the scoped change while retaining the intended boundary.
Any rollback that restores write defaults or a broader bypass requires a separate
reviewed incident decision and leaves 4.4 incomplete. Restore Builds only from its
exact reviewed previous writable model, preserving all checks. An App selection
rollback must preserve the previous repository set and continue excluding private
repositories; switching back to all would violate the boundary. Do not delete a
repository or revoke unrelated integrations/credentials as an automatic rollback.

Administrator's creation bypass cannot change/delete an existing tag because the
immutable ruleset has no bypass. Exceptional recovery requires an explicitly
reviewed owner policy edit, retained lineage and restored verification; no hidden
tag immutability bypass is supplied. Do not use recovery to grant a speculative
publication identity or change organization ownership, billing or cluster state.

API contract references for the operational review:
[repository rulesets](https://docs.github.com/en/rest/repos/rules),
[workflow defaults and overrides](https://docs.github.com/en/organizations/managing-organization-settings/disabling-or-limiting-github-actions-for-your-organization).
They were not fetched during this local-only run.
