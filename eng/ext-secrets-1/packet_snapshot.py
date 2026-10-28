"""Per-admission public bytes. Packet construction may only consume retained validated inputs."""
import os


class PublicPacketSnapshot:
    def __init__(self):
        self._bytes = {}

    def capture(self, path, maximum=1024*1024):
        import qualify as q
        name = os.fspath(path)
        if name not in self._bytes:
            self._bytes[name] = q.bounded_public_file(path, maximum)
        data = self._bytes[name]
        if not 0 < len(data) <= maximum:
            raise ValueError('BoundedAdmittedPublicSnapshot')
        return data

    def retained(self, path):
        return self._bytes[os.fspath(path)]

    def json(self, path, *, capture=False):
        import qualify as q
        return q.decode_json(self.capture(path) if capture else self.retained(path))


class AdmittedCoordination(dict):
    def __init__(self, value, snapshot):
        super().__init__(value)
        self.snapshot = snapshot


def snapshot_for(coordinated):
    if not isinstance(coordinated, AdmittedCoordination):
        raise ValueError('ExactAdmittedPacketSnapshotRequired')
    return coordinated.snapshot
