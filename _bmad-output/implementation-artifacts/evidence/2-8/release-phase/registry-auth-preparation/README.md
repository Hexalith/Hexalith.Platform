## Local activation credential preparation

This phase changes only staged activation source and reconciles four exact implementation files to Builds `d18bb63daf85d4a5b32b0ac322b3ec069570596d`. The source inventory and preservation comparisons are in `source-before-edit.json`, `source-after.json` and `implementation-parity.json`. Earlier local/release source inventories remain historical.

The reusable workflow requires explicit `registry-pull-username` and `registry-pull-password` inputs. EventStore and Parties map them to `HEXALITH_CI_REGISTRY_USERNAME` and `HEXALITH_CI_REGISTRY_PASSWORD`; their existing workflow/tool pins are unchanged. The planned principal is `ci-reader`, read-only with no admin membership. Provider/publisher/workload-reader credentials are not reused. No secrets inheritance or privileged PR event was added.

Login follows isolated success, uses password stdin and a private job-temporary Docker config. The integration step also requires successful authentication. Failed login removes the config immediately; an unconditional cleanup removes it after exact owner teardown and before diagnostics upload. The existing process environment allowlist already carries DOCKER_CONFIG. No runtime change was needed for this path.

Validation passed: 251 workflow assertions, including executable fake-Docker success, failed-login, missing-secret and foreign-path controls; 24 helper controls in the original staged tree; focused actionlint syntax checks for the reusable workflow and both callers. The installed actionlint 1.7.12 reports four existing `job.workflow_*` context diagnostics both before and after this credential patch. Current [GitHub Cloud context documentation](https://docs.github.com/en/actions/reference/workflows-and-actions/contexts#job-context) supports those properties. Exact diagnostics, baseline comparison and the narrow focused-lint filter are retained; the governance expressions were preserved.

These are local source and parser controls. No registry account, policy, Secret, GitHub secret or cluster object was changed. No actual credential was read or persisted. No caller/workflow was activated, no accepted pin changed, and no hosted candidate qualification or publication is claimed. All callers must satisfy the required secret contract before shared activation.

The subsequent hosted test-fixture correction is recorded separately in
`post-hosted-fixtures-parity.json`. It checks all earlier source hashes before
copying the four test files from committed Builds `86ed14c`, preserves original
heads/indexes and unrelated bytes, and records all 12 current phase file hashes.
`source-after.json` and `implementation-parity.json` remain historical d18
observations. The 37 fresh CI-environment controls compiled and executed in the
isolated foundation; the original tree receives byte-identical test source.
