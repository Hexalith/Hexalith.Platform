# EXT-PROTECTION-1 loop 8 handback

Status remains `in-progress`. This record does not accept a target, profile, date, or availability. Loop 7 candidates stay withdrawn. Attempt 01 remains BlockedUnknown and attempt 02 remains the historical native label for its own graph. R7-BH-7, the generic unbounded subprocess capture, is unchanged.

The native five-case workload was not run again. `OpenBaoErasureRootIntegrationTests` still constructs custody without installed-owner requirements and calls `WrapAsync`, not the new preflight encrypt. This is not a new native pass.

## Corrections

- R8-BH-2: `PreflightWrapAsync` now reads native policy and epoch, then encrypts a throwaway 32-byte buffer at the head epoch. A missing transit key or an encrypt failure returns before `RegisterOriginalAsync` writes an independent source or a head CAS. Metadata that is still readable is not enough.
- R8-BH-3: production host composition requires the deletion, occurrence, and lifecycle owner scopes for every installed tenant. `ReadAsync` returns null when one of those required scopes is absent from the head and its index. It does not create the anchor. Standalone custody with an empty requirement still wraps. The engine fixture requires only the registry scope it actually enrolls.
- R8-BH-8: `Role.request` passes `EXT_PROTECTION_ACCEPTED_ROLE_CONFIGURATION_SHA256`. `RunAsync` compares that digest to the role JSON before any HTTP call. `InvokeAsync` verifies a signed endpoint binding with the existing public anchor before it reads or sends the bearer.
- R8-BH-9: a frozen runtime manifest must name every regular file in its directory except itself. An unlisted assembly refuses with `FrozenRuntimeClosureIncomplete` before image inspect and before `frozenRuntimeManifestSha256` is recorded.
- R8-G1: executed producer originals use the seven-field schema that includes `SafeRequest`, through `require_executed_operation_producer`.
- R8-G4: `DeadlineAnchoredStateAuthority` forwards `ReadAdmittedOriginalAsync` under the existing deadline, so reconciliation can recover an admitted original when no pending bytes were stored.
- R8-G5: profile credential validation accepts 8185 visible ASCII characters (`Bearer ` plus the value is 8192) and rejects 8186 characters, spaces, controls, and non-ASCII.
- R8-G6: `payload-protection.yml` runs the pinned `actions/setup-node` v7.0.0 and `npm ci` before the Python qualification tests.
- R8-G7: qualification builds use `references/Hexalith.Platform` when that Executor project is present, and the sibling `platform` checkout only when it is not.
- R8-G10: an owned HTTPS test drives `ProtectionQualificationRoleProgram.InvokeAsync` against `ProtectionQualificationRoleApplication` and checks the bearer, contract header, and signed correlation.

## Commands and results

Debug project-reference builds used `-nodeReuse:false`, `UseHexalithProjectReferences=true`, `GeneratePackageOnBuild=false`, `HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore`, `HexalithCommonsRoot=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Commons`, and `HexalithTenantsBasePath=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Tenants/src`. The process `HexalithCommonsRoot` was the same Commons path. Both builds reported 0 warnings and 0 errors.

Test DLLs were executed directly:

- `Hexalith.Platform.Protection.Tests.dll` for `ErasureRootLoop8AdmissionTests`, `ProtectionQualificationRoleProgramClientTests`, `ErasureRootLoop5ConfiguredSealTests`: 26 passed.
- The same DLL for `ErasureRootCustodyTests`, `ErasureRootHostCompositionTests`, `ProtectionQualificationLoop4EvidenceTests`: 30 passed.
- `ErasureRootLoop3TenantGraphTests` passed in the earlier 46-test run, whose only failure was the loop 5 observation case fixed before the 26-test rerun.
- `Hexalith.Platform.Custody.Tests.dll` method `DeadlineWrapperForwardsAdmittedOriginalAndReconcilesLossBeforePending`: 1 passed.

Python:

- `python3 -m unittest discover -s tools/tests -p 'test_ext_protection_1_loop8.py'`: 3 passed.
- `ExtProtectionPolicyTests.test_role_environment_contains_only_its_own_credential` and `CapabilityOriginalEvidenceTests.test_all_anchor_pins_are_rechecked_before_a_later_role_call`: passed.
- `python3 eng/ext-protection-1/test_probe_erc_native.py`: 6 passed, including the unlisted-assembly refusal.

## Still missing

Full installed qualification remains external. That includes the 41-case installed run, physical-copy qualification, A1 HTTPS/storage/native/genesis inputs, no-backup and all-copy restore and retirement, independently operated per-tenant authorities and usable trust, private grants, disjoint role credentials and signers, source/control/fault cells, a signed read-only observer permission with physical backing, and legacy transport allowances. The payload-protection `npm ci` step is in the workflow and was not executed here. The installation closure test's `setUpClass` was not run, because it builds the declared Platform submodule rather than this working tree. The child-environment assertion was updated for the configuration digest and covered by the resolver and offline role-request tests.
