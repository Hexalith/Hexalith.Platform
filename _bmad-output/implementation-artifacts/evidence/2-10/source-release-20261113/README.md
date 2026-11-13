# Story 2.10 compatible source preparation — 2026-11-13

This source prerequisite carries the EventStore endpoint correction
`3f7659e2ff660212bc4f31c6e118a14a65f55da5`. It does not qualify the lifecycle,
accept a tool, or supply a new outcome for any historical campaign.

The corrected Platform publication tuple selects its already configured 5.0.1
and the actual published Builds source
`3c2f5944aaa28dc7854a00a8a9df171aa810b0c7`. Its compatibility range floor is
aligned with that same configured version, preserving the strict matching guard.
The exact Platform tool manifest remains 5.0.1 and Parties main remains 5.0.0.
The prior non-release Builds gitlink and 5.0.0 publication binding were inconsistent;
none is relabelled as a 5.0.1 publication.

The proposed binding and independent publication verification retain the actual
release/run/artifact identities, original manifest bytes and SHA-256 values.
The downloaded signed 5.0.1 archive matches every original package payload entry;
NuGet adds only its repository signature, whose verification passed.

Current owner source bindings are regenerated from the actual committed owner
Git objects. Current owner sources and package-release declaration provenance
retain separate identities. Fresh legacy, pre-extension and package-reader
companions must copy this complete source unchanged except for the existing
bounded frozen tuple metadata and gitlinks. The original 349/412 profiles,
frozen sources, tools, declaration provenance, controls and all comparison guards
remain unchanged.

`preparation-checks/initial-controls.json` records the initial source/binding
controls and 45 lifecycle preparation controls. The actual checks grant only
source/preparation evidence. The automatic publisher still requires successful
exact-source push/main tuple execution, all six catalog SDK jobs, and fresh
before-tag/before-release checks. Final run identities and publication receipts
must be recorded separately after those gates execute; this preparation document
claims no publication.

The independently downloaded 5.1.0 archive is preparation for the later dedicated
Parties qualification caller. Its original signed payload and signature are
verified separately. A final fixed input selection can be made only after the
compatible Platform source is actually published. Keep that candidate tool
selection on a qualification branch; do not update either main accepted-tool
manifest before the complete local/hosted lifecycle and real cancellation gates
pass. Historical `local.py` remains bound to 4.47.0 and is not a 5.1.0 controller.

All historical evidence, including the supplied preparation record, keeps its
original source, tool, run and decision identities. No lifecycle service or
qualification workflow was run by this source preparation. All acceptance flags
remain false and Story 2.10 remains in progress.
