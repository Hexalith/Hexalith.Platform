# EXT-HOST-1 owner gap evidence — 2026-11-02

This is a read-only preparation and verifier-safety handoff, not a replacement commitment or an OQ-5.4-1 attempt. Neither EXT-HOST-1 nor EXT-SECRETS-1 is Available. The authoritative Agents register still accepts `abbec4093715c1db854433119b32d4e16df20d08` for both records. The clean Platform source used below was `92f87e6a49e4357f91b2ffb821e044931291c0e6`, an unaccepted successor. This verifier fix is not an accepted replacement target.

## Read-only preparation

The commands ran from the Platform repository root. The detached checkout at `/tmp/hexalith-platform-ext-host-baseline-20261102` was clean at the source commit above. The two prepare commands exited `2`, reported zero qualification effects, and retained their exact JSON output here.

```bash
git worktree add --detach /tmp/hexalith-platform-ext-host-baseline-20261102 HEAD
python3 eng/ext-host-1/prepare_acceptance.py --root /tmp/hexalith-platform-ext-host-baseline-20261102 --output /tmp/ext-host-1-preparation-20261102 --register /home/administrator/projects/hexalith/agents/_bmad-output/planning-artifacts/external-dependency-register.md
python3 eng/ext-secrets-1/prepare_acceptance.py --root /tmp/hexalith-platform-ext-host-baseline-20261102 --configuration /tmp/hexalith-platform-ext-host-baseline-20261102/eng/ext-secrets-1/runtime-reference.example.json --output /tmp/ext-secrets-1-preparation-20261102 --artifacts-argument /tmp/ext-secrets-1-full --evidence-argument /tmp/ext-secrets-1-evidence --register /home/administrator/projects/hexalith/agents/_bmad-output/planning-artifacts/external-dependency-register.md
```

The host report has 18 missing references, including coordinated installation context, both owner dates, protected engine and observer, Parties and Conversations bindings, and replay/recorder/worker/spool installation. The Secrets report has 75 missing inputs. That report used the repository's **example** runtime configuration, so its field-level nulls are a diagnostic inventory, not a claim about a private installed configuration. Both reports have `completeReplacementPacket:false` and `dependencyAvailable:false`.

The exact retained files are [host-blocked-inputs.json](host-blocked-inputs.json) (SHA-256 `50ecdabb9e82619f7dccc59d61b2336800e7593739350c3b16b18de2c1e8ffb3`) and [secrets-example-blocked-inputs.json](secrets-example-blocked-inputs.json) (SHA-256 `6d40e038fd7ec89c9a95f2d8f00d494024a6304379fd220c1d727ca31db13d20`). The source `eng/ext-secrets-1/public-joint-plan.json` SHA-256 is `cbd4fa28443b121c0139cf4c67a939e05ef611666028100e4d63ecafebf22a99`; the Secrets case-plan digest is `16dd493ff0414fea13be95f137d372e54088881550665aa3239a5814a9e862ed`. No installed joint configuration/profile or three independent role-anchor hashes were available to bind to either digest.

The currently accepted commands below retain their exact old target. They cannot qualify this checkout. The new coordinated commands cannot be formed until both preparation reports are complete and the register owner accepts the replacement.

```bash
test "$(git -C ../platform rev-parse HEAD)" = "abbec4093715c1db854433119b32d4e16df20d08" && test -z "$(git -C ../platform status --porcelain)" && bash ../platform/eng/verify-agents-host.sh --mode Full --artifacts-path /tmp/ext-host-1-full
test "$(git -C ../platform rev-parse HEAD)" = "abbec4093715c1db854433119b32d4e16df20d08" && test -z "$(git -C ../platform status --porcelain)" && env EXT_SECRETS_1_RUNTIME_CONFIGURATION=/home/administrator/hexalith-secrets-evidence/ext-secrets-1/acceptance-abbec40-20261018/detached-packet/runtime-reference.json EXT_SECRETS_1_ACCEPTANCE_PACKET=/home/administrator/hexalith-secrets-evidence/ext-secrets-1/acceptance-abbec40-20261018/detached-packet/replacement-acceptance-packet.json pwsh -NoProfile -File ../platform/eng/verify-ext-secrets-1.ps1 -Mode Live -EventStoreSourceRoot ../eventstore -ArtifactsDirectory /tmp/ext-secrets-1-live-20261018-abbec40-artifacts -EvidenceDirectory /tmp/ext-secrets-1-live-20261018-abbec40-evidence
```

## Production and acceptance blockers

- The Agents AppHost adds sibling projects and reference strings. No production process calls `AddInstalledAgentsOwningProcess`; `AgentsHostComposition.TryCompose` supplies no private capabilities. The private recorder's exact-stream ACL is exercised in qualification code, with no installed production binding demonstrated. Source registration and component tests therefore cannot establish H1–H4 or Levels 4/5.
- The EventStore protection record is still `Uncommitted` with target, date and owner command `TBD`. Its complete installed profile and production FR-34 identity/erase proof are absent. A no-op or self-reporting wrapper is not a substitute.
- The immutable shared Platform replacement target, exact host and Secrets profile/configuration bytes, installation context, complete trust anchors, owner dates and exact coordinated commands are absent. The existing `prepare_acceptance.py` scripts correctly refuse to emit a complete replacement packet.
- OQ-5.4-1 remains unrun. Its required order is protection owner command, Secrets cases without host references, host with refusing Agents consumers, then mutual S3/S4/v23 and H3/H4/v21/v23 cases. A partial or failed attempt cannot advance either Platform record.

The accepted MVP custody amendment prioritizes ordinary identity and operational-security work but supplies no reduced executable contract. The retained software-key `ObservedRevival` result precludes a non-revival or irreversible all-copy erasure claim. Full protected-content, history, export, destruction and release gates remain blocked. Complete owner installation and a new accepted exact-target replacement packet are required before the one permitted joint attempt.

## Verification of this local safety change

`python3 -m unittest test_verify_full test_prepare_acceptance` in `eng/ext-host-1` passed 15 tests. The Full component route now preserves component results, reports `DependencyNotAvailable`, and exits 1. No production composition change is claimed: `AddInstalledAgentsOwningProcess` still has no Agents Server caller.

An attempted C# startup guard was removed after review showed it did not install the private services in Agents Server and interrupted existing composition checks. During that investigation, the focused .NET test command `dotnet test tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj --configuration Debug --filter FullyQualifiedName~AgentsAcceptedModelIdentityTests -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` failed on analyzer errors in unchanged `Hexalith.Platform.Protection` files before tests ran. The serialized fallback `timeout 120s dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj --configuration Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` timed out after an unchanged `PlatformRoutingCatalogMetadata.cs:23` `CA2007` error; its [captured log](composition-build.log) is retained. No .NET test pass is claimed.
