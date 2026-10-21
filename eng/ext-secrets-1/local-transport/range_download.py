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
import shutil
import http.client
import socket
import threading
from urllib.parse import urlsplit


def fetch(url, output, ranges, seeds=(), storage_address=None):
    output, ranges = Path(output), Path(ranges)
    if output.exists() or ranges.exists():
        raise ValueError("Fresh download/range paths required")
    ranges.mkdir(parents=True)
    initial_headers, initial_body = ranges / "initial.headers", ranges / "initial.bin"
    resolution = ["--resolve", "storage.googleapis.com:443:" + storage_address] if storage_address else []
    for seed in map(Path, seeds):
        if (seed / "initial.headers").is_file() and (seed / "initial.bin").is_file():
            shutil.copyfile(seed / "initial.headers", initial_headers); shutil.copyfile(seed / "initial.bin", initial_body)
            break
    if not initial_headers.exists():
        subprocess.run(["curl", "--ipv4", "--http1.1", "--fail", "--location", "--silent", "--show-error", "--connect-timeout", "10", "--max-time", "30",
                        "--retry", "3", "--retry-all-errors", "--range", "0-0", "--dump-header", str(initial_headers), "--output", str(initial_body), *resolution, url], check=True, timeout=150)
    match = re.search(r"content-range: bytes 0-0/(\d+)", initial_headers.read_text().lower())
    if not match or initial_body.stat().st_size != 1:
        raise ValueError("Public endpoint must support bounded byte ranges")
    total = int(match[1])
    locations = re.findall(r"(?im)^location:\s*(https://storage.googleapis.com/[^\r\n]+)", initial_headers.read_text())
    download_url = locations[-1] if locations else url
    remote = urlsplit(download_url)
    transport = threading.local()
    chunk = 1024 * 1024

    def part(start):
        end = min(total, start + chunk) - 1
        path = ranges / f"{start:012d}.bin"
        for seed in map(Path, seeds):
            original = seed / path.name
            if original.is_file() and original.stat().st_size == end - start + 1:
                shutil.copyfile(original, path)
                return {"start": start, "end": end, "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "retainedSeed": str(original)}
        for attempt in range(4):
            try:
                if not hasattr(transport, "connection"):
                    transport.connection = http.client.HTTPSConnection(remote.hostname, timeout=15)
                    if storage_address:
                        transport.connection._create_connection = lambda address, timeout, source_address=None: socket.create_connection((storage_address, address[1]), timeout, source_address)
                transport.connection.request("GET", remote.path + ("?" + remote.query if remote.query else ""), headers={"Range": f"bytes={start}-{end}"})
                response = transport.connection.getresponse()
                content = response.read()
                (ranges / f"{start:012d}-attempt-{attempt}.json").write_text(json.dumps({"status": response.status, "contentRange": response.getheader("Content-Range"), "bytes": len(content)}))
                if response.status != 206 or response.getheader("Content-Range") != f"bytes {start}-{end}/{total}" or len(content) != end - start + 1:
                    raise ValueError("Unexpected public byte range")
                path.write_bytes(content)
                return {"start": start, "end": end, "sha256": hashlib.sha256(content).hexdigest()}
            except (OSError, ValueError, http.client.HTTPException):
                if hasattr(transport, "connection"):
                    transport.connection.close(); del transport.connection
                if attempt == 3:
                    raise
    with ThreadPoolExecutor(max_workers=2) as pool:
        manifest = list(pool.map(part, range(0, total, chunk)))
    with output.open("wb") as stream:
        for entry in manifest:
            stream.write((ranges / f"{entry['start']:012d}.bin").read_bytes())
    if output.stat().st_size != total:
        raise ValueError("Incomplete assembled archive")
    (ranges / "manifest.json").write_text(json.dumps({"url": url, "resolvedPublicStorageAddress": storage_address, "bytes": total, "ranges": manifest,
        "sha256": hashlib.sha256(output.read_bytes()).hexdigest()}, indent=2) + "\n")
