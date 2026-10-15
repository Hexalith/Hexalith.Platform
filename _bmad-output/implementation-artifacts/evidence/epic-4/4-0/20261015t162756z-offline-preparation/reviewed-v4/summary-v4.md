# Final reviewed v4 offline preparation closeout

Active manifest: `packet-v4.json`, SHA-256 `1ed5eec5579adeedf7e49519fcba125656caacbaaeaffb6509c49b6575714b14`.
Its explicit local verifier returned `offline-preparation-pass`, exit 0, with
`operationalGate: closed`, `approvalAuthenticated: false` and
`currentRecoveryClaimed: false`. The 34 focused verifier tests and 12 synthetic
preflight tests pass. The parent independently reran those same focused suites.
No actual static transport preflight or actual kubeconfig content read ran.

The final packet contains 140 files (139 payloads excluding its active manifest),
with all 132 prior v1/v2/v3 files unchanged, 348 preserved historical files and 107
inert frozen historical sources. Eight v4 files were created only after the
parent's explicit v4 seal clearance. Earlier manifests are pinned to independent
reviewed digests in verifier source; candidate hashes cannot replace them.
Every original source, signed record, failed original, old packet and policy/RID/
preparation/expiry timestamp remains unchanged. All 31 named blockers remain.

The immediate proposal is exclusively the Administrator's protected LOCAL
static transport schema preflight in `NEXT-OPERATIONAL-APPROVAL-v4.md`, SHA-256
`6fbf05a4b39e960faf6767525f174f11809ad4fb42142efe0d12730974b0a44f`. The fixed local binding
`static-transport-preflight-v4.json` has SHA-256
`33e92de1a846f43f7f09f9a7e4a341e039b7e5e40ac1110ab2ef224b4c061c30`. The active operation plan
binds this exact request filename/digest. No inherited network approval is active:
all Kubernetes/API/provider/IAM/application/SSH/Docker and 4.1 lanes stay excluded
or BLOCKED until separate exact adapter/runtime/authority qualification and
separately reviewed approval. Future selectors are inert reference only.

The exact next proposed owner request is:

> Approve only the Administrator's protected local execution of the exact
> static transport preflight bound below for RID 20261015t162756z. Permit
> reading only the fixed owner-only kubeconfig into protected memory after
> its exact digest/custody check, and the exact local kubectl config-view
> parser over a sealed in-memory copy. Permit no authentication, API/provider/
> IAM/network request, private-key file read, secret output, credential grant,
> capture, restore, decryption, signing, publication, acceptance or later stage.
> On any mismatch or unknown result, stop and return the sanitized refusal.

The proposed protected invocation, which has NOT run, is:

```text
/usr/bin/python3.14 -I -S -B /home/administrator/projects/hexalith/platform/eng/recovery-preparation/static_transport_preflight.py --binding-sha256 33e92de1a846f43f7f09f9a7e4a341e039b7e5e40ac1110ab2ef224b4c061c30
```

Successful future local receipts bind UTC observedAt and exact input digests;
API0/authfalse/authorityfalse/closed remain required. Raw config, invalid args,
supplied invalid hashes and child diagnostics never reach output. Input is a
sealed owner-only 0600 memfd; stdout is streamed to a 2 MiB cap and stderr counted/
discarded at 64 KiB under one 10s deadline. Overflow/timeout kills/reaps only the
owned process group. Synthetic tests use synthetic files and harmless local
child processes or mocks; actual binding/config contents are never test inputs.
No actual source auth, API/provider call, capture/restore, decryption, signing,
publication, retirement, upgrade, Rancher or Timesheets action ran.

The reviewed tests reject each Memories field omission and weakened boolean/
901s-drain/single-store value; removed-guard and key-only mutations are detected.
A joint README/checksum/original-manifest rewrite is rejected against anchors
fixed before attack. Existing 0755 nonwritable config-parent custody is supported;
fresh binding parent 0700 and private config 0600 remain required without chmod.

Preparation stays 2026-10-15T16:27:56Z; unchanged 86400s policy ceiling expires
2026-10-16T16:27:56Z. Capture/acceptance remain null. Story 3.6 / Epic 3 / 4.27 stay
in progress, spec 23 draft, 4.1 closed, release FAIL. Prior failures/source snapshots
and all sealed v1/v2/v3 evidence are retained. Exact payload/source hash tables,
31 blocker descriptions and receipts are in `summary-v4.json`.
