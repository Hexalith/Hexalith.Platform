"""Read a created PR and compare parsed JSON strings as exact UTF-8 bytes.

Invoke only after the parent has submitted the approved subject. No remote edits.
--input supports offline verification against a saved gh JSON response.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess


def compare(response, title, body, head):
    return {
        'titleBytesMatch': response['title'].encode('utf-8') == title,
        'bodyBytesMatch': response['body'].encode('utf-8') == body,
        'headMatches': response['headRefOid'] == head,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('pr', help='the newly created pull request number or URL')
    parser.add_argument('--input', type=Path)
    args = parser.parse_args()
    out = Path(__file__).resolve().parent
    command = ['gh', 'pr', 'view', args.pr, '-R', 'dapr/components-contrib', '--json', 'title,body,headRefOid']
    record = {
        'checkedUtc': datetime.now(timezone.utc).isoformat(),
        'pullRequest': args.pr,
        'status': 'ATTEMPTED',
        'command': command if not args.input else None,
        'input': str(args.input) if args.input else None,
        'rawResponse': 'pr-readback-raw.json',
        'rawStderr': 'pr-readback-stderr.log',
        'method': 'parse gh JSON, then compare the decoded strings encoded as UTF-8; no --jq newline',
    }
    raw, stderr = b'', b''
    # Persist the attempted identity before any command or parse can fail.
    (out / 'pr-readback.json').write_text(json.dumps(record, indent=2) + '\n')
    try:
        if args.input:
            raw = args.input.read_bytes()
            record['exitCode'] = None
        else:
            result = subprocess.run(command, capture_output=True)
            raw, stderr = result.stdout, result.stderr
            record['exitCode'] = result.returncode
            if result.returncode:
                record['status'] = 'COMMAND_FAILED'
                record['error'] = 'gh exited with status ' + str(result.returncode)
        if record['status'] == 'ATTEMPTED':
            record['status'] = 'INVALID_RESPONSE'
            response = json.loads(raw)
            commit = json.loads((out / 'commit-identity.json').read_text())
            title = (out / 'pr-title.txt').read_bytes()
            body = (out / 'pr-body.md').read_bytes()
            checks = compare(response, title, body, commit['head'])
            record.update({
                'status': 'PASS' if all(checks.values()) else 'MISMATCH',
                'checks': checks,
                'headRefOid': response['headRefOid'],
                'titleSha256': hashlib.sha256(response['title'].encode('utf-8')).hexdigest(),
                'bodySha256': hashlib.sha256(response['body'].encode('utf-8')).hexdigest(),
            })
    except Exception as error:
        if record['status'] == 'ATTEMPTED':
            record['status'] = 'COMMAND_FAILED'
        record['error'] = str(error)
    (out / 'pr-readback-raw.json').write_bytes(raw)
    (out / 'pr-readback-stderr.log').write_bytes(stderr)
    (out / 'pr-readback.json').write_text(json.dumps(record, indent=2, ensure_ascii=False) + '\n')
    print(json.dumps(record, indent=2))
    raise SystemExit(0 if record['status'] == 'PASS' else 3)


if __name__ == '__main__':
    main()
