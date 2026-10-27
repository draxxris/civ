#!/usr/bin/env python3
"""Warn about slow tests based on the rolling VSTest JSONL history."""

import json
from collections import defaultdict
from pathlib import Path

LOG = Path(__file__).resolve().parents[2] / ".test-metrics" / "test-runtimes.jsonl"

# Categories that never run in the default suite. Their tests are long by design, so warning that a
# whole-game soak grew past a second would only bury the warnings that matter.
OTHER_LANES = {"E2E", "WorldGen", "Extractor"}
MIN_REPORT_MS = 500
MIN_GROWTH_MS = 500
ONE_OFF_MS = 120_000
MIN_SAMPLES = 3


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
            if field(record, "Outcome") != "Passed":
                continue
            # Rows written before the logger recorded a category cannot be split by lane, and the ones from the
            # era when the log rewrote itself per result are inflated by that I/O, so they would drown the real
            # warnings.
            if field(record, "Category") is None:
                continue
            categories = (field(record, "Category") or "").split(",")
            if any(category in OTHER_LANES for category in categories):
                continue
            runs[field(record, "Test")].append(float(field(record, "DurationMs")))
        except (ValueError, KeyError, TypeError):
            continue

    warnings = []
    for test, durations in runs.items():
        first, last = durations[0], durations[-1]
        if len(durations) == 1:
            if last >= ONE_OFF_MS:
                warnings.append(f"{test}: one recorded run took {last / 1000:.1f}s")
        elif (
            len(durations) >= MIN_SAMPLES
            and last >= MIN_REPORT_MS
            and last - first >= MIN_GROWTH_MS
            and last >= first * 1.5
        ):
            warnings.append(
                f"{test}: runtime increased from {first / 1000:.2f}s "
                f"to {last / 1000:.2f}s ({last / first:.0%} of first run)"
            )

    for warning in warnings:
        print(f"WARNING: slow test: {warning}")


if __name__ == "__main__":
    main()
