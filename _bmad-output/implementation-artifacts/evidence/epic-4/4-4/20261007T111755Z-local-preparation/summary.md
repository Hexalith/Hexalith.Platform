# Story 4.4 local preparation — operational acceptance blocked

Prepared CODEOWNERS, pinned desired policy, GET-only discovery tooling, exact
change proposals, baseline preflight, effective-state verification, a custody/
application/recovery runbook, and 44 passing standard-library tests. This run made
zero GitHub requests, pushes or remote mutations under the spec's **"No push. No
remote ops."** restriction. No private repository was created or populated.

`proposed-changes.json` contains draft payloads and manual App selection values
from the retained `20261007T101349Z/discovery.json`; independent review and a new
complete checkpoint remain required. Builds changes only bypass actors and keeps
Codacy, SonarCloud, commitlint and all other retained writable fields. Platform
main and all tags use the pinned Administrator; immutable tags have no bypass.

`retained-baseline-preflight.json` and `retained-discovery-verification.json` both
returned exit code 1 (`blocked`), as required. These are checks against historical
retained evidence, **not fresh pre-change observations or live readbacks**. The
original discovery and checksum remain unchanged. `local-validation.json` records
local test outcomes and file digests. SHA256SUMS records raw file bytes; tool
records use canonical JSON digests for state/proposal bindings.

Outstanding operational work:

- Independently review payloads and current API contracts; collect complete fresh
  owner-authenticated discovery and stop on drift or API rejection.
- Publish CODEOWNERS on Platform main through a reviewed PR and prove ownership
  resolution; apply and read back main/tag rulesets, Builds bypass and the read
  base permission, preserving all other Builds settings and the Free plan.
- Confirm all eight installation selections, including the selected installation
  whose coverage is missing. Convert the seven all-repository selections while
  preserving existing repository access; create both private repositories only
  after the read default and selected App checkpoints pass. Prove exclusions
  before executable content.
- Read back private repository effective access, read-only tokens with PR approval
  disabled, all-ref workflow contents, PAT/App/deploy-key permission extent and
  each owner's independently authenticated custody evidence. Initial empty refs,
  incomplete pagination, HTTP 404/403, missing PAT inventories and statements
  without owner evidence do not qualify acceptance.
- Record live acceptance and then synchronize the story. Story 4.4 and sprint
  tracking remain `in-progress`; local passing tests cannot complete the story.

The runbook is [eng/repository-controls/README.md](../../../../../../eng/repository-controls/README.md).
