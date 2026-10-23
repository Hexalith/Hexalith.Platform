# Native client-certificate delivery evidence

`verification.json` records corrected-source offline verification: 297 passing
tests, zero skips, and disabled public activation. `command-results.json` and
seven adjacent logs preserve the exact commands and results. `sources/` contains
the four changed Python source/test snapshots; their SHA-256 values and unchanged
runtime/helper source hashes are in the receipt. `matrix-coverage.md` maps every
frozen scenario and acceptance criterion to checks.

`spec-baseline.md` is the original unmodified implementation spec.
`preservation.json` checks all three handoff prefixes, frozen intent, the parent
baseline, unchanged tracked historical evidence, common/S3/historical transport
sources and index. `diff-check.log` records `git diff --check`.
`parent-cancellation-audit.json` distinguishes the first candidate's two leaked
sealed descriptors from the corrected parent-observed zero-descriptor result.
The regression interrupts the first installation-cleanup line after real key
loading and preserves the primary KeyboardInterrupt. Initial candidate command
logs remain separately under `first-candidate/` and supply no corrected-source
qualification.

All material is unsigned fixture evidence. The client/server keys embedded in
the test fixture source are deliberately public synthetic material and confer no
authority. No actual credentials, configuration, endpoints or operational
certificates were accessed or created. No attempt/policy allocation, recovery
signing, upload, recovery or infrastructure mutation occurred. Actual
credential/authority/runtime, system/target/proof-consumer and recovery
qualification remain pending. Public operational gates stay closed; Story 4.27
stays in-progress, 4.1 closed and Timesheets 3.6 open/FAIL.
