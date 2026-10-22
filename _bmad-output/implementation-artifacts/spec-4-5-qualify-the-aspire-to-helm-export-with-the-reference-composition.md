---
title: 'Qualify the Aspire-to-Helm export with the reference composition'
type: 'feature'
epic: 4
story: 5
created: '2026-10-22'
status: 'in-progress'
baseline_commit: 'e39d59b9e7f93c5880eff423e24b8a0f274560ed'
route: 'dispatch'
review_loop_iteration: 1
story_key: '4-5-qualify-the-aspire-to-helm-export-with-the-reference-composi'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Platform has no qualified reference-composition Helm package. Its Development composition creates local infrastructure and literal secrets; the Timesheets chart is only an enrollment candidate.

**Approach:** Share declaration-derived workload metadata between local composition and hosted publishing. Probe native Aspire publishing; if an AD-1 trigger is demonstrated, record it and select one small maintained chart with parity checks. Qualify the selected package independently of future publication and staging deployment.

## Boundaries & Constraints

**Always:** Include Parties, EventStore, Tenants and Memories and every enabled server. Use one shared Dapr/security helper. Bind hosted images by digest through values, data services by non-secret endpoints, and credentials by existing references. Record actual tooling identities: current Aspire is 13.6.1, rather than the story's historical 13.5.4. Preserve unrelated work and local behavior.

**Never:** Duplicate topology, hand-model module Dapr plumbing, patch generated files repeatedly, or build a custom exporter. Create no Gateway, Secret, data-service workload or persistent infrastructure in the application chart. Do not publish, mutate existing clusters, upgrade dependencies, alter module declarations, or claim OCI-attestation/off-site-restoration acceptance.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Modes | Same inputs; normal/recovery/remove-workloads | Normal serves; recovery closes public routes and quiesces workloads; removal omits workloads/routes and preserves environment resources | Unknown modes fail |
| Bindings | Image digests, endpoint parameters, existing credential references | Deterministic output; HTTPRoutes name Gateway and namespace | Missing bindings, tags, credential literals and unsafe endpoints fail |
| Export boundary | Native export requires an AD-1 forbidden approach | Record evidence; maintained chart replaces exporter for downstream stories | Incomplete probes never count as qualification |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Composition/PlatformDeclarationSet.cs` — canonical dependency selection. `PartiesAdoptionTests.Options` selects the actual Parties declaration and seven-server closure.
- `src/Hexalith.Platform.Composition/PlatformServerBindings.cs` — server identities/readiness, currently coupled to `PlatformLocalProfile.Bind`; extract neutral metadata before local secrets are generated.
- `src/Hexalith.Platform.Composition/PlatformDaprBindings.cs` — reuse capability names, consumers, ACLs and subscriptions; its current renderer is self-hosted.
- `src/Hexalith.Platform.Composition/PlatformAppModel.cs` and `apphost.cs` — retain one composition entry; current Compose adds local providers and credentials.
- `eng/timesheets-deployment/chart/` and `test_hosted_chart.py` — reuse chart/security/testing patterns; preserve their candidate status. `eng/cluster-management/rehearse.py` demonstrates owned kind targeting/cleanup.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Platform.Composition/PlatformApplicationGraph.cs`, `PlatformServerBindings.cs`, `PlatformDaprBindings.cs` — expose shared neutral workload/Dapr metadata; local binding consumes the same graph without behavior changes.
- [x] `src/Hexalith.Platform.Composition/PlatformHostedPublishing.cs`, `Hexalith.Platform.Composition.csproj`, `apphost.cs` — add hosted selection and a native publishing probe using catalog-pinned APIs; avoid local secret/provider setup. Record the export decision and trigger evidence in `eng/helm-qualification/README.md`.
- [x] `eng/helm-qualification/chart/` — when fallback is triggered, supply the single generic maintained chart. Otherwise use native generated output. Centralize Dapr/security, namespaced Gateway references, digest/endpoint/reference values and all three modes; separate application-owned Dapr objects from environment prerequisites.
- [x] `tests/Hexalith.Platform.Composition.Tests/PlatformHostedPublishingTests.cs` — test graph parity, selection and local regression. `eng/helm-qualification/test_chart.py` — exercise the matrix, secret/provider exclusion, two identical renders per mode, archive parity and changed-digest values without regeneration.
- [x] `eng/helm-qualification/qualify.py` and `toolchain.json` — pin verified Helm 4.2.0+, node image, Gateway CRDs and catalog Dapr patch. Create only an invocation-owned kind cluster/kubeconfig; enforce restricted policy. Server-dry-run actual Pod templates through Dapr injection, inspect sidecar/init security, and require an unsafe Pod rejection. Install/upgrade the archive with Helm SSA; prove Apply ownership, unforced conflict refusal and removal boundaries. Bound waits and clean up only owned resources on failure/success.
- [x] `_bmad-output/implementation-artifacts/evidence/4-5/qualification.json` and `eng/helm-qualification/README.md` — retain commands, exact source/input/tool/package hashes, results, exclusions and cleanup evidence; synchronize the existing sprint key only to demonstrated progress.

