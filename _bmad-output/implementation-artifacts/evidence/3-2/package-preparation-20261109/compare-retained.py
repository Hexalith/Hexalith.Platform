#!/usr/bin/env python3
"""Compare selected fresh cases with both retained broad inventories; do not infer a broad rerun."""
import collections
import json
from pathlib import Path
import xml.etree.ElementTree as ET

here = Path(__file__).resolve().parent
retained = here.parent / "broad-verification-20261109"


def normalize(name):
    # xUnit XML and the old JSON runner encode the same quote with different escaping.
    return name.replace("\\", "")


def xml_cases(path):
    return [{"name": case.attrib["name"], "result": case.attrib["result"]}
            for case in ET.parse(path).iter("test")]


def compare(cases, old):
    index = collections.defaultdict(list)
    for case in old:
        index[normalize(case["name"])].append(case["result"])
    rows = []
    for case in cases:
        previous = index[normalize(case["name"])]
        assert len(previous) <= 1, "Ambiguous retained display name: " + case["name"]
        rows.append({"name": case["name"], "fresh": case["result"],
                     "retained": previous[0] if previous else None})
    return {"freshCounts": dict(collections.Counter(case["result"] for case in cases)),
            "common": sum(row["retained"] is not None for row in rows),
            "added": [row["name"] for row in rows if row["retained"] is None],
            "regressions": [row for row in rows if row["fresh"] == "Fail" and row["retained"] not in (None, "Fail")],
            "freshFailures": [row for row in rows if row["fresh"] == "Fail"], "cases": rows}


result = {"scope": "Focused fresh source and unchanged-pin Release/package selections versus retained Debug broad inventories; no fresh full-suite pass or broad failure total is inferred.",
          "baselineRevision": "f7c68398cdcb494d535e789e3d9577af9e8d5d36",
          "priorCurrentRevision": "59fb32933d76a7e71d9479e1903087a73ea66f2b", "comparisons": {}}
for lane, filename in [("source", "parent-source-tests.xml"),
                       ("package", "story32-preparation-published-package-classes.xml")]:
    cases = xml_cases(here / "logs" / filename)
    result["comparisons"][lane] = {}
    for side in ["baseline", "current"]:
        old = json.loads((retained / (side + "-results.json")).read_text())
        result["comparisons"][lane][side] = compare(cases, old)
result["story28"] = {"retainedAttribution": json.loads((retained / "regression-attribution.json").read_text()),
                     "freshObservation": "Modified preparation tree refuses earlier at the trusted parent-plan/key and unchanged-source/test guard. Keep this distinct from the retained genuine Memories Contracts prerequisite; neither is an extension failure.",
                     "checksWeakened": False}
(here / "retained-comparison.json").write_text(json.dumps(result, indent=2) + "\n")
print(json.dumps({lane: {side: {key: len(value) if isinstance(value, list) else value for key, value in data.items() if key != "cases"}
                        for side, data in sides.items()} for lane, sides in result["comparisons"].items()}, indent=2))
