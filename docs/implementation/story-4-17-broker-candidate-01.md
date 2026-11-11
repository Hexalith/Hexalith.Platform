# Story 4.17 — broker configuration candidate 01

Prepared on 2026-11-11. This is one unexecuted configuration experiment for
EventStore's Platform input. It carries forward the
[2026-11-10 dispositions](story-4-17-local-broker-dispositions-2026-11-10.md)
without re-deriving them. All five guarantees remain **REJECTED**. No Docker,
infrastructure, CI or live qualification ran.

## Inputs and declared provenance

| Item | Value |
| --- | --- |
| Configuration | [story-4-17-broker-candidate-01.json](story-4-17-broker-candidate-01.json), schema 1 |
| Configuration SHA-256 | `472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc` |
| Declared Platform revision | `6d422033b959c249c149a4cb3785704e9eebe03c` |
| Declared Platform path | `docs/implementation/story-4-17-broker-candidate-01.json` |
| Previous EventStore gitlink | `c94e6fffaf2d689c94bb288f151a65e10a2fea82` |
| Candidate EventStore gitlink | `02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213` |
| Explicit profile | `deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json` |

The declared Platform revision is the actual source baseline for preparing this
experiment. The JSON is newly added after that baseline; it is not claimed to
exist in that commit. Revision and path are content-bound, **unverified
declarations**: they do not authenticate Platform's author, prove committed-file
membership or establish accepted execution scope. The exact original JSON must be
supplied independently to both capture and verification, outside the packet.
The packet's retained copy or an alias to it cannot serve as that independent input.

