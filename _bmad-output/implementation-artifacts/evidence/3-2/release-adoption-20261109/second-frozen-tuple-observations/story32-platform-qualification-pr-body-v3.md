Extension composition now reads the actual Builds-materialized
`Hexalith.Platform.EventStoreHost` snapshot view. Source references carry the
selected catalog SDK while retaining explicit per-reference selections and
existing authenticated mapping authority.

This preserves upstream's complete SDK4.2 inventory and finite admission rules.
The Platform-owned fixture publisher requires an exact qualified source release,
immutable producer/declaration/native-test bytes, and normal NuGet Trusted
Publishing. Its proof paths bind in runtime steps before use. This PR grants no
fixture runtime admission; signed publication and its subsequent finite host
cohort remain required.

Validation completed locally:

- All562 package executions passed across559 unique cases at merged C# source
  53dbd485: preserved456 plus97 additional controls and9 required extension cases.
  Three repeated methods are named in the receipt; source/output bodies stayed
  unchanged. Current C# code and these profiles remain unchanged by the CI fixes.
- All48 source extension/companion cases and9 actual MSBuild metadata controls
  passed. The unchanged actual Story2.8 fixture passed1/1; earlier failures stay
  separately retained.
- All20 finite legacy/checkout,9 pre-extension,19 tuple-binding and29 automatic
  source-release controls pass. Actual actionlint1.7.12 passes all four workflows.
- Actual signed owner sources remain EventStore4.2.0/AA and Builds4.47.0/5ec.

The fresh same-code sources are linear and reachable:
L91abcb2379f913d40174797a9af431821041c71d →
Na409d45f57d3f3e3876c2aa669aa307833aea094 →
M862fd14f17bf48432c76993afd668b5a067e887d.
Both actual checkout source-equivalence checks pass. L binds the original
PartiesBB/40cee declaration required by the retained349 controls; N/M bind
PartiesEBE/d9c. Every lane rechecks the actual initialized source and manifest.
Arbitrary, missing, reversed and older-provenance substitutions refuse.

The prior frozen refs and exact failed hosted/preflight results remain retained.
Both complete fresh349/412 local inventories pass with unchanged source and
output guards. All six catalog jobs pass; the five-job hosted tuple is pending. Keep this PR draft until all required results pass and
preserve merge ancestry when landing. No production deployment or real module
manifest adoption is included.
