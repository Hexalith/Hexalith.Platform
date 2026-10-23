# Offline telemetry PostgreSQL recovery proposal

`telemetry_postgres_adapter.py` constructs private PostgreSQL 18 capture,
isolated restore, source consumer pause/resume and owned target cleanup
proposals. It contains no backend, subprocess invocation, socket client,
credential loader, uploader, multipart abort, signer or live dispatcher.
No command, SQL transaction or manifest in its output has been executed.

The planner compiles the exact SHA-256-pinned bytes of the sibling
`recovery_adapters.py` for its bounded JSON, hashing and descriptor custody
primitives. It never imports historical recovery helpers or resolves the
sibling through the Python import path. Changing that sibling requires a
deliberate pin update and re-verification. Interpreter, standard library,
`/usr/bin/env`, dynamic dependencies and remote executable custody still need
independent qualification before any operational driver could use these
proposals.

## Local interface and custody

Run the offline CLI with an isolated interpreter:

```text
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/telemetry_postgres_adapter.py proposal --input <absolute-private-json> --sha256 <exact-input-sha256> --private-output <absolute-new-private-plan>
```

The input must be an owner-owned, single-link regular file with mode 0600,
at most 2 MiB, with the exact supplied SHA-256. Descriptor reads reject
symlink traversal, unsafe ancestors, changed file metadata, relative paths,
wrong hashes, duplicate JSON fields, floats and nonfinite numbers. A private
output is optional. Its parent must already be owner-owned mode 0700; output
creation is exclusive, no-follow, mode 0600 and fsynced. Existing output bytes
are preserved. The CLI prints only a fixed neutral result and input/plan/helper
hashes, with zero backend requests and closed operational gates. Errors use a
fixed sanitized failure, without input values, paths or backend diagnostics.
Only `proposal` is accepted. Live/activation/execution/stage arguments are
refused before reading the input. There is no credential/configuration fallback.

`render_proposal(value)`, the two separate runtime constructors and the tagged
canary parser/comparator are pure
after module loading: no local reads, tool execution or remote access. Output
does not alias the caller's input. Exact input hashing detects drift; hashes
and synthetic constructor success do not authenticate observations or policy.

## Closed input schema

The executable synthetic example is `fixture()` in
`test_telemetry_postgres_adapter.py`; `validate()` is the closed schema. Every
object rejects unknown fields. All identity-bearing values belong in the
parent's private bundle. Public documentation contains no sensitive source
names, endpoints, credential locations, raw metadata or custody details.

| Top-level field | Contract |
| --- | --- |
| `kind`, `schemaVersion` | `hexalith.telemetry-postgresql-proposal-input`, integer `1` |
| `observedAt` | UTC second timestamp; an observation never refreshes policy |
| `parentProposalSha256` | Exact parent proposal digest, retaining its destination, owner decisions and scope |
| `retainedDecisionsSha256` | Exact retained decisions digest |
| `evidence` | Exactly `observationSha256`, `metadataSha256`, `storageSha256`, `toolHashesSha256`, `identitySha256` |
| `source` | Source projection described below |
| `sourcePinSha256` | SHA-256 of `canonical(source)`; all source identity, metadata and tool fields are pinned together |
| `worker` | Exact source Deployment `resource` and observed `previousReplicas: 1` |
| `clock` | Exact, distinct source Deployment `resource` and observed `previousReplicas: 0`; preserve it |
| `target` | New isolated target proposal described below |
| `transfer` | Approved `store`, exact `key` under `memories/`, `writerPrincipal`, distinct `validatorPrincipal` or null, `recipientSha256` or null, exact integer `retentionDays: 30` and `maximumProofAgeSeconds: 86400` |

