# Parent combined-source verification — loop 5

## Parent loop-5 source review candidates — 2026-10-30

The complete local review candidates are Platform `b1e5bbefa129ca205e6618e01451ff77710ae6fc` and EventStore `6a26fa86be77eb9745b3b839dab871acead8651a`, retained in each repository as `refs/ext-protection-1/source-20261030-loop5-review`. All earlier pairs remain withdrawn/historical. These candidates are unaccepted and unpublished by this workflow. Concurrent commits on both main branches were preserved; temporary indexes/local commit objects did not move either branch. All 267 scoped source blobs match the candidates after Git normalization. Acceptance documents accompany the source candidates separately.

| Proposed field | Value |
| --- | --- |
| EventStore TargetVersionOrCommit | `6a26fa86be77eb9745b3b839dab871acead8651a` |
| ProtectionPlatformTargetCommit | `b1e5bbefa129ca205e6618e01451ff77710ae6fc` |
| Proposed integration date | `2026-11-02`, unaccepted |
| Accepted profile/date/availability | Unset; no Committed/Available record |

The combined shared-checkout run passes all 18 checks: Protection 471 total/465 executed/six explicit native-only skips, lifecycle plus revocation 68/68, existing Secrets adapter/failure 58/58, EventStore 444/444, payload 897/897, offline verifier 116/116 and harness/bootstrap 12/12. Required Debug/project-reference/host/three-role/standalone analyzer builds have zero warnings/errors. Retained contracts anchors pass 14/14. Counts overlap. Source mode keeps 41 cases and liveExecuted:false. The additional adapter checks compile the exact existing classes/helpers with a temporary MSBuild selection, preserving SDK runner sources; the broader project's pre-existing duplicate local owner type and intermediate harness/package-lock failures are retained in attempts, without source edits or warning suppression.

Fresh frozen native-v7 passes 5/5, zero failures/errors/skips, in 2,534.671s. It includes all 1,000 targets, actual 9 MiB state, a 9,485,043-byte head, both history geometries and the 256-owner/occurrence authoritative index (7,013,381 archive bytes, 5,021 creates, 47,829-byte head and lookup below 16 native reads). All five shipped-policy-derived restricted tokens/denials and bootstrap-only root use are retained. Cleanup and container absence are verified. Earlier interrupted/timed-out attempts stay BlockedUnknown, never passes.

The parent read the original-baseline functional changes and every loop-5 delta, matched 266 implementation inventory files plus one explicit evidence-attribute-only update, verified all 267 candidate blobs, 249 packet artifacts, the 255-file delivery mirror and four final source/runtime mirrors. All 117 native-relevant source files match native-v7 and qualification-v8; their four qualification-only source/test differences are explicit. Complete binaries are not equated. The 238-file C# declaration scan has no new multiple-type violations; existing adapter and SnapshotManager types remain unchanged. All four frozen matrix rows and 67 new loop-5 cases/orderings ran and passed.

Read results lost before their separate original-retention CAS remain Unknown when no atomic original exists. Physical-copy qualification and the complete independently installed 41-case run remain external; production destruction stays independently gated. Exact missing installed inputs, role grants/signers/credentials, physical backing proof, A1 genesis/no-backup/copy/restore/retirement evidence and accepted profile/engine pins remain in the implementation packet. No Live/OQ/Agents consumer, register edit, cluster/provisioning/production operation or Git remote operation occurred.

From the Agents repository root, the existing executable owner command remains unrun and requires the accepted profile to pin both proposed targets above:

```bash
python3 ../eventstore/tools/verify-ext-protection-1.py --mode Live --profile /home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json --evidence-root /home/administrator/hexalith-protection-evidence/ext-protection-1/attempts
```

See [verification](verification.json), [candidate blobs](source-candidates.json), and [matrix audit](matrix-audit.json).
