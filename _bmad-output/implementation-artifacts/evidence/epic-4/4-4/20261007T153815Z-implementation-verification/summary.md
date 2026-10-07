# Story 4.4 further implementation verification

Story 4.4 remains **in-progress**. Fresh sanitized GET discovery finished at
`2026-10-07T15:38:43.177510+00:00`, retaining 44 observations and 67 organization
repositories. Public main/tag controls, Administrator CODEOWNERS, organization
Free/read defaults and preservation of Builds produce no verification finding.
Configuration inspection does not establish mutation-probe results or operational
acceptance. This run made no remote mutation, commit, push or custody assertion.

The verifier now rejects discarded permission metadata, unknown/empty PAT
permission extent, unknown effective permission levels and incomplete historical
Builds/App preservation baselines. Positive collaborator push/maintain/admin flags
still count when a separate permission response omits them. Complete historical
baselines need no freshness; current observations still do. Test fixtures copy
their observations so adding a synthetic collaborator cannot alter the approved
writer policy. Seven added regression tests pass; all **56 tests pass**. Whitespace
and sprint YAML checks pass, with the story status preserved.

`preflight.json` and `verification.json` both report **blocked**; their CLI exit
codes are **1**. Full verification retains 69 findings concerning App coverage and
selection, PAT inventories, missing private repository observations and custody.
It reports `complete: false` and `operational_acceptance: false`. Proposed public
settings are locally consistent with their fresh review checkpoints; no proposal
grants mutation authority. `local-validation.json` retains actual command results
and source hashes; `matrix-tests.json` maps the frozen I/O matrix to passing tests.

All eight CLI App coverage reads return **403**. Seven Apps still select all
repositories; Travis selects repositories but its exact selection remains
unproved. Connector discovery confirms only the connected App's own 67 repository
IDs and a terminal empty offset-100 page, matching the CLI organization inventory.
Its Travis read also returns **403**. These supplementary observations do not
replace failed CLI coverage or prove other Apps' coverage. No callable connector
operation changes installation selection mode or reads the required PAT inventory.

Both PAT inventories return **404**, retained as unavailable. GitHub documents
App user/installation credentials with organization read permissions for these
inventories. App coverage through the user-installation endpoint requires an App
user access token; removing a repository requires an already selected installation.
There is no documented selection-mode change endpoint on the inspected installation
API page. See [GitHub PAT inventory contracts](https://docs.github.com/en/rest/orgs/personal-access-tokens)
and [installation contracts](https://docs.github.com/en/rest/apps/installations).

The registered browser-control option, Node REPL, fails with **Transport closed**.
No Chrome process/window, active X11 window or debug endpoint at ports 9222, 9223
or 9333 was available. No usable authenticated browser settings session was
established. `browser-access-observations.json` records the limits without reading
credential contents or copying a profile.

Operations and Notifications are absent from the complete owner-visible inventory;
their lookup 404s alone are not absence evidence. Creation is blocked until every
App's existing access is fully measured and preserved through selected repository
settings. Both owners' authenticated custody evidence remains required, including
all six inventory kinds and eventual private IDs bound to a fresh final discovery.
The remaining work is the ordered App settings/readbacks, PAT inventory access,
private repository creation/configuration/effective-access and workflow audit,
and owner custody evidence already documented in `eng/repository-controls/README.md`.
No executable content, unrelated integration/credential change, ownership/billing
change or cluster operation occurred.

`SHA256SUMS` hashes every retained evidence file except the manifest itself.
