#!/usr/bin/env python3
"""Attach to the live service child PID, break on /ready, then detach safely."""
import os
import re
import subprocess
import sys
import threading
import time
import urllib.request

executable, pid, source, url = sys.argv[1:]
process = subprocess.Popen([executable, '--interpreter=cli', '--attach', pid], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
output = []
lock = threading.Lock()

def pump():
    while True:
        chunk = os.read(process.stdout.fileno(), 4096)
        if not chunk:
            return
        text = chunk.decode('utf-8', 'replace')
        print(text, end='', flush=True)
        with lock:
            output.append(text)

threading.Thread(target=pump, daemon=True).start()

def text():
    with lock:
        return ''.join(output)

def wait(pattern, start=0, timeout=30):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        match = re.search(pattern, text()[start:])
        if match:
            return match
        if process.poll() is not None:
            raise RuntimeError('Debugger exited before expected output')
        time.sleep(.1)
    raise TimeoutError(pattern)

def send(command):
    print('\n>>> ' + command, flush=True)
    process.stdin.write((command + '\n').encode())
    process.stdin.flush()

response = []
def request():
    with urllib.request.urlopen(url, timeout=60) as result:
        response.append((result.status, result.headers.get('X-Hexalith-Parties-Mvp-Compliance-Warning'), result.read().decode()))

try:
    wait(r'stopped|\(ncdb\)')
    mark = len(text())
    send('break ' + source + ':9')
    wait(r'Breakpoint \d+ at ', mark)
    mark = len(text())
    send('continue')
    requester = threading.Thread(target=request)
    requester.start()
    wait(r'stopped, reason: breakpoint \d+ hit', mark, timeout=40)
    send('backtrace')
    time.sleep(1)
    send('delete')
    send('detach')
    requester.join(timeout=30)
    assert response and response[0][0] == 200, response
    print('\nREADY RESPONSE:', response, flush=True)
    print('BREAKPOINT PROOF PASSED', flush=True)
finally:
    if process.poll() is None:
        try:
            send('detach')
            send('quit')
            process.wait(timeout=10)
        except Exception:
            process.terminate()
            process.wait(timeout=10)
