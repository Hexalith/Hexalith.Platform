Selected CI suites need durable recovery ownership before allocation and
attachment withdrawal before resources stop. Persist exact job/run receipts,
allow same-job CI attachments, and disable CI failure retention across success,
failure, cancellation and partial startup.

Confine helper output to checked workspace/TestResults and the recovery receipt
to fixed RUNNER_TEMP/hexalith-ci-owner.json. Writes use an open directory handle
and refuse links. Validate declared ASCII profiles and bind every user option
value with --key=value, preserving real signal forwarding and receipt-bound
finalization. Remove the remaining source-only guards around the run-scoped
catalog APIs already published in EventStore 3.129.0.

Make local-retention fixtures explicitly local when tests execute on CI. Cover
blocked withdrawal with no resource stop and permitted withdrawal with the
original expired stop token; independent metadata waits do not renew that token.

Validation: fresh Release graph build with zero warnings/errors; 24 helper
controls including real-process cancellation and path/argument negatives;
37 focused cases under complete relevant GitHub-hosted CI identity variables,
including all 16 CI lifecycle cases and every method that failed on d18.
Python style, complexity and Bandit checks passed. The broader 238-case runtime
baseline passed at ee6074995b7da585f731f938cc295a6f7d3bf6a3. The d18 hosted
.NET failure and its local reproduction remain retained as historical evidence.
Lifecycle controls simulate CI identity with existing source fixtures and real
local child/stop-observer processes. Local archive verification is a prerequisite
source control, and does not establish hosted candidate acceptance.

Existing published EventStore 3.129.0 remains selected. Shared domain-CI
activation, caller enrollment, consumer pins and hosted qualification remain
separate work. Merge and publication remain held.
