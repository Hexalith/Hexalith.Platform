# Measured stage-one tuple assembly

This bundle prepares artifact identity inputs. It grants no Platform tuple,
default-cohort, source-release, fixture-publication or extension-host admission.
No adopted file is written by the assembler. Its output directory must be new.

Run `python3 prepare-builds-binding.py --help` for the required actual version,
full source, successful push CI run, successful normal publisher run, prior
independent signed-package measurement, owner checkout and fresh output path.
Python optimization is refused so identity assertions cannot disappear.

The assembler checks the complete chain before writing a candidate binding:

1. Official GitHub records must identify completed successful exact-source main
   push CI and the protected release workflow, plus a stable tag/release at that
   same source. The official inventory must be release eligible, source clean,
   controls passed, fixtures tracked and CI source/run/attempt bound.
2. It redownloads the exact official NuGet archive URL itself, compares that
   fresh hash to the prior independent measurement, executes `dotnet nuget
   verify --all` successfully, requires the NuGet repository signature, and
   checks the actual unique nuspec package/version/repository source. A caller
   measurement path or a signature filename cannot satisfy those checks.
3. The signed archive hash and qualified unsigned archive hash must appear
   together in the successful normal publisher's exact remote payload
   verification message. That owner code compares every non-signature entry
   before emitting the message. The assembler also downloads the official
   qualification carrier, verifies its exact member set and every inventoried
   hash/size, and binds its producer source/workflow/job/run/attempt. Its seed
   observation must match the actual signed archive's seed body.
4. It measures the actual AppHost, both identical Tooling copies, seed project,
   all archive members and the immutable Git catalog blob/raw hash. Duplicate
   JSON fields, ZIP members, release assets, inventory packages, measurement
   packages and nuspec identities are rejected. The measurement's work path
   supplies no archive authority.

The resulting binding and measurements remain component evidence with
`candidateAcceptance:false` and `qualificationCredit:false`. The source and
catalog must subsequently be recorded in both real gitlinks and the complete
atomic Platform metadata. The unchanged `eng/prepare_builds_artifacts.py
--require-recorded` and tuple preflight must verify the actual clean recorded
candidate; this assembler cannot replace either check.

Only after that candidate's actual 97 SDK4.2 default-cohort cases (including all
five real entry/model cases), nine extension-component cases, validation-pair
cross-product controls and relevant source cases pass may the final linear
L/N/M freeze be prepared. The exact automatic obligations are five tuple jobs
(source resolution, three execution lanes, aggregate verdict) and six catalog
jobs. Historical 412/349 cases remain mandatory on their genuine same-code
commits. The fresh final receipt must bind the actual L/N/M identities and hosted
runs; the older three-job plan remains historical.

The reviewed named-consumer patch remains unapplied until the owning released
API/catalog is present. It selects `Hexalith.Platform.EventStoreHost` and makes
the default snapshot, host snapshot and live catalog versions distinct. New
validation-pair entries may use only the actual signed measurements. Stage one
adds no fixture catalog pin or extension-host admission. The approved two-stage
fixture publication/admission sequence still follows the normal first-stage
source release.