Resource objects are exactly `{apiVersion, kind, namespace, name, uid,
resourceVersion}`. Cluster-scoped resources use an empty namespace. Current
resources require exact non-null UID and resourceVersion; only the proposed
target Namespace has null UID/resourceVersion. Kubernetes names, endpoint
IP/port, image digests, tool paths, native database identity and PostgreSQL
identifiers are checked without shell interpolation. PostgreSQL identifiers
use the bounded ASCII letter/underscore/digit profile and are quoted in SQL.
Connection strings, URI/option injection, traversal and reference aliases are
refused. The planner does not support a general conninfo input.

`source` contains exactly:

- `cluster`, `namespace`, `statefulSet`, `pod`, `pvc`, `pv`, `storageClass`:
  exact observed resources and cluster UID/HTTPS endpoint/CA digest.
- `container`, `image`, `imageId`, `containerId`, `restartCount`: declared
  digest-pinned image and separately observed runtime image/container identity.
  Declared and runtime image digests must agree in this supported profile.
- `volumePath`: `{pvUid,nodeUid,path}` under the supported local-volume root,
  or null when unobserved. Null blocks future restore binding.
- `database`, `user`, `peerOsUser`, `socket`, `port`: explicit peer-only source
  connection and required OS execution identity; no password/auth material.
- `systemIdentifier`, `timeline`, `serverVersionNum`: measured native PG
  identity, positive uint64 decimal identifier, positive timeline and PG18
  numeric server version. These are current facts, not target placeholders.
- `schemas`: exactly the two observed schema names; `tables`: exactly the two
  distinct observed `{schema,name}` table identities.
- `extensions`: observed `{name,schema,version}`; `roles`: password-free
  observed `{name,canLogin,superuser}` inventory, including the source peer user.
  This projection does not prove membership, full grants or other role flags.
- `configuration`: bounded `{name,sha256,size}` records, never configuration
  content or source credential file locations.
- `tools`: exactly `psql`, `pg_dump`, `pg_restore`, each with measured absolute
  `path` and `sha256`. No unknown binary size is invented.

`target` contains exactly `cluster`, a proposed `namespace`, `statefulSetName`,
`pvcName`, `secretName`, `networkPolicyName`, `container`, `storageClass`,
`storage`, `runAsUser`, `runAsGroup`, `fsGroup`, `resources`, `socket`, `port`,
`bootstrapUser`. Storage class is `{resource,reclaimPolicy: "Delete"}`;
requests and limits each contain CPU and memory quantities. Proposed OS IDs
must be positive; storage and resources are explicit quantities. The target
Namespace, StatefulSet and claim use distinct source/target names. Its image
is always the exact source digest, never a separate override. The proposed
bootstrap user is the target-local OS postgres peer. All startup, nonroot
ownership, fsGroup, extension, storage and capacity assumptions remain
unqualified; numeric inputs do not establish deployability.
The target socket mount cannot equal, contain or sit within the data,
credential or temporary mounts.
CPU supports positive integer cores or integer millicores (`1` equals
`1000m`); memory supports positive integer `Mi`/`Gi` (`1Gi` equals `1024Mi`).
Each request must be no greater than its limit after dimensional conversion.
Memory units in CPU fields, CPU units in memory fields, fractional/scientific
notation and unsupported suffixes are refused. Manifest resources are rendered
as normalized integer millicores and Mi. This consistency check establishes
no actual capacity.

The parent constructs the actual input using current private evidence and
integrates both input and rendered plan hashes into its single private review
bundle. The planner neither loads those raw evidence files nor changes them.

## Proposed capture and canaries

Every PG argv starts with explicit `/usr/bin/env -i`, a small fixed environment,
`PGPASSFILE=/dev/null`, `PGSERVICEFILE=/dev/null`, explicit Unix socket, port,
user/database, exact absolute binary and `--no-password`. `psql` additionally
disables startup scripts and stops on SQL errors. No ambient HOME, proxy,
service, password, shell expansion or source connection-string override is
used. A future driver must independently establish the OS peer mapping and
exact current source binding before invocation.

