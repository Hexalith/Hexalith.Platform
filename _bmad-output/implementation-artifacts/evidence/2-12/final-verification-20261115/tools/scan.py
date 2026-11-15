#!/usr/bin/env python3
"""Scan evidence candidates for credential-shaped content before retention."""
import re
import sys
from pathlib import Path

PATTERNS = {
    "jwt": re.compile(r"eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}"),
    "private-key": re.compile(r"-----BEGIN [A-Z ]*PRIVATE KEY-----"),
    "certificate": re.compile(r"-----BEGIN CERTIFICATE-----"),
    "client-secret": re.compile(r"client_secret\s*[=:]\s*[^\s\"',;]{6,}", re.I),
    "password-value": re.compile(r"PASSWORD=(?!\"\s*\+)[^\s\"',;]{4,}"),
    "bearer": re.compile(r"Bearer\s+[A-Za-z0-9._-]{16,}"),
    "capability": re.compile(r"capability\"?\s*[:=]\s*\"?[0-9a-f]{32,}", re.I),
    "access-token": re.compile(r"access_token\"?\s*[:=]\s*\"?[A-Za-z0-9._-]{16,}", re.I),
}

hits = 0
for name in sys.argv[1:]:
    for path in ([Path(name)] if Path(name).is_file() else sorted(Path(name).rglob("*"))):
        if not path.is_file():
            continue
        text = path.read_text(errors="replace")
        for label, pattern in PATTERNS.items():
            for match in pattern.finditer(text):
                hits += 1
                print(f"{path}: {label}: {match.group(0)[:40]}...")
print(f"hits={hits}")
sys.exit(1 if hits else 0)
