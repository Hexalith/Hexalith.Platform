Extension composition reads the actual Builds-materialized
`Hexalith.Platform.EventStoreHost` snapshot view. Plain Debug builds now carry
source mode and the selected catalog SDK through direct and SDK-promoted edges.
Standard static graph restore preserves that context for project and solution
entry points. Explicit global/per-edge choices, package mode and authenticated
source mappings retain their authority.

The owners remain signed Builds 4.47.0/5ec and EventStore 4.2.0/AA. The prepared
fixture publisher requires an exact qualified source release, immutable
producer/declaration/native-test bytes and normal NuGet Trusted Publishing. This
PR grants no signed-fixture or runtime admission; actual publication, a released
Builds host-consumer pin and finite producer/host qualification remain required.

Validation of the source-default repair:

- All 25 real MSBuild metadata/restore controls pass, including the precise
  package-restored hosted command shape, explicit edge overrides and both entry
  points.
- Cold plain Debug project and full solution builds each pass with zero warnings
  or errors, separate empty private caches and absent outputs, using only the
  verified tool path. Actual DomainService DLLs are 4.2.0.0/AA and .deps identify
  project SDK 4.2. No source, SDK, restore-engine or version override was supplied.
- All 48 source extension/companion cases pass with unchanged full inputs and
  output snapshots. All 48 replay/binding Python controls, 29 automatic
  source-release controls and all four actionlint checks pass.

The fresh linear sources are L a64a9ea4028a842d73cae8831303664f1620fc9c →
N 044e94ae08c29bdc2c7a133e5b2c223ac4298aed →
M 9ea927c0733a6ac4acdfd25dc6e513b1c5e7be4f. Actual checkout source comparisons
pass; L binds exact Parties BB/40cee, N/M exact EBE/d9c. All nonmetadata trees
match. Prior frozen refs and every failed/intermediate observation remain intact.

Fresh 349/412 local inventories, full plain-default Debug broad observation and
normal hosted five-job tuple/six-job catalog gates are running. The current lane
must retain 456+97+9=562 executions covering 559 unique identities, with three
explicit duplicate methods. The prior frozen head's complete hosted gates passed
but cannot qualify this repair. Keep this PR draft until current gates and
regression attribution close; preserve merge ancestry when landing. No
production deployment or real module-manifest adoption is included.
