---
title: 'Platform 4.27: scoped GitHub ownership-source check'
status: draft
scope: create-only-read-only-github-source-findings
story_status: in-progress
operational_gate: closed
release: FAIL
revision: 2
---

# Actual GitHub ownership-source findings

**Source result: all three authorized records retrieved, HTTP 200.** At the
recorded observation times GitHub reported account `jpiquot` as an active
Hexalith organization owner and as an effective administrator of
`Hexalith/Hexalith.Platform`. The subject and organization IDs match across
responses. This establishes the observed hosting roles only. The owner-side
trust bootstrap remains incomplete; no human identity, account control,
project trust-owner mandate, private-key control or signing-purpose authority
is accepted by this result.

The two required inputs were read:
[ownership-source findings v2](OWNERSHIP-SOURCE-FINDINGS-20261016t143546z-v2.md)
and [its independent review](REVIEW-OWNERSHIP-SOURCE-FINDINGS-20261016t143546z.md).
This create-only v2 corrects the transport-provenance wording and carries
forward the missing reliance and possession-proof qualifications identified
by independent review. The [first draft](GITHUB-OWNERSHIP-SOURCE-FINDINGS-20261016t150441z.md)
is retained unchanged. This is a source-result handoff following the exact
prior lead, with no replacement general plan. A separate independent agent
documentation review is to be
recorded in
[the new review record](REVIEW-GITHUB-OWNERSHIP-SOURCE-FINDINGS-20261016t150441z.md).
That review cannot supply any required human review or trust acceptance.

## Clock and actual source observations

The first action checked actual UTC using the clock tool:
**2026-10-16T15:03:04Z**, before the unchanged ceiling
**2026-10-16T16:27:56Z**. Clock checks immediately before the three reads were
`15:04:41Z`, `15:05:02Z` and `15:05:18Z`. Each request helper also checked local
UTC immediately before dispatch and bounded its timeout by the remaining
ceiling (at most 20 seconds). All three completed before expiry.

Each invocation addressed the genuine **`https://api.github.com`** origin
using the exact authorized absolute HTTPS URL. The normal GitHub CLI transport
was used. Its automatic redirect behavior was not independently audited; the
reported final response status does not expose or establish every HTTP hop.
No redirected project identity was accepted. These observations do not claim
a transport-level audit excluding additional automatic requests.
Observation times are actual local UTC at dispatch and completion, not provider
issuance times, preparation timestamps, approvals or a new validity interval.

| Order | Authorized source path and method | Dispatch UTC | Completion UTC | HTTP status / CLI exit |
| --- | --- | --- | --- | --- |
| 1 | `GET /orgs/Hexalith/memberships/jpiquot` | `2026-10-16T15:04:41.775259Z` | `2026-10-16T15:04:42.109383Z` | `200` / `0` |
| 2 | `GET /repos/Hexalith/Hexalith.Platform` | `2026-10-16T15:05:02.116479Z` | `2026-10-16T15:05:02.610757Z` | `200` / `0` |
| 3 | `GET /repos/Hexalith/Hexalith.Platform/collaborators/jpiquot/permission` | `2026-10-16T15:05:18.552955Z` | `2026-10-16T15:05:18.860276Z` | `200` / `0` |

The following are returned values, not expected values copied from the prior
procedure. The API enum `role=admin` on an active organization membership is
the observed organization-owner role; the repository permission was obtained
from the account-specific endpoint, not the repository caller-permissions field.

| Record | Actual retained fields |
| --- | --- |
| Membership | `state=active`; `role=admin`; `user.login=jpiquot`; `user.id=6775094`; `user.node_id=MDQ6VXNlcjY3NzUwOTQ=` |
| Membership organization | `organization.login=Hexalith`; `organization.id=80614290`; `organization.node_id=MDEyOk9yZ2FuaXphdGlvbjgwNjE0Mjkw` |
| Repository | `id=1328589450`; `node_id=R_kgDOTzCqig`; `full_name=Hexalith/Hexalith.Platform`; `html_url=https://github.com/Hexalith/Hexalith.Platform` |
| Repository owner | `owner.login=Hexalith`; `owner.type=Organization`; `owner.id=80614290`; `owner.node_id=MDEyOk9yZ2FuaXphdGlvbjgwNjE0Mjkw` |
| Account-specific repository permission | `permission=admin`; `role_name=admin`; `user.login=jpiquot`; `user.id=6775094`; `user.node_id=MDQ6VXNlcjY3NzUwOTQ=` |

