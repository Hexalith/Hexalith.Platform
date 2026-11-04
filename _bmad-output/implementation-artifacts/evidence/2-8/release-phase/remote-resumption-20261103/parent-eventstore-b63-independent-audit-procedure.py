import datetime,hashlib,json,stat,subprocess,zipfile,xml.etree.ElementTree as ET
from pathlib import Path,PurePosixPath
base=Path('/home/administrator/.cache/hexalith/story-2-8/eventstore-b63-hosted-20261104');base.mkdir(parents=True,exist_ok=True)
ev=Path('_bmad-output/implementation-artifacts/evidence/2-8/release-phase/remote-resumption-20261103')
items=[('contracts',13306215804,'61a1d027952e6340094a1a360c492fbb2c51761553a036e32320f3504ac38b20'),('tenants-source',13305153516,'20dba827b704e5f6141fccfe23a9cf5476bcbb7acea5df2037d961a66846080b')]
reports=[]
for name,aid,expected in items:
 p=base/(name+'.zip')
 if not p.exists():p.write_bytes(subprocess.check_output(['gh','api',f'repos/Hexalith/Hexalith.EventStore/actions/artifacts/{aid}/zip']))
 assert hashlib.sha256(p.read_bytes()).hexdigest()==expected
 trx=[]
 with zipfile.ZipFile(p) as z:
  for i in z.infolist():
   n=PurePosixPath(i.filename);assert not n.is_absolute() and '..' not in n.parts and '\\' not in i.filename and not stat.S_ISLNK(i.external_attr>>16)
   if i.filename.endswith('.trx'):
    root=ET.fromstring(z.read(i));ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};c=root.find('.//t:ResultSummary/t:Counters',ns);cases=root.findall('.//t:UnitTestResult',ns)
    assert c.attrib['failed']=='0';failed=[c.attrib['testName'] for c in cases if c.attrib['outcome'] not in ['Passed','NotExecuted']];assert not failed
    trx.append({'file':i.filename,'sha256':hashlib.sha256(z.read(i)).hexdigest(),'counters':c.attrib,'actualCases':len(cases),'outcomes':{status:sum(c.attrib['outcome']==status for c in cases) for status in sorted({c.attrib['outcome'] for c in cases})}})
 assert trx
 reports.append({'artifactId':aid,'archiveSha256':expected,'name':name,'trx':trx})
p=ev/'parent-eventstore-b63-independent-artifact-audit.json';assert not p.exists();p.write_text(json.dumps({'at':datetime.datetime.now(datetime.timezone.utc).isoformat(),'source':'b63dc2fe15e41621741d0e49e088f10046ec7bed','runId':41796717649,'attempt':1,'reports':reports,'scope':'Completed independent gates only; main isolated/native execution still pending.','nativeAcceptance':False,'candidateAcceptance':False},indent=2)+'\n');print(json.dumps(reports,indent=2))

