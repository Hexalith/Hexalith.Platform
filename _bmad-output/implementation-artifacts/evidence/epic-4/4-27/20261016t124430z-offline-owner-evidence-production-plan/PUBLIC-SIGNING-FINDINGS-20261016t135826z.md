---
title: 'Platform 4.27: public signing findings and sole-owner trust bootstrap'
status: draft
scope: offline-public-only-findings
story_status: in-progress
operational_gate: closed
release: FAIL
---

# Public signing findings and sole-owner trust bootstrap

The existing public key is technically usable for an Ed25519/SSHSIG evidence
format. Its recorded trust mapping permits only historical recovery signing;
it does not establish current owner-evidence authority. No personal X.509
certificate is needed for the proposed bare-key format. The immediate missing
input is an independently authenticated owner-to-key-and-purpose binding, not
another search for a certificate or recovery signing setup.

Jérôme Piquot declares himself project owner and sole contributor and says he
has no personal public certificate. These are planning facts supplied by the
user, not authenticated identity, project mandate or signer authority. No
corporate security team is assumed. A non-contributing independent person can
witness trust establishment; no such person or trusted channel is established
by this document.

This is a separate findings/handoff, not a replacement production plan, trust
attestation, accepted format contract or operational approval. All existing
plans, reviews and evidence retain their original meaning.

## Sources and targeted observations

The four requested handoff documents were read: `PLAN-v3.md`, `REVIEW.md`,
`OWNER-TRUST-CLARIFICATION-20261016t130702z.md` and
`REVIEW-OWNER-CLARIFICATION-20261016t130702z.md` in this directory. They supply
historical status and preservation context; their old searches and validation
were not replayed.

The new inspection followed the user-named public key and public verification
references in `eng/cluster-management/NEXT-OPERATOR-ACTION.md`, particularly
lines 58–66, 90–98 and 248–251. That file was read as documentation. None of
its shell blocks, tools or operational instructions was executed.

| Public input inspected | Actual observation |
| --- | --- |
| `/home/administrator/.ssh/id_ed25519_git_signing.pub` | Regular non-symlink file; all path components inspected without following symlinks. UID 1000, mode 0644, 100 bytes. Bare `ssh-ed25519` key, 32-byte Ed25519 public value; not an SSH certificate or X.509 certificate. Comment `jpiquot@itaneo.com` is an unauthenticated label. Exact file SHA-256 `1eb59a1bd7db64d2ea0dfcb969286c506ccde400183215f3f95be00c73d4c989`. |
| `/home/administrator/hexalith-recovery-evidence/4-0/final-run-tools/allowed_signers.template` | Regular non-symlink public verification metadata, UID 1000/mode 0600, 131 bytes. One principal `jpiquot@itaneo.com`, option `namespaces="hexalith-recovery"`, same exact public-key blob. File SHA-256 `77df53775f890a393578cb5480512b532ef730bcc854a2bf1d4cdd9bb1d77247`. |
| `/home/administrator/hexalith-recovery-evidence/4-0/20261014t112353z/signatures/allowed_signers` | Same 131 public bytes and digest as the template; UID 1000/mode 0600, regular non-symlink. This is a retained expired recovery's verifier mapping, not a new authority register. |
| `/home/administrator/hexalith-recovery-evidence/4-0/20261014t112353z/policy/recovery-policy.json.sig` | Public signature metadata only: ASCII `SSH SIGNATURE` armor, `SSHSIG` magic, version 1, namespace `hexalith-recovery`, payload hash `sha512`, empty reserved field, signature algorithm `ssh-ed25519`, embedded public-key fingerprint matching the named key. File SHA-256 `7f5dc8431113f6f5a993ed7cefda5a427673a3334fff2a380d8a1a3eab715f0e`. The payload was not opened and the historical signature was not cryptographically reverified. |

Actual OpenSSH key fingerprint, computed over the decoded SSH public-key blob:

`SHA256:8XlNQvE3ucPf/e509wU4qtNgiyWA+TKmLei7F7+TCvk`

Exact public line from the named file:

