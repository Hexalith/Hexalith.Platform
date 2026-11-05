Production release validation could skip the final live-source check when using executed qualification, and the shared branch guard compared only the extracted SHA. Validate the complete branch reference and require the late check in the protected release context while preserving offline local qualification and exact recorded CI run/attempt checks.

The five-file change preserves the newer final prepare hook, source-CI delegation, package qualification, publisher and prerelease controls. CI-proof test doubles remain mode-specific; a private omission mutation is refused.

Validation: 79 source-CI cases, 28 live-reference cases, four real wrapper scenarios, 71 artifact checks, full pure publisher checks, 68 selected Python cases, legacy/v2 workflow assertions, a resource-free Release build with no warnings or errors, and the three controlled Aspire equality cases passed. Hosted source CI and genuine publication remain separate gates.
