# tenant tenant-b scope tenant-b:custody:key-lifecycle-v1|custody-key-lifecycle-candidate-v1
path "heads-215f7bd6b73d4c58b241803a108369a8/data/tenants/df6b6a5f230ea55af66fbc138653f50906674c62e103019d7af1d3bba6862ac2" {
  capabilities = ["read", "update"]
}
path "heads-215f7bd6b73d4c58b241803a108369a8/data/archives/df6b6a5f230ea55af66fbc138653f50906674c62e103019d7af1d3bba6862ac2/*" {
  capabilities = ["read", "create"]
}
path "transit-215f7bd6b73d4c58b241803a108369a8/encrypt/epoch-b" {
  capabilities = ["update"]
}
path "transit-215f7bd6b73d4c58b241803a108369a8/decrypt/epoch-b" {
  capabilities = ["update"]
}
path "transit-215f7bd6b73d4c58b241803a108369a8/rewrap/epoch-b" {
  capabilities = ["update"]
}
path "transit-215f7bd6b73d4c58b241803a108369a8/keys/epoch-b" {
  capabilities = ["read"]
}
path "transit-215f7bd6b73d4c58b241803a108369a8/keys/epoch-b/rotate" {
  capabilities = ["update"]
}
path "transit-215f7bd6b73d4c58b241803a108369a8/keys/epoch-b/config" {
  capabilities = ["update"]
  allowed_parameters = {
    "min_decryption_version" = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64]
    "min_encryption_version" = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64]
  }
}
path "transit-215f7bd6b73d4c58b241803a108369a8/keys/epoch-b/trim" {
  capabilities = ["update"]
}
# Deny fragment included in every rendered tenant/scope policy.
# This file grants no tenant head or transit path. Bootstrap renders one policy per
# installed tenant and scope; production hosts must use that rendered policy.
# Mount names remain erc-transit and erc-heads.
path "erc-heads/metadata/*" {
  capabilities = ["deny"]
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
