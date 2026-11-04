---
title: "Story 8.3 Platform API prerequisite matrix"
story: "8.3"
created: "2026-07-07T11:00:30+02:00"
status: "complete"
source_spec: "_bmad-output/implementation-artifacts/spec-8-3-platform-api-prerequisites.md"
---

# Story 8.3 Platform API Prerequisite Matrix

Story 8.3 is an evidence and approval gate. No Parties source migration starts in Story 8.3. No production source migration was performed. Do not edit submodules or production source for Story 8.3.

Status vocabulary:

- `available` means the owner surface exists and no additive API is required. It becomes consumable only when its matrix row records an immutable availability identity selected by the consumer: either a released package version containing the cited surface or the exact SHA of the root-declared submodule gitlink. The consuming story must verify that its actual dependency mode and identity match that row before source migration or local deletion.
- `needs-additive-api` means a partial source surface exists, but later Epic 8 work must wait for additive owner API, owner approval, or parity proof before deleting the Parties-local rollback path.
- `blocked` means the planning artifacts do not identify enough owner or proof detail for migration planning.

A working-tree checkout or `git -C ... rev-parse HEAD` alone is not root-submodule-pin proof. Submodule consumption evidence must include `git ls-tree <Parties-commit> <root-declared-submodule>` and a matching checkout SHA. Completed consumption may retain its historical validated pin; Story 8.10 reconciles the final identities actually used.

### Story 8.6 latest-selection override — 2026-08-01

The projection/query and DataProtection rows below retain the audit trail for the
exact `fa2d1c99` owner authorization and its failed consumer compile. After that
failure, the user explicitly directed this consumer to use the latest EventStore
release. Root gitlink, checkout, and release tag now all select EventStore
`v3.89.0` at `c590590bc581a3f72ef6e67148eda988ba4b8fe6`. That identity contains the
tenant-shared rebuild accumulation, complete replacement, and stale-entry pruning
surfaces missing at `fa2d1c99`; the Story 8.6 source consumer build passes with
zero warnings and errors, and the projection parity suite passes 150/150.

This explicit latest-release selection supersedes only the Story 8.6 consumer
identity block described in those two rows. It does not change the status or
authority of any other matrix row, including the payload-protection prerequisite.

### Story 8.6 identity refresh and G5 receipt refresh — 2026-08-04

`sprint-change-proposal-2026-08-04.md` (approved) formalizes the Administrator's
direct authorization to select EventStore `v3.91.0`
(`1d6e9321acfc416768c1c78e9facf573c9c41f71`) for Story 8.6, matching Builds
catalog identity `824d7ef100455423aabbcd399c8364074000b2e0`
(`HexalithEventStoreVersion=3.91.0`). A current package-mode restore and
Release build of the narrow projection project completed with zero warnings
and zero errors at that identity. This refresh updates only the two Story 8.6
rows below and the payload-protection (G5) row's current-identity receipt. It
does not promote the payload-protection row's status, does not close its
retention action, and does not change Story 8.7's `blocked` status: the
release adds Story 8.6's shared-projection rebuild completion mechanics only,
not the G5 runtime engine.

### Historical source observation — 2026-11-01

Selected from the ten exact pushed owner revisions for release synchronization
on 2026-11-01. Root adoption and qualification still require their own committed
evidence. The unchanged exact-selection gate requires committed root gitlinks
and matching clean checkouts; staged entries or working-tree HEADs alone cannot
satisfy it.
EventStore package `3.130.2`, Commons `2.30.8`, FrontComposer `4.7.4`, Memories
`3.0.13` and Tenants `6.0.0` are selected. These are published package selections.
Administrator / jpiquot approved this exact source/package set for Story 8.10
qualification on 2026-11-01 (Round 7 decision 3; I20 row below). Selection does not revalidate historical parity receipts or grant package publication,
deployment, data-custody or rollback qualification.
The pinned module tool is `4.41.1` and remains the source-root and dependency-mode
authority. The successful source mapping with module tool `4.39.0` on 2026-10-31
remains a historical observation: it reads the parent HEAD Platform gitlink,
then that selected Platform commit's Builds gitlink. The initial `HXW009`
observation used a stale Platform identity and remains historical evidence.
Prior source Contracts and owner host compilation and package topology verification
do not qualify these newer source selections or the Platform runtime.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `9b78aa008cfc62cd88ad0ea4b39b1f07311956ce` (`9b78aa00`) | Approved selection (2026-11-01 Round 7 decision 3); qualification still requires its own executed proof. |
| Builds | `df13d9d31f7ee1a1b0fb7eeac7e8b27b9a9f9fb0` (`v4.41.0-16-gdf13d9d3`) | Approved selection (2026-11-01 Round 7 decision 3); qualification still requires its own executed proof. |
| Commons | `bbbce518bcca9ca359762393688f02d142af1c61` (`v2.30.8`) | Approved selection (2026-11-01 Round 7 decision 3); qualification still requires its own executed proof. |
| EventStore | `ba4c5548ad260b049bda5a1c7def2e8af3484733` (`v3.130.2-63-gba4c5548`) | Approved selection (2026-11-01 Round 7 decision 3); qualification still requires its own executed proof. |
| FrontComposer | `c092e8f91ebb670fad4ab959f2d3c8e13d809e0f` (`v4.7.4-15-gc092e8f9`) | Approved selection (2026-11-01 Round 7 decision 3); qualification still requires its own executed proof. |
| McpCli | `17eb4fbbc151ca4b99dfa452e9f65655db2b402f` (`v1.1.0-55-g17eb4fbb`) | Approved selection (2026-11-01 Round 7 decision 3); qualification still requires its own executed proof. |
| Memories | `b93b32cf77f3d76e51d3221550eff99f4d1e93cb` (`v3.0.13-13-gb93b32cf`) | Approved selection (2026-11-01 Round 7 decision 3); qualification still requires its own executed proof. |
| Platform | `03799ed553a16c8a1528068ba4d95bb5509ea75e` (`v1.0.1-187-g03799ed5`) | Approved selection (2026-11-01 Round 7 decision 3); qualification still requires its own executed proof. |
| PolymorphicSerializations | `821b784bb51483acefa9725acc6792d111304a4e` (`v1.19.7-1-g821b784b`) | Approved selection (2026-11-01 Round 7 decision 3); qualification still requires its own executed proof. |
| Tenants | `2d3fa6da2312c1b5cf7f5c9461cc2d0ffc995b43` (`v6.0.0-17-g2d3fa6da`) | Approved selection (2026-11-01 Round 7 decision 3); qualification still requires its own executed proof. |

### EventStore package/source alignment observation — 2026-11-02

The user authorized alignment to the latest EventStore release for the Parties
Admin UI build fix. The selected EventStore package family is `3.132.2` in
`Directory.Packages.props`; the already committed root EventStore gitlink is
`7c93a1234d14c66f5ee85ed48caaee1d30a671f3`, the upstream `v3.132.2` tag.
No root gitlink changed in this correction. The 2026-11-01 table and I20 row
above remain dated approved selections. At this observation, other root gitlinks
had advanced since that approval, so I16 committed-selection tests were red
pending owner reconciliation (DW-182). The later 2026-11-02 decisions below
resolve source selection only. Matching package/source versions and a clean
build do not grant current parity, deployment, or rollback qualification or
promote any other 8.3 row.

### Current source observation — 2026-11-04

The active user's explicit Story 2.8 delegation selects genuine Platform v1.1.7
at `ed1560a57f4d4cb55b6b03f485cbfa01316837a7` and its exact published
Builds v4.43.6 source `725754075e1d6369430a3adf94d46fb71e6eaf9e`.
This local incoming-main reconciliation retains the previously selected coherent
v1.1.7/v4.43.6 caller tuple. Incoming main
`3e4367622764118fc2ad710ad1040994ae4cfa49` selected an older tuple; that
observation and all earlier selections remain historical below. The eventual new
tuple still requires genuine publication and a separate adoption handoff.
This is codex-agent source-selection bookkeeping, not a new human approval.
The published CLI, ordinary CI and registered manual baseline select that same
Builds revision. The incoming subscription safety source selects EventStore
`f213e4115ec23cb736e8aa0113f61b9d4854480c` (`v4.0.4-12-gf213e411`);
all other incoming root selections remain unchanged.
The graph selects EventStore package `4.0.4`, Commons `2.30.10`,
PolymorphicSerializations `1.19.8`, FrontComposer `4.7.5`, Memories `3.0.18`,
Tenants `7.0.0`, and McpCli `2.0.0`.
Selection does not revalidate historical parity receipts or grant publication,
deployment, custody, rollback deletion, runtime or candidate acceptance.
Every prior human decision and historical observation body is preserved below;
all six canonical parity receipts remain Unvalidated until actually rerun.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `1745b6663ad32bb1288167135ed0f979366c53a0` (`1745b666`) | Committed selection; qualification still requires its own executed proof. |
| Builds | `725754075e1d6369430a3adf94d46fb71e6eaf9e` (`v4.43.6`) | Released catalog pinned by Platform v1.1.7; qualification still requires its own executed proof. |
| Commons | `11555ace9efcae2928a00122dcba91243e5a8a1a` (`v2.30.10`) | Committed selection; qualification still requires its own executed proof. |
| EventStore | `f213e4115ec23cb736e8aa0113f61b9d4854480c` (`v4.0.4-12-gf213e411`) | Delegated subscription safety source; published package stays 4.0.4 and qualification still requires its own executed proof. |
| FrontComposer | `fbe01c913cb863abb0fd4a317d370d36fff593b3` (`v4.7.5-19-gfbe01c91`) | Owner-approved advance; qualification still requires its own executed proof. |
| McpCli | `2e80701c31aafdf00abee05ac469a45ed76dfdf5` (`v2.0.0-26-g2e80701c`) | Owner-approved advance; qualification still requires its own executed proof. |
| Memories | `d959bdae5af9c22f53573eda71c091cb5c0aae4c` (`v3.0.18-8-gd959bdae`) | Owner-approved advance; qualification still requires its own executed proof. |
| Platform | `ed1560a57f4d4cb55b6b03f485cbfa01316837a7` (`v1.1.7`) | Released Platform tuple; qualification still requires its own executed proof. |
| PolymorphicSerializations | `bce4eb9f45c6b131b960ab4fa39e50ff01e51960` (`v1.19.8-1-gbce4eb9f`) | Committed selection; qualification still requires its own executed proof. |
| Tenants | `33835a03e92579a26400e7cb7cf68d666c715b3e` (`v7.0.0-18-g33835a03`) | Owner-approved advance; qualification still requires its own executed proof. |

### Historical source observation — 2026-11-03 (Story 2.8 released tuple continuation)

The active user's explicit Story 2.8 delegation selects genuine Platform v1.1.7
at `ed1560a57f4d4cb55b6b03f485cbfa01316837a7` and its exact published
Builds v4.43.6 source `725754075e1d6369430a3adf94d46fb71e6eaf9e`.
This is codex-agent source-selection bookkeeping, not a new human approval.
The published CLI, ordinary CI and registered manual baseline select that same
Builds revision. All other incoming root selections remain unchanged.
The graph selects EventStore package `4.0.4`, Commons `2.30.10`,
PolymorphicSerializations `1.19.8`, FrontComposer `4.7.5`, Memories `3.0.18`,
Tenants `7.0.0`, and McpCli `2.0.0`.
Selection does not revalidate historical parity receipts or grant publication,
deployment, custody, rollback deletion, runtime or candidate acceptance.
Every prior human decision and historical observation body is preserved below;
all six canonical parity receipts remain Unvalidated until actually rerun.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `1745b6663ad32bb1288167135ed0f979366c53a0` (`1745b666`) | Committed selection; qualification still requires its own executed proof. |
| Builds | `725754075e1d6369430a3adf94d46fb71e6eaf9e` (`v4.43.6`) | Released catalog pinned by Platform v1.1.7; qualification still requires its own executed proof. |
| Commons | `11555ace9efcae2928a00122dcba91243e5a8a1a` (`v2.30.10`) | Committed selection; qualification still requires its own executed proof. |
| EventStore | `a9205f511b4f96b5daf94d65369d6dee63e0c623` (`v4.0.4-11-ga9205f51`) | Owner-approved advance; qualification still requires its own executed proof. |
| FrontComposer | `fbe01c913cb863abb0fd4a317d370d36fff593b3` (`v4.7.5-19-gfbe01c91`) | Owner-approved advance; qualification still requires its own executed proof. |
| McpCli | `2e80701c31aafdf00abee05ac469a45ed76dfdf5` (`v2.0.0-26-g2e80701c`) | Owner-approved advance; qualification still requires its own executed proof. |
| Memories | `d959bdae5af9c22f53573eda71c091cb5c0aae4c` (`v3.0.18-8-gd959bdae`) | Owner-approved advance; qualification still requires its own executed proof. |
| Platform | `ed1560a57f4d4cb55b6b03f485cbfa01316837a7` (`v1.1.7`) | Released Platform tuple; qualification still requires its own executed proof. |
| PolymorphicSerializations | `bce4eb9f45c6b131b960ab4fa39e50ff01e51960` (`v1.19.8-1-gbce4eb9f`) | Committed selection; qualification still requires its own executed proof. |
| Tenants | `33835a03e92579a26400e7cb7cf68d666c715b3e` (`v7.0.0-18-g33835a03`) | Owner-approved advance; qualification still requires its own executed proof. |

### Historical source observation — 2026-11-04 (incoming main 3e436762 selection)

Administrator / jpiquot approved on 2026-11-03 keeping the EventStore,
FrontComposer, McpCli, Memories and Tenants gitlinks that `/pushall` advanced to
their reviewed `origin/main` tips in Parties
`f7ef3e23561b58c3dca38c787258e5b2d5fa0d86`. Because `prepare-module-build`
requires an exact Platform release tag (HXW011) and the Builds identity pinned
by that release (HXW009), the owner then chose a released Platform/Builds
tuple: Platform v1.1.6 at `8ea8fe092f8abb5c932a61830aae16051d442463` pins
Builds v4.43.5 at `89f62052c932d054bda2b435bdfef60d96e77612`, whose catalog
carries a single `Azure.Security.KeyVault.Keys` pin. The untagged `/pushall`
Platform `1080e92c0eedfd5cbb048871da475eb0a6c71c86` and Builds
`725754075e1d6369430a3adf94d46fb71e6eaf9e` advances are withdrawn. AI.Tools,
Commons and PolymorphicSerializations are unchanged. The selected Builds catalog
keeps the package defaults of the Story 2.8 selection below. The graph selects
EventStore package `4.0.4`, Commons `2.30.10`, PolymorphicSerializations `1.19.8`,
FrontComposer `4.7.5`, Memories `3.0.18`, Tenants `7.0.0`, and McpCli `2.0.0`.
Selection does not revalidate historical parity receipts or grant publication,
deployment, custody, rollback deletion, runtime or candidate acceptance. All
earlier dated decisions and their source/package values remain below as history.

The user subsequently delegated implementation and main publication of the
subscription safety fixes. EventStore source now selects `f213e4115ec23cb736e8aa0113f61b9d4854480c`
(`v4.0.4-12-gf213e411`), preserving the exact qualified cohort. Package versions
are unchanged. The subscription safety row below records this separate delegated
selection; the earlier owner decisions remain historical.

On 2026-11-04, the delegated CI repair selected the released Platform v1.1.8
at `e2cc081e539942563ec0d6a243b308b4cab77898` and its matching released
Builds v4.43.6 at `725754075e1d6369430a3adf94d46fb71e6eaf9e`. The source
selection aligns the package catalog used by `prepare-module-build`; the
effective package versions and historical owner decisions remain unchanged.

The subsequent explicit request to push the existing working-tree changes selects
Platform v1.1.6 and Builds v4.43.5 again. Their existing release signoffs remain
valid; the current source identities below supersede the earlier CI repair
selection. Effective package versions and historical parity receipts are unchanged.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `1745b6663ad32bb1288167135ed0f979366c53a0` (`1745b666`) | Committed selection; qualification still requires its own executed proof. |
| Builds | `89f62052c932d054bda2b435bdfef60d96e77612` (`v4.43.5`) | Released catalog pinned by Platform v1.1.6; qualification still requires its own executed proof. |
| Commons | `11555ace9efcae2928a00122dcba91243e5a8a1a` (`v2.30.10`) | Committed selection; qualification still requires its own executed proof. |
| EventStore | `f213e4115ec23cb736e8aa0113f61b9d4854480c` (`v4.0.4-12-gf213e411`) | Delegated subscription safety source; published EventStore package stays 4.0.4. Transport proof is recorded in the dated subscription receipt. |
| FrontComposer | `fbe01c913cb863abb0fd4a317d370d36fff593b3` (`v4.7.5-19-gfbe01c91`) | Owner-approved advance; qualification still requires its own executed proof. |
| McpCli | `2e80701c31aafdf00abee05ac469a45ed76dfdf5` (`v2.0.0-26-g2e80701c`) | Owner-approved advance; qualification still requires its own executed proof. |
| Memories | `d959bdae5af9c22f53573eda71c091cb5c0aae4c` (`v3.0.18-8-gd959bdae`) | Owner-approved advance; qualification still requires its own executed proof. |
| Platform | `8ea8fe092f8abb5c932a61830aae16051d442463` (`v1.1.6`) | Released Platform tuple; qualification still requires its own executed proof. |
| PolymorphicSerializations | `bce4eb9f45c6b131b960ab4fa39e50ff01e51960` (`v1.19.8-1-gbce4eb9f`) | Committed selection; qualification still requires its own executed proof. |
| Tenants | `33835a03e92579a26400e7cb7cf68d666c715b3e` (`v7.0.0-18-g33835a03`) | Owner-approved advance; qualification still requires its own executed proof. |

### Historical source observation — 2026-11-03 (owner-approved v1.1.6 selection)

Administrator / jpiquot approved on 2026-11-03 keeping the EventStore,
FrontComposer, McpCli, Memories and Tenants gitlinks that `/pushall` advanced to
their reviewed `origin/main` tips in Parties
`f7ef3e23561b58c3dca38c787258e5b2d5fa0d86`. Because `prepare-module-build`
requires an exact Platform release tag (HXW011) and the Builds identity pinned
by that release (HXW009), the owner then chose a released Platform/Builds
tuple: Platform v1.1.6 at `8ea8fe092f8abb5c932a61830aae16051d442463` pins
Builds v4.43.5 at `89f62052c932d054bda2b435bdfef60d96e77612`, whose catalog
carries a single `Azure.Security.KeyVault.Keys` pin. The untagged `/pushall`
Platform `1080e92c0eedfd5cbb048871da475eb0a6c71c86` and Builds
`725754075e1d6369430a3adf94d46fb71e6eaf9e` advances are withdrawn. AI.Tools,
Commons and PolymorphicSerializations are unchanged. The selected Builds catalog
keeps the package defaults of the Story 2.8 selection below. The graph selects
EventStore package `4.0.4`, Commons `2.30.10`, PolymorphicSerializations `1.19.8`,
FrontComposer `4.7.5`, Memories `3.0.18`, Tenants `7.0.0`, and McpCli `2.0.0`.
Selection does not revalidate historical parity receipts or grant publication,
deployment, custody, rollback deletion, runtime or candidate acceptance. All
earlier dated decisions and their source/package values remain below as history.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `1745b6663ad32bb1288167135ed0f979366c53a0` (`1745b666`) | Committed selection; qualification still requires its own executed proof. |
| Builds | `89f62052c932d054bda2b435bdfef60d96e77612` (`v4.43.5`) | Released catalog pinned by Platform v1.1.6; qualification still requires its own executed proof. |
| Commons | `11555ace9efcae2928a00122dcba91243e5a8a1a` (`v2.30.10`) | Committed selection; qualification still requires its own executed proof. |
| EventStore | `a9205f511b4f96b5daf94d65369d6dee63e0c623` (`v4.0.4-11-ga9205f51`) | Owner-approved advance; qualification still requires its own executed proof. |
| FrontComposer | `fbe01c913cb863abb0fd4a317d370d36fff593b3` (`v4.7.5-19-gfbe01c91`) | Owner-approved advance; qualification still requires its own executed proof. |
| McpCli | `2e80701c31aafdf00abee05ac469a45ed76dfdf5` (`v2.0.0-26-g2e80701c`) | Owner-approved advance; qualification still requires its own executed proof. |
| Memories | `d959bdae5af9c22f53573eda71c091cb5c0aae4c` (`v3.0.18-8-gd959bdae`) | Owner-approved advance; qualification still requires its own executed proof. |
| Platform | `8ea8fe092f8abb5c932a61830aae16051d442463` (`v1.1.6`) | Released Platform tuple; qualification still requires its own executed proof. |
| PolymorphicSerializations | `bce4eb9f45c6b131b960ab4fa39e50ff01e51960` (`v1.19.8-1-gbce4eb9f`) | Committed selection; qualification still requires its own executed proof. |
| Tenants | `33835a03e92579a26400e7cb7cf68d666c715b3e` (`v7.0.0-18-g33835a03`) | Owner-approved advance; qualification still requires its own executed proof. |

### Historical source observation — 2026-11-03 (Story 2.8 delegated selection)

The active user's explicit Story 2.8 authorization delegates this source selection
to codex-agent. Builds v4.43.4 at `26991e8c542704be44cd8d9f58084d56fa627adb` and
genuine Platform v1.1.5 at `bf101b9d5f19ba741852b0230e526d1f01422022` form the
proposed package-mode source/catalog identity. Other root selections are unchanged.
The graph selects EventStore package `4.0.4`, Commons `2.30.10`,
PolymorphicSerializations `1.19.8`, FrontComposer `4.7.5`, Memories `3.0.18`,
Tenants `7.0.0`, and McpCli `2.0.0`. These values were evaluated using the actual
published CLI's supported package preparation and the recorded catalog, after
correcting the fallback path declaration order. No package version override is
used. The same selected catalog defaults now govern those families.
Selection does not revalidate historical parity receipts or grant publication,
deployment, custody, rollback deletion, runtime or candidate acceptance. All dated
jpiquot decisions and their original source/package values remain below as
history; this continuation does not impersonate another approval.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `1745b6663ad32bb1288167135ed0f979366c53a0` (`1745b666`) | Committed selection; qualification still requires its own executed proof. |
| Builds | `26991e8c542704be44cd8d9f58084d56fa627adb` (`v4.43.4`) | Committed selection; qualification still requires its own executed proof. |
| Commons | `11555ace9efcae2928a00122dcba91243e5a8a1a` (`v2.30.10`) | Committed selection; qualification still requires its own executed proof. |
| EventStore | `7c93a1234d14c66f5ee85ed48caaee1d30a671f3` (`v3.132.1-2-g7c93a123`) | Committed selection; description remains anchored to `v3.132.1`. Qualification still requires its own executed proof. |
| FrontComposer | `07089dcbdad784a1d53037e9cea78d1d7becd8d6` (`v4.7.5`) | Committed selection; qualification still requires its own executed proof. |
| McpCli | `3bc89642ad43305531c4018b75b5e1b9e0681e10` (`v1.1.0-57-g3bc89642`) | Committed selection; qualification still requires its own executed proof. |
| Memories | `e8956eb5c1bd49297f86702f9606eb9b013bc2f1` (`v3.0.15`) | Committed selection; qualification still requires its own executed proof. |
| Platform | `bf101b9d5f19ba741852b0230e526d1f01422022` (`v1.1.5`) | User-delegated source-only selection of genuine Platform v1.1.5; qualification still requires its own executed proof. |
| PolymorphicSerializations | `bce4eb9f45c6b131b960ab4fa39e50ff01e51960` (`v1.19.8-1-gbce4eb9f`) | Committed selection; qualification still requires its own executed proof. |
| Tenants | `4d0d599879767196928e1c265622483a62b457ae` (`v6.0.0-71-g4d0d5998`) | Committed successor selection; qualification still requires its own executed proof. |

### Historical source observation — 2026-11-02

Administrator / jpiquot approved the ten committed root gitlinks below and the
effective Release package graph for current-selection assertions on 2026-11-02
(DW-182 / I16 decision). After Parties `18579b62ef9ac3c8780fc5e8ba26112d2665f566`
advanced Tenants, the owner approved its exact successor
`4d0d599879767196928e1c265622483a62b457ae` for current-selection
assertions and signoff. The other nine source identities are unchanged.
The subsequent Platform source selection advances to `c635a2dc22f404d97c9fe7e27f7e7f4c9017943b`
for the EventStore 3.133.0 scoped consumer. The earlier approval row remains dated
history; this source update carries no new parity or release qualification.
The graph selects EventStore package `3.132.2`, Commons
`2.30.10`, PolymorphicSerializations `1.19.8`, FrontComposer `4.7.4`, Memories
`3.0.13`, Tenants `6.0.0`, and McpCli `1.1.0`. The selected Builds catalog defaults
EventStore `3.132.1`, FrontComposer `4.7.5`, and Memories `3.0.15`; Parties root
pins override those defaults. Selection does not revalidate historical parity receipts
or grant publication, deployment, custody, or rollback qualification. Tenants
source `4d0d5998` defaults its own `HexalithEventStoreVersion` to `3.133.0` (it
qualified `3.132.2` at Tenants `512209ad`, and its CI tests the `3.129.0` floor and
the `3.133.0` default); Parties ships the Tenants package `6.0.0`, and the Release
solution lane builds that Tenants source with its own default.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `1745b6663ad32bb1288167135ed0f979366c53a0` (`1745b666`) | Committed selection; qualification still requires its own executed proof. |
| Builds | `d2a430c988462a634674a69c562ea4afd1f06ef4` (`v4.42.1-5-gd2a430c9`) | Committed selection; qualification still requires its own executed proof. |
| Commons | `11555ace9efcae2928a00122dcba91243e5a8a1a` (`v2.30.10`) | Committed selection; qualification still requires its own executed proof. |
| EventStore | `7c93a1234d14c66f5ee85ed48caaee1d30a671f3` (`v3.132.1-2-g7c93a123`) | Committed selection; upstream `v3.132.2` tag is absent locally. Qualification still requires its own executed proof. |
| FrontComposer | `07089dcbdad784a1d53037e9cea78d1d7becd8d6` (`v4.7.5`) | Committed selection; qualification still requires its own executed proof. |
| McpCli | `3bc89642ad43305531c4018b75b5e1b9e0681e10` (`v1.1.0-57-g3bc89642`) | Committed selection; qualification still requires its own executed proof. |
| Memories | `e8956eb5c1bd49297f86702f9606eb9b013bc2f1` (`v3.0.15`) | Committed selection; qualification still requires its own executed proof. |
| Platform | `c635a2dc22f404d97c9fe7e27f7e7f4c9017943b` (`v1.1.1-35-gc635a2dc`) | Committed successor source selection for the EventStore 3.133.0 scoped consumer; qualification still requires its own executed proof. |
| PolymorphicSerializations | `bce4eb9f45c6b131b960ab4fa39e50ff01e51960` (`v1.19.8-1-gbce4eb9f`) | Committed selection; qualification still requires its own executed proof. |
| Tenants | `4d0d599879767196928e1c265622483a62b457ae` (`v6.0.0-71-g4d0d5998`) | Committed successor selection; qualification still requires its own executed proof. |

