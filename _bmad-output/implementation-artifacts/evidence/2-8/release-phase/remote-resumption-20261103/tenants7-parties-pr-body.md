Selecting Memories 3.0.18 leaves Parties' explicit refresh capability disabled because the owning guard recognizes only earlier qualified versions. Add exactly 3.0.18 after published API and client-protocol qualification, preserving earlier entries and refusal of unqualified versions.

The offline CI job gains a focused Release/package build and 52-test refresh/recovery lane using selected EventStore 4.0.4, Tenants 7.0.0 and Memories 3.0.18. It exercises the actual published client with controlled HTTP responses and checks capability metadata, revision/idempotency fields, serialized restart recovery and persisted test-store end states. The recorded EventStore root supplies ordinary gateway configuration only. Older offline inputs, legacy caller, independent jobs and strict assertions remain intact.

Validation:
- Fresh selected-package build: zero warnings/errors; 52 existing wire/recovery tests passed, zero failed/skipped, with minimum 52 and fail-skips enabled.
- Eight capability controls preserve earlier supported versions and refuse unqualified fixtures; signed client/Contracts 3.0.18 APIs and loaded bytes were verified.
- Final Tenants 7 checks use a fresh task-only official NuGet cache. Loaded Client/Contracts/Server/Testing 7 bytes match the verified signed archives; the shared cache is untouched.
- Exact commit/title pass locked commitlint 21.2.4 under Node 24.22.1/npm 11.19.1.

Hosted qualification is still blocked by the recorded Builds catalog's Redis 3.4.29 floor versus Gateway 4.0.4's >=3.4.31 dependency, and by the existing offline-job ordering assertion detecting the added build's -m:1 after the first test command. Both gates remain blocking and need scoped follow-up; no version override or assertion weakening was used. Four optional live Memories/Dapr cases and full runtime/cohort qualification remain outstanding. This prerequisite grants no Story 2.8 hosted lifecycle or candidate acceptance.
