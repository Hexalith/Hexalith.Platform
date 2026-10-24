# Review-5 offline native certificate candidate

This packet binds the corrected sources, seven suite logs, finite cancellation
controls and executable receipt checker separately from every prior candidate.
The original baseline remains `693390a790d95a9bad9b1bd9cc0f0f3052f3fbef`;
`preparation-head.txt` and `current-head.txt` are separate historical/current
observations. Full spec dispatch/verification checkpoints are historical bytes;
later workflow edits may differ while frozen human intent must remain exact.

Replay from the repository root with the recorded Python 3.14 interpreter.
The recorder replaces outputs beside its own script. Copy it to a fresh output
directory first; leave the packet's bound logs and command results untouched:

```bash
review5_packet=_bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/review-5
review5_replay=$(mktemp -d /tmp/bmad427-review5-replay.XXXXXX)
cp "$review5_packet/run-seven-suites.py" "$review5_replay/run-seven-suites.py"
/usr/bin/python3.14 -I -S -B "$review5_replay/run-seven-suites.py"
/usr/bin/python3.14 -I -S -B "$review5_packet/run-cancellation-sweeps.py" > "$review5_replay/cancellation-sweeps.json"
/usr/bin/python3.14 -I -S -B "$review5_packet/verify-receipt.py" --self-test
/usr/bin/python3.14 -I -S -B "$review5_packet/check-parser-mutant.py"
```

The recorder keeps its existing containing-directory output contract. These
replay outputs remain separate observations and do not rebind the packet.

The checker is content-based and needs no preparation Git HEAD or index. It
requires independent complete source and supporting-file inventories (every
packet file except the self-referential receipt), seven exact named commands,
plain OK/zero skips and exact verbose executed class/method source inventories.
It also requires the five exact verification command/log records and pins the
complete preserved inventory independently of rebound receipt metadata. It
parses the bound cancellation results and reconciles counts, release and
propagation flags and terminal classifications. Its isolated copies exercise
coherent source/snapshot and metadata drift, real adverse transcripts with
required identities, unrelated/repeated same-count plain-OK transcripts,
empty/omitted inventories and omitted-dependency drift, adverse sweep outcomes,
count/terminal mismatches, false release flags, verification-command omissions,
and empty/omitted/rebound historical inventories with actual corruption. The separate parser mutant
must still be detected with the expected test identities intact.

The original parent probe scripts are `historical-parent-*.py`; their original
logs/results and complete independent run are under `pre-correction/`. Those
records describe review-4 defects and success boundaries, not qualification of
this corrected candidate. `prior-packet-sha256.json` binds all earlier packet
bytes; no earlier checker, receipt, transcript or checkpoint was rewritten.

The exact earlier 335-test candidate, including its seven-suite and sweep
results, is preserved at `../review-5-first-candidate/`. Independent final
verification passes **336 tests, zero skips**: 60 certificate cases and 276
boundary/readback/authentication/S3/common/telemetry cases. Current finite sweeps
pass 307 line, 2,341 opcode, 93 secondary and 32 prepared-entry targets with no
findings or unraisable exceptions. One terminal cancellation is classified only
after exact peer/descriptor release. The complete 215-file historical inventory
and every prior positive test remain preserved. Parent full verification
supersedes the explicitly provisional focused-correction checkpoint.

This is finite process-local qualification, not protection from arbitrary hostile
descriptor mutation, unsupported closers, indefinitely repeated cancellation,
kernel stalls or a signal-atomic terminal return.

All credentials and keys are public synthetic fixtures. Real SSLContext and
MemoryBIO exercise exact identities, pinned CAs, default/TLS 1.2 protocol paths,
native wire bytes and source/target composition without sockets. The unsigned
receipt provides reproducibility, no external authenticity or authority. No
actual credential/configuration/endpoint, operational issuance, allocation,
signing/upload/recovery, activation or infrastructure mutation occurred. Story
4.27 remains in-progress, 4.1 closed and Timesheets 3.6 open/FAIL. All actual
credential/authority/runtime, system/target/proof-consumer and recovery
qualification remains pending. No staging, commit, push or helper replay.
