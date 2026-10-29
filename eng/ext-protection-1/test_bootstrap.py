"""Prepared bootstrap credential redirect regression; only synthetic loopback HTTP is used."""
import importlib.util
from pathlib import Path
import threading
import unittest
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, HTTPServer

spec = importlib.util.spec_from_file_location('erc_bootstrap', Path(__file__).resolve().parents[2] / 'deploy/erasure-root/bootstrap.py')
bootstrap = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bootstrap)


class BootstrapRedirectTests(unittest.TestCase):
    def test_credential_never_follows_redirect_to_another_origin(self):
        seen = []
        class Receiver(BaseHTTPRequestHandler):
            def do_GET(self):
                seen.append(self.headers.get('X-Vault-Token'))
                self.send_response(200)
                self.end_headers()
            def log_message(self, *args):
                pass
        target = HTTPServer(('127.0.0.1', 0), Receiver)
        class Redirect(BaseHTTPRequestHandler):
            def do_GET(self):
                self.send_response(302)
                self.send_header('Location', 'http://127.0.0.1:' + str(target.server_port) + '/stolen')
                self.end_headers()
            def log_message(self, *args):
                pass
        origin = HTTPServer(('127.0.0.1', 0), Redirect)
        threads = [threading.Thread(target=server.serve_forever, daemon=True) for server in (origin, target)]
        for thread in threads:
            thread.start()
        try:
            request = urllib.request.Request('http://127.0.0.1:' + str(origin.server_port) + '/', headers={'X-Vault-Token': 'synthetic-component-credential'})
            with self.assertRaises(urllib.error.HTTPError) as rejected:
                urllib.request.build_opener(bootstrap.NoRedirect()).open(request, timeout=2)
            self.assertEqual(rejected.exception.code, 302)
            self.assertEqual(seen, [])
        finally:
            for server in (origin, target):
                server.shutdown()
                server.server_close()
            for thread in threads:
                thread.join(timeout=2)


if __name__ == '__main__':
    unittest.main()
