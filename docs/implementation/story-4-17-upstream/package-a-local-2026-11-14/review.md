# Review disposition

Three independent BMAD review layers examined [review-round-1.patch](review-round-1.patch), SHA-256 `b207d5780597e5ee806410adfd1786480d215ff6bd74f3e9cca00a4c2356ea27`. The blind reviewer calculated a floor of eight findings from 54,661 bytes; the edge reviewer returned two and the verification reviewer returned three. Every finding was triaged before grouping. The parent also assessed one implementation observation.

| Finding | Verdict | Disposition |
|---|---|---|
| BH1: abort-induced cleanup errors | high | Corrected; native Close/reconnect tests and original reproduction pass. |
| BH2: live-context deadline exhaustion returns nil | high | Corrected; deadline/cancellation failures remain recorded and exhaustion fails. |
| BH3: transport failure overwrites NACK | high | Corrected; broker outcomes retained separately across cancellation/exhaustion. |
| BH4: queued shutdown loses terminal classification | medium | Corrected; terminal/stopped failures return without reconnect retries. |
| BH5: native asynchronous 403 lost | high | Corrected; bounded channel/connection close listeners and native denial tests. |
| BH6: NACK followed by close becomes uncertain shutdown | high | Corrected; actual NotifyPublish outcome distinguishes NACK from shutdown. |
| BH7: no native default no-confirm coverage | medium | Added success, blocked-write cancellation and subsequent usability tests. |
| BH8: actual shared-subscription recovery is unproved | maybe-false | Real-broker execution excluded by user; concrete recovery/redelivery assertions retained in integration proposal. |
| EH1: native asynchronous 403 lost | high | Same verified root as BH5; independent reproduction retained and correction verified. |
| EH2: earlier conclusive failure lost on exhaustion | high | Same error-selection correction as BH3; covered with uncanceled exhaustion. |
| VG1: Init cancellation unverified | medium | Added public Init pre-cancel/dial and native handshake/confirm-setup cancellation tests. |
| VG2: URI handshake timeout unverified | medium | Added native stalled handshake using connection_timeout=20 with a live caller; default deadline also asserted. |
| VG3: native denial tests modeled synchronous errors | high | Added actual AMQP channel.close/connection.close 403 frames after complete publication. |
| OWN1: declaration caching precedes cancellation check | false | Cache records a completed successful declaration; immediate context check prevents subsequent Publish and reconnect clears aborted generation. No bad outcome demonstrated. |

No source defect or owner-scope question remained after corrections. The individual triage and verification reasoning are recorded in the [build spec](../../../../_bmad-output/implementation-artifacts/spec-4-17-rabbitmq-package-a-local-contribution.md#review-triage-log). Saved reproduction test text, scripts, commands and outputs use `blind-review-*`, `edge-review-*` and `verification-review-*` filenames.

## Targeted follow-up

The reviewers verified corrected candidate SHA-256 `b49529292b89e8fc64222a4e955c9deb96b2c7039f42b788981e88814ecf19bb`:

- Blind reviewer: findings 1–7 addressed, integration gap 8 retained explicitly, no concrete new source defect; all five preserved reproductions pass in [blind-review-followup-output.log](blind-review-followup-output.log).
- Edge reviewer: native channel/connection 403, genuine NACK plus disconnect, error retention, cancellation and generation isolation verified; no concrete remaining source defect.
- Verification reviewer: public Init and native URI-timeout assertions close both coverage gaps; native denial, no-confirm and recovery tests model the required API behavior. No verification gaps found within local scope. The integration proposal correctly separates actual RabbitMQ, Dapr, subscriptions and persisted readback.

The edge follow-up also reproduced an SDK observability limit: when the native close-ok reply write stalls, cancellation may return before NotifyClose exposes an already-decoded broker reason. [Diagnostic evidence](edge-review-close-reply-cancellation.log) and [interpretation](edge-review-close-reply-cancellation-command.txt) retain this as uncertainty, not a fabricated denial or new component defect. The [integration proposal](integration-proposal.md) carries this limit along with the authentication-handshake marker loss.

The final patch differs from that reviewed candidate only in `TestMustReconnectOnAMQPError`: the fixture now explicitly starts open, existing false expectations are preserved, and closed-state assertions were added. The parent inspected that exact correction, reran it under race ten times and reran the full component suite successfully. Final patch SHA-256 is `ad71546ef66169c02b6b61f926b313c48d5f251e1a52e99c4129a44059dfa3b7`; production and focused-test source hashes match the reviewed/race-tested candidate. No broker/Dapr experiment was executed. Follow-up workers and disposable jobs completed before cleanup.