**Acceptance Criteria:**
- Given the canonical reference graph, when either selected publishing path renders, then every enabled server has matching identity, readiness and shared Dapr/security configuration, with no independent module topology.
- Given the packaged chart, when qualified, then every mode renders identically twice and matches source-chart output; changing image digests requires values only, and Dapr HotReload stays disabled.
- Given restricted admission with Dapr installed, when each workload's Pod is dry-run, then the response includes a compliant injected sidecar; the unsafe control is rejected. A Deployment-only dry-run is insufficient.
- Given Helm SSA and separately owned environment fixtures, when install, conflicting upgrade and removal run, then ownership/conflict behavior is proven and environment fixtures survive.
- Given missing tools or failed checks, when reporting, then qualification fails explicitly; skipped checks cannot close this story.

### Review Findings

Code review 3 (2026-10-22). Scope: baseline `e39d59b` to `dcb01b1`, limited to the 4.5 Code Map files. The `evidence/4-5/` logs were excluded, and only the `Platform:Hosted:Enabled` hunk of `PlatformAppModel.cs` was included. Layers: Blind Hunter (BH), Edge Case Hunter (EH), Verification Gap (VG) and Acceptance Auditor (AA). Chart claims were re-verified with real Helm 4.2.0.

- [x] [Review][Decision] Scheme-less connection strings and `postgresql://` bypass the TLS and loopback endpoint policy (medium; AA-1, AA-2, BH, EH) — Bindings row ("unsafe endpoints fail") and native-TLS rule. Real Helm accepts `ConnectionStrings:redis` = `redis.data.svc:6379,ssl=false`, `127.1:6379` and `127.0.0.2:6379`, because `reference.endpoint` runs only for values containing `://` and the literal guard matches only `127.0.0.1`. A `state.postgresql` component with `postgresql://pg…:5432/db` also renders: the scheme does not imply TLS, queries are forbidden so `sslmode` cannot be stated, and only Redis has a TLS check. Resolved (Administrator, 2026-10-22): validate scheme-less literals and require stated TLS. Now a patch item.
- [ ] [Review][Patch] Validate scheme-less data-service literals and require stated TLS (medium; resolved decision) [eng/helm-qualification/chart/templates/_helpers.tpl:71, :171-172] — Run scheme-less `host:port[,option…]` literal authorities through the existing authority validator (canonical non-loopback IPv4 or DNS subdomain, explicit port 1..65535). Require `ssl=true` and reject `ssl=false`. Accept `postgresql://` only with `?sslmode=verify-full`, `require` or `verify-ca` as the single permitted query. Add regressions for the verified bypasses.
- [ ] [Review][Patch] Numeric values render in scientific notation (medium; BH-1, EH-1) [eng/helm-qualification/chart/templates/_helpers.tpl:173, chart/templates/dapr.yaml:25] — `toString` on Helm float64 turns `2000000` into `"2e+06"` for top-level and decoded JSON leaves (verified). Values such as `retentionSeconds: 2592000` then fail .NET integer binding. Format numbers through `toJson`, which does not use exponent form for this range, or format integral floats as `%.0f`. Add a regression with a value ≥ 1e6.
- [ ] [Review][Patch] The credential key denylists are incomplete and differ between configuration and component metadata (medium; BH-8, EH-2, EH claim) [eng/helm-qualification/chart/templates/_helpers.tpl:32, :178] — Verified to render: structured leaves `Clock__Sources__0__Pwd=hunter2` and `…__AccessKey=hunter2`, and statestore metadata `accessKey`/`masterKey` literals. This contradicts the README claim that "credential literals inside structured configuration fail". Use one shared helper for both sites, extended with pwd, accesskey, accountkey, masterkey, sharedaccesskey, passphrase and credential(s), and add regressions.
- [ ] [Review][Patch] Endpoints rendered in authority form drop the port when the URL omits it (medium; EH-7) [eng/helm-qualification/chart/templates/dapr.yaml:16, :40] — `https://dapr-sentry.dapr-system.svc.cluster.local` renders `sentryAddress: "dapr-sentry.dapr-system.svc.cluster.local"`, and `rediss://host` renders `redisHost` without a port (verified). The sidecar and Redis clients need host:port. Fail when the Sentry endpoint or an `endpointFormat: authority` endpoint lacks an explicit port.
- [ ] [Review][Patch] The evidence directory keeps 92 stale command logs from superseded runs (medium; BH, AA-8, EH) [eng/helm-qualification/qualify.py:81] — `evidence/4-5/` holds 258 tracked `.log` files, but the passing `qualification.json` references 166. Leftovers have colliding indices, for example `006-curl.log` beside the referenced `006-git.log`. Remove the unreferenced logs, and have the qualifier clear prior `NNN-*.log` files, or write to a fresh per-run directory, before its first command.
- [ ] [Review][Patch] The Helm "pin" is checked only by its version string (medium; BH, AA-3, EH claim) [eng/helm-qualification/qualify.py:124] — `toolchain.json` records the Helm archive URL and SHA256, but they are never used. Preflight accepts any binary whose `helm version --short` starts with `v4.2.0+`. This contradicts the task "pin verified Helm" and the README claim "requires the pinned Helm binary". Obtain Helm through `download('helm', …)` and extract it (the `tarfile` import is unused), or compare the binary hash against a recorded expected value.
- [ ] [Review][Patch] Build processes outlive qualifier timeouts and MSBuild node reuse is enabled (medium; BH, EH-14) [eng/helm-qualification/qualify.py:75] — `subprocess.run(timeout=…)` kills only the direct child, so the processes spawned by `aspire publish` (240 s) and `dotnet msbuild` (30 s) survive. Neither `MSBUILDDISABLENODEREUSE=1` nor `-nodeReuse:false` is set, although builds are known to hang under cross-session load here. Start commands in a new session, kill the process group on timeout, and disable node reuse in `self.env`.
- [ ] [Review][Patch] Chart negative tests do not isolate rules, so many validations can be removed with the suite still green (medium; VG-1–VG-5, BH-2) [eng/helm-qualification/test_chart.py:100] — Pre-verified by mutation: the suite stays green with any one of these removed. Covered sites:
  - `_helpers.tpl:26`, `:28`, `:29`, `:32`, `:33` (component metadata);
  - `:76`, `:80`, `:88` (port range, short numeric host, localhost);
  - `:171` (local or credential literals);
  - `:209` (reserved credential override);
  - `:47` and `:98` (token key, reference shape).

  `bad()` never asserts which rule fired, and the single `redisPassword` case also strips `enableTLS`. Add an expected-message argument to `bad()` and one isolating case per rule.
