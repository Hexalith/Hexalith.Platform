# Parent combined-source verification — loop 6

## Parent loop-6 source review candidates — 2026-10-30

The complete local review candidates are Platform `a7aa986eb97cdf28311756fdb0b1f3171a1c572b` and EventStore `6a4ed39ca45e646a3a6e4e16f32a9f2b93970042`, retained in each repository as `refs/ext-protection-1/source-20261030-loop6-review`. They are unaccepted and unpublished by this workflow. All earlier pairs remain withdrawn and historical. Temporary indexes and local commit objects preserved the shared branches, existing staging and concurrent changes. All 288 scoped source blobs match their recorded candidate inputs after Git normalization. Two host inputs use the already verified isolated task sources; concurrent shared catalog-only changes remain untouched and are excluded from these candidates. Acceptance documents accompany the source candidates separately.

| Proposed field | Value |
| --- | --- |
| EventStore TargetVersionOrCommit | `6a4ed39ca45e646a3a6e4e16f32a9f2b93970042` |
| ProtectionPlatformTargetCommit | `a7aa986eb97cdf28311756fdb0b1f3171a1c572b` |
| Proposed integration date | `2026-11-02`, unaccepted |
| Accepted profile/date/availability | Unset; no Committed/Available record |

The combined shared-checkout run passes all20 checks: Protection510 total/502 executed/eight explicit owned-integration skips, lifecycle/revocation68/68, full Secrets.Runtime.Tests build plus185 affected tests, EventStore450/450, payload898/898, contracts17/17, offline116 without skips and harness13. Counts overlap. Source mode retains41 cases and liveExecuted:false. Required Debug/project-reference/-nodeReuse:false host/three-role/standalone analyzer builds have zero warnings/errors. The original task host build passes. A later supplemental shared host build fails3 CS0234 after concurrent catalog-only host edits reference types present only in the separate Platform references checkout; its failed output is retained. The immutable candidates use the exact verified task host sources for those two paths, preserving the concurrent shared files. The initial unchanged30ms payload drain test failed under parallel load, passed alone, and the complete898 rerun passed with serial classes; its failed output and both reruns remain intact, with no source/assertion change for that failure.

Fresh native-final-02 on frozen graph07 passes5/5 with zero failures/errors/skips in2998.951s. It retains the unchanged complete1000 targets, actual9MiB state, 9,439,430-byte head, both history geometries and256 native owners/occurrences (7,013,381 archive bytes,5021 creates,2216-byte head, lookup below16 reads). All five shipped-policy-derived restricted policies and bootstrap-only root use are independently hash-checked. Container absence and tmpfs unmount are verified; the parent independently rechecked owned container absence. Native-final-01 exhausted the unchanged3600s bound and remains BlockedUnknown with cleanup, never a pass.

Actual Dapr graph05 passes all four phases, including exact9/32MiB source retention, a supported opaque snapshot with independently retained original and authenticated dead-ERC typed-Erased replay, and legacy4MiB chunks refusing at4MiB then reading exactly with verified8MiB sidecar/SDK allowances. Shipped Python --apply followed by six exact runtime owner genesis checks and two lifecycle actors passes on07. A separate real native transport precheck covers success, wrong-context400, absence404, eight ACL403 denials and invalid-token403 followed by valid success. These owned executions account for all eight ordinary-suite integration skips.

The parent verified287 functional inventory hashes in the isolated source, all80 loop-6 deltas,211 packet artifact hashes, and the257-file runtime plus5505/5506-file source originals and persistent mirrors for final graphs05/07. All production sources are byte-identical between qualification04 and native07; four test-only differences and exact graph pins are explicit. Whole binaries are not equated. The candidate overlay adds only parent evidence attributes. Preserved concurrent catalog host changes are explicitly excluded from the candidate; their current shared-checkout build failure remains separate. The256-file C# scan finds no new multiple-type violation. All four frozen matrix rows and the38 additional executed cases executed and passed. Source and exact command/hash evidence remain separate from installed and physical qualification.

Read results lost before their separate original-retention CAS remain Unknown when no exact atomic original survives. Existing unsafe whole-object plaintext skeleton metadata requires independent sanitization; legacy source chunks require the documented response allowances. Physical-copy qualification and the complete independently installed41-case run remain external and unestablished. Production destruction remains independently gated.

Still required: reviewed targets and accepted profile/date; actual A1 HTTPS/storage/genesis/native/no-backup/all-copy/restore/retirement evidence; independent per-tenant authorities and complete usable trust; exact private phase/final grants including lifetime migration/retention; disjoint mounted credentials and executor/observer/enrollment identities/signers; source/control/fault cells; current signed read-only observer permission with independent physical component/backing proof; and legacy transport allowances where needed. No Live/OQ/Agents consumer, register edit, cluster/provisioning/production operation or Git remote operation occurred.

From the Agents repository root, this existing executable owner command remains unrun. Its accepted profile must pin both proposed commits above:

```bash
python3 ../eventstore/tools/verify-ext-protection-1.py --mode Live --profile /home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json --evidence-root /home/administrator/hexalith-protection-evidence/ext-protection-1/attempts
```

See [verification](verification.json), [candidate blobs](source-candidates.json), and [executed matrix](matrix-audit.json).
