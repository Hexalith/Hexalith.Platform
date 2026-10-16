# Story 3.6 offline enrollment/execution bindings — 2026-10-16

Offline preparation and implementation are complete. **135 synthetic tests** and
**13 existing built LaunchReadiness checks** pass; three independent review layers
and follow-ups close all **19 finding rows**. No operational approval is ready.
Actual current server/CA/peer/client identity, independent grants/revocations/read
rights, protected delivery/enrollment, live runtime/OpenSSL and actual response
compatibility remain missing. [Exact evidence gaps](evidence-gaps-v1.json) and
[NEXT-PREREQUISITE.md](NEXT-PREREQUISITE.md) identify each requirement.

Final create-only manifest: `/home/administrator/hexalith-recovery-evidence/4-0/20261016t103632z-native-enrollment-execution-revision-sealed/packet-v1.json`.
SHA-256: `c71f38517e02ea5bc7fa176cfb1abe8f12b43b499fbe6f74fbdc339d91f0b23d`. It binds **88 payloads / 89 files** and selects
`enrollment-execution-binding-v4.json`. Original supplement remains exactly
`a7b9bf06139cec3022fbced5fe182b42c773b9a7099c40c84162893a766e2631`.
No prior packet, signed original, expired recovery or failure history is replaced.
This is another binding revision for RID `20261015t162756z`, never new recovery.

## Implementation and review

Owning Platform adds `native_enrollment_execution.py` and
`test_native_enrollment_execution.py`: one-use inert credential delivery; strict
public-binding/witness shapes and validity; exact 23-selector rights; callback-free
inert data/clock/transport validation; descriptor-relative exclusive scratch
custody; post-write identity/digest/clock/source checks; fixed sanitized refusal.
`verify_enrollment_revision.py` and its tests bind complete runtime/TLS metadata,
exact schema/identity, twelve evidence gaps and their historical sources,
preservation outcomes and the one pinned concurrent status change. Public live
enrollment/execution always refuse before inspecting inputs or real credentials.
TLS requirements are metadata requirements; no actual TLS or credential-pair
qualification is claimed. Retained synthetic scratch receipts are provisional
until post-write checks; a failed returned result supplies no acceptance.

Review candidates v1–v3, source snapshots, all eight enrollment attempt logs,
failed verifier results, triage and qualification outputs remain retained. First
review found cleanup/field callback/final-clock/metadata validation and coverage
gaps; follow-ups caught mutable delivery/clock state, source-classification
association and the concurrent-status negative-test gap. All were patched and
rechecked. Removing client/witness validity now fails 21 subtests; discarding
either returned or retained measurements fails exact independent output checks;
removing the status pin fails the resealed-drift test.

## Commands and results

All synthetic commands use `/usr/bin/python3.14 -I -S -B` with absolute scripts
under `/home/administrator/projects/hexalith/platform/eng/recovery-preparation/`:

| Script | Passed |
| --- | ---: |
| `test_native_measurement_adapter.py` | 27 |
| `test_native_https_transport.py` | 17 |
| `test_verify_native_supplement.py` | 26 |
| `test_native_enrollment_execution.py` | 43 |
| `test_verify_enrollment_revision.py` | 22 |
| Total synthetic | 135 |

The existing Timesheets executable is invoked as
`tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor`.
It passes 13/13 with no failures/errors/skips. No restore, new .NET build, live
journey or deployed acceptance is claimed. Exact retained synthetic output is in
`/home/administrator/hexalith-recovery-evidence/4-0/20261016t103632z-native-enrollment-execution-revision-sealed/test-results/` and the original attempt logs in both packet directories.

```bash
/usr/bin/python3.14 -I -S -B /home/administrator/projects/hexalith/platform/eng/recovery-preparation/verify_enrollment_revision.py --sha256 c71f38517e02ea5bc7fa176cfb1abe8f12b43b499fbe6f74fbdc339d91f0b23d
```

