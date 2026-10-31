from pathlib import Path
import datetime,hashlib,json,os,subprocess,sys,time
root=Path('/tmp/ext-protection-loop10-native-fixtures-44662c3e1c3a')
label=sys.argv[1]; argv=sys.argv[2:]
log=root/(label+'.log')
if log.exists(): raise SystemExit('New log required')
env=dict(os.environ,HexalithCommonsRoot='/home/administrator/projects/hexalith/eventstore/references/Hexalith.Commons')
started=datetime.datetime.now(datetime.timezone.utc).isoformat();before=time.monotonic()
with log.open('xb') as output:
 p=subprocess.run(argv,stdout=output,stderr=subprocess.STDOUT,env=env)
record={'label':label,'argv':argv,'cwd':os.getcwd(),'environment':{'HexalithCommonsRoot':env['HexalithCommonsRoot']},'startedUtc':started,'elapsedSeconds':round(time.monotonic()-before,3),'exitCode':p.returncode,'log':str(log),'logSha256':hashlib.sha256(log.read_bytes()).hexdigest()}
(root/(label+'.command.json')).write_text(json.dumps(record,indent=2)+'\n')
print(json.dumps(record)); print(log.read_text(errors='replace')[-3500:]); sys.exit(p.returncode)