Pushed-revision check on 2026-11-11 (from Platform's root):

```bash
git -C references/Hexalith.EventStore ls-remote --heads origin
git -C references/Hexalith.EventStore merge-base --is-ancestor 02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213 b9a7ba5560e4e7be40f59c9982573fd0ee48668a
```

The first command reported `refs/heads/main` at
`b9a7ba5560e4e7be40f59c9982573fd0ee48668a`; the second exited 0 with no output.
The gitlink selects the exact input-enabling commit, which is contained in pushed
main. Later unrelated changes are outside this pin. Nested submodules were not updated.

## Chosen experiments

These are permitted values, not proven remedies. The JSON contains only the
schema/provenance fields and the four admitted tuning fields.

| Field | Chosen value (original default) | Rejected guarantee and experimental question |
| --- | --- | --- |
| RabbitMQ `reconnectWaitSeconds` | 2 s (1 s) | **RabbitMQ rotation**: probe whether a different reconnect cadence changes the stale-generation outcome within the fixed 3 s observation window. A longer wait could worsen it; no attributable denial is promised. |
| RabbitMQ `outboundTimeoutMilliseconds` | 500 ms (3000 ms) | **RabbitMQ rotation**: probe whether a shorter outbound policy changes the response seen after revocation. A timeout alone cannot establish the required broker-attributable `ACCESS_REFUSED` denial. |
| Kafka `consumeRetryIntervalMilliseconds` | 200 ms (1000 ms) | **Kafka pressure**, and secondarily **Kafka rotation** diagnostics: probe consumer retry/readback timing for missing effects within the existing bounds. This is a consumer setting; it does not change the stale publisher's principal, session or ACL enforcement. |
| Kafka `group.initial.rebalance.delay.ms` | 100 ms (0 ms) | **Kafka pressure**, and secondarily **Kafka rotation** diagnostics: probe sensitivity to a small nonzero initial group delay. This increases the initial delay from the default; it is not assumed to speed rejoin or fix stale-publisher authorization. |

The single candidate combines these settings, so any eventual changed result will
not isolate one setting's causal effect. Kafka rotation remains undiagnosed:
neither admitted Kafka knob addresses its observed stale publisher persistence.

RabbitMQ pressure and missing binding remain **REJECTED** for their separate
upstream dependencies. The retained November 10 record identifies #4748 as
unreleased; pressure needs a released runtime embedding that fix. Missing binding
needs upstream `mandatory` and return handling. These knobs solve neither.

## Offline parser and admission validation

Executed from Platform's repository root with Python 3 and the pinned EventStore
checkout. Exact command:

```bash
PYTHONOPTIMIZE=0 PYTHONPATH=references/Hexalith.EventStore/tools python3 -B - <<'PY'
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import tempfile
from types import SimpleNamespace
from unittest.mock import patch

if not __debug__:
    raise RuntimeError("validation requires Python assertions enabled")

from dapr_platform_broker_configuration import (
    CONFIGURATION_PATH, PROVENANCE_PATH, load_configuration, provenance,
)
from dapr_runtime_qualification import CANDIDATE_PROFILE, PLATFORM_PROFILE

eventstore = Path("references/Hexalith.EventStore")
candidate = Path("docs/implementation/story-4-17-broker-candidate-01.json")
data, configuration = load_configuration(candidate)
expected_sha256 = "472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc"
if hashlib.sha256(data).hexdigest() != expected_sha256:
    raise ValueError("candidate 01 SHA-256 mismatch")
assert configuration["platformPath"] == candidate.as_posix()
spec = importlib.util.spec_from_file_location(
    "broker_producer", eventstore / "tools/qualify-dapr-brokers.py"
)
producer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(producer)

with tempfile.TemporaryDirectory(prefix="platform-broker-admission-") as scratch:
    scratch = Path(scratch)
    with (
        patch.dict(os.environ, {"QUALIFICATION_SUPERVISOR_ROOT": ""}),
        patch.object(tempfile, "tempdir", str(scratch)),
        patch("subprocess.Popen", side_effect=AssertionError("subprocess forbidden")) as process,
        patch("socket.socket", side_effect=AssertionError("socket forbidden")) as network,
    ):
        run = producer.Run(SimpleNamespace(
            profile=eventstore / PLATFORM_PROFILE,
            platform_config=candidate,
            output=scratch / "admission",
        ))
        assert run.platform_configuration == configuration
        assert (run.output / CONFIGURATION_PATH).read_bytes() == data
        assert json.loads((run.output / PROVENANCE_PATH).read_text()) == provenance(data, configuration)
        assert run.packet["schemaVersion"] == 3
        assert run.packet["profile"] == PLATFORM_PROFILE
        assert run.packet["selectedBroker"] is None
        assert run.packet["productionEligible"] is False
        assert run.receipts == [] and run.containers == [] and run.processes == []
        inputs = len(run.profile["inputs"])
        for index, (profile, config) in enumerate((
            (PLATFORM_PROFILE, None),
            (CANDIDATE_PROFILE, candidate),
        )):
            output = scratch / f"rejected-{index}"
            try:
                producer.Run(SimpleNamespace(
                    profile=eventstore / profile, platform_config=config, output=output,
                ))
            except ValueError as error:
                assert str(error) == "platformConfig: configuration and explicit platform profile are required together"
            else:
                raise AssertionError("invalid profile/configuration pairing admitted")
            assert not output.exists()
        process.assert_not_called()
        network.assert_not_called()
assert not scratch.exists()
print("PASS: candidate 01 SHA-256 matches; Python assertions enabled")
print(f"PASS: schema-1 parser; explicit profile admission; {inputs} pinned inputs")
print("PASS: schema-3 metadata; exact retained configuration/provenance; no broker selected")
print("PASS: missing configuration and historical-profile pairing rejected before output")
print("PASS: no subprocess or socket calls; temporary admission files removed")
print("SHA-256: " + hashlib.sha256(data).hexdigest())
PY
```

Result: exit **0**, with this exact stdout:

```text
PASS: candidate 01 SHA-256 matches; Python assertions enabled
PASS: schema-1 parser; explicit profile admission; 91 pinned inputs
PASS: schema-3 metadata; exact retained configuration/provenance; no broker selected
PASS: missing configuration and historical-profile pairing rejected before output
PASS: no subprocess or socket calls; temporary admission files removed
SHA-256: 472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc
```

This checks the expected candidate hash, enables Python assertions explicitly,
and invokes EventStore's actual strict file parser and producer constructor.
The constructor validates the explicit profile and all 91 pinned inputs before
admission. It retains the configuration/provenance in temporary files and creates
schema-3 metadata. The two invalid pairings are rejected before output creation.
Subprocess and socket calls are blocked and asserted unused; all admission/private
files are removed. No preparation, main, finish, worker allocation, broker
execution or qualification packet verification is invoked. Schema-3 metadata here
is an admission observation, not a qualification packet or proof of a guarantee.

## Platform execution handoff — documented only

**Stop and ask before any live run.** This slice authorizes only the offline work
above. Subsequent execution requires Platform's separately accepted scope, runner
support and an infrastructure-owner-issued exclusive disposable worker allocation.
An arbitrary local allocation file cannot establish exclusive ownership. The new
input adds no Platform runner capability or acceptance.

The following is the exact Bash sequence Platform would use after those
prerequisites and live approval, on a supported Linux amd64 runner with local
Unix-socket Docker, Python 3.11+ and OpenSSL. Start in Platform's root, set
`QUALIFICATION_EXCLUSIVE_WORKER` to the absolute path of the genuine owner-issued
allocation, and use fresh, absent tools/output paths. The subshell stops on setup
failure, checks the exact EventStore revision and clean working tree, and checks
the candidate SHA-256 before any installation or execution. The installer obtains
and verifies the pinned tools; it was not run in this slice.

```bash
(
set -eu
PLATFORM_CONFIG="$(pwd -P)/docs/implementation/story-4-17-broker-candidate-01.json"
: "${QUALIFICATION_EXCLUSIVE_WORKER:?set to the absolute owner-issued allocation path}"
EVENTSTORE_REVISION="$(git -C references/Hexalith.EventStore rev-parse HEAD)"
test "$EVENTSTORE_REVISION" = 02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213
EVENTSTORE_STATUS="$(git -C references/Hexalith.EventStore status --porcelain)"
test -z "$EVENTSTORE_STATUS"
printf '%s  %s\n' 472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc "$PLATFORM_CONFIG" | sha256sum --check
test ! -e /tmp/platform-story-4-17-candidate-01-tools
test ! -e /tmp/platform-story-4-17-candidate-01
cd references/Hexalith.EventStore
python3 -B tools/install-dapr-qualification-tools.py --profile deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json --destination /tmp/platform-story-4-17-candidate-01-tools
export QUALIFICATION_DAPRD=/tmp/platform-story-4-17-candidate-01-tools/runtime/daprd
export PATH="/tmp/platform-story-4-17-candidate-01-tools/cli:$PATH"
producer_status=0
python3 -B tools/qualify-dapr-brokers.py --profile deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json --platform-config "$PLATFORM_CONFIG" --brokers rabbitmq,kafka --exclusive-worker "$QUALIFICATION_EXCLUSIVE_WORKER" --output /tmp/platform-story-4-17-candidate-01 || producer_status=$?
integrity_status=0
python3 -B tools/validate-dapr-broker-qualification.py /tmp/platform-story-4-17-candidate-01 --platform-config "$PLATFORM_CONFIG" --allow-failures || integrity_status=$?
technical_status=0
python3 -B tools/validate-dapr-broker-qualification.py /tmp/platform-story-4-17-candidate-01 --platform-config "$PLATFORM_CONFIG" || technical_status=$?
authority_status=0
python3 -B tools/validate-dapr-broker-qualification.py /tmp/platform-story-4-17-candidate-01 --platform-config "$PLATFORM_CONFIG" --require-production-authority || authority_status=$?
printf 'producer=%s integrity=%s technical=%s authority=%s\n' "$producer_status" "$integrity_status" "$technical_status" "$authority_status"
test "$producer_status" -eq 0 && test "$integrity_status" -eq 0 && test "$technical_status" -eq 0 && test "$authority_status" -eq 0
)
```

The producer and each evaluation retain their own exit code; expected failures
cannot prevent the remaining evaluations even with inherited `errexit`. The final
subshell status is nonzero if any evaluation fails. The verifier calls use the same
separate original JSON to evaluate capture integrity with truthful failures,
technical qualification, and production authority, respectively. `--allow-failures` never converts a
rejected guarantee to PASS. Timeouts and missing bounded effects retain their
uncertainty/FAIL meaning; cold stale-generation restart and exact Kafka causality
are not established by this candidate.

The explicit profile retains runtime/CLI 1.19.1, SDK/components 1.19.0, pinned
broker images, both brokers and all 24 vectors. Harness assertions, TLS,
authorization, durability, pressure controls, publication counts and observation
bounds remain EventStore-owned and unchanged. Platform owns eventual configuration
execution; EventStore evaluates the results. No broker is selected, no exception
is granted, production eligibility stays FAIL, and Platform 4.17 and EventStore
3.21 remain open.

Source contracts at the pinned EventStore revision:
[qualification README](../../references/Hexalith.EventStore/deploy/dapr/qualification/README.md#bounded-platform-configuration-input),
[input-enabling spec](../../references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-3-21-platform-broker-configuration-input.md),
and [schema-1 parser](../../references/Hexalith.EventStore/tools/dapr_platform_broker_configuration.py).