- [ ] [Review][Patch] The digest-change test never asserts the rendered image or the document count (medium; VG-6, EH) [eng/helm-qualification/test_chart.py:345] — The test copies the new render's image onto the old one before comparing, and `zip` truncates when the document counts differ. A constant image template passed the full suite. Assert each `app` image equals `values['images'][identity]` and ends with the new digest, and assert equal document counts.
- [ ] [Review][Patch] Pod-template pull secret, ConfigMap mount and Dapr app-port/listen-address annotations are unasserted (medium; VG-7) [eng/helm-qualification/test_chart.py:136] — Four mutants passed the full suite: pull secret removed, environment-components mount and volume removed, `sidecar-listen-addresses` removed, and `app-port` changed to `"80"`. Compare the complete Pod template in `assert_complete_services`.
- [ ] [Review][Patch] The `secure_containers` admission inspector has no tests for its rejection paths (medium; VG-8) [eng/helm-qualification/qualify.py:29] — Its only caller is the live run with compliant Pods, and `test_qualify.py:57` stubs `admission_and_ssa`. Add fixture Pod cases to `test_qualify.py`: zero or two `daprd`, an init container without `drop: [ALL]`, a writable root filesystem, `runAsUser: 0`, and Unconfined seccomp.
- [ ] [Review][Patch] The hosted publish-mode probe and its local-selection guard are untested (medium; VG-9, BH, AA-9) [tests/Hexalith.Platform.Composition.Tests/PlatformHostedPublishingTests.cs:57] — Deleting `PlatformHostedPublishing.cs:48-49` lets Hosted+Timesheets publish the four-owner graph with no test failing. No test checks that publish mode writes the graph, requires `GraphPath`, or adds only the Kubernetes environment and seven workloads with no Redis, Keycloak, parameter or secret resources. Use the publish-mode builder pattern from `TimesheetsHostBindingTests.cs`.
- [ ] [Review][Patch] The recorded source revision does not reveal a dirty tree (low; BH, EH-22) [eng/helm-qualification/qualify.py:135] — The passing evidence names `cebbf6a`, while the qualified chart and qualifier differ from that commit, and submodule commits are not recorded. Record `git status --porcelain` (or its hash) and `git submodule status` beside `rev-parse HEAD`.
- [ ] [Review][Patch] The README says the component ConfigMap is mounted only when needed; the chart mounts it on every workload (low; BH, EH claim, VG other) [eng/helm-qualification/README.md:39] — `application.yaml:51-62` mounts `environment.componentConfigMap` without condition, so every Deployment requires that ConfigMap. Correct the README sentence.
- [ ] [Review][Patch] The Dapr injector settings that sidecar compliance depends on are not listed as environment prerequisites (low; AA-11) [eng/helm-qualification/README.md:103] — Admission passes only because `qualify.py:304-307` sets `sidecarRunAsNonRoot`, `sidecarRunAsUser=1654`, `sidecarReadOnlyRootFilesystem` and `sidecarDropALLCapabilities`. A staging Dapr installed without them (stories 4.16/4.22) would be rejected by restricted admission. List them among the environment-owned prerequisites.
- [ ] [Review][Patch] The "non-secret endpoint parameters are required" check is unreachable (low; BH) [eng/helm-qualification/chart/templates/_helpers.tpl:18] — The `required … sentryEndpoint` lookup on line 17 fires first when endpoints are empty (verified). Move line 18 before line 17.
- [ ] [Review][Patch] Cleanup records `onlyInvocationOwnedResourcesDeleted: True` before verifying anything (low; BH) [eng/helm-qualification/qualify.py:426] — The flag stays `True` even when cleanup fails. Initialize it to `False` and set it after the preexisting-cluster subset check passes.
- [ ] [Review][Patch] The unit-suite step uses the default 120 s timeout (low; EH-18) [eng/helm-qualification/qualify.py:273] — A suite run with about 80 Helm renders and three msbuild evaluations takes about 51 s unloaded, so it can time out under load. Pass an explicit bound, for example `timeout=600`.
- [ ] [Review][Patch] The local binder now reports readiness errors before the Timesheets opt-in error (low; AA-10, EH, BH) [src/Hexalith.Platform.Composition/PlatformServerBindings.cs:46-52] — `PlatformApplicationGraph.Resolve` validates readiness for all modules before the per-workload opt-in check. A Timesheets declaration with no enabled servers also no longer fails. This conflicts with the task "without behavior changes". Hoist the per-declaration opt-in check above graph resolution.
- [ ] [Review][Patch] Component metadata keys are compared to the endpoint/credential keys case-sensitively (low; EH-9) [eng/helm-qualification/chart/templates/_helpers.tpl:32] — `RedisHost` renders beside the canonical `redisHost` (verified), and Dapr metadata decoding is case-insensitive. Compare with `lower`.
- [ ] [Review][Patch] `PlatformServerBindings` keeps the removed inner loop's indentation (low; BH) [src/Hexalith.Platform.Composition/PlatformServerBindings.cs:55-92] — The loop body is still indented one extra level after the refactor. Reformat it.
- [x] [Review][Defer] Every server receives all of its module's inputs and secrets (BH) [src/Hexalith.Platform.Composition/PlatformHostedPublishing.cs:20-24] — deferred: pre-existing declaration shape. `runtime.extensions[].configurationInputs` is module-scoped, and `PlatformLocalProfile.Bind` binds the same list per server. Per-server scoping needs a declaration-contract change, which this story forbids. It matters for least-privilege secret exposure once real staging Secrets bind.