Membership was read first. Only after its success and matching subject, active
state and owner role was repository metadata requested. Only after the exact
project locator and the repository owner's IDs matched the membership
organization was the permission endpoint requested. The returned permission
subject matches the membership login and both user IDs. No mismatch, failed
HTTP response, missing required ID or customized role was observed. No missing
read access blocked this check. A 403/404 would not establish absence of
membership; neither occurred here.

## Source-observation provenance and limits

The direct observer was the parent Codex execution session `/root`, through
local GitHub CLI invocations of `gh api --hostname github.com --method GET
--include` with the exact absolute HTTPS source URL for each record. GitHub CLI
resolved existing authorized access internally for ordinary authenticated API
reads. No fresh login, authentication refresh or credential creation was
initiated; the agent did not inspect credentials, cookies, authentication
headers or session/account diagnostics. Update notifiers and prompts were
disabled and debug logging was suppressed. Exactly these three API invocations
were issued; no pagination, retry, account enumeration, `/user` read or other
provider operation was intentionally requested. Uninstrumented automatic
redirect behavior remains the transport-provenance limitation stated above.

The normal GitHub CLI HTTPS transport supplied source authentication for these
direct observations. No TLS verification bypass was requested. Response status
and a field allowlist were extracted in memory; only the nonsecret values
above were emitted and retained. Full profiles, response bodies, arbitrary
headers and credentials were not retained in artifacts. The CLI caller's
account identity, credential type and scope were not inspected or established.
An authorized read of `jpiquot`'s record does not demonstrate that the caller,
the user presenting this task, or Jérôme controls `jpiquot`.

The retained summary and execution-tool output are agent-observed provenance,
not a GitHub-signed attestation or a separately authenticated human identity
record. **Independent downstream authentication of this retained observation
is unestablished:** no relying human verifier has been appointed, no direct
human observation is claimed, and no pre-established accepted evidence channel
or reliance policy has been supplied. A document hash identifies bytes only.
The independent agent review checks the summary against the supplied sanitized
execution results; it cannot independently reauthenticate GitHub or manufacture
human acceptance. Relying-human acceptance, an explicit freshness policy and
source verification through that accepted channel remain unestablished. The
observations say nothing about permission persistence after their stated times.

## Bindings kept separate

| Binding | Result of this check |
| --- | --- |
| Jérôme Piquot's human identity | Unestablished. No independently authenticated Jérôme-to-user-ID `6775094` binding was supplied. Profile names, account roles, chat declarations and contribution history are not used to infer it. |
| Control of account `jpiquot` | Unestablished. These three lookups identify the subject of provider records, not the human controlling the account or the authenticated caller. No account-control proof was performed. |
| Current hosting-owner permissions and project trust-owner mandate | Hosting result observed: active organization-owner membership and effective Platform repository `admin` / `admin` role with matching IDs. Hexalith, organization ID `80614290`, is the repository owner. The narrow Platform trust-owner mandate and the relying policy accepting it remain unestablished; hosting permissions alone do not supply them or legal/IP ownership. |
| Private-key control | Unestablished by this task. No key, SSH agent or historical signature was inspected; old signatures and provider roles cannot establish current possession or custody. |
| Signing-purpose authority | Unestablished. Proposed namespace **`hexalith-owner-evidence` remains unauthorised**. There is no designation, challenge, signing authorization or signature, and historical recovery authority does not transfer. |

Jérôme's declared ownership, sole contribution and lack of a personal
certificate remain planning context. No additional contributor or compulsory
witness is required. A pre-existing independently authenticated ownership record
may supply the identity and mandate bindings. Any existing independent human
reviews remain separate and unsatisfied by an agent documentation review.

