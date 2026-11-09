import argparse, concurrent.futures, hashlib, json, os, pathlib, signal, subprocess, time, xml.etree.ElementTree as ET
parser=argparse.ArgumentParser(); parser.add_argument('plan'); parser.add_argument('side', choices=['current','baseline']); args=parser.parse_args()
plan=json.loads(pathlib.Path(args.plan).read_text()); root=pathlib.Path(plan[args.side]['root']); evidence=pathlib.Path(plan['evidence'])/('results-'+args.side); evidence.mkdir(exist_ok=True)
project='tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj'
dll=root/'tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll'
commands=[]
def run(command,log,limit):
    start=time.monotonic(); commands.append({'cwd':str(root),'command':command,'timeoutSeconds':limit,'log':str(log)})
    with log.open('w') as output:
        p=subprocess.Popen(command,cwd=root,stdout=output,stderr=subprocess.STDOUT,start_new_session=True)
        try: code=p.wait(timeout=limit); timeout=False
        except subprocess.TimeoutExpired:
            os.killpg(p.pid,signal.SIGTERM)
            try: p.wait(timeout=10)
            except subprocess.TimeoutExpired: os.killpg(p.pid,signal.SIGKILL); p.wait()
            code=p.returncode; timeout=True
    result={'exitCode':code,'timedOut':timeout,'seconds':round(time.monotonic()-start,3),'log':str(log),'sha256':hashlib.sha256(log.read_bytes()).hexdigest()}
    (evidence/'commands.json').write_text(json.dumps(commands,indent=2)+'\n')
    return result
tool_restore=run(['dotnet','tool','restore'],evidence/'tool-restore.log',180)
(evidence/'tool-restore.json').write_text(json.dumps(tool_restore,indent=2)+'\n')
if tool_restore['exitCode']!=0: raise SystemExit('pinned tool restore failed')
build=run(['dotnet','build',project,'-c','Debug','-nodeReuse:false','--disable-build-servers','--nologo'],evidence/'build.log',1200)
(evidence/'build.json').write_text(json.dumps(build,indent=2)+'\n'); print(args.side,'build',json.dumps(build),flush=True)
if build['exitCode']!=0: raise SystemExit(build['exitCode'] or 1)
classes_run=run(['dotnet','exec',str(dll),'-list','classes/json','-noColor'],evidence/'classes.json',60)
if classes_run['exitCode']!=0: raise SystemExit('discovery failed')
raw=(evidence/'classes.json').read_text(); classes=json.loads(raw[raw.index('['):raw.rfind(']')+1]); (evidence/'class-inventory.json').write_text(json.dumps(classes,indent=2)+'\n')
print(args.side,'discovered',len(classes),'classes',flush=True)
results=[]
for index,cls in enumerate(classes):
    # All tests are selected. Per-class processes isolate mutable runner state and bound hangs.
    filename=f'{index:03d}-'+cls.rsplit('.',1)[-1]; xml=evidence/(filename+'.xml'); log=evidence/(filename+'.log')
    limit=3600 if cls.endswith('PlatformAppModelTests') else 900 if cls.endswith(('EventStoreExtensionCompositionTests','PlatformRoutingCatalogIntegrationTests')) else 360
    result=run(['dotnet','exec',str(dll),'-class',cls,'-parallelMode','none','-maxThreads','1','-noColor','-longRunning','60','-result-xml',str(xml)],log,limit)
    result.update({'class':cls,'resultXml':str(xml),'resultXmlPresent':xml.exists()})
    if xml.exists():
        assemblies=ET.parse(xml).getroot().findall('assembly'); result['counts']={key:sum(int(a.get(key,'0')) for a in assemblies) for key in ('total','passed','failed','skipped','not-run')}
    results.append(result); (evidence/'class-results.json').write_text(json.dumps(results,indent=2)+'\n')
    print(args.side,index+1,'/',len(classes),cls.rsplit('.',1)[-1],result.get('counts',result),flush=True)
combined=ET.Element('assemblies'); tests=[]
for result in results:
    if result['resultXmlPresent']:
        for assembly in ET.parse(result['resultXml']).getroot().findall('assembly'):
            combined.append(assembly)
            for test in assembly.findall('.//test'):
                failure=test.find('failure/message'); tests.append({'class':test.get('type'),'method':test.get('method'),'name':test.get('name'),'result':test.get('result'),'time':test.get('time'),'failure':failure.text if failure is not None else None})
ET.ElementTree(combined).write(evidence/'combined.xml',encoding='utf-8',xml_declaration=True)
summary={'side':args.side,'revision':plan[args.side]['revision'],'classesDiscovered':len(classes),'classesFinished':sum(r['resultXmlPresent'] for r in results),'classTimeouts':[r['class'] for r in results if r['timedOut']],'counts':{key:sum(r.get('counts',{}).get(key,0) for r in results) for key in ('total','passed','failed','skipped','not-run')},'fullInventoryExecuted':len(classes)==len(results) and all(r['resultXmlPresent'] and not r['timedOut'] for r in results)}
(evidence/'tests.json').write_text(json.dumps(tests,indent=2)+'\n'); (evidence/'summary.json').write_text(json.dumps(summary,indent=2)+'\n'); print('SUMMARY',json.dumps(summary),flush=True)
