---
title: 'Platform 4.27: concrete GitHub ownership-verification source'
status: draft
scope: create-only-offline-source-findings
story_status: in-progress
operational_gate: closed
release: FAIL
---

# Concrete ownership source and verification handoff

The configured hosting service is **GitHub.com**, with local canonical project
identity **`Hexalith/Hexalith.Platform`**. The concrete ownership source already
referenced in this repository is GitHub's **Hexalith organization-owner
membership record for account `jpiquot`**. A retained September 28 review reports
that record and Platform repository administrator access. It is a useful exact
source lead, not a currently authenticated ownership attestation. Neither
Jérôme's human identity nor authority to designate the existing key for
`hexalith-owner-evidence` is established by this task.

This document supplies source findings and an exact future verification
procedure, not another general production plan. No ownership record, accepted
key designation, signature challenge, trust-root file or operational approval
is produced. All endpoint references below are documentation; no request was
made. A witness is optional when an independently authenticated record and an
accepted identity binding supply the required trust. Existing independent
human-review obligations remain separate.

## Local observations and explicit source trail

Initial actual UTC: **2026-10-16T14:33:45Z**, strictly before the unchanged ceiling
**2026-10-16T16:27:56Z**. Later checks at `14:35:13Z` and `14:35:46Z` were also
before it. These are documentation observations only.

