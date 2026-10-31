Declared CI suites now persist exact recovery ownership before allocation, admit compatible
same-job attachments, withdraw active holds before resource stops, and clean up without
retaining CI failures. First outcomes and expiry remain unchanged through cancellation and
retry.

The workflow helper confines evidence to workspace/TestResults and uses the fixed job-temp
receipt. It refuses link traversal, validates ASCII profile and owner identities, binds option
values as data, and forwards cancellation before exact-owner finalization. The packaged host can
use the run-scoped catalog APIs already published in EventStore 3.129.0.

Local-retention fixtures explicitly select local test policy on hosted runners. CI controls
cover blocked withdrawal with no resource stop and permitted withdrawal with the original
expired stopping token. Independent metadata work does not renew the resource stopping
allowance.

Validation: all normal PR checks and CI run 41236477150 pass. The hosted module suite passes
2,663 cases with five existing skips; other unit, package and catalog gates pass. Focused
verification passes 24 helper controls and 37 CI-environment cases, including all 16 CI
lifecycle cases, with zero-warning Release builds. Real extracted-package seed checks pass.
Fixture and archive checks do not establish reference hosted topology or candidate acceptance.

Shared workflow activation, compatible owner-package adoption, publication and reference hosted
integration remain separate steps.
