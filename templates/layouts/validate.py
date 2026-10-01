#!/usr/bin/env python3
"""
Validator for templates/layouts/*.json (wave2-spec.md section 5).

For every template, asserts:
  - required schema fields are present (name, version, description, geometryStatus, anchor,
    roles, nodes, segments, footprint)
  - every segment's from/to node id exists in nodes
  - every segment length (straight chord between its two nodes, XZ only) is <= 100 m
  - no two DISTINCT-named nodes are closer than 8 m (the bridge's snapDistance) unless they are
    identical points (same x, z - elevation may differ for a stacked crossing)
  - grade per segment, computed as |dElevation| / xzLength, is <= 8% (0.08)

Run from the repo root or this directory:
    python3 templates/layouts/validate.py
Exits non-zero if any template fails any assertion.
"""
import json
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REQUIRED_FIELDS = ["name", "version", "description", "geometryStatus", "anchor", "roles", "nodes", "segments", "footprint"]
MAX_SEGMENT_LEN_M = 100.0
MIN_NODE_SEPARATION_M = 8.0  # snapDistance
MAX_GRADE = 0.08


def xz_dist(a, b):
    return math.hypot(a["x"] - b["x"], a["z"] - b["z"])


def validate_file(path):
    errors = []
    try:
        data = json.loads(path.read_text())
    except Exception as e:
        return [f"invalid JSON: {e}"]

    for field in REQUIRED_FIELDS:
        if field not in data:
            errors.append(f"missing required field: {field}")
    if errors:
        return errors  # can't validate further without the basics

    nodes = data["nodes"]
    segments = data["segments"]

    # every segment's from/to node exists
    for i, seg in enumerate(segments):
        for key in ("from", "to"):
            if seg.get(key) not in nodes:
                errors.append(f"segment[{i}] ({seg.get('name', seg.get('role'))}): {key} node '{seg.get(key)}' not in nodes")

    # segment length <= 100 m, grade <= 8%
    for i, seg in enumerate(segments):
        a = nodes.get(seg.get("from"))
        b = nodes.get(seg.get("to"))
        if not a or not b:
            continue  # already reported above
        length = xz_dist(a, b)
        label = seg.get("name") or f"{seg.get('from')}->{seg.get('to')}"
        if length > MAX_SEGMENT_LEN_M:
            errors.append(f"segment[{i}] ({label}): length {length:.1f} m exceeds {MAX_SEGMENT_LEN_M} m")
        d_elev = abs(a.get("elevation", 0) - b.get("elevation", 0))
        if length > 0:
            grade = d_elev / length
            if grade > MAX_GRADE:
                errors.append(f"segment[{i}] ({label}): grade {grade*100:.1f}% exceeds {MAX_GRADE*100:.0f}% (dElev {d_elev} over {length:.1f} m)")
        elif d_elev > 0:
            errors.append(f"segment[{i}] ({label}): zero-length segment with nonzero elevation delta ({d_elev})")

    # no two distinct nodes closer than 8 m unless identical (same x,z)
    names = list(nodes.keys())
    for i in range(len(names)):
        for j in range(i + 1, len(names)):
            n1, n2 = nodes[names[i]], nodes[names[j]]
            d = xz_dist(n1, n2)
            identical = (n1["x"] == n2["x"] and n1["z"] == n2["z"])
            if not identical and d < MIN_NODE_SEPARATION_M:
                errors.append(
                    f"nodes '{names[i]}' and '{names[j]}' are {d:.1f} m apart (< {MIN_NODE_SEPARATION_M} m snap distance) and not identical"
                )

    # prefab roles sanity: every role has a default present in its own allowed list
    for role_name, role in data.get("roles", {}).items():
        default = role.get("default")
        allowed = role.get("allowed", [])
        if default not in allowed:
            errors.append(f"role '{role_name}': default '{default}' is not in its own allowed list {allowed}")

    return errors


def main():
    files = sorted(HERE.glob("*.json"))
    if not files:
        print("no templates found in", HERE)
        return 1
    total_errors = 0
    for f in files:
        errors = validate_file(f)
        if errors:
            total_errors += len(errors)
            print(f"FAIL {f.name}")
            for e in errors:
                print(f"  - {e}")
        else:
            print(f"ok   {f.name}")
    print()
    if total_errors:
        print(f"{total_errors} error(s) across {len(files)} template(s)")
        return 1
    print(f"all {len(files)} templates passed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
