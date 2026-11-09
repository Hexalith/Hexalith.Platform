# Story 3.2 signed-fixture publication resume

Status: **in-progress**. The first-stage SDK/default-cohort adoption is released.
The test fixture remains unsigned and unpublished; no fixture pin, producer,
reader/host admission, native acceptance or Story 3.2 completion is asserted.
No production deployment or real module-manifest adoption is authorized.

## Completed first-stage source

- EventStore 4.2.0: `aa16227bd07f0564a3026d619a5abd973e02656f`.
- Builds 4.47.0: `5ec1779040606be89306129a7d2b245258c9acd2`.
- Platform PR39 landed as `6e43e042347358e522bfbb2f909986c95fadc6ec`, parents
  `081178aa5cc01f5dc736ba0d1980aa80fedc6fe1` and frozen
  `dca0dbc60ffab47ab08a82fa6ef4a8bad8277fe3`. Its tree is exactly
  `680eb05194dac6e443e677743c87875539d2e7ad`, the independently reviewed PR tree.
- Actual L `1b4b2fd0c60091bc191cbf050d44ca8b79f740bb` and N
  `3adbcc2b24042c0dd089f7a255dbbf9202fe8916` retain identical nonmetadata code,
  tests, helpers and docs with M. L binds exact Parties BB/40cee; N/M EBE/d9c.
- Exact-main tuple run42711758062 has all five jobs successful; catalog
  run42711757909 has all six jobs successful. Normal source publisher
  run42716608651 verified and published stable `v1.10.0`; its direct Git tag
  points exactly to 6e43. The earlier callback42712187089 deferred correctly
  with its publish job skipped and is not the publication proof.

The ordinary `gh pr merge` client refused its generic review state. The existing
ruleset24657106 explicitly permits user6775094 (`jpiquot`) as an always-allowed
maintainer, and the documented ordinary REST merge endpoint accepted the exact
head SHA, `merge_method=merge` and validated commit text. No admin/bypass request
field, rule change or source-qualification override was used. Every actual CI
gate had passed. The request, refusal, policy, actor, response and parents/tree
are retained under `normal-merge/`.

## Validation retained

Current package qualification executes 456+97+9=562 cases covering559 unique
identities. Exactly three extension methods run twice. Local and official hosted
XML pass all 562, plus 349/412 genuine historical cases, with zero failures,
skips or not-run cases. Official signed archives, all 1,585 archive bodies,
1,575 exported extracted bodies and actual SDK package assets were independently
verified. The ten hidden extracted members omitted by the artifact uploader
remain present and hash-verified inside the complete signed archives.

Twenty-five real MSBuild/restore controls cover direct/promoted edges, explicit
case-insensitive selections, package/authenticated-mapping exclusions and
project/solution restore. Cold plain Debug project/solution builds each have zero
warnings/errors from separate empty caches and absent outputs, with only the
verified tool path. Actual DomainService DLLs/deps prove source SDK 4.2. All 48
source extension/companion cases pass. Existing fixture-copy cases and the
operator imported-input drift controls also pass without changing assertions.

The complete plain-default Debug broad inventory at dca executes 958 cases:
786 Pass, 160 Fail, 12 retained skips. Full input/output guards close unchanged.
Every class/method/display/result multiplicity exactly equals both M9ea's normal
default run and M862's explicit-source run. Original 919/retained 955 comparisons
retain the separately attributed genuine M23 Contracts source prerequisite.
The unchanged actual Story2.8 package fixture at dca passes 1/1 with unchanged
inputs/outputs. That package result does not rewrite the source refusal or older
modified-input/SDK-guard observations. This broad lane is nonqualifying.

## Exact external publication refusal

Normal fixture workflow42716926527 used version 1.10.0 at exact 6e43. Its
qualification passed: all 14 Node/30 Python controls, latest source/tuple/catalog
and source-tag checks, native MTP project compilation, package-only restore,
actual signed SDK identity checks and immutable declaration/producer/native
payload comparison. The existing production review was approved normally by
eligible jpiquot; admin bypass remains disabled and only main is allowed.

NuGet/login then refused the real OIDC exchange:

> HTTP401: Workflow mismatch for policy 'Hexalith.Platform': expected
> 'release.yml', actual 'test-extension-release.yml'.

The push and signed-public verification steps never ran. The official package
URL returned404. The qualified and publication artifacts are byte-identical
unsigned candidates: archive SHA-256
`d09ad59f348b429090dea3b790758348f1875a9746b745901d5f47b6f607703c`,
DLL SHA-256
`1b360ea49f77216e8fcb100f06e54221c7ae828c137fd1dd88e8d3f7e71533f4`.
All 11 non-signature members match actual immutable source and expected metadata.
These are unsigned measurements only; binaries/archives remain outside Git.
The raw job output, API evidence, artifact JSON and independent review are here.

## Required account-side policy

The existing `Hexalith.Platform` policy for `release.yml` must remain intact.
Create a separate, narrowly scoped Trusted Publisher for the fixture using an
authenticated NuGet account session. User authorization already exists; the
missing prerequisite is account access/configuration, not renewed release consent.

