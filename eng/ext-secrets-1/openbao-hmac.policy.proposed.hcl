# PROPOSED ACL template: replace placeholders with exact enrolled existing KV2 mount,
# isolated tenant and purpose. Never apply to all tenants or reuse the operator token.
# Opaque application key versions map independently to exact numeric KV2 versions.
path "<ENROLLED_HMAC_MOUNT>/data/<ISOLATED_TENANT>/<HMAC_PURPOSE>/*" {
  capabilities = ["read"]
}
path "<ENROLLED_HMAC_MOUNT>/metadata/<ISOLATED_TENANT>/<HMAC_PURPOSE>/*" {
  capabilities = ["deny"]
}
path "<INDEPENDENT_DECISION_SIGNER_MOUNT>/*" {
  capabilities = ["deny"]
}
path "sys/*" {
  capabilities = ["deny"]
}
# No create/update/delete/destroy/list, transit signing, auth/token creation, ordinary
# snapshot access or lifecycle authority is granted. Separate inventory authority must
# independently read current inventory/revocation; this HMAC token cannot certify itself.