| Local source | Concrete observation | Evidential limit |
| --- | --- | --- |
| Local Git configuration, exact `remote.*.url` fields only | `remote.origin.url` normalizes to host `github.com`, owner `Hexalith`, repository `Hexalith.Platform`; sanitized project locator [Hexalith.Platform](https://github.com/Hexalith/Hexalith.Platform). Parsing printed only the expected host/project pattern, withholding userinfo, query, fragment and any unexpected value. | Establishes the configured checkout destination, not provider-authenticated ownership or an immutable GitHub repository ID. No full configuration, credential store or authentication state was inspected. |
| [README.md](/home/administrator/projects/hexalith/platform/README.md:1), lines 1, 10, 57–59; [.gitmodules](/home/administrator/projects/hexalith/platform/.gitmodules:1) | Project title `Hexalith.Platform`, Hexalith GitHub module links; Owner says only “Platform Maintainer (Hexalith).” | Corroborating local project naming, no personal owner mandate. Submodule URLs do not identify this repository's human owner. |
| [.github/CODEOWNERS](/home/administrator/projects/hexalith/platform/.github/CODEOWNERS:1), lines 1–4 | All changes and workflows name `@jpiquot`. | Repository-editable review routing. Not GitHub organization ownership, current effective permissions, human identity or signing authority. |
| [2026-09-28 review-reality.md, RV-1](/home/administrator/projects/hexalith/platform/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/reviews/update-2026-09-28/review-reality.md:19), especially lines 28–31 | Explicitly reports `gh api orgs/Hexalith/members?role=admin` returning `jpiquot` and `tinouit`; also reports both as administrators of `Hexalith.Platform` and `Hexalith.Builds`. | Historical prose reporting a provider read, not its authenticated current response. The report supplies no immutable user/org/repository IDs or separately authenticated retained response for this binding. No quoted command was run here. |
| [Architecture .memlog.md](/home/administrator/projects/hexalith/platform/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/.memlog.md:233), lines 233, 236; [gate-summary.md, D4](/home/administrator/projects/hexalith/platform/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/reviews/update-2026-09-28/gate-summary.md:36) | Repeats the historical two-owner observation; records acceptance of the second owner as a named writer. Gate summary says no repository settings changed during that documentation run. | Recorded architecture decision, not a measurement that permission changes were applied or that either role remains current. |
| [ARCHITECTURE-SPINE.md, Roles](/home/administrator/projects/hexalith/platform/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:38) | Defines Administrator as production authority/recovery owner/Platform architecture owner; includes the second organization owner among named writers. | Role policy context, not an independently authenticated Jérôme-to-account or person-to-Administrator assignment. |

The exact September 28 review directory was listed: eight Markdown review/gate
files, with no raw membership response file there. RV-1 references the provider
query but no separate response artifact to authenticate. No claim is made that
no ownership record exists elsewhere; no broad private-custody search occurred.
The older September 27 r2 review independently contains the same historical
administrator-access lead, but no new authority is inferred from repetition.

Read-only Git observation: branch `main`, canonical HEAD
`6fdfdc5ca99980d4aa4831e0931cd220aeb49487`; scoped status excluding submodule
worktrees was clean before writing. This is a local observation, not the sealed
baseline or a disposition of changes since previous handoffs. No authorship or
commit evidence is used to authenticate ownership.

The requested [public-signing findings v2](PUBLIC-SIGNING-FINDINGS-20261016t135826z-v2.md),
[its independent review](REVIEW-PUBLIC-SIGNING-FINDINGS-20261016t135826z.md) and
[owner clarification](OWNER-TRUST-CLARIFICATION-20261016t130702z.md) were read.
Jérôme Piquot's declared project ownership, sole contribution and lack of a
personal public certificate remain user-supplied planning context. Historical
multiple organization owners do not contradict sole contribution: contribution
and administrative ownership are different properties. `tinouit` is not assigned
as a witness, current owner or reviewer by this task.

## Five bindings that must remain distinct

| Binding | What the identified source could establish | What remains unestablished |
| --- | --- | --- |
| Human identity | An existing independently authenticated identity-to-account record could bind Jérôme Piquot to a stable GitHub user ID. | Chat statements, a profile display name, email label, author field or CODEOWNERS entry do not establish that binding. No independently authenticated person/account record is identified locally. |
| Account control | A relying verifier observing an already authorized session directly at GitHub, with the current authenticated account identified by GitHub, could establish control of that session/account at observation time. | A public profile or membership lookup alone does not show that the person presenting this request controls the account. A copied `/user` result from the claimant alone is insufficient. An API credential may be delegated; its account attribution alone does not identify the human operator. |
| Project-owner permissions / mandate | A fresh GitHub membership result for `jpiquot` with `state=active`, `role=admin` identifies that account as an organization owner; the specific repository permission record can establish its effective Platform admin permission. | Organization `Hexalith` is the repository-owning account, not Jérôme personally. A repository admin is not automatically an organization owner. Hosting authority is not legal/IP ownership or automatically the Platform trust-owner/Administrator mandate. The relying verifier must explicitly accept hosting-owner authority for this narrow designation under its governance policy, or use the relevant independently authenticated project mandate. |
| Key control | A separately authorized future proof bound to the authenticated encounter and exact reviewed purpose could demonstrate control of the corresponding private key at that time. | No key control, custody, availability or compromise status follows from GitHub permissions or an old signature. No challenge or signing is authorized here. |
| Signing-purpose authority | An authenticated owner with an established narrow mandate could explicitly designate the exact public key for a defined project/purpose and validity/revocation policy. | Neither GitHub membership nor repository admin permission authorizes `hexalith-owner-evidence`. Historical `hexalith-recovery` does not transfer. No such designation is accepted here. |

Existing candidate, carried forward without reinspection:
`/home/administrator/.ssh/id_ed25519_git_signing.pub`, Ed25519,
**`SHA256:8XlNQvE3ucPf/e509wU4qtNgiyWA+TKmLei7F7+TCvk`**;
historical principal `jpiquot@itaneo.com`, historical permitted namespace
`hexalith-recovery`. Proposed namespace **`hexalith-owner-evidence` remains
unauthorised**. No key, allowed-signers file or historical signature was reopened.
A personal X.509 certificate is not needed to identify the provider record.

## Exact source-specific procedure for a separately scoped verification

This procedure is unexecuted. It requires separately scoped source access and
an intended relying verifier, R, whose source/identity acceptance policy is
explicit. It grants no network or authentication permission in this task.
R can be an existing reviewer; no additional contributor or compulsory human
witness is introduced. An independent human review of production evidence
cannot be replaced by this agent's document review.

1. R obtains results directly from the genuine GitHub HTTPS origin using an
   already authorized account/session when required. Do not accept a screenshot,
   edited JSON export, new adjacent attestation key or claimant-provided URL as
   independent source authentication. Check the exact organization/account/
   repository below, observed time and provider response outcome. Do not follow
   a transfer/rename or accept a different project implicitly. No token values,
   cookies, private keys, auth headers or full session diagnostics belong in
   the retained result.
2. Read only the following specific provider records. These are exact source
   targets and required checks, not shell commands or claims of present values.
   The first endpoint narrows the historically referenced owner-list query to
   the one account and exposes active membership rather than relying on an
   incomplete paginated list.

| Source target at `https://api.github.com` | Required nonsecret observation / check |
| --- | --- |
| `GET /orgs/Hexalith/memberships/jpiquot` | Successful current response; `state=active`, `role=admin`; provider-issued `user.login`, immutable `user.id` and `user.node_id`; organization login `Hexalith` and its immutable IDs. Record actual response, never fill expected fields from this document. Role `member`, pending membership or an unobserved/denied response does not pass. |
| `GET /repos/Hexalith/Hexalith.Platform` | Repository `id`, `node_id`, exact `full_name=Hexalith/Hexalith.Platform`, `html_url`, and `owner.login=Hexalith`, `owner.type=Organization`, owner immutable IDs matching the membership organization. These IDs are currently unknown locally. The response's caller `permissions` field, if any, does not prove `jpiquot`'s permission. |
| `GET /repos/Hexalith/Hexalith.Platform/collaborators/jpiquot/permission` | Effective `permission=admin`, actual `role_name`, account login and stable ID matching the membership subject. If the role is customized or differs, establish its exact authority separately; never infer owner status from the admin permission alone. Inherited organization-owner access is acceptable when evidenced, without requiring a new direct collaborator grant. |
| `GET /user`, only if session-account control is to be demonstrated | In an R-observed already authorized owner session, actual login `jpiquot` and immutable user IDs matching the above. This is evidence about authenticated account attribution/control, not proof that the operator is Jérôme. No login, challenge, token creation or session inspection is performed now. |

3. Resolve the person/account/mandate bridge without making a self-asserted root.
   R must already accept an independently authenticated record or established
   identity channel linking Jérôme Piquot to that immutable GitHub user ID.
   A GitHub profile's editable name/email is insufficient. If the source record
   itself supplies that accepted identity binding, no additional witness is
   required. If no such binding exists, retain this exact gap rather than
   treating an authenticated account role as authenticated human identity.
   Likewise record the pre-existing policy/mandate by which that hosting-owner
   role permits the narrow owner-side format-contract designation; if R does
   not accept this role as sufficient, the specific missing record is an
   independently authenticated assignment of Platform's trust-owner mandate to
   that person/account. No such existing assignment location is referenced in
   the inspected local metadata; do not invent one.
4. Retain a minimal nonsecret source-observation result: exact source origins
   and paths, successful/failed response status, actual observation UTC, stable
   organization/repository/user IDs, account login, membership state/role,
   effective repository permission/role, source authentication method, who
   directly observed it and how R authenticates that observation, accepted
   person/account binding and mandate reference, and unresolved gaps. Preserve
   only allowlisted public/authorization metadata; review it before sharing.
   An HTTP request ID or file hash may identify the result but cannot
   authenticate its contents. A copied response ceases to be independently
   verifiable unless R directly observed it or accepts a pre-established
   authenticated evidence channel. If a provider-origin result cannot be made
   independently checkable, record verification unavailable.
5. Stop on missing IDs, inconsistent subjects, transfer/rename, stale result,
   denied/unknown request or insufficient mandate. A 403/404 does not prove
   that no membership exists; never widen access, create grants or substitute
   authorship/history. Record the verification time and R's explicit freshness
   limit; refresh only the independently scoped source observation if later
   needed, never the recovery preparation timestamp or ceiling.

A successful result from steps 1–4 would establish only the stated current
hosting role and any independently accepted identity/mandate bridge. It is not
an owner-to-key designation. A later designation would still have to bind
this project, the exact existing public bytes/fingerprint, the narrow initial
owner-side evidence-format-contract purpose, proposed namespace, validity,
revocation/replacement source and independently reviewed format. Current key
control and every existing separate human review remain distinct prerequisites.
No designation template or purported attestation is emitted here.

## Exact missing records and smallest access boundary

The first missing record is **a current GitHub-origin membership response for
`Hexalith` / `jpiquot`, with active owner role and stable account/organization
IDs**, independently obtained by R. RV-1 supplies the exact historic owner-list
source, not that current result. Its smallest targeted replacement is the
single-account membership read above, accompanied by the exact repository
metadata and effective account-permission reads needed to bind the mandate to
Platform. Public repository metadata can be obtained without private repository
contents; private membership visibility may require an existing organization
session entitled to view this specific membership. Use the least already
available read access that can return it, not a new owner grant, broad org
inventory, audit-log access, write token or provider administration permission.
Credential-mechanism permission details must be checked within that separate
source-access task; no new token/app or permissions are requested here.

The other exact missing bindings are the independently authenticated
**Jérôme Piquot ↔ returned immutable `jpiquot` user ID** record/channel and
**that person's authority for the narrow Platform trust-owner designation**
where R does not accept the hosting-owner role as sufficient. Their existing
record locations are not identified in the inspected sources. Access needed
is only an already authoritative nonsecret identity/mandate extract accepted
by R, not identity-document publication, infrastructure/IAM access or credentials.
Even supplying these records leaves the actual purpose-specific key designation
and key-control evidence unproduced. An existing authenticated ownership record
may combine the bridges; no extra person or certificate is inherently required.

**One concrete next step:** in a separately scoped read-only GitHub source check,
have the intended relying verifier obtain and retain the actual
`GET /orgs/Hexalith/memberships/jpiquot` result, including active owner role and
stable subject/organization IDs. This is the first missing authoritative source
result, not a prompt to authenticate now and not authority to sign or operate.

## Fixed stop, preservation and workflow disposition

Preparation remains **2026-10-15T16:27:56Z**, RID `20261015t162756z`; the ceiling
remains **2026-10-16T16:27:56Z**. At or after expiry, stop candidate-dependent
qualification, enrollment, production, review-for-activation and execution.
Any remaining work must be historical assessment and explicitly record that
expiry stop. No source verification can revive an expired candidate; no
preparation date is refreshed and no replacement recovery is allocated.

Recorded verifier **exit 1** at `2026-10-16T11:25:36.164697Z`, reason
`offline-revision-integrity-or-custody-refusal`, remains unrerun. Final sealed
SHA-256 remains
**`c71f38517e02ea5bc7fa176cfb1abe8f12b43b499fbe6f74fbdc339d91f0b23d`**.
The sealed exception binding
`f6c25046d9db40c069cb183cf87f8fef19a0a952b601c87dbe690d0b0aff7e43`
and additional Story 1.8 observation
`c94f8fedcc4d9e362985c3056c2a2b3e6628ef74b525ed377b92f7cf920a12c5`
are carried forward, not recomputed or dispositioned.

No writes to either preparation directory:

- `/home/administrator/hexalith-recovery-evidence/4-0/20261015t162756z`
- `/home/administrator/hexalith-recovery-evidence/4-0/20261016t103632z-native-enrollment-execution-revision`

Immutable packets, signed originals, expired recovery `20261014t112353z`
(deadline `2026-10-15T11:40:42Z`), failures, original modes, concurrent changes,
index and gitlinks remain outside the write boundary. No new preservation
audit, historical signature verification, evidence replay or acceptance is
claimed. The only intended writes are create-only findings/review documents
and bmad-build's local renderer cache.

The bmad-build renderer ran exactly once successfully; the clarify, investigation,
template and one-shot instructions were read. In-memory route: small documentation
handoff, no unresolved intent gap for this deliverable, no irreversible action.
The user's explicit scope overrides general spec/plan generation, source
implementation/tests, existing-file appends, status promotion, checkpoints and
commits. A context-free independent agent reviews the document and source
claims; its result is recorded separately, and supplies no human authority.

No private-key or credential read, SSH-agent access, authentication,
network/API/provider/IAM request, NEXT-OPERATOR-ACTION.md script execution,
preflight/verifier replay, tests, implementation, key generation, signing,
publication, operational action, staging, commit, push or dependency update
occurs. No operational approval prompt is emitted.

**Spec-23 draft; Story 3.6/Epic 3/Platform 4.27 in progress;
4.1 operational gate closed; release FAIL.** Existing state files are untouched.