### Historical source observation — 2026-10-30

Observed from committed Parties `f9757d94a9e3f144d250b21f295b903c00d5f412` selections.
EventStore package `3.128.4` remains unchanged. These Git objects describe selected
gitlinks; they do not assert that the mapped source checkout equals that selection
or is clean. The module tool remains the source-root and dependency-mode authority.
Source-mode tests independently record each mapped checkout's actual HEAD, dirty
state and whether it overrides the selection, with `immutable_consumption=false`.
Unmapped immutable-consumption checks still require exact selected identities and
clean checkouts. Selection does not revalidate historical parity receipts or grant
owner approval, package, deployment, data-custody or rollback qualification.
Descriptions below are anchored to the observed tag for the selected Git object.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `70dbf73e80787299c832d30d911b16b105ab194d` (`70dbf73e`) | Observed committed selection only; mapped source overrides and immutable-consumption proof remain separate. |
| Builds | `1884a88ab80d3d0ae80c09a5ed9dfe23867d9fb9` (`v4.38.3-39-g1884a88a`) | Observed committed selection only; mapped source overrides and immutable-consumption proof remain separate. |
| Commons | `3edfbdafc7d4fa915286dce8a37dc4cc7c06085a` (`v2.30.3-5-g3edfbdaf`) | Observed committed selection only; mapped source overrides and immutable-consumption proof remain separate. |
| EventStore | `28ccd32c95ab521755d12deee5d7ad16927e7c92` (`v3.128.4-30-g28ccd32c`) | Observed committed selection only; mapped source overrides and immutable-consumption proof remain separate. |
| FrontComposer | `e7e892b6ed584501f6dd18e11acd299948c0113f` (`v4.7.1-32-ge7e892b6`) | Observed committed selection only; mapped source overrides and immutable-consumption proof remain separate. |
| McpCli | `c05335fc2ed90901bcaeef82a67263fb2d1c8703` (`v1.1.0-25-gc05335fc`) | Observed committed selection only; mapped source overrides and immutable-consumption proof remain separate. |
| Memories | `fc8b46f07f77b73c67cab6687feb07e0e600d585` (`v3.0.10-10-gfc8b46f0`) | Observed committed selection only; mapped source overrides and immutable-consumption proof remain separate. |
| Platform | `aa8c66edc782b1735539e60b2b949f7f4015e3e3` (`v1.0.1-85-gaa8c66ed`) | Observed committed selection only; mapped source overrides and immutable-consumption proof remain separate. |
| PolymorphicSerializations | `221ac4592ba908ae7661ba1fa9e2d1fcc23baee0` (`v1.19.5`) | Observed committed selection only; mapped source overrides and immutable-consumption proof remain separate. |
| Tenants | `4fa78baa94f939a2b752bce52d0bac77214ddd83` (`v5.11.1-16-g4fa78baa`) | Observed committed selection only; mapped source overrides and immutable-consumption proof remain separate. |

### Historical source observation — 2026-10-28 (before descriptor fixture mapping)

