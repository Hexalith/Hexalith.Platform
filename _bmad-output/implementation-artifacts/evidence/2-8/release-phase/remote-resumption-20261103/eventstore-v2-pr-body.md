EventStore's required CI currently uses the legacy direct-integration contract. This proposal pins `domain-ci-v2.yml@26991e8c542704be44cd8d9f58084d56fa627adb` and the verified published CLI 4.43.4, selecting the existing `platform-package` suite for runner-owned gateway/persistence testing and unconditional exact-owner cleanup after isolated success.

All nineteen existing unit projects remain, and the self-contained AppHost model-test project moves from the retired direct-integration input into the isolated list. The separate Contracts, governance, compatibility and source-mode jobs are unchanged. The caller removes retired Dapr overrides and maps only the two dedicated registry-reader secrets. Exact workflow, tool, suite and secret assertions are updated; existing blocking/refusal assertions remain.

This is a blocked adoption proposal. Both recorded dependency pointers are preserved: Builds `048260858048494d1c47156f4bf5e05a5403a02d` carries the approved Azure.Core 1.64.0 / Azure.Security.KeyVault.Keys 4.10.2 additions, and Platform is `ab56a8deb9bafef8a717640beba11aca43d8d65a`. Released preparation requires a genuinely release-tagged Platform identity whose Builds pin exactly equals EventStore's catalog. Current official Platform v1.1.5 at `bf101b9d5f19ba741852b0230e526d1f01422022` pins Builds269, so changing only that pointer would still fail HXW009. Normal coherent catalog/Platform publication and owner adoption are required before merge; there is no downgrade, shadow catalog or identity bypass.

Validation:
- Actionlint passed; every independent job and all twenty required project selections are preserved against canonical main.
- Actual signed published CLI 4.43.4 validates the unchanged v2 manifest.
- Fresh Contracts.Tests Release/package build against the exact recorded048 catalog passed with zero warnings/errors. The three owning caller/permission/independent-gate cases passed, with minimum3 and no skips.
- Exact commit/title passed owning locked commitlint21.2.4 under Node24.22.1/npm11.19.1; normal staged-file guard ran.
- Actual preparation helper exits1 before export because this isolated local owner layout has no direct Platform `.git`. This first failure is retained; no nested dependencies or synthetic identity fixtures were initialized.

Full current-head hosted CI, coherent recorded dependency adoption, actual package/runtime behavior, cancellation/partial-startup cleanup and zero-owned-leftover evidence remain required. This proposal claims no runtime, hosted lifecycle or candidate acceptance.
