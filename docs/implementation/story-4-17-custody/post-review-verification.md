# Story 4.17 — post-review evidence and local hardening

The exact inspected/executed transfer source is preserved as
[transfer_once.executed.py](transfer_once.executed.py), SHA-256
`19cb51fdf00db95a1d25080c7d88cc0997a1640e6ca693e2b9b6697f22be0c11`.
The original [inspection](inspection.json), [plan](plan.md), preflight, test matrices/reports,
upload records and [custody receipt](receipt.json) remain historical evidence and are unchanged.
Their transfer-script digest refers to this executed alias.

The retained execution records establish **five PutObject API invocations and five returned
object versions**, followed by successful separate exact-version readback. They do not retain
HTTP transport-send counts. The historical `total_max_attempts=1` configuration disabled the
ordinary SDK retry allowance; it did not prove that S3 region-redirect handling could not send
another HTTP PUT. Earlier no-retry wording must be read with this limit. Absence of historical
HTTP resends is unproved; none is inferred from returned versions, ETags or API-call counts.
The executed source also fsynced file contents without fsyncing new parent-directory entries;
crash durability of those markers was not established. This historical attempt cannot resume.

[transfer_once.py](transfer_once.py) now contains **unexecuted local hardening**: each new
evidence file is fsynced and its parent directory is fsynced; a before-call reset and
before-send guard refuse a second HTTP send within one sequential PutObject API invocation;
failures after the start marker retain a sanitized terminal incident. This changed source has
no new inspection approval and has not been used for any archive operation. Local test
inspection files are synthetic fixtures in temporary directories, not execution authority.
Terminal incidents distinguish zero-intent failures (`before-first-put`, zero API invocations)
from `after-put-intent` failures using fixed journal counts. After an intent exists, invocation
and transport-send counts remain unknown; a journal entry alone does not prove a send.

[test_post_review.py](test_post_review.py) adds focused local controls without altering either
original matrix. It checks file/directory fsync, reproduces the pinned SDK's redirect resend
against a fake transport and checks its suppression, verifies pre-PUT failure incidents,
rejects a correct-count packet with one wrong artifact digest, and exercises fake GOVERNANCE
readback through `verify_readback.main()`. That last case must write a per-file rejection,
an overall unaccepted receipt and return exit code 1 despite the other four files passing.
These fake responses/receipts are not live custody evidence. Each run allocates fresh reports
under `/tmp/story-4-17-post-review-*` and rejects accidental socket connections.

Run only these supplemental controls:

```bash
uv run --quiet --no-project --with boto3==1.44.3 --with botocore==1.44.3 python -B docs/implementation/story-4-17-custody/test_post_review.py
```

To reproduce the **unchanged original 25 verifier controls** without deleting or replacing
`verifier-negative-checks.json`, use the separate local-only mode. It redirects the original
runner's sole report write to a fresh path and verifies historical files are unchanged:

```bash
uv run --quiet --no-project --with boto3==1.44.3 --with botocore==1.44.3 python -B docs/implementation/story-4-17-custody/test_post_review.py --original-verifier-controls-only
```

## Additional read-only admission evidence

[additional-admission-checks.json](additional-admission-checks.json) preserves both the
bounded scan PASS for the unchanged downloaded manifest and the five denied exact-version
ACL reads (HTTP 403). Its overall `FAIL_ADDITIONAL_ADMISSION_INCOMPLETE` remains intact.
[acl-current-version-checks.json](acl-current-version-checks.json) separately records
`PASS_CURRENT_PRIVACY_INFERENCE` at `2026-11-14T01:34:09.084163Z`: two identical inventories
bracket five current-object ACL reads, seven GET/list requests in that supplement. All five accepted
versions were latest, with no extras/delete markers/truncation, and each ACL showed only
owner FULL_CONTROL. Current privacy is inferred from those surrounding inventories; ACL
responses did not echo versions. This is neither successful version-bound ACL evidence nor
an atomic/continuous guarantee. No grants, credentials, controls or original receipt changed.
Between the supplements, root also made two diagnostic reads: an exact-version ACL GET
returned 403 AccessDenied, and a current-object ACL GET returned owner-only FULL_CONTROL
without an echoed version. The additional ACL/list follow-up totals **fourteen** reads:
five original denied calls, those two diagnostics and seven bracketed inventory/ACL calls.

## Administrator checkpoint and deletion review

**Review by December 13, 2026 (Europe/Paris)**, before the first enforced minimum ends at
`2026-12-14T01:08:47.313Z`. Using read-only access, compare the five receipt object/version
identities with version-bound HEAD/retention and current version/lifecycle/access observations;
record a dated follow-up and whether the guarantees remain open. Preserve any denied probe
and distinguish a current-ACL inference from version-bound proof. This is a manual checkpoint;
no schedule, provider action or automatic control change is created here.

While guarantees remain open, the Administrator's no-deletion hold continues beyond the
initial thirty-day COMPLIANCE minimum. `evidence/` has no lifecycle expiry; the minimum's end
does not remove that hold. At guarantee closure, review policy, remaining evidence dependencies
and retention/backup/rollback needs before deciding whether deletion is appropriate. Any
deletion requires a separate exact-version scope and authorization after applicable retention;
closure and December 14 are not automatic deletion dates.

Preservation supplies no qualification credit. Platform 4.17/EventStore 3.21 remain
in-progress; five guarantees REJECTED/open, technical FAIL (20/24), selection null and
production FAIL. Shared-host automated verification, bounded private-material scans and
historical/reference IAM identity bindings retain their recorded limits.
