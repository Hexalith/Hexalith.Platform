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
