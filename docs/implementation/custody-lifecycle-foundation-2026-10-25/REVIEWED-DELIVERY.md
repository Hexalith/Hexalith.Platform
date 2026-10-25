# Reviewed custody lifecycle foundation

The Dapr-only source foundation and unapplied deployment artifacts are implemented and independently reviewed. **The provider remains uninstalled and physically unqualified.** Independent issuer/observer services, owning-host/EventStore integration and actual installation inputs remain explicit blockers. No accepted dependency target, integration date or availability changed.

## Implementation

The public [custody client](../../../src/Hexalith.Platform.Secrets.Runtime/CustodyLifecycleClient.cs) performs wrap, ephemeral key delivery, independent holder completion, pin/unpin and authenticated original lookup through configured logical Dapr roles. Root bytes never enter the authorizer: it receives public metadata and exact request hashes. Responses bind the enrolled component, full response hash and fresh caller attempt/challenge. Duplicate/lost releases remain value-free; replaying an earlier valid signed key response exposes no key. Disposal clears the owned array, while independent closure remains mandatory for every other holder.

The production lifecycle provider resolves per-key destruction through its exact original complete reserved manifest. Full-registration verification prevents changed key/lifecycle/store/contract identities inheriting another target's receipt. The manifest provider returns Consumed only after complete bounded-page assembly, original hash/signature/vector and restore-barrier verification. Lookup starts no native effect.

Actor owner revisions remain in unchanged original requests and registrations. The fresh custody revision separately governs native head comparison and CAS. The actual lifecycle actor now exercises two registrations, pin/unpin and original recovery with diverged revisions. Authority, client, provider and page stages enforce bounded operations; caller cancellation preserves the original token, and internal expiry denies. Only an exact retained authenticated NotPerformed outcome can clear a pending pin.

The existing semantic component's independent authority, allocation exclusions, copy inventory, native intents, shared-cut lifecycle and immutable originals are reused. Native dependencies and resource identities stay inside infrastructure. The reference backend consumes only its explicitly mounted scoped credential profile; no ambient/default credential chain is selected. Its presence does not adopt AWS as a mandatory backend or establish physical erasure.

## Deployment and missing inputs

The [deployment bundle](../../../eng/ext-secrets-1/custody-deployment/README.md) includes the component recipe/preparer, Dapr binding/no-retry configuration, protected socket workload, explicit mTLS identity/bootstrap and API-channel references, app-channel/service-discovery wiring, and a suspended exact-original worker with output ownership configuration. Nothing was applied or activated.

The [current ledger](installation-input-ledger-after-review.json) contains **68 unique missing inputs**, each with its owner and next action. [Expanded profile/copy/holder members](reference-profile-inputs.json) and [source dispositions](source-dispositions.json) separately identify:

- Missing private authorizer, durable control issuer and independent closure observer implementations and installations.
- Missing owning custody API/DI composition, EventStore source/package acceptance, and registration/reserved-manifest locator implementations with exact retained mappings.
- Actual authority/store identities, archive enrollment/version/retention, epoch/KEK mappings, independently enrolled genesis, current trust/revocation, placement, recovery access and scoped credential references.
- Actual copy/holder/producer inventories, transport/termination/sanitation verifiers, capacity measurements and physical non-revival/survivor-recovery proof.
- Reviewed image pins and installed Kubernetes identity, channel, discovery and storage inputs.

Release publish produced 41 actual local files. The repaired image preparer requires a hash-bound `{runtimeImage, files}` packet and matching immutable runtime-image reference. The real image-context attempt is **Blocked** because that reviewed runtime pin is missing; no image was built. Synthetic image references occur only in offline regression fixtures. The earlier 41-file context result is pre-review evidence and does not satisfy this repaired gate.

Credential values were not recorded. The native file/profile and Dapr/API token inputs are references only. Decision-approval private keys remain outside Platform/Agents custody. Existing history policy, Open export disposition, timing and OQ-5.4-1 remain unchanged.

## Final local verification and review

| Check | Final result |
| --- | --- |
| Component scenarios, complete default list | 23 passed |
| Targeted Runtime tests | 128 passed; no errors, skips or unrun tests |
| Protection suite | 5 passed; no errors, skips or unrun tests |
| Offline deployment/preparer tests | 10 passed |
| Fresh component/Runtime/Protection builds | Zero warnings/errors |
| Release component publish | Passed; 41 published files |
| Source invariance | 2,575 captured files unchanged during verification |
| Uninstalled preflight / missing image pin | Expected Blocked refusals |

The [final command journal](verification/parent-final-l3qf1w_t/commands.json), [verification receipt](verification/parent-final-l3qf1w_t/verification.json) and adjacent logs retain the executed commands, exit codes and exact output hashes. Builds explicitly used the current sibling EventStore source graph; the installed package graph still lacks the accepted owner-state integration. Temporary generated lock drift was retained as build evidence and the original repository lock bytes were preserved. These source builds do not qualify that dirty owner graph as an installed immutable dependency.

Three independent review layers examined the custody change. All 23 individually recorded observations, including duplicate reports, were repaired and confirmed; none was deferred. The [spec triage log](../../../_bmad-output/implementation-artifacts/spec-5-4-production-custody-lifecycle-foundation.md) records each verdict and the [review confirmations](verification/independent-review-confirmations.json) retain the final dispositions. Review corrected revision-domain conflation, authenticated NotPerformed cleanup, signed replay coverage, cancellation/deadline behavior, deployment identity/channel/discovery/credential wiring, exact input parsing, runtime-image binding and evidence retention. Follow-up reviews were static; the parent executed the final suites above.

The pre-review bundle's 50 files remain byte-for-byte preserved under [pre-review-snapshot](verification/pre-review-snapshot/), with its original checksum manifest verified. Earlier successes, compile failures, package/restore failures and rejected contexts remain evidence of their own snapshots. The first parent verification used an incompatible restore option and failed; that failure is retained separately and is superseded by the established source-build command. [FINAL-VERIFICATION.md](FINAL-VERIFICATION.md) and the earlier source inventory describe the pre-review candidate; [REVIEW-REPAIRS.md](REVIEW-REPAIRS.md) describes the intermediate repair pass. Use this document and `reviewed-source-inventory.json` for the final source state.

## Physical evidence boundary

The recorded OpenBao result remains **ObservedRevival**, `g1Qualified:false`, SHA-256 `6efa381bc70b3de57a24142e5e679f53781c7f89d5ab6740fb7664966f30888e`. Its bytes are unchanged. A wrapper, restored tombstone, missing key, scheduled deletion or local model supplies no irreversible-erasure proof.

No deployment, native serve/worker call, new destructive experiment, owner Live/Full command, cohort attempt or consuming Agents seam ran. Original journals and the remaining owner qualification attempt were preserved. The source and deployment preparation are reviewable; production admission remains blocked until the named actual implementations, installations and independently accepted physical evidence exist.

This foundation ledger does not replace the complete Secrets/Host commitment packet or the broader prerequisites in the Agents dependency register. The independent owner bridge and its one-line fixture compatibility correction were reused. Unrelated concurrent Story 2.5, schema, composition and submodule work remains outside this custody publication scope.
