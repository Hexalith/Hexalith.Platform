# tenant tenant-a scope tenant-a:custody:key-lifecycle-v1|custody-key-lifecycle-candidate-v1
path "heads-5704d77c337b42eb8dd537e5e4cea01a/data/tenants/80a707af7dc77ee1228f9127180f3964835e5beb4c4ab0d812f0fe7593579b3a" {
  capabilities = ["read", "update"]
}
path "heads-5704d77c337b42eb8dd537e5e4cea01a/data/archives/80a707af7dc77ee1228f9127180f3964835e5beb4c4ab0d812f0fe7593579b3a/*" {
  capabilities = ["read", "create"]
}
path "transit-5704d77c337b42eb8dd537e5e4cea01a/encrypt/epoch-a" {
  capabilities = ["update"]
}
path "transit-5704d77c337b42eb8dd537e5e4cea01a/decrypt/epoch-a" {
  capabilities = ["update"]
}
path "transit-5704d77c337b42eb8dd537e5e4cea01a/rewrap/epoch-a" {
  capabilities = ["update"]
}
path "transit-5704d77c337b42eb8dd537e5e4cea01a/keys/epoch-a" {
  capabilities = ["read"]
}
path "transit-5704d77c337b42eb8dd537e5e4cea01a/keys/epoch-a/rotate" {
  capabilities = ["update"]
}
path "transit-5704d77c337b42eb8dd537e5e4cea01a/keys/epoch-a/config" {
  capabilities = ["update"]
  allowed_parameters = {
    "min_decryption_version" = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64]
    "min_encryption_version" = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64]
  }
}
path "transit-5704d77c337b42eb8dd537e5e4cea01a/keys/epoch-a/trim" {
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