The future capture algorithm must first pause the exact telemetry consumer
with UID/resourceVersion/previous-replica JSON Patch tests, preserve the clock
at zero, and satisfy the parent MCP/paired-store pause and clean-drain gates.
It must check the current source table census against the two bound tables,
then hold one REPEATABLE READ READ ONLY transaction exporting its snapshot.
The same psql backend and stdin stay open without EOF. The proposed idle
transaction timeout is five minutes; qualification must establish that the
holder survives until all importers and dump completion. No `--command`/EOF
holder that exits immediately is proposed. Release is a later explicit
ROLLBACK after all dependent operations finish.

The exported snapshot ID is null in the proposal. The separate pure
`snapshot_commands(value,binding)` constructor requires exact proposal/source
hashes, safe native snapshot syntax, live-holder PID/open-transaction binding,
matching native PG identity, and exact `preSnapshotTableCensus` and
`snapshotTableCensus` lists. Extra, missing or changed data-bearing user tables
must stop and rebind before dump. The schema's two table checks never imply
coverage for additional tables.

Only after those separate observations may the constructor return custom
archive `pg_dump --file=- --snapshot=<bound-id>` argv and same-snapshot canary
stdin. Dump has no table/schema filter, so it covers the whole database and
both observed schemas. The canary importer sets the snapshot before its first
query. No runtime constructor executes it or validates a holder's actual
liveness. No exported snapshot, dump, capture, canary or restore was run here.

`canaryContract` labels portable comparison categories: table census,
schemas/columns/defaults/owners/object ACLs, both table counts and full-content
SHA-256 hashes, constraints, indexes, sequence definitions/state/ownership,
extensions and default ACLs.
This is a limited supported database profile, not a proof of arbitrary schema
completeness. Both bound tables must occur as ordinary-table (`r`) relations
with nonempty column metadata. Every relation must belong to a bound namespace
and be an ordinary table, index (`i`) or sequence (`S`). Columns must refer to
an observed relation, have distinct names/positive native positions up to
1,600, nonempty type definitions and coherent identity/generated flags.
Index definitions, sequence definitions/census/ownership and constraint table
references are cross-checked against those relations and table columns.
Missing/orphan/duplicate references, unknown tables/namespaces, views,
materialized views, foreign tables, partitioned tables/indexes and other
relation kinds refuse comparison. Catalog position gaps can represent dropped
columns; the surviving column definitions must agree on both sides.

The schema payload also records portable `database` semantics: encoding,
locale provider, collate/ctype, provider locale, ICU rules, recorded collation
version, actual provider collation version and the default tablespace name.
These values must agree across source and target; a non-null recorded version
must match its actual provider version. No current locale facts are invented
in the proposal input. A future target bootstrap must establish compatible
locale semantics before restore/canary acceptance; the existing manifest
templates do not prove that compatibility.

Column records include column ACLs, resolved collation identity and semantics,
native storage/compression modes, statistics target, options and declared array
dimensions. A collation is null for a noncollatable column; otherwise it records
schema/name, provider, encoding applicability, collate/ctype, locale, ICU rules,
version/actual version and determinism, with no raw OID comparison. Database
default collations resolve their effective provider version through the database
semantics. ACL null and an empty ACL remain distinct. Native statistics targets
are null or integers from zero through 10,000; compression supports the default,
pglz and LZ4 modes and storage supports the native p/e/m/x modes. These are
observations to compare, not evidence that their actual SQL representation or
target support has been qualified.
Within this builtin scalar/array profile, text, name, character and character
varying (including their arrays) require a collation; noncollatable base types
such as bigint and their arrays require null. Referenced collations must belong
to the supported `pg_catalog` profile. User-schema collations refuse even when
the supplied unsupported census is empty.

Relations record persistence, resolved access method, relation/toast options
and a tablespace name without any physical source path. This limited profile
accepts only logged heap tables, logged btree indexes and logged native
sequences. Nondefault relation/toast/column options, custom tablespaces,
unlogged/temp persistence and other access methods refuse. The unsupported
census also observes identifiers for custom relation/database tablespaces,
security labels, database/role settings and extended statistics. Label values,
setting values and tablespace paths are never returned.
The same census refuses unusable indexes (not valid/ready/live), nondefault
replica identity or a replica-identity index, a CLUSTER-selected index, and
internal constraint triggers whose enable mode is not standard origin mode.
Operator classes with an explicit physical key storage type are also outside
this heap/btree profile. These are refusal entries in the existing payload;
no new runtime qualifications or execution authority follow.

