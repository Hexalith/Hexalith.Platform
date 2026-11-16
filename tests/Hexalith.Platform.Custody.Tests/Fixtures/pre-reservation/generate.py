#!/usr/bin/env python3
"""Generate retained synthetic historical JSON with exact pre-reservation sources.
Run from any directory with run-with-temp python3 /path/to/generate.py.
"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

REVISION = "8a56bd57837784806aae989159fbcbc71fbd41d4"
OWNER = Path(__file__).resolve().parents[4]
OUTPUT = Path(__file__).resolve().parent
WORK = Path(os.environ["TMPDIR"]) / "historical-spool-probe"
WORK.mkdir()
SOURCE_NAMES = ("SecuritySpoolSnapshot", "SecurityObservationRecord", "SecurityObservationIntent",
                "SecurityEventRecordReceipt", "SecuritySpoolArchivePage")
sources = {}
for name in SOURCE_NAMES:
    path = f"src/Hexalith.Platform.Custody/{name}.cs"
    data = subprocess.check_output(["git", "show", f"{REVISION}:{path}"], cwd=OWNER)
    (WORK / f"{name}.cs").write_bytes(data)
    sources[path] = hashlib.sha256(data).hexdigest()
(WORK / "Probe.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net11.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>')
(WORK / "Program.cs").write_text('''using System.Text.Json;
using Hexalith.Platform.Custody;
using System.Security.Cryptography;
var output = args[0];
void Save(string name, object value) => File.WriteAllBytes(Path.Combine(output,name+".json"),JsonSerializer.SerializeToUtf8Bytes(value));
var empty = new SecuritySpoolSnapshot("installed-epoch-1",0,[]); Save("empty",empty);
var intent = new SecurityObservationIntent("historical-pending","tenant-a","invalid-tag",new string('A',64),"system-observation-key-v1");
var first = new SecurityObservationRecord(intent,new DateTimeOffset(2026,9,1,0,0,0,TimeSpan.Zero),"2026-09-01",1,null);
var head = empty with { Revision=1, Records=new[]{first} }; Save("pending-head",head);
var digest=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(intent)));
var receipt=new SecurityEventRecordReceipt(intent.ObservationId,intent.RoutingTenantId,first.UtcDay,digest,"tenant-a:agents:SecurityEventLog-2026-09-01",1,"historical-event");
var acknowledged=head with { Revision=2,Records=new[]{first with {Receipt=receipt}}}; Save("acknowledged-head",acknowledged);
Save("archive-page",new SecuritySpoolArchivePage(0,acknowledged,null));
''')
command = ["dotnet", "run", "--project", str(WORK / "Probe.csproj"), "--", str(WORK)]
result = subprocess.run(command, cwd=WORK, capture_output=True, text=True)
(OUTPUT / "generation.log").write_text(result.stdout + result.stderr)
result.check_returncode()
for name in (*SOURCE_NAMES, "Program"):
    shutil.copyfile(WORK / (name + ".cs"), OUTPUT / (name + ".cs.txt"))
outputs = {}
for name in ("empty", "pending-head", "acknowledged-head", "archive-page"):
    path = WORK / (name + ".json")
    shutil.copyfile(path, OUTPUT / path.name)
    outputs[path.name] = hashlib.sha256(path.read_bytes()).hexdigest()
(OUTPUT / "provenance.json").write_text(json.dumps({"revision": REVISION, "sources": sources,
    "command": command, "generatorSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
    "programSha256": hashlib.sha256((WORK / "Program.cs").read_bytes()).hexdigest(), "outputs": outputs}, indent=2) + "\n")
