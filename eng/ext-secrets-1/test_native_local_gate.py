"""Actual Local entrypoint must stop when its native console fails. No owner/Live command."""
import os
from pathlib import Path
import shutil
import shlex
import subprocess
import tempfile
import unittest

class NativeLocalGate(unittest.TestCase):
    @unittest.skipUnless(shutil.which('pwsh') and shutil.which('dotnet'),'PowerShell is required for the real Local entrypoint')
    def test_failed_native_console_fails_local_before_other_suites(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);fake=root/'dotnet'
            real_dotnet=shlex.quote(shutil.which('dotnet'))
            fake.write_text('#!/bin/sh\nif [ "$1" = build ]; then printf "    0 Warning(s)\\n    0 Error(s)\\n"; exit 0; fi\ncase "$1" in *Hexalith.Platform.Custody.Dapr.Tests.dll) printf "sourceComponentOnly=true physicalQualification=false passed=0 failed=1\\n"; exit 42;; esac\nexec '+real_dotnet+' "$@"\n');fake.chmod(0o700)
            gate=Path(__file__).resolve().parents[1]/'verify-ext-secrets-1.ps1'
            run=subprocess.run(['pwsh','-NoProfile','-File',str(gate),'-Mode','Local','-ArtifactsDirectory',str(root/'artifacts'),'-EvidenceDirectory',str(root/'evidence')],env=os.environ|{'PATH':str(root)+os.pathsep+os.environ['PATH']},capture_output=True,text=True,timeout=20)
            self.assertNotEqual(0,run.returncode);self.assertIn('Local native console suite failed',run.stderr)
            self.assertEqual(1,len(list((root/'evidence').glob('native-*/tests.log'))));self.assertFalse((root/'evidence'/'local-build.log').exists())