**Rejected (29):**
- `MaxTokens`-style keys are rejected as credentials (EH): low. The rule fails closed with a loud error, and no declared input hits it.
- `https://169.254.169.254` is accepted (BH, EH): low. Values are written by the trusted environment writer, and the fix adds a guard.
- IPv6 literals cannot be written (BH): low. They fail loudly; no current need.
- Leading-zero ports (EH): low. `:0443` still dials 443, and `:08443` fails loudly.
- `endpointMetadataKey` equal to `credentialMetadataKey` (EH): low. Unlikely, and the fix adds a guard.
- Map or list metadata values render as `map[…]` (EH): low. Unlikely, and the fix adds a guard.
- Mid-segment route parameters (EH): low. No current declaration has one.
- `/api/users` PathPrefix widening (BH, AA-7): low. Tenants-api serves only `/api/users/{userId}/tenants` under that prefix, so nothing undeclared is exposed.
- Clock per-source tokens are not validated at render time (AA-5, EH): low. The README documents `credentials` keys, startup fails loudly, and the fix would put module-specific logic into the generic chart.
- Hosted wiring gives every workload both app and API tokens, unlike local (AA-6): maybe-false/low. It is a consistent superset; the SDK reads `DAPR_API_TOKEN`, and no failure was shown.
- One endpoint+credential shape for every component (BH): low. Fails loudly, and no reference capability needs another shape.
- No per-workload ServiceAccount; token automount is on (BH): low. Default ServiceAccounts carry no RBAC, and changing it needs re-qualification of injection.
- Names not release-scoped; standard labels, values schema and `kubeVersion` missing (BH): low. One release per namespace, and Helm refuses ownership collisions.
- kubectl/docker unpinned, PSS `enforce-version` hard-coded, `test_chart` accepts Helm ≥ 4.2.0 (BH): low. kubectl v1.36.1 is within skew of v1.35, and `qualify.py` supplies the pinned `HELM_BIN`.
- No live check of Dapr objects after the mode upgrades (BH): low. Render tests compare the complete objects per mode, and Helm keeps the manifest objects.
- Unittest skips would pass `static_checks` (AA-4): low. Latent, since no skips exist and missing tools raise; the fix adds a guard.
- The fallback decision ignores native customisation callbacks (BH): false. Adding Dapr annotations per resource is itself the hand-modeled plumbing that triggers AD-1.
- Stale `project.assets.json` could satisfy package evidence (BH, EH): false. Assets are keyed by the exact apphost path and reflect the current restore; an mtime guard would wrongly fail no-op restores.
- Gateway CRDs are not awaited as Established (EH): false. A bounded `helm install --wait` of Dapr runs between CRD apply and the first Gateway resource.
- A concurrent `h45` cluster deletion fails cleanup (EH): low. Not observed, and the fix weakens a guard.
- SIGHUP and repeated signals are not handled (EH): low. Unlikely in automated runs, and the fix adds handlers.
- The constructor leaks its temp directory if `toolchain.json` is unreadable (EH): low.
- Hosted mode ignores ActiveModule/DeclarationPath/SourceMapping (EH): low. Only the qualifier invokes hosted mode, and the fix adds a guard.
- `GetValue<bool>` throws on `"1"` (EH): false. A loud conversion failure is correct behavior.
- `AccessControl` raises `KeyNotFoundException` for an unknown appId (BH, EH): false. Every caller passes graph appIds.
- `Capabilities`/`Subscriptions` typed as `object` (BH): rejected; no named harm.
- Duplicate eventstore check in `PlatformServerBindings` (BH): rejected; redundant but harmless.
- Recorded dotnet hash covers only the muxer (EH): low. The SDK version `10.0.402` is recorded.
- Retained assets files contain absolute local paths (BH): low. They are evidence copies, not inputs.

