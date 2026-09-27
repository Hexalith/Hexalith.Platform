# PRD update: original brief-addendum reconciliation

**Input:** [Hexalith Platform: Supporting Context](../../../briefs/brief-platform-2026-09-27/addendum.md).

**Targets:** updated [PRD](../prd.md) and [addendum](../addendum.md), checked on 2026-09-27.

**Verdict:** no material omission. User constraints are captured as requirements; historical repository evidence and technical rationale remain in the supporting document or its explicitly linked source. Earlier unknowns are distinguished from accepted later decisions and implementation evidence still required.

| Input material | Current destination and disposition |
| --- | --- |
| User-supplied common platform, seven modules, Aspire/local/CI/hosted scope | PRD Vision/scope, FR-1–FR-5/FR-10/FR-12 and success measures; no environment or MVP module lost. |
| Platform as direct module submodule with minimum composition | PRD FR-2/FR-3; addendum Workspace and build constraints and Parties example. Accepted domain/technical workspace split governs original broad wording. |
| Hosted IP, staging and production domains, separate application data/credentials | PRD scope/FR-10/FR-11/NFR-3; addendum hosted table preserves values and qualifications. |
| Automatic production deployment and failed-deployment rollback | PRD FR-6–FR-8/NFR-1 and production-entry gates. Accepted release modes and compatibility prerequisites refine original shorthand; no E2E bypass is introduced. |
| Historical optional Works preview, sibling/nested paths, Dapr composition and unimplemented complete system | Addendum Existing implementation context preserves the observed limitation. Workspace section says removing those alternate paths is implementation work. These observations are not converted into current capability claims. |
| Works rollback composition until migration parity and Agents story ownership | Addendum Existing implementation context explicitly retains both and links the original evidence source. This migration detail need not become an MVP product requirement. |
| Sixteen declared references and no immediate module Platform references found | Addendum Existing implementation context retains inventory and historical qualification. Repository declaration is not equated to runtime integration. |
| McpCli canonical sibling repository, missing inspected root entry, .NET tool and decorated Contracts | Addendum McpCli context preserves source facts as historical observations. FR-2 requires the supported source path outcome; actual enrollment remains qualification work. |
| McpCli stdio, deferred HTTP, empty catalog and unverified enrollment | Addendum McpCli context retains historical baseline and now records accepted caller-hosted CLI/stdio/HTTPS-gateway architecture. PRD FR-12 requires positive named operations and rejects empty executable catalogs. |
| Root-only policy, direct module/Platform workspace implications, no recursive/remote updates and deinitialize accidental nested submodules | PRD FR-2; addendum Workspace and build constraints retains all policy context. No checkout mutation is performed by this audit. |
| Debug active checkout; shared local Debug/source and CI Release/NuGet rules | PRD FR-2/SM-2; addendum AD-4 source identity explanation. Accepted mapping resolves the original implementation question and forbids sibling/ancestor/nested/package fallback for declared source. |
| Dependencies of dependencies, optional services, real vs fake, manifest/source choices | PRD FR-3–FR-5 and downstream module declarations; addendum module-owned configuration, three testing options and accepted declaration contract. Technical file/schema qualification is still owned. |
| Assumed integration testers and operators | PRD Target users describes developers/CI/Administrator/deputy and their actual jobs. No invented standalone persona remains. |
| Assumed reduction in hosting repetition/configuration drift; unquantified effort | Vision retains the common-environment problem and ownership split without asserting measured savings. Quantification remains intentionally absent because no target was accepted. |
| Candidate startup/setup/resource/recovery measures, no selected values in brief | Later accepted FR-4/FR-7/FR-8/NFR-2 and SM/counter-metrics provide chosen timing and recovery measures. Setup-effort and resource-consumption targets are set aside as unadopted candidates, not missing promises. |
| CI location/lifecycle, operating responsibilities and production expectations were unresolved | PRD FR-4, roles, FR-7–FR-9/NFR-2; addendum selects disposable hosted CI, runner lifecycle, deputy boundaries and recovery coverage. Evidence prerequisites remain explicit. |
| Ingress/DNS/certificates, storage/capacity/credentials/backups and topology unknown | Addendum Hosted architecture questions and remaining qualification distinguish accepted architecture from live qualification. PRD G1–G3/downstream table assigns prerequisites. Historical unknowns do not override accepted architecture. |
| External research about Aspire resources/deployment, Kubernetes isolation/production and evidence limits | Preserved through explicit source-addendum links and technical rationale; the main PRD states required outcomes without treating vendor documentation as proof. No external research is newly claimed as verified by this extraction. |

## Gaps and dispositions

No actionable source-reconciliation gap. Technical evidence detail remains recoverable through the linked original addendum; duplication of every historical line number or vendor reference is unnecessary. Unselected candidate metrics and assumptions are intentionally not promoted into requirements.

The updated addendum still correctly requires future proof of actual artifacts, access, recovery, infrastructure currency and G1–G3 qualification. That outstanding work is not evidence that the original input was lost.
