# Bound final native metadata and finite source guards

Tenants now resolves the existing scoped GatewayNativeSourceMetadataReader through its actual TenantOwnerSourceGateway. The final metadata-only 0..0 request still requires native proof, exact identity, bounded valid range, no events/truncation, and valid native codec; the authority still compares final proof and observation time. Existing RSA bound proof, actual workload assertion, selected resource/domain scope and catalog enrollment remain mandatory. Raw actor admission is not bypassed.

Actual source checks passed SDK native-metadata reader/admission60/60 and Tenants global administrator authority/verifier35/35, zero failures/skips/not-run. Actual Tenants executable compiled; its test graph needed one internal MSB3026 retry, then succeeded.

The finite receiver rejects root and bare-wildcard grants at input. Its shared cache checks the original token after materialization and retries at most three times before refusing capture. Meaningful regressions: before these two guards, four new cases executed with three failures (literal root, root wildcard, changing source); after repair all58 selected finite/HTTP security cases pass. The continuously changing source faults WaitForCaptureAsync and leaves Observe unavailable; host ApplicationStarted callback exceptions alone do not imply StartAsync must throw. Initial setup/namespace failures and corrected final red/green logs remain retained.

This is source-only prerequisite evidence, not native suite or immutable-release acceptance.
