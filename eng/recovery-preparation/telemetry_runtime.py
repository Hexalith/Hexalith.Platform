"""Measured PG identity/schema/session checks used by an authenticated caller.

No credentials, backend or acceptance exist here. Native namespace/storage and
exact transport binding remain the caller's responsibility. SQL has no writes.
"""
import json
import re
import time


def require(ok,why):
    if not ok:raise ValueError(why)


def metadata_sql():
    return "SELECT json_build_object('systemIdentifier',(pg_control_system()).system_identifier::text,'timeline',(pg_control_checkpoint()).timeline_id,'serverVersionNum',current_setting('server_version_num')::int,'roles',(SELECT json_agg(json_build_object('name',rolname,'canLogin',rolcanlogin,'superuser',rolsuper) ORDER BY rolname) FROM pg_roles),'extensions',(SELECT json_agg(json_build_object('name',e.extname,'schema',n.nspname,'version',e.extversion) ORDER BY e.extname) FROM pg_extension e JOIN pg_namespace n ON n.oid=e.extnamespace),'clock',clock_timestamp()::text)::text;\n"


def empty_schema_sql():
    user_namespace="n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname !~ '^pg_toast' AND n.nspname !~ '^pg_temp'"
    relation="FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE "+user_namespace
    return ("SELECT json_build_object('tables',coalesce((SELECT json_agg(json_build_object('schema',n.nspname,'name',c.relname) ORDER BY n.nspname,c.relname) "+relation+" AND c.relkind IN ('r','p','f','v','m')), '[]'::json),"
            "'sequences',coalesce((SELECT json_agg(json_build_object('schema',n.nspname,'name',c.relname) ORDER BY n.nspname,c.relname) "+relation+" AND c.relkind='S'), '[]'::json),"
            "'functions',coalesce((SELECT json_agg(json_build_object('schema',n.nspname,'name',p.proname,'oid',p.oid) ORDER BY p.oid) FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE "+user_namespace+"), '[]'::json),"
            "'types',coalesce((SELECT json_agg(json_build_object('schema',n.nspname,'name',t.typname) ORDER BY n.nspname,t.typname) FROM pg_type t JOIN pg_namespace n ON n.oid=t.typnamespace WHERE "+user_namespace+"), '[]'::json),"
            "'nondefaultSchemas',coalesce((SELECT json_agg(n.nspname ORDER BY n.nspname) FROM pg_namespace n WHERE "+user_namespace+" AND n.nspname<>'public'), '[]'::json))::text;\n")


def observe_empty_schema(exec_sql):
    require(callable(exec_sql),'telemetry-runtime-executor-required')
    observed=json.loads(exec_sql(empty_schema_sql()))
    require(type(observed) is dict and set(observed)=={'tables','sequences','functions','types','nondefaultSchemas'}
            and all(type(x) is list for x in observed.values()),'telemetry-empty-schema-observation-refused')
    return observed


def session_guard(exec_sql,phase,names,owned,timeout=20):
    require(phase in ('before','alive','released') and set(names)=={'holder','canary','dump'}
            and len(set(names.values()))==3 and all(isinstance(v,str) and re.fullmatch(
                r's427-tel-[0-9]{8}t[0-9]{6}z-(?:holder|canary|dump)',v) for v in names.values())
            and type(owned) is dict and set(owned)<=set(names) and 0<timeout<=20,'telemetry-session-guard-input-refused')
    query=("SELECT coalesce(json_agg(json_build_object('pid',pid,'backendStart',backend_start::text,'user',usename,"
           "'database',datname,'applicationName',application_name,'state',state)), '[]'::json)::text FROM pg_stat_activity WHERE application_name IN ("
           +','.join("'"+x+"'" for x in names.values())+");\n")
    deadline=time.monotonic()+timeout
    while True:
        rows=json.loads(exec_sql(query))
        require(type(rows) is list,'telemetry-session-observation-refused')
        if phase in ('before','released'):
            if not rows:return True
            require(phase=='released' and time.monotonic()<deadline,'telemetry-owned-remote-sessions-not-absent')
            time.sleep(.1);continue
        require(len(rows)==len(owned) and len({r['pid'] for r in rows})==len(rows)
                and {r['applicationName'] for r in rows}=={v['applicationName'] for v in owned.values()},
                'telemetry-owned-session-set-drift')
        for row in rows:
            require({k:v for k,v in row.items() if k!='state'} in owned.values()
                    and row['state']=='idle in transaction','telemetry-owned-session-binding-or-state-drift')
        return True