**One concrete next step:** supply the nonsecret extract of a pre-existing
independently authenticated ownership/identity-and-mandate record that links
Jérôme Piquot to GitHub user ID **`6775094`** and the narrow Platform trust-owner
mandate for repository ID **`1328589450`**, through an evidence channel accepted
by the intended relying human. The record's location and its acceptance are
not established here. This is a missing-record handoff, not an appointment,
acceptance, signing instruction or operational approval prompt. Supplying that
record alone would still leave account/private-key control, the actual
purpose-specific designation, and independent relying-human acceptance of the
retained GitHub observations through an accepted evidence channel with an
explicit freshness policy unestablished. At or after the fixed ceiling, this
next step can only inform historical assessment of this bootstrap.

The preserved [possession-proof restriction](OWNERSHIP-SOURCE-FINDINGS-20261016t143546z-v2.md#five-bindings-that-must-remain-distinct),
corrected under O5 in its independent review, still applies: any future fresh,
one-use possession challenge belongs only inside the exact independently
reviewed format-contract payload, bound to the authenticated encounter, project,
designation and exact key. Signing those exact bytes requires separate explicit
authorization. No arbitrary challenge signature or separate proof-signing
purpose is permitted. This source check produces no challenge or signature.

## Fixed stop and preservation

Preparation remains **2026-10-15T16:27:56Z**, RID `20261015t162756z`;
the ceiling remains **2026-10-16T16:27:56Z**. This task completed its provider
reads before that ceiling and makes no further provider requests. At or after
expiry, stop candidate-dependent verification, qualification, enrollment,
production, review-for-activation and execution; record an actual expiry stop
in a historical-assessment handoff if work continues then. Nothing here
extends recovery, refreshes preparation timestamps or allocates a replacement.
This is not evidence that expiry has already occurred.

Recorded verifier **exit 1** at `2026-10-16T11:25:36.164697Z`, reason
`offline-revision-integrity-or-custody-refusal`, remains unchanged and unrerun.
Final sealed SHA-256 is carried forward without recomputation:
**`c71f38517e02ea5bc7fa176cfb1abe8f12b43b499fbe6f74fbdc339d91f0b23d`**.
The sealed exception binding
`f6c25046d9db40c069cb183cf87f8fef19a0a952b601c87dbe690d0b0aff7e43`
and Story 1.8 observation
`c94f8fedcc4d9e362985c3056c2a2b3e6628ef74b525ed377b92f7cf920a12c5`
are carried forward without rehashing or disposition.

Both preparation directories remain outside the write boundary:

- `/home/administrator/hexalith-recovery-evidence/4-0/20261015t162756z`
- `/home/administrator/hexalith-recovery-evidence/4-0/20261016t103632z-native-enrollment-execution-revision`

Immutable packets, signed originals, expired recovery `20261014t112353z`
(deadline `2026-10-15T11:40:42Z`), failures, original modes, concurrent changes,
index and gitlinks are preserved by leaving them untouched. No preservation
audit, key inspection or historical signature verification was repeated.

Initial read-only Git HEAD: `b2890d20575356239555df5b113e3100c1d594c1`.
Initial status, excluding submodule worktrees, showed concurrent modifications
to the three Dapr access-control YAML files, `Hexalith.Platform.slnx` and
`WorksPreviewModel.cs`, plus untracked `config/`, `WorksProductionProfile.cs`
and the Works tests directory. These are outside this task; no reconciliation,
staging, reset, index or gitlink write is authorized or performed.

The bmad-build renderer ran exactly once successfully. Clarify, investigation,
template and one-shot instructions were read. The user's scope overrides
creation of a general plan/spec, implementation, tests, existing-file appends,
workflow state promotions, checkpoints, commits and deferred-ledger edits.
The footprint is create-only findings and separate review documents plus the
local renderer cache. The configured Blind Hunter agent documentation review
will be recorded separately; no operational verification layer applies.

No private-key read, SSH-agent access, fresh authentication, new token/app,
permission change, NEXT-OPERATOR-ACTION.md script execution, preflight/verifier
replay, tests, implementation, dependency
update, key generation, signing, publication, operational mutation, staging,
commit or push was performed. Provider access was limited to the three explicit
GitHub API invocations; no other provider/network/IAM operation was intentionally
requested. This statement retains the automatic-redirect audit limitation above.
No operational approval prompt was emitted.

**Spec-23 draft; Story 3.6/Epic 3/Platform 4.27 in progress;
4.1 operational gate closed; release FAIL.** Existing state files are untouched.