## Implementation Notes

- Local composition and hosted publishing share the neutral seven-server graph and the same Dapr capability, ACL and subscription metadata. Hosted selection bypasses local profile/provider/credential generation.
- Native Aspire 13.6.1 with resolved Kubernetes integration 13.6.1-preview.1.26506.6 reproduces missing Dapr annotations for all seven workloads. The retained AD-1 measurement selects the single maintained generic chart.
- Final parent verification passed a zero-warning build, 52 focused C# tests and 21 Python tests with zero skips. The packaged archive passed seven injected-Pod admission checks, same-identity unsafe rejection, Helm Apply ownership, unforced conflict refusal, recovery/removal/uninstall preservation, and owned cleanup.
- Verification used an isolated snapshot because concurrent sessions continued changing shared inputs. All 1,441 recorded source inputs stayed fixed, all 166 command-log hashes match, and the chart/direct publishing inputs match the shared checkout. Exact root/owner revisions and 206 source overrides reconstruct the tested snapshot; concurrent local-test launch/executor changes are separately identified. The owned cluster, kubeconfig and seven temporary worktrees were removed.
- Both independent review rounds are triaged below. All retained in-scope findings were corrected and verified; three independent-work follow-ups were recorded in the deferred ledger. The broad composition run (230 passed, six failed, nine opt-in skips) is retained separately; its owner subprocess failure subsequently passed with the inherited package-wrapper environment. The other broad-suite failures concern released-tool provenance, gateway seeding and a Timesheets fixture.
- See [qualification](evidence/4-5/qualification.json), [verification](evidence/4-5/verification.json), [snapshot/reconstruction metadata](evidence/4-5/isolated-verification.json), [broad-suite results](evidence/4-5/parent-full-suite.json) and the [guide](../../eng/helm-qualification/README.md). Publication, OCI attestation, restoration and staging deployment remain excluded.

