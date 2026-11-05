# Current tuple preparation

The current model M prepares the explicit `platform-current-package` purpose:
measured Release artifacts from the active Parties source, released SDK `4.1.0`,
Tenants `7.0.4` and Memories `3.0.23`, and 269 primary cases. M selects genuine
[Builds v4.44.4](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v4.44.4)
at `5fa2b97c8b22047c2c33e2c9f7017fd5d604c9ef`, published by successful normal
[release 42077837054](https://github.com/Hexalith/Hexalith.Builds/actions/runs/42077837054)
after exact-source [CI 42073886800](https://github.com/Hexalith/Hexalith.Builds/actions/runs/42073886800),
with exact tool pin and supported floor `4.44.4`. The committed tuple records
catalog blob `5bd9ab8dd93358942eaa8d164c99ca2bd18f0204`, catalog SHA-256
`43a6668f2c54ed6f400cb3ac9a4bdbbe46dcad2ac0c58ea22cf6a17fcdde428c`,
and the actual signed archive, Tooling and inventory identities. Its measured
validation pair is recorded below; hosted exact-source/native acceptance remains
separate and required.

The separate same-code legacy lane L retains its genuine Builds `4.44.3`/fc212224
and Tenants `7.0.2` tuple and all original 349 cases. The nine conditional old
public-reader cases stay in L and are not counted as current reader execution.
Current private-reader/reference and native observations require their own real
source/run/attempt bindings. Both lanes execute actual committed revisions, with
identical shared source and only eight bounded metadata differences; neither
source equivalence nor counts imply binary, runtime or publication acceptance.
The parent must create a new real L commit after shared changes before activating
its exact descriptor in M. No nested checkout or dependency source fallback is
permitted.

The signed module archive SHA-256 is
`d375583dba2800fcc76e4d013a102b59937470af87487f890fbd0926a37af294`.
Its AppHost body is
`e85148b3809600a8f6e81c1163618c0250b179f8f9e5435a46c2ad5fe119dc10`;
its root and adjacent g4-host Tooling bodies both are
`57eff14f810059878e1c43eafe2b1ee04382ea01cb8fd860cfc6810e9d2505a2`.
Admission requires this complete pair or the retained historical 4.44.3 pair;
cross-pair mixtures and ordinary caller claim suppression remain refused.

## Historical released tuple and earlier preparation


> The historical released owner configuration selected
> [Builds v4.44.3](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v4.44.3)
> at `fc2122240a39f03628c04fa8433e5eece90b8b4c`, from successful normal
> [release 41998213500](https://github.com/Hexalith/Hexalith.Builds/actions/runs/41998213500),
> with supported floor `4.44.3`. Actual signed archive, Tooling and inventory
> identities are in [`eng/builds-tuple.json`](../eng/builds-tuple.json).
> The selected SDK/Tenants pair is `4.1.0`/`7.0.2`; Memories `3.0.23`, Parties
> `3.0.0` and Hexalith.McpCli `2.0.0` retain their own authenticated public bodies.
> All 342 required controls must execute on this genuine tuple. Local signed-tool
> components and audit-only graph checks do not replace exact-HEAD hosted tuple,
> catalog-matrix or real package-runtime qualification.
> The procedure below preserves earlier unqualified preparation and its
> historical authorization context; it does not describe current acceptance.

The source candidate is official Builds `6cc96b4ff23c40c77651e1e4f85ed3170c25c7a4`.
Its catalog blob is `5d7eeadf561eed182fcb03c2b1fd97565dd52852`, SHA-256
`abe630443a9f095a1bce171c4b10314a714a8bba9f3bde8980a5c40ef46ef172`.
It preserves the selected newer dependencies. This is preparation, with
`candidateAcceptance:false`; no compatible published tuple is verified.

The existing bootstrap tool 4.38.3 and range are retained but unqualified for
this catalog. Its published source is a different revision. The current
recorded Builds gitlink is unavailable from the official remote. An unapplied
candidate gitlink patch is retained in the Projects Story 6.4 evidence; do not
record it until the exact producer source has passed qualification and its
actual publication has been verified. Live upstream heads can advance
independently; new source requires a fresh exact-input decision.

## Qualification and recording order

1. Qualify the exact official producer source. Require successful push CI for
   that full SHA, and require it still to be the intended official release
   branch tip. The observed run 41102168937 was cancelled; it is no success.
   `Tools/verify-g4-release-source.sh` checks live branch identity and exact-SHA
   successful push CI both before and after protected release approval. A newer
   main cannot silently replace this candidate. Reconcile any replacement,
   catalog and release inputs before continuing.
2. In the producer's protected hosted release environment, run its existing
   catalog, dependency audit, provenance, release-contract and package controls.
   `Tools/test-g4-tool-package-contracts.ps1 -RequireControls` qualifies actual
   Release packages and all required source/package controls. Some fixture
   harnesses create Git commits, so they were not run during this no-Git-mutation
   preparation. Local Debug host results are separate evidence.
3. Compute the proposed release through the real configured semantic-release
   analyzer/dry-run for that exact source and history. A proposal is not a
   publication. Obtain separate approval for protected producer publication.
   `verify-g4-tool-release.ps1` qualifies the actual computed version before
   tagging; `publish-g4-tool-packages.ps1` publishes and verifies all three real
   archives and their inventory/symbols. This preparation authorizes none of
   those mutations or CI dispatches.
4. Verify the official release/tag/full source, signed archives, qualification
   inventory, embedded tool version/source, Tooling bytes and source catalog.
   Fill every `publication` field in `eng/builds-tuple.json` with those observed
   identities. Its `sourceRevision` and `catalogSha256` must match `producer`.
   `inventorySha256` and `inventoryUrl` bind the verified inventory digest and
   exact official release asset. They remain unavailable until the actual producer
   evidence is retained and verified. Existing producer controls at
   `Tools/test-g4-tool-package-contracts.ps1:983` generate the evaluated expected
   runtime catalog; line 1051 compares it with installed embedded catalog bytes.
   Retain that verified actual producer evidence before filling these fields;
   Platform's binding records the observation and does not repeat qualification.
   Keep `candidateAcceptance:false`. The binding is deliberately incomplete
   until the real version and hashes exist.
5. Align `.config/dotnet-tools.json`, the range floor in `hexalith-platform.json`
   and README with that verified release, and obtain approval to record the
   exact owner configuration/gitlink. Preserve the selected catalog and direct
   AppHost compatibility. The proposed Builds source is already recorded; no
   duplicate producer commit is needed. Record the actual Platform owner revision
   after verifying that producer release. Defer the umbrella pointer until the
   separately approved consumer adoption in step 7; the preparation patch is unapplied.
6. Run fresh hosted `tuple-ci.yml` against the full recorded Platform revision.
   Retain signature, pin/catalog, clean-source, extracted-byte, foreign-reference,
   package-only Release, suite-count and output-identity checks. Passing this
   preflight still leaves authenticated candidate/policy/merge-result decisions,
   ready real services and generated/direct-host runtime gates open.
7. After independent acceptance, obtain separate Platform publication approval
   and verify its official identity. Obtain separate consumer adoption approval;
   align Projects' exact tuple, run actual package preparation and complete
   expected/actual restored package and symbol qualification. Unsupported File
   mapping and existing runtime readiness limitations remain explicit.

No local source checkout, old runtime receipt, source-equivalent catalog or
bootstrap range can substitute for any missing publication or acceptance gate.
