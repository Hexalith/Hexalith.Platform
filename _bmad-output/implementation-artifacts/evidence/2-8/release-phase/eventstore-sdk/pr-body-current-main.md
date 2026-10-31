Native module integration suites need to inspect the exact synthetic event
stream through their descriptor-owned Redis endpoint. Add
TestEnvironmentStreamReader to the shared EventStore integration testing SDK,
refuse foreign tenants before opening persistence, and declare its centrally
versioned Redis dependency.

Reconciled normally with current main 484f4bd82fd2ef4b6c2653fdde57feeeb6d85a3c,
including the independently qualified native/live-sidecar prerequisite fixes.
The resulting tree differs from main in exactly the original three SDK files;
all independent owner files are preserved exactly.

Validation: merged source 2ed2f03346d11c925104f4bfc31ac747182a3dc8 builds in
Release with zero warnings/errors against the owner's pinned Builds package
props and published cross-repository dependencies. All 15 descriptor fixture
tests pass, including the foreign-tenant refusal control. Owner-pinned
Commitlint 21.2.4 and the normal staged-file guard pass. The original live-sidecar
failure remains historical evidence; all seven current-base PR checks must
succeed before merge is considered.

Caller enrollment, workflow/tool configuration, module manifests, solution
activation and consumer pins remain for later steps. SDK PR merge and protected
release dispatch remain held for all required current-base gates and exact
owner/cohort source reconciliation. No version override or dependency pin was
changed. Hosted module CI qualification remains pending.
