# Module intake v1

Platform records the module releases selected for a candidate in `eng/module-intake.json`. The last entry for each module selects its candidate release. Each module has one GitHub `repository` (`OWNER/REPO`) and an append-only `releases` list in strictly increasing version order. The schema value is `hexalith.platform.module-intake.v1`.

## Release entries

| Field | Meaning |
| --- | --- |
| `version` | Normalized release version `MAJOR.MINOR.PATCH`, optionally with a SemVer prerelease suffix in lowercase. |
| `commit` | Full lowercase Git commit SHA of the owner release. |
| `packages` | Published NuGet package IDs and lowercase `sha256` values of the exact `.nupkg` bytes. |
| `images` | Registry-qualified repository `name` and `sha256:…` `digest` of the released OCI index or Docker manifest list. |
| `attestation` | Expected attestation identity for the release's images, or `null` when one has yet to be established. |
| `evidence` | HTTPS links to the owner's release and any accompanying release evidence. |
| `classification` | The owner's v2 declaration `lifecycle.classification.changeClass` and `declarationSha256`, the lowercase SHA-256 of the exact declaration bytes. |

`python3 -B eng/module_intake.py validate` checks the fields and recomputes the classification. `--check` also compares existing releases to the canonical manifest at `HEAD` or the revision supplied with `--against`. A baseline identifier shared with the comparison manifest must keep the same module versions. Tuple CI uses the pull request base, the previous `main` commit on a push, or the explicit candidate's first parent on manual dispatch. Use `--manifest PATH` before the subcommand to work with a separate manifest. A standalone manifest can be checked offline and remains independent of the canonical Git history unless `--against` is supplied.

To add an owner release, supply the peer-reviewed commit, version, package IDs, tag-free image repositories and evidence URLs. Repeat `--package`, `--image`, and `--evidence` for each item. The command hashes the owner declaration at the exact commit and the released package bytes from NuGet.org. It queries Docker Buildx for the published image indexes using the release version as the registry tag, or `--image-tag TAG` when the registries use a different common tag. It appends the resolved entry and recomputes the classification in one atomic update. For an attested release, pass `--attestation PATH` with the following identity object.

| Field | Meaning |
| --- | --- |
| `workflow` | Owner caller path under `.github/workflows/`. |
| `ref` | Exact protected source ref, including `refs/heads/` or `refs/tags/`. |
| `buildsWorkflow` | Fully qualified reusable workflow path `OWNER/REPO/.github/workflows/FILE.yml`. |
| `buildsSha` | Full lowercase commit SHA to which the reusable Builds workflow is pinned. |

Review of the canonical manifest establishes the approved protected ref, the reusable Builds workflow and pin, and their binding to the owner release and its artifact digests.

## Image admission

Run `python3 -B eng/module_intake.py admit MODULE VERSION IMAGE` with the exact image repository from the release. The verifier contacts the registry using `oci://IMAGE@DIGEST` and uses `gh attestation verify --format json` to cryptographically verify GitHub Artifact Attestations scoped to the owner repository. The caller needs a GitHub CLI with OCI attestation and JSON certificate support and read access to the registry and the repository's attestations. The admission result comes from build provenance.

The tool checks the verified statement for the pinned SHA-256 subject digest. In the same result, it requires these exact, case-sensitive values from `verificationResult.signature.certificate`.

| Certificate field | Expected value |
| --- | --- |
| `issuer` | `https://token.actions.githubusercontent.com` |
| `runnerEnvironment` | `github-hosted` |
| `sourceRepositoryURI` | `https://github.com/{repository}` |
| `sourceRepositoryDigest` | Release `commit`. |
| `buildConfigURI` | `https://github.com/{repository}/{workflow}@{ref}` |
| `sourceRepositoryRef` | Recorded protected `ref`. |
| `buildSignerURI` | `https://github.com/{buildsWorkflow}@{buildsSha}` |
| `buildSignerDigest` | Recorded `buildsSha`. |

Admission succeeds with a JSON receipt containing `image`, `subjectDigest` and the checked `certificate` fields. All refusal cases exit nonzero with empty stdout and a message on stderr.

| Refusal code | Condition |
| --- | --- |
| `attestation-missing` | The release identity is `null`, or the verifier reports no bundle for the pinned digest. A GitHub API 404 is treated as absence when a public lookup confirms that the repository is public and the pinned digest has no build provenance bundle. |
| `attestation-mismatch` | Verified provenance differs in its subject or certificate. The message names the mismatched fields. GitHub CLI repository or owner policy diagnostics map to `sourceRepositoryURI`, and its OIDC issuer policy diagnostic maps to `issuer`. |
| `attestation-unverified` | The verifier is missing, unsupported, times out, cannot reach the registry or API, or returns malformed output. An ambiguous API result or a generic signature or subject verification failure from GitHub CLI also receives this code. |

A passing image vulnerability scan cannot satisfy this gate.

## Classification and the release record

An owner declares `breaking` when its predecessor cannot read the state, events or security state the release writes, when the baseline host cannot tolerate its adapter or descriptor changes, or when its staging rehearsal fails. Other changes are `additive`. The owner declares `none` when no release-owned object changed.

`classification.workingBaseline` holds the working Platform release identifier. `classification.baselineVersions` maps modules in that release to their exact versions. Run `python3 -B eng/module_intake.py classify --baseline RELEASE-ID --baseline-release MODULE=VERSION` with one `--baseline-release` for every module in that release. Running it with `--baseline none` records the absence of a working baseline and omits `baselineVersions`. Reusing the current baseline name uses its recorded module versions.

`classification.chain` records `{module, version, changeClass}` for every release strictly after that module's baseline through its last entry. A module absent from the baseline contributes starting with its first entry. The list is in module-name order, then release order. `classification.effective` takes the maximum class over the chain, using `breaking > additive > none`, with `none` for an empty chain.

Story 4.12 consumes the last releases and their admission receipts before it writes a signed release record. In `moduleArtifactDigests`, use `<module>/package/<lowercase NuGet ID>` for packages and `<module>/image/<recorded image repository>` for images. Store SHA-256 values with the `sha256:` prefix.

Retain the exact intake manifest bytes and put their `sha256:` digest in `evidenceDigests["module-intake"]`. When the signed release becomes the working baseline, verify the retained manifest against that digest and read each selected version from its last module release entry.

For each module in `moduleChangeClassifications`, take its maximum over the chain with `none` as the empty default, then translate to the release-record wire values below. The candidate-wide maximum remains in `classification.effective`.

| Intake | Release record |
| --- | --- |
| `none` | `unchanged` |
| `additive` | `compatible` |
| `breaking` | `breaking` |
