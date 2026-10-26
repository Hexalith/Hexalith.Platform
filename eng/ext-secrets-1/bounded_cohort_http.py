"""Bounded unauthenticated acquisition for the independent cohort receiver."""
import socket
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

MAX_HEADERS = 16 * 1024
MAX_BODY = 1024 * 1024


class HeaderReader:
    def __init__(self, stream):
        self.stream, self.header_bytes, self.headers = stream, 0, True

    def readline(self, limit=-1):
        if not self.headers:
            return self.stream.readline(limit)
        remaining = MAX_HEADERS - self.header_bytes
        if remaining <= 0:
            raise ValueError('header bound')
        data = self.stream.readline(min(remaining + 1, limit) if limit >= 0 else remaining + 1)
        self.header_bytes += len(data)
        if self.header_bytes > MAX_HEADERS:
            raise ValueError('header bound')
        return data

    def __getattr__(self, name):
        return getattr(self.stream, name)


def make_server(application, host, port, *, acquisition_timeout=30, max_connections=16):
    if not 0 < acquisition_timeout <= 30 or not 1 <= max_connections <= 128:
        raise ValueError('bounded receiving configuration')

    class Server(ThreadingHTTPServer):
        daemon_threads = True
        request_queue_size = max_connections

        def __init__(self):
            self.slots = threading.BoundedSemaphore(max_connections)
            self.deadlines = {}
            super().__init__((host, port), Handler)

        def process_request(self, request, address):
            if not self.slots.acquire(blocking=False):
                self.shutdown_request(request)
                return
            def expire():
                try: request.shutdown(socket.SHUT_RDWR)
                except OSError: pass
            request.settimeout(acquisition_timeout)
            timer = threading.Timer(acquisition_timeout, expire)
            timer.daemon = True
            self.deadlines[request] = (time.monotonic() + acquisition_timeout, timer)
            timer.start()
            try: super().process_request(request, address)
            except Exception:
                timer.cancel(); self.deadlines.pop(request, None); self.slots.release()
                self.shutdown_request(request)

        def process_request_thread(self, request, address):
            try: super().process_request_thread(request, address)
            finally:
                value = self.deadlines.pop(request, None)
                if value: value[1].cancel()
                self.slots.release()

        def handle_error(self, request, address):
            pass  # Never log unauthenticated bodies, credentials or native results.

    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *args): pass

        def setup(self):
            super().setup()
            self.rfile = HeaderReader(self.rfile)

        def do_POST(self):
            from joint_live_phase_runner import canonical
            self.close_connection = True
            try:
                if not self.path.startswith('/joint/v1/') or self.headers.get_all('Transfer-Encoding'):
                    raise ValueError('request')
                values = self.headers.get_all('Content-Length', [])
                if len(values) != 1 or not values[0].isascii() or not values[0].isdigit():
                    raise ValueError('framing')
                length = int(values[0])
                if not 0 < length <= MAX_BODY: raise ValueError('bound')
                self.rfile.headers = False
                body = self.rfile.read(length)
                deadline, timer = self.server.deadlines[self.connection]
                if len(body) != length or time.monotonic() >= deadline: raise ValueError('deadline')
                timer.cancel()
                # Native work survives client connection closure under its own admitted-original semantics.
                result = application.handle(self.path[len('/joint/v1/'):], self.headers, body)
                data = canonical(result)
                self.send_response(200); self.send_header('Content-Type', 'application/json')
                self.send_header('Content-Length', str(len(data))); self.end_headers(); self.wfile.write(data)
            except Exception:
                try:
                    self.send_response(503); self.send_header('Content-Length', '0'); self.end_headers()
                except OSError: pass

    return Server()