Index records use native pg_index key/INCLUDE slots and column counts, with
ordinal, source table column position/name and explicit expression markers.
Constraint records use native pg_constraint kind/flags, constrained slots,
referenced FK table/slots and supporting index identity. Both include resolved
column dependency records from pg_depend and their counts. Every direct or
dependency reference must resolve to its exact observed bound table and column
position/name. Counts, ordinals, duplicate/orphan references, FK reference
lengths, supporting index ownership and key positions are checked; index slots
also agree with their own index relation columns. Expression slots retain zero
attribute markers and explicit expression text; constant expressions can have
an explicit empty dependency census. Expression and predicate text and full
portable SQL definitions remain compared. The consumer does not parse SQL or
claim universal validation of its meaning: reference coverage is exactly the
native slots/dependencies that the proposed catalog queries observe. Dropped
or unresolved dependency attributes remain visible as nulls and refuse.
Simple index/INCLUDE native columns must have the indexed column's base type;
native typmod spelling differences are permitted. Expression result types
remain separately observed and compared. CHECK native column slots must be
unique and equal their resolved column dependency positions, so independently
clearing either array together with its count refuses. A genuinely zero-column
CHECK may explicitly have both censuses empty. Supporting indexes must belong
to the exact constrained/referenced table, deferred constraints must be
deferrable, and native NOT NULL constraints require exactly one NOT NULL column.
These are catalog consistency checks, not a parser for arbitrary SQL meaning.

Identity columns require NOT NULL, no generated/default expression and exactly
one internally owned (`i`) sequence with a matching native value type. Every
internal ownership edge must point to such an identity; serial `a` ownership
remains separate. Generated columns require a nonempty generation expression
and no identity flag. Identity/generated flags are accepted only on ordinary
table columns, never index or sequence columns. Each sequence relation must have exactly the native
three columns in order: `last_value bigint`, `log_cnt bigint`, `is_called
boolean`, all NOT NULL and with no identity/generated/default expression.
The directly observed sequence state remains a separate stable before/after
comparison. Missing metadata or contradictory identity/generated/native
sequence shapes refuse even when both transcripts share the same omission.

An explicit `unsupportedObjects` tagged payload must contain exactly empty
arrays for `routines`, `triggers`, `rowSecurity`, `policies`, `types` and
`otherObjects`. Read-only catalog queries observe non-extension-owned
functions/procedures/aggregates, user triggers, RLS flags/policies, standalone
user types/domains/enums/ranges and other unhandled schema objects. Automatic
table row types/array types are distinguished from standalone types. The
other-object census covers user operators/operator classes/operator families,
collations/conversions/text-search objects, rules/inheritance, event triggers,
languages, publications/subscriptions, foreign servers/wrappers, large objects,
casts, access methods, transforms, extended statistics, security labels,
custom tablespaces and database/role settings. It records identities, never subscription
connection strings, routine bodies or row content. Custom casts/access methods
use the native PG18 normal-object OID boundary (16,384) as an additional
conservative detection rule; that assumption still needs runtime qualification.
The scope is non-system namespaces and listed database catalogs, not a blanket
claim of equality for every PostgreSQL object or cluster-global setting.

Any nonempty or missing unsupported census stops comparison even when source
and target would otherwise match. Only the pinned observed `plpgsql` and
optional observed `pgstattuple` extensions are supported, with exact identity,
schema and version comparison. Extension-owned routines/types have only that
bounded extension coverage; no routine-body equality is claimed. Actual
unsupported-object observations and supported-profile qualification remain
null in the proposal.
Catalog OIDs are used only to join/resolve portable names, never as direct
comparison fields. The source and restored canaries must agree by category.
Sequences are not MVCC-snapshot isolated: all writers must be quiesced and
sequence state independently stable, with source/dump/target agreement.
Snapshot lifetime and sequence stability remain null qualification gates.

