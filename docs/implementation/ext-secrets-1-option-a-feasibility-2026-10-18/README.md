# Ordinary Raft non-revival negative control

This bundle retains sanitized observations from a disposable local OpenBao 2.6.2 instance using fresh test keys. It demonstrates a deleted native transit key returning after ordinary pre-deletion Raft snapshot recovery. It is not an EXT-SECRETS-1 provider, EventStore owner qualification, all-copy erasure proof, or complete production target.

`native-result.json` retains the exact probe source/configuration hashes, native image/revision, per-stage command statuses, target/control observations and cleanup result. `native-command.json` retains the exact outer invocation and its log hashes. Private stdin, tokens, unseal keys, plaintext, ciphertext and snapshots are omitted from these files. This does not establish erasure of host Python/subprocess memory, swap or core dumps.

From this directory, verify the retained bytes with:

```bash
sha256sum --check SHA256SUMS
```

The original attempt records and broader source/input bindings remain in the evidence directory named in the [decision record](../ext-secrets-1-dek-provider-decision-2026-10-18.md). The engineering implementation and focused tests are [the probe](../../../eng/ext-secrets-1/probe_raft_non_revival.py) and [its tests](../../../eng/ext-secrets-1/test_probe_raft_non_revival.py).

Exit 2 / `ObservedRevival` means the ordinary-snapshot non-revival requirement failed. Exit 1 means an incomplete or unknown observation. Neither qualifies G1. Production engine construction and the atomic manifest proof remain stopped pending a concrete independently surviving erasure mechanism and complete copy policy.
