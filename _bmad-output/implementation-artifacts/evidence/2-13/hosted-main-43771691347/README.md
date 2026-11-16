# Hosted main acceptance

[Run 43771691347](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43771691347),
attempt 1, completed successfully for the main push of candidate
`755de998e91a11545826748615785f0e43833152`, matching the local recheck exactly.
`resolve-sources`, `tuple` and `tuple-verdict` all succeeded; their steps and
retained evidence upload also succeeded. This satisfies Story 2.13 AC3.

[The summary](summary.json) reconciles **565 Composition executions**, **36 groups**
and **zero skips** against the inventories recorded at that candidate: current42 **463**,
base42 **93**, extensions **9**. Every group's selectors, positive required count,
candidate source and raw XML counts agree. Every XML test element passes and
matches a declared class or method selector; every selector is represented. The
summary retains each inventory's Git-blob SHA-256. All three tuple results report
`passed`; the jobs, artifact and bootstrap bind the selected run and attempt.

The downloaded artifact matched GitHub's recorded SHA-256. Its large ZIP stayed
under the `run-with-temp` private workspace and was removed on completion.
[Selected raw observations](verified-hosted-observations.tar.gz) preserve all three
tuple results, all 36 XML groups and the bootstrap. [The receipt](observations-receipt.json)
records the original artifact digest and every retained member hash. API metadata
is retained in `run.json`, `jobs.json` and `artifacts.json`; the tuple job log is
retained losslessly in `tuple-job.log.gz` with its original hash in the summary.

`verify-artifact.py` records the verification procedure; its ephemeral download URL
is supplied through stdin and is not retained. Use `--retained` to recheck the
archived observations and compressed job log without another download.
[Focused verifier controls](verifier-checks.json) exercise authentic reruns,
recorded-revision inventory selection, mismatched identities and XML/refusal
mutations; rejected inputs leave the existing evidence unchanged.
All remote operations were reads,
authorized by the user on 2026-11-16. This hosted result grants CI acceptance only;
it does not grant separate image, runtime or release admission.