Each bound table's full-content query converts each row to `row_to_json(row)::text`,
sorts that text under `C` collation without removing duplicates, frames each
row as its UTF8 byte length followed by `:` and its JSON text, and hashes the
concatenation with PostgreSQL 18's built-in `sha256(bytea)`. Empty tables hash
the empty byte string. No extension is installed. The session fixes UTF8,
UTC, ISO/YMD dates, ISO8601 intervals, hex bytea, `extra_float_digits=3`, `C`
numeric formatting and catalog-only search path. Row security is disabled so
insufficient visibility fails instead of quietly hashing a subset. The same
source exported snapshot is imported before any canary query. Stable type
representation under the exact PG18 image still needs runtime qualification.
The row serialization preserves native `json` field representation, including
duplicate keys, ordering and lexical whitespace, instead of normalizing it
through JSONB. Native `jsonb` values already have PostgreSQL's stored JSONB
semantics. This is a PG18 row-output representation contract, not a byte-for-byte
copy of heap tuples or a universal canonicalization across server/type versions.

The proposed supported profile is at most 100,000 rows per table, 65,536 UTF8
bytes per row and 16 MiB of serialized row content per table. A materialized
100,001-row prefix detects over-profile tables; a false profile or null digest
is refused by the consumer. Per-statement and lock timeouts are 15 and 5
seconds. No row JSON is returned in the transcript. Catalog arrays allow at
most 4,096 records, and the future driver must enforce the 2 MiB transcript
bound independently. These are proposed limits, not measured table sizes or
qualified performance. All actual row counts/content hashes remain unknown;
no SQL/content hash query has run in this implementation.
Nonempty content statistics require a maximum of at least two bytes (the
minimum JSON object representation), and a feasible total: at least the maximum
plus two bytes per remaining row and at most `rows * maximum`. Zero rows require
zero maximum/total and the exact empty SHA256 digest. Proposed bounds and
consistency checks establish no actual table size or SQL performance.

Sequence definitions from `pg_sequences` are accompanied by an explicit
catalog sequence census and direct `last_value` **and** `is_called` reads from
each sequence relation. Catalog-derived names use PostgreSQL `format` `%I`
identifier quoting and `%L` literal quoting. Read-only generated SELECTs are
delivered with psql `\gexec`; there is no CHECKPOINT, nextval, setval or sequence
mutation. Both before and after sample categories must exactly cover the
census, with no duplicates, and agree. A zero-sequence result requires an
explicit empty census and two explicit empty sample arrays; missing output
never means zero. The future source driver must retain writer quiescence,
start the dump after the first source sample and withhold the final sample
and ROLLBACK until all dump/canary work completes. The supplied streaming
stdin protocol records this coordination requirement; no scheduler enforces
it here. Source and restored sequence state must also agree.
Definitions support only native `smallint`, `integer` and `bigint` sequences.
Canonical signed decimal strings must fit their type/range; minimum must be
less than maximum, start and direct observed values must lie inside those
bounds. Increment is a nonzero signed int64 independent of the sequence value
type (a smallint sequence can have increment 100,000); cache is a positive
int64. Stable target samples with a different last value or
`is_called` still refuse source/target agreement.

Every result is one newline-delimited JSON object with exactly `category` and
`payload`; payloads use named fields and explicit JSON null/empty arrays.
There are no raw delimiter-separated tuples or bare category marker lines.
`parse_canaries(value, raw_bytes)` accepts exactly one record for every known
category, rejects extra/missing/duplicate/spoofed categories and duplicate
catalog identities, and validates table coverage, counts/content profile and
sequence census/stability. It is bounded to 2 MiB total, 512 KiB per record,
4,096 rows per catalog array, depth 16 and a finite JSON-node budget.
Escaped separators/newlines remain data; SQL NULL, a string `null` and an
empty ACL remain distinct. Catalog ordering is normalized for comparison.

