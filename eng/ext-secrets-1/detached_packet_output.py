"""Durable local packet publication. Artifacts are immutable; a completion report follows verified output bytes."""
import os
from pathlib import Path
import tempfile


def _sync(directory):
    fd = os.open(directory, os.O_RDONLY | os.O_DIRECTORY | os.O_CLOEXEC)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


def _publish(path, data, *, report=False, complete=False):
    path = Path(path)
    fd, name = tempfile.mkstemp(prefix='.pending-', dir=path.parent)
    temporary = Path(name)
    published = False
    try:
        with os.fdopen(fd, 'wb') as output:
            output.write(data)
            output.flush()
            os.fsync(output.fileno())
        if report:
            os.replace(temporary, path)
            temporary = None
        else:
            os.link(temporary, path)
            temporary.unlink()
            temporary = None
        published = True
        _sync(path.parent)
    except BaseException:
        # An output error after final report visibility cannot leave a success marker.
        if complete and published:
            path.unlink(missing_ok=True)
        raise
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def artifact(path, data):
    _publish(path, data)


def report(path, value):
    import qualify as q
    _publish(path, q.canonical(value), report=True, complete=value.get('packetPrepared') is True)


def verify(artifacts):
    import qualify as q
    for path, data in artifacts.items():
        if q.bounded_public_file(path, 1024*1024) != data:
            raise ValueError('ExactDurableDetachedPacketBytes')
