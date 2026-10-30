### Successful bootstrap never verifies the owner genesis it installs

- Changed surface: deploy/erasure-root/bootstrap.py:101 constructs native tenant heads and serializes enrolled owner genesis anchors when --apply is supplied.
- Impacted consumer: ErasureRootTransitionAuthority.ValidateStateAsync:37 requires exact scope/revision/digest before occurrence, deletion or lifecycle advancement.
- Regression gap (pre-verified): test_bootstrap.py:133 calls --apply only with invalid enrollment; successful CLI test:151 omits --apply. Native tests use separate C# OpenBaoNativeCustodyFixture bootstrap. Searches for bootstrap.py/erc_bootstrap/bootstrap.main/BootstrappedNotQualified found only these tests, identical reference copy and unrelated bootstrap-runtime tests.
- Missing verification: successful shipped --apply followed by runtime validation of every installed owner genesis.
- Demonstration: replacing serialized Anchors at bootstrap.py:103 with {} leaves those tests green; negatives stop before serialization, default CLI writes nothing, native tests create their own heads.
- Consequence: BootstrappedNotQualified can report success with heads blocking all three anchored owners.
- Filed disposition: patch; add successful shipped-bootstrap case to owned disposable verification path and validate its occurrence/deletion/lifecycle genesis through ErasureRootTransitionAuthority.
