# Builds 5.2.1 identity verification evidence

These records support the [published validation-pair receipt](../builds521-validation-pair.json).
They establish public package identity and local compilation only. They grant no
hosted runtime, lifecycle, image, native, candidate or Story acceptance.

## Original public verification

[verify-builds-successor.py.txt](verify-builds-successor.py.txt) preserves the exact
original verification procedure as documentation source. It checks the public
release/tag source, successful normal publisher, original artifact digest and
manifest, signed NuGet SHA-512 catalog observations, nuspec source, repository
signatures, original publisher payload bytes and measured validation assemblies.
It is retained here for audit; production and CI do not import or invoke it.

[signature-commands.json](signature-commands.json) records the two original
`dotnet nuget verify ... --all` commands and their successful exit codes.
The [Evidence signature transcript](Hexalith.Builds.Evidence.Cli.signature.txt)
and [Module signature transcript](Hexalith.Builds.Module.Cli.signature.txt) are
byte-for-byte copies of the original captured stdout/stderr. The command record
preserves their original temporary input paths; the receipt links durable
owner-local evidence paths.

## Measured original member comparison

[publisher-member-comparison.json.gz](publisher-member-comparison.json.gz) measures
every original member from the already authenticated release ZIP against its
actual signed NuGet archive: 38 Evidence members and 316 Module members, 354 in
total. Each record includes the member name, size and independently calculated
original/signed SHA-256 values. The signed member sets are exactly the original
sets plus `.signature.p7s`; the manifest also measures that additional member.
The original artifact and both signed archive digests are checked against the
existing receipt before comparison.

[measure-original-members.py.txt](measure-original-members.py.txt) retains the
local measurement procedure as documentation source. To reproduce it from the
same authenticated ZIP and signed archives into a new output file:

```bash
python3 docs/implementation/builds521-verification/measure-original-members.py.txt \
  --inputs <authenticated-input-directory> \
  --receipt docs/implementation/builds521-validation-pair.json \
  --output <new-comparison-json>
```

## Actual owning-project default selection

[default-project-commands.json](default-project-commands.json) records the actual
evaluation and nonincremental Release build of the real composition test project
in an isolated, byte-identical owner snapshot. It supplies the authenticated
published tool and selected dependency roots explicitly. It supplies no
`HexalithEventStoreVersion` property and inherits no catalog/version or
source-mapping environment overrides. All tracked owner configuration remains
present and warnings remain errors.

The [actual evaluation output](default-project-evaluation.json.gz) observes the
named Platform view selecting SDK4.2.0, Microsoft.AspNetCore.OpenApi10.0.15 and
Microsoft.OpenApi2.13.0 through the default import. The
[build output](default-project-build.txt) records success with zero warnings and
errors. The [verification summary](default-project-verification.json.gz) records
the observed properties, restored package observations and unchanged source and
build-configuration hashes. This evidence is local owning-project verification;
final exact-source hosted qualification and normal publication remain separate.

The three large JSON observations are stored as deterministic gzip artifacts
(with no timestamp or original filename). Their complete decompressed bytes
are retained; the receipt binds both compressed and uncompressed SHA-256 values.
Inspect an observation with, for example:

```bash
gzip -dc docs/implementation/builds521-verification/publisher-member-comparison.json.gz
```
