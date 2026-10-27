#!/usr/bin/env python3
"""Warn about slow tests based on the rolling VSTest JSONL history."""

import json
from collections import defaultdict
from pathlib import Path

LOG = Path(__file__).resolve().parents[2] / ".test-metrics" / "test-runtimes.jsonl"


def field(record: dict, name: str):
    """Read a field whatever casing the logger wrote it in."""
    return record.get(name, record.get(name.lower()))


def main() -> None:
    if not LOG.exists():
        return

    runs = defaultdict(list)
    for line in LOG.read_text(encoding="utf-8").splitlines():
        try:
            record = json.loads(line)
            if field(record, "Outcome") == "Passed":
                runs[field(record, "Test")].append(float(field(record, "DurationMs")))
        except (ValueError, KeyError, TypeError):
            continue

    warnings = []
    for test, durations in runs.items():
        first, last = durations[0], durations[-1]
        if len(durations) == 1 and last >= 30_000:
            warnings.append(f"{test}: one recorded run took {last / 1000:.1f}s")
        elif len(durations) > 1 and first > 0 and last >= first * 1.5:
            warnings.append(
                f"{test}: runtime increased from {first / 1000:.2f}s "
                f"to {last / 1000:.2f}s ({last / first:.0%} of first run)"
            )

    for warning in warnings:
        print(f"WARNING: slow test: {warning}")


if __name__ == "__main__":
    main()
