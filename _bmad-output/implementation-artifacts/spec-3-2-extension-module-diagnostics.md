---
title: '3.2 Preserve module ownership in extension runtime diagnostics'
type: 'bugfix'
created: '2026-11-08'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '4ce16b855e687e1c27b5447bb59a497c7fdad57e'
story_key: '3-2-compose-the-eventstore-host-with-enrolled-extension-packages'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

The user selected review decision D3 option 1: add optional declaring-module metadata to runtime extension diagnostics. Preserve EventStoreExtensionDeclaration's existing three-argument constructor and three-value deconstruction; add optional ModuleId metadata without changing the positional contract. Platform supplies its validated declaring module through the generated extension configuration. EventStore accepts legacy entries without ModuleId and includes provided ownership in malformed-entry, unsupported-major, duplicate, missing-identity, drift, unavailable-assembly and adapter-source refusals. Duplicate diagnostics identify both supplied owners. Metadata is diagnostic context and grants no registration or provider authority. Preserve stable refusal conditions, existing registration ownership and readiness behavior. Verify configured and legacy declarations plus a real host refusal. No release, push, merge, tuple update, dependency pin change, module manifest migration or unrelated review patch is part of this fix.

</frozen-after-approval>

## Implementation Notes

- User approval is explicit in the current review conversation. Intent gaps: none. Irreversible work: none. Small footprint: extension declaration, configuration parser/refusal messages, Platform environment binding and focused tests. Route: oneshot.
- Continue the existing local fix branches. Platform baseline is 4ce16b855e687e1c27b5447bb59a497c7fdad57e; EventStore baseline is 5ec3436778a498612b08ae354f8b2dad95d6093a. The parent working gitlink already differs from its recorded pin because of the preceding approved source fix; preserve that state and the recorded dependency pins.
- Use an optional init property rather than a fourth positional parameter, retaining the constructor and deconstruction signatures used by already compiled consumers. Prefix existing runtime refusal text only when ownership is supplied; retain the old text when it is absent.
- Added ModuleId to the host configuration entry whitelist and parser. A supplied value must be nonblank and trimmed; omission remains valid. Platform derives it from the enrolled module identity, with no manifest or schema change.
- Unsupported-major, missing-attribute, declaration-drift, both unavailable-assembly paths and adapter-source refusals now prefix their existing message with the supplied module. Duplicate messages associate each package with its own supplied owner. Stable condition identifiers are unchanged. A malformed owner-bearing entry names its module/package; legacy malformed-entry text remains unchanged.
- New owning-repository tests exercise the three-argument constructor and three-output deconstruction through their runtime reflection signatures, optional metadata round-trip alongside a legacy entry, seven owner-bearing refusal cases, both duplicate types, and malformed supplied metadata. Existing registration tests remain unchanged and cover legacy behavior.
- Platform tests assert the generated ModuleId environment value and pass it through the real host's configuration into a declaration-drift refusal. Existing no-extension, readiness, registration ownership and subscription cases remain in the focused run.
- Owning EventStore Debug build passed with zero warnings/errors and 41 focused tests passed. Platform Debug build also passed with zero warnings/errors; final host verification and independent review are recorded below when complete.
- Review corrections reject object/array ModuleId sections instead of silently interpreting them as omitted. Owner-bearing malformed-entry guidance now lists all four allowed fields and includes the received package/major. Added object/array refusal cases and a valid-owner/invalid-major case.
- Duplicate tests now cover both owned, first-only owned, second-only owned and both legacy declarations for each duplicate kind; legacy duplicate messages are asserted verbatim. Configuration XML documentation states omission, scalar/nonblank/trimmed values and diagnostic-only authority. After these corrections, the owning suite passes 50 tests.

## Review Triage Log

| Finding | Verdict | Evidence and resolution |
| --- | --- | --- |
| R1: Structured ModuleId is discarded as omitted | medium | A child section has no scalar Value, so the original parser accepted it without ownership. Reject child sections under the new field and cover object/array shapes through public registration. Fixed. |
| R2: Malformed entry guidance excludes valid optional metadata | low | The legacy three-field sentence was reused for owner-bearing entries and appended a generic metadata warning. Preserve that exact sentence only for legacy entries; describe all four allowed fields and the received package/major when metadata is present. Fixed. |
| R3: Deferred adapter enumeration can escape the named refusal | low, rejected | The loop outside the invocation catch predates this metadata change and still stops startup. Existing factories return materialized inventories; adding another catch/guard for a future throwing iterator is outside this focused correction and repeats the prior rejected story finding. |
| R4: Mixed ownership and exact legacy duplicate text untested | medium | Initial tests covered only both-owned declarations. Added all four ownership combinations for each duplicate kind and golden legacy-message assertions. Fixed. |
| R5: Configuration field validation contract undocumented | low | Added scalar/nonblank/trimmed, omission and diagnostic-only authority to the existing public configuration-section documentation. Fixed. |

The independent reviewer verified all four corrections and found no new correctness issues. No implementation-review items were deferred; the pre-existing iterator item was rejected under the review's low-impact rule.

## Final verification

- Final Debug builds of the EventStore server test project and Platform composition test project passed with zero warnings and zero errors. Workspace source roots were explicitly bound to the root-declared repositories; no nested submodules were initialized.
- From EventStore: dotnet exec tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Extensions.EventStoreExtensionRegistrationTests -class Hexalith.EventStore.Server.Tests.Extensions.EventStoreExtensionOwnerDiagnosticTests — 50 passed, 0 failed/skipped/not-run. Includes legacy runtime API signatures, old registration behavior, supplied/omitted/structured metadata, all named owner-aware refusal paths, mixed duplicates and exact legacy messages.
- From Platform: dotnet exec tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -class Hexalith.Platform.Composition.Tests.GatewayExtensionRegistrationTests -class Hexalith.Platform.Composition.Tests.EventStoreExtensionCompositionTests -class Hexalith.Platform.Composition.Tests.GatewaySeedSubscriptionTests — 32 passed, 0 failed/skipped/not-run. Includes generated metadata, a real host's owner-bearing drift refusal, readiness and the preceding registration-owner regressions.
- Total: 82 focused tests passed. Logs: /tmp/story-3-2-module-diagnostics-owner-build.log, /tmp/story-3-2-module-diagnostics-owner-tests.log, /tmp/story-3-2-module-diagnostics-platform-build.log and /tmp/story-3-2-module-diagnostics-platform-tests.log. Whitespace checks passed in both repositories.
- Independent review: four direct corrections verified, one pre-existing low-impact iterator finding rejected, no new correctness issues or deferred implementation items.
- EventStore local source commit: a74e05b5419863e749ce72dd24d9288f713f90af, following the registration-owner commit 5ec3436778a498612b08ae354f8b2dad95d6093a. Commitlint 21.2.4 accepted the exact owner and Platform messages under their respective configurations; receipts are /tmp/story-3-2-module-owner-commitlint.log and /tmp/story-3-2-module-platform-commitlint.log.
- The parent recorded EventStore gitlink remains b13271ba1d5ce4553372ee5e9edb91ff4ca7b0ce; its working submodule contains the companion commits for local source verification. SDK publication and Platform dependency adoption remain separate. No published pin, tuple, release, remote ref or PR was changed by this implementation.
- Sprint tracking remains review, already at the oneshot workflow's target. Original Story 3.2 P1–P4 and its qualification follow-ups remain open; this focused implementation closes D3/P6 only.
