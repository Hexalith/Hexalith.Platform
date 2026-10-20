# Source verification — 2026-10-19

The complete Local gate passed against clean detached EventStore and Builds source checkouts. It reports **709 custody tests, 123 runtime tests and 146 Python tests passed**, with zero failures, errors, skipped or unrun tests. Both .NET builds had zero warnings/errors. This is source/component evidence only; no installed Live result or availability follows.

```
env HexalithBuildsRoot=/tmp/ext-secrets-1-builds-20261019 pwsh -NoProfile -File eng/verify-ext-secrets-1.ps1 -Mode Local -EventStoreSourceRoot /tmp/ext-secrets-1-eventstore-20261019 -ArtifactsDirectory /tmp/ext-secrets-1-completion-final-20261019-artifacts -EvidenceDirectory /home/administrator/hexalith-secrets-evidence/ext-secrets-1/completion-20261019/validation
```

Dependency source commits:

- EventStore: `dfe963ffd44c4df1d3b43891234e30231a9a28e8`.
- Builds: `b435f2e3b3892e03f69322d24de6003b0baf7a7d`.

The original sibling working checkouts were not used as mutable build inputs. Root-declared EventStore Builds was initialized non-recursively at its gitlink using the local repository; no remote operation or deployment was performed.

Evidence under `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/completion-20261019/validation/`:

| File | SHA-256 |
|---|---|
| `local-tests.xml` | `6ab3a2679dc51278426fa648f3766b245890837bd2d743e53885a60cdda04629` |
| `runtime-tests.xml` | `810f2d8e19874cae86eda9ba7c9be82fd837a40b6fdaa9c5f3e05cf323c25be9` |
| `python-tests.log` | `feebf4917ef317325a93f5945a01b48e3ffcdc7d2faf4cc3121d14dc1f41195a` |
| `local-evidence.json` | `c80301a025e80c75c5b5d8e6e6d1621a7a46d866d712b53f7e619ed064052bd8` |
| `local-build.log` | `73a9a7643dc4e3fe0ac8eb24258c850f3b718af18ba430d257b8d1f96b46f1e3` |
| `runtime-build.log` | `745320cf6c00ff8e47223b3657b15ebeebb976ce50941b257c75bcf190c1d82a` |

`validation-record.json` records the exact command, source dependency revisions and artifact hashes. The final detached handoff additionally binds the reviewed canonical candidate and any focused review-correction checks. The external preflight report is an engineering refusal against the actual register; it is not an accepted owner-command qualification attempt.

After the independent review corrections, the full runtime suite passed **132/132** with a warning/error-free rebuild. The complete Python suite passed **153/153**. Custody source was unchanged from the complete 709-test gate, so that long suite was not repeated. Earlier intermediate correction logs are retained separately; use the final filenames below.

| Final correction evidence | SHA-256 |
|---|---|
| `review-correction-runtime-build.log` | `21c6dad0e079e53fa59908f18b04c14f87be0b9650c22ef9f5c0e7ae917fdb8a` |
| `review-correction-runtime-tests.xml` | `0a4a5f4520eefe0b3990605745cd9eaefc00546b9821819fc4227f8271d426df` |
| `final-python-tests.log` | `c3df25096778b81db0793fde9f8b48d0b492833d14c4ab9ca56d9781485e7f10` |

The final descriptor-exclusion correction then passed the complete **154-test Python suite**. All three independent review layers confirmed closure of their findings; no unresolved code findings remain within the reviewed changes. Physical/runtime delivery blockers remain open as documented.

Final Python log SHA-256: `a9d6d1f2afe1b7646aa51a8a62a456e0bb1fc70b23cbfb5b4d12f64f6badcf09` (`final-descriptor-python-tests.log`).