## Spec Change Log

- Review 1: BH-1/BH-4 showed that a single hostname and text-only structured-input handling leave real reference workloads unusable; BH-2/BH-3/EH-2 exposed credential validation bypasses. The non-frozen implementation details now specify workload hostname bindings, decoded recursive references, case-equivalent key protection and exact token agreement, with regression expectations. Avoid recreating the known-bad shared-host or pre-decoding validator state.
  KEEP: Preserve the neutral seven-server graph, shared ACL/capability/subscription helper, local-binding parity, measured native AD-1 fallback, generic maintained chart, digest/reference-only values, all three modes, pinned verified tools, controller-identity Pod injection and unsafe rejection, Helm Apply/conflict/removal checks, invocation-only cluster targeting and cleanup. A positive implementation snapshot is available at `/tmp/bmad-4-5-keep-ovkljnb4`; reuse those working parts while re-deriving the corrected chart/qualifier from these requirements. Prior passing qualification is evidence of the preserved foundation, not acceptance of the revised bytes.

## Review Triage Log

Final resolution: all in-scope survivors were corrected by the review-1 re-derivation and review-2 patches. Final 52/21 tests and the complete isolated qualification pass; rejected findings retain their refutations, and independent-work deferrals remain recorded.

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| BH-1 | medium | bad_spec | All public workloads share one host, and the current graph gives EventStore and Memories the identical /api/v1 match. Distinct backends therefore cannot both receive that match. |
| BH-2 | high | bad_spec | Real Helm accepted Authentication\u0054oken and emitted review-literal as plaintext after JSON decoding; validation currently inspects serialized text. |
| BH-3 | medium | bad_spec | Real Helm accepted a lowercase checkpoint-store credential key beside the canonical key; .NET resolves configuration without case sensitivity. |
| BH-4 | medium | bad_spec | Nested endpoint reference objects are flattened into an extra __endpoint key instead of resolved; a literal HTTPS leaf is rejected. Clock Sources cannot bind a usable endpoint through that documented structured input. |
| BH-5 | medium | patch | Real Helm rejected registry.example:5000/app@sha256 despite a valid immutable digest; the repository validator forbids the authority port. |
| BH-6 | medium | defer | The concurrent 4.11 runner calls communicate before checking response length, so its stated limit does not bound peak buffering. This is outside the 4.5 implementation and must remain preserved. |
| BH-7 | medium | patch | All 81 referenced logs exist and match hashes, but git check-ignore confirms *.log excludes them from durable repository evidence. |
| BH-8 | medium | patch | cleanup removes its private directory after commands that can raise; failed deletion or inventory leaves the kubeconfig behind. |
| BH-9 | medium | patch | The final source check hashes only the initial filenames; a newly added chart template is omitted from that inventory and can escape detection. |
| BH-10 | medium | defer | The concurrent Parties check truncates invocationId to 110 characters; accepted distinct suffixes can select one aggregate. It is separate 4.11 work. |
| VG-1 | medium | patch | Pre-verified: deleting every Subscription loop from a chart copy left all six chart groups green. Rendered subscriptions need full graph comparison in every mode and archive. |
| VG-2 | medium | patch | Pre-verified: replacing the API-token annotation with the app-token reference left all tests green because fixtures use identical references. |
| VG-3 | medium | patch | Pre-verified: removing the Tenants HTTPRoute left all six groups green because assertions iterate only present routes. Compare the complete expected route set and backends. |
| VG-other-1 | medium | bad_spec | Same verified host/path collision as BH-1; retained normal.yaml contains identical hostname/listener/path matches for different backends. |
| EH-1 | high | bad_spec | Same decoded-secret defect as BH-2; the escaped field name is converted into a plaintext AuthenticationToken environment entry. |
| EH-2 | medium | bad_spec | Real Helm accepted credentials.EventStore__AppChannelToken pointing to a different Secret than appToken; credential overrides run after declared input agreement validation. |
| EH-3 | medium | patch | Real Helm accepted replicas=1.5; int conversion passes the range guard while the unconverted fractional value is emitted into the Deployment. |
| EH-4 | medium | patch | Same failure-path cleanup defect as BH-8; private file removal is bypassed when an earlier cleanup command raises. |
| EH-5 | medium | defer | The concurrent 2.3 live test awaits each teardown without collecting exceptions; the first failure prevents second-session cleanup. This is not changed by 4.5. |
| EH-6 | medium | patch | urlopen timeout bounds individual blocking socket operations; unbounded response.read can keep a download alive on trickled data. Bound the complete download operation. |


