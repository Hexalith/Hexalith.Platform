# Independent review of concrete ownership-source findings

**Independent documentation review: PASS after corrections.** Final handoff:
[OWNERSHIP-SOURCE-FINDINGS-20261016t143546z-v2.md](OWNERSHIP-SOURCE-FINDINGS-20261016t143546z-v2.md).
Exact reviewed document: **21,032 bytes**, SHA-256
`ffe71cdf5e28ddf37679edb4a22ce1698bb48caa2ede63ca0fdadf6e20e79525`.
The digest identifies document bytes only; it establishes no owner authority.
The retained [first draft](OWNERSHIP-SOURCE-FINDINGS-20261016t143546z.md)
is 20,147 bytes, SHA-256
`0f5d3ea6a5a1d39ed1ce0242507f41a49c2c9f7be11b66c6b9dbca5655920e15`.
Both are create-only; neither replaces the prior public-signing findings,
plans, reviews or evidence originals.

The independent reviewer confirmed all five initial findings addressed and
found no concrete contradiction introduced by the amendments. No concrete
open documentation finding or deferred review item remains. Provider records
and current API contracts remain unverified future procedure; this review
performs no provider lookup and authenticates no human, account control,
project mandate, key control, signing-purpose designation or operational result.
Existing independent human reviews are not supplied by agent document review.

## Method and findings disposition

The bmad-build skill was invoked and rendered exactly once successfully. Its
clarify, investigation, template and one-shot instructions were read. The user
restricted this continuation to new source-specific findings and independent
review, so general plan/spec creation, implementation, existing-file appends,
status promotion, checkpoints and commits were not performed. No intent gap
remained for this documentation deliverable, no irreversible action was needed,
and the footprint was new documents plus the renderer cache.

The only active configured review layer was Blind Hunter; none was skipped.
Runtime, tests, historical-verification and operational-validation work was
inapplicable to this document and expressly excluded by the user. No such work
was run. Reviewer `/root/ownership_source_review` was launched context-free at
the parent session's model capability, read the exact draft and permitted local
source comparisons directly, and returned findings as text. It did not invoke
skills, spawn further agents or write files.

Initial review arithmetic:
`kB = 20,147 / 1000 = 20.147; N = min(floor(sqrt(20.147) + 1), 10) = 5`.
The parent checked each finding against the text and sources before applying
create-only corrections. No finding was silently dropped.

| ID | Verified finding and harm | Verdict / route | Correction and independent disposition |
| --- | --- | --- | --- |
| O1 | The directory wording asserted no raw response file existed, although `rg --files --hidden` without `--no-ignore` excludes ignored files. This overstates the inspected scope. | medium / patch | v2 limits the observation to the eight listed Markdown files, explicitly excludes ignored files, and retains the substantive fact that RV-1 references no response artifact. Reviewer confirms addressed. |
| O2 | D4's source link pointed to line 36 (D6), impeding direct inspection of the second-owner decision. Actual D4 is line 34. | low / patch | Parent read numbered source lines 29–38; v2 points to line 34. Reviewer confirms addressed. |
| O3 | The unlinked older r2 sentence said “independently contains,” implying independent corroboration without establishing independent acquisition. | medium / patch | Removed the sentence in v2. The source-specific result depends on September 28 RV-1's explicit provider reference, not repeated agent reports. Reviewer confirms addressed. |
| O4 | R was not explicitly defined as the human relying reviewer, losing the prior handoff's distinction between a human trust decision and automated consumption. | medium / patch | v2 defines R as the human recipient/reviewer deciding reliance; automation implements R's explicit policy and cannot appoint itself as trust authority. Authenticated records can supply the binding without another contributor or compulsory witness. Reviewer confirms addressed. |
| O5 | The shortened possession-proof description omitted the prior reviewed restriction to a fresh challenge inside the exact independently reviewed format-contract payload, with separate authorization to sign those bytes. | medium / patch | v2 carries forward that exact restriction, project/designation/key/encounter binding, and prohibition on a separate proof-signing purpose or arbitrary challenge signatures. It links the preserved restriction reviewed previously as B3. No challenge or signing is authorized. Reviewer confirms addressed and the section link resolves. |

The follow-up reviewed the amended v2 directly and the five dispositions;
unchanged validation was not replayed. Its conclusion at
**2026-10-16T14:40:40Z** was **PASS for documentation**, with no remaining
concrete document finding. This result does not accept the future API contract
or convert any expected response field into an observed provider fact.

## Source result and precise remaining gap

