Preserve current domain-ci.yml callers and introduce declared package integration
through domain-ci-v2.yml. The legacy workflow and dedicated documentation remain
byte-identical to merged foundation 57255b502fdd546bb09851d04fc1830175d897f3.

V2 requires isolated success, declared suites, dedicated pull-only registry secrets,
exact owner finalization and classified diagnostics. Bind its actual workflow
identity and direct integration executor into the authorized closure; reject legacy
identities/digests and missing or changed executor source. Remove unused legacy-only
inputs, preserve direct integration/AppHost refusals, and require HTTPS downloads
and redirects. Run both validator modes in CI/release and document explicit migration.

When explicit down races startup's outcome publication, supported attachment owners
now wait for the metadata lock within the original stopping token. Durable withdrawal
still precedes resource cleanup; persistent lock failure remains a bounded refusal
with exact retry. Owners whose context cannot admit attachments retain their existing
immediate bookkeeping/cleanup path. Preserve first outcomes, times and attachment
expiry; no stopping allowance is extended. The C# candidate consumer and package
layout remain unchanged. The scoped attributes entry preserves authored CRLF in the
existing shell controls.

Validation: fresh full Debug project graph builds with zero warnings/errors. On the
same binary with complete simulated GitHub context, all 59 local lifecycle cases,
16 CI lifecycle cases and three existing legacy bookkeeping scenarios pass, with no
failures/skips/errors. Deterministic baseline controls passed the four original cases
and failed both forced contention cases before the fix. Exact commands, source and
binary hashes, earlier failed diagnostics and source limits are retained.

Unchanged v2 source controls passed 411 legacy and 254 strict executable assertions,
95 provenance tests, 56 governed workflow tests, 11 preparation tests and 24 unchanged
helper tests. Actionlint and diff checks pass. The earlier 159-case publisher run is
a historical baseline. Local controls and simulated CI supply no hosted reference or
candidate acceptance; normal complete exact-head hosted CI remains mandatory.

E/P adoption, registry provisioning, compatible releases and hosted lifecycle
qualification remain pending. No merge or publication is part of this PR stage.