### Review 2

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| R2-BH-1 | high | patch | Real Helm accepts ConnectionStrings:redis with Password = literal because the literal guard matches password= only; this writes a credential into rendered Pod environment/release data. |
| R2-BH-2 | medium | patch | Real Helm accepts integer and octal loopback spellings (2130706433 and 0177.0.0.1); textual 127/localhost checks do not normalize numeric authorities. |
| R2-BH-3 | medium | patch | A required Audience binding of {} creates no flattened leaves and therefore silently disappears despite successful rendering. |
| R2-BH-4 | medium | patch | A recursive null leaf reaches toString and is emitted as <nil>; the existing top-level null guard does not protect descendants. |
| R2-BH-5 | medium | patch | The shared loose regex admits dotted namespaces and empty DNS labels, which are invalid for those fields. The dotted listener-section subclaim is false: the pinned Gateway API 1.5.1 CRD admits DNS subdomains up to 253 characters, so valid dotted sections must remain accepted. |
| R2-BH-6 | medium | patch | The inventory omits linked DaprSelfHostedMtls.cs, root build imports and Builds resolver sources consumed by native compilation; modifying those inputs escapes the final content guard. |
| R2-BH-7 | false | reject | The native model is a deliberately unqualified diagnostic for the Dapr AD-1 trigger, not the selected publishing artifact. The maintained chart is selected and each declared readiness path is asserted against graph/local bindings; absent native readiness does not invalidate that measured omission or selected-artifact parity. |
| R2-BH-8 | medium | patch | toolchain.aspireKubernetes is not tied to recorded resolved package identity; the qualifier should compare its expected value with resolved native/composition integration assets and retain the package evidence. |
| R2-BH-9 | medium | patch | No chart assertion verifies the complete Service set or its selector/target port against a workload; the current correct mapping lacks regression protection. |
| R2-BH-10 | medium | patch | Normal mode asserts full Dapr fields, but recovery/removal checks do not compare the complete retained Component/Configuration objects, leaving scopes/ACL/mTLS/HotReload regressions unobserved. |
| R2-VG-1 | medium | patch | Pre-verified: changing every Service targetPort to missing-port left all nine chart groups green; verify Service-to-Pod and HTTPRoute-to-Service mappings in both serving/quiesced modes and archive. |
| R2-VG-2 | medium | patch | Pre-verified: replacing every Component type with state.redis left its identity/security test green; compare exact provider and named endpoint/credential metadata to each binding. |
| R2-VG-other-1 | medium | patch | The source-mode closure reads EventStore/Tenants/Memories owner manifests, but only Parties is captured. A temporary-root change to EventStore after capture demonstrably passes verify_source; include all four authoritative manifests. |

