#!/usr/bin/env python3
"""Launch a .NET DLL under netcoredbg (CLI interpreter), set breakpoints, and record the hit transcript.

Usage: ncdb_launch.py <netcoredbg> <dll> <cwd> <breakpoint>[;<breakpoint>...] -- <program args...>
A breakpoint is a function name or <absolute file>:<line>, sent verbatim to `break`.
Prints a transcript with '>>>' for sent commands.
"""
import os
import re
import subprocess
import sys
import threading
import time
from datetime import datetime, timezone

ncdb, dll, cwd, breakpoints = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4].split(";")
program_args = sys.argv[sys.argv.index("--") + 1:]

buffer = []
lock = threading.Lock()


def pump(stream):
    while True:
        chunk = os.read(stream.fileno(), 4096)
        if not chunk:
            break
        text = chunk.decode("utf-8", "replace")
        sys.stdout.write(text)
        sys.stdout.flush()
        with lock:
            buffer.append(text)


def output():
    with lock:
        return "".join(buffer)


def wait_for(pattern, start, timeout=120):
    deadline = time.time() + timeout
    regex = re.compile(pattern)
    while time.time() < deadline:
        match = regex.search(output(), start)
        if match:
            return match
        time.sleep(0.1)
    raise TimeoutError(f"pattern {pattern!r} not seen within {timeout}s")


def send(process, command):
    sys.stdout.write(f"\n>>> {command}\n")
    sys.stdout.flush()
    process.stdin.write((command + "\n").encode())
    process.stdin.flush()


def utc():
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


print(f"# utc={utc()} cwd={cwd}")
print(f"# $ {ncdb} --interpreter=cli -- dotnet {dll} {' '.join(program_args)}")
process = subprocess.Popen(
    [ncdb, "--interpreter=cli", "--", "dotnet", dll, *program_args],
    cwd=cwd,
    stdin=subprocess.PIPE,
    stdout=subprocess.PIPE,
    stderr=subprocess.STDOUT,
)
reader = threading.Thread(target=pump, args=(process.stdout,), daemon=True)
reader.start()
hits = 0
try:
    time.sleep(1.0)
    for breakpoint in breakpoints:
        mark = len(output())
        send(process, f"break {breakpoint}")
        wait_for(r"Breakpoint \d+ at ", mark, 30)
    mark = len(output())
    send(process, "run")
    while True:
        match = wait_for(r"stopped, reason: breakpoint \d+ hit|stopped, reason: exited", mark, 180)
        mark = match.end()
        if "breakpoint" not in match.group(0):
            break
        hits += 1
        print(f"\n# HIT={hits} utc={utc()}")
        time.sleep(0.5)
        send(process, "backtrace")
        time.sleep(1.0)
        mark = len(output())
        send(process, "continue")
    time.sleep(1.0)
    try:
        send(process, "quit")
    except BrokenPipeError:
        pass
    process.wait(timeout=30)
finally:
    if process.poll() is None:
        process.kill()
    reader.join(timeout=5)
print(f"\n# netcoredbg exit={process.returncode} breakpoint hits={hits}")
