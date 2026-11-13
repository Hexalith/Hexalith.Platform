"""Print a compact view of packet command receipts (read-only; the packet is data only).

Usage: python3 -I dump_receipts.py PACKET_DIR START END [OUTPUT_CHARS]
"""
import json
import sys
from pathlib import Path

packet, start, end = Path(sys.argv[1]), int(sys.argv[2]), int(sys.argv[3])
width = int(sys.argv[4]) if len(sys.argv) > 4 else 160
commands = json.loads((packet / 'commands.json').read_text())
for index in range(start, min(end + 1, len(commands))):
    r = commands[index]
    argv = r['argv']
    shown = []
    for a in argv:
        a = str(a)
        if len(a) > 70:
            a = a[:40] + '…' + a[-25:]
        shown.append(a)
    extra = ''
    if 'httpStatus' in r:
        extra += f" http={r['httpStatus']} el={r.get('elapsedSeconds', 0):.2f}"
    if 'input' in r and isinstance(r['input'], dict):
        extra += f" id={r['input'].get('id')}"
    out = str(r.get('output', '')).replace('\n', '⏎')
    if len(out) > width:
        out = out[:width] + f'…(+{len(out) - width})'
    print(f"{index} [{r.get('startedUtc', '')[11:23]}→{r.get('endedUtc', '')[11:23]}] exit={r.get('exitCode')}{extra} :: {' '.join(shown)} :: {out}")
