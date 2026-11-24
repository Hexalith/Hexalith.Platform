# Story 4.8 live admission evidence

Recorded 2026-11-24. The four seeded releases were resolved with `module_intake.py add`. Package SHA-256 values came from the exact `.nupkg` bytes on NuGet.org, using the package lists from the owners' GitHub release assets. Image digests came from Docker Buildx inspection of the owner-published release tags. The three Platform declaration copies and the Parties reference declaration matched the bytes fetched at the four owner tag commits. All four declarations report `additive`.

| Owner release | Packages pinned | Image indexes refused `attestation-missing` |
| --- | ---: | --- |
| EventStore 5.4.0 | 14 | `eventstore` |
| Tenants 8.0.4 | 5 | `tenants`, `tenants-api` |
| Memories 4.1.0 | 10 | `memories`, `memories-access-telemetry`, `memories-access-telemetry-clock`, `memories-domain-service`, `memories-mcp` |
| Parties 4.0.1 | 11 | `parties`, `parties-mcp`, `parties-ui` |

Ran `python3 -B eng/module_intake.py admit MODULE VERSION IMAGE` against the seeded manifest for each of the 11 image indexes, using the exact registry repository name recorded in the manifest. All exited 1, emitted `attestation-missing` on stderr and produced empty stdout. The manifest has `attestation: null` for these releases, so refusal occurs before an API lookup. A separate read-only lookup against the public GitHub attestation endpoint returned HTTP 404 for each of the 11 digests.

A temporary copy with an expected identity for EventStore also reached GitHub through `gh` and refused the absent bundle with `attestation-missing`.

## Public attestation

Verified the Liatrio AutoGov Policy Library build provenance using GitHub CLI 2.105.0. The CLI binary's archive matched the release checksum. The temporary input manifest recorded the public OCI digest, caller and cross-repository SHA-pinned reusable workflow. The verifier returned exit 0 and emitted the receipt below. [Build provenance](https://github.com/liatrio/autogov-policy-library/attestations/69042236), [public build](https://github.com/liatrio/autogov-policy-library/actions/runs/44271201977), [GitHub CLI release](https://github.com/cli/cli/releases/tag/v2.105.0).

```json
{
  "image": "ghcr.io/liatrio/autogov-policy-library",
  "subjectDigest": "sha256:7cc263bee55f986aeeb47758298eb5e62b7a82f81cd1251019e0f464170792f0",
  "certificate": {
    "issuer": "https://token.actions.githubusercontent.com",
    "runnerEnvironment": "github-hosted",
    "sourceRepositoryURI": "https://github.com/liatrio/autogov-policy-library",
    "sourceRepositoryDigest": "4dcb1b38fe18870031228ec65129235015ca80eb",
    "buildConfigURI": "https://github.com/liatrio/autogov-policy-library/.github/workflows/build.yaml@refs/heads/main",
    "sourceRepositoryRef": "refs/heads/main",
    "buildSignerURI": "https://github.com/liatrio/autogov-workflows/.github/workflows/rw-attest-image.yaml@d397979634bba731651d0cb0ae1bcb80d767216e",
    "buildSignerDigest": "d397979634bba731651d0cb0ae1bcb80d767216e"
  }
}
```

Four further invocations used the same image and changed one expected field at a time in the input manifest. Each exited 1 with empty stdout and `attestation-mismatch`.

| Changed manifest field | Mismatch diagnostics |
| --- | --- |
| Owner caller workflow | `buildConfigURI` |
| Source ref `refs/heads/Main` | `buildConfigURI`, `sourceRepositoryRef` |
| Reusable workflow path | `buildSignerURI` |
| Reusable workflow SHA | `buildSignerDigest`, `buildSignerURI` |

The final reviewed verifier reproduced the 11 seeded refusals, the public receipt, all four altered identity refusals, and the EventStore public no-bundle refusal.

Story 4.8 remains in progress pending a GitHub-attested Hexalith module image admitted by Platform.
