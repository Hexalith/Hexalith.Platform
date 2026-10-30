### Successful bootstrap never verifies the owner genesis it installs

- **Changed surface:** `deploy/erasure-root/bootstrap.py:101` constructs native tenant heads and serializes the enrolled owner genesis anchors when `--apply` is supplied.
- **Impacted consumer or site:** `ErasureRootTransitionAuthority.ValidateStateAsync` at `src/Hexalith.Platform.Protection/ErasureRootTransitionAuthority.cs:37` requires those exact scope/revision/digest values before occurrence, deletion, or lifecycle owners can advance.
- **Existing test evidence:**
  - `Regression gap`: `eng/ext-protection-1/test_bootstrap.py:133` invokes `--apply` only with invalid enrollment, asserting refusal before transport.
  - The successful CLI test at `eng/ext-protection-1/test_bootstrap.py:151` omits `--apply` and asserts zero requests.
  - Native tests bootstrap through separate C# code at `tests/Hexalith.Platform.Protection.Tests/OpenBaoNativeCustodyFixture.cs:33`; they never execute the shipped Python bootstrap.
  - Repository-wide Python/C# symbol and import-reference searches for `bootstrap.py`, `erc_bootstrap`, `bootstrap.main`, and `BootstrappedNotQualified` found these tests, their identical reference-checkout copy, and unrelated `bootstrap-runtime` tests.
- **Missing verification:** A successful execution of the shipped `--apply` path followed by runtime validation of every installed owner genesis.
- **Demonstration:** Replace the serialized `Anchors` value at `bootstrap.py:103` with `{}`, while retaining enrollment validation. None of the tests above would fail: negative cases stop before serialization, the default CLI performs no writes, and native tests create their own heads.
- **Consequence:** Bootstrap could report `BootstrappedNotQualified` successfully while installing heads that prevent all three anchored owners from accepting their initial state.
- **Disposition:** `patch` — add a successful shipped-bootstrap case to the owned disposable verification path, then validate the resulting genesis through `ErasureRootTransitionAuthority` for occurrence, deletion, and lifecycle scopes.
