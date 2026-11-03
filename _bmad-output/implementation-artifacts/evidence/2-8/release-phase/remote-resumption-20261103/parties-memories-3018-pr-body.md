Selecting Memories 3.0.18 leaves Parties' explicit refresh capability disabled because the owning guard recognizes only earlier qualified versions. Add exactly 3.0.18 after published API and client-protocol qualification, preserving earlier entries and refusal of unqualified versions.

The existing offline CI job gains a focused 3.0.18 Release/package build and 52-test refresh/recovery lane. It uses the actual published client with controlled HTTP responses and checks capability metadata, revision and idempotency fields, serialized restart recovery, and persisted intent/mapping end states. The recorded EventStore root supplies ordinary gateway configuration only. Existing jobs, the older package-cohort checks and the legacy caller remain intact.

Validation:
- Fresh Release/package Parties.Tests build: zero warnings/errors against EventStore 4.0.4, Tenants 6.0.0 and Memories 3.0.18, with external source selectors false and NuGet audit enabled.
- Existing wire/recovery regressions: 52 passed, zero failed/skipped; exact extracted CI script also passes with minimum 52 and fail-skips enabled.
- Eight capability controls preserve earlier supported versions and refuse two explicitly unqualified fixtures.
- Loaded REST client and Contracts assembly bytes match the inspected signed official 3.0.18 archives; their public APIs match 3.0.13.
- Exact commit/title pass locked commitlint 21.2.4 under Node 24.22.1/npm 11.19.1.

The four optional live Memories/Dapr cases remain separate and unqualified here. Full current-base hosted gates and live runtime/cohort qualification are still required; this prerequisite grants no Story 2.8 hosted lifecycle or candidate acceptance.
