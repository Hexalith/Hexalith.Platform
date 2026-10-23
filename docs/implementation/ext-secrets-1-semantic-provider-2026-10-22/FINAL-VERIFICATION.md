# Reviewed semantic component — final source verification

The bounded implementation and approval preparation are complete. This is **source/component evidence**, with no installed provider, physical qualification, accepted replacement target or dependency availability. Native dependencies remain inside the infrastructure executable. No physical provisioning, experiment, owner Live/Full/cohort command or consuming Agents seam ran.

The preserved spec baseline is `543ea182bd83c813609c7df0491d1419b7f1f99e`. Platform HEAD observed before local publication is `1516a6be0e3b9500cbcaeb14da1c0101f88caafa`; it is an observation, not a complete accepted target. Concurrent unrelated changes were preserved and excluded from the custody review and local publication scope.

## Executed final checks

Both builds passed with **zero warnings/errors**. All **18 component scenarios** and **103 Runtime tests** passed, with zero failures, skips or unrun tests. Production source stayed unchanged during final verification. The final two-target fixture extension was rebuilt and rerun; its latest log supersedes the earlier singleton run without deleting it.

Commands from the Platform root:

```bash
dotnet build infrastructure/Hexalith.Platform.Custody.Dapr.Tests/Hexalith.Platform.Custody.Dapr.Tests.csproj --artifacts-path /tmp/hexalith-semantic-custody-final-component -m:1
dotnet /tmp/hexalith-semantic-custody-final-component/bin/Hexalith.Platform.Custody.Dapr.Tests/debug/Hexalith.Platform.Custody.Dapr.Tests.dll
dotnet build tests/Hexalith.Platform.Secrets.Runtime.Tests/Hexalith.Platform.Secrets.Runtime.Tests.csproj --artifacts-path /tmp/hexalith-semantic-custody-final-runtime -m:1
dotnet /tmp/hexalith-semantic-custody-final-runtime/bin/Hexalith.Platform.Secrets.Runtime.Tests/debug/Hexalith.Platform.Secrets.Runtime.Tests.dll -class Hexalith.Platform.Secrets.Runtime.Tests.CustodyBindingContractTests -class Hexalith.Platform.Secrets.Runtime.Tests.CustodyBindingDaprInvokerTests -class Hexalith.Platform.Secrets.Runtime.Tests.CustodyBindingOriginalTests -noColor
```

Logs, intermediate failures and the exact emitted synthetic original/public verifier/preparation statement are retained under [verification/final-review-2026-10-23](verification/final-review-2026-10-23/). The final component logs are `hexalith-custody-final-component-build-multitarget.log` and `hexalith-custody-final-component-tests-multitarget.log`; final Runtime logs are `hexalith-custody-final-runtime-build.log` and `hexalith-custody-final-runtime-tests.log`. Earlier build failures and prior-binary outputs remain intermediate evidence, never final passes. Synthetic signatures are not installation anchors.

## Matrix audit

Every frozen matrix row is covered by default-list scenarios that executed and passed:

| Required behavior | Executed coverage |
| --- | --- |
| Identity-bound, strongly consistent current authority; no bootstrap | `real-native-store-identity-immutable-cas-and-loss`, `serving-startup-and-disjoint-profile-lanes`, `public-owner-adapter-real-sdk-signed-genesis-and-transition-wire` |
| Hold/complete-reservation ordering | `hold-pin-release-and-complete-manifest-reservation`, `signed-pre-cycle-hold-reservation-denial`; a pin on the second target leaves the entire head unchanged and schedules no effect |
| Permanent intents, duplicate/loss recovery without native resend | `lost-wrap-result-recovers-without-native-resend`, `registration-only-original-lookup-and-substitution-denial`, `shared-cut-independent-closure-and-exact-original-pages`, `uncertain-successor-retirement-abandonment-and-replacement` |
| Complete inventory, retirement, survivor copy, adoption and final publication | `shared-cut-independent-closure-and-exact-original-pages` traverses the real B1–B5 methods with two destruction targets and a survivor; `uncertain-successor-retirement-abandonment-and-replacement` traverses tainted-candidate retirement and replacement within three lane epochs |
| App restore/reissue cannot replace authority or original | `single-head-cas-and-app-restore-exclusions`, `independent-owner-genesis-and-consecutive-transitions`, native-store clone/missing-authority cases and public adapter stale-genesis denial |
| Complete exact original/vector/signature recovery | Lifecycle preparation loses acknowledgement, then recovers the same signed bytes; two ordered target receipts and exact pages are asserted. `maximum-manifest-transition-immutable-index-roundtrip` covers a legal 1,000-target manifest, maximum transition bytes and corrupt immutable payload refusal |
| Missing trust/qualification/closure grants no effect | `missing-qualification-and-enrollment-close-effects`, missing holder/native completion cases in the lifecycle, invalid signed reservation cases and actual service proof/refusal cases |