`compare_canaries(value, source_bytes, target_bytes)` compares each portable
category independently, including both table digests, then checks the separate
session, role-isolation and fresh database-activation expectations. It refuses
equal row counts with changed digests, changed metadata or mismatched sequence
samples. Supplied records are data, not executable SQL. Neither function opens
a database, client, credential file or provider connection. A consistent
synthetic comparison has null operational qualification and recovery
acceptance, and `executionAuthorized: false`; it authenticates no transcript.

Session identity, role isolation and database activation are separately
recorded assertions with deliberate expected differences. The source peer
user differs from the target bootstrap user. Target application roles are
proposed NOLOGIN/NOSUPERUSER/NOCREATEDB/NOCREATEROLE/NOREPLICATION/NOBYPASSRLS/NOINHERIT,
with no copied passwords or activation authority; PG18 builtins and the fresh
isolated bootstrap remain target-local. Source roles are recorded without
passwords. The new database's bootstrap ownership/connection ACL is
target-local because a no-create restore does not copy source database
activation ACLs. Object/schema/default ACLs and ownership still require
separate agreement. Whole-transcript equality is explicitly false. Archive
inspection and deliberate isolation differences require qualification
by the future orchestration/proof consumers.

The runtime `roleIsolation` payload is exactly `{roles, memberships}`. Each
role records `name`, `canLogin`, `superuser`, `createDb`, `createRole`,
`replication`, `bypassRls` and `inherit`. Memberships record resolved role,
member and grantor names plus `admin`, `inherit` and `set` options from
`pg_auth_members`. No passwords are queried. The source name/login/superuser
projection must equal the pinned input inventory; its other authority flags
are observed at runtime and may record existing source authority. Those
observations are null now and do not become invented source input facts.

The supported membership profile permits only builtin-to-builtin edges
(both role and member begin `pg_`), granted by the isolated bootstrap name,
with exact source/target graph and option equality. Any custom application
role participating as member, granted role or grantor stops comparison,
including grants with apparently inactive options. This planner does not
silently discard arbitrary source role semantics. Target custom roles require
all seven authority/inheritance flags false and no memberships. Target builtin
roles use the stock profile (only `inherit` true), matching source builtin
flags; the fresh isolated bootstrap is the explicit exception with all seven
flags true. Exact names/inventory, missing fields, unexpected flags and grants
are checked independently from portable object ACL/schema agreement. These
are proposed runtime assertions; actual role authority, builtin membership
profile, archive inspection and isolation remain unqualified.

## Proposed isolated restore

Manifest templates propose a new Namespace, default-deny ingress/egress
NetworkPolicy, new dynamically provisioned Delete-class PVC, fresh run-owned
bootstrap Secret and one nonroot StatefulSet using the exact source image
digest. There is no Service, Ingress, host mount, source mount, host networking
or service-account token. PostgreSQL listens only on its isolated Unix socket.
API exec delivery, network-policy enforcement and isolation still require
qualification. A NetworkPolicy never qualifies as an API creation fence.

Bootstrap credential data is null and the templates are marked unapplyable.
Only separately supplied fresh run-owned material may complete them. They
contain no source credential bytes or credential-location input. Image
entrypoint behavior, PG18 data layout, arbitrary nonroot IDs, writable mounts,
fsGroup, storage class semantics, peer identity, empty initial database,
extensions and capacity are proposed assumptions, not observed facts.

`restore_command(value,binding)` separately requires:

1. The exact proposal hash; independently observed new Namespace,
   StatefulSet, pod, PVC and PV UIDs/resourceVersions and expected names, with
   no reused source identity; exact image, runtime image/container identity.
2. Exact PVC/PV claim binding, Delete storage class identity, independently
   observed target local-volume/node mapping, and a resolved source mapping.
   Equal, ancestor or descendant source/target paths are refused.
3. Explicit empty application tables, sequences, functions, types and
   nondefault schemas; separately assessed isolation digest. Actual GET,
   empty-schema and isolation observations are future driver requirements.
4. Exact immutable artifact store/key/version/hash/length, exact independent
   readback through the distinct validator, and an
   `authenticatedCaptureRecord` containing `recordSha256`, `artifactVersion`,
   `artifactSha256`, `plaintextSha256`, `plaintextSize`, `capturedAt`, `expiresAt`. The ciphertext
   version/hash must match that capture record. Decrypted stdin hash/length
   must exactly equal its separately authenticated plaintext values. Ciphertext
   and plaintext hashes/lengths can differ. No archive is opened or decrypted.
5. Capture is no earlier than the bound `observedAt`; expiry is exactly
   `capturedAt + 86400 seconds`. Independent readback must include exact
   `store`, `key`, `version`, `sha256`, `size`, distinct `validatorPrincipal`,
   `retentionMode: "COMPLIANCE"`, `encryption: "AES256"` and `retainUntil`
   identical to the artifact and at least `capturedAt + 30 days`. Missing,
   year-2000, short, mismatched or different-mode retention evidence refuses.
   These checks only establish consistency of supplied already-authenticated
   records. Actual capture/signature authentication, provider readback,
   current proof freshness and enforcement remain separate null gates.

The returned argv restores a custom archive from exact verified/decrypted
stdin with `--exit-on-error --single-transaction`. It has no `--clean`,
`--create`, source overwrite, archive filename, parallel jobs, or owner/ACL
suppression. Separate target role bootstrap SQL is proposed before restore.
Archive inspection, actual tool bytes, payload delivery, decryption, signature
authentication and canary acceptance are required external qualifications.
A constructor checks the supplied bindings for consistency; it cannot prove
the truth or authentication of a caller-supplied receipt. Both runtime
constructors return `executionAuthorized: false` and null qualification.

## Resume, cleanup and operational limits

Pause/resume shapes test only the exact observed source consumer UID, exact
resourceVersion and replicas. Pause tests recorded one then replaces zero;
resume tests zero and restores exactly one. Future resume resourceVersion is
null. Clock replicas remain zero with no clock PATCH. Parent MCP/paired-store
recovery, PG18 restore/canary agreement, source store health, fresh zero
pending/in-flight/missing/duplicate drain with unchanged stream identity,
source paused readback and post-PATCH reconciliation must all pass before any
resume acceptance. A proposal PATCH is never a resumed source.

Cleanup binds future run-owned resource identities/resourceVersions,
creation fence, complete inventory, volume/node paths and absence outcomes.
They are all null now. It proposes preconditioned deletion only of the new
owned resources, Namespace last, with independently qualified continuous API
creation prevention, fresh inventory, exact NotFound and same-node path
absence. Unknown fence, UID, inventory, path or outcome remains blocked. No
source storage writes, source object deletion or automatic cleanup is supplied.
`ownedTargets` contains only common-adapter allowed deletion roots: the new
Namespace, StatefulSet, PVC, eventual PV, bootstrap Secret and NetworkPolicy.
The generated Pod appears only in `cleanupCensusTemplate`, alongside those
roots; it cannot become a destructive deletion root. The template is not a
complete observed inventory. Actual `cleanupCensus`, `targetPaths`, owned
UIDs/resourceVersions, qualified creation fence and outcomes remain null.
Future orchestration must bind the complete inventory and target storage paths,
refresh the qualified fence after identity binding and before any deletion,
then retain the common contract's per-dispatch fence freshness. No Pod DELETE
or live cleanup dispatcher is produced here.

Current read-only private observations established peer role/database access,
two source tables, schemas, roles/extensions, configuration hashes, exact tool
hashes and native PG control identity. Those catalog/control observations read
no database rows or passwords and performed no dump/snapshot/restore. The
preceding local observer failure is retained as a zero-remote-request failure.
This planner's tests are synthetic and do not qualify those observations.

