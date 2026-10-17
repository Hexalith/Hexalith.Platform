# Fresh offline prerequisites after the expired 20261015 candidate

`fresh_prerequisite.py` prepares a **new unsigned, incomplete prerequisite**.
The actual preparation clock defines its new RID and exclusive 24-hour ceiling.
It creates new attempt-scoped proposed recovery targets. It never changes the
expired RID `20261015t162756z`, its preparation or expiry, sealed sources,
signatures, original failure, old verifiers or historical acceptance.

This is preparation freshness only. It supplies no current capture, restore,
authority, signing permission, deployment permission or Timesheets acceptance.
It cannot execute a command, open a credential loader or contact a provider.
All operational results remain null and every operational gate remains closed.

The source candidate's retained policy decisions are copied unchanged into the
new unsigned policy. They are historical decisions, including proposed source,
destination, custody and signer references. Their presence does not establish
current environment facts. The source policy, original verifier refusal and
latest owner documentary policy are also retained as exact original byte copies.
The original verifier remains refused, exit 1; this tool never runs it.

The owner's current policy permits owner self-review of owner documents. It
does not supply actual PKI, authorization grants, provider rights, maintenance,
inventory, continuity or runtime observations. Optional `--record ROLE PATH`
arguments bind already sanitized public/local records by their exact bytes.
They do not authenticate those records or promote their claims. Never pass a
credential, kubeconfig, secret-bearing response, raw log or encrypted payload
as a `--record`. Use finalized records: later drift correctly refuses offline
verification. At most 32 distinct record roles are allowed.

The output parent must already exist. The preparer holds non-symlink directory
descriptors through creation, refuses an existing RID, creates a 0700 directory
and 0600 files, and flushes them to disk. Partial failed attempts are retained;
there is no automatic deletion, retry or replacement allocation. A separately
retained `packet.json` SHA-256 anchors subsequent local consistency checks.

```bash
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/fresh_prerequisite.py prepare \
  --output-parent /home/administrator/hexalith-recovery-evidence/4-0 \
  --prior-policy /home/administrator/hexalith-recovery-evidence/4-0/20261015t162756z/policy/recovery-policy.candidate.json \
  --original-refusal /home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/evidence/3-6/20261016t112503z-offline-owner-evidence-intake/exact-verifier-result.json \
  --owner-policy /home/administrator/projects/hexalith/timesheets/references/Hexalith.Platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261017t093206z-sole-owner-documentary-policy/sole-owner-policy.json
```

For the actual RID and manifest digest returned by that exact invocation:

```text
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/fresh_prerequisite.py verify --packet <new-absolute-directory> --sha256 <retained-packet-sha256>
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/test_fresh_prerequisite.py
```

`consistent-unsigned-incomplete` establishes only local byte/custody consistency.
The result explicitly reports expiry independently and always returns
`operationalGate: closed`, `approvalGranted: false`, `recoveryCurrent: false`
and `timesheetsAcceptance: false`. A candidate remains inspectable after its
ceiling; expiry never changes its dates or makes it current again.

The twelve prerequisite categories remain explicit nulls even if a supplied
record contains a useful partial observation. A later factual assessment must
resolve their actual scope rather than changing these immutable snapshots.
Recovery still requires exact current sources, destination protections,
qualified capture/restore/cleanup adapters, proposed target identities, current
maintenance/resume scope and the owner's concrete operational approval.
No live recovery command is offered by this preparer. Existing historical
executors are never selected by a fresh RID or an offline verification pass.
Timesheets separately owns deployed Story 3.6 acceptance and remains incomplete.