```text
ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIL0Kt34ByT8WvAx325SbxYRNLKBZ3ggbgWomqD1nCHq4 jpiquot@itaneo.com
```

The three public-key blobs match. The fingerprint is not the SHA-256 of the
text file; the SSHSIG message hash `sha512` is a third, different field. None
of those digests establishes who currently owns or controls the key.

The public path is present and no unsafe-path blocker was found in this scoped
inspection. No private counterpart, credential, SSH agent, socket, kubeconfig or
encryption-recipient file was opened or queried. No private-location search
was needed. The only original signature access was structural public metadata
parsing, not a verification replay or a search through signed originals.

For format semantics, the installed local
`/usr/share/man/man1/ssh-keygen.1.gz` documents detached signatures, namespaces,
principal matching, optional key-validity bounds and a separate revocation
input. No online documentation or API was contacted. No `ssh-keygen` signing,
verification or private-key extraction command was invoked.

## What provenance and authority are actually established

The runbook records the retained recovery principal, fingerprint and namespace
and calls the template a pinned single-key recovery root. The inspected template
and expired bundle mapping agree with that record. The inspected signature's
metadata agrees with the same key and namespace. This is a concrete local
provenance trail for the recorded recovery setup, not independent proof of the
human owner's original enrollment or mandate.

The previously recorded eight authentic recovery signatures and old approvals
remain historical facts reported by their records. They were not searched for
or verified again. Their old policy deadline remains
`2026-10-15T11:40:42Z`; they supply neither current freshness nor authority for a
new purpose. The filename's `git_signing` label also does not prove any Git
signing configuration or authorize another use. No Git signing configuration
was inspected.

Both inspected allowed-signers files contain only the principal, recovery
namespace and bare key. They contain no `cert-authority`, `valid-after` or
`valid-before` option and no human-role mandate or independently current
revocation/head record. Lack of those options does not establish indefinite
validity or absence of revocation. SSHSIG has no trusted signing timestamp or
human identity/project ownership built into it; principal selection comes from
the external verifier mapping. Historical label `jpiquot@itaneo.com` is not a
current proof of email control, Jérôme's identity or project ownership.

| Purpose | Recorded fact versus current conclusion |
| --- | --- |
| `hexalith-recovery` | Explicitly permitted by the two historical verification mappings. Runbook scopes this to the retained recovery records; old run-specific signing delegations do not transfer. No present signing authorization, current key control, freshness or nonrevocation is established here. |
| `hexalith-retirement` | Runbook expressly says recovery trust is not a retirement root. No authorization established. |
| Owner evidence, trust designation, format/freshness policy or preservation disposition | No inspected mapping permits these purposes. A proposed new mapping requires independently established owner identity/mandate, explicit narrow purpose and current validity/revocation handling. |
| Server/client TLS identity, CA issuance, native grants/revocations/current head, effective rights, credential or offsite custody, runtime qualification | None follows from a project-owner designation or recovery key. Their distinct source authority and evidence remain required. |
| Independent human review, qualification/execution/activation approval, Timesheets acceptance | Unestablished. The owner key cannot make its own producer an independent reviewer. Agent document review supplies no such authority. |

No claim is made that additional external mandates do not exist. They are not
established by these inspected public records. This handoff changes no existing
trust file and enrolls no signer.

## Technical fit and the concrete tooling gap

**Technical fit: yes for a proposed format; no accepted production path yet.**
Ed25519 with standard detached SSHSIG can authenticate exact original JSON or
JSONL bytes without a personal certificate. A narrow proposed profile is
`ssh-ed25519`, SSHSIG v1, message hash `sha512`, exact original UTF-8 bytes
including their original newline/framing, and a distinct proposed namespace
`hexalith-owner-evidence`. That namespace is a design proposal, not a registered
or authorized purpose. Do not reuse or broaden `hexalith-recovery`, and do not
rewrite the historical allowed-signers files.

