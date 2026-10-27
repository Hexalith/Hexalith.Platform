"""Real loopback acquisition tests. No cohort authority or native effect executes."""
import socket
import threading
import time
import unittest
from unittest.mock import patch
from bounded_cohort_http import make_server


class BoundedHttpTests(unittest.TestCase):
    def test_timer_and_worker_startup_failure_release_one_slot_before_next_actual_request(self):
        class Application:
            calls=0
            def handle(self,*args): self.calls+=1;return {'fixture':True}
        for failure in ('timer','worker'):
            with self.subTest(failure=failure):
                app=Application()
                with make_server(app,'127.0.0.1',0,acquisition_timeout=1,max_connections=1) as server:
                    # Exercise actual accepted sockets through process_request; no worker can own a failed startup.
                    listener=socket.socket();listener.bind(('127.0.0.1',0));listener.listen(1)
                    with listener, socket.create_connection(listener.getsockname(),timeout=1) as client:
                        request,address=listener.accept()
                        target='threading.Timer.start' if failure=='timer' else 'threading.Thread.start'
                        original=threading.Thread.start
                        def fail_worker(thread):
                            if isinstance(thread,threading.Timer): return original(thread)
                            raise RuntimeError('isolated worker startup failure')
                        with patch(target,side_effect=RuntimeError('isolated timer startup failure')) if failure=='timer' else patch(target,fail_worker):
                            server.process_request(request,address)
                        for _,timer in list(server.deadlines.values()):
                            if timer.ident is not None:timer.join(1)
                        self.assertEqual({},server.deadlines)
                        self.assertTrue(server.slots.acquire(blocking=False));server.slots.release()
                        self.assertEqual(b'',client.recv(1))
                    serving=threading.Thread(target=server.serve_forever,kwargs={'poll_interval':.01},daemon=True);serving.start()
                    try:
                        with socket.create_connection(server.server_address,timeout=1) as good:
                            good.settimeout(1);good.sendall(b'POST /joint/v1/test HTTP/1.0\r\nContent-Length: 2\r\n\r\n{}');self.assertIn(b'200',good.recv(1024))
                        self.assertEqual(1,app.calls)
                    finally: server.shutdown();serving.join(1)
    def test_actual_timer_and_request_worker_retain_connection_before_bootstrap_acknowledgement(self):
        native_start=threading._start_joinable_thread;start=threading.Thread.start
        class LostAcknowledgement(RuntimeError):pass
        class InterruptedEvent:
            def __init__(self,event):self.event=event
            def is_set(self):return self.event.is_set()
            def set(self):return self.event.set()
            def wait(self,timeout=None):raise LostAcknowledgement('OS launch before Python acknowledgement')
        for selected in ('timer','worker'):
            with self.subTest(selected=selected):
                class Application:
                    calls=0
                    def handle(self,*args):self.calls+=1;return {'fixture':True}
                app=Application();gate=threading.Event();created=[]
                def matches(thread):return isinstance(thread,threading.Timer)==(selected=='timer')
                def delayed(function,**kwargs):
                    def bootstrap():
                        self.assertTrue(gate.wait(2));function()
                    return native_start(bootstrap if matches(function.__self__) else function,**kwargs)
                def interrupted(thread):
                    if matches(thread):thread._started=InterruptedEvent(thread._started);created.append(thread)
                    return start(thread)
                with make_server(app,'127.0.0.1',0,acquisition_timeout=1,max_connections=1) as server:
                    listener=socket.socket();listener.bind(('127.0.0.1',0));listener.listen(1)
                    with listener,socket.create_connection(listener.getsockname(),timeout=2) as client:
                        request,address=listener.accept();client.sendall(b'POST /joint/v1/test HTTP/1.0\r\nContent-Length: 2\r\n\r\n{}')
                        try:
                            with patch.object(threading,'_start_joinable_thread',delayed),patch.object(threading.Thread,'start',interrupted):server.process_request(request,address)
                            self.assertIsNone(created[0].ident);self.assertFalse(server.slots.acquire(blocking=False));self.assertEqual(0,app.calls)
                            gate.set();self.assertTrue(created[0]._started.event.wait(1));created[0].join(2);self.assertFalse(created[0].is_alive())
                            for _,timer in list(server.deadlines.values()):
                                if timer.ident is not None:timer.join(1)
                            self.assertEqual({},server.deadlines);self.assertTrue(server.slots.acquire(blocking=False));server.slots.release()
                            self.assertEqual(1 if selected=='worker' else 0,app.calls)
                        finally:gate.set()
                    serving=threading.Thread(target=server.serve_forever,kwargs={'poll_interval':.01},daemon=True);serving.start()
                    try:
                        with socket.create_connection(server.server_address,timeout=1) as good:
                            good.settimeout(1);good.sendall(b'POST /joint/v1/test HTTP/1.0\r\nContent-Length: 2\r\n\r\n{}');self.assertIn(b'200',good.recv(1024))
                    finally:server.shutdown();serving.join(1)

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

    def test_actual_cumulative_16k_header_boundary_includes_request_line_and_small_headers(self):
        class Application:
            calls=0
            def handle(self,*args): self.calls+=1;return {'fixture':True}
        app=Application()
        with make_server(app,'127.0.0.1',0,acquisition_timeout=1,max_connections=2) as server:
            thread=threading.Thread(target=server.serve_forever,kwargs={'poll_interval':.01},daemon=True);thread.start()
            try:
                for aggregate in (False,True):
                    for size,accepted in ((16*1024,True),(17*1024,False)):
                        start=b'POST /joint/v1/test HTTP/1.0\r\nContent-Length: 2\r\n'
                        padding=b''
                        if aggregate:
                            padding=b''.join(b'X-%02d: '%i+b'a'*510+b'\r\n' for i in range(30))
                        header=start+padding+b'Padding: '+b'p'*(size-len(start)-len(padding)-13)+b'\r\n\r\n'
                        self.assertEqual(size,len(header));before=app.calls
                        with socket.create_connection(server.server_address,timeout=1) as client:
                            client.settimeout(1);client.sendall(header+b'{}')
                            try: response=client.recv(1024)
                            except ConnectionResetError: response=b''
                        self.assertEqual(accepted,b'200 OK' in response)
                        self.assertEqual(before+int(accepted),app.calls)
            finally: server.shutdown();thread.join(1)