| Field | Required value |
| --- | --- |
| NuGet policy creator/login used by the workflow | `jpiquot` |
| Suggested distinct policy name | `Hexalith.Platform.TestExtension` |
| GitHub repository owner | `Hexalith` |
| GitHub repository | `Hexalith.Platform` |
| Workflow filename | `test-extension-release.yml` |
| GitHub environment | `production` |
| Intended package / exact package glob | `Hexalith.Platform.TestExtension` |
| Required publishing scopes | New package and package versions for that exact package |

Use NuGet's [Trusted Publishing account page](https://www.nuget.org/account/TrustedPublishing)
and its [official setup documentation](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing).
The available HTTP access redirects that account page to sign-in. No authenticated
NuGet browser/session is exposed to this task; the authenticated GitHub identity
does not supply NuGet account access. GitHub production/main controls and the
repository variables already read back correctly; do not alter them, widen
organization secrets, borrow another repository identity or rename this workflow
to match the unrelated existing policy.

## Conditional normal retry

Before retrying, require **all** of the following to remain true: remote main is
6e43, stable v1.10.0 still names6e43, and the latest normal exact-source tuple,
catalog and source-release qualifications remain successful. Useful read-only
identity checks are:

```bash
gh api repos/Hexalith/Hexalith.Platform/commits/main --jq .sha
gh api repos/Hexalith/Hexalith.Platform/git/ref/tags/v1.10.0 --jq .object.sha
gh release view v1.10.0 --repo Hexalith/Hexalith.Platform
```

After the dedicated policy is configured, perform one normal **full** retry:

```bash
gh run rerun 42716926527 --repo Hexalith/Hexalith.Platform
```

Do not use `--failed` or `--job`: the qualification/download artifact names both
include `github.run_attempt`. A publish-only attempt2 would look for an attempt2
artifact while the successful qualification artifact is still named attempt1.
The full retry repeats source/SDK/native/package checks, creates its own immutable
attempt artifact and then requires the ordinary production review. Approve only
that verified normal job; the prior authorization covers this package publication.
Retain the real OIDC response, exact archive identities and signed public result.
Do not overwrite or silently skip an already published version.

If main has advanced, stop this retry path. Qualify the actual new source through
its normal exact-source tuple/catalog/source-release gates and use its real new
stable fixture version. Do not force old main, reuse stale qualifications or
publish v1.10.0 from another source. An unexpected existing public package must
be independently signature/source/body verified and attributed to its actual
normal publisher before adoption; it must not trigger a duplicate push.

## Next finite SDK and Platform work

Latest owning Builds main was rechecked at unchanged
`93c9db2e2892be80ab8595052b91cbda5b7d158f`. Its existing
sample-consumer/Windows CI work stays intact. `next-owning-sdk-design/` is bounded
preparation only and grants no runtime authority. Signed fixture measurements
must exist before an approved record or actual named-consumer fixture pin is
added to that SDK.

The approved future path preserves the original platformtestextension identity,
serviceEnrollment=false and the genuine EventStore/Tenants/Memories/fixture
closure. Keep generic Repository()/SourceMapping, QualificationOwners and all
ordinary service/image audits unchanged. An SDK-issued producer binding must
authenticate the signed archive/DLL, immutable declaration/producer and native
files, actual owned Platform root, selected profile and the current tuple's real
imports/catalog. Never require the old whole producer HEAD to equal a later
adopting tuple HEAD. No raw SourceHash, arbitrary root or caller-made record can
issue the binding.

Concrete sequencing from the current owner code: ModuleTestSelection already
selects a valid MTP entry under the actual repository, but MatchesSource seals
only the manifest/csproj. CompositionEngine loads its declaration closure before
materialization and probes before allocating the final plan; the new path must
validate the signed declaration early, then authenticate the actual materialized
host-consumer view/imports before provisioning. Carry the private producer proof
through the plan with purpose/run/attempt/environment/catalog/closure binding,
and recheck all shipped native files at execution. Preserve positional ABI and
every existing no-producer path. Keep actual all-resources-Healthy readiness in
NativeTestExecutor and descriptor issuance unchanged.

After normal SDK review/qualification/release, measure its real signed artifacts
and update the complete Platform tuple atomically with genuine refreshed L/N/M,
all current 562/559 plus new mandatory fixture cases, retained 349/412, five tuple
and six catalog jobs. Add the distinct measured four-module reader/host purpose;
do not reuse the private two-owner proof or admit by SDK-only hashes. Verify
assets/deps plus the DLL actually loaded by the owned host, not readiness text
alone. Run real bounded signed-package host/native startup with the applicable
isolated Aspire/Dapr lifecycle and cleanup rules. Existing unsigned503 native
interoperability remains nonqualifying.

## Preservation and scope

Frozen intent SHA-256 remains
`4a472331c89e561becf7be150c20b835cf571e2970b282093e30a692b9fa8c49`;
original baseline f7c and starting27a remain intact and ancestral. All 23 original
preparation and 19 original broad evidence bodies remain byte-identical. This
branch appends evidence/status only and must not advance main/source while the
v1.10 fixture attempt awaits account configuration. The actual main checkout's
concurrent ae9 work, staged Story2.10 files and dirty dependency gitlinks remain
untouched.