This exits **0** at **2026-10-16T11:09:28.724624Z**. The
[exact verifier receipt](final-packet-verification.json) matches 88 payloads,
56 original supplement files, 140 v4 files, 348 historical files, six original
sources and 1,337 runtime files/nine libraries. Authority/approval/live flags stay
false and the gate stays closed. At that UTC the candidate is unexpired. Policy
preparation/ceiling stay **2026-10-15T16:27:56Z / 2026-10-16T16:27:56Z**.

## Preservation and concurrency

The 557-file initial map remains immutable: **556 unchanged** and one explicitly
recorded concurrent Platform sprint-status change. Its three versions are kept:
sealed `1d66b846fb554deefa67acb44711671376d5d354f74558ffb9b34a87a638283d`,
pre-session `2d0c7c85dc259481180cd9c65fb327c32706e9ddbfa0c1a01fcd848da2d2d41e`,
current `f6c25046d9db40c069cb183cf87f8fef19a0a952b601c87dbe690d0b0aff7e43`.
The original full verifier still exits **1**; its exact refusal and committed
provenance are retained. A separate verifier never calls that result a pass.

Session heads were Timesheets `ee95f90f529812c744761090691a23196c971c78` and
Platform `1711deba08a4e66b46acfff8d08e7f2ad7f74ba1`. Another process committed
`b14e98b` and `114e08830da2d8cca0e77cd870b8867506b4c68b`, including new task files
and moving unrelated Story 1.7 from review to done. This task made no Git mutation
and preserves that history and other concurrent changes. Gitlinks remain unchanged.

The unsealed preparation at `/home/administrator/hexalith-recovery-evidence/4-0/20261016t103632z-native-enrollment-execution-revision` retains all **75 original files and modes**,
including eight newly generated 0644 logs. To keep those exact originals untouched,
the separate final revision copied every byte into 0700 directories / 0600 files.
Its `preparation-custody-inventory.json` and `preparation-preservation-after.json`
record that distinction. Earlier review candidate modes remain dated evidence.
No final packet member is subsequently modified.

## Limits and remaining acceptance

Preserve 23 exact ordered GET selectors; 15s/request, 300s/lane, one attempt each,
8KiB headers, 256KiB/response, 4MiB/lane and 64KiB final receipt. Exact
Content-Length only; no retry, fallback identity, proxy, redirect, DNS, pagination,
arbitrary command or scope expansion. Detailed node, pod/init-image, PVC/PV and
current CNPG-controller families remain unsupported. First failure/unknown,
tampering, rights loss, drift, expiry, clock regression, timeout, overflow or
collision stops dependent work. The real output slots stay absent.

No actual kubeconfig/private credential read, authentication, network/API/provider/
IAM request, preflight replay, signature verification, signing, publication,
infrastructure change, capture, restore, retirement, upgrade, deployment or domain
write occurred. No dependency or gitlink update was performed. Existing Timesheets
resource-binding issuer, legacy coverage/continuity/maintenance, subscriber and
shared-rebuild/deployed acceptance remain unmet. Spec-23 stays draft; Story
3.6/Epic 3/4.27 in progress; 4.1 operational gate closed; release **FAIL**.

## Completion recheck — 11:12 UTC

The [completion verifier](completion-verification.json) again exits 0 at
2026-10-16T11:12:06.201894Z with unchanged closed gates and preservation counts.
[Independent final review](independent-final-review.json) passed the sealed bytes
and all 75 original preparation files. [LaunchReadiness](launch-readiness-final.json)
passes 13/13 in 0.129s after documentation updates. Both repositories keep the
same gitlinks and empty staged diffs.

Platform concurrently advanced again to `9256c675186b020929d67230a22892bb13488bc2`; source/packet bytes still
match. Repository-wide `git diff --check` exits 2 for trailing whitespace at
`src/Hexalith.Platform.Custody/Hexalith.Platform.Custody.csproj:12`, an unrelated
concurrent change. It is preserved. The [scoped check](scoped-diff-check.json)
against the original Platform baseline exits 0 for all four task source/test files.
Timesheets `git diff --check` exits 0. No broad .NET build is claimed.