This observation reconciles the six fitness failures recorded in
`tests/five-data-lifecycle-qualification-2026-10-28/parent-final-verification.json`.
EventStore package `3.128.4` retains the previously approved root selection and
Tenants 5.11.1 dependency floor described in the published-FrontComposer amendment.
The source rows below identify committed root selections and matching clean
checkouts; they record observed identities, not new owner approval.
Selection does not revalidate historical parity receipts or grant production
migration, data-custody, deployment or rollback qualification. Automated CI and
exact-source registry verification remain separate required release evidence.
The module tool remains the source-root and dependency-mode authority.
Source descriptions are anchored to the observed tag; the EventStore source
anchor `v3.128.3` is separate from the approved `3.128.4` package selection.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| EventStore | `36c78f3435d2d492a73f1b0707cf865c8735d740` (`v3.128.3-6-g36c78f34`) | Package `3.128.4`; consumer qualification belongs to current executed receipts. |
| Memories | `72c36c7113c750c8b809f9d72b945a2a8c2d4555` (`v3.0.3-4-g72c36c71`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| FrontComposer | `5af87f9e5cc8db16896fc97d19b6a828b2ed0d0d` (`v4.7.1-7-g5af87f9e`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| Tenants | `6deb5d5dbaad4e26405910b347d45b82deae1057` (`v5.11.0-13-g6deb5d5d`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` (`3f194e17`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| Commons | `3edfbdafc7d4fa915286dce8a37dc4cc7c06085a` (`v2.30.3-5-g3edfbdaf`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| Builds | `d1931b1e803144048d927fff16cb8119afde541e` (`v4.38.2-2-gd1931b1e`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| PolymorphicSerializations | `221ac4592ba908ae7661ba1fa9e2d1fcc23baee0` (`v1.19.5`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| McpCli | `2d7311cbbcc592c0ea0348463037389251d1cb4b` (`v1.1.0-5-g2d7311cb`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| Platform | `8de90958bc41b7fd01b7ed22b8ab364447a800e4` (`v1.0.1-8-g8de90958`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |

### Historical source observation — 2026-10-28 (initial synchronization)

Observed at Parties `11113687277f49e9bb74d91d6db3c869a337bb40`.
The following text and identities retain that observation's original scope.

The operator requested synchronization of the root submodules and a release.
These exact revisions are clean upstream main selections; the new Builds catalog
aligns Aspire 13.6.4 with the AppHost SDK. EventStore package `3.128.3` matches
the independently evaluated external runtime package graphs.
Selection does not revalidate historical parity receipts or grant production
migration, data-custody, deployment or rollback qualification. Automated CI and
exact-source registry verification remain separate required release evidence.
The module tool remains the source-root and dependency-mode authority.
Source descriptions are anchored to the tag captured in each observation; later
tags on the same exact commit do not rewrite that historical descriptor.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| EventStore | `eec6ed131a8959c85ae191dff75f88dd34a74832` (`v3.128.3-2-geec6ed13`) | Package `3.128.3`; consumer qualification belongs to current executed receipts. |
| Memories | `c9d07c7cdbb93f4c0999b85bfca11a2ffc5d0cbe` (`v3.0.0-2-gc9d07c7c`) | Observed source selection only; package/runtime gates retain their own evidence. |
| FrontComposer | `8f7c4906a91387c2caf8d46dddc4d3e6dd0af905` (`v4.7.1-1-g8f7c4906`) | Observed source selection only; package/runtime gates retain their own evidence. |
| Tenants | `3d13880bceb43fbbb686ad5089170f9ce5f97eb3` (`v5.10.1-93-g3d13880b`) | Observed source selection only; package/runtime gates retain their own evidence. |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` (`3f194e17`) | Observed source selection only; package/runtime gates retain their own evidence. |
| Commons | `1663028fb6d9f46a0bf17919edeb21bb99be1587` (`v2.30.3-4-g1663028f`) | Observed source selection only; package/runtime gates retain their own evidence. |
| Builds | `1745ea0f09df9e518aeea3eeb1a7df836f67e09d` (`v4.38.1-5-g1745ea0f`) | Observed source selection only; package/runtime gates retain their own evidence. |
| PolymorphicSerializations | `221ac4592ba908ae7661ba1fa9e2d1fcc23baee0` (`v1.19.5`) | Observed source selection only; package/runtime gates retain their own evidence. |
| McpCli | `8a860ee409d0c6f87a82bfdb87d9a84a15c49018` (`v1.1.0-1-g8a860ee4`) | Observed source selection only; package/runtime gates retain their own evidence. |
| Platform | `c8acfe4ee08737ec0beca735083f7e08cd8885d0` (`v1.0.0-2-gc8acfe4e`) | Observed source selection only; package/runtime gates retain their own evidence. |

### Historical source observation — 2026-10-27

The approved five-item legacy recovery task starts at Parties
`67d16261ee9ba5f8b8595351ba96c211ebb5f9d2`. Only the Memories root selection
changes, to reviewed owner commit `f480fd70a7c25587264f50b5174358eee709c513`, rebased onto
upstream `2bfc735600ed153bce9069570a43ba93fe77ac0f`. The source implementation and
runtime/evidence qualification are retained in
`tests/five-legacy-recovery-and-evidence-2026-10-27/`; the prior dated observation
remains below. Selection does not revalidate historical parity receipts or grant
publication, deployment, custody, migration or owner-release authority.

EventStore package `3.128.0` and the effective central catalog remain selected.
The module tool remains the sole source-root and mode authority. Inspect each
exact source object listed below with `git -C references/Hexalith.<dependency>
rev-parse <revision>^{commit}` and `git -C references/Hexalith.<dependency>
describe --tags --always --abbrev=8 <revision>`. Moving HEAD diagnostics establish
current state only; they do not reproduce a dated receipt.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| EventStore | `1b404e7fbcdbb5604418f04025d16756436cec24` (`v3.128.0-2-g1b404e7f`) | Package `3.128.0`; qualification belongs to the current executed receipts. |
| Memories | `f480fd70a7c25587264f50b5174358eee709c513` (`v2.29.15-38-gf480fd70`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| FrontComposer | `9c4166e4b7cb45e2512e2b6db887a071f35ce244` (`v4.7.0-106-g9c4166e4`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| Tenants | `692c17b27d94677a3a4d065330d150b262cdb570` (`v5.10.1-56-g692c17b2`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` (`3f194e17`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| Commons | `1663028fb6d9f46a0bf17919edeb21bb99be1587` (`v2.30.3-4-g1663028f`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| Builds | `db897eac9f2819220b32d237109d7255218b46a9` (`v4.38.0-14-gdb897eac`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| PolymorphicSerializations | `221ac4592ba908ae7661ba1fa9e2d1fcc23baee0` (`v1.19.5`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| McpCli | `dc282889e624a4c990faa3f005ad7eaa8db7a473` (`v1.0.0-76-gdc282889`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| Platform | `8d61addebff5bb2f0af81b3e373d2d31a3a774a2` (`8d61adde`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |

### Historical source observation — 2026-10-26

The approved published-SDK closure task starts at Parties
`463ad1bf14701f0622b68af4352e07941d1a7969`. Every source row below matches its
committed root gitlink and clean checkout. These are selection facts only.
Selection does not revalidate historical parity receipts, grant owner approval,
or change migration, custody, deployment or release authority.

EventStore package `3.128.0` is selected explicitly before the Builds catalog.
Its published repository revision is `c5d1c280d8c1c1a207cd159d817f614e93c93130`,
containing semantic query aliases and both recovery fixes. Actual restored
package identities and executed consumer controls are recorded in
`tests/five-published-sdk-and-runtime-closure-2026-10-26/`.
The effective catalog selects Commons `2.30.3`, FrontComposer `4.7.0`, Memories
`2.29.9`, Tenants `5.10.1` and Parties `1.6.0`;
CommunityToolkit.Aspire.Hosting.Dapr remains `13.6.1-beta.935`.

The Memories Server is non-packable and the AppHost resolves its actual project
by path. Its algorithm-policy qualification therefore uses the selected Server
project registration and compares the relevant source with released `2.29.15`
revision `38128624763c313238904c30aaeb88eb4fd9a6d0`. The client package cannot
establish the Server policy. Refresh/inventory APIs absent from that release,
sidecar secret-token publication and container/deployment gates remain separate.
The selected EventStore Story 8.5 is `review`; this structural observation does
not establish G5 availability or change its retained rollback obligations.

Reproduce this dated observation with the immutable root revision:
`git ls-tree 463ad1bf14701f0622b68af4352e07941d1a7969 references/Hexalith.<dependency>`.
Resolve each dependency's full source revision from that immutable root tree,
then inspect exactly that object rather than the dependency checkout's HEAD:

```bash
for dependency in EventStore Memories FrontComposer Tenants AI.Tools Commons Builds PolymorphicSerializations McpCli Platform; do
    read -r mode kind revision path < <(git ls-tree 463ad1bf14701f0622b68af4352e07941d1a7969 "references/Hexalith.$dependency")
    git -C "$path" rev-parse "$revision^{commit}"
    git -C "$path" describe --tags --always --abbrev=8 "$revision"
done
```

These commands reproduce committed identities; the captured clean-checkout status
is retained in `tests/five-published-sdk-and-runtime-closure-2026-10-26/package-provenance.json`.
Current-state diagnostics are separate: `git ls-tree HEAD references/Hexalith.<dependency>`,
`git -C references/Hexalith.<dependency> rev-parse HEAD` and
`git -C references/Hexalith.<dependency> status --porcelain` inspect today's checkout
and cannot reproduce the dated clean-checkout observation.
The root pin retains the pinned module tool as the sole source-root/mode authority;
source selection and default Release packages are distinct.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| EventStore | `1b404e7fbcdbb5604418f04025d16756436cec24` (`v3.128.0-2-g1b404e7f`) | Package `3.128.0`; qualification belongs to the current executed receipts. |
| Memories | `22b792cf4f4a7aa5c9b6f129e6a491821169a8bf` (`v2.29.15-36-g22b792cf`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| FrontComposer | `9c4166e4b7cb45e2512e2b6db887a071f35ce244` (`v4.7.0-106-g9c4166e4`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| Tenants | `692c17b27d94677a3a4d065330d150b262cdb570` (`v5.10.1-56-g692c17b2`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` (`3f194e17`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| Commons | `1663028fb6d9f46a0bf17919edeb21bb99be1587` (`v2.30.3-4-g1663028f`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| Builds | `db897eac9f2819220b32d237109d7255218b46a9` (`v4.38.0-14-gdb897eac`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| PolymorphicSerializations | `221ac4592ba908ae7661ba1fa9e2d1fcc23baee0` (`v1.19.5`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| McpCli | `dc282889e624a4c990faa3f005ad7eaa8db7a473` (`v1.0.0-76-gdc282889`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| Platform | `8d61addebff5bb2f0af81b3e373d2d31a3a774a2` (`8d61adde`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |

### Historical source observation — 2026-10-23

An earlier external `/pushall` advanced the root source selections in Parties
`ec3d919c159aa87453910011618cb6d07f873baa` during the prior five-item task.
The following identities are observed selection facts, without extending owner
approval, historical parity, custody, migration or release authority. Selection does not revalidate historical parity receipts.


The reviewed recovery-integration task selects EventStore `da50f853b6c2c37193e9b1a8819858778d48d0f3`,
Memories `5a852389327bf45df7d59b2c2e29c7adbe353f19` and FrontComposer
`aee73e5841b4004803dafa851f060e143550716a`. All three owner checkouts are clean
and these commits are published on their main branches. The operator approved the
frozen five-item spec and instructed publication on main; source selection is
limited to this reviewed work and preserved incoming owner ancestry. Other
externally advanced rows remain observations, and historical parity is unchanged.

The user selected EventStore package `3.127.0`. The effective catalog selects
Commons `2.30.3`, FrontComposer `4.7.0`, Memories `2.29.8`, Tenants `5.10.1` and
Parties `1.6.0`; CommunityToolkit.Aspire.Hosting.Dapr remains `13.6.1-beta.935`.
The root package pin retains the pinned module tool as source-root/mode authority.
Package selection, source observations and immutable publication executors remain
separate. The source SDK recovery fix is absent from published 3.127.0.

The incoming EventStore lifecycle work records owner Story 8.5 as `in-progress`.
The live structural search below reflects that state. Dated backlog/parity
receipts remain historical; this work does not establish G5 availability or
extend its approvals.


Reproduce observations with `git ls-tree HEAD references/Hexalith.<dependency>`,
`git -C references/Hexalith.<dependency> rev-parse HEAD`, `git -C references/Hexalith.<dependency> describe --tags --always --abbrev=8 HEAD`
and `git -C references/Hexalith.<dependency> status --porcelain`. A dirty source
checkout is not clean-commit qualification. The three reviewed owner follow-ups
are committed and pushed; no historical receipt below is recertified.

Default package mode and the explicit source graph remain distinct; ordinary Release builds
consume Commons HTTP from its package. The pinned `hexalith-module` tool is the
sole source-root/mode authority. Repository-local autodetection is absent;
`UseHexalithProjectReferences=true` alone does not supply roots or select source.
Catalog selection is separate from the
immutable validation/release workflow pins and their approval receipts.

Reproduce the committed source selection with:

```bash
git ls-tree HEAD references/Hexalith.<dependency>
git -C references/Hexalith.<dependency> rev-parse HEAD
git -C references/Hexalith.<dependency> describe --tags --always --abbrev=8 HEAD
git -C references/Hexalith.<dependency> status --porcelain
dotnet msbuild src/Hexalith.Parties/Hexalith.Parties.csproj -p:Configuration=Release -getProperty:HexalithEventStoreVersion,HexalithCommonsVersion,HexalithFrontComposerVersion,HexalithMemoriesVersion,HexalithTenantsVersion,HexalithPartiesVersion
```

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| EventStore | `da50f853b6c2c37193e9b1a8819858778d48d0f3` (`v3.127.0-8-gda50f853`) | Package `3.127.0`; local source recovery is qualified; matching package adoption stays open. |
| Memories | `5a852389327bf45df7d59b2c2e29c7adbe353f19` (`v2.29.11-4-g5a852389`) | Approved task source selection; MCP policy tested; package/deployment gates remain separate. |
| FrontComposer | `aee73e5841b4004803dafa851f060e143550716a` (`v4.7.0-76-gaee73e58`) | Approved task source selection; mapped UI asset delivery tested; package/parity gates remain separate. |
| Tenants | `48b0cd98be5ff75402e1206e083e561552146454` (`v5.10.1-30-g48b0cd98`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` (`3f194e17`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| Commons | `1663028fb6d9f46a0bf17919edeb21bb99be1587` (`v2.30.3-4-g1663028f`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| Builds | `8c9caa77feb85a2dce03153519d81684128890a6` (`v4.37.0-17-g8c9caa77`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| PolymorphicSerializations | `221ac4592ba908ae7661ba1fa9e2d1fcc23baee0` (`v1.19.5`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| McpCli | `d9dc31a2920ef9daf31117c1a917cfffcad110ef` (`v1.0.0-63-gd9dc31a2`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |
| Platform | `ae932d038a03846e1349fc550a3ae1a3a5d14ab0` (`ae932d03`) | Observed source selection only; package/runtime/owner gates retain their own evidence. |

Source-mode consumer property fixtures use Debug and explicit inputs, for
example:

```bash
dotnet msbuild src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:HexalithEventStoreRoot="$PWD/references/Hexalith.EventStore" -p:HexalithEventStoreFromSource=true -p:HexalithCommonsRoot="$PWD/references/Hexalith.Commons" -p:HexalithCommonsHttpFromSource=true -getItem:PackageReference,ProjectReference
```

The fitness gate evaluates default Release, explicit package mode, Debug with
only the bare source switch, and explicit source property fixtures. Consumer
discovery uses package/project identities independently of selector conditions.
Effective versions include VersionOverride and centrally evaluated PackageVersion
metadata; selected project paths must exist. It retains negative sibling-layout coverage without mapping and positive
coverage with explicit `HexalithMcpCliRoot`/`HexalithMcpCliFromSource=true` property fixtures
for both supported McpCli layouts; only Abstractions may be a source reference.
These consumer property fixtures do not run generated imports, mapping-hash
checks, or tool validation targets and therefore do not verify actual module-tool
mapping. They prove consumer branch selection only, not tool composition, source
compilation, package release or historical parity. Current describe values identify these exact checkouts; they do not change
the selected package versions or extend historical qualification.

Selection does not revalidate historical parity receipts or authorize deletion
of migration rollback paths. Historical owner approvals remain attached to their
recorded identities; G5 remains `needs-additive-api`.

### Historical source observation — 2026-10-21

Deferred-work validation reconciles the independently committed
source graph at Parties `63cd1279a82df47af3d16dd0ff9f002f3e4c86eb`. Every root checkout below
matches its committed gitlink and has an empty `git status --porcelain` result.
This records present-tense selection facts only; it does not extend dated owner
approvals, parity/runtime/custody qualification or migration authority. The
2026-10-20 observation and earlier receipts remain historical below.

The imported Builds catalog and evaluated default Release graph select EventStore package `3.126.1`, Commons `2.30.3`, FrontComposer `4.7.0`,
Memories `2.27.1`, Tenants `5.9.0`, and Parties `1.5.1`.
Default package mode and the explicit source graph remain distinct; ordinary Release builds
consume Commons HTTP from its package. The pinned `hexalith-module` tool is the
sole source-root/mode authority. Repository-local autodetection is absent;
`UseHexalithProjectReferences=true` alone does not supply roots or select source.
Catalog selection is separate from the
immutable validation/release workflow pins and their approval receipts.

Reproduce the committed source selection with:

```bash
git ls-tree 63cd1279a82df47af3d16dd0ff9f002f3e4c86eb references/Hexalith.<dependency>
git -C references/Hexalith.<dependency> rev-parse HEAD
git -C references/Hexalith.<dependency> describe --tags --always --abbrev=8 HEAD
git -C references/Hexalith.<dependency> status --porcelain
dotnet msbuild src/Hexalith.Parties/Hexalith.Parties.csproj -p:Configuration=Release -getProperty:HexalithEventStoreVersion,HexalithCommonsVersion,HexalithFrontComposerVersion,HexalithMemoriesVersion,HexalithTenantsVersion,HexalithPartiesVersion
```

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` (`3f194e17`) | Instruction baseline. |
| Builds | `3fcb8f38919bb0961b06a19b57aa45623b35a33e` (`v4.35.1-13-g3fcb8f38`) | Imported package catalog and build tooling; EventStore 3.126.1 selected. |
| Commons | `5c23c818f639c5bb3175f964c5549440bd5b876d` (`v2.30.3`) | Default Release package is `2.30.3`; source requires explicit tool mapping. |
| EventStore | `1748b16b11fb63b885dfa2fd4a9c7a420ea103a9` (`v3.126.1`) | Source inspection; runtime consumers restore package `3.126.1`. |
| FrontComposer | `a258b992d08e73e2d3251c865ef01a982eba31ac` (`v4.7.0-42-ga258b992`) | Selected package is `4.7.0`. |
| McpCli | `7f4a08cb4b7ff9402d1dcf0826ad45ab0256ba65` (`v1.0.0-42-g7f4a08cb`) | Committed root pointer; not used for G5 proof. |
| Memories | `0af009c20c97038b685f3d4b62c0c8e757982d14` (`v2.29.8`) | Selected package remains `2.27.1`. |
| Platform | `d189c7de74293e1a718e5e78b1b3b7c0e47b48b5` (`d189c7de`) | Committed root pointer; not used for G5 proof. |
| PolymorphicSerializations | `221ac4592ba908ae7661ba1fa9e2d1fcc23baee0` (`v1.19.5`) | Existing source dependency. |
| Tenants | `66def172379f37d12e746fe6eb9195801929c572` (`v5.9.0-50-g66def172`) | Selected package is `5.9.0`. |

Source-mode consumer property fixtures use Debug and explicit inputs, for
example:

```bash
dotnet msbuild src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:HexalithEventStoreRoot="$PWD/references/Hexalith.EventStore" -p:HexalithEventStoreFromSource=true -p:HexalithCommonsRoot="$PWD/references/Hexalith.Commons" -p:HexalithCommonsHttpFromSource=true -getItem:PackageReference,ProjectReference
```

The fitness gate evaluates default Release, explicit package mode, Debug with
only the bare source switch, and explicit source property fixtures. Consumer
discovery uses package/project identities independently of selector conditions.
Effective versions include VersionOverride and centrally evaluated PackageVersion
metadata; selected project paths must exist. It retains negative sibling-layout coverage without mapping and positive
coverage with explicit `HexalithMcpCliRoot`/`HexalithMcpCliFromSource=true` property fixtures
for both supported McpCli layouts; only Abstractions may be a source reference.
These consumer property fixtures do not run generated imports, mapping-hash
checks, or tool validation targets and therefore do not verify actual module-tool
mapping. They prove consumer branch selection only, not tool composition, source
compilation, package release or historical parity. Current describe values identify these exact checkouts; they do not change
the selected package versions or extend historical qualification.

Selection does not revalidate historical parity receipts or authorize deletion
of migration rollback paths. Historical owner approvals remain attached to their
recorded identities; G5 remains `needs-additive-api`.

### Historical source observation — 2026-10-20

Story 4.10 release-prerequisite verification reconciles the independently committed
source graph at Parties `ea926163fb51033b226541f069ebf6a5941922af`. Every root checkout below
matches its committed gitlink and has an empty `git status --porcelain` result.
This records present-tense selection facts only; it does not extend dated owner
approvals, parity/runtime/custody qualification or migration authority. The
2026-10-18 observation and earlier receipts remain historical below.

The imported Builds catalog and evaluated default Release graph select EventStore package `3.126.0`, Commons `2.30.3`, FrontComposer `4.7.0`,
Memories `2.27.1`, Tenants `5.9.0`, and Parties `1.5.0`.
Default package mode and the explicit source graph remain distinct; ordinary Release builds
consume Commons HTTP from its package. The pinned `hexalith-module` tool is the
sole source-root/mode authority. Repository-local autodetection is absent;
`UseHexalithProjectReferences=true` alone does not supply roots or select source.
Catalog selection is separate from the
immutable validation/release workflow pins and their approval receipts.

Reproduce the committed source selection with:

```bash
git ls-tree ea926163fb51033b226541f069ebf6a5941922af references/Hexalith.<dependency>
git -C references/Hexalith.<dependency> rev-parse HEAD
git -C references/Hexalith.<dependency> describe --tags --always --abbrev=8 HEAD
git -C references/Hexalith.<dependency> status --porcelain
dotnet msbuild src/Hexalith.Parties/Hexalith.Parties.csproj -p:Configuration=Release -getProperty:HexalithEventStoreVersion,HexalithCommonsVersion,HexalithFrontComposerVersion,HexalithMemoriesVersion,HexalithTenantsVersion,HexalithPartiesVersion
```

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` (`3f194e17`) | Instruction baseline. |
| Builds | `fff4a212180d8cd1c5c0759655a9bc4fb6963c45` (`v4.35.0`) | Imported package catalog and build tooling; EventStore 3.126.0 selected. |
| Commons | `5c23c818f639c5bb3175f964c5549440bd5b876d` (`v2.30.3`) | Default Release package is `2.30.3`; source requires explicit tool mapping. |
| EventStore | `54261aeccb1d8d8c5c99beb2437e2e8a5ad69eaa` (`v3.126.0-20-g54261aec`) | Source inspection; runtime consumers restore package `3.126.0`. |
| FrontComposer | `913c4132c71cd7f77fcdf6befa9e396d284dd4bc` (`v4.7.0-16-g913c4132`) | Selected package is `4.7.0`. |
| McpCli | `9354dc08176744c75c2ddb61d924c0948b373b98` (`v1.0.0-32-g9354dc08`) | Committed root pointer; not used for G5 proof. |
| Memories | `35df7e2080c369003cacac12f5b44117d7f3ed35` (`v2.29.5`) | Selected package remains `2.27.1`. |
| Platform | `4f036ced45aa40700948c19949bb874881c960b5` (`4f036ced`) | Committed root pointer; not used for G5 proof. |
| PolymorphicSerializations | `221ac4592ba908ae7661ba1fa9e2d1fcc23baee0` (`v1.19.5`) | Existing source dependency. |
| Tenants | `3ae9db014dfe52bdee4f6731465cc2a7b65701b7` (`v5.9.0-26-g3ae9db01`) | Selected package is `5.9.0`. |

Source-mode consumer property fixtures use Debug and explicit inputs, for
example:

```bash
dotnet msbuild src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:HexalithEventStoreRoot="$PWD/references/Hexalith.EventStore" -p:HexalithEventStoreFromSource=true -p:HexalithCommonsRoot="$PWD/references/Hexalith.Commons" -p:HexalithCommonsHttpFromSource=true -getItem:PackageReference,ProjectReference
```

The fitness gate evaluates default Release, explicit package mode, Debug with
only the bare source switch, and explicit source property fixtures. Consumer
discovery uses package/project identities independently of selector conditions.
Effective versions include VersionOverride and centrally evaluated PackageVersion
metadata; selected project paths must exist. It retains negative sibling-layout coverage without mapping and positive
coverage with explicit `HexalithMcpCliRoot`/`HexalithMcpCliFromSource=true` property fixtures
for both supported McpCli layouts; only Abstractions may be a source reference.
These consumer property fixtures do not run generated imports, mapping-hash
checks, or tool validation targets and therefore do not verify actual module-tool
mapping. They prove consumer branch selection only, not tool composition, source
compilation, package release or historical parity. Current describe values identify these exact checkouts; they do not change
the selected package versions or extend historical qualification.

Selection does not revalidate historical parity receipts or authorize deletion
of migration rollback paths. Historical owner approvals remain attached to their
recorded identities; G5 remains `needs-additive-api`.

### Historical source observation — 2026-10-18

DW-163 began at Parties `6b0641829b68d22656fb173eec6578c65e8e91f0`.
During implementation, a separate `/pushall` advanced the committed root graph
to `7e73709992512d944f4950e6efb28c6132f9473c` and recorded the authorized
EventStore 3.125.1 catalog selection. The actual selected source identities below
are reconciled to that revision; all ten checkouts match their committed gitlinks
and are clean. This records the concurrent revision change without extending
historical owner approvals or runtime/custody qualification. The original DW-163
spec baseline and historical parity receipts retain their original identities.

The imported Builds catalog selects EventStore package `3.125.1`, Commons
`2.30.1`, FrontComposer `4.6.0`, Memories `2.27.1`, Tenants `5.8.0`, and Parties
`1.2.1`. Its CommunityToolkit.Aspire.Hosting.Dapr value remains `13.6.1-beta.935`.
Catalog selection remains separate from release-workflow identities and their
approval receipts.

Reproduce the committed source selection with:

```bash
git ls-tree 7e73709992512d944f4950e6efb28c6132f9473c references/Hexalith.<dependency>
git -C references/Hexalith.<dependency> rev-parse HEAD
git -C references/Hexalith.<dependency> describe --tags --always --abbrev=8 HEAD
git -C references/Hexalith.<dependency> status --porcelain
```

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` (`3f194e17`) | Instruction baseline. |
| Builds | `332b2c144defba546fe0a09626a4a72f2340e22f` (`v4.33.0-8-g332b2c14`) | Imported package catalog and build tooling; EventStore 3.125.1 selected. |
| Commons | `b7e6e7d5e28568b30c523130339cc4c52e12c674` (`v2.30.1-20-gb7e6e7d5`) | Source fallback; selected package remains `2.30.1`. |
| EventStore | `d55c6f7c11e9bce6f17d490a84c83f1561979d63` (`v3.125.1-3-gd55c6f7c`) | Source inspection; runtime consumers restore package `3.125.1`. |
| FrontComposer | `1513fe3ed21a554eeee7a9d807f9b9e25fdb4cd2` (`v4.6.0-61-g1513fe3e`) | Selected package remains `4.6.0`. |
| McpCli | `c25e2140e14a76b696a76aed30c8302107714fd6` (`v1.0.0-13-gc25e2140`) | Committed root pointer; not used for G5 proof. |
| Memories | `d0c5d8ca96b06e02acdda88f255b33288642a7f7` (`v2.28.1-125-gd0c5d8ca`) | Selected package remains `2.27.1`. |
| Platform | `5f18593639832d1245577dd93ec07687517d3acb` (`5f185936`) | Committed root pointer; not used for G5 proof. |
| PolymorphicSerializations | `80ce5afd99216ce5f591d66c833feafaf371f171` (`v1.19.4-1-g80ce5afd`) | Existing source dependency. |
| Tenants | `c9feedb6656d70c7271e851c844ee29bdfb28c3e` (`v5.8.0-131-gc9feedb6`) | Selected package is `5.8.0`. |

Selection does not revalidate historical parity receipts or authorize deletion
of migration rollback paths. Earlier observations and owner approvals remain
below at their recorded identities; G5 remains `needs-additive-api`.

### Historical source observation — 2026-10-17

DW-127 completion-gate reconciliation observes the committed root graph at
Parties `8299e075f92219c135a4d075aa6ef9a0c574ad5a`. The root checkouts match
their committed gitlinks. This updates observations and fitness expectations
only; dependency versions, gitlinks, dated receipts and owner approvals remain
unchanged. No new owner signoff or platform/custody qualification is inferred.

The imported Builds catalog selects EventStore package `3.124.0`, Commons
`2.30.1`, FrontComposer `4.6.0`, Memories `2.27.1`, Tenants `5.8.0`, and Parties
`1.2.1`. Its CommunityToolkit.Aspire.Hosting.Dapr value is `13.6.1-beta.935`.
Catalog selection remains separate from release-workflow identities and their
approval receipts.

Reproduce the current observations with:

```bash
git ls-tree HEAD references/Hexalith.<dependency>
git -C references/Hexalith.<dependency> rev-parse HEAD
git -C references/Hexalith.<dependency> describe --tags --always --abbrev=8 HEAD
git -C references/Hexalith.<dependency> status --porcelain
```

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` | Instruction baseline. |
| Builds | `4f53d9f37c5b86cfd43fbcc8fa5dd4df19b84c77` (`v4.32.0-6-g4f53d9f3`) | Imported package catalog and build tooling. |
| Commons | `9e874b1a3c6e7b6ff07b6f40b81d36e4e1403798` | Source fallback; selected package remains `2.30.1`. |
| EventStore | `9ed9b9742bd006e568f026cc1c588275cf9db097` (`v3.124.0-10-g9ed9b974`) | Source inspection; runtime consumers restore package `3.124.0`. |
| FrontComposer | `b102b85b148f21fb26d5742e120c344109df7084` (`v4.6.0-44-gb102b85b`) | Selected package remains `4.6.0`. |
| McpCli | `b35b0975a234f1f97e2e35e9dad381d0a988f4af` | Committed root pointer; not used for G5 proof. |
| Memories | `f6b60d5a2e9b81d84fddad789c7d9bed40d3a533` (`v2.28.1-109-gf6b60d5a`) | Selected package remains `2.27.1`. |
| Platform | `b4b6d4a1efcd2f13c6d03270f8e952f27ed8148b` | Committed root pointer; not used for G5 proof. |
| PolymorphicSerializations | `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` | Existing source dependency. |
| Tenants | `0e1fe111a5b4cce684455948b4fd12960589ef55` (`v5.8.0-98-g0e1fe111`) | Selected package is `5.8.0`. |

Selection does not revalidate historical parity receipts or authorize deletion
of migration rollback paths. Earlier observations and owner approvals remain
below at their recorded identities; G5 remains `needs-additive-api`.

### Historical source observation — 2026-10-16 post-sync

DW-119 baseline repair observes the committed root graph at Parties
`95f21bbcd09dfbfa8d1c71a4b382c12c8eef8a0f`. The three source gitlinks advanced in
`86d03a63d31b337cbcde8fc998dcde4396d48df4` are EventStore, FrontComposer, and
Tenants. All ten root checkouts match their committed gitlinks and are clean.
This records current source identity only; `.gitlink-signoff.tsv` and the dated
owner approvals remain unchanged, including their earlier selected identities.
No new owner signoff, folded-snapshot enrollment, or platform qualification is
inferred from this observation or from the local Parties test-project gate.

The imported Builds catalog still selects EventStore package `3.120.0`, Commons
`2.30.1`, FrontComposer `4.6.0`, Memories `2.27.1`, Tenants `5.8.0`, and Parties
`1.2.1`. The owned central SignalR.Common pin stays `10.0.13` with NuGet audit
active. Dependency versions and gitlinks are unchanged by this repair. Builds
catalog identity `9f0121c77e3f085c58f5c926057606db8e6511a7` remains separate from
release tooling identity `397c94a4e246c90b21cf408790fa0d55bf32d795`.

Reproduce the receipts for each root-declared dependency with:

```bash
git ls-tree 95f21bbcd09dfbfa8d1c71a4b382c12c8eef8a0f references/Hexalith.<dependency>
git -C references/Hexalith.<dependency> rev-parse HEAD
git -C references/Hexalith.<dependency> describe --tags --always --abbrev=8 HEAD
git -C references/Hexalith.<dependency> status --porcelain
```

The checkout must match its gitlink and the status output must be empty. The
eight source dependencies checked by package/source fitness remain distinct
from McpCli and Platform.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` | Instruction baseline. |
| Builds | `9f0121c77e3f085c58f5c926057606db8e6511a7` (`v4.31.2-1-g9f0121c7`) | Imported package catalog and build tooling. |
| Commons | `9e874b1a3c6e7b6ff07b6f40b81d36e4e1403798` | Source fallback; selected package remains `2.30.1`. |
| EventStore | `7edc260d8c0bd904f1215f801bad02b99c6694cf` (`v3.122.0-13-g7edc260d`) | Source inspection; runtime consumers restore package `3.120.0`. |
| FrontComposer | `5fac9ab1df26da19ceadf626f9e557884a189e7c` (`v4.6.0-40-g5fac9ab1`) | Selected package remains `4.6.0`. |
| McpCli | `7e99305a621e1bdf7119b6e924348857e2b5a5a4` | Committed root pointer; not used for G5 proof. |
| Memories | `99431f0fed102ea6ddbae33d28b9da4fc7f215a0` (`v2.28.1-99-g99431f0f`) | Selected package remains `2.27.1`. |
| Platform | `c3af1f2c9e7b0d1b003a1bd5978545f664e38167` | Committed root pointer; not used for G5 proof. |
| PolymorphicSerializations | `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` | Existing source dependency. |
| Tenants | `6ad5e502fa5d6e453e24610ef418007e1bd53298` (`v5.8.0-89-g6ad5e502`) | Selected package is `5.8.0`. |

Selection does not revalidate historical parity receipts or authorize deletion
of migration rollback paths. The earlier 2026-10-16 source observation and all
older receipts and owner approvals remain below at their recorded identities.
G5 remains `needs-additive-api`.

### Historical source observation — 2026-10-16 pre-sync

At Parties `493730941329d1bca3b4f6b2f6a67ffe305c2443`, the user's explicit
CI-fix and release instruction selects the seven newer committed Builds, Commons,
EventStore, FrontComposer, Memories, Platform, and Tenants pointers recorded
under release owner jpiquot in `.gitlink-signoff.tsv`. All ten root checkouts
match their committed gitlinks. The eight source dependencies
used by the package/source fitness proof remain distinct from McpCli and
Platform, which are selected root pointers but are not consumed by this G5
proof. The imported Builds catalog still selects EventStore package `3.120.0`,
Commons `2.30.1`, FrontComposer `4.6.0`, Memories `2.27.1`,
Tenants `5.8.0`, and Parties `1.2.1`.

The owned central catalog also pins SignalR.Common `10.0.13` with NuGet audit
active; shared catalog source remains unchanged.

The root Builds catalog selection at
`9f0121c77e3f085c58f5c926057606db8e6511a7` is separate from the SHA-pinned
release preparation and publication tooling in `.github/workflows/release.yml`,
which executes at `397c94a4e246c90b21cf408790fa0d55bf32d795`.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` | Instruction baseline. |
| Builds | `9f0121c77e3f085c58f5c926057606db8e6511a7` (`v4.31.2-1-g9f0121c7`) | Imported package catalog and build tooling. |
| Commons | `9e874b1a3c6e7b6ff07b6f40b81d36e4e1403798` | Source fallback; selected package remains `2.30.1`. |
| EventStore | `210b90da172cc7ca946fbb06cfd44390bffa707b` (`v3.122.0-1-g210b90da`) | Source inspection; runtime consumers restore package `3.120.0`. |
| FrontComposer | `b06f86d554363ea5b8192cbc149a75c820dab2b8` (`v4.6.0-33-gb06f86d5`) | Selected package remains `4.6.0`. |
| McpCli | `7e99305a621e1bdf7119b6e924348857e2b5a5a4` | Signed root pointer; not used for G5 proof. |
| Memories | `99431f0fed102ea6ddbae33d28b9da4fc7f215a0` (`v2.28.1-99-g99431f0f`) | Selected package remains `2.27.1`. |
| Platform | `c3af1f2c9e7b0d1b003a1bd5978545f664e38167` | Signed root pointer; not used for G5 proof. |
| PolymorphicSerializations | `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` | Existing source dependency. |
| Tenants | `51f404513f2657e98f949f58f0df43b99b68a7d0` (`v5.8.0-77-g51f40451`) | Selected package is `5.8.0`. |

At the selected FrontComposer source, projection-connection and lifecycle
components still delegate announcements to `FcSurfaceStatus`. Its shared
status region now renders `aria-live="@(_suppressSpeech ? "off" : "polite")"`:
suppressed speech selects `off`, and other announcements select `polite`.
The marked matrix command checks this full conditional expression at the
exact shared source file. This structural observation does not refresh
accessibility or UI behavior parity; earlier runtime receipts retain their
original identities.

Selection does not revalidate historical parity receipts or authorize deletion
of migration rollback paths. The dated approvals and prior test receipts below
retain their original identities. G5 remains `needs-additive-api`.

### Historical source observation — 2026-10-13

At Parties `1e64c3e1bc37bb1cdfb0c4f1d7e2f174222984c9`, the user's explicit
CI-fix and release instruction selects the six newer committed Builds,
EventStore, FrontComposer, Memories, Platform, and Tenants pointers recorded
under release owner jpiquot in `.gitlink-signoff.tsv`. All ten root checkouts
match their committed gitlinks and are clean. The eight source dependencies
used by the package/source fitness proof remain distinct from McpCli and
Platform, which are selected root pointers but are not consumed by this G5
proof. The imported Builds catalog still selects EventStore package `3.120.0`,
Commons `2.30.1`, FrontComposer `4.6.0`, Memories `2.27.1`,
Tenants `5.8.0`, and Parties `1.2.1`.

The root Builds catalog selection at
`b2f59c59d16fc2234af6a0f850297958c6143e0d` is separate from the SHA-pinned
release preparation and publication tooling in `.github/workflows/release.yml`,
which executes at `397c94a4e246c90b21cf408790fa0d55bf32d795`.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` | Instruction baseline. |
| Builds | `b2f59c59d16fc2234af6a0f850297958c6143e0d` (`v4.30.1-35-gb2f59c59`) | Imported package catalog and build tooling. |
| Commons | `b247ed116c6523f8c596ec0a933eff8973d11568` | Source fallback; selected package remains `2.30.1`. |
| EventStore | `2a7b80a4ec639bdba7ff8acc74cacb4f8880b24c` (`v3.120.0-75-g2a7b80a4`) | Source inspection; runtime consumers restore package `3.120.0`. |
| FrontComposer | `c3174dc917b094f3736b098ac78a531f197dbe56` (`v4.6.0-25-gc3174dc9`) | Selected package remains `4.6.0`. |
| McpCli | `7e99305a621e1bdf7119b6e924348857e2b5a5a4` | Signed root pointer; not used for G5 proof. |
| Memories | `24b1fa7408eabc1db24b7536fd35f575c7a572a7` (`v2.28.1-88-g24b1fa74`) | Selected package remains `2.27.1`. |
| Platform | `be294641fe06490b7f1d9788f383512088842e7d` | Signed root pointer; not used for G5 proof. |
| PolymorphicSerializations | `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` | Existing source dependency. |
| Tenants | `ed7cef6e327156628c0479071f3bf419fd64612c` (`v5.8.0-63-ged7cef6e`) | Selected package is `5.8.0`. |

At the selected FrontComposer source, projection-connection and lifecycle
components still delegate announcements to `FcSurfaceStatus`. Its shared
status region now renders `aria-live="@(_suppressSpeech ? "off" : "polite")"`:
suppressed speech selects `off`, and other announcements select `polite`.
The marked matrix command checks this full conditional expression at the
exact shared source file. This structural observation does not refresh
accessibility or UI behavior parity; earlier runtime receipts retain their
original identities.

Selection does not revalidate historical parity receipts or authorize deletion
of migration rollback paths. The dated approvals and prior test receipts below
retain their original identities. G5 remains `needs-additive-api`.

### Historical source observation — 2026-10-11 post-sync

At Parties `4bd3b1f08f4f1181b07a0c1e68e09e431a2a92ed`, the release owner
selected four newer committed Builds, EventStore, Platform, and Tenants
pointers, separately recorded in `.gitlink-signoff.tsv`. The eight source
dependencies used by the package/source fitness proof have matching clean
checkouts. The imported Builds catalog still selects EventStore package `3.120.0`,
Commons `2.30.1`, FrontComposer `4.6.0`, Memories `2.27.1`,
Tenants `5.8.0`, and Parties `1.2.1`. McpCli and Platform are signed root
pointers but are not consumed by this G5 package/source proof.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` | Instruction baseline. |
| Builds | `608c164c3637240fed2dd92dbfdaaf581ba9846d` (`v4.30.1-33-g608c164c`) | Imported package catalog and build tooling. |
| Commons | `b247ed116c6523f8c596ec0a933eff8973d11568` | Source fallback; selected package remains `2.30.1`. |
| EventStore | `ef6e688672e1b09a78e78237e4dbfb607d3635a5` (`v3.120.0-40-gef6e6886`) | Source inspection; runtime consumers restore package `3.120.0`. |
| FrontComposer | `19f4839231ae6ec7cc6318842eb329c2d04caaa9` (`v4.6.0-9-g19f48392`) | Selected package remains `4.6.0`. |
| McpCli | `7e99305a621e1bdf7119b6e924348857e2b5a5a4` | Signed root pointer; not used for G5 proof. |
| Memories | `499b7a838a36a4cd500ab517d86acf07b1d74ad4` (`v2.28.1-69-g499b7a83`) | Selected package remains `2.27.1`. |
| Platform | `124fc2007333ed5a394c73fdba9975835f4834c8` | Signed root pointer; not used for G5 proof. |
| PolymorphicSerializations | `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` | Existing source dependency. |
| Tenants | `85a5a6cc18ef6a0f2da69e4fd2c2a1a408f32359` (`v5.8.0-40-g85a5a6cc`) | Selected package is `5.8.0`. |

Selection does not revalidate historical parity receipts or authorize deletion
of migration rollback paths. The dated approvals and prior test receipts below
retain their original identities. G5 remains `needs-additive-api`.

### Historical source observation — 2026-10-11 pre-sync

The release owner selected the eight changed committed root pointers recorded
in `.gitlink-signoff.tsv`. At Parties `c8832e9eb4dcfc96c168429cfae620a9592fe563`,
the eight source dependencies verified below have matching clean checkouts.
The imported Builds catalog selects EventStore package `3.120.0`, Commons
`2.30.1`, FrontComposer `4.6.0`, Memories `2.27.1`, Tenants `5.8.0`,
and Parties `1.2.1`. McpCli and Platform are root gitlinks in the selected
release graph, but are not consumed by this G5 package/source proof. Platform
has unrelated local uncommitted work; only its committed pointer was signed off.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` | Instruction baseline. |
| Builds | `1bde4d92bb9a9ce48b19a6ca17236075ea28a78b` (`v4.30.1-32-g1bde4d92`) | Imported package catalog and build tooling. |
| Commons | `b247ed116c6523f8c596ec0a933eff8973d11568` | Source fallback; selected package remains `2.30.1`. |
| EventStore | `b57310fb44ef2231815f3c3cfea09714d942334d` (`v3.120.0-35-gb57310fb`) | Source inspection; runtime consumers restore package `3.120.0`. |
| FrontComposer | `19f4839231ae6ec7cc6318842eb329c2d04caaa9` (`v4.6.0-9-g19f48392`) | Selected package remains `4.6.0`. |
| McpCli | `7e99305a621e1bdf7119b6e924348857e2b5a5a4` | Signed root pointer; not used for G5 proof. |
| Memories | `499b7a838a36a4cd500ab517d86acf07b1d74ad4` (`v2.28.1-69-g499b7a83`) | Selected package remains `2.27.1`. |
| Platform | `6f703dd0227d8fac023ad97d909c5aaa42115578` | Signed committed pointer; local uncommitted work excluded from proof. |
| PolymorphicSerializations | `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` | Existing source dependency. |
| Tenants | `711e100858e59cf8de2b626ea21790d59f969023` (`v5.8.0-39-g711e1008`) | Selected package is `5.8.0`. |

Selection does not revalidate historical parity receipts or authorize deletion
of migration rollback paths. The dated approvals and prior test receipts below
retain their original identities. G5 remains `needs-additive-api`.

### Historical source observation — 2026-10-09

At Parties `91efcfd9ed31a0b4cb39dfe8263ddb48842190fb`, root gitlinks and
clean checkouts match the source identities below. The Parties pre-import pin
and Builds catalog both select EventStore package `3.117.1`. These are observed
source and package identities, not a new adoption approval. The EventStore
Story 8.3 core is now `done`, but its non-packable project is still not a
consumable G5 runtime provider.

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` | Instruction baseline. |
| Builds | `fef031806321793c9effb17235c2465118984432` (`v4.30.1-6-gfef03180`) | Imported package catalog and build tooling. |
| Commons | `b247ed116c6523f8c596ec0a933eff8973d11568` | Source fallback; selected package remains `2.30.1`. |
| EventStore | `75a08f0069d8c2495d9dff20a0deb84edb6cc638` (`v3.117.1-18-g75a08f00`) | Source inspection; runtime consumers restore package `3.117.1`. |
| FrontComposer | `0e114214007c22f5cdbac21a6853cff4208340ee` (`v4.6.0-7-g0e114214`) | Selected package remains `4.6.0`. |
| Memories | `aa4684064a8aa62833dec910f5e988bf094b187e` (`v2.28.1-44-gaa468406`) | Selected package remains `2.27.1`. |
| PolymorphicSerializations | `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` | Existing source dependency. |
| Tenants | `0d8cc8d854738d00a18c0cdfee0e7b82579eaafa` (`v5.8.0-9-g0d8cc8d8`) | Selected package remains `5.7.0`. |

Selection does not revalidate historical parity receipts or authorize deletion
of migration rollback paths. The dated approvals and prior test receipts below
retain their original identities. G5 remains `needs-additive-api`.

### Historical source observation — 2026-10-08

Administrator / jpiquot authorized the EventStore CI fix, normal full-CI release,
and Parties package upgrade on 2026-10-08. EventStore package `3.117.1` is
selected before the shared catalog import because sidecar-channel authorization
requires the API first published in `3.117.0`. The previously verified Builds
catalog defaulted to `3.115.0`; the workspace catalog now also selects `3.117.1`.
The table records the owner-committed graph at Parties
`ccaf77399d92be696e44ecb8ab17acf77d97646a`, verified against each clean checkout.
The package patch and advanced source checkouts have separate identities;
[the 3.117.1 receipt](tests/eventstore-3.117.1-consumer-2026-10-08/README.md)
retains its original observations. This refresh grants no new owner approval and
does not revalidate
historical parity, release, consumer, or rollback receipts. Package values are
EventStore `3.117.1`, Commons `2.30.1`, Memories `2.27.1`, Tenants `5.7.0`,
FrontComposer `4.6.0`, and Parties `1.1.1`.

Reproduce each source receipt at the recorded Parties revision with
`git ls-tree HEAD references/Hexalith.<dependency>`,
`git -C references/Hexalith.<dependency> rev-parse HEAD`,
`git -C references/Hexalith.<dependency> describe --tags --always --abbrev=8 HEAD`,
and `git -C references/Hexalith.<dependency> status --porcelain`; the checkout must
match the committed pointer and remain clean. Package catalog inspection is
separate from this source proof. Published package provenance and current consumer
checks are in [the package upgrade receipt](tests/eventstore-package-upgrade-2026-10-08/README.md).

| Root dependency | Current source identity | Release use |
| --- | --- | --- |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` | Instruction baseline; unchanged. |
| Builds | `f717a87c26a8266bdde95d18f998ef2ab366d43a` (`v4.30.1`) | Imported package catalog and build tooling. |
| Commons | `116d26815eb81e35b3c161e1799e5ee12805fc0a` | Existing HTTP/ServiceDefaults source fallbacks; unchanged. |
| EventStore | `9542d3c9f48bf9ce1c57f2ef68904703eaba56cc` (`v3.117.1-5-g9542d3c9`) | Diagnostic/orchestration source; Parties runtime consumers restore package `3.117.1`; source identity remains separately recorded. |
| FrontComposer | `0e114214007c22f5cdbac21a6853cff4208340ee` (`v4.6.0-7-g0e114214`) | Current diagnostic source selection; package `4.6.0` is selected by the unchanged Builds catalog. |
| Memories | `3e18d0dcdceb387eff89862c382637da89ad7e47` (`v2.28.1-36-g3e18d0dc`) | Current diagnostic source selection; package `2.27.1` remains selected. |
| PolymorphicSerializations | `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` | Existing solution source dependency; unchanged. |
| Tenants | `fcdcb4205a3f6e46f736cdd3e6f2b20ca2f241df` (`v5.7.0-180-gfcdcb420`) | Current diagnostic/orchestration source selection; package `5.7.0` remains selected. |

Selection does not revalidate historical parity receipts or authorize deletion of
migration rollback paths. The dated 2026-10-05 reconciliation and its approvals
remain below. Dated Release/test evidence remains recorded separately in
`tests/test-summary.md`; no wider platform parity is claimed. The G5 structural
and rollback checks run against the current source selection while its missing
provider/enrollment/closure gates remain closed. The historical G5 gitlink and
release-tag receipt is verified at Parties
`75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6` and EventStore
`865cd9e49273dffbb1cdae85efeaf1aac322e09e` (`v3.113.0`); the live release
selection is checked separately.

The historical FrontComposer `2cc8dd3a` component directly contained
`aria-live="polite"` and a `private string AriaLive` member. At the current
`0e114214` selection, both projection-connection and lifecycle components delegate
announcements to `FcSurfaceStatus`, which contains the polite status region.
The marked matrix's reproducible structural commands now check that delegation
and the shared region. This is source inspection, not refreshed accessibility
or UI behavior parity; earlier runtime receipts remain at their original pin.

The selected EventStore source contains
`HealthCheckBuilderExtensions.AddEventStoreDaprHealthChecks` and the audience-aware
`WithEventStoreJwtAuthentication` helper with
`HexalithEventStoreJwtAuthenticationOptions.PrimaryAudience` / `ValidAudiences`.
The marked rows below now cite those delivered symbols. Degraded-response behavior,
DAPR option/tag parity, granular client registration, consumer authentication and
publish proof, integrated topology, and exercised rollback remain required;
neither row is promoted by this source observation. Earlier dated missing-surface
and owner-delivery receipts below retain their historical scope.

### Story 8.10 final retained-identity reconciliation — 2026-10-05

This dated ledger records the graph and exact identity set approved on 2026-10-05.
The current release selection is recorded separately above.
Historical provenance: the initial receipt was authored 2026-08-18, amended by
2026-09-05/06 approvals, the 2026-09-07/13 catalog/gitlink refreshes, the Round 6
selection, and the 2026-10-04 three-pin and working-tree five-pin approvals. Package
and source identities remain separate; neither proves the other. On 2026-10-05 the
Administrator / jpiquot made four decisions (spec 8.10 "Final identity, approval, and
EventStore 3.113.0 reconciliation — 2026-10-05"; I20 rows below): (1) approve exactly
Builds `360a2b9c4e96809365a7de785be9a68152d5ac28` and Tenants
`72b8e4f508176b69826549e87b7b2a286f607fd1`; (2) update EventStore to `3.113.0` as both
package and source, with the root checkout at tag `v3.113.0` =
`865cd9e49273dffbb1cdae85efeaf1aac322e09e`; (3) approve I16 identity re-validation for
the final set, under which a receipt is `Pass` only when it was rerun at the stamped
identity; (4) accept the missing I13 runtime proof as the named DW-111 deferral owned by
`8.9-frontcomposer-ui-consolidation`. Final set: AI.Tools `3f194e17`, Builds `360a2b9c`,
Commons `116d2681`, EventStore `865cd9e4` (`v3.113.0`), FrontComposer `2cc8dd3a`,
Memories `5b43fe2f`, PolymorphicSerializations `98de6e01`, Tenants `72b8e4f5`.
Every root gitlink in the final set is committed at Parties HEAD
`75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6` (FrontComposer and Memories in `2f6157d3`,
Builds and Tenants in `2bdb303b`, EventStore in `75b4fa1f`), so the committed-tree
guard is green for all eight. Never approved (dated history):
EventStore `547c938d52e4fd31b47783b9dd032528a5100549` (committed in `2f6157d3`) and
`0dc44e46ccb7f56c3182b855c21f337173a1307f` (committed in `2bdb303b`), and Tenants
`b55f96d89b839fd443ac589835880102add46d64` (committed in `2f6157d3`). Superseded and
never committed: the 2026-10-04 approvals of EventStore `2242ad55` and Tenants
`04e655cf`. Claims not rerun at the final set remain **unvalidated**; the canonical
`tests/test-summary.md` receipt table records which lanes were rerun.

| Surface | Selected mode | Current consumer evidence | Immutable identity | Disposition and rollback |
| --- | --- | --- | --- | --- |
| EventStore domain-service host and DataProtection/query SDK | Package (default Release graph) | `Directory.Build.props` defaults `UseHexalithProjectReferences=false`; `Hexalith.Parties`, `Contracts`, `Client`, `Projections`, `Security`, and test projects select EventStore `PackageReference` branches when `HexalithEventStoreFromSource != true`; Parties pins `HexalithEventStoreVersion=3.113.0` before importing the shared catalog, which itself still defaults to `3.112.0` at Builds `360a2b9c`, so the Parties pin governs. | Released package `3.113.0`, selected by Administrator / jpiquot on 2026-10-05 (decision 2); supersedes package `3.112.0` (2026-10-04) and `3.110.0` (Round 6). Package and source are the same release (`v3.113.0`). | Selected in the graph; compatibility parity counts only for receipts rerun at this identity (I16 re-validation approved 2026-10-05; see the canonical `tests/test-summary.md` table). If package restore or compatibility regresses, set the explicitly supported source-mode properties for diagnostic rollback and use the separately recorded source identity below; do not relabel the source build as package proof. |
| EventStore domain-service host and DataProtection/query SDK | Source (explicit project-reference graph) | `HexalithEventStoreFromSource` is enabled only when `UseHexalithProjectReferences=true`; the approved current root identity contains the SDK host, query, projection, rebuild, DataProtection, and cursor surfaces consumed by Parties. | Approved current root identity `865cd9e49273dffbb1cdae85efeaf1aac322e09e` (`v3.113.0`, exact release tag), approved by Administrator / jpiquot on 2026-10-05 (decision 2: "update eventstore to version 3.113.0" covers package and source). The clean root checkout matches this identity, one fast-forward commit past `0dc44e46`, and the committed root gitlink matches it: Parties `75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6` (`build(deps): select eventstore 3.113.0 and final pins`) committed it. Dated history: Parties `47e2da3244fd7d6d14e39bc30513b23504e9bc27` recorded the never-approved `0dc44e46ccb7f56c3182b855c21f337173a1307f` before that commit. Never approved (dated history): `547c938d52e4fd31b47783b9dd032528a5100549` (committed in `2f6157d330139e38d031c9a3d1925bca5c317ff7`) and `0dc44e46ccb7f56c3182b855c21f337173a1307f` (committed in `2bdb303b31739b7631036fc15ac21a012724ddb8`). Superseded: working-tree approval `2242ad55a1b678828df8aa093fd92399c29af5bf` (`v3.112.0-2-g2242ad55`, 2026-10-04, never committed), three-pin approval `cbbe41501ba722731bf36b2c343efdef4ac714fb` (`v3.111.0-10-gcbbe4150`, 2026-10-04, committed and checked out at Parties `c782b68c5cf56a19e6a2a237f5f44e3043d5e461`), and Round 6 selection `b046425503c694d5857e8bba890351840fa52c26` (`v3.111.0-2-gb0464255`). | Retained diagnostic/source path. Source consumption parity counts only for receipts rerun at this identity. Roll back a source-only regression by returning to the default package graph at `3.113.0`; any later source adoption must refresh both gitlink and checkout proof. |
| Commons HTTP helpers | Source | `HexalithCommonsHttpFromSource` selects `references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http` when the root checkout exists; `Hexalith.Parties`, `Hexalith.Parties.Client`, and `Hexalith.Parties.Security` consume that branch. | Approved Round 6 root identity `116d26815eb81e35b3c161e1799e5ee12805fc0a` (`v2.30.1-15-g116d268`), unchanged in the 2026-10-05 final set. Committed root gitlink and clean checkout match this approved identity at Parties `c782b68c5cf56a19e6a2a237f5f44e3043d5e461` and still match at Parties `75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`. | Selected from source; behavior parity counts only for receipts rerun at this identity. The catalog's `2.30.1` package is a fallback, not current consumption proof. Roll back a source regression by explicitly selecting the package branch, restoring, and rerunning client/security/package compatibility before deleting any local HTTP behavior. |
| Builds central catalog | Source import | `Directory.Packages.props` imports `references/Hexalith.Builds/Props/Directory.Packages.props`; the effective graph selects EventStore `3.113.0` through the Parties pre-import pin, while the approved catalog at `360a2b9c` itself still selects EventStore `3.112.0`; the imported catalog selects Commons `2.30.1`, Memories `2.27.1`, Tenants `5.7.0`, FrontComposer `4.5.0`, and Parties `1.1.1`. Parties does not import `Hexalith.Build.props` or `Hexalith.Package.props`. | Approved current root identity `360a2b9c4e96809365a7de785be9a68152d5ac28` (`v4.29.1-17-g360a2b9`), approved by Administrator / jpiquot on 2026-10-05 (decision 1). The committed root gitlink (Parties `2bdb303b31739b7631036fc15ac21a012724ddb8`) and clean checkout match this identity at Parties `75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`. Superseded: working-tree approval `145ae921d9032f110b7371614939559e0aee202a` (`v4.29.1-16-g145ae92`, 2026-10-04, committed in `2f6157d330139e38d031c9a3d1925bca5c317ff7`) and Round 6 identity `688eec9a4333245cc0ff7772115c769094471863` (`v4.29.1-14-g688eec9`). | Selected for the catalog only; build/package receipts count only when rerun at this identity. Shared props/targets adoption remains under `8.8-runtime-boundary-cleanup`; retain Parties-local build props, targets, scripts, and probes. Roll back catalog regressions by restoring the prior approved Builds gitlink `145ae921d9032f110b7371614939559e0aee202a` and rerunning warning, restore, Release build, test, pack, and consumer gates. |
| FrontComposer shell (G4 work package F only) | Source (shell slice) and package `4.5.0` (bUnit/CI/container graph) | `src/Hexalith.Parties.UI/Components/Layout/MainLayout.razor` renders `FrontComposerShell` and consumes `#fc-main-content`, `#fc-nav`, ContentLabel, and both `.fc-skip-link` anchors; `tests/Hexalith.Parties.UI.Tests/MainLayoutAccessibilityTests.cs` asserts that contract against the package graph, while `tests/e2e/playwright.config.ts` builds the source graph. | Approved current root identity `2cc8dd3a3ac76c03f5ea6f6f92e65829306db470` (`v4.5.0-121-g2cc8dd3a`), approved by Administrator / jpiquot on 2026-10-04 (working-tree decision "approve working tree") and unchanged in the 2026-10-05 final set. The committed root gitlink (Parties `2f6157d330139e38d031c9a3d1925bca5c317ff7`) and clean checkout match this identity at Parties `75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`. Superseded: three-pin approval `374bb83392d8ab4e8a8397cfd312d09948fb0b9d` (`v4.5.0-117-g374bb833`, 2026-10-04) and Round 6 selection `bf40099f81fcaeac324b7b4377513ac7d49cead4` (`v4.5.0-113-gbf40099f`); never approved: intermediate committed pointer `37c8c6d295032968edba347940c41614ce8f2573` (Parties `7c720c7f9879ecb469993b1d2dff2bdb72b96e1b`). Catalog package `HexalithFrontComposerVersion=4.5.0` remains the package-mode identity. | Delivered slice recorded by `sprint-change-proposal-2026-08-19-story-8-10-frontcomposer-shell-slice-backfill.md`. The source and package identities differ by 121 commits and are recorded separately. Commit `47e2da32` gates the source-only `FrontComposerRouteOptions` wiring behind `HFC_ROUTE_OPTIONS` (source mode only), so the package-mode UI builds against Shell `4.5.0`. Since 2026-10-05 (DW-141) the same `HFC_ROUTE_OPTIONS` source-mode gate covers `PartiesOverview`: its `FcPageTabs` `ModuleRoute`/`DefaultTabId` parameters, which Shell `4.5.0` lacks, are passed only in source mode, and package-mode wiring stays the DW-139 follow-up. The accessibility receipt at this identity is the 2026-10-05 row in the canonical `tests/test-summary.md` table. I13 parity is not discharged: the DW-111 runtime content-control focus, forced-colors, and reduced-motion proof is an accepted deferral (2026-10-05, decision 4) owned by `8.9-frontcomposer-ui-consolidation`. Roll back by restoring the Parties-owned skip links and landmarks from the parent of `2b63ab9`. The remaining G4 work packages A-E stay deferred under `8.9-frontcomposer-ui-consolidation`. |
| Memories submodule | Source (optional rich search) | Not consumed by a Story 8.10 source migration; LocalFuzzyPartySearchProvider remains the default. Optional rich search is gated behind Parties:MemoriesSearch. Identity recorded for completeness only. | Approved current root identity `5b43fe2f8a0f04dc021921a077dff1a573c2ce5e` (`v2.28.0`), approved by Administrator / jpiquot on 2026-10-04 (working-tree decision "approve working tree") and unchanged in the 2026-10-05 final set. The committed root gitlink (Parties `2f6157d330139e38d031c9a3d1925bca5c317ff7`) and clean checkout match this identity at Parties `75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`. Superseded: Round 6 identity `3d72927f4dac66af4968cc6726c4e96df292f2e4` (`v2.27.1-23-g3d72927f`); never approved: intermediate committed pointer `47027d35a1f4a2c6986bec85b5cae601ce1b201e` (Parties `7c720c7f9879ecb469993b1d2dff2bdb72b96e1b`). Catalog package `HexalithMemoriesVersion=2.27.1` remains the package-mode identity; the source tag `v2.28.0` is not the selected package. | Recorded for identity completeness only, not consumption. Rich search stays gated behind `Parties:MemoriesSearch`. |
| Tenants AppHost topology | Source (diagnostic) and package `5.7.0` (default graph) | AppHost and tenant-event subscription consume Hexalith.Tenants; package 5.7.0 is the default graph, explicit source diagnostics select the approved checkout. docs/architecture.md distinguishes source and package identities. No Story 8.10 source migration. | Approved current root identity `72b8e4f508176b69826549e87b7b2a286f607fd1` (`v5.7.0-143-g72b8e4f5`), approved by Administrator / jpiquot on 2026-10-05 (decision 1). The committed root gitlink (Parties `2bdb303b31739b7631036fc15ac21a012724ddb8`) and clean checkout match this identity at Parties `75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`. Never approved (dated history): `b55f96d89b839fd443ac589835880102add46d64` (committed in `2f6157d330139e38d031c9a3d1925bca5c317ff7`) and the intermediate pointer `c05fe3171b2d3f95e0dd0970edfa6c421bed2f19` (Parties `7c720c7f9879ecb469993b1d2dff2bdb72b96e1b`). Superseded: working-tree approval `04e655cf070b17eced9daefb9eaa87a09ec60e81` (`v5.7.0-141-g04e655cf`, 2026-10-04, never committed), three-pin approval `cc348c9d7839ec7aad01649fc1c0e6f4fe672da4` (`v5.7.0-134-gcc348c9d`, 2026-10-04), and Round 6 selection `b63bdbba8613801bb488eaa9116f386e41a261f8` (`v5.7.0-133-gb63bdbba`). Catalog package `HexalithTenantsVersion=5.7.0` remains the package-mode identity. | Recorded for identity completeness. Story 8.8 still owns G7/G9/G12 consumption proof. Source consumption parity counts only for receipts rerun at this identity. Roll back a source-only regression by returning to package `5.7.0`. |
| PolymorphicSerializations submodule | Source (solution build graph) | Builds as part of `Hexalith.Parties.slnx`; carries the StyleCop compatibility fix (SA1000/SA1010/SA1313/SA1316) that cleared the former 21-error Release solution build, plus the later SA1316 tuple-element-casing fix that cleared the 16-error regression measured at `0dca9e9d`. | Approved Round 6 root identity `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` (`v1.19.4`), unchanged in the 2026-10-05 final set. Committed root gitlink and clean checkout match this approved identity at Parties `c782b68c5cf56a19e6a2a237f5f44e3043d5e461` and still match at Parties `75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`. | Retained; the 2026-10-05 Release solution build at the final set, which compiles this submodule, is recorded in the canonical `tests/test-summary.md` table; owner tests were not rerun, and the historical 2026-09-06 Release proof at `8aeed1d27c9a050bc4bec6d89051aa00de306a69` stays dated history. Preserve rollback to `5e01ff3ab7a7393c2252ee0c2fc1247556e7c129` (historical 21-error baseline). |
| AI.Tools instruction baseline | Source (instructions only) | Required Hexalith baseline is read from the root-declared AI.Tools submodule; no runtime package or production consumer. | Approved Round 6 root identity `3f194e17174994d308ec84af9ee2b5aa68674d0d` (`3f194e1`), unchanged in the 2026-10-05 final set. Committed root gitlink and clean checkout match this approved identity at Parties `c782b68c5cf56a19e6a2a237f5f44e3043d5e461` and still match at Parties `75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`. | Recorded for identity completeness; baseline changes require renewed review, not runtime consumption proof. |

The four previously named `available` receipts are therefore reconciled as
follows: the domain-service host and EventStore DataProtection rows use the
EventStore package/source identities above; Commons HTTP uses the selected
source identity; Builds uses the imported catalog identity. Unconsumed
historical pins and the Commons package fallback remain audit or rollback
information only. The open G1/G2, G4-G9, and G11 surfaces remain governed by
the accepted Story 8.7-8.9 deferrals; none is promoted to `available` here.
The older marked matrix below is a historical decision log: references there
to Story 8.6 being `in-progress` or to EventStore `v3.91.0`/`1d6e9321` are
superseded by Story 8.6=`done`, the current receipts above, and the accepted
residual-debt disposition below.

| Accepted deferral | Owner | Exit proof | Rollback | Evidence |
| --- | --- | --- | --- | --- |
| `8.6-residual-review-debt` | Parties development/test owners plus EventStore SDK owners for producer/runtime proof | Resolve or supersede every unchecked Story 8.6 `[Review][Defer]`; exercise authenticated SDK handler discovery and runtime deny-default EventStore-only ACL enforcement before deleting the related seams. | Retain source/package selection, the Parties AppHost/gateway topology, the exact ACL, and remaining compatibility seams. | `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`; `_bmad-output/implementation-artifacts/deferred-work.md` |
| `8.7-data-protection-extraction` | EventStore payload-protection plus Parties development/test owners | Deliver G5 and pass the complete compatibility/switch-back packet. | Retain Parties security, adapter, DI selection, and compatibility harness. | `_bmad-output/implementation-artifacts/deferred-work.md` |
| `8.8-runtime-boundary-cleanup` | EventStore, Commons, FrontComposer, Builds, platform-AppHost, and Parties owners | Prove every G1/G2/G6-G9/G11 adoption slice at exact identities with integrated rollback. | Retain every Parties-local runtime/client/MCP/AppHost/build path. | `_bmad-output/implementation-artifacts/deferred-work.md` |
| `8.9-frontcomposer-ui-consolidation` | FrontComposer, UX, Parties development/test owners | Deliver G4 and pass producer plus Parties bUnit/Playwright parity. | Retain all Parties UI primitives and Fluent 2 behavior until slice parity passes. | `_bmad-output/implementation-artifacts/deferred-work.md` |
| `external-runtime-deployment` | External platform operations/deployment owners | Prove environment-specific orchestration and rollback using immutable images. | Redeploy the prior immutable image/configuration set externally. | `docs/deployment-guide.md`; `_bmad-output/implementation-artifacts/deferred-work.md` |

On 2026-10-05 the Administrator / jpiquot directed "update eventstore to version
3.113.0" for both package and source (decision 2). That selection supersedes the
package `3.112.0` requested on 2026-10-04, which itself superseded `3.110.0`; the
original approval/history rows below retain their dated package identities.
`Directory.Packages.props` pins the release before the shared import, so the selection
persists independently of the Builds catalog default (`3.112.0` at `360a2b9c`). Source
rows select the 2026-10-05 final set (I20 rows below). Identity selection grants no
parity by itself: under the approved I16 re-validation a receipt counts only when it was
rerun at the stamped identity.

<!-- platform-api-prerequisite-matrix:start -->
| Surface | Owner | Status | Evidence paths | Proof required before migration | Dependent Epic 8 stories | Story 8.3 decision / rollback | Validation command or inspection used |
| --- | --- | --- | --- | --- | --- | --- | --- |
| EventStore domain-service host | Hexalith.EventStore | available | references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs; references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs | Proof required: Story 8.5 must validate release or submodule-pin availability, Parties gateway routing, DAPR ACLs, command validation rejections, crypto redaction hooks, and rollback using the SDK host shape. Consumption availability validated by Story 8.5 on 2026-07-07 at the root-declared `references/Hexalith.EventStore` gitlink `9f8b54dc161a4d5a9b2e6b1deacf331d1b80f1e0`: SDK `/process` parity, SDK `/replay-state`, domain casing, gateway routing, domain processor validation, projection/rebuild compatibility, and DAPR ACL checks passed. This historical consumption identity remains authoritative for 8.5; Story 8.10 must reconcile the final EventStore identity actually retained. | 8.4, 8.5, 8.10 | No Parties source migration starts in Story 8.3. Keep rollback path: revert Story 8.5 host/processor changes to the pre-cutover manual `/process` map and production `IDomainServiceInvoker` registration if SDK routing regressions appear; degraded-response/DAPR-health and Aspire/AppHost rows remain local/unresolved. | `git ls-tree bff30c1182e95af1a922d74777a6611e788a53ee references/Hexalith.EventStore`; `git -C references/Hexalith.EventStore cat-file -e 9f8b54dc161a4d5a9b2e6b1deacf331d1b80f1e0:src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs`; `rg -n -F 'AddEventStoreDomainService' references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs`; `rg -n -F 'UseEventStoreDomainService' references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs`; `rg -n -F 'AddEventStoreDomainService' src/Hexalith.Parties/Program.cs`; `rg -n -F 'UseEventStoreDomainService' src/Hexalith.Parties/Program.cs`; focused build/process/gateway/domain/fitness/ACL validation recorded in `_bmad-output/implementation-artifacts/tests/test-summary.md`. |
| EventStore projection/query SDK | Hexalith.EventStore | available | references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/IDomainProjectionHandler.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/IAsyncDomainProjectionHandler.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/IDomainQueryHandler.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/IReadModelBatchStore.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/IReadModelConditionalEraser.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/ReadModelWritePolicy.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Queries/IQueryCursorCodec.cs | Proof required: Story 8.6 must validate release or submodule-pin availability and the distributed consumer boundary before deleting rollback code. The formally owner-approved identity `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` (EventStore Story 1.20, `final_decision: available`, `authorize_consumer_migration: true`) failed the required source-mode projection build with CS0246: it does not expose `IAsyncDomainSharedProjectionRebuildHandler`, `DomainSharedProjectionRebuildIdentity`, or `DomainSharedProjectionRebuildCandidate`, which the Parties tenant-shared index handler requires. Story 8.6 completed its projection/query SDK migration on 2026-08-01 against EventStore v3.89.0 (`c590590bc581a3f72ef6e67148eda988ba4b8fe6`), which does expose the required surfaces and closes the routed Fable-platform gaps G3 (read-model erasure hooks), G6 (freshness mapping), and G10 (index batching), under the Administrator's explicit direct authorization to use the latest EventStore release after the `fa2d1c99` compile failure. This authorization is recorded in `sprint-change-proposal-2026-08-02-story-8-6-eventstore-identity-authorization-backfill.md`, approved 2026-08-02. All 18 previously-governed rollback artifacts and all 10 `catch (NotImplementedException)` fallbacks are removed; full regression passed 452/452 at `c590590b`. The current root gitlink has since moved to `4bcf2484a09eb26490cb2d32ceb6df8949f90cc6` (Story 8.7's independent G5 revalidation) — Story 8.6's regression evidence is not yet re-confirmed at that later pin; re-run the focused projection/query suite before treating `4bcf2484` as validated for Story 8.6's purposes. Story 8.6 selected EventStore `v3.91.0` (`1d6e9321acfc416768c1c78e9facf573c9c41f71`) under the Administrator's recorded direct authorization; `sprint-change-proposal-2026-08-04.md` (approved) formalizes that identity. The matching Builds catalog identity is `824d7ef100455423aabbcd399c8364074000b2e0` and selects `HexalithEventStoreVersion=3.91.0`. A current package-mode restore and Release projection build completed with zero warnings and zero errors on 2026-08-04. | 8.6, 8.10 | No Parties source migration starts in Story 8.3. Keep rollback path was this row's original gate decision; Story 8.6 has since satisfied this row's own consumer-evidence gates (parity, GDPR reads, rebuild-vs-replay) and deleted the rollback path under Administrator authorization at `c590590b`, recorded in the linked SCP. That identity-and-parity evidence is narrow and does not mean Story 8.6 overall is complete: `sprint-status.yaml` and `8-6-projection-and-query-sdk-migration.md` record Story 8.6 as `in-progress` with material open review findings (diagnostic-logging, corrupt-vs-redacted observability, rebuild/live-write concurrency, remaining parity-test gaps) unrelated to this row. Open follow-up (not blocking): re-run Story 8.6's focused projection/query regression at the current EventStore pin before relying on it as re-validated evidence. | `git ls-tree 03ab938c637aa15f7a0af402afc8664dfc54d1a4 references/Hexalith.EventStore`; `rg -n -F 'IDomainProjectionHandler' references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/IDomainProjectionHandler.cs`; `rg -n -F 'IAsyncDomainProjectionHandler' references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/IAsyncDomainProjectionHandler.cs`; `rg -n -F 'IDomainQueryHandler' references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/IDomainQueryHandler.cs`; `rg -n -F 'IReadModelStore' references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs`; `rg -n -F 'IReadModelBatchStore' references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/IReadModelBatchStore.cs`; `rg -n -F 'IReadModelConditionalEraser' references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/IReadModelConditionalEraser.cs`; `rg -n -F 'IReadModelFreshness' references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/IReadModelFreshness.cs`; `rg -n -F 'IQueryCursorCodec' references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Queries/IQueryCursorCodec.cs`; `rg -n -F 'project/v2' references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs`; `rg -n -F 'MethodName = "query"' references/Hexalith.EventStore/src/Hexalith.EventStore/Queries/DaprDomainQueryInvoker.cs`; `rg -n -F 'name: /process' src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml`; `git ls-tree <2026-08-04-HEAD> references/Hexalith.EventStore` -> `160000 commit 1d6e9321acfc416768c1c78e9facf573c9c41f71 references/Hexalith.EventStore`; `git -C references/Hexalith.EventStore describe --tags --exact-match <2026-08-04-HEAD>` -> `v3.91.0`; `git ls-tree <2026-08-04-HEAD> references/Hexalith.Builds` -> `160000 commit 824d7ef100455423aabbcd399c8364074000b2e0 references/Hexalith.Builds`; `rg -n -F 'HexalithEventStoreVersion' references/Hexalith.Builds/Props/Directory.Packages.props`; `dotnet restore src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj -p:UseHexalithProjectReferences=false -p:HexalithEventStoreFromSource=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0`; `dotnet build src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj --configuration Release --no-restore -nr:false -m:1 -p:UseHexalithProjectReferences=false -p:HexalithEventStoreFromSource=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -> 0 warnings, 0 errors. |
| EventStore DataProtection | Hexalith.EventStore | available | references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDataProtectionServiceCollectionExtensions.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/DaprXmlRepository.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Registration/QueryCursorCodecServiceCollectionExtensions.cs | Proof required: Story 8.6 must validate release or submodule-pin availability, cursor purpose/scope compatibility, DAPR key-ring persistence, and rollback. The formally owner-approved identity `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` (`v3.82.0-24-gfa2d1c99`) verified the three cited surfaces but failed the required tenant-shared rebuild build (see the projection/query SDK row above). Consumption availability for these three surfaces was revalidated at exact approved source `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` on 2026-08-01. Story 8.6 adopted the cursor and DataProtection surfaces at EventStore v3.89.0 (`c590590bc581a3f72ef6e67148eda988ba4b8fe6`) on 2026-08-01, under the same Administrator authorization recorded in `sprint-change-proposal-2026-08-02-story-8-6-eventstore-identity-authorization-backfill.md`. Cursor purpose/scope compatibility and persisted DAPR key-ring behavior passed as part of Story 8.6's 452/452 regression at `c590590b`. The current root gitlink has since moved to `4bcf2484a09eb26490cb2d32ceb6df8949f90cc6` (Story 8.7's independent G5 revalidation); the same current-pin re-validation caveat from the row above applies here. Story 8.6 selected EventStore `v3.91.0` (`1d6e9321acfc416768c1c78e9facf573c9c41f71`) under the Administrator's recorded direct authorization; `sprint-change-proposal-2026-08-04.md` (approved) formalizes that identity. The matching Builds catalog identity is `824d7ef100455423aabbcd399c8364074000b2e0` and selects `HexalithEventStoreVersion=3.91.0`. A current package-mode restore and Release projection build completed with zero warnings and zero errors on 2026-08-04. | 8.6, 8.10 | No Parties source migration starts in Story 8.3. Keep rollback path was this row's original gate decision; Story 8.6 adopted these surfaces and deleted the rollback path under the same Administrator authorization at `c590590b`. That identity-and-parity evidence is narrow and does not mean Story 8.6 overall is complete: `sprint-status.yaml` and `8-6-projection-and-query-sdk-migration.md` record Story 8.6 as `in-progress` with material open review findings unrelated to this row. Same open follow-up as the row above: re-run at the current pin before relying on it as re-validated. | `git ls-tree 03ab938c637aa15f7a0af402afc8664dfc54d1a4 references/Hexalith.EventStore`; `rg -n -F 'AddEventStoreDataProtection' references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDataProtectionServiceCollectionExtensions.cs`; `rg -n -F 'DaprXmlRepository' references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/DaprXmlRepository.cs`; `rg -n -F 'AddEventStoreQueryCursorCodec' references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Registration/QueryCursorCodecServiceCollectionExtensions.cs`; `git ls-tree <2026-08-04-HEAD> references/Hexalith.EventStore` -> `160000 commit 1d6e9321acfc416768c1c78e9facf573c9c41f71 references/Hexalith.EventStore`; `git -C references/Hexalith.EventStore describe --tags --exact-match <2026-08-04-HEAD>` -> `v3.91.0`; `git ls-tree <2026-08-04-HEAD> references/Hexalith.Builds` -> `160000 commit 824d7ef100455423aabbcd399c8364074000b2e0 references/Hexalith.Builds`; `rg -n -F 'HexalithEventStoreVersion' references/Hexalith.Builds/Props/Directory.Packages.props`. |
| EventStore degraded response and DAPR health checks | Hexalith.EventStore | needs-additive-api | references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs; references/Hexalith.EventStore/src/Hexalith.EventStore/HealthChecks/HealthCheckBuilderExtensions.cs; src/Hexalith.Parties/Middleware/DegradedResponseMiddleware.cs; src/Hexalith.Parties/HealthChecks/PartiesHealthCheckExtensions.cs; src/Hexalith.Parties/HealthChecks/DaprStateStoreHealthCheck.cs; src/Hexalith.Parties/HealthChecks/DaprPubSubHealthCheck.cs | Proof required: G1 shared degraded-response middleware remains unproved. G2 `AddEventStoreDaprHealthChecks` is delivered in the selected source; owner-approved Parties option/status-header/health-tag parity, exact consumer availability, and exercised rollback are still required before adoption. | 8.5, 8.8, 8.10 | No Parties source migration starts in Story 8.3. Keep local rollback path: Parties degraded-response middleware and DAPR health checks stay until owner APIs prove parity. | `rg -n -F 'AddEventStoreDaprHealthChecks' references/Hexalith.EventStore/src/Hexalith.EventStore/HealthChecks/HealthCheckBuilderExtensions.cs`; `rg -n -F 'UseEventStoreDomainService' references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs`; `rg -n -F 'DegradedResponseMiddleware' src/Hexalith.Parties/Middleware/DegradedResponseMiddleware.cs`; `rg -n -F 'DaprStateStoreHealthCheck' src/Hexalith.Parties/HealthChecks/DaprStateStoreHealthCheck.cs`; `rg -n -F 'DaprPubSubHealthCheck' src/Hexalith.Parties/HealthChecks/DaprPubSubHealthCheck.cs`; delivered-symbol note: EventStore exposes `AddEventStoreDaprHealthChecks`; remaining proof gap: shared degraded-response behavior and owner-approved Parties health option/tag parity plus consumer/rollback evidence for G1/G2 remain unproved. |
| Payload protection engine package | Hexalith.EventStore | needs-additive-api | references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Events/NoOpEventPayloadProtectionService.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs; references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md; references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml; references/Hexalith.EventStore/_bmad-output/planning-artifacts/story-id-migration-2026-08-01.md; _bmad-output/implementation-artifacts/sprint-status.yaml; src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/IPersonalDataPolicy.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/IErasureStateProvider.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj | Proof required: G5 additive shared payload-protection engine package, with PersonalDataAttribute, key storage/wrapping/rotation, audit/retry/circuit breaker, typed unreadable outcomes, production backend, AAD binding in pdenc-v2, json+pdenc-v1 reads, stable formats/state keys/actor names/metrics, and golden compatibility harnesses. IPersonalDataPolicy and IErasureStateProvider are present read-only Parties policy hooks pending I19a/I20 classification; their presence does not authorize engine adoption. Historical 2026-08-04 receipt: EventStore 1d6e9321acfc416768c1c78e9facf573c9c41f71 / v3.91.0 and Builds 824d7ef100455423aabbcd399c8364074000b2e0. Historical 2026-09-07 receipt: EventStore d45206f7cbd80a112519c1d4687d7279a745f0c5 / package 3.103.0 and Builds 7b0b1837ce368e314b5e11b011b73603637a17e1; these are dated inspection identities, not the retained set above. Historical 2026-10-03 inspection superseded absence claims: public policy/erasure contracts and the non-packable internal pdenc-v2 core exist; owner 8.2 is done, 8.3 in-progress, and 8.4-8.11 remain backlog. Missing: AzureKeyVault project, catalog/release enrollment, approved runtime/provider registration, dual-provider parity, post-v2 rollback, and Story 8.11 approval-closure packet. Story 8.11 alone may record G5 available and unblock Parties Story 8.7. G5 remains needs-additive-api, Parties 8.7 remains blocked, retention action stays open, and no crypto/key-management deletion is authorized. Historical 2026-10-05 inspection below confirmed the same closed gate at EventStore v3.113.0 / package 3.113.0 and observed Builds 90f3836dd7482db35c2c187a50999b99215919b0. I2/I19a policy-hook classification approval remains pending. Identity selection is not G5 approval. 2026-11-03 re-observation at owner-approved EventStore a9205f511b4f96b5daf94d65369d6dee63e0c623 (v4.0.4-11-ga9205f51): owner 8.1-8.5 are done, 8.6 is in-progress and 8.7-8.11 remain backlog; the AzureKeyVault adapter project now exists but is non-packable and is not enrolled in the selected Builds catalog or the owner release inventory, and the Story 8.11 closure packet is absent. G5 remains needs-additive-api, Parties 8.7 remains blocked, and no crypto/key-management deletion is authorized. | 8.7, 8.10 | No Parties source migration starts in Story 8.3. Keep local rollback path: `Hexalith.Parties.Security` and Parties GDPR policy stay intact until Story 8.7 proves compatibility and rollback. | Historical gitlink receipt (not current parity): `git ls-tree 75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6 references/Hexalith.EventStore` -> `160000 commit 865cd9e49273dffbb1cdae85efeaf1aac322e09e references/Hexalith.EventStore`; `git ls-tree 75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6 references/Hexalith.Builds` -> `160000 commit 360a2b9c4e96809365a7de785be9a68152d5ac28 references/Hexalith.Builds`; `git -C references/Hexalith.EventStore describe --tags --exact-match --match v3.113.0 865cd9e49273dffbb1cdae85efeaf1aac322e09e` -> `v3.113.0`. Current structural inspection: `rg -n -F 'IEventPayloadProtectionService' references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs`; `rg -n -F 'NoOpEventPayloadProtectionService' references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Events/NoOpEventPayloadProtectionService.cs`; `rg -n -F 'TryAddSingleton<IEventPayloadProtectionService, NoOpEventPayloadProtectionService>' references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`; `rg -n -F 'IPersonalDataPolicy' references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/IPersonalDataPolicy.cs`; `rg -n -F 'IErasureStateProvider' references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/IErasureStateProvider.cs`; `rg -n -F 'status: approved-authorized' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`; `rg -n -F 'decision: adopted-amendment' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`; `rg -n -F 'story_8_2_authorized: true' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`; `rg -n -F 'Hexalith.EventStore.PayloadProtection' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`; `rg -n -F 'Hexalith.EventStore.PayloadProtection.AzureKeyVault' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`; `rg -n -F 'pdenc-v2' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`; `rg -n -F 'json+pdenc-v1' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`; `rg -n -F 'IPersonalDataPolicy' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`; `rg -n -F 'IErasureStateProvider' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`; `rg -n -F '8-1-shared-payload-protection-security-spec-and-adr: done' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml`; `rg -n -F '8-2-payload-protection-contracts-and-golden-vectors: done' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml`; `rg -n -F '8-3-pdenc-v2-core-cryptographic-engine: done' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml`; `rg -n -F '8-4-compatibility-readers-and-mixed-history-routing: done' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml`; `rg -n -F '8-5-policy-and-key-lifecycle-mechanics: done' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml`; `rg -n -F '8-6-azure-key-vault-production-adapter-conformance: in-progress' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml`; `rg -n -F '8-7-server-persistence-and-snapshot-integration: backlog' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml`; `rg -n -F '8-8-package-and-release-integration: backlog' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml`; `rg -n -F '8-9-parties-dual-provider-parity: backlog' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml`; `rg -n -F '8-10-post-v2-write-rollback-rehearsal: backlog' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml`; `rg -n -F '8-11-g5-evidence-and-approval-closure: backlog' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml`; `rg -n -F 'only this story can record' references/Hexalith.EventStore/_bmad-output/planning-artifacts/story-id-migration-2026-08-01.md`; `test ! -f references/Hexalith.EventStore/_bmad-output/implementation-artifacts/8-11-g5-evidence-and-approval-closure.md`; `rg -n -F '<IsPackable>false</IsPackable>' references/Hexalith.EventStore/src/Hexalith.EventStore.PayloadProtection.AzureKeyVault/Hexalith.EventStore.PayloadProtection.AzureKeyVault.csproj`; `rg -n -F 'Hexalith.EventStore.PayloadProtection' references/Hexalith.Builds/Props/Directory.Packages.props` (expected no matches); `rg -n -F 'Hexalith.EventStore.PayloadProtection' references/Hexalith.EventStore/tools/release-packages.json` (expected no matches); `rg -n -F 'PartyPayloadProtectionService' src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs`; `rg -n -F 'EventStorePartyPayloadProtectionAdapter' src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs`; All 18 MOVE files, 5 KEEP files, and CryptoKeyManagementCompatibilityHarnessTests remain; historical 19/19 compatibility receipt is unvalidated at the refreshed graph. |
| EventStore client envelopes/freshness/error codes | Hexalith.EventStore | needs-additive-api | references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Gateway/IEventStoreGatewayClient.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Commands/SubmitCommandRequest.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Queries/QueryResponseMetadata.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Queries/QueryProblemReasonCodes.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Problems/GatewayProblemDetailsExtensions.cs | Proof required: additive or approved adapter proof for G6 Parties `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly` freshness semantics, warning codes, ProblemDetails reason mapping, and typed command/query client outcome compatibility. | 8.6, 8.8, 8.9, 8.10 | No Parties source migration starts in Story 8.3. Keep local rollback path: Parties client envelopes, paging/freshness models, and bounded outcome mapping stay until Story 8.8 proves compatibility. | `rg -n -F 'IEventStoreGatewayClient' references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Gateway/IEventStoreGatewayClient.cs`; `rg -n -F 'QueryResponseMetadata' references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Queries/QueryResponseMetadata.cs`; `rg -n -F 'QueryProblemReasonCodes' references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Queries/QueryProblemReasonCodes.cs`; `rg -n -F 'GatewayProblemDetailsExtensions' references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Problems/GatewayProblemDetailsExtensions.cs`. |
| Tenant claims transformation | Hexalith.EventStore and Hexalith.Commons | needs-additive-api | references/Hexalith.EventStore/src/Hexalith.EventStore/Authentication/EventStoreClaimsTransformation.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs; references/Hexalith.Commons/src/libraries/Hexalith.Commons.UniqueIds/UniqueIdHelper.cs; references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs; src/Hexalith.Parties.Authentication/PartiesClaimsTransformation.cs | Proof required: G7/G9 ownership was confirmed on 2026-07-16 in `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-g7-g9-tenant-claims-ownership.md`: EventStore.Contracts owns the public `eventstore:tenant` constant and `AggregateIdentity.IsValid(string)`; a lightweight `Hexalith.EventStore.Authentication` package owns the reusable `EventStoreTenantClaimsTransformation`; Commons owns `UniqueIdHelper.IsValidUlid(string)`. Delivery proof remains required: named owner acceptance of the exact APIs, released packages or approved root-submodule pins, package/API inventory, producer-consumer parity, and rollback evidence. | 8.4, 8.8, 8.10 | No Parties source migration starts in Story 8.3. Keep the `Hexalith.Parties.Authentication` rollback path; it remains after Story 8.4 until the approved APIs are delivered or pinned and Parties parity plus rollback pass. Ownership approval alone does not authorize deletion. | Approved G7/G9 decision: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-g7-g9-tenant-claims-ownership.md`; `rg -n -F 'TenantClaimType = "eventstore:tenant"' references/Hexalith.EventStore/src/Hexalith.EventStore/Authentication/EventStoreClaimsTransformation.cs`; `rg -n -F 'AggregateIdentity' references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs`; `rg -n -F 'CrockfordBase32Pattern' references/Hexalith.Commons/src/libraries/Hexalith.Commons.UniqueIds/UniqueIdHelper.cs`; additive `AggregateIdentity.IsValid(string)` and `UniqueIdHelper.IsValidUlid(string)` remain undelivered. |
| Aspire publish helpers | Hexalith.EventStore.Aspire and Hexalith.EventStore and Hexalith.FrontComposer and platform AppHost owners | needs-additive-api | references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreDomainModuleExtensions.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreJwtAuthenticationOptions.cs; references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs; references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.AppHost/Program.cs; src/Hexalith.Parties.AppHost/Program.cs; src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs | Proof required: G8 owner proof requires three accepted work packages: (A) EventStore.Aspire now supplies `WithEventStoreJwtAuthentication(resource, localSecurity, options)` with `HexalithEventStoreJwtAuthenticationOptions.PrimaryAudience` and `ValidAudiences`; owner-approved consumer and publish/external-orchestrator proof still must cover authority, issuer, workload plus EventStore audience relationships, HTTPS metadata, signing-key clearing, and no-secret manifest output; (B) EventStore.Client supplies independently selectable command/query/GDPR transport registration with deterministic handler and repeat-registration behavior that preserves Parties and FrontComposer module-typed clients; (C) FrontComposer.AppHost or an explicitly approved platform AppHost owns the integrated EventStore/Tenants/Parties local topology, while external platform operations retains runtime deployment. Story 8.8 must record named approval, exact release or root-submodule identity, producer tests, Parties consumer topology/client-coexistence evidence, source/package expectations, and tested rollback before deleting local wiring. | 8.5, 8.8, 8.10 | No Parties source migration starts in Story 8.3. Keep rollback path: the current Parties AppHost and its typed-client registrations remain until Packages A-C and consumer parity are accepted; then retire the domain-owned AppHost rather than retain a permanent thin host. | `rg -n -F 'AddEventStoreDomainModule' references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreDomainModuleExtensions.cs`; `rg -n -F 'WithEventStoreJwtAuthentication' references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs`; `rg -n -F 'HexalithEventStoreJwtAuthenticationOptions' references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreJwtAuthenticationOptions.cs`; `rg -n -F 'PrimaryAudience' references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreJwtAuthenticationOptions.cs`; `rg -n -F 'ValidAudiences' references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreJwtAuthenticationOptions.cs`; `rg -n -F 'WithJwtBearerSecurity' references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs`; `rg -n -F 'AddEventStoreGatewayClient' references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs`; `rg -n -F 'AddPartiesClient' src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs`; `rg -n -F 'WithJwtAuthentication' src/Hexalith.Parties.AppHost/Program.cs`; delivered-symbol note: current EventStore.Aspire exposes `WithEventStoreJwtAuthentication` using `HexalithEventStoreJwtAuthenticationOptions`; remaining proof gap: granular module-typed command/query/GDPR registration and deterministic coexistence, owner-approved Parties authentication/publish evidence, integrated topology including MCP/UI/publish targets, and exercised rollback remain unproved; FrontComposer.AppHost already composes EventStore, Tenants, and Parties. Routed by `sprint-change-proposal-2026-07-16-g8-aspire-publish-helper-routing.md`; routing is not delivery and status remains `needs-additive-api`. |
| FrontComposer UI primitives | Hexalith.FrontComposer.Contracts.UI and Hexalith.FrontComposer.Shell | needs-additive-api | references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Forms/FcDestructiveConfirmationDialog.razor; references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/EventStore/FcProjectionConnectionStatus.razor; references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Lifecycle/FcLifecycleWrapper.razor; references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Layout/FrontComposerShell.razor; src/Hexalith.Parties.Picker/Components/PartyPicker.razor; src/Hexalith.Parties.ConsumerPortal/Components/FreshnessStatus.razor; src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor; src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor | Proof required: G4 owner proof requires six accepted work packages: (A) additive generic `FcEntityPicker<T>` with durable-key binding, async search/resolve, stale-response rejection, WAI-ARIA combobox keyboard/focus behavior, and an adopter event adapter; (B) additive UI-normalized per-record freshness indicator without redefining EventStore/G6 wire semantics; (C) a reusable public routine `status`/`polite` versus validation/failure `alert`/`assertive` live-region contract; (D) injectable file/JSON browser download with safe caller-owned filenames, stream interop, object-URL/anchor cleanup, cancellation, and no payload log/persistence; (E) source-compatible typed-name mode on `FcDestructiveConfirmationDialog` with ordinal exact match, disabled confirmation, safe cancel focus, and Escape cancellation; (F) owner-certified parity for the existing shell skip links, adding only a narrow customization seam if Parties evidence proves it necessary. Story 8.9 must record named owner approval, exact Contracts.UI/Shell release or root-gitlink identity, public API/package validation, producer bUnit evidence, Parties bUnit/Playwright parity, and rollback before migration. Routed by `sprint-change-proposal-2026-07-16-g4-g11-frontcomposer-shared-primitives-routing.md`; routing is not delivery. | 8.9, 8.10 | No Parties source migration starts in Story 8.3. Keep rollback path: the Parties picker, per-record freshness/status regions, browser download helpers, typed erasure confirmation, optimistic reconcile, and portal components remain until each Story 8.9 slice proves parity against the approved FrontComposer identity; status changes to `available` only after all G4 work packages and consumer rollback evidence are accepted. **Delivered-slice amendment 2026-08-19** (`sprint-change-proposal-2026-08-19-story-8-10-frontcomposer-shell-slice-backfill.md`): work package F only — shell skip links and `role="main"`/`role="navigation"` landmarks — was adopted 2026-08-18 by Story 8.10 at FrontComposer gitlink `7a337a21d4ba261bf27aeb3feedde47789f0160a` (`v4.1.1-104-g7a337a21`); retained identity amendments were `5cbc5583142a6774ff7813698ad98ec267b336f0` (2026-09-05/06), `f0c3b6fd7dbf0a750170b5ec72d09d17febb8f6a` (2026-09-07), `b0ad2fb69bcf5e7aadd7b388d25415d0fba876d5` (2026-09-13), the Round 6 identity `bf40099f81fcaeac324b7b4377513ac7d49cead4` and three-pin identity `374bb83392d8ab4e8a8397cfd312d09948fb0b9d` (both 2026-10-04, superseded), and the working-tree approved identity `2cc8dd3a3ac76c03f5ea6f6f92e65829306db470` in the reconciliation table above (approved 2026-10-04, committed in `2f6157d330139e38d031c9a3d1925bca5c317ff7`, unchanged in the 2026-10-05 final set), so skip links are removed from the keep-rollback list above. Work packages A-E (`FcEntityPicker<T>`, freshness indicator, live-region contract, file/JSON download, typed-name confirmation mode) remain undelivered and the row status stays `needs-additive-api`. Source-only module-landing routing (`FrontComposerRouteOptions`, `FcModuleLandingPage`) was also adopted by commit `37f1d04bdf8cc0c54cd600036ee4961f61eb409c`; packaged Shell `4.5.0` lacks those types. Commit `47e2da3244fd7d6d14e39bc30513b23504e9bc27` (2026-10-04; Administrator Round 6 D4 option (b)) gates the `FrontComposerRouteOptions` reserved-segment wiring behind `HFC_ROUTE_OPTIONS`, defined only when `HexalithFrontComposerFromSource=true`, and references the Shell assembly through `FrontComposerShell` instead of `FcModuleLandingPage`, so the package-mode UI compiles against Shell `4.5.0` and resolved DW-124's build blocker. Package-mode reserved-segment and module-landing wiring still waits for a FrontComposer release (DW-139); this additional surface does not promote G4, and its parity is unvalidated. Since 2026-10-05 (DW-141) the same `HFC_ROUTE_OPTIONS` source-mode gate covers `PartiesOverview`: its `FcPageTabs` `ModuleRoute`/`DefaultTabId` parameters, which Shell `4.5.0` lacks, are passed only in source mode, and package-mode wiring stays the DW-139 follow-up. The slice's I13 parity is conditional: the app-owned focus-visible and forced-colors regression it introduced must be repaired before the slice counts as I13 evidence. On 2026-10-05 the Administrator / jpiquot accepted the missing runtime content-control focus, forced-colors, and reduced-motion proof as the named DW-111 deferral owned by `8.9-frontcomposer-ui-consolidation` (I20 row below); I13 is not discharged. | `rg -n -F 'FcDestructiveConfirmationDialog' references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Forms/FcDestructiveConfirmationDialog.razor.cs`; `rg -n -F 'FcSurfaceStatus Surface="projection-connection"' references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/EventStore/FcProjectionConnectionStatus.razor`; `rg -n -F 'aria-live="@(_suppressSpeech ? "off" : "polite")"' references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Rendering/FcSurfaceStatus.razor`; `rg -n -F 'FcSurfaceStatus Surface="@AnnouncementSurface"' references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Lifecycle/FcLifecycleWrapper.razor`; `rg -n -F 'fc-skip-link' references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Layout/FrontComposerShell.razor`; `rg -n -F 'FcEntityPicker' references/Hexalith.FrontComposer/src` (expected no matches); `rg -n -F 'FileDownload' references/Hexalith.FrontComposer/src` (expected no matches); `rg -n -F 'JsonDownload' references/Hexalith.FrontComposer/src` (expected no matches); Parties-local picker, freshness, download, and typed-confirmation implementations remain as rollback evidence. |
| Commons HTTP helpers | Hexalith.Commons | available | references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/HttpClientRegistration.cs; references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/BoundedProblemDetailsReader.cs; references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/HttpCorrelation.cs; references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/README.md | Proof required: Story 8.8 must validate release or submodule-pin availability, package consumption, endpoint validation behavior, bounded ProblemDetails behavior, and correlation semantics before deleting Parties-local HTTP helpers. Consumption availability recorded 2026-07-16 at released `Hexalith.Commons.Http` `2.28.1` / tag `v2.28.1`, matching root-declared `references/Hexalith.Commons` gitlink `b03469b13408530bb757d3d02279c2d772ee4848`; the cited helper files exist at that release. If Story 8.8 selects another package or gitlink identity, it must refresh this row before consumption. | 8.8, 8.10 | No Parties source migration starts in Story 8.3. Keep local rollback path: Parties client registration and HTTP helper code stay until Story 8.8 proves release/package and behavior compatibility. | `git ls-tree HEAD references/Hexalith.Commons`; `git -C references/Hexalith.Commons rev-parse v2.28.1^{}`; `rg -n -F 'HttpClientRegistration' references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/HttpClientRegistration.cs`; `rg -n -F 'BoundedProblemDetailsReader' references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/BoundedProblemDetailsReader.cs`; `rg -n -F 'HttpCorrelation' references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/HttpCorrelation.cs`. |
| MCP, deep-link, and search probes | Hexalith.FrontComposer.Mcp (auth/context/relay policy); Hexalith.FrontComposer.Contracts.UI and Hexalith.FrontComposer.Shell (EventStore link and UI capability contract); Hexalith.Commons.Http (domain-neutral header, URI, and bounded health mechanics) | needs-additive-api | references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Mcp/FrontComposerMcpOptions.cs; references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Mcp/IFrontComposerMcpAgentContextAccessor.cs; references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Mcp/HttpFrontComposerMcpAgentContextAccessor.cs; references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Mcp/Extensions/FrontComposerMcpServiceCollectionExtensions.cs; references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/HttpClientRegistration.cs; src/Hexalith.Parties.Mcp/McpContextForwardingHandler.cs; src/Hexalith.Parties.Mcp/PartiesMcpRequestContext.cs; src/Hexalith.Parties.AdminPortal/Services/AdminPortalEventStoreAdminLinks.cs; src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs | Proof required: G11 owner proof requires three accepted work packages: (A) FrontComposer.Mcp composes outbound relay from server-resolved authenticated tenant/user context, while Commons.Http may supply CR/LF-free single-value replace-not-append header mechanics; bearer credentials come only from an approved authenticated source, FrontComposer MCP API keys are never reused downstream, API-key calls require an explicit host credential provider or fail closed, diagnostics disclose no tokens/raw identity, and existing tenant-tool/resource-visibility gates remain mandatory; (B) FrontComposer exposes a configured EventStore Admin UI aggregate/stream and correlation deep-link builder over optional Commons URI mechanics, requiring absolute HTTP/HTTPS base URIs without user-info, safe base-path/query preservation, single encoding, and typed unavailable outcomes; (C) Commons.Http bounds HTTP/JSON and named-health-check extraction while FrontComposer owns the configured UI capability result, with cancellation/timeouts/size limits and fail-closed Available/LocalOnly/Degraded mapping without raw downstream detail. Story 8.8 must record named approvals, exact package versions or root-gitlink identities, public API/package validation, producer security/bounds tests, Parties MCP/AdminPortal consumer parity, and rollback before deletion. Routed by `sprint-change-proposal-2026-07-16-g4-g11-frontcomposer-shared-primitives-routing.md`; routing is not delivery. | 8.8, 8.10 | No Parties source migration starts in Story 8.3. Keep rollback path: `McpContextForwardingHandler`, `PartiesMcpRequestContext`, the five Parties MCP tool definitions, `AdminPortalEventStoreAdminLinks`, and `GetRichSearchCapabilityAsync` remain until each Story 8.8 G11 slice proves parity against the approved producer identities; delivery of one sub-surface does not authorize deletion of another. | `rg -n -F 'GetContext()' references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Mcp/HttpFrontComposerMcpAgentContextAccessor.cs`; `rg -n -F 'IFrontComposerMcpTenantToolGate' references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Mcp/Extensions/FrontComposerMcpServiceCollectionExtensions.cs`; `rg -n -F 'HttpClientRegistration' references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/HttpClientRegistration.cs`; `rg -n -F 'TryAddWithoutValidation' src/Hexalith.Parties.Mcp/McpContextForwardingHandler.cs`; `rg -n -F 'BuildCorrelationLink' src/Hexalith.Parties.AdminPortal/Services/AdminPortalEventStoreAdminLinks.cs`; `rg -n -F 'GetRichSearchCapabilityAsync' src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs`; explicit inspection found no approved shared relay, EventStore Admin link builder, or bounded named capability-health probe. |
| Builds shared props/targets | Hexalith.Builds | available | references/Hexalith.Builds/Hexalith.Build.props; references/Hexalith.Builds/Hexalith.Package.props; references/Hexalith.Builds/Props/Directory.Packages.props; references/Hexalith.Builds/README.md; references/Hexalith.Builds/Github/package-release/README.md | Proof required: Story 8.8 and 8.10 must validate centralized package versions, source/package mode, CI/release templates, release or submodule-pin availability, and no-warning-override gates before removing Parties-local build probing. Consumption availability recorded 2026-07-16 at release/tag `4.18.5` / `v4.18.5`, matching root-declared `references/Hexalith.Builds` gitlink `ed75ae3c45425b9610d5e75e6c5ec3e8d5283fe1`; the cited props/targets and release guidance exist at that release. If Story 8.8 selects another gitlink identity, it must refresh this row before consumption. | 8.8, 8.10 | No Parties source migration starts in Story 8.3. Keep local rollback path: Parties `Directory.Build.props`, test script guards, and local build probes stay until Story 8.8 proves the shared build path. | `git ls-tree HEAD references/Hexalith.Builds`; `git -C references/Hexalith.Builds rev-parse v4.18.5^{}`; `rg -n -F 'TreatWarningsAsErrors' references/Hexalith.Builds/Hexalith.Build.props`; `rg -n -F 'Package' references/Hexalith.Builds/Hexalith.Package.props`; `rg -n -F 'Directory.Packages.props' references/Hexalith.Builds/README.md`; `rg -n -F 'package-release' references/Hexalith.Builds/Github/package-release/README.md`. |
| Package publishing/source-mode CI | Hexalith.Commons and Hexalith.Tenants | available | references/Hexalith.Commons/.releaserc.json; references/Hexalith.Commons/scripts/pack-release-packages.py; references/Hexalith.Commons/scripts/validate-nuget-packages.py; references/Hexalith.Tenants/.releaserc.json; references/Hexalith.Tenants/scripts/pack-release-packages.py; references/Hexalith.Tenants/scripts/validate-consumer-package-references.py; _bmad-output/implementation-artifacts/tests/test-summary.md | Proof required: Story 8.8 must validate release package consumption and behavior parity before deleting Parties-local rollback paths. G12 publication proof recorded 2026-07-11: NuGet serves `Hexalith.Commons.Http` 2.28.0, `Hexalith.Commons.ServiceDefaults` 2.28.0, `Hexalith.Tenants.Client` 2.4.2, and `Hexalith.Tenants.Testing` 2.4.2 at the repository pins; package-only restore and the Release solution build completed with 0 warnings and 0 errors. | 8.8, 8.10 | No Parties source migration starts in Story 8.3. Package publication was selected; source-mode CI blessing is not required for G12. Keep local rollback path until Story 8.8 proves the remaining G6/G8/G11/G7-G9 prerequisites and behavior compatibility. | `rg -n -F 'Hexalith.Commons.Http' references/Hexalith.Commons/scripts/pack-release-packages.py`; `rg -n -F 'Hexalith.Commons.ServiceDefaults' references/Hexalith.Commons/scripts/pack-release-packages.py`; `rg -n -F 'Hexalith.Tenants.Client' references/Hexalith.Tenants/scripts/validate-consumer-package-references.py`; `rg -n -F 'Hexalith.Tenants.Testing' references/Hexalith.Tenants/scripts/validate-consumer-package-references.py`; `curl -fsS https://api.nuget.org/v3-flatcontainer/hexalith.commons.http/index.json`; `curl -fsS https://api.nuget.org/v3-flatcontainer/hexalith.commons.servicedefaults/index.json`; `curl -fsS https://api.nuget.org/v3-flatcontainer/hexalith.tenants.client/index.json`; `curl -fsS https://api.nuget.org/v3-flatcontainer/hexalith.tenants.testing/index.json`; package-only restore and Release build commands/results are recorded in `_bmad-output/implementation-artifacts/tests/test-summary.md`. |

The G5 cell keeps dated inspection identities only; the current reconciliation above selects the approved retained graph. G5 availability and adoption remain gated.
<!-- platform-api-prerequisite-matrix:end -->

### I20 approval table — reconciled 2026-10-05

The original selection row transcribes the Round 6 reviewer-recorded decision in `spec-8-10-final-readiness-documentation-and-retirement-gate.md` (D1). The three advance rows record the Administrator's explicit "yes" on 2026-10-04 to approving exactly the EventStore, FrontComposer, and Tenants pins shown below. That approval authorizes recording/signing these existing current source pins and updating their guards; it does not authorize owner releases, publishing, rollback deletion, or parity approval. Their "approved current" and "now select" wording was true at Parties `c782b68c5cf56a19e6a2a237f5f44e3043d5e461`. The working-tree five-pin row records the Administrator's later 2026-10-04 choice "approve working tree"; it supersedes the Builds and Memories Round 6 identities and the EventStore, FrontComposer, and Tenants three-pin identities, which remain dated history. Selecting an identity does not certify parity. Pending rows grant no authority; reviewer/human approval and exact artifact/identity evidence must complete each applicable row before its gated action. The 2026-10-05 rows record the Administrator's four decisions in spec 8.10 "Final identity, approval, and EventStore 3.113.0 reconciliation — 2026-10-05": Builds/Tenants identity selection, the EventStore `3.113.0` package and source update, the I16 identity re-validation approval for the final set, and the I13/DW-111 accepted deferral. They supersede the 2026-10-04 EventStore and Tenants working-tree identities (never committed) and the Builds working-tree identity `145ae921`; the never-approved committed pointers stay dated history.

| Decision | Artifact and exact scope | Named human | Date | Disposition / authority |
| --- | --- | --- | --- | --- |
| I4/I16 Round 6 identity selection | Spec 8.10 Round 6 D1: AI.Tools `3f194e17174994d308ec84af9ee2b5aa68674d0d`; Builds `688eec9a4333245cc0ff7772115c769094471863`; Commons `116d26815eb81e35b3c161e1799e5ee12805fc0a`; EventStore `b046425503c694d5857e8bba890351840fa52c26`; FrontComposer `bf40099f81fcaeac324b7b4377513ac7d49cead4`; Memories `3d72927f4dac66af4968cc6726c4e96df292f2e4`; PolymorphicSerializations `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375`; Tenants `b63bdbba8613801bb488eaa9116f386e41a261f8`; EventStore package `3.110.0`, Commons `2.30.1`, FrontComposer `4.5.0` | Administrator / jpiquot (existing Round 6 decision) | 2026-10-04 | Recorded approval of this exact set only; prior parity receipts unvalidated. |
| I4/I16 later EventStore advance | `references/Hexalith.EventStore`: prior approved `b046425503c694d5857e8bba890351840fa52c26` → approved current `cbbe41501ba722731bf36b2c343efdef4ac714fb` at Parties `c782b68c5cf56a19e6a2a237f5f44e3043d5e461`; constants and `.gitlink-signoff.tsv` now select the current ID | Administrator / jpiquot | 2026-10-04 | **Approved identity selection** — explicit human "yes" to the three-pin approval request; parity remains unvalidated and the separate I16 re-validation row remains pending. |
| I4/I16 later FrontComposer advance | `references/Hexalith.FrontComposer`: prior approved `bf40099f81fcaeac324b7b4377513ac7d49cead4` → approved current `374bb83392d8ab4e8a8397cfd312d09948fb0b9d` at Parties `c782b68c5cf56a19e6a2a237f5f44e3043d5e461`; constants and `.gitlink-signoff.tsv` now select the current ID | Administrator / jpiquot | 2026-10-04 | **Approved identity selection** — explicit human "yes" to the three-pin approval request; parity remains unvalidated and the separate I16 re-validation row remains pending. |
| I4/I16 later Tenants advance | `references/Hexalith.Tenants`: prior approved `b63bdbba8613801bb488eaa9116f386e41a261f8` → approved current `cc348c9d7839ec7aad01649fc1c0e6f4fe672da4` at Parties `c782b68c5cf56a19e6a2a237f5f44e3043d5e461`; constants and `.gitlink-signoff.tsv` now select the current ID | Administrator / jpiquot | 2026-10-04 | **Approved identity selection** — explicit human "yes" to the three-pin approval request; parity remains unvalidated and the separate I16 re-validation row remains pending. |
| I4/I16 EventStore package 3.112.0 selection | Parties `Directory.Packages.props` pre-import CPM pin; all `Hexalith.EventStore.*` package consumers select `3.112.0`. Supersedes package `3.110.0`; source identities remain separate. | Administrator / jpiquot | 2026-10-04 | **Approved package selection** — explicit human request "update eventstore to version 3.112.0". No parity or unrelated source-pin approval is inferred. |
| I4/I16 working-tree five-pin approval | Exactly five clean fast-forward root checkouts observed at Parties `7c720c7f9879ecb469993b1d2dff2bdb72b96e1b`: Builds `688eec9a4333245cc0ff7772115c769094471863` → `145ae921d9032f110b7371614939559e0aee202a` (`v4.29.1-16-g145ae92`); EventStore `cbbe41501ba722731bf36b2c343efdef4ac714fb` → `2242ad55a1b678828df8aa093fd92399c29af5bf` (`v3.112.0-2-g2242ad55`); FrontComposer `374bb83392d8ab4e8a8397cfd312d09948fb0b9d` → `2cc8dd3a3ac76c03f5ea6f6f92e65829306db470` (`v4.5.0-121-g2cc8dd3a`); Memories `3d72927f4dac66af4968cc6726c4e96df292f2e4` → `5b43fe2f8a0f04dc021921a077dff1a573c2ce5e` (`v2.28.0`); Tenants `cc348c9d7839ec7aad01649fc1c0e6f4fe672da4` → `04e655cf070b17eced9daefb9eaa87a09ec60e81` (`v5.7.0-141-g04e655cf`). The intermediate committed pointers at that HEAD (EventStore `e968467c7db5b5685894fa2b85cfa7ec14512a7c`, FrontComposer `37c8c6d295032968edba347940c41614ce8f2573`, Memories `47027d35a1f4a2c6986bec85b5cae601ce1b201e`, Tenants `c05fe3171b2d3f95e0dd0970edfa6c421bed2f19`) are not approved. Constants and `.gitlink-signoff.tsv` now select the five approved IDs; the gitlinks stay uncommitted until the human commits them. | Administrator / jpiquot | 2026-10-04 | **Approved identity selection** — explicit human choice "approve working tree" for exactly these five identities and nothing else, recorded in spec 8.10 "Working-tree identity reconciliation — 2026-10-04". Authorizes recording, signing, and guarding these source pins only; no I16 parity, owner release, publishing, commit, or rollback deletion. The committed-tree guard stays red until the gitlinks are committed; the separate I16 re-validation row remains pending. Outcome (2026-10-05): Builds `145ae921`, FrontComposer `2cc8dd3a`, and Memories `5b43fe2f` were committed in Parties `2f6157d330139e38d031c9a3d1925bca5c317ff7`; EventStore `2242ad55` and Tenants `04e655cf` were never committed. The 2026-10-05 rows below supersede the Builds, EventStore, and Tenants identities. |
| I4/I16 final Builds and Tenants identities (2026-10-05 decision 1) | `references/Hexalith.Builds`: `145ae921d9032f110b7371614939559e0aee202a` → `360a2b9c4e96809365a7de785be9a68152d5ac28` (`v4.29.1-17-g360a2b9`); `references/Hexalith.Tenants`: `04e655cf070b17eced9daefb9eaa87a09ec60e81` (never committed, superseded) → `72b8e4f508176b69826549e87b7b2a286f607fd1` (`v5.7.0-143-g72b8e4f5`). Both were committed in Parties `2bdb303b31739b7631036fc15ac21a012724ddb8` and are checked out clean at Parties HEAD `47e2da3244fd7d6d14e39bc30513b23504e9bc27`. The committed EventStore `547c938d52e4fd31b47783b9dd032528a5100549` / `0dc44e46ccb7f56c3182b855c21f337173a1307f` and Tenants `b55f96d89b839fd443ac589835880102add46d64` are never approved. Constants and `.gitlink-signoff.tsv` select the two approved IDs. | Administrator / jpiquot | 2026-10-05 | **Approved identity selection** — exactly these two identities and nothing else, recorded in spec 8.10 "Final identity, approval, and EventStore 3.113.0 reconciliation — 2026-10-05". No owner release, publishing, or rollback deletion. Outcome (2026-10-05): Parties `75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6` committed the EventStore `865cd9e4` gitlink, so all eight final-set gitlinks are committed. |
| I4/I16 EventStore 3.113.0 package and source (2026-10-05 decision 2) | Parties `Directory.Packages.props` pre-import CPM pin `HexalithEventStoreVersion=3.113.0` for every `Hexalith.EventStore.*` package consumer (supersedes `3.112.0`); root checkout `references/Hexalith.EventStore` at tag `v3.113.0` = `865cd9e49273dffbb1cdae85efeaf1aac322e09e`, one fast-forward commit past `0dc44e46`, superseding the never-committed 2026-10-04 approval of `2242ad55a1b678828df8aa093fd92399c29af5bf`. Constants and `.gitlink-signoff.tsv` select this identity; the root gitlink commit is pending (the human-directed commit that follows this pass). | Administrator / jpiquot | 2026-10-05 | **Approved package and source selection** — explicit human direction "update eventstore to version 3.113.0" covering both; package and source are the same release. The committed-tree guard stays red for EventStore until the gitlink is committed. No owner release, publishing, or rollback deletion. Outcome (2026-10-05): Parties `75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6` committed the EventStore `865cd9e4` gitlink, so all eight final-set gitlinks are committed. |
| I1 route-list change | Any local/external ACL or subscription delta | Pending — named accountable owner | Pending | Pending; no approval inferred from routing, source presence, or compile. |
| I1a successor AppHost and deferral re-approval | Successor topology and every AppHost rollback deferral | Pending — named accountable owner | Pending | Pending; no approval inferred from routing, source presence, or compile. |
| I2/I19a policy-hook classification | G5 IPersonalDataPolicy / IErasureStateProvider classification | Pending — named accountable owner | Pending | Pending; no approval inferred from routing, source presence, or compile. |
| I5/I7 intentional public/certificate versioning and erasure doors | Any public contract, certificate, or erasure-door change | Pending — named accountable owner | Pending | Pending; no approval inferred from routing, source presence, or compile. |
| I10 freshness dialect | Named G6 freshness shape and grammar | Pending — named accountable owner | Pending | Pending; no approval inferred from routing, source presence, or compile. |
| I16 identity parity re-validation | Exact I9/I10/I13 surfaces and all affected claim stamps at the 2026-10-05 final set: AI.Tools `3f194e17174994d308ec84af9ee2b5aa68674d0d`; Builds `360a2b9c4e96809365a7de785be9a68152d5ac28`; Commons `116d26815eb81e35b3c161e1799e5ee12805fc0a` / package `2.30.1`; EventStore `865cd9e49273dffbb1cdae85efeaf1aac322e09e` / package `3.113.0`; FrontComposer `2cc8dd3a3ac76c03f5ea6f6f92e65829306db470` / package `4.5.0`; Memories `5b43fe2f8a0f04dc021921a077dff1a573c2ce5e` / package `2.27.1`; PolymorphicSerializations `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375`; Tenants `72b8e4f508176b69826549e87b7b2a286f607fd1` / package `5.7.0` | Administrator / jpiquot | 2026-10-05 | **Approved (decision 3)** for the final set only. The approval does not turn a receipt into `Pass`: a receipt is `Pass` only when it was rerun at the stamped identity, and any receipt not rerun stays `Unvalidated` in the canonical `tests/test-summary.md` table. Claims stamped at any other identity stay unvalidated. No rollback deletion is authorized by this row. |
| I13 / DW-111 accepted closure deferral (2026-10-05 decision 4) | The missing runtime content-control focus, forced-colors, and reduced-motion proof (DW-111) is accepted as a named deferral owned by `8.9-frontcomposer-ui-consolidation` for Story 8.10 closure. I13 itself is **not** discharged; spine §7 keeps it `Deferred`. | Administrator / jpiquot | 2026-10-05 | **Accepted deferral** — a waiting contract only. It grants no parity, no I13 discharge, and no deletion of the retained Parties UI primitives or the app-owned focus-visible, forced-colors, and reduced-motion CSS. |
| I4/I16 current identity set and selected-gitlink accessibility stamp (2026-11-01 Round 7 decision 3) | Parties `b6443b2475d3495b58358f84cdb161b05c62d0f9`: AI.Tools `9b78aa008cfc62cd88ad0ea4b39b1f07311956ce`; Builds `df13d9d31f7ee1a1b0fb7eeac7e8b27b9a9f9fb0`; Commons `bbbce518bcca9ca359762393688f02d142af1c61` / package `2.30.8`; EventStore `ba4c5548ad260b049bda5a1c7def2e8af3484733` / package `3.130.2`; FrontComposer `c092e8f91ebb670fad4ab959f2d3c8e13d809e0f` / package `4.7.4`; McpCli `17eb4fbbc151ca4b99dfa452e9f65655db2b402f`; Memories `b93b32cf77f3d76e51d3221550eff99f4d1e93cb` / package `3.0.13`; Platform `03799ed553a16c8a1528068ba4d95bb5509ea75e`; PolymorphicSerializations `821b784bb51483acefa9725acc6792d111304a4e`; Tenants `2d3fa6da2312c1b5cf7f5c9461cc2d0ffc995b43` / package `6.0.0`. Accessibility Pass names the selected FrontComposer root gitlink bound by the v2 `parties_snapshot`; closed evidence uses the closure commit's selection. | Administrator / jpiquot | 2026-11-01 | **Approved exact identity set and stamp rule** — transcribed from the resolved Round 7 decision 3 in spec 8.10. This approval qualifies no closure lane; the canonical `tests/test-summary.md` table (the last `### Validation receipts` heading) records each lane's status. No historical parity, publication, deployment, custody or rollback deletion is authorized. |
| DW-182 / I16 current committed selection (2026-11-02) | Parties `a92d15866bbf4f49cefa6c03f4a9eeec442af1de`: AI.Tools `1745b6663ad32bb1288167135ed0f979366c53a0`; Builds `d2a430c988462a634674a69c562ea4afd1f06ef4`; Commons `11555ace9efcae2928a00122dcba91243e5a8a1a`; EventStore `7c93a1234d14c66f5ee85ed48caaee1d30a671f3`; FrontComposer `07089dcbdad784a1d53037e9cea78d1d7becd8d6`; McpCli `3bc89642ad43305531c4018b75b5e1b9e0681e10`; Memories `e8956eb5c1bd49297f86702f9606eb9b013bc2f1`; Platform `92f87e6a49e4357f91b2ffb821e044931291c0e6`; PolymorphicSerializations `bce4eb9f45c6b131b960ab4fa39e50ff01e51960`; Tenants `4b3e431745550f8466f1ce0d70e6b507c747b59a` and effective Release packages EventStore `3.132.2`, Commons `2.30.10`, PolymorphicSerializations `1.19.8`, FrontComposer `4.7.4`, Memories `3.0.13`, Tenants `6.0.0`, McpCli `1.1.0`; exact comparison in `dw-182-i16-approval-comparison-2026-11-02.md`. | Administrator / jpiquot | 2026-11-02 | **Approved current-selection assertions only.** The 2026-11-01 approval and earlier receipts remain dated history; all six canonical parity receipts stay Unvalidated until rerun at their stamped identities. No publication, deployment, custody, or rollback deletion authority follows. |
| DW-182 / I16 Tenants successor selection (2026-11-02) | Parties `18579b62ef9ac3c8780fc5e8ba26112d2665f566`: AI.Tools `1745b6663ad32bb1288167135ed0f979366c53a0`; Builds `d2a430c988462a634674a69c562ea4afd1f06ef4`; Commons `11555ace9efcae2928a00122dcba91243e5a8a1a`; EventStore `7c93a1234d14c66f5ee85ed48caaee1d30a671f3`; FrontComposer `07089dcbdad784a1d53037e9cea78d1d7becd8d6`; McpCli `3bc89642ad43305531c4018b75b5e1b9e0681e10`; Memories `e8956eb5c1bd49297f86702f9606eb9b013bc2f1`; Platform `92f87e6a49e4357f91b2ffb821e044931291c0e6`; PolymorphicSerializations `bce4eb9f45c6b131b960ab4fa39e50ff01e51960`; Tenants `4d0d599879767196928e1c265622483a62b457ae` and effective Release packages EventStore `3.132.2`, Commons `2.30.10`, PolymorphicSerializations `1.19.8`, FrontComposer `4.7.4`, Memories `3.0.13`, Tenants `6.0.0`, McpCli `1.1.0`; exact comparison in `dw-182-i16-approval-comparison-2026-11-02.md`. | Administrator / jpiquot | 2026-11-02 | **Approved Tenants successor current-selection assertion and gitlink signoff only.** This supersedes the initial 2026-11-02 Tenants `4b3e431745550f8466f1ce0d70e6b507c747b59a` selection; the other nine gitlinks and effective package versions are unchanged. All six canonical parity receipts stay Unvalidated until rerun at their stamped identities. No publication, deployment, custody, or rollback deletion authority follows. Explicitly confirmed on 2026-11-02 at the spec 8.10 Round 9 review (reviewer gate): presented the exact SHA and the option to confirm it explicitly, the owner replied "apply recommended". |
| DW-182 / I16 Platform successor selection (2026-11-02) | Platform `c635a2dc22f404d97c9fe7e27f7e7f4c9017943b` (`v1.1.1-35-gc635a2dc`, on Platform `origin/main`), selected by Parties `957111c5a516b7fa7c2fe19eaadf3ae09522e792` and recorded by `fa9d6286a16fc725acb45215b3bebec202c7a78e`. Since Parties `18579b62ef9ac3c8780fc5e8ba26112d2665f566` no other root gitlink, `.gitmodules`, `global.json`, top-level `.props`/`.targets` or Client project input changed, so the other nine gitlinks and the effective Release package selection of the Tenants successor row stand. | Administrator / jpiquot | 2026-11-02 | **Approved Platform successor gitlink signoff only.** This supersedes Platform `92f87e6a49e4357f91b2ffb821e044931291c0e6` for current-selection assertions. The owner confirmed it explicitly on 2026-11-02 during the spec 8.10 Round 10 requalification: shown the exact SHA and the failing `gitlink-rc-gate` result, they chose "Sign off and requalify". The validated-advance row is in `.gitlink-signoff.tsv`. This approval qualifies no closure lane; the canonical `tests/test-summary.md` table (the last `### Validation receipts` heading) records each lane's status. No publication, deployment, custody, or rollback deletion authority follows. |
| Story 2.8 delegated selection (2026-11-03) | Builds `26991e8c542704be44cd8d9f58084d56fa627adb`; Platform `bf101b9d5f19ba741852b0230e526d1f01422022`; effective prepared Release packages EventStore `4.0.4`, Commons `2.30.10`, PolymorphicSerializations `1.19.8`, FrontComposer `4.7.5`, Memories `3.0.18`, Tenants `7.0.0`, McpCli `2.0.0`. Other root source selections are unchanged. | codex-agent (explicit user delegation) | 2026-11-03 | **Source-selection only.** Historical jpiquot approvals and all six Unvalidated parity receipts remain unchanged; no package/runtime parity, publication, deployment, custody, rollback deletion or candidate acceptance is claimed. |
| DW-182 / I16 owner-approved /pushall advance (2026-11-03) | Parties `f7ef3e23561b58c3dca38c787258e5b2d5fa0d86` with the released Platform v1.1.6 / Builds v4.43.5 tuple: AI.Tools `1745b6663ad32bb1288167135ed0f979366c53a0`; Builds `89f62052c932d054bda2b435bdfef60d96e77612`; Commons `11555ace9efcae2928a00122dcba91243e5a8a1a`; EventStore `a9205f511b4f96b5daf94d65369d6dee63e0c623`; FrontComposer `fbe01c913cb863abb0fd4a317d370d36fff593b3`; McpCli `2e80701c31aafdf00abee05ac469a45ed76dfdf5`; Memories `d959bdae5af9c22f53573eda71c091cb5c0aae4c`; Platform `8ea8fe092f8abb5c932a61830aae16051d442463`; PolymorphicSerializations `bce4eb9f45c6b131b960ab4fa39e50ff01e51960`; Tenants `33835a03e92579a26400e7cb7cf68d666c715b3e` and effective Release packages EventStore `4.0.4`, Commons `2.30.10`, PolymorphicSerializations `1.19.8`, FrontComposer `4.7.5`, Memories `3.0.18`, Tenants `7.0.0`, McpCli `2.0.0`. | Administrator / jpiquot | 2026-11-03 | **Approved current-selection assertions and gitlink signoff only.** This supersedes the Story 2.8 delegated Builds/Platform selection and the 2026-11-02 Tenants and Platform successor rows for current-selection assertions; those rows remain dated history. The owner confirmed it explicitly on 2026-11-03: presented with the seven advanced gitlinks, the expected fitness and `gitlink-rc-gate` failures and the revert alternative, they chose "keep bumps". After `prepare-module-build` refused the untagged Platform/Builds pair (HXW011/HXW009), the owner chose "Pin released tuple", and the untagged Platform and Builds advances were withdrawn. The validated-advance rows are in `.gitlink-signoff.tsv`. All six canonical parity receipts stay Unvalidated until rerun at their stamped identities. No publication, deployment, custody, or rollback deletion authority follows. |
| Story 2.8 released tuple continuation (2026-11-03) | Builds `725754075e1d6369430a3adf94d46fb71e6eaf9e` (`v4.43.6`); Platform `ed1560a57f4d4cb55b6b03f485cbfa01316837a7` (`v1.1.7`); effective Release packages EventStore `4.0.4`, Commons `2.30.10`, PolymorphicSerializations `1.19.8`, FrontComposer `4.7.5`, Memories `3.0.18`, Tenants `7.0.0`, McpCli `2.0.0`. All other incoming source selections are unchanged. | codex-agent (explicit user delegation) | 2026-11-03 | **Source-selection only.** Every dated human approval and all six Unvalidated parity receipts remain unchanged. Actual Parties native/hosted qualification is pending; no new human ratification, package/runtime parity, publication, deployment, custody, rollback deletion or candidate acceptance is claimed. |
| Earlier package catalog alignment (2026-11-04) | Parties direct Platform `e2cc081e539942563ec0d6a243b308b4cab77898` (v1.1.8) and Builds `725754075e1d6369430a3adf94d46fb71e6eaf9e` (v4.43.6) gitlinks; effective package graph unchanged. | codex-agent (explicit user delegation) | 2026-11-04 | **Source-selection and CI repair only.** The active request authorizes synchronizing direct submodules and fixing CI. Platform v1.1.8 and Builds v4.43.6 have public release and exact-source CI evidence; `.gitlink-signoff.tsv` records both advances. No historical parity, release, deployment or rollback authority follows. |
| Package catalog alignment (2026-11-04) | Parties direct Platform `8ea8fe092f8abb5c932a61830aae16051d442463` (v1.1.6) and Builds `89f62052c932d054bda2b435bdfef60d96e77612` (v4.43.5) gitlinks; effective package graph unchanged. | codex-agent (explicit user delegation) | 2026-11-04 | **Source-selection and CI repair only.** The user explicitly requested pushing the existing working-tree selections. This supersedes the earlier package catalog alignment for current-selection assertions and retains the existing release signoffs. Historical parity receipts and publication, deployment and rollback authority remain unchanged. |
| Subscription safety delegated source (2026-11-03) | EventStore `f213e4115ec23cb736e8aa0113f61b9d4854480c` (`v4.0.4-12-gf213e411`); published EventStore package `4.0.4`. Other root selections and the effective package graph remain as in the preceding owner decision. | codex-agent (explicit user delegation) | 2026-11-03 | **Source-selection and executed transport proof only. Package versions unchanged.** Authorized by the active request to implement five deferred items and push main, then approve and continue. The dated subscription receipt records the scoped tests. Selection does not revalidate historical parity receipts or grant release, deployment, custody, rollback deletion, native replay or full Memories acceptance. |
| I17 activation handover | Any second activation of an accepted closure deferral | Pending — named accountable owner | Pending | Pending; no approval inferred from routing, source presence, or compile. |
| I18 successor tests | Any baseline-test replacement | Pending — named accountable owner | Pending | Pending; no approval inferred from routing, source presence, or compile. |
| I19 store reclassification / class-(b) test set | Touched stores, semantics, and approved consistency tests | Pending — named accountable owner | Pending | Pending; no approval inferred from routing, source presence, or compile. |
| §2 divergence from Epic 7 | G7/G9 historical SCP and 2026-09-27 McpCli course correction: authority backfill required | Pending — named accountable owner | Pending | Pending; no approval inferred from routing, source presence, or compile. |

### Story 8.7 G5 gate revalidation — 2026-11-03

At Parties `fcf7cbc86076ce43f0982e4959da9691feaf2d3a`, EventStore
`7c93a1234d14c66f5ee85ed48caaee1d30a671f3` and Builds
`d2a430c988462a634674a69c562ea4afd1f06ef4` match clean committed root
checkouts. Default Release consumption evaluates to EventStore `3.132.2`;
Builds defaults to `3.132.1`. This is prerequisite inspection, not G5 approval
or a new source/package selection.

Owner 8.1–8.4 are done, 8.5 is in progress, and 8.6–8.11 remain backlog.
The core is non-packable, Server defaults to the no-op, and the AzureKeyVault
project, engine catalog/release enrollment, and 8.11 closure packet are absent.
I2/I19a classification, named G5/security approval, consumable runtime/backend
and persistence integration, dual-provider GDPR parity, seven deletion proofs,
and post-v2 rollback remain missing. G5 stays `needs-additive-api`; Parties 8.7
stays `blocked`; the crypto-retention action stays open.

The unchanged G5 row's 32 structural commands pass, and all 24 retained files,
the local harness, and four local DI registrations remain. The focused Debug
source test-project build is blocked by 15 Tenants CS0234/CS0246 errors for
missing EventStore checkpointed projection APIs; no focused xUnit execution or
provider parity is credited. See `tests/story-8-7-g5-2026-11-03/` and the dated
`tests/test-summary.md` entry. Production, dependencies, gitlinks, rollback,
and approval-table decisions are unchanged.

### Story 8.7 G5 gate revalidation — 2026-10-10

At Parties `56cb4401c4179d431385ad3d931fefa402e4fc5a`, clean root
checkouts match EventStore `785260fa233880e403bf80bd1bc2f1d53b1f4836`
(`v3.119.0-9-g785260fa`) and Builds
`6a002df5fa60f2f880f09ffd90b3bfaf819bcbf5`
(`v4.30.1-22-g6a002df`). Parties still selects EventStore package `3.117.1`;
Builds now defaults to `3.119.0`. These are observations, not an approved G5
source or package identity.

EventStore 8.4 is now `done`: its internal compatibility router can classify
mixed event and snapshot histories and route v1 to a registered legacy reader.
Its approved scope excludes Server read wiring, lifecycle, Azure, package
release, Parties adoption, and v2 write enablement. The core remains
`IsPackable=false`; Server still defaults to the no-op. Owner 8.5–8.11 are
`backlog`; the 8.11 closure packet, AzureKeyVault backend, release enrollment,
named G5/security approval, I2/I19a classification, dual-provider parity,
seven deletion proofs, and post-v2 switch-back are missing. G5 stays
`needs-additive-api`, Parties 8.7 stays `blocked`, and local rollback remains.

The former 32-check procedure and Parties prerequisite fitness both contain
stale live assertions: the owner 8.4 status changed, while the latter also
selects older root gitlinks. The G5 row command and its owner-status fitness
assertion now check `done`; the release-identity guard is left red pending
separate exact identity approval. Results are in `tests/test-summary.md`.

### Story 8.7 G5 gate revalidation — 2026-10-09

At Parties `91efcfd9ed31a0b4cb39dfe8263ddb48842190fb`, EventStore and Builds
root gitlinks match clean checkouts `75a08f0069d8c2495d9dff20a0deb84edb6cc638`
and `fef031806321793c9effb17235c2465118984432`, respectively. Both package
selectors remain EventStore `3.117.1`. EventStore 8.3 is `done` under its own
2026-10-09 closure packet, which explicitly grants no G5 availability, successor,
package, or provider approval. EventStore 8.4–8.11 remain `backlog`.

The v2 core remains non-packable; Server still defaults to the no-op. The
compatibility reader, key lifecycle, AzureKeyVault backend, Server persistence,
release enrollment, dual-provider GDPR parity, post-v2 rollback, and 8.11
approval-closure packet are absent. I2/I19a policy-hook classification is still
pending. G5 remains `needs-additive-api`, Parties 8.7 remains `blocked`, and all
local rollback files and registrations remain. This observation grants no
production or dependency change, parity credit, or deletion authority.

### Historical Story 8.7 G5 gate revalidation — 2026-10-08

At Parties `998c0b649d7bbf01e0fed4cc79924662dd16cc91`, EventStore's root gitlink and clean checkout match
`9542d3c9f48bf9ce1c57f2ef68904703eaba56cc` (`v3.117.1-5-g9542d3c9`); Builds' root gitlink and clean checkout
match `6f07763bd955d22ace0123798add528dc933bf51` (`v4.30.0-13-g6f07763`). Both catalogs select EventStore
`3.117.1`. These observations supersede the 2026-10-05 inspection for Story 8.7;
they do not change the dated G5 row, I20 approvals, or frozen adoption identity.

| Gate evidence | Current inspection | Disposition |
| --- | --- | --- |
| Owner contracts/core | Public policy/erasure hooks and internal v2 core exist; core is `IsPackable=false`. Owner 8.2 is `done`, 8.3 is `in-progress`. | Partial delivery; existing owner approvals do not grant G5 availability. |
| Compatibility, lifecycle, backend, runtime, release | Owner 8.4-8.8 remain `backlog`; AzureKeyVault project absent; payload packages absent from catalog and release inventory; server still defaults to the no-op. | No consumable approved provider. |
| Consumer parity and rollback | Owner 8.9/8.10 remain `backlog`; real dual-provider GDPR parity and post-v2 switch-back unproven. | Adoption and deletion remain gated. |
| Availability and policy ownership | Owner 8.11 remains `backlog`; closure packet absent; G5 availability and I2/I19a hook-classification approvals missing. | G5 stays `needs-additive-api`; 8.7 stays `blocked`. |
| Parties continuity | 8.6 is `done`; all 24 MOVE/KEEP/adapter files, local harness, and local DI remain. | Sequence satisfied; crypto-retention remains `open`. |

All 32 recorded G5 static checks, retained-file/DI checks, and matching-clean-identity
assertions passed. Exact commands/results appear in `tests/test-summary.md`.
No product/dual-provider/GDPR/post-v2 suites were run or credited. Production KMS
remains a separate release gate. No production, dependency, submodule, frozen-spec,
approval-table, or sprint-status data change occurred.

### Historical Story 8.7 G5 gate revalidation — 2026-10-05

At Parties `b3794a4dcbe2fff3e9ea5c420a3c4695e2d1a8ca`, EventStore's root gitlink and clean checkout
match `865cd9e49273dffbb1cdae85efeaf1aac322e09e` (`v3.113.0`). Builds' root gitlink and clean
checkout match `90f3836dd7482db35c2c187a50999b99215919b0` (`v4.29.1-21-g90f3836`). Both the current
catalog and Parties pre-import pin select EventStore `3.113.0`. The I20 table
already approves EventStore's package/source selection; its dated Builds approval
still selects `360a2b9c4e96809365a7de785be9a68152d5ac28`. The later Builds identity
is observed only. This receipt changes no approval or consumption identity and
does not rewrite the 2026-10-05 Story 8.10 reconciliation above.

| Gate evidence | Current inspection | Disposition |
| --- | --- | --- |
| Owner contracts/core | Public policy/erasure hooks and internal v2 core exist; core is `IsPackable=false`. Owner 8.2 is `done`, 8.3 is `in-progress`. Existing owner contract/spec approvals are recorded. | Partial delivery; no approved consumable provider. |
| Compatibility, lifecycle, backend, runtime, release | Owner 8.4-8.8 remain `backlog`; AzureKeyVault project absent; neither payload package is catalog/release-enrolled; server defaults to `NoOpEventPayloadProtectionService`. | Missing runtime/release receipts. |
| Consumer parity and rollback | Owner 8.9/8.10 remain `backlog`; dual-provider GDPR/parity and post-v2 switch-back are unproven. | No adoption or deletion proof. |
| Availability and policy ownership | Owner 8.11 remains `backlog`; closure packet absent; G5 availability and I2/I19a hook-classification approvals are missing. | G5 remains `needs-additive-api`; 8.7 remains `blocked`. |
| Parties continuity | 8.6 is `done`; all 24 MOVE/KEEP/adapter files, local harness, and local DI remain. | Crypto-retention action stays `open`; accepted Epic closure deferrals do not complete 8.7. |

All 32 recorded G5 static validation commands passed, as did retained-file/DI and
matching-clean-identity assertions. Exact commands/results are in
`tests/test-summary.md`. No product tests, dual-provider/GDPR parity, or post-v2
rollback were run or credited. Production KMS remains a separate release gate.
The frozen 8.7 identity is historical and requires reconciliation at activation;
no production, dependency, submodule, frozen-spec, or approval-table edit occurred.

### Historical Story 8.7 G5 gate revalidation — 2026-10-03

At Parties baseline `06714c166c090200ac87373b11d8243aa11b2126`, EventStore's root gitlink and
clean checkout both equal `2c58ffda41759e895ace4b9625c9bd931a217672` (`v3.111.0`). Builds' root
gitlink and checkout both equal `688eec9a4333245cc0ff7772115c769094471863`; its imported catalog
selects `HexalithEventStoreVersion=3.110.0`. These are observed identities,
not approval to adopt a new G5 dependency. The locked Story 8.7 specification
still names `c21bd749154d701c3b7d68e40d1008d3475e35c4` / `3.95.0`.

This receipt supersedes older G5 claims that the contracts, v2 source, and
PayloadProtection project are absent. It does not refresh unrelated Story 8.10
consumption approvals or promote the G5 row.

| Gate evidence | Current inspection | Disposition |
| --- | --- | --- |
| Policy and erasure extension seams | Public `IPersonalDataPolicy` and `IErasureStateProvider` now exist in EventStore.Contracts/Security. EventStore Story 8.2 is `done`. | Additive contract delivery only. |
| v2 implementation | `Hexalith.EventStore.PayloadProtection.csproj` exists with `IsPackable=false`; `PayloadProtectionCore`, its context, and its `json+pdenc-v2` wire format are internal. EventStore Story 8.3 is `in-progress`. | Partial owner implementation, not a consumable approved provider. |
| Compatibility and key/backend integration | EventStore Stories 8.4-8.8 remain `backlog`; the AzureKeyVault project is absent. Neither PayloadProtection package is enrolled in the imported catalog or owner release inventory. Server DI still defaults to `NoOpEventPayloadProtectionService`. | Runtime/release delivery incomplete. |
| Consumer parity, post-v2 rollback, and closure | EventStore Stories 8.9-8.11 remain `backlog`; `8-11-g5-evidence-and-approval-closure.md` is absent. No named G5 `available` approval is recorded. | G5 stays `needs-additive-api`; Parties Story 8.7 stays `blocked`. |
| Sequence and retained rollback | Parties Story 8.6 is `done`; all 18 MOVE files, 5 KEEP files, the adapter, and local DI remain intact. | Sequence prerequisite satisfied; G5 is not waived. |

Static inventory inspection passed. Dual-provider, GDPR, topology, and post-v2
rollback tests were not run and are not credited. Production KMS remains a
separate release prerequisite. Exact inspection commands and results are in
`tests/test-summary.md` under the 2026-10-03 Story 8.7 receipt. No production,
DI, package, or root-submodule pointer changes were made.



## EventStore Owner Routing — 2026-07-11

Routed to Hexalith.EventStore owners through
`_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-11-eventstore-additive-api-prerequisite-routing.md`.

1. **Projection/query parity — Story 8.6**
   - G3 additive read-model erasure hooks.
   - G10 additive index batching with idempotent write-policy parity.
   - G6 freshness mapping preserving Parties states and last-known fallback.
   - Owner approval, release or root-submodule pin, parity results, and rollback proof required.

2. **Shared payload-protection engine — Story 8.7**
   - G5 additive shared engine package.
   - `pdenc-v2` writes with AAD binding.
   - `json+pdenc-v1` read compatibility.
   - `IPersonalDataPolicy` and `IErasureStateProvider`.
   - Moved golden compatibility harness covering protected, redacted, legacy,
     typed-unreadable, no-leak, export, processing-record, certificate, and rollback behavior.

3. **Client envelope/freshness/error adapter — Stories 8.6 and 8.8**
   - G6 additive adapter preserving typed command/query outcomes, freshness states,
     warning/error codes, and bounded ProblemDetails reason mapping.

4. **Degraded response and DAPR health checks — Story 8.8**
   - G1/G2 additive degraded-response parity.
   - `AddEventStoreDaprHealthChecks` with status/header and health-tag policy parity.

All affected rows remain `needs-additive-api`. Routing is not delivery or approval.
No Parties projection/query, security, client, middleware, or health-check implementation
may be deleted until the corresponding row records named owner approval, exact release
or root-submodule pin, green parity evidence, and tested rollback.

## G8 Aspire and AppHost Owner Routing — 2026-07-16

Approved and routed through
`_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-g8-aspire-publish-helper-routing.md`.

1. **Audience-aware security — Hexalith.EventStore.Aspire**
   - Deliver `WithEventStoreJwtAuthentication(audience)` or owner-approved
     documentation/tests proving `WithJwtBearerSecurity(..., audience)` is the
     canonical replacement for local and publish/external-orchestrator settings.
   - Prove authority, issuer, workload plus EventStore audiences, HTTPS metadata,
     signing-key clearing, and no-secret publish output.
2. **Granular transport registration — Hexalith.EventStore.Client**
   - Deliver independently selectable command/query/GDPR transport registration.
   - Prove Parties and FrontComposer module-typed clients coexist with deterministic
     handler ordering and repeat-registration behavior.
3. **Integrated topology — Hexalith.FrontComposer.AppHost / approved platform host**
   - FrontComposer is the target owner, not current parity evidence.
   - Close or explicitly disposition the current gaps for `parties-mcp`, standalone
     `parties-ui` versus `frontcomposer-ui`, and Docker/Kubernetes/ACA publish-target
     selection.
   - Record an exact FrontComposer source commit/root gitlink or approved host-artifact
     identity and the exact EventStore.Aspire identity it consumes.
4. **Runtime deployment — external platform operations**
   - Parties retains workload source, CI, and immutable image publication.
   - Production Dapr, ingress, secrets, apply, signing/scanning, and promotion remain
     outside this repository.

Revalidation after concurrent repository synchronization found no delivered G8
closure. The root-declared EventStore gitlink remains
`82ed167c1c78d4ff50d3f8eab43850bb6abd0fe7`; the concurrently checked-out
`ef541f9aae9d843cd1697fe018e954941f36a723` (`v3.67.1`) still exposes
`WithJwtBearerSecurity` and the generic `AddEventStoreGatewayClient`, but no
`WithEventStoreJwtAuthentication` or granular Parties/FrontComposer module-typed
registration surface. The root-declared and checked-out FrontComposer identity
`32db5c3460a4aa0ae6382ae9db36fa42e512ffd3` composes EventStore, Tenants,
Parties, and `frontcomposer-ui`, but not `parties-mcp` or standalone `parties-ui`,
and it has no Parties-equivalent `PUBLISH_TARGET` selection.

The row therefore remains `needs-additive-api`. The current Parties AppHost and
typed-client registrations remain the rollback path; routing and ownership approval
do not authorize deletion.

## G8-A EventStore.Aspire Owner Delivery Receipt — 2026-08-01

The preceding routing revalidation is the historical baseline. Package A now has a
local owner-source delivery, approved by Administrator for implementation and local
commit on 2026-08-01 at 08:50:32+02:00. That approval did not authorize a push,
release, Parties gitlink update, consumer adoption, or a G8 row transition.
Administrator separately authorized the EventStore push on 2026-08-01; remote
publication completed at 09:49:12+02:00 without authorizing any remaining action.

### Exact identity and public API inventory

- Producer base: Hexalith.EventStore
  `9b9c776791c149cab26c795a476d23d3d11f7796`.
- Producer delivery: Hexalith.EventStore
  `e7cf91fa714b780d60eb129722f4ab82fc7b0b26`, local `main`, commit
  `feat(aspire): add mode-aware JWT security`; it is reachable from remote `main`
  integration commit `a7af77ecf141278d891b3ef64b03c0da7d9dab74`. No release was performed.
- Consumer/rollback identity: Parties
  `8c7b4e6ddb2bce9a0d3041cc2e8f3d6153e45c3c` still selects the root-declared
  EventStore gitlink `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`.
  The shared working checkout is intentionally at the producer commit to create and
  inspect the delivery, but it also contains explicitly excluded uncommitted
  EventStore.Client and DomainService work. Exact-commit verification therefore used
  a clean detached worktree; neither checkout is root-submodule-pin consumption proof.
- New surface: `HexalithEventStoreJwtAuthenticationOptions` exposes only
  `PrimaryAudience`, `ValidAudiences`, `ExternalAuthority`, and `ExternalIssuer`;
  `WithEventStoreJwtAuthentication(IResourceBuilder<ProjectResource>,
  HexalithEventStoreSecurityResources?, HexalithEventStoreJwtAuthenticationOptions)`
  selects the run or publish application model. The existing
  `WithJwtBearerSecurity` signature and local behavior remain available.

### Verified delivery file inventory — 2026-10-21

The historical delivery object's parent is the recorded producer base. Its complete
changed-file inventory is below; Parties fitness checks bind these paths, the
recorded consumer gitlink, integration ancestry, and the focused 26-case test lane
to the immutable Git objects. This is provenance verification, not a new producer
runtime run, package publication, adoption approval, or parity receipt.

```text
docs/reference/nuget-packages.md
src/Hexalith.EventStore.Aspire/HexalithEventStoreJwtAuthenticationOptions.cs
src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs
tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreJwtAuthenticationTests.cs
```

### Producer evidence

| Evidence | Exact command or inspection | Result |
| --- | --- | --- |
| Shared-workspace focused source-mode build | `dotnet build tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj --no-restore -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false --disable-build-servers -m:1 --verbosity:minimal` | Passed; 0 warnings, 0 errors. This verifies source-graph compatibility but is not exact-commit isolation because the shared EventStore checkout contains the excluded concurrent work listed above. |
| Shared-workspace broad source-mode build | `dotnet build Hexalith.EventStore.slnx -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false --disable-build-servers -m:1 --verbosity:minimal` | Passed; 0 warnings, 0 errors across the solution. This is supplementary source-graph evidence, not clean producer identity proof. |
| Clean producer identity | `git worktree add --detach /home/administrator/projects/hexalith/parties/references/.g8a-eventstore-review-e7cf91fa e7cf91fa714b780d60eb129722f4ab82fc7b0b26`; `git status --short --branch` | Detached exact producer commit with no tracked or untracked changes. The temporary worktree was removed after validation. |
| Clean package-dependency build | From the clean worktree: `dotnet restore tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:NuGetAudit=false --disable-build-servers --verbosity:minimal`; then `dotnet build tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj --no-restore -c Debug -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true --disable-build-servers -m:1 --verbosity:minimal` | Passed at exact producer commit; 0 warnings, 0 errors. All EventStore outputs resolved under the clean worktree. |
| JWT application-model tests | From the clean worktree: `dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Debug/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class Hexalith.EventStore.AppHost.Tests.Configuration.HexalithEventStoreJwtAuthenticationTests` | Passed 26/26; 0 errors, failures, skips, or not-run tests. The cases cover local Keycloak authority/issuer/reference/wait behavior, external publish authority/issuer with a supplied-but-ignored local-security handle and no project dependency, ordered de-duplicated workload plus EventStore audiences, HTTPS metadata, empty signing key, query/fragment/user-info rejection, invalid-input non-mutation, repeat-application rejection before second mutation, prohibited credential-field inspection, and legacy-helper compatibility. |
| Diff and commit policy | `git diff --check`; `printf '%s\n' 'feat(aspire): add mode-aware JWT security' \| npx commitlint --verbose`; `npx commitlint --last --verbose`; `git diff-tree --no-commit-id --name-status -r e7cf91fa714b780d60eb129722f4ab82fc7b0b26` | Passed. The commit contains only the options type, Aspire security extension, focused AppHost tests, and package reference documentation. |
| Remote publication | After a non-fast-forward rejection exposed concurrent upstream commits, an isolated clean branch merged `f8e121a46f4c4f0a5283659a22dbd208e22c7c8e` with owner commit `e7cf91fa714b780d60eb129722f4ab82fc7b0b26`; `npx commitlint --last --verbose`; clean package-dependency build and focused test commands above; `git push origin HEAD:main`; `git ls-remote origin refs/heads/main` | Merge commit `a7af77ecf141278d891b3ef64b03c0da7d9dab74` passed commitlint, 0-warning/0-error build, and 26/26 tests, then reached remote `main` without force. GitHub reported that the direct push bypassed the pull-request rule and seven expected status checks; no release, root-gitlink change, or consumer adoption followed. |

### Consumer, publish, and rollback disposition

No Parties or FrontComposer consumer has selected the producer delivery. Runtime
binding and enforcement of the emitted `TokenValidationParameters__ValidAudiences`
entries, a real token acceptance/rejection matrix, an actual owner-AppHost publish
artifact poison scan, package publication, granular EventStore.Client registration,
Parties/FrontComposer typed-client coexistence, integrated platform AppHost topology,
and external platform-operations deployment remain unproved and separately routed.

Rollback before consumer adoption is the unchanged Parties root gitlink
`fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`; no Parties source switch is required.
For the pushed EventStore owner commit, the non-destructive rollback is a reviewed
`git revert e7cf91fa714b780d60eb129722f4ab82fc7b0b26` on the updated remote history,
followed by the focused build and tests above; forward restoration is a revert of that
rollback commit or a new reviewed adoption commit. Switch-back execution is
intentionally pending because no consumer was changed.

Package A therefore has exact producer proof but not a releasable/selected consumer
identity or end-to-end parity. The matrix row remains `needs-additive-api`, Story 8.8
remains blocked, and the current Parties AppHost and typed-client registrations remain
the rollback path until Packages B and C plus consumer, publish, operations, and
exercised rollback evidence are accepted.

## G7/G9 Owner API Delivery Receipt — 2026-07-31

The delivery course correction was approved by Administrator on 2026-07-31 at
23:33:05+02:00:
`_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-31-g7-g9-owner-api-delivery.md`.
This approval defines the delivery gate; it is not platform-owner API approval,
release approval, consumer parity, or deletion authorization.

**Receipt status:** incomplete. The tenant-claims row remains
`needs-additive-api`; deferred Story 8.4 Authentication deletion and the G7/G9
slice of Story 8.8 remain blocked.

### Revalidated baseline — not delivery proof

| Dependency mode | Exact identity | Result |
| --- | --- | --- |
| EventStore root gitlink and matching clean checkout | `9b9c776791c149cab26c795a476d23d3d11f7796` selected by Parties `6f572e37b7520a259947075eebd520f2dd3ed721` | Missing public `EventStoreClaimTypes.Tenant`, `AggregateIdentity.IsValid(string)`, and a separately packable `Hexalith.EventStore.Authentication`; cannot be selected as the delivery receipt. |
| EventStore package mode | `Hexalith.EventStore.Contracts` `3.86.0`; no selected `Hexalith.EventStore.Authentication` package | Required public/package surfaces are not available; cannot be selected as the delivery receipt. |
| Commons root gitlink and matching clean checkout | `f2b5f1b12b478dce902756876138a60cde4fde65` selected by Parties `6f572e37b7520a259947075eebd520f2dd3ed721` | Missing `UniqueIdHelper.IsValidUlid(string)`; cannot be selected as the delivery receipt. |
| Commons package mode | `Hexalith.Commons.UniqueIds` `2.29.0` | Required predicate is not available; cannot be selected as the delivery receipt. |
| Parties retained rollback source | `Hexalith.Parties.Authentication` and its focused tests at Parties `6f572e37b7520a259947075eebd520f2dd3ed721`; governed files are clean relative to that commit | Retained and buildable pending adoption, parity, and exercised switch-back. |

### Named approval ledger

A role or persona label alone is not an approval. Each completed field must record
the actual approver name or accountable handle, role, timestamp with UTC offset,
review/decision reference, and accepted identity/scope.

| Approval | Status | Required record before row transition |
| --- | --- | --- |
| EventStore API boundary and behavior | Pending | Named API owner acceptance of the public claim constant, aggregate-ID predicate, lightweight transformer, Gateway composition, dependency boundary, tests, and exact producer identity. |
| EventStore release or root pin | Pending | Named release/root-pin owner acceptance of coherent released packages or an exact root-declared EventStore gitlink plus selecting Parties parent commit. |
| Commons API and release or root pin | Pending | Named Commons API/release owner acceptance of the strict non-throwing ULID predicate, tests, and coherent released package or exact root gitlink. |
| Parties consumer adoption | Pending | Named Parties owner acceptance of exact selected identities, compatibility alias, supported dependency modes, host/UI composition, and parity evidence. |
| Independent test and rollback | Pending | Named test owner acceptance of producer-consumer results, negative/boundary coverage, switch-back, and forward restore. |

Administrator's approval of the course correction is recorded above and does not
substitute for any pending platform, release, consumer, or test approval.

### Exact delivery identity ledger

No delivery identity is selected yet. Record one coherent path for each owner;
never combine source observed at one commit with packages built from another.

| Owner | Released-package path | Root-gitlink path | Selected identity |
| --- | --- | --- | --- |
| EventStore | Exact versions, feed/source, and available hashes for both `Hexalith.EventStore.Contracts` and `Hexalith.EventStore.Authentication`; package/API inventory must contain the public constant, predicate, and transformer. | Exact 40-character EventStore gitlink plus the exact Parties parent commit selecting it; checkout must match and contain all three public surfaces and the packable project. | Pending |
| Commons | Exact `Hexalith.Commons.UniqueIds` version, feed/source, available hash, and API inventory containing `UniqueIdHelper.IsValidUlid(string)`. | Exact 40-character Commons gitlink plus the exact Parties parent commit selecting it; checkout must match and contain the public predicate. | Pending |

If a root-gitlink delivery supports only source mode, package-mode adoption and
deletion remain blocked unless source-only CI/release consumption is separately
approved and recorded.

### Producer-consumer parity ledger

All entries are pending until exact commands, results, test counts, artifact paths,
and producer/consumer identities are recorded.

| Evidence group | Status | Minimum evidence |
| --- | --- | --- |
| EventStore producer | Pending | Public/package API inventory; dependency fitness; safe bounded logs; canonical, `tenants`, `tenant_id`, `tid`, mixed, multiple, duplicate, blank, malformed, missing, and repeated-transform cases; Gateway composition; aggregate-ID predicate/constructor parity for semantic, ULID-shaped, GUID-shaped, length, character, and null/blank boundaries. |
| Commons producer | Pending | Public/package API inventory; valid upper/lowercase ULIDs; first-character range; excluded Crockford characters; length, null/blank, non-ASCII, regex-shaped parser-invalid boundaries; unchanged generation/conversion behavior. |
| Parties consumer | Pending | Shared-versus-local truth table; domain-service host transient and UI host scoped composition/order; authorization outcomes; no sensitive logs; compatibility alias; correct separation of permissive aggregate/Party semantic validation from strict ULID validation; every supported source/package build and package/API lane. |

### Rollout and rollback ledger

1. **Adopt without deletion — pending.** Select the approved identities and switch
   both Parties hosts through a reversible selector or compatibility adapter while
   keeping `Hexalith.Parties.Authentication` and its focused tests buildable.
2. **Forward parity — pending.** Run and record producer-consumer, authorization,
   identifier, package/API, and supported source/package evidence.
3. **Switch-back — pending.** Run both hosts on the shared path, switch both to the
   retained local path, rerun focused authentication/authorization checks, restore
   the shared path, and rerun them. A theoretical revert is not execution proof.
4. **Retirement rollback — pending.** Record the exact later retirement commit,
   trigger conditions, non-destructive `git revert <retirement-commit>` procedure,
   dependency re-pin/restore commands, results, and forward-restore procedure.
5. **Delete separately — blocked.** Only a complete receipt and named independent
   acceptance authorize an isolated removal of implementation, tests,
   registrations, solution/project references, CI/scripts, and package inventory.

The row may change to `available` only when every approval, exact identity,
producer-consumer, and rollback entry above is complete. That transition unblocks
only the G7/G9 authentication slice; all other Story 8.8 prerequisites remain.

## Residual Blockers Preserved

### Live EventStore identity refresh (2026-08-04, superseded same day)

The live EventStore checkout moved to `7854f8e51ce9b852bb6c3cac6012670122e93792`
(`v3.90.0`) earlier on 2026-08-04, then to
`1d6e9321acfc416768c1c78e9facf573c9c41f71` (`v3.91.0`) later the same day under
`sprint-change-proposal-2026-08-04.md` (approved). The current-identity receipt
in the payload-protection (G5) row and the two Story 8.6 rows above now reflects
`v3.91.0`; the row's historical `v3.89.0` receipts remain unchanged. Evidence:
`git -C references/Hexalith.EventStore rev-parse <2026-08-04-HEAD>` -> `1d6e9321acfc416768c1c78e9facf573c9c41f71`;
`git -C references/Hexalith.EventStore describe --tags --exact-match <2026-08-04-HEAD>` -> `v3.91.0`.

- Full `Hexalith.Parties.Tests` Release source-mode remains blocked by the Story 8.1 `Hexalith.Memories` Release guard: `Release builds must use NuGet package references for external Hexalith libraries. Remove -p:UseHexalithProjectReferences=true or build Debug for source-debugging.`
- Full `Hexalith.Parties.Tests` Debug source-mode still has the five pre-existing tenant-event failures recorded by Story 8.1 and Story 8.2.
- The G12 package-mode blocker is resolved as of 2026-07-11. Package-only restore and Release build evidence for the four required packages is recorded in `_bmad-output/implementation-artifacts/tests/test-summary.md`.
- Production KMS remains a release prerequisite before regulated EU personal data can be processed.
- Epic 8 deletion-heavy migrations remain gated by platform owner proof and the Story 8.3 no-migration decision above.

## Review Gate

Later Epic 8 stories may only start source migration for a row when owner evidence, release or submodule-pin proof, rollback instructions, and validation results are recorded in this matrix row before the migration starts. A consuming story may add supplementary proof, but it must not replace the row update. A row status of `available` means source evidence exists; it is not enough by itself. A checked-out submodule source file is not sufficient by itself.

### Working source observations during the EventStore 3.112.0 update — 2026-10-04

**Approved 2026-10-04 (working-tree decision).** After this observation, the
Administrator / jpiquot chose "approve working tree" for exactly the five
"Existing working checkout" identities below; they are recorded in the I20
five-pin row, `.gitlink-signoff.tsv`, the reconciliation table, and the test
constants. The "Committed gitlink" column was never approved. Approval is
source-pin selection only: no parity, owner release, publishing, commit, or
rollback deletion. The observation text below is preserved as dated history.

**Outcome, 2026-10-05.** Commit `2f6157d330139e38d031c9a3d1925bca5c317ff7` committed the
approved Builds `145ae921`, FrontComposer `2cc8dd3a`, and Memories `5b43fe2f` but recorded
the never-approved EventStore `547c938d` and Tenants `b55f96d8`; `2bdb303b` then moved Builds
to `360a2b9c`, EventStore to `0dc44e46` (never approved), and Tenants to `72b8e4f5`. The
Administrator's 2026-10-05 decisions (I20 rows above) approve Builds `360a2b9c`, Tenants
`72b8e4f5`, and EventStore `v3.113.0` (`865cd9e4`); the 2026-10-04 EventStore `2242ad55`
and Tenants `04e655cf` approvals were never committed and are superseded.

Observed at Parties `7c720c7f9879ecb469993b1d2dff2bdb72b96e1b` before this update.
These existing user checkout advances are preserved. No staging, Git commit,
source reset, or nested-submodule update occurred. The package approval above
selects 3.112.0; it does not certify these source identities or their parity.

| Root dependency | Committed gitlink | Existing working checkout |
| --- | --- | --- |
| Builds | `688eec9a4333245cc0ff7772115c769094471863` | `145ae921d9032f110b7371614939559e0aee202a` |
| EventStore | `e968467c7db5b5685894fa2b85cfa7ec14512a7c` | `2242ad55a1b678828df8aa093fd92399c29af5bf` (`v3.112.0-2-g2242ad55`) |
| FrontComposer | `37c8c6d295032968edba347940c41614ce8f2573` | `2cc8dd3a3ac76c03f5ea6f6f92e65829306db470` |
| Memories | `47027d35a1f4a2c6986bec85b5cae601ce1b201e` | `5b43fe2f8a0f04dc021921a077dff1a573c2ce5e` |
| Tenants | `c05fe3171b2d3f95e0dd0970edfa6c421bed2f19` | `04e655cf070b17eced9daefb9eaa87a09ec60e81` |

The committed-tree and clean matching-checkout guards remain unchanged. Any
identity failure caused by this drift remains owner-visible; package build
results are recorded separately in `tests/test-summary.md`.

## 2026-10-22 published routing observation — DW-139

This is current package-routing proof at Parties baseline
`d55058ddaa4bfc94c47b799e5f5d9e9eff8dce77`, appended to the identity and
FrontComposer G4 observations above. Earlier source identities, receipts and
owner approvals retain their original scope.

| Scope | Current observation | Proof and disposition |
| --- | --- | --- |
| Package and source selection | The evaluated Release graph already selects `Hexalith.FrontComposer.Shell` 4.7.0 and EventStore 3.126.1. Committed Builds is `3fcb8f38919bb0961b06a19b57aa45623b35a33e`; FrontComposer is `a258b992d08e73e2d3251c865ef01a982eba31ac`. | Published API inspection confirms `FrontComposerRouteOptions.ReservedSegments`, `FcModuleLandingPage`, and `FcPageTabs.ModuleRoute` / `DefaultTabId`. No package selection or gitlink is changed or newly approved. |
| Package routing slice | Reserved segments `admin`, `me`, `no-party-binding` are configured without a source define. The obsolete `HFC_ROUTE_OPTIONS` gate and dictionary are removed; Parties overview passes its module/default-tab parameters directly. | Standalone UI Release/package build has zero warnings/errors. Actual-host checks reject reserved-name module collisions, discover generic module and Parties endpoints, and serve registered fixture module alias/canonical landings. bUnit verifies Parties alias/canonical/default-tab behavior and unknown-tab fallback. |
| Authorization | The existing role policies and authorized overview destinations remain enforced. | Host policies cover Admin, TenantOwner, Consumer and unrelated roles. Wrong-role HTTP requests to admin, profile, privacy and no-binding areas render the forbidden page inside the Blazor SSR shell. Final focused routing suite passes 46/46; final full UI suite passes 412/412, including protected-content/read-capture checks and actual explicit, registered generic and unavailable route selection. |
| G4 and topology boundary | DW-139 is complete for published-package routing. | G4 remains `needs-additive-api`; this receipt does not close the other primitive packages, focus/accessibility deferrals, owner signoffs or topology prerequisites. The pinned source launcher fails HXP020 because the mapped Parties project is unavailable; no resources started. Package checks do not certify Debug/source or integrated topology. |

Commands, selected identities, exact outcomes and limitations:
`tests/five-identity-routing-deferred-items-2026-10-22/validation.json` and its
adjacent API, build, routing and startup receipts. Fresh dependency-reconciliation
fitness checks pass 2/2; no historical parity is recertified.


## FrontComposer B–F maintenance qualification — 2026-10-27

This amendment covers DW-91, DW-100, DW-101, DW-102 and DW-112 only.
G4 stays `needs-additive-api`; the G4-A picker spec, ordered prerequisites and
Story 8.9 remain authoritative. Source candidates use
`Parties:FrontComposer:UseSharedUiPrimitives=true` only after tool-owned source
selection; the option defaults to false. Named owner acceptance and producer /
consumer parity are still required before production migration. Published
Shell and Contracts.UI 4.7.0 retain the local adapters and existing package gates.

| Slice | Candidate behavior | Remaining gate and rollback |
| --- | --- | --- |
| B/C — DW-100 | UI-normalized freshness and live regions; callback reconciliation preserves Parties labels, wire/status mappings and transport lifetimes. | Owner acceptance and matching publication; leave the option false to retain existing components/reconciliation. |
| D — DW-101 | Shared ephemeral stream/file/JSON download preserves original bytes, safe caller filenames and failure feedback. | Owner acceptance and matching publication; retained Admin/Consumer helpers remain available. |
| E — DW-102 | Optional ordinal typed-name mode retains guarded authorization, one submit, safe cancellation and accessible warning semantics. | Owner acceptance and matching publication; retained Admin dialog remains the package path. |
| F — DW-91 / DW-112 | Existing route/mounted-target fixes plus one-shot landmark focus prevent a late retry from stealing later focus. | Matching published Shell adoption; package negative controls remain enforced. |

The [batch receipt](tests/five-frontcomposer-ui-deferred-items-2026-10-27/README.md)
records exact revisions, executed binaries/assets, source mapping, public API
compatibility, failures and final checks. Concurrent external Git work advanced
reference identities during implementation; a HEAD alone is not a substitute for
its recorded source checksums. The narrow FrontComposer/Tenants metadata
conditions defer to the pinned mapper while retaining legacy selection outside
mapped mode. They do not bypass its validation guards.

## Published FrontComposer adoption — 2026-10-28

This amendment supersedes the 2026-10-27 publication/adoption gates only for
DW-91/100/101/102/112. The approved Shell and Contracts.UI packages are 4.7.1,
with nuspec producer revision `5688c3eba98567133285b0f51843d9daa8f24f0b`.
`Parties:FrontComposer:UseSharedUiPrimitives` remains default false. Approval
covers the named primitives and their proven package exits; it does not certify
readiness-capable reconciliation, G4-A, the whole G4 set or Story 8.9.

| Item | Qualified package disposition | Remaining boundary / rollback |
| --- | --- | --- |
| DW-91 | Closed: cold/hydrated and client-route forward Tab, heading announcement and target/onward keyboard flow pass without capability skips. | Shell package supplies the fix; source qualification remains independent. |
| DW-100 | Partial: shared record freshness and polite/assertive/none live regions preserve Parties text and mappings; real registration selects them. | Open: 4.7.1 lacks registration readiness. Registered package feedback still uses corrected local orchestration; fixed-reconciler publication/adoption needs its own approval/parity. Disable registration for local component rollback. |
| DW-101 | Closed: actual Admin/Consumer package transfers preserve bytes and safe caller names; repeated download, cancellation, ephemeral resource cleanup and existing error feedback pass. | Disable registration to retain the local helpers. No producer asset is substituted in package qualification. |
| DW-102 | Closed: actual shared ordinal typed-name dialog preserves guarded one-submit authorization, warning/ARIA, safe Cancel focus and Cancel/Escape return focus. | Disable registration to retain the local dialog. Parties owns restoring its recreated initiating control. |
| DW-112 | Closed: one mounted navigation target and matching bypass link across desktop/phone/tablet/resize; content bypass and announcement remain available. | Published Shell owns the responsive behavior; no aggregate readiness promotion. |

The [bounded receipt](tests/five-published-frontcomposer-adoption-2026-10-28/README.md)
records individual Debug builds with zero warnings/errors, 445 UI, 327 Admin,
85 Consumer and 68 browser passes without skips/retries, exact installed package
and copied assembly hashes, source identities and actually served asset hashes.
Initial strict restore refused NU1109 because the root EventStore 3.128.3 override
was below Tenants 5.11.1's 3.128.4 floor. The human separately approved the exact
one-line root alignment to 3.128.4; no other selection or Builds catalog edit was
made for this task. Fresh pinned-tool source launch refuses HXR015 (installed
Aspire CLI 13.6.4 versus catalog SDK 13.6.1). No mapping/dependency change bypasses
that prerequisite, and package parity supplies no source-mode qualification.