For an initial format-contract document, use one detached `.sig` over the whole
original payload. The payload and an independently established authority entry
must both constrain project, schema/version, purpose, signer role, relevant
input/revision digests and validity; a namespace alone cannot distinguish all
those roles. Preserve original serialization, byte count and SHA-256 inventory;
never verify a reserialized approximation or only an unauthenticated digest.
A future JSONL consumer must define whole-file versus per-record signature
framing and exact original spans before new production. The existing plan's
schema, parser-bound, freshness, revocation and separate-review obligations stay
open. Neither the key nor SSHSIG supplies currentness or authoritative facts
inside a signed payload.

`PLAN-v3.md` records I1's missing production public-key/role/format verification.
The two proposed files
`eng/recovery-preparation/owner_evidence_contracts.py` and
`eng/recovery-preparation/verify_owner_evidence.py` are currently absent at
those exact paths (not symlinks). This targeted existence check establishes no
repository-wide absence claim. The concrete next tooling increment is a
reviewed offline original-byte SSHSIG consumer plus a separately trusted,
purpose-constrained authority input, bounded schema/framing checks and current
validity/revocation input. Reuse a standard SSHSIG verifier; do not invent new
cryptography. Pin verification algorithms/namespace outside the submitted
assertion. Reject altered bytes, wrong key/namespace/purpose, self-supplied
roots, stale/revoked keys and absent independent review. No implementation,
fixture, test or verifier execution occurs here, and the closed sealed verifier
must not be patched to accept owner evidence or preservation drift.

## Smallest sole-owner bootstrap proposal

The smallest useful trust step is **one independently authenticated offline
owner-to-public-key-and-purpose designation**, observed by an independent
non-contributor. It establishes a narrow trust mapping, not all downstream
authorities or operational approval. It needs no personal certificate, new
keypair, corporate department or reconstructed recovery history.

The concrete candidate to take to that step is already known: the exact
100-byte public file and fingerprint above. Propose Jérôme as project trust
owner and this key solely for **owner-side evidence-format-contract signing**
as the initial purpose, under proposed namespace `hexalith-owner-evidence`.
That limited designation does not authorize every evidence class, grant,
custody claim, reviewer signature or operational approval. Broader purposes
would need distinct mandate evidence and review later.

The independent verifier must be able to authenticate Jérôme's identity and
project-owner mandate through a pre-existing basis outside this workspace and
outside the proposed assertion: for example, their already established direct
knowledge of the person and project mandate, or an independently authenticated
retained project-ownership record. These are candidate methods, not claims that
such knowledge or records exist. Merely choosing a witness in chat is not
trust establishment. The eventual relying verifier must already trust that
person/channel for this identity-and-mandate determination; otherwise the
bootstrap remains blocked rather than introducing another self-asserted root.
No such witness, authority basis or channel has been supplied by the inspected
records. No provider or repository-host request is needed or permitted here.

In that independently authenticated encounter, the witness compares the full
public bytes and fingerprint with the owner's independently presented public
copy and records the actual designation, exact initial purpose, project scope,
source/method, named identities and independence, observation time, validity
and revocation/replacement contact mechanism. A second copy from this same
workspace alone does not provide independent provenance. Retain the observation
through an independently authenticated witness channel and bind any eventual
signed witness record to a separately established witness key; never trust a
new adjacent witness public key merely because it signs the record. No witness
record or designation is manufactured by this handoff.

This first designation is a trust-establishment observation; it does **not**
prove current possession of the corresponding private key. Before first
production use, a separately scoped, independently reviewed format and explicit
signing authorization would allow an offline fresh-challenge proof of control
under the exact new purpose. The challenge must be bound to that authenticated
encounter, project and designation to prevent replay. Historical signatures
cannot supply that proof. No challenge, signature, unlock or private access is
performed, requested or allocated here. New signing must wait for the reviewed
format contract; an external authenticated designation must precede trusting
that contract's signer, so the new decoder does not bootstrap itself.

Validity/freshness and a current revocation/replacement channel must be made
concrete and independently checkable before accepting production evidence.
The designation observation alone is not a current nonrevocation attestation,
an accepted `trust-root-establishment.json`, or a substitute for the original
producer/reviewer signature requirements. No missing authority field is filled
with an agent assertion. If no pre-existing independent identity/ownership
basis can be established, this is the exact trust blocker; another self-signed
JSON or a new personal certificate would not remove it.

