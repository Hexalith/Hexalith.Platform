Helper startup currently reports the same Aspire “missing” diagnostic for a missing executable, denied execute permission and an executable held open for writing. Retain a finite failure kind and native error code through HXR015 so these refusals are distinguishable, while preserving fail-closed behavior, output bounds and existing deadlines. Exception text, paths, arguments and environment values are excluded from the new metadata.

The branch retains the original reviewed guard commits from PR162 in its review history; their content is already on restored main. Current main has been merged into this feature branch without changing its reviewed content tree. Main integration will use the squash method required by the live ruleset after all required checks pass.

Validation: all27 focused cases pass, including all19 original cases. Three actual filesystem regressions fail before the fix and in a compiled metadata-omission copy, then pass with the correction. The normal Release rebuild has zero warnings/errors. All12 restored Hexalith package archives passed normal repository-signature verification; actual public output bodies were matched to their signed members. Required hosted CI and merged-main validation remain required. The original hosted native launch error is still unproved.

<!-- CURSOR_SUMMARY -->
---

> [!NOTE]
> **Low Risk**
> Changes are limited to composition helper launch diagnostics and HXR015 message wording; fail-closed prerequisite behavior and output bounds are unchanged.
> 
> **Overview**
> Helper process startup now records **bounded launch failure metadata** (`CompositionProcessStartFailure` / `CompositionProcessStartFailureKind`) when `Process.Start` fails, instead of treating every refusal as an undifferentiated non-start.
> 
> `CompositionProcess.TryStart` classifies `ReturnedFalse`, `Win32` (with native error code), I/O, invalid operation, and platform-not-supported failures and attaches them on `CompositionProcessResult.StartFailure`. **Aspire prerequisite probe (HXR015)** appends `(start failure {Kind}[, native error N])` to the diagnostic so missing binary, permission denied, and file-in-use cases can be told apart while still omitting paths, arguments, env values, and exception text.
> 
> Tests assert the new metadata stays within safe bounds and that nonzero exits and timeouts do **not** populate `StartFailure`.
> 
> <sup>Reviewed by [Cursor Bugbot](https://cursor.com/bugbot) for commit 7b053cfd5f7cb63513841edd07168f4c7aabeda0. Bugbot is set up for automated code reviews on this repo. Configure [here](https://www.cursor.com/dashboard/bugbot).</sup>
<!-- /CURSOR_SUMMARY -->