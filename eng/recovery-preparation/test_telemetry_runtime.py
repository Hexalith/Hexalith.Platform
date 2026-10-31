import json,unittest
import telemetry_runtime as r
class RuntimeTests(unittest.TestCase):
 def names(self):return {k:'s427-tel-20261031t175250z-'+k for k in ('holder','canary','dump')}
 def test_actual_empty_and_nonempty_schema_observations_are_kept(self):
  empty={k:[] for k in ('tables','sequences','functions','types','nondefaultSchemas')}
  self.assertEqual(r.observe_empty_schema(lambda _:json.dumps(empty)),empty)
  empty['tables']=[{'schema':'public','name':'unexpected'}]
  self.assertEqual(r.observe_empty_schema(lambda _:json.dumps(empty))['tables'],empty['tables'])
  with self.assertRaises(ValueError):r.observe_empty_schema(lambda _:'{}')
 def test_both_schema_and_table_coverage_are_requested(self):
  sql=r.empty_schema_sql()
  for catalog in ['pg_class','pg_proc','pg_type','pg_namespace']:self.assertIn(catalog,sql)
  self.assertIn("('r','p','f','v','m')",sql)
 def test_owned_session_requires_pid_backend_start_app_user_db_and_idle_state(self):
  name=self.names();owned={'holder':{'pid':123,'backendStart':'actual-time','user':'source-user','database':'source-db','applicationName':name['holder']}}
  row=dict(owned['holder'],state='idle in transaction')
  self.assertTrue(r.session_guard(lambda _:json.dumps([row]),'alive',name,owned))
  for field,value in [('pid',456),('backendStart','replacement'),('user','other'),('applicationName',name['dump']),('state','active')]:
   changed=dict(row);changed[field]=value
   with self.subTest(field=field),self.assertRaises(ValueError):r.session_guard(lambda _:json.dumps([changed]),'alive',name,owned)
 def test_initial_nonempty_and_unreleased_sessions_refuse_and_injection_never_dispatches(self):
  called=[]
  with self.assertRaises(ValueError):r.session_guard(lambda _:('[{}]'),'before',self.names(),{})
  with self.assertRaises(ValueError):r.session_guard(lambda _:('[{}]'),'released',self.names(),{},timeout=.01)
  names=self.names();names['dump']="bad' OR true"
  with self.assertRaises(ValueError):r.session_guard(lambda x:called.append(x),'before',names,{})
  self.assertEqual(called,[])
if __name__=='__main__':unittest.main()
