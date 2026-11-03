# Story 2.8 local reconciliation, 2026-11-03

Status: incomplete. Existing runner and versioned workflow implementation is
present; reference activation and hosted qualification remain gated. This check
performed no remote retrieval or operation and changed no source, caller, tool
pin, registry resource, credential, or publication state.

[The verification summary](verification-summary.json) binds the current local
repository revisions, context/source hashes, declared catalog defaults, reference
profiles, caller/tool pins, and cached archive identities. Current Builds selects
EventStore 4.0.4, Tenants 6.0.0, Memories 3.0.18, and Parties 3.0.0. Both reference
repositories still call `domain-ci.yml` and pin CLI 4.41.1. Their package profile
entries exist; no package suite was executed by this reconciliation.

The integration helper passes 24 tests, the registry preparation passes 3 tests
and its offline artifact check, and the workflow validators pass 411 legacy and
254 v2 assertions. These local controls grant no hosted acceptance. No production
code changed or C# build/runtime qualification was performed in this check.

[NuGet signature verification](cached-package-signatures.json) succeeds for the
cached CLI 4.41.1 and 4.43.2 archives and EventStore.Testing 4.0.4. Exact stdout and
stderr are retained alongside that receipt. The verifier used
`NUGET_CERT_REVOCATION_MODE=offline` and inherited a seccomp filter returning
`ENETUNREACH` for `connect`, `sendto`, `sendmsg`, and `sendmmsg`; an actual socket
connection control confirms the restriction. Verification therefore used local
trust/revocation material and does not establish fresh online revocation status.
The attempted network-namespace isolation was unavailable (`unshare --net true`:
`Operation not permitted`); the inherited syscall restriction was used instead.

Both CLI archives' nuspec source commits match their local release tags, and the
CLI/Tooling assemblies in the extracted cache match the verified archive bytes.
The pinned 4.41.1 executable exposes `test --ci-owner-receipt`. Its packaged
EventStore host depends on EventStore 3.130.2; CLI 4.43.2 packages EventStore 4.0.0.
Neither observation qualifies compatibility with the current 4.0.4 catalog.
Parties.EventStoreExtensions 3.0.0 is absent from the local package cache; that
does not establish whether the package is published.

The next execution phase must verify the complete owner-selected released
package/tool/declaration/image cohort and perform the reviewed registry reader
procedure, including pinned live drift checks and real authenticated read/write
refusal measurements. Only then may it adopt the exact v2 producer in EventStore
and Parties, update their owning caller contracts while retaining all existing
test projects, and collect hosted success/failure/cancellation/partial-startup,
attachment withdrawal and zero-owned-leftover evidence. The current local-only
handoff authorizes none of those remote operations. Story status remains in
progress; candidate acceptance is not claimed.
