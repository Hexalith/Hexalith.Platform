"""Run documented standalone tuple/source/publication/catalog/restore controls with no service starts."""
from pathlib import Path
import subprocess,sys,json
root=Path.cwd();out=Path(sys.argv[1]);out.mkdir(parents=True,exist_ok=False)
names=['test_builds_tuple','test_builds_publication','test_current_tuple_source','test_file_host_catalog','test_tuple_collectors','test_tuple_runtime_context','test_tuple_sdk_suites','test_tuple_output_identity','test_current_catalog_view','test_host_builds_resolution','test_historical_security_packages','test_verify_platform_pins']
results=[]
for name in names:
 path=root/'eng'/(name+'.py')
 if not path.exists():raise ValueError('Missing documented control: '+str(path))
 command=[sys.executable,'-B',str(path)]
 with (out/(name+'.log')).open('w') as log:r=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT,timeout=1200)
 results.append({'control':name,'command':command,'exitCode':r.returncode})
 (out/'results.json').write_text(json.dumps(results,indent=2)+'\n')
 print(name,r.returncode,flush=True)
sys.exit(any(r['exitCode'] for r in results))