**First genuinely necessary owner decision:** select the independent verifier
and the pre-existing identity/project-ownership basis and channel that the
verifier can actually authenticate for this narrow key designation. The key,
fingerprint, historical principal and format no longer need to be supplied or
rediscovered. Reuse for the proposed new purpose remains a subsequent explicit
authenticated owner designation, not an assumed authorization.

**One concrete next step:** arrange that offline, independently witnessed
comparison and narrow designation using the already identified public key;
retain its actual provenance for separate format/trust-contract review. This
is a proposed next task, not an operational approval prompt or permission to
sign or authenticate in the present task.

## Time, workflow scope and preservation

Actual clock-tool observations: `2026-10-16T13:54:16Z`,
`2026-10-16T13:57:09Z` and `2026-10-16T13:58:26Z`, each strictly before the
unchanged ceiling **`2026-10-16T16:27:56Z`**. These are documentation observations,
not recovery preparation/capture/approval refreshes. Preparation stays
`2026-10-15T16:27:56Z`, RID `20261015t162756z`. At or after the ceiling, stop
all candidate-dependent qualification, enrollment, production,
review-for-activation and execution. Only historical assessment may continue,
with that expiry stop stated. This document does not extend any interval or
allocate replacement recovery. Independent trust establishment cannot revive
an expired candidate or the old recovery.

The exact recorded verifier remains **exit 1** at
`2026-10-16T11:25:36.164697Z`, reason
`offline-revision-integrity-or-custody-refusal`. Its result was not replayed.
Final sealed manifest SHA-256 remains
`c71f38517e02ea5bc7fa176cfb1abe8f12b43b499fbe6f74fbdc339d91f0b23d`.
The sealed exception remains bound to
`f6c25046d9db40c069cb183cf87f8fef19a0a952b601c87dbe690d0b0aff7e43`;
additional Story 1.8's recorded observation remains
`c94f8fedcc4d9e362985c3056c2a2b3e6628ef74b525ed377b92f7cf920a12c5`.
Neither is replaced by a newly observed hash or owner assertion. Prior Story
1.7 drift and subsequent concurrent changes remain undispositioned here.

No writes are made to either preparation directory:

- `/home/administrator/hexalith-recovery-evidence/4-0/20261015t162756z`
- `/home/administrator/hexalith-recovery-evidence/4-0/20261016t103632z-native-enrollment-execution-revision`

The sealed revision, immutable packets, signed originals, expired recovery
`20261014t112353z`, original modes, failures, earlier drafts/reviews, concurrent
changes, index and gitlinks are preserved by the write boundary. No new
full-byte preservation audit or historical acceptance is claimed.

Read-only Git observation on `main` returned canonical HEAD
`d9bda286ce6e04b7f3e76a1a775ef78963e144c0`; status with submodule worktrees
excluded returned no tracked/untracked changes at that moment. This is a
scoped working-tree observation, not submodule validation or a new sealed
baseline. It does not disposition intervening commits or concurrent changes.
No Git write operation occurs.

The bmad-build renderer was run exactly once successfully. Relevant clarify,
plan, template and one-shot implementation/review instructions were read.
The user's create-only findings scope overrides new general-spec/plan creation,
existing spec/status appends or promotion, operational checkpoints and automatic
commits. For this documentation deliverable: no unresolved intent gap, no
irreversible action; footprint is new findings and review documents plus the
renderer cache. The remaining trust decision is reported, not solicited here.
No ready-for-development or operational sufficiency claim is made.

No authentication, network/API/provider/IAM request, preflight/verifier replay,
key generation, signing, publication, infrastructure/operational action,
staging, commit, push or dependency update occurred. No completed evidence
search, test, implementation or historical signature verification was repeated;
new work was restricted to the named public signing setup and findings.

**Spec-23 draft; Story 3.6/Epic 3/Platform 4.27 in progress;
4.1 operational gate closed; release FAIL.** Existing state files are untouched.
