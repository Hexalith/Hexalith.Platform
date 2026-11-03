Package-mode composition still records older dependency declarations. Adopt the exact released EventStore 4.0.4 and Tenants 7.0.0 snapshots and the EventStore/Tenants/Memories 3.0.18 source/hash records selected by the verified catalog. Tenants 7 has identical declaration bytes to 6; its provenance now identifies the verified 7.0.0 release revision.

The refusal regression explicitly mutates its isolated provenance to an unqualified version, preserving refusal before resources are added as real snapshots advance. The reviewed Builds declaration bindings and dependency metadata correction are included in Builds main 26991e8c542704be44cd8d9f58084d56fa627adb; PR154 was superseded by that incoming implementation.

Validation:
- Tenants 7.0.0: five official archives verified for identity/source/signatures; declaration hash and both multi-platform image content/version/revision observations verified.
- Fresh Release/package Composition.Tests diagnostic build: zero warnings/errors; 18 focused tests passed, zero failed/skipped.
- Final checks use a task-isolated official NuGet cache. Consumed Tenants 7 assemblies match the verified signed archives; an earlier shared-cache source discrepancy is preserved and the shared cache is untouched.
- Diagnostic dependencies are explicit unpublished Debug Tooling from the documented Builds source and the prior verified packaged host. These checks supply metadata/preparation evidence only.

A qualified published tool and supported tuple adoption remain required. Accepted tuple/tool pins are unchanged, including the existing recorded producer/gitlink refusal. This PR grants no runtime/cohort, hosted lifecycle or candidate acceptance.
