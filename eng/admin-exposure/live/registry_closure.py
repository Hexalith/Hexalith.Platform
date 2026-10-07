"""Authenticated reachability closure of every tag in every repository (manifests, index children, configs, layers and
OCI referrers), and comparison of two closures across a GC run.

Usage: registry_closure.py READER_CREDENTIAL.json OUT.json [REPOSITORY ...]
       registry_closure.py --compare BEFORE.json AFTER.json

Untagged manifests that no tag or referrer reaches are not enumerable through the distribution API and are not covered.
"""
import concurrent.futures as cf, datetime, hashlib, json, os, re, sys, threading
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); from oci import Client, ACC


class ListError(Exception):
    pass


def paged(client, path, key):
    """Follow Link rel="next" pagination; any non-200 page is an error rather than an empty list."""
    items, url = [], path
    while url:
        s, h, b = client.call("GET", url)
        if s != 200:
            raise ListError(f"{path} -> {s}")
        items += json.loads(b).get(key) or []
        m = re.search(r'<([^>]+)>;\s*rel="next"', h.get("Link") or "")
        url = m.group(1) if m else None
    return items


def repo_closure(client, repo):
    objs, missing, lock = set(), [], threading.Lock()

    def walk(ref):
        s, _, b = client.call("GET", f"/v2/{repo}/manifests/{ref}", headers={"Accept": ACC})
        if s != 200:
            with lock: missing.append({"ref": ref, "status": s})
            return None
        d = "sha256:" + hashlib.sha256(b).hexdigest(); m = json.loads(b)
        with lock:
            if d in objs:
                return d
            objs.add(d)
        for c in m.get("manifests", []):
            walk(c["digest"])
        for x in ([m["config"]] if m.get("config") else []) + m.get("layers", []):
            st = client.call("HEAD", f"/v2/{repo}/blobs/{x['digest']}")[0]
            with lock:
                objs.add(x["digest"])
                if st != 200: missing.append({"ref": x["digest"], "status": st})
        rs, _, rb = client.call("GET", f"/v2/{repo}/referrers/{d}")
        if rs == 200:
            for ref in json.loads(rb).get("manifests") or []:
                walk(ref["digest"])
        else:
            with lock: missing.append({"ref": "referrers:" + d, "status": rs})
        return d

    tags = paged(client, f"/v2/{repo}/tags/list?n=1000", "tags")
    with cf.ThreadPoolExecutor(8) as ex:
        tagmap = dict(zip(tags, ex.map(walk, tags)))
    return {"tags": tagmap, "objects": sorted(objs), "missing": missing}


def compare(before, after):
    """Objects or tag bindings present before and gone or changed after; new content is reported separately."""
    out = {"objectsLost": {}, "tagsChanged": {}, "newObjects": {}, "newTags": {}}
    for repo in sorted(set(before["repositories"]) | set(after["repositories"])):
        b, a = before["repositories"].get(repo, {}), after["repositories"].get(repo, {})
        bo, ao, bt, at = set(b.get("objects", [])), set(a.get("objects", [])), b.get("tags", {}), a.get("tags", {})
        for key, val in (("objectsLost", sorted(bo - ao)), ("newObjects", sorted(ao - bo)),
                         ("tagsChanged", sorted(t for t in bt if at.get(t) != bt[t])), ("newTags", sorted(set(at) - set(bt)))):
            if val: out[key][repo] = val
    out["pass"] = not out["objectsLost"] and not out["tagsChanged"]
    return out


def main(argv):
    if argv[0] == "--compare":
        res = compare(json.load(open(argv[1])), json.load(open(argv[2])))
        print(json.dumps(res, indent=1))
        return res
    cred = json.load(open(argv[0]))
    client = Client("https://registry.hexalith.com", cred["username"], cred["password"])
    repos = argv[2:] or paged(client, "/v2/_catalog?n=1000", "repositories")
    closure = {repo: repo_closure(client, repo) for repo in repos}
    out = {"capturedAt": datetime.datetime.now(datetime.timezone.utc).isoformat(), "principal": cred["username"], "repositories": closure,
           "summary": {k: {"tags": len(v["tags"]), "objects": len(v["objects"]), "missing": len(v["missing"])} for k, v in closure.items()}}
    out["closureSha256"] = hashlib.sha256(json.dumps(closure, sort_keys=True).encode()).hexdigest()
    json.dump(out, open(argv[1], "w"), indent=1)
    print(json.dumps(out["summary"], indent=0)); print("closureSha256", out["closureSha256"])
    for k, v in closure.items():
        if v["missing"]: print(k, "missing:", v["missing"][:4])
    return out


if __name__ == "__main__":
    main(sys.argv[1:])
