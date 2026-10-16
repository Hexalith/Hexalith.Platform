# Independent review of the scoped GitHub ownership-source check

**Independent agent documentation review: PASS after four create-only
corrections.** Final findings:
[GITHUB-OWNERSHIP-SOURCE-FINDINGS-20261016t150441z-v2.md](GITHUB-OWNERSHIP-SOURCE-FINDINGS-20261016t150441z-v2.md).
Reviewed v2: **13,898 bytes**, SHA-256
`80e54864cbd53dfc3ae3a249ea49c38a95671a2a9b3d344f2312045d336163c5`.
Retained [first draft](GITHUB-OWNERSHIP-SOURCE-FINDINGS-20261016t150441z.md):
12,075 bytes, SHA-256
`ce21c4f0cdb05c7d24a3f98d8d10baf12708c44b4cc5d505b63f04d78781c818`.
These hashes identify document bytes; they do not authenticate source contents,
human identity, project mandate or signing authority.

Reviewer `/root/github_ownership_source_review` was launched context-free at
the parent session's model capability using bmad-build's active Blind Hunter
layer. It directly inspected the exact draft and compared it with the two
specified prior local handoffs and the parent's allowlisted execution results.
It made no provider lookup, wrote no files, invoked no skills and launched no
further agents. It independently assessed documentation, not the provider
responses. No configured review layer was skipped; tests, runtime, candidate
activation and operational validation were excluded by the user and were not
performed. Existing independent human reviews remain separate.

Initial arithmetic:
`kB = 12,075 / 1000 = 12.075; N = min(floor(sqrt(12.075) + 1), 10) = 4`.
The parent checked all four claims against the draft and actual request method,
then created v2 without changing the first draft. Every finding is dispositioned
below; none was dropped or added to an existing ledger.

| ID | Checked finding / implication | Parent verdict and route | Correction and independent disposition |
| --- | --- | --- | --- |
| G1 | Broad origin/request assertions exceeded the available transport evidence: exact request URLs, final HTTP statuses and returned identities do not audit automatic redirect behavior. | medium / patch | v2 distinguishes the three explicit CLI invocations from potentially automatic HTTP hops and states that redirects were not independently audited. The reviewer confirms addressed. No redirect is asserted to have occurred. |
| G2 | “No authentication was initiated” was too broad for ordinary authenticated API reads using existing credentials. | low / patch | v2 says no fresh login, authentication refresh or credential creation was initiated, while retaining ordinary authenticated reads. The reviewer confirms addressed. |
| G3 | The qualification following the missing identity/mandate record did not repeat unresolved independent source acceptance and freshness prerequisites. | medium / patch | v2 explicitly retains relying-human acceptance of the observations through an accepted evidence channel with an explicit freshness policy. The reviewer confirms addressed. |
| G4 | Forward-looking possession/designation language omitted the prior O5 restriction. | medium / patch | v2 links and preserves the fresh one-use challenge only inside the exact independently reviewed format-contract payload, bound to encounter/project/designation/key, with separate explicit authorization for those exact bytes and no arbitrary or separate proof-signing purpose. The reviewer confirms addressed. |

The follow-up reviewer directly inspected v2 at
**2026-10-16T15:10:04Z**, confirmed its byte count and digest, and returned
**PASS for all four documentation corrections**, finding no concrete remaining
documentation defect in the amendments. It did not replay unchanged work or
reauthenticate GitHub evidence. No concrete open document finding or deferred
review item remains. The explicitly retained provenance and trust gaps are
limits of the source result, not manufactured acceptance.

## Actual source result and remaining bindings

The parent observed successful responses for the three authorized reads to
`https://api.github.com`, using existing GitHub CLI access, in dependency order:

| Source path | Actual completion UTC | Actual result |
| --- | --- | --- |
| `GET /orgs/Hexalith/memberships/jpiquot` | `2026-10-16T15:04:42.109383Z` | HTTP `200`, CLI exit `0`; membership `state=active`, `role=admin` |
| `GET /repos/Hexalith/Hexalith.Platform` | `2026-10-16T15:05:02.610757Z` | HTTP `200`, CLI exit `0`; exact project `Hexalith/Hexalith.Platform`, repository ID `1328589450`, owner organization `Hexalith` |
| `GET /repos/Hexalith/Hexalith.Platform/collaborators/jpiquot/permission` | `2026-10-16T15:05:18.860276Z` | HTTP `200`, CLI exit `0`; effective `permission=admin`, `role_name=admin` |

Returned subject `jpiquot`, user ID **`6775094`**, matches in membership and
permission responses; organization ID **`80614290`** matches membership and
repository owner. Node IDs, exact dispatch times, project URL, source paths
and observation provenance are retained in the final findings, copied from the
actual allowlisted results rather than populated from prior expectations.
No denied or missing membership response occurred, so no missing read access
blocked this task. A 403/404 would not establish membership absence.

