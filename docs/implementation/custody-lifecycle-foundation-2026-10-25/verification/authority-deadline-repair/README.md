# Authority deadline and cancellation follow-up

Every lifecycle-authority operation now runs in the client's existing thirty-second operation budget. Owner forwards receive its shared token and bounded wait; registration verification uses the same budget for authenticated lookup; outcome verification bounds both the registration locator and lifecycle lookup with the original shared token/deadline. Internal expiry returns false. Explicit cancellation is normalized to the original caller token, including an already-cancelled task whose exception carries a foreign linked token. No public API or wire field was added.

The primary deployment README now supplies the closed `{runtimeImage, files}` inventory and matching actual digest-pinned `--runtime-image` argument. The real publish inventory still lacks its reviewed runtime-image pin and remains Blocked.

Targeted verification only:

- `runtime-build-3.log`: successful build, zero warnings/errors. `runtime-build-1.log` and `runtime-build-2.log` retain the initial xUnit1051 analyzer failures while adding intentional default-token regressions; the final tests narrowly suppress that rule only for those cases and retain cancellable outer test waits.
- `authority-cancellation-targeted-tests.log`: ten passing tests, including virtual-clock unresponsive owner/registration locator, slow registration followed by lifecycle lookup, refusing adapters and completed cancelled foreign-token tasks.
- `authorizer-contract-targeted-test.log`: the existing credential-bound exact value-free authorizer contract test passed.
- `scoped-diff-check.log`, `checks.json`: scoped diff check passed; primary image schema/argument and retained Blocked status checked.
- `commands.json`: exact commands, exit codes and log hashes. Build artifacts are external at `/tmp/custody-authority-deadline-repair-20261025a`.

Earlier evidence and concurrent unrelated work remain preserved. Parent owns full verification, final inventory and review.
