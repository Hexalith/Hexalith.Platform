import pathlib,json,hashlib
root=pathlib.Path('/home/administrator/projects/hexalith/platform')
pairs=[]
for name in ['hexalith.test-environment.v1.json','hexalith.test-result.v1.json']:
 left=root/'docs/contracts/schemas'/name
 right=root/'references/Hexalith.Builds/schemas'/name
 assert left.read_bytes()==right.read_bytes(),name
 pairs.append({'schema':name,'sha256':hashlib.sha256(left.read_bytes()).hexdigest(),'byteIdentical':True})
print(json.dumps({'schemaParity':pairs},indent=2))
