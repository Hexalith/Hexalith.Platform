#!/usr/bin/env python3
"""Retain validated public archive ranges when one long TLS transfer stalls.

This changes no module content. `go mod download` must still authenticate the
assembled archive against the pinned go.sum before any build consumes it.
"""
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
from pathlib import Path
import subprocess
import re


def fetch(url, output, ranges):
    output, ranges = Path(output), Path(ranges)
    if output.exists() or ranges.exists():
        raise ValueError("Fresh download/range paths required")
    ranges.mkdir(parents=True)
    initial_headers, initial_body = ranges / "initial.headers", ranges / "initial.bin"
    subprocess.run(["curl", "--http1.1", "--fail", "--location", "--silent", "--show-error", "--connect-timeout", "10", "--max-time", "30",
                    "--retry", "3", "--retry-all-errors", "--range", "0-0", "--dump-header", str(initial_headers), "--output", str(initial_body), url], check=True, timeout=150)
    match = re.search(r"content-range: bytes 0-0/(\d+)", initial_headers.read_text().lower())
    if not match or initial_body.stat().st_size != 1:
        raise ValueError("Public endpoint must support bounded byte ranges")
    total = int(match[1])
    chunk = 1024 * 1024

    def part(start):
        end = min(total, start + chunk) - 1
        for attempt in range(4):
            try:
                path = ranges / f"{start:012d}.bin"
                headers = ranges / f"{start:012d}-attempt-{attempt}.headers"
                subprocess.run(["curl", "--http1.1", "--fail", "--location", "--silent", "--show-error", "--connect-timeout", "10", "--max-time", "30",
                                "--range", f"{start}-{end}", "--dump-header", str(headers), "--output", str(path), url], check=True, timeout=35)
                content = path.read_bytes()
                if f"content-range: bytes {start}-{end}/{total}" not in headers.read_text().lower() or len(content) != end - start + 1:
                    raise ValueError("Unexpected public byte range")
                return {"start": start, "end": end, "sha256": hashlib.sha256(content).hexdigest()}
            except (OSError, ValueError, subprocess.SubprocessError):
                if attempt == 3:
                    raise
    with ThreadPoolExecutor(max_workers=8) as pool:
        manifest = list(pool.map(part, range(0, total, chunk)))
    with output.open("wb") as stream:
        for entry in manifest:
            stream.write((ranges / f"{entry['start']:012d}.bin").read_bytes())
    if output.stat().st_size != total:
        raise ValueError("Incomplete assembled archive")
    (ranges / "manifest.json").write_text(json.dumps({"url": url, "bytes": total, "ranges": manifest,
        "sha256": hashlib.sha256(output.read_bytes()).hexdigest()}, indent=2) + "\n")