Actual policy/signature/readback, exported snapshot, holder lifetime, canary
agreement, dump/archive, decrypted stdin, created targets, capacities, fence,
restore, drain, source resume, target cleanup and recovery acceptance all
remain null. Full orchestration and proof-consumer updates remain explicitly
required before an operational bundle. Approved `memories/` transfer scope
and independent validator exact versions are recorded, but no upload/abort or
permission changes are implemented. Source capture/restore, retirement,
upgrade and deployment are not authorized. Story 4.27 remains in progress;
4.1 remains closed; Timesheets 3.6 remains open/FAIL. Historical, signed,
frozen and bound bytes remain unchanged.

## Verification

```text
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/test_telemetry_postgres_adapter.py
```

Synthetic tests cover every frozen matrix row: deterministic zero-I/O proposal;
unsafe source/tool/image/identifier drift; snapshot/census binding and missing
snapshot refusal; custom capture/safe restore flags; new isolated resources;
unknown/reused target UIDs, empty-schema/image/PVC/path gates; exact immutable
validator readback and captured/decrypted plaintext mismatch; portable canary
categories and deliberate role differences; exact prior-replica patches;
null runtime/fence/artifact/acceptance gates; neutral CLI/private create-only
custody and activation refusal before local access. No actual PG process,
cluster or provider call is made.

The 70 synthetic tests retain all 62 earlier test methods, including mutation tests
in both directions for independent returned contracts, exact retention/freshness
consistency, dimensional resource limits, same-count/different-content refusal,
direct sequence samples and explicit zero census, bounded semantic tagged
comparison, changed metadata, category spoofing and separators/newlines/NULL.
Additional regressions cover native JSON preservation, complete/coherent
catalog references, unsupported relation/object refusal, native sequence
numeric/range limits and stable target sequence differences, feasible content
statistics, full target role authority/membership checks and cleanup root/census
alignment. New cases cover database/column locale and ACL changes, persistence
and storage limits, native expression/INCLUDE index and FK references,
identity/generated/fixed sequence coherence, valid smallint/int64 increments,
new unsupported census categories and independent source-side builtin and
custom role mutations. The earlier incorrect sequence increment restriction
was updated to the specified independent int64 rule. This implementation
starts no database and executes no proposed SQL.
Bounded in-memory guard mutations were also checked: removing the source
builtin authority guard triggers five independent assertion failures; removing
the pinned source custom role projection guard triggers two. These checks use
only synthetic records and establish test sensitivity, not source qualification.
An earlier separate review used an ephemeral cached PG18.6 synthetic container
with no network or existing source connection. That review does not qualify
this final revised SQL, the current actual proposal or any recovery outcome.

Primary algorithm references:
[PostgreSQL 18 pg_dump](https://www.postgresql.org/docs/18/app-pgdump.html),
[PostgreSQL 18 pg_restore](https://www.postgresql.org/docs/18/app-pgrestore.html),
[PostgreSQL 18 snapshot synchronization](https://www.postgresql.org/docs/18/functions-admin.html#FUNCTIONS-SNAPSHOT-SYNCHRONIZATION),
[PostgreSQL 18 built-in binary hashes](https://www.postgresql.org/docs/18/functions-binarystring.html),
[PostgreSQL 18 psql generated-query execution](https://www.postgresql.org/docs/18/app-psql.html),
[PostgreSQL 18 row JSON construction](https://www.postgresql.org/docs/18/functions-json.html),
[PostgreSQL 18 role memberships](https://www.postgresql.org/docs/18/catalog-pg-auth-members.html),
[PostgreSQL 18 database locale metadata](https://www.postgresql.org/docs/18/catalog-pg-database.html),
[PostgreSQL 18 column metadata](https://www.postgresql.org/docs/18/catalog-pg-attribute.html),
[PostgreSQL 18 native index references](https://www.postgresql.org/docs/18/catalog-pg-index.html),
[PostgreSQL 18 native constraint references](https://www.postgresql.org/docs/18/catalog-pg-constraint.html).
