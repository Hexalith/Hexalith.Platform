# Targeted implementation review fixes

Four parent-requested fixes are implemented: selected archive writes enforce the
existing owned-path and link policy, terminal metadata uses nonblocking held
handles with a 4 MiB ceiling and the original cancellation token, each entry
archives its inspected observation bytes, and batch diagnostics use attempted /
not attempted wording.

The Debug test-project build passed with zero warnings/errors. The affected
handoff class passed 49/49; the isolated archive-link theory passed 5/5; the
existing report-safety cases passed 13/13. No broader tests or live runs were
started in this fix pass. Full verification remains parent-owned.

`verification.json` records exact commands, source fingerprints and the four
item-to-test mappings. The initial analyzer build failure and initial class
failure are retained. That class invocation had 27 manifest/Git fixture lookup
failures before native execution, including unchanged cases. The same source
then passed the isolated archive theory and complete 49-case class rerun; the
fixture failure cause is unconfirmed.