The configured project is GitHub.com **`Hexalith/Hexalith.Platform`**. The exact
historical source lead is
[RV-1, lines 28–31](/home/administrator/projects/hexalith/platform/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/reviews/update-2026-09-28/review-reality.md:28):
`orgs/Hexalith/members?role=admin` reported `jpiquot` and `tinouit`, and Platform
admin access was reported for both. Neither this prose, CODEOWNERS, the remote
locator nor architecture role policy establishes current human owner authority.
The historical second-owner role is not a claim of another contributor and does
not assign that account any present witness/reviewer role.

The first exact missing current record is GitHub's organization membership
for **Hexalith / jpiquot**, showing actual active owner role and stable subject/
organization IDs, independently obtained by the intended human relying verifier.
The handoff specifies `GET /orgs/Hexalith/memberships/jpiquot` and the exact
project metadata/account-permission reads needed to bind it to Platform. It
separately identifies missing authenticated Jérôme-to-account identity and any
additional trust-owner mandate needed by the relying policy. Current private-key
control and a purpose-specific key designation remain separate and unproduced.
The membership record alone does not authorize `hexalith-owner-evidence`.

**One concrete next step:** in a separately scoped read-only source check, have
the intended relying verifier obtain the actual GitHub
`GET /orgs/Hexalith/memberships/jpiquot` result with active owner role and stable
user/organization IDs. Use only an existing session entitled to view that
specific record when authentication is required. No credential export, new
token/app, grant, broad inventory or operational permission is requested here.
This statement is a handoff, not an authentication or approval prompt.

## Time, concurrent work and preservation

Parent clock observations: **2026-10-16T14:33:45Z** (first action),
`14:35:13Z`, `14:35:46Z`, `14:38:12Z`, `14:39:44Z`, `14:40:27Z`,
and **14:41:05Z**. Independent review observations: `14:38:01Z`,
`14:39:22Z`, and **14:40:40Z**. All precede the unchanged ceiling
**2026-10-16T16:27:56Z**. These are document/review observations, not replacement
preparation/capture/approval dates. Preparation remains
**2026-10-15T16:27:56Z**, RID `20261015t162756z`.
At or after the ceiling, stop candidate-dependent qualification, enrollment,
production, review-for-activation and execution. Any later historical assessment
must explicitly record that stop. No recovery interval was extended or replaced.

The draft records initial HEAD `6fdfdc5ca99980d4aa4831e0931cd220aeb49487`
on `main`. At `14:39:44Z`, concurrent work had moved HEAD to
`cafa3f0db528af66a03b542eb4f26fd1ef06433b`, also observed at `14:41:05Z`.
A read-only comparison of those exact revisions for the seven cited ownership
metadata/source files (README, .gitmodules, CODEOWNERS, September 28 reality
review/gate summary, architecture .memlog and spine) returned no changed paths.
No broader commit assessment, new sealed baseline or disposition is claimed.
Scoped status excluding submodule worktrees showed only the new findings files
before this review record was created. Concurrent commits/changes, index and
gitlinks received no write from this task and were not reset or reconciled.

Recorded verifier **exit 1** at `2026-10-16T11:25:36.164697Z`, reason
`offline-revision-integrity-or-custody-refusal`, remains unchanged and unrerun.
Final sealed SHA-256 remains
**`c71f38517e02ea5bc7fa176cfb1abe8f12b43b499fbe6f74fbdc339d91f0b23d`**.
The sealed exception binding
`f6c25046d9db40c069cb183cf87f8fef19a0a952b601c87dbe690d0b0aff7e43`
and additional Story 1.8 observation
`c94f8fedcc4d9e362985c3056c2a2b3e6628ef74b525ed377b92f7cf920a12c5`
are carried forward without rehashing or disposition.

Both preparation directories remain outside the write boundary:

- `/home/administrator/hexalith-recovery-evidence/4-0/20261015t162756z`
- `/home/administrator/hexalith-recovery-evidence/4-0/20261016t103632z-native-enrollment-execution-revision`

Immutable packets, signed originals, expired recovery `20261014t112353z`
and its `2026-10-15T11:40:42Z` deadline, failures, original modes, prior documents,
concurrent work and gitlinks are preserved by that boundary. No full preservation
audit or revalidation is claimed. No key/public-signature reinspection occurred.

No private-key/credential read, SSH-agent access, authentication,
network/API/provider/IAM request, NEXT-OPERATOR-ACTION.md script execution,
preflight/verifier replay, tests, implementation, key generation, signing,
publication, operational action, staging, commit, push or dependency update
occurred. No operational approval prompt was emitted. The write footprint is
the two new findings versions, this review record and the renderer cache.

**Spec-23 draft; Story 3.6/Epic 3/Platform 4.27 in progress;
4.1 operational gate closed; release FAIL.** Existing state files are untouched.
