-- Provision only under independent installation/enrollment authority. Runtime never runs this migration.
-- The independent installer must initialize the actual nonempty identity row before runtime opens this database.
-- Identity is externally enrolled with the held database inode and DELETE journal placement; runtime may not infer it.
CREATE TABLE IF NOT EXISTS database_storage_enrollment(slot INTEGER PRIMARY KEY CHECK(slot=1), identity TEXT NOT NULL CHECK(typeof(identity)='text' AND length(trim(identity))>0));
CREATE TABLE accepted_sets(binding_digest TEXT PRIMARY KEY, binding_json BLOB NOT NULL, accepted INTEGER NOT NULL CHECK(accepted IN (0,1)));
CREATE TABLE current_set(slot INTEGER PRIMARY KEY CHECK(slot=1), binding_digest TEXT NOT NULL REFERENCES accepted_sets(binding_digest));
CREATE TABLE attempts(binding_digest TEXT PRIMARY KEY REFERENCES accepted_sets(binding_digest), attempt_id TEXT NOT NULL UNIQUE);
CREATE TABLE originals(request_id TEXT PRIMARY KEY, attempt_id TEXT NOT NULL REFERENCES attempts(attempt_id), phase INTEGER NOT NULL CHECK(phase BETWEEN 0 AND 3), request_json BLOB NOT NULL, UNIQUE(attempt_id,phase));
CREATE TABLE results(request_id TEXT PRIMARY KEY REFERENCES originals(request_id), result_json BLOB NOT NULL);
CREATE TABLE phase_originals(request_id TEXT PRIMARY KEY, request_json BLOB NOT NULL, result_json BLOB NULL);
CREATE TABLE phase_absences(request_id TEXT NOT NULL, nonce TEXT NOT NULL, absence_json BLOB NOT NULL, PRIMARY KEY(request_id,nonce));
CREATE TABLE workload_grants(bearer_sha256 TEXT PRIMARY KEY, methods_json BLOB NOT NULL, valid_until INTEGER NOT NULL, revoked INTEGER NOT NULL CHECK(revoked IN (0,1)));
CREATE TABLE service_enrollment(role TEXT PRIMARY KEY, public_anchor_sha256 TEXT NOT NULL, profile_sha256 TEXT NOT NULL);
