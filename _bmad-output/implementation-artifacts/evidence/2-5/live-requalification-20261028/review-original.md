# Blind Hunter review before corrections

Changed content approximately 704.651 kB. Required finding floor: 10.

1. The audit requires original logs, but the repository ignores `.log` files. Preserve those logs and compressed AppHost replacements.
2. The source inventory omits AppHost Program.cs, apphost.cs, central build-property files and the Tooling project file.
3. Final archive validation only checks files still present and does not enforce the retained complete manifest.
4. Runtime claim validation does not inspect the durable owner receipts beneath `claims/owners`.
5. Historical command receipts do not bind helper contents; changed assertion lines make superseded failures hard to reconstruct.
6. Prior 184-file provenance is accepted through a copied boolean instead of retained original fingerprints.
7. Initial failure-retained results and first terminal observations are only checked in memory, not retained before down.
8. Peer identity comparison starts at the first post-cleanup observation, without retaining the original baseline.
9. Earlier cleanup attempts and retry summaries are not independently reconciled.
10. Ignoring entire retention/cleanup objects permits historical failure identities to disappear undetected.

All ten findings have individual verdicts and corrections in `review-triage.json`. This file summarizes the original review against the earlier state, not the corrected implementation.

The follow-up reviewer identified that `cleanup.ownedResourceIds` includes current observed leftovers and legitimately changes after cleanup. The audit now treats that field as an observation while preserving terminal, bindings and `retention.failureRunIds`.

The follow-up also queried preservation of the first host inventories. Those inventories had already been copied to `host-before-before-review.json` and `host-after-before-review.json` before the second scan replaced the current pointers. Their original timestamps distinguish them from the second run's scans. The original qualification summaries, runtime manifest, assembly identities and portability receipts also have `*-before-review` copies.
