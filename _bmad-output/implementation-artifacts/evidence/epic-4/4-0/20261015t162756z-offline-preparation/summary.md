# Fresh Story 4.0 offline preparation — 20261015t162756z

The unsigned local packet is complete for preparation review. Its sole current
review input is `packet-v2.json`, SHA-256
`70a721ca7ced74281029c480419d08889658dc07f83844cdcefd10c7f8f99d59`.
The private handoff is `README-v2.md`; the proposed next owner action is
`NEXT-OPERATIONAL-APPROVAL-v2.md`. There is no operational approval or current
recovery claim, and `operationalGate` remains closed.

```text
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/verify_packet.py --packet /home/administrator/hexalith-recovery-evidence/4-0/20261015t162756z --manifest packet-v2.json --sha256 70a721ca7ced74281029c480419d08889658dc07f83844cdcefd10c7f8f99d59
```

Focused isolated tests pass 24/24; actual v2 verification exits 0. A separate
isolated standard-library walk independently recomputed every packet checksum,
348 historical files, 107 inert frozen sources and all 119 initial packet files.
The packet now contains 124 files, all private directories 0700/files 0600.
No operational artifact or executable source is installed in its layout.
Current measurements and operational results remain null with 31 named blockers.

Initial construction refused the installed `timeout` binary's 115 hardlinks.
That output is retained; the verifier now binds the link count for trusted local
binaries while refusing hardlinked packet/history files. A focused regression
covers both. Initial actual packet verification refused a formatting error in
the `spec23` metadata key; the same constructor also changed `actualC3Required`.
Every initial file was retained byte-for-byte. Only additive explicitly selected
v2 bindings/checksums/manifest/README/next-action files correct those names and
bind the final source bytes. The original manifest still refuses. No policy,
RID, preparation time or expiry was refreshed by the correction.

The candidate preparation time is 2026-10-15T16:27:56Z; its unchanged 24-hour
ceiling expires 2026-10-16T16:27:56Z. Capture and acceptance remain null. The
expired 20261014t112353z originals, authentic historical acceptance, failed OIDC
original, prior reviewed packet and historical executors remain unchanged.
The old 162-file inventory had 161 matches and one pre-existing unrelated
Platform sprint-status drift; that exception is preserved separately from the
fresh five-root preservation baseline. No 162/162 old-inventory pass is claimed.

Next approval is restricted to fixed Kubernetes metadata/health/storage/authority
GETs and named provider GETs under existing authority, bounded time/page counts,
strict stop conditions and no retries. Unsafe Docker custody, application
exec/canary access, independent native/recovered SSH authority and unbound
credentials remain blockers requiring further exact measurement proposals.
Capture/restore/cleanup/signatures/fresh C3/final acceptance remain future
operational results. After measured identities and fresh safe adapter review,
exact execution/signing approval is still required. The old preparers, `--dry`,
policy upload path, hardcoded OIDC guards and generic dispatcher are reference
sources only; none may execute as a fresh safe adapter.

The packet preserves OIDC loopback 38080/28080, disabled proxies/redirects and
all eight checks, plus actual Memories C3, one final pre/census, <=900s drain,
paired stores, split semantic gates and immediate already-approved conditional
resume. No source/provider/API call, authentication, private key read, capture,
restore, decryption, signing, publication, retirement, upgrade or deployment ran.
Only permitted local preparation and isolated Python checks ran. Story 3.6,
Epic 3 and 4.27 stay in progress; spec 23 stays draft; 4.1 stays closed; release
stays FAIL. Platform sprint status and unrelated files were not edited.
