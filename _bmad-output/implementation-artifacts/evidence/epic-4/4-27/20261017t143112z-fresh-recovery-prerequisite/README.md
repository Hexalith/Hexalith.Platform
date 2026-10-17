# Fresh offline recovery prerequisite for Timesheets 3.6

Prepared **2026-10-17T14:32:02Z**, RID **`20261017t143202z`**, exclusive
preparation ceiling **2026-10-18T14:32:02Z**. The user expressly authorized fresh
offline preparation and owner self-review of project documents. This new
unsigned prerequisite replaces no historical record. It is not a current
recovery point. Story 3.6 and Platform 4.27 remain in progress, spec-23 draft,
the 4.1 operational gate closed, and release FAIL.

The private create-only packet is
`/home/administrator/hexalith-recovery-evidence/4-0/20261017t143202z`.
Its independently retained `packet.json` SHA-256 is
**`d14efe6e8e82d983187416392ccc77207b446827f149caf3b45ed4b1ae90939a`**.
The [preparation receipt](preparation-receipt.json) records actual exit 0.
Its [exact executed command](preparation-command.json) retains every input and
the owning working directory.
The [local consistency receipt](verification-receipt.json) records actual exit
0, `candidateExpired: false`, twelve unresolved complete-prerequisite
categories and `operationalGate: closed`. It verifies local bytes and custody;
it authenticates no owner evidence and supplies no operational approval.

## Historical preservation

The expired candidate remains RID `20261015t162756z`, prepared
`2026-10-15T16:27:56Z`, with unchanged exclusive ceiling
`2026-10-16T16:27:56Z`. It remains expired permanently. The new packet retains
the original candidate policy bytes, SHA-256
`a4be017625ba312ab93d76d71bbbf8433f84c08ed43d798c892a39da8939fcdd`, and the
original Timesheets verifier-refusal receipt bytes, SHA-256
`38610efadcc9e0358a806e9178c9de75cb737079e864e5186cee21c744d6f810`.
These two explicit input reads are not a whole-history preservation sweep.
The old preparation files, sealed supplement/revision, failed artifacts and
verifiers received no writes, and the original verifier was not executed.

The retained original refusal remains **exit 1** for sealed revision SHA-256
`c71f38517e02ea5bc7fa176cfb1abe8f12b43b499fbe6f74fbdc339d91f0b23d`, observed
2026-10-16T11:25:36.164697Z, reason
`offline-revision-integrity-or-custody-refusal`. A new tool's consistency pass
does not revise, waive or replace that refusal. Historical capture, acceptance
and policy times are never refreshed.

## New capability and actual observations

Platform owns the separate offline
[`fresh_prerequisite.py`](../../../../../../eng/recovery-preparation/fresh_prerequisite.py)
preparer/verifier and its
[contract](../../../../../../eng/recovery-preparation/FRESH-PREREQUISITE.md).
It holds non-symlink directory descriptors during creation, uses exclusive
creation, writes 0700/0600 private custody, preserves partial failures, pins
both historical originals and retains the old policy decisions unchanged.
It creates only new proposed names and an unsigned policy. It has no live
mode, subprocess, credential loader, signing, provider or network capability.

[Source attribution and focused test results](source-and-tests.json) bind the
exact new sources: **11/11 synthetic offline tests passed, no skips**. They
cover collision refusal, old-byte/date preservation, substitution of original
records, expiry, future dates, payload/source drift, forged authority,
retained-policy drift, unsafe custody, duplicate keys/roles and racing ancestor
replacement without writes to the replacement directory. Syntax, whitespace
and final-newline checks passed. No unchanged historical test suite or exact
verifier was replayed.

The packet binds the finalized Timesheets `native-observation.json` at
`_bmad-output/implementation-artifacts/evidence/3-6/20261017t142841z-deployment-authority-runtime/`
and EventStore's `docs/evidence/story-3-6/20261017t142649z-production-authority-preparation/current-rights.json`.
The former actually observes the expected native cluster UID
`b5860674-1d1e-4a3b-b413-e1bb3b9a4557` at
`https://192.168.1.30:6443`, current authenticated user `jpiquot`, matching public
client-certificate identity and complete current wildcard resource/nonresource
rules at its recorded cut. A separate TLS 1.3 connection verifies the served
peer's CA and IP SAN using OpenSSL 3.5.5; that particular TLS probe did not
present the client certificate. The authenticated client observation comes
from the existing kubectl identity query. The bounded rights receipt records
twelve current allowed checks. These are useful present observations. They
do not establish historical grants/revocations, independent recovered-client
custody or the old frozen parser/runtime profile.

The native observation found no Timesheets deployment in its inspected kinds;
it supplies no deployed Timesheets confirm/adjust, single-use, replay, rebuild
or restart result. The current source and test record attributes Platform
`d186ad667639b9d629f7de51921e9600e7f7f992` and Timesheets
`a376b71d43330a716f836c3086e562e505511dd3` as local HEADs. Working source
changes are neither a published revision nor a deployed artifact.

## Proposed targets and operational boundary

The unsigned policy proposes only:

| Purpose | Fresh proposed identity |
| --- | --- |
| Keycloak pretest namespace | `s40-kcpre-20261017t143202z` |
| Keycloak restore namespace / cluster / application | `s40-kc-20261017t143202z` / `s40-postgres-20261017t143202z` / `s40-oidc-20261017t143202z` |
| OpenBao restore namespace | `s40-bao-20261017t143202z` |
| Memories restore namespace | `s40-mem-20261017t143202z` |
| Proposed off-node evidence prefix | `evidence/4-0/20261017t143202z/` |
| Proposed payload prefixes | `keycloak/20261017t143202z/`, `openbao/20261017t143202z/`, `memories/20261017t143202z/` |

None was created or accepted. Their actual UIDs are null. Historical destination
policy decisions, RPO, retention and custody choices remain unchanged in the
unsigned policy; their present configuration is not inferred.

The complete recovery prerequisites remain unresolved: current revocation and
protected credential custody; source continuity and full legacy inventory;
writer/delivery quiescence and maintenance/resume evidence; provider policy,
IAM and independent immutable readback; qualified fresh capture/restore/cleanup
adapters; actual fresh captures and isolated restores; exact operational
approval; and deployed Timesheets acceptance. The packet retains explicit
nulls for complete categories even where the bound observations supply a
partial fact. Owner self-review removes the earlier distinct-human documentary
ceremony; it cannot create those facts or operational permissions.

No recovery action is ready for final approval. The new tool intentionally
provides no live dispatcher or command that could select a historical executor.
Before a recovery mutation, prepare a separate exact reviewed argv bundle
bound to these fresh targets, current source/destination identities and tool
digests, bounded expected effects, failure retention and target-only cleanup /
approved source-resume procedure. Unknown custody, inventory or adapter facts
must be resolved before asking to execute that bundle. Deployment approval is
separately prepared by the Platform hosting work and is not granted here.
There was no deployment, recovery, migration, signing, publication, outage,
permission change, staging, commit, push or submodule update in this increment.
