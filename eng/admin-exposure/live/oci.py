"""Minimal OCI distribution client: exact-byte push from an OCI layout, uncached digest-verified pull, permission probes."""
import base64, hashlib, json, os, urllib.error, urllib.parse, urllib.request

ACC = ", ".join(["application/vnd.oci.image.index.v1+json", "application/vnd.docker.distribution.manifest.list.v2+json",
                 "application/vnd.oci.image.manifest.v1+json", "application/vnd.docker.distribution.manifest.v2+json"])

class Client:
    def __init__(self, base, user=None, password=None):
        self.base = base.rstrip("/")
        self.auth = ("Basic " + base64.b64encode(f"{user}:{password}".encode()).decode()) if user else None

    def call(self, method, path, data=None, headers=None, full=False):
        url = path if path.startswith("http") else self.base + path
        h = dict(headers or {})
        if self.auth:
            h["Authorization"] = self.auth
        req = urllib.request.Request(url, data=data, method=method, headers=h)
        try:
            with urllib.request.urlopen(req, timeout=120) as r:
                return r.status, r.headers, r.read()
        except urllib.error.HTTPError as e:
            return e.code, e.headers, e.read()

    def push_layout(self, layout, repo, tag):
        idx = json.load(open(os.path.join(layout, "index.json")))
        mdesc = idx["manifests"][0]
        blob = lambda d: open(os.path.join(layout, "blobs", *d.split(":")), "rb").read()
        mbytes = blob(mdesc["digest"])
        assert "sha256:" + hashlib.sha256(mbytes).hexdigest() == mdesc["digest"]
        m = json.loads(mbytes)
        results = []
        for d in [m["config"]] + m["layers"]:
            dig = d["digest"]
            s, _, _ = self.call("HEAD", f"/v2/{repo}/blobs/{dig}")
            if s == 200:
                results.append((dig, "exists")); continue
            data = blob(dig)
            s, h, b = self.call("POST", f"/v2/{repo}/blobs/uploads/")
            if s != 202:
                return {"ok": False, "step": "start-upload", "status": s}
            loc = h["Location"]
            loc = loc if loc.startswith("http") else self.base + loc
            sep = "&" if "?" in loc else "?"
            s, _, _ = self.call("PUT", f"{loc}{sep}digest={urllib.parse.quote(dig)}", data=data,
                                headers={"Content-Type": "application/octet-stream", "Content-Length": str(len(data))})
            if s != 201:
                return {"ok": False, "step": "put-blob", "status": s}
            results.append((dig, "uploaded"))
        s, h, _ = self.call("PUT", f"/v2/{repo}/manifests/{tag}", data=mbytes, headers={"Content-Type": mdesc["mediaType"]})
        return {"ok": s == 201 and h.get("Docker-Content-Digest") == mdesc["digest"], "status": s,
                "digest": h.get("Docker-Content-Digest"), "blobs": results}

    def pull(self, repo, ref):
        """Uncached pull: fetch manifest and every referenced blob, verifying sha256 of each."""
        s, h, b = self.call("GET", f"/v2/{repo}/manifests/{ref}", headers={"Accept": ACC})
        if s != 200:
            return {"ok": False, "manifestStatus": s}
        dig = "sha256:" + hashlib.sha256(b).hexdigest()
        if ref.startswith("sha256:") and dig != ref:
            return {"ok": False, "manifestDigestMismatch": dig}
        m = json.loads(b)
        out = {"ok": True, "manifestDigest": dig, "mediaType": m.get("mediaType"), "children": [], "blobs": 0, "bytes": len(b)}
        for c in m.get("manifests", []):
            r = self.pull(repo, c["digest"])
            out["children"].append({"digest": c["digest"], "ok": r["ok"]})
            out["ok"] &= r["ok"]; out["blobs"] += r.get("blobs", 0); out["bytes"] += r.get("bytes", 0)
        for d in ([m["config"]] if m.get("config") else []) + m.get("layers", []):
            s, _, data = self.call("GET", f"/v2/{repo}/blobs/{d['digest']}")
            ok = s == 200 and "sha256:" + hashlib.sha256(data).hexdigest() == d["digest"]
            out["ok"] &= ok; out["blobs"] += 1; out["bytes"] += len(data)
            if not ok:
                out.setdefault("failedBlobs", []).append({"digest": d["digest"], "status": s})
        return out

    def probe(self, repo, ref):
        """Permission probe statuses for catalog, tag list and manifest."""
        return {"catalog": self.call("GET", "/v2/_catalog")[0],
                "tags": self.call("GET", f"/v2/{repo}/tags/list")[0],
                "manifest": self.call("GET", f"/v2/{repo}/manifests/{ref}", headers={"Accept": ACC})[0]}
