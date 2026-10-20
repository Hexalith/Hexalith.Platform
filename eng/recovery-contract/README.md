# Recovery contract conformance tooling

The [normative contract](../../docs/contracts/recovery-hook-v1.md) and
[Draft 2020-12 schema](../../schemas/hexalith.recovery-hook.v1.json) define version
`1.0.0`. This Python 3.11+ CLI uses locked `jsonschema` and its required
`rfc3339-validator` date-time checker. It checks
wire shape, IDs, typed positions, dependencies, ordering, retry identity, result
gates and independently supplied authority bindings. It performs no live work.

From the repository root, reproduce the checks with uv:

```bash
uv sync --locked --project eng/recovery-contract
uv run --locked --project eng/recovery-contract python -m unittest discover -s eng/recovery-contract -p 'test_*.py'
uv run --locked --project eng/recovery-contract python eng/recovery-contract/validate.py \
  eng/recovery-contract/fixtures/valid-chain.json \
  --context eng/recovery-contract/fixtures/expected-context.json \
  --commit-context eng/recovery-contract/fixtures/expected-commit-context.json
git diff --check
```

The valid chain produces exactly:

```json
{"conformant":true,"outcome":"complete","reopeningEligible":true,"runtimeQualified":false}
```

Full chain validation requires both independent contexts. The invocation context
represents the expected admission authority, and the commit context represents
current authority at completion. Both are separate files; consumers MUST populate
them from trusted state, never copy assertions out of the submitted request.
All deliveries and commits are checked. The commit snapshot follows the results
in time, and its changed epoch invalidates the chain. A single offline snapshot
cannot prove current authority at each actual runtime operation.

For a standalone declaration, invocation, result, context or chain shape check:

```bash
uv run --locked --project eng/recovery-contract python eng/recovery-contract/validate.py \
  eng/recovery-contract/fixtures/result.json --schema-only
```

That mode reports `outcome: shape-valid`, with both reopening eligibility and
runtime qualification false. The schema carries every rule a single record can
express. ID uniqueness within arrays, phase-dependent rules (position due phases,
premature gate and reopening claims) and all cross-record semantics need full
chain validation.

The CLI writes one compact JSON report to stdout. Success is deterministic;
diagnostics contain stable codes and never reflect JSON values, property names,
paths or library exception messages. Exit codes are:

| Code | Meaning |
| --- | --- |
| 0 | Complete offline conformance, or explicitly requested shape validation. A complete chain can still have reopening eligibility false when reissue is pending or final module gates withhold eligibility. |
| 2 | Refusal: malformed JSON (including duplicate keys or nonfinite numbers), unsupported version, invalid shape, identity/authority mismatch or other cross-record contradiction. |
| 3 | Structurally and semantically conforming chain with `failed`, `incomplete` or `unqualified` outcome; reopening is blocked. |

All files in `fixtures/` are **synthetic offline examples**. None is release,
authentication, fencing or recovery-qualification evidence. They retain only
credential references and binding metadata, never credential values. The valid
chain includes all three recovery classes, scoped opaque EventStore/Memories
positions, dependency ordering and a custody fence. `declaration.json`,
`invocation.json` and `result.json` are individual shape examples extracted from
that chain. The matrix rows map to fixtures as follows: `valid-chain.json` is the
conforming chain, `invalid-shape.json` the invalid shape, `takeover-context.json`
the authority mismatch, and `incomplete-chain.json` and `failed-chain.json` the
incomplete recovery. Tests also derive unqualified, in-place, retry,
replay-exception, cycle and mismatch cases from the valid chain.

To exercise independent epoch takeover, replace the commit-context argument with
`eng/recovery-contract/fixtures/takeover-context.json`; expect exit 2 and
`commit-context-changed`. Replace the chain path with `incomplete-chain.json` or
`failed-chain.json` while keeping the normal contexts; expect exit 3 and the
explicit preserved outcome. `invalid-shape.json --schema-only` returns exit 2.

Consumers remain responsible for exact release/version selection and retention,
module-specific position proofs, qualification of declarations/evidence, private
endpoint routing, authenticated issuer/credential validation, workload/custody
isolation, current-attempt reads at every retry and atomic guarded commit,
authority fencing, idempotency, and all operator reopening gates. The wire
metadata and offline contexts implement none of those controls. This directory
does not integrate with release records or publication workflows.
