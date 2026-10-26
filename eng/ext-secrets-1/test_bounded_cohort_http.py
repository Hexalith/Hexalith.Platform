"""Real loopback acquisition tests. No cohort authority or native effect executes."""
import socket
import threading
import time
import unittest
from bounded_cohort_http import make_server


class BoundedHttpTests(unittest.TestCase):
    def test_total_header_body_deadline_saturation_and_malformed_framing_never_dispatch(self):
        class Application:
            calls=0
            def handle(self,*args):self.calls+=1;return {'fixture':True}
        app=Application()
        with make_server(app,'127.0.0.1',0,acquisition_timeout=.25,max_connections=1) as server:
            thread=threading.Thread(target=server.serve_forever,kwargs={'poll_interval':.01},daemon=True);thread.start()
            def connection():
                value=socket.create_connection(server.server_address,timeout=1);value.settimeout(1);return value
            try:
                for initial,trickle in ((b'POST /joint/v1/test HTTP/1.0\r\nX-Slow: ',b'a'),
                    (b'POST /joint/v1/test HTTP/1.0\r\nContent-Length: 100\r\n\r\n',b'a')):
                    with connection() as slow:
                        slow.sendall(initial);time.sleep(.05)
                        with connection() as surplus:
                            surplus.sendall(b'POST /joint/v1/test HTTP/1.0\r\nContent-Length: 2\r\n\r\n{}')
                            try:self.assertEqual(b'',surplus.recv(100))
                            except ConnectionResetError:pass
                        started=time.monotonic()
                        while time.monotonic()-started<.4:
                            try:slow.sendall(trickle)
                            except OSError:break
                            time.sleep(.03)
                        try:data=slow.recv(1024)
                        except (ConnectionResetError,BrokenPipeError):data=b''
                        self.assertNotIn(b'200 OK',data)
                        self.assertLess(time.monotonic()-started,.8)
                    time.sleep(.04)
                for framing in (b'Content-Length: 2\r\nContent-Length: 2', b'Transfer-Encoding: chunked',b'Transfer-Encoding:',b'Content-Length: 99999999'):
                    with connection() as bad:
                        bad.sendall(b'POST /joint/v1/test HTTP/1.0\r\n'+framing+b'\r\n\r\n{}');self.assertIn(b'503',bad.recv(1024))
                    time.sleep(.02)
                self.assertEqual(0,app.calls)
                with connection() as good:
                    good.sendall(b'POST /joint/v1/test HTTP/1.0\r\nContent-Length: 2\r\n\r\n{}');self.assertIn(b'200',good.recv(1024))
                self.assertEqual(1,app.calls)
            finally:server.shutdown();thread.join(1)
