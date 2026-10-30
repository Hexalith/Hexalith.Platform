# Prepared policy for mounts erc-transit and erc-heads. Bootstrap requires matching exact mount names.
# Keep one credential per installed infrastructure tenant/scope; never expose it to Agents or public callers.
path "erc-heads/data/tenants/*" {
  capabilities = ["read", "update"]
}
path "erc-heads/metadata/*" {
  capabilities = ["deny"]
}
path "erc-transit/encrypt/*" {
  capabilities = ["update"]
}
path "erc-transit/decrypt/*" {
  capabilities = ["update"]
}
path "erc-transit/rewrap/*" {
  capabilities = ["update"]
}
path "erc-transit/keys/*" {
  capabilities = ["read"]
}
path "erc-transit/keys/+/rotate" {
  capabilities = ["update"]
}
path "erc-transit/keys/+/config" {
  capabilities = ["update"]
  allowed_parameters = {
    "min_decryption_version" = []
    "min_encryption_version" = []
  }
}
path "erc-transit/keys/+/trim" {
  capabilities = ["update"]
}
path "erc-transit/backup/*" {
  capabilities = ["deny"]
}
path "erc-transit/restore/*" {
  capabilities = ["deny"]
}
path "erc-transit/export/*" {
  capabilities = ["deny"]
}
path "sys/storage/raft/snapshot*" {
  capabilities = ["deny"]
}
path "sys/raw/*" {
  capabilities = ["deny"]
}
# Immutable originals live in the same snapshot-excluded ERC, with create-only native CAS zero.
# No archive update, delete, metadata or restore authority is granted to the runtime.
path "erc-heads/data/archives/*" {
  capabilities = ["read", "create"]
}
