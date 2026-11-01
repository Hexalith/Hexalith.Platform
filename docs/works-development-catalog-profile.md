# Works Development owner profile

The canonical Works preview requires `Platform:Works:DevelopmentCatalogProfile`
before composition. Its ordinary external `dapr init` Redis at localhost:6379 can
retain business receipts. Startup cannot prove that backend empty from a missing
catalogue, lifecycle record or local directory, and refuses to generate replacement
digest keys. This refusal creates no runnable resource and writes no business state.

For retained state, restore the exact private profile, digest ring, complete SDK
catalogue custody and corresponding Redis/Scheduler fleet. Keep the complete owner
directory outside all source checkouts, with owner-only permissions. Do not invent a
new key with an old generation name, copy receipts into a new authority cell, or
treat missing custody as lost-key recovery.

For a new development environment, provision an independently owned, empty Redis
backend and its retained volume together with one new private digest ring. Record
ownership and retain the backend; generating a fresh profile against an existing
shared backend is unsafe. Place its actual Dapr `state.redis` component in the same
private owner directory. Select explicit state, pub/sub and configuration components
for that owned environment, preserve the actor state component's existing role, and restrict all input files to the owner
(`chmod 600` on Unix, with an owner-private parent directory). Never put key material
on a command line or into ordinary evidence/logs.

The private JSON profile uses these property names. The key value is the actual
retained base64 material, at least 32 decoded bytes; the example placeholder is not
a usable key:

```json
{
  "StateStoreComponentPath": "/private/works-owner/statestore.yaml",
  "PubSubComponentPath": "/private/works-owner/pubsub.yaml",
  "ConfigStoreComponentPath": "/private/works-owner/configstore.yaml",
  "Catalog": {
    "CustodyDirectory": "/private/works-owner/catalog-custody",
    "AuthorityComponentName": "routing-works-owner",
    "AuthorityStateKey": "works-development-authority",
    "ActiveDigestKeyGeneration": "owner-v1",
    "DigestKeys": { "owner-v1": "ACTUAL_RETAINED_BASE64_MATERIAL" },
    "SecretMapGeneration": "owner-v1",
    "SecretStore": "secretstore",
    "LogicalSecretName": "eventstore-idempotency-ring"
  },
  "Replicas": 1
}
```

Select the file with
`--Platform:Works:DevelopmentCatalogProfile=/private/works-owner/profile.json`.
Platform copies each exact component into its own private component-only resource
directory. Dapr never scans the directory containing digest keys or catalogue custody,
and an ambient default component cannot replace the selected business backend.
Use `Replicas: 2` for the existing Works HA lane: Platform creates `works` and
`works-ha` before catalogue capture, with the same logical Dapr AppId `works`,
independent physical identities, channel credentials and readiness attestations.
Late fixture changes cannot replace the selected idempotency configuration.

The SDK exclusively owns the profile while running. It retains canonical bytes,
rendered authority and ACL descriptors, resolved private backend dependencies,
activation/readiness/commit/resume receipts and the actual digest generation before
removing its disposable composition directory. A recomposition uses the same
authority cell and ring, builds a real successor from the retained canonical
predecessor, and requires every physical host ready with no uncertain old work.
Changed backend identity, missing custody or retained durable leases refuse rather
than silently opening another cell. An exact child restart uses verified resume.
Backend replacement and key rotation require separately qualified owner transitions.

Keep previous runtime files, credentials and fleet state for an eligible rollback;
the catalogue does not recreate old binaries or secret bytes. Run-scoped workload
issuer credentials retain their existing deletion policy. The canonical disposable
proof supplies its genuinely owned Redis and retained `story415` material before
composition, and verifies complete custody plus actual Redis authority at fleet
capture and restore. A partial model or focused test does not qualify live parity.
