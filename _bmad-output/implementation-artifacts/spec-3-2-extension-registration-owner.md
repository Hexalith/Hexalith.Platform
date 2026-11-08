---
title: '3.2 Give declared extensions one adapter registration owner'
type: 'bugfix'
created: '2026-11-08'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '769507aa6906ca46105dd1403693ed699b9045d6'
story_key: '3-2-compose-the-eventstore-host-with-enrolled-extension-packages'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Fix duplicate adapter registration when a declared EventStore extension also appears in the Development routing catalog. The user approved the recommendation that the extension loader owns declared packages and the catalog bootstrap reuses their registrations. Undeclared legacy packages retain their current bootstrap registration behavior, including in mixed-adoption compositions. Load declared extensions before AddEventStoreServer, which can register the bootstrap internally. Retain the shared registry's rejection of conflicting adapters from different packages and the extension readiness report. Verify extension-only, overlapping inventories, legacy/mixed inventories and actual conflict refusal through the real registration entry points. Keep the implementation local and focused: no new registration framework, public API, module adoption, release, push, tuple change or dependency pin change. Existing review decisions outside registration ownership remain separate.

</frozen-after-approval>

## Implementation Notes

- User selected the recommendation with "apply recommended". Intent gaps: none. Irreversible work: none. Expected footprint: EventStore bootstrap filtering, Platform host call ordering, focused registration tests and review bookkeeping. Route: oneshot.
- Existing changes in the parent checkout are this session's authorized review documents and are preserved. The EventStore checkout was clean at b13271ba1d5ce4553372ee5e9edb91ff4ca7b0ce. No unrelated user edits were found.
- Reuse the EventStoreExtensionCatalog singleton already registered as an instance. The bootstrap can inspect that instance without constructing a service provider and skip factories only for its verified package identities. AddEventStoreServer invokes the bootstrap internally when CatalogPath is present, so moving only the explicit bootstrap call is insufficient.
- Implemented the package filter inside EventStore's internal RegisterLegacyAdapters method, called by the existing public bootstrap. Declared packages reuse their registered instances; undeclared assemblies retain the original factory/registration loop. The registry and readiness implementation are unchanged.
- Platform Program now loads extensions before AddEventStoreServer. The public EventStore server, extension and bootstrap registration methods document this ordering. No public registration abstraction or API was added.
- Platform regression tests use the real on-disk test extension and Parties legacy assembly through AddEventStoreRunScopedCatalogGateway. They verify one probe factory invocation across repeated bootstrap calls, the same registered instance, legacy-only and mixed registration, successful registry construction and extension readiness. A separate actual-Program test inspects registrations before hosted activation and proves the implicit AddEventStoreServer bootstrap sees the extension first. Existing HTTP readiness/subscription tests remain intact.
- Owning EventStore tests cover declared reuse, undeclared registration and a conflicting legacy assembly alongside a declared package. The conflict still reaches and is refused by the shared registry. The existing distinct-declared-package conflict test also remains.
- Initial focused verification passed 32 Platform tests and 24 EventStore tests. After review corrections, the EventStore project rebuilt with zero warnings/errors and all 27 extension registration tests passed. Final Platform rebuild and rerun are recorded below when complete.

## Review Triage Log

| Finding | Verdict | Evidence and resolution |
| --- | --- | --- |
| R1: Missing declared-versus-legacy conflict case | medium | Existing tests covered conflicts between declared packages, while the newly changed bootstrap path needed its own refusal case. Added AConflictingLegacyPackageStillFailsTheRegistryAlongsideADeclaredPackage; two distinct assembly identities still produce a registry refusal for their conflicting command. Fixed. |
| R2: Public ordering prerequisite undocumented | low | AddEventStoreServer can invoke bootstrap internally, so SDK consumers need the same order as Platform. Added remarks to all three existing public registration methods. Fixed by direct documentation corrections. |
| R3: Owning repository lacked reuse regression tests | medium | The initial tests exercised the public bootstrap only in Platform's source suite. Extracted the existing loop into one internal method and tested it in EventStore's own suite; Platform still covers actual bootstrap and host entry points. Fixed. |
| R4: Assertion reinvoked the legacy factory | low | Calling PartiesCatalogAdapters.Create only to count results added an unnecessary factory invocation. Replaced it with a contribution assertion; the undeclared probe scenario independently checks exact legacy invocation count across repeated bootstrap calls. Fixed. |

The independent reviewer checked the corrections and reported no remaining concrete correctness issues. No new implementation-review items were deferred. Existing Story 3.2 review decisions and deferred package/CI qualification work remain separate.

## Final verification

- Final Debug builds of the Platform composition test project and EventStore server test project passed with zero warnings and zero errors. Source dependency roots were explicitly bound to the workspace's root-declared repositories.
- From Platform: dotnet exec tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -class Hexalith.Platform.Composition.Tests.GatewayExtensionRegistrationTests -class Hexalith.Platform.Composition.Tests.EventStoreExtensionCompositionTests -class Hexalith.Platform.Composition.Tests.GatewaySeedSubscriptionTests — 32 passed, 0 failed/skipped/not-run. Includes actual Program ordering, overlapping and mixed inventories, generated host build, extension HTTP readiness and existing subscription behavior.
- From EventStore: dotnet exec tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Extensions.EventStoreExtensionRegistrationTests — 27 passed, 0 failed/skipped/not-run. Includes reuse, one factory invocation, declaration drift and both declared/declared and declared/legacy conflicts.
- Total: 59 focused tests passed. Logs: /tmp/story-3-2-registration-build.log, /tmp/story-3-2-registration-tests.log, /tmp/story-3-2-registration-owner-build.log, /tmp/story-3-2-registration-owner-tests.log. Whitespace checks passed in both owning repositories.
- Independent review raised four findings; all received direct fixes and the reviewer verified the corrections without finding remaining concrete correctness issues.
- EventStore local source commit: 5ec3436778a498612b08ae354f8b2dad95d6093a on fix/declared-extension-registration-owner. The Platform source companion is on fix/story-3-2-extension-registration-owner. Commitlint 21.2.4 accepted both exact commit messages under their owning repository configurations; receipts are /tmp/story-3-2-owner-commitlint.log and /tmp/story-3-2-platform-commitlint.log.
- The Platform recorded EventStore gitlink remains b13271ba1d5ce4553372ee5e9edb91ff4ca7b0ce. Its working submodule holds the companion source commit for local validation. No released version/tool/tuple pins were changed; landing requires the normal EventStore SDK release and subsequent Platform adoption. This local diagnostic proof is not a published-package or candidate qualification.
- Sprint tracking remains review, which is already at the oneshot workflow's target. This focused fix does not close the remaining Story 3.2 diagnostic-owner decision or its deferred qualification work. No remote push, PR update, merge, CI dispatch or publication was performed.
