# Story 2.5 fresh live source qualification

Status: **source-live-qualified**, 2026-10-28. Sprint status remains **review** pending acceptance.

The reviewed source passed the real lifecycle test with all four scenarios in **384.682 seconds**. Success cleans automatically. Test failure, execution timeout and partial-startup failure retain survivors until explicit down. Repeated down succeeds. Each scenario preserves the independent peer's four containers, three stable processes and Redis value `unchanged`, compared with its retained pre-scenario baseline. The independent host audit finds no tagged processes or containers for any of the five owners; active state, run workspaces and outstanding resource claims are absent. Five deliberately durable empty owner receipts remain as ownership proofs and are independently retained and verified.

All three retained-failure scenarios' first-down attempts returned `HXR028` with empty resource observations. Exact retries and subsequent idempotent down succeeded. The complete retry sequence is checked against `verification.json` and remains in `live-final/cleanup-*.json`. These observations exercise the existing bounded retry contract; no product fix was required.

The isolated home is `/home/administrator/.hexalith/dapr/cli-1.18.2-runtime-1.18.7`. Login and interactive shell profiles export `HEXALITH_DAPR_HOME`. It contains CLI **1.18.2** at `tools/dapr` and runtime **1.18.7** binaries `daprd`, `placement` and `scheduler` at `.dapr/bin/`. Archive checksums were checked against both upstream release API digests and upstream checksum files. `dapr-installation.json` retains download URLs, archive/binary hashes, catalog identity, shell-file hashes and local backup locations. `prerequisites.json` records matching Aspire **13.6.4**, Docker and .NET availability.

Both Debug builds pass with zero warnings/errors. `source-before-final.json` and `source-after-final.json` bind **261 unchanged source/configuration files**, including AppHost, Tooling and central build inputs. `prior-source-fingerprints.json` independently establishes all **184** matches to the prior **950-case** offline verification. The full offline suites were **not rerun**; the focused cleanup-helper regression was rerun and passed **1/1**. Individual builds use timestamped local versions, so `executed-assemblies.json` records each exact assembly identity. The qualification fixture now saves its peer baseline and initial results before down; no product source, catalog pin or published artifact changed.

The following files provide the audit trail:

- `verification.json`: qualification summary, scenario outcomes, retry history and scope.
- `live-qualification-final.command.json`, `.log`, `.log.gz` and `platform-live-tests-final.xml`: exact environment/command, timestamps, exit status and passing live test.
- `live-final/`: per-scenario commands, retention listings, real resource observations, complete cleanup attempts, peer audits and final cleanup receipts.
- `live-final/peer-before.json`: peer identities and Redis sentinel recorded before the first scenario.
- `live-final/initial-diagnostics/`, `live-final/diagnostics/`: snapshots before explicit down and after per-scenario cleanup. The audit compares terminal outcome/cause/time, bindings and historical failure identities, and checks each snapshot's diagnostic hashes.
- `live-final/final-diagnostics/`: additional snapshots after the fixture's final cleanup replay; `final-runtime-archive.json` binds the complete filename inventory, original locations and hashes.
- `runtime-audit.json`: immutable first-result equality, complete final snapshot byte equality against runtime files, diagnostic hashes and absent workspaces/state/outstanding claims.
- `claim-receipts.json`, `claim-receipts/`: independently retained empty ownership receipts for all five owners.
- `host-before.json`, `host-after.json`: independent Docker label and Linux process-owner scans. The preexisting protected user `systemd` process remains identified by PID and start time; no new uninspectable process appears. Ambient container changes are listed with attribution unavailable; those observations grant no isolation credit.
- `retained-audit.json`, `portability.json`: offline validation and successful auditing from a temporary location with deliberately unavailable original runtime paths. Five negative controls reject missing archive files, lost failure history, changed initial peer identity, missing cleanup attempts and mismatched prior source hashes.
- `review-triage.json`, `review-original.md`: individual findings, corrections, follow-up verification and no new deferrals.
- `script-snapshots/`: Python helper bytes captured by hash before new commands execute. `superseded-scripts/` explicitly identifies reconstructed historical failed assertions, for which no pre-execution hash is claimed.
- `evidence-sha256.json`: final evidence-file inventory and SHA-256 identities.

All command records retain original and compressed log hashes. Original logs have an exact Git ignore exception; AppHost logs also have compressed copies. Superseded audit failures are preserved: the initial host scan rejected the preexisting protected systemd process; the initial archive check incorrectly compared mutable cleanup bookkeeping across later cleanup attempts. The final audits explicitly track inspection uncertainty and preserve all three snapshots while checking immutable fields and final byte parity.

The earlier **423.363-second** passing live attempt remains under `live/`, with its original XML, commands and `*-before-review` verification/audit files. `host-before-before-review.json` and `host-after-before-review.json` preserve its full host inventories. It predates the stronger retained observations and is superseded by `live-final/` for the final qualification.

Run the retained audit from any copied evidence directory:

```bash
python3 audit.py
python3 relocate-audit.py
```

While original runtime diagnostic files still exist, reobserve them with:

```bash
python3 audit.py --check-runtime
```

The final live invocation is captured verbatim in `live-qualification-final.command.json`. Use a new empty evidence directory and a new XML path for another live attempt. `run-command.py` refuses to replace existing command logs. `setup-dapr.py` installs only into a new isolated home and refuses to replace an existing installation.

This qualifies the **Linux/WSL local source lifecycle**. It grants no package, candidate-acceptance or Windows/macOS native qualification. Existing decisions and deferrals remain. No push was performed.
