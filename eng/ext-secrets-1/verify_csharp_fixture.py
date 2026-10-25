"""Isolated C# source/receiving fixture verification. Never installation or physical evidence."""
import copy
import json
import sys
from pathlib import Path
import qualify as q
from cases import plan


def verify(path):
    data = json.loads(Path(path).read_text())
    request, config, binding = (data[k] for k in ("request", "config", "binding"))
    case = next(case for case in plan() if case.id == request["caseId"])
    execution = q.verify_signed(data["execution"], "executor", config, binding)
    q.check_binding(execution, request)
    observation = q.verify_signed(data["observation"], "observer", config, binding)
    q.check_observation(case, observation, execution, request)
    for field, replacement in (("targetCommit", "f" * 40), ("requestId", "f" * 64)):
        changed = copy.deepcopy(request)
        changed[field] = replacement
        try:
            q.check_observation(case, observation, execution, changed)
        except q.QualificationFailed:
            pass
        else:
            raise AssertionError("changed request binding accepted: " + field)
    changed = copy.deepcopy(data["observation"])
    changed["payload"]["snapshots"][0]["originalResultSha256"] = "f" * 64
    try:
        q.verify_signed(changed, "observer", config, binding)
    except q.QualificationFailed:
        pass
    else:
        raise AssertionError("changed source receipt accepted")
    print("CSharpReceivingSourceFixture: passed")


if __name__ == "__main__":
    verify(sys.argv[1])
