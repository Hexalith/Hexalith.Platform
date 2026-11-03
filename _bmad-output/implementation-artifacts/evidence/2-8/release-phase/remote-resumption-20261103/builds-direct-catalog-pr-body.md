Package-mode native CI in a module checkout with only direct dependencies fails before allocation: declaration loading looks for the Builds catalog inside Platform's uninitialized nested checkout. Select the module root's direct catalog when its resolved Platform identity identifies a consuming module. The existing resolver still verifies the module/Platform catalog pin, checkout and clean state; Platform-root callers and existing defaults retain their catalog path. Public signatures, release authorities, snapshots, budgets and cleanup behavior are unchanged.

Add real Git workspace regressions that resolve the mapping before cold declaration loading, with authentic released EventStore and Memories fixtures checked against production release hashes. No release-authority override dictionary or nested module initialization is used. Cover the direct-only layout, Platform root, dirty/missing/wrong catalog and dirty Platform refusals, changed snapshot hash and unknown release version.

Validation:
- Fresh owning Release build: zero warnings/errors.
- 83 focused tests pass with zero failures/skips: 8 direct-layout/default-authority cases, 23 declaration-closure cases, 9 released-metadata cases and 43 identity cases.
- Before the fix, the new lane records 6 passes/2 failures, reproducing the missing nested catalog; the published 4.43.4 public caller also reproduces HXP020 on the actual unchanged Parties checkout.
- Fresh signed pinned 4.43.4 preparation followed by LoadVerified and the source-built default declaration boundary loads EventStore, Tenants, Memories and Parties from the actual root-only layout before composition workspace materialization. Loaded Tooling bytes match the test/CLI/project outputs. This is metadata-only source diagnostic evidence.
- An additional source-built public execution diagnostic remains unqualified: it returns HXT007 with Aspire deliberately absent and creates no state/workspace. A standalone source Tooling 1.0.0 probe correctly refuses the genuine Platform tool range; that gate is unchanged. Both first results are retained.
- Exact commit/title and fixture commit text pass locked commitlint 21.2.4 under Node 24.22.1/npm 11.19.1.

Normal hosted checks, fresh qualified publication, official package content verification and coherent consumer adoption remain required. No published runtime, lifecycle cleanup or candidate acceptance is claimed. The existing source89 publication and Parties PR59 baseline are preserved.

