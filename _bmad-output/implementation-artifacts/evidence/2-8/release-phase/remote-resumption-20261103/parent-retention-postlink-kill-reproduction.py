import datetime,hashlib,importlib.util,json,os,signal,tempfile
from pathlib import Path
helperPath=Path('/home/administrator/.cache/hexalith/story-2-8/builds-memories-retention-20261104/Hexalith.Builds/Github/run-module-integration/run_module_integration.py')
moduleSpec=importlib.util.spec_from_file_location('retention_io_review',helperPath);h=importlib.util.module_from_spec(moduleSpec);moduleSpec.loader.exec_module(h)
out=Path('_bmad-output/implementation-artifacts/evidence/2-8/release-phase/remote-resumption-20261103/parent-retention-postlink-kill-reproduction.json');assert not out.exists()
body=b'{"runId":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","outcome":"failed"}\n';name='lifecycle-'+hashlib.sha256(body).hexdigest()+'.json'
with tempfile.TemporaryDirectory(prefix='story-2-8-retention-kill-') as tmp:
 root=Path(tmp);evidence=root/'TestResults/module-integration';evidence.mkdir(parents=True,mode=0o700)
 prior=os.environ.get('GITHUB_WORKSPACE');os.environ['GITHUB_WORKSPACE']=str(root)
 try:
  pid=os.fork()
  if pid==0:
   realLink=h.os.link
   def killedAfterLink(*args,**kwargs):
    realLink(*args,**kwargs);os.kill(os.getpid(),signal.SIGKILL)
   h.os.link=killedAfterLink
   h.write_diagnostic(evidence,name,body,1024*1024)
   os._exit(99)
  waited,status=os.waitpid(pid,0);assert waited==pid and os.WIFSIGNALED(status) and os.WTERMSIG(status)==signal.SIGKILL
  target=evidence/name;assert target.read_bytes()==body
  links=target.stat().st_nlink;assert links==2
  leftoverTemps=[p.name for p in evidence.glob('.diagnostic-*.tmp')]
  try:h.write_diagnostic(evidence,name,body,1024*1024)
  except ValueError as exc:retry={'accepted':False,'exception':type(exc).__name__,'message':str(exc)}
  else:retry={'accepted':True}
  assert retry['accepted'] is False
  receipt={'at':datetime.datetime.now(datetime.timezone.utc).isoformat(),'helperSourceSha256':hashlib.sha256(helperPath.read_bytes()).hexdigest(),'scope':'Actual fork/file/SIGKILL collection IO regression only; no services, no GitHub job identity fabricated, no native acceptance.','interruption':'SIGKILL child immediately after actual atomic os.link publication, before temporary unlink/finally','childSignal':'SIGKILL','publishedBytesCompleteAndHashCorrect':True,'publishedLinkCount':links,'privateTemporaryLinksRemaining':leftoverTemps,'identicalRetry':retry,'cause':'Published final file and orphan temp are hard links; strict one-link check permanently rejects otherwise valid identical retry.','nativeAcceptance':False,'candidateAcceptance':False}
  out.write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(receipt,indent=2))
 finally:
  if prior is None:os.environ.pop('GITHUB_WORKSPACE',None)
  else:os.environ['GITHUB_WORKSPACE']=prior