## Design Notes

### Required implementation details after review 1

- In `chart/values.yaml` and `application.yaml`, bind each public workload through `hostnames["module/server"]`. Reject identical normalized host/listener/path-prefix matches assigned to different backends; require every public workload route and its declared match set. Preserve declaration-derived paths and the namespaced Gateway reference.
- In `_helpers.tpl`, decode structured JSON before validating keys and leaf values. Recursively resolve nested `{endpoint: parameter}` and `{configMapKeyRef: {name, key}}` leaves into flattened .NET environment keys, including Clock Sources arrays. Reject literal credential leaves after decoding, including escaped spellings and unsafe URLs. Existing Secret references remain the credential mechanism.
- Normalize all configuration/environment keys using .NET case-insensitive semantics before detecting equivalent duplicates. Additional `credentials` must not override canonical Dapr/security values or declared channel/API-token inputs; reject mismatched app/API references. Distinct legitimate app and API token references must remain supported and wired correctly.
- Accept digest images from registry authorities with explicit numeric ports while rejecting tags. Reject fractional replica values before emitting Kubernetes objects.
- In `test_chart.py`, compare the complete rendered Subscription set to the shared graph for each mode and source/archive, compare the complete expected normal route set and backend matches, and test distinct per-workload app/API token references and their declared input bindings. Add regressions for the concrete decoded-secret, case-alias, token-override, nested-reference, host-collision, image-port and replica cases above.
- In `qualify.py`, remove private temporary files in a `finally` even when cluster cleanup fails, retaining truthful cleanup failure evidence. Bound the entire download operation, compare both the complete source-file inventory and hashes at the end, and retain all referenced command logs in version-control-visible evidence. Test these demonstrated failure paths without relying on another live cluster.
- Repeat the complete pinned-tool, owned-cluster qualification after these changes; earlier successful evidence is superseded. Preserve the frozen intent, source mapping, local behavior and concurrent work.


Hosted publishing must bypass local secret generation, not redact its output. Native export remains preferred until measured against AD-1. The existing Timesheets fallback is precedent, not proof that the complete reference chart is qualified. Recovery rendering quiesces workloads; it does not perform data restoration.

## Verification

- `dotnet test --project tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj` — run focused hosted and existing declaration/composition regressions using the repository runner.
- `python3 -m unittest discover -s eng/helm-qualification -p 'test_*.py' -v` — real Helm checks pass without skips.
- `python3 eng/helm-qualification/qualify.py` — owned-cluster admission/SSA evidence passes and cleanup completes; never use the ambient kubeconfig.
