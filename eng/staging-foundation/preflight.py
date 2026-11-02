"""Guarded, offline candidate entry point for Story 4.14.

Validate the complete input and resource relationships before calling the
source-pinned renderer. No command in this module executes a plan or probes a
cluster.
"""

import argparse
from decimal import Decimal, DecimalException
import json
from pathlib import Path
import re
import sys

import render


# Kubernetes Quantity: decimal mantissa with SI, binary SI, or decimal exponent.
QUANTITY = re.compile(r"^([+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+))([eE][+-]?[0-9]+|Ki|Mi|Gi|Ti|Pi|Ei|[numkMGTPE])?$")
SCALES = {
    "": Decimal(1), "n": Decimal("1e-9"), "u": Decimal("1e-6"),
    "m": Decimal("1e-3"), "k": Decimal(1000), "M": Decimal(1000) ** 2,
    "G": Decimal(1000) ** 3, "T": Decimal(1000) ** 4,
    "P": Decimal(1000) ** 5, "E": Decimal(1000) ** 6,
    **{f"{unit}i": Decimal(1024) ** power for power, unit in enumerate("KMGTPE", 1)},
}
LIMIT_FIELDS = {
    "defaultRequestCpu": "cpu", "defaultRequestMemory": "memory",
    "defaultLimitCpu": "cpu", "defaultLimitMemory": "memory",
    "pvcMinStorage": "storage", "pvcMaxStorage": "storage",
}
QUOTA_FIELDS = {
    "requestsCpu": "cpu", "requestsMemory": "memory",
    "limitsCpu": "cpu", "limitsMemory": "memory",
    "pods": "count", "persistentVolumeClaims": "count",
    "requestsStorage": "storage",
}


def quantity(value, kind):
    """Return a comparable base-unit Decimal or reject malformed quantities."""
    if not isinstance(value, str):
        raise ValueError("must be a Kubernetes quantity string")
    match = QUANTITY.fullmatch(value)
    if match is None:
        raise ValueError("must be a Kubernetes quantity with a valid suffix")
    mantissa, suffix = match.groups()
    suffix = suffix or ""
    try:
        if suffix in SCALES:
            result = Decimal(mantissa) * SCALES[suffix]
        else:
            result = Decimal(mantissa) * (Decimal(10) ** int(suffix[1:]))
    except (DecimalException, OverflowError, ValueError, KeyError):
        raise ValueError("quantity is outside the supported numeric range") from None
    if not result.is_finite() or result < 0:
        raise ValueError("must be a nonnegative finite quantity")
    if result > 2**63 - 1:
        raise ValueError("quantity exceeds Kubernetes' maximum magnitude")
    if kind == "cpu" and result % Decimal("0.001") != 0:
        raise ValueError("CPU quantity must have at most 1m precision")
    if kind == "count" and result != result.to_integral_value():
        raise ValueError("count quantity must represent a whole number")
    return result


def validate(inputs):
    """Report every unsafe or unresolved field before candidate qualification."""
    problems = []
    try:
        render.validate(inputs)
    except render.InputError as error:
        problems.extend(error.problems)

    values = {}
    for prefix, fields in (("limitRange", LIMIT_FIELDS),
                           ("quotas.application", QUOTA_FIELDS),
                           ("quotas.data", QUOTA_FIELDS)):
        for field, kind in fields.items():
            path = f"{prefix}.{field}"
            raw = render._get(inputs, path)
            if raw is None or render._placeholder(raw):
                continue
            try:
                values[path] = quantity(raw, kind)
            except ValueError as error:
                problems.append(f"{path}: {error}")

    def ordered(low, high):
        if low in values and high in values and values[low] > values[high]:
            problems.append(f"{low}: exceeds {high}")

    for resource in ("Cpu", "Memory"):
        ordered(f"limitRange.defaultRequest{resource}", f"limitRange.defaultLimit{resource}")
    ordered("limitRange.pvcMinStorage", "limitRange.pvcMaxStorage")
    for scope in ("application", "data"):
        prefix = f"quotas.{scope}."
        if values.get(prefix + "pods", 0) > 0:
            for quota, default in (("requestsCpu", "defaultRequestCpu"),
                                   ("requestsMemory", "defaultRequestMemory"),
                                   ("limitsCpu", "defaultLimitCpu"),
                                   ("limitsMemory", "defaultLimitMemory")):
                qpath, dpath = prefix + quota, "limitRange." + default
                if qpath in values and dpath in values and values[qpath] < values[dpath]:
                    problems.append(f"{qpath}: cannot fit one {dpath}")
        if values.get(prefix + "persistentVolumeClaims", 0) > 0:
            qpath, dpath = prefix + "requestsStorage", "limitRange.pvcMinStorage"
            if qpath in values and dpath in values and values[qpath] < values[dpath]:
                problems.append(f"{qpath}: cannot fit one {dpath}")
    if problems:
        raise render.InputError(problems)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    candidate = commands.add_parser("render")
    candidate.add_argument("--inputs", required=True)
    candidate.add_argument("--output", required=True)
    plan = commands.add_parser("plan")
    plan.add_argument("--inputs", required=True)
    plan.add_argument("--observed-context", required=True)
    plan.add_argument("--observed-cluster-uid", required=True)
    plan.add_argument("--manifest-dir", default=".")
    args = parser.parse_args(argv)
    try:
        try:
            inputs = json.loads(Path(args.inputs).read_text())
        except (OSError, ValueError) as error:
            raise render.InputError([f"--inputs unreadable or not valid JSON: {error}"])
        validate(inputs)
        if args.command == "render":
            files = render.render(inputs)
            output = Path(args.output)
            output.mkdir(parents=True, exist_ok=True)
            for name, content in files.items():
                (output / name).write_text(content)
        else:
            result = render.build_apply_plan(inputs, args.observed_context,
                                             args.observed_cluster_uid, args.manifest_dir)
            print(json.dumps(result, indent=2))
    except render.InputError as error:
        for problem in error.problems:
            print(f"refused: {problem}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