The observation supports the account's hosting roles at those times.
It does not authenticate Jérôme Piquot's human identity, establish who controls
`jpiquot`, accept a Platform trust-owner mandate, demonstrate private-key control
or authorize a signing purpose. No human relying verifier was appointed, no
human source observation or acceptance is claimed, and no accepted evidence
channel or freshness policy was supplied. Ordinary HTTPS/CLI source observation
and an agent-reviewed retained summary do not supply independent downstream
human acceptance or a provider-signed attestation. Automatic redirect behavior
was not independently audited; the three deliberate invocations and final
responses are the available transport evidence. No provider request was repeated
to repair this limitation.

Jérôme's ownership declaration, sole contribution and lack of a personal
certificate remain planning context. An independently authenticated ownership
record may supply identity and mandate bindings; no additional contributor or
compulsory witness is required. Existing independent human reviews are not
replaced. **`hexalith-owner-evidence` remains unauthorised**; no designation,
challenge, key inspection or signing occurred.

**One concrete next step:** supply a nonsecret extract of a pre-existing,
independently authenticated ownership/identity-and-mandate record linking
Jérôme Piquot to GitHub user **`6775094`** and the narrow Platform trust-owner
mandate for repository **`1328589450`**, through an evidence channel accepted
by the intended relying human. Its location and acceptance remain unknown.
This does not appoint that human or complete source acceptance/freshness,
account control, private-key control or purpose-specific designation. At or
after expiry, the record may inform historical assessment only for this
bootstrap. This handoff is not an operational approval prompt.

## Time, concurrent changes and preservation

The parent checked actual UTC first at **2026-10-16T15:03:04Z**. Request clock
checks were `15:04:41Z`, `15:05:02Z`, `15:05:18Z`; further parent observations
were `15:05:29Z`, `15:07:35Z`, `15:08:45Z`, `15:09:35Z` and
**2026-10-16T15:10:22Z**. Review at `15:10:04Z` and every source completion
precede the unchanged ceiling **2026-10-16T16:27:56Z**. These timestamps record
observations only. Preparation remains **2026-10-15T16:27:56Z**,
RID `20261015t162756z`.

At or after the ceiling, stop candidate-dependent verification, qualification,
enrollment, production, review-for-activation and execution, make no provider
requests for this bootstrap, and explicitly record the actual expiry stop in
any continued historical-assessment handoff. This task has not reached that
ceiling; no fictitious expiry observation is recorded. No preparation timestamp
was refreshed and no replacement recovery was allocated. Source success cannot
revive an expired candidate or open an operational gate.

Initial and final read-only HEAD observations both returned
`b2890d20575356239555df5b113e3100c1d594c1`.
Initial status already contained concurrent Dapr/Works/solution modifications
and untracked config/Works source/tests. By `15:07:35Z`, additional concurrent
modifications appeared in `apphost.cs`, `eng/verify-platform-pins.py` and the
Composition project file; by `15:10:22Z`, `eng/test_verify_platform_pins.py`
was also modified. Status excluded submodule worktrees and disabled optional
Git locks. These observations are not a full preservation audit or a judgment
of concurrent changes. No such file, index or gitlink was written, reset,
reconciled, staged or committed by this task.

Recorded verifier **exit 1** at `2026-10-16T11:25:36.164697Z`, reason
`offline-revision-integrity-or-custody-refusal`, remains unchanged and unrerun.
Final sealed SHA-256 is carried forward, not recomputed:
**`c71f38517e02ea5bc7fa176cfb1abe8f12b43b499fbe6f74fbdc339d91f0b23d`**.
Sealed exception
`f6c25046d9db40c069cb183cf87f8fef19a0a952b601c87dbe690d0b0aff7e43`
and Story 1.8 observation
`c94f8fedcc4d9e362985c3056c2a2b3e6628ef74b525ed377b92f7cf920a12c5`
remain carried forward without rehashing or disposition.

No write was made to either preparation directory:

- `/home/administrator/hexalith-recovery-evidence/4-0/20261015t162756z`
- `/home/administrator/hexalith-recovery-evidence/4-0/20261016t103632z-native-enrollment-execution-revision`

Immutable packets, signed originals, expired recovery `20261014t112353z`
(deadline `2026-10-15T11:40:42Z`), failures, original modes and prior documents
remain outside the write boundary. No key inspection, historical signature
verification or preservation audit was repeated.

The bmad-build renderer ran exactly once successfully; clarify, investigation,
template and one-shot instructions were read. The user's narrow scope overrides
general plan/spec creation, implementation/tests, existing-file updates,
status promotion, checkpoints, deferred-ledger changes and commits. The write
footprint is the two new findings versions, this create-only review record and
the local renderer cache. No private-key read, SSH-agent access, credential or
authentication-header inspection, fresh login, token/app creation, grant change,
other deliberate network/provider/IAM operation, NEXT-OPERATOR-ACTION.md script
execution, preflight/verifier replay, tests, implementation, dependency update,
key generation, signing, publication, operational mutation, staging, commit or
push occurred. No operational approval prompt was emitted.

**Spec-23 draft; Story 3.6/Epic 3/Platform 4.27 in progress;
4.1 operational gate closed; release FAIL.** Existing state files are untouched.