The maximum-size test retains the public bounds and unchanged 32 MiB index/pending cap. Large complete manifests, registration material and admitted transition bytes are immutable payloads selected by one whole-head CAS. Exact predecessor references are reused; preflight count/index exhaustion performs zero writes. No target batch is split. Native-store tests also cover formatted enrollment JSON, exact attribute comparison, lost CAS acknowledgement and stale predecessor refusal.

The suite invokes the real `BindingService.Init/Invoke`, including the SDK bridge. The real AWS key adapters run against API doubles that enforce encryption contexts, native identities, survivor re-encryption and schedule/refusal semantics. These tests exercise actual adapter code; they do not establish provider durability or irreversible erasure.

## Independent review

Three context-free review layers returned 17 separately triaged findings: ten blind-hunter findings, four edge-case findings and three verification gaps. The complete per-finding verdict/evidence log is in the spec. Duplicate root causes were grouped only after each verdict. All surviving entries were patched; none was deferred.

Repairs cover serving-only socket checks; disjoint native epoch/protector identities and complete lanes; authenticated worker process incarnations; canonical delivery tuples; immutable-reference reuse and pre-write capacity checks; verbatim native head comparison; rejected-wrap cleanup; historical transition originals; and the real KMS/service/pre-dispatch-hold regressions. Separate follow-up passes by all three reviewers confirmed their original findings resolved and found no concrete new defect in those corrections. Those follow-ups were static inspections; the parent executed the final tests above.

The reviewed repair diff was `/tmp/semantic-custody-review-32307sa8/custody-scoped-after-repair.diff`, SHA-256 `d76b88d7681bf6d29afde2348ec32745e19c2ce48df51cb01c598aba9d85cda3`. The final fixture strengthening changed tests only. The final scope/diff and actual source/artifact hashes are retained separately; a concurrent branch head does not redefine them.

## Open integration and installation facts

[final-installation-gaps.json](final-installation-gaps.json) records the exact still-null public runtime fields, absent EventStore profile and missing semantic inputs. The installed public HMAC reference remains SHA-256 `31c28a9d1dd21eed8ca4d0c421088cb946b262c624ba461d0795cab92cd1feb3`; it is not this DEK provider. The Agents register remains SHA-256 `5bf35ac2857664c0589966a10944cd66278af85242c7c1f08e31ae29aa40e8a2` and was not edited.

The public Dapr-only owner adapter and exact authenticated installation/genesis/current-owner handback are delivered in [EVENTSTORE-HANDBACK.md](EVENTSTORE-HANDBACK.md). EventStore still needs its owning-source integration; its existing revision-zero/guard-revision/synthetic-proof code was rechecked and remains a blocker. The independent private/recovery authorizer and durable hold/dispatch-reservation issuer also need owning-service implementations and installation. These are source integration gaps, not values to fabricate in environment variables.

Actual enrolled resources, independent placement/credentials/trust, accepted profiles, measured capacity/latency, complete copy and buffer inventories, termination/sanitation verifiers, retention extensions, native deletion evidence, old-copy non-revival and survivor recovery remain absent or unqualified. First-release authority replacement remains unsupported. No local model, digest, successful native-shaped response or restored deny flag closes those gaps.

[OWNER-APPROVAL.md](OWNER-APPROVAL.md) and its machine packet propose one two-tenant F2/F3 schedule, five epochs including the sole tainted candidate, two KEK protectors, one authority, two archives, two control instances and USD 100 total. Account/placement/owners/trust, current quote, retention sign-off and explicit authorization remain missing. Preparation grants no execution permission.

The authoritative final byte inventory is [final-source-inventory.json](final-source-inventory.json); installed/accepted artifact, configuration and trust hashes remain null. Earlier inventories and checksum files are preserved as historical evidence. `SHA256SUMS` covers the final repository bundle and does not authenticate an installation.
