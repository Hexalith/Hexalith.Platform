import os, pathlib, subprocess, sys, json, re
root=pathlib.Path(__file__).resolve().parents[6]
evidence=pathlib.Path(__file__).resolve().parent
mapping=root/'_bmad-output/implementation-artifacts/evidence/3-3/verification/catalog-live-aaefa6410931488b9a470b4133a84857/source-mapping'
env=os.environ.copy()
imports={'CustomBeforeDirectoryBuildProps':'SourceMapping.targets','CustomAfterMicrosoftCommonProps':'SourceMapping.props','CustomBeforeDirectoryBuildTargets':'SourceMapping.before-targets.props','CustomAfterDirectoryBuildTargets':'SourceMapping.late.props','AfterMicrosoftNETSdkTargets':'SourceMapping.final.targets'}
for key,name in imports.items():env[key]=str(mapping/name)
env['HEXALITH_SOURCE_MAPPING_HASH']=re.search(r'<HexalithSourceMappingHash>([^<]+)</HexalithSourceMappingHash>',(mapping/'SourceMapping.values.props').read_text()).group(1)
env['Configuration']='Debug'
name=sys.argv[1]; command=sys.argv[2:]
with (evidence/(name+'.txt')).open('w') as log:
 result=subprocess.run(command,env=env,cwd=root,stdout=log,stderr=subprocess.STDOUT)
receipt={'command':command,'cwd':str(root),'sourceMapping':str(mapping/'mapping.json'),'mappingHash':env['HEXALITH_SOURCE_MAPPING_HASH'],'imports':{key:env[key] for key in imports},'exitCode':result.returncode}
(evidence/(name+'.command.json')).write_text(json.dumps(receipt,indent=2)+'\n')
print(name+': '+str(result.returncode))
sys.exit(result.returncode)
