# Dedicated CI registry reader: prepared change

This is a preparation and execution procedure for the single `ci-reader` account
at `registry.hexalith.com`. The account is intended only for pull access in the
EventStore and Parties v2 CI jobs. This directory contains no credentials. The
prepared public artifacts are in
`_bmad-output/implementation-artifacts/evidence/2-8/release-phase/registry-reader/`.
No account, Kubernetes resource, GitHub secret, or registry policy was changed
when this procedure was prepared.

## Exact reviewed baseline

`prepare.py --offline-check` verifies the retained public configuration and
patches without contacting the cluster. `prepare.py --check` makes pinned,
read-only Kubernetes GET requests and refuses any difference from those artifacts.
It must pass immediately before application. It checks cluster UID
`b5860674-1d1e-4a3b-b413-e1bb3b9a4557`, context `jpiquot@local`, namespace
`registry-distribution`, ConfigMap `distribution-registry-config` UID
`d00eeb4b-f507-440f-8328-fb838a7d5da3`, resourceVersion `27899151`, and
the exact `config.json` SHA-256
`90805228cf4ac16752d3bb1ca15b47b92f8c24f1a572b5275f1e54c9eecb0317`.
It reads only metadata and key names from Secret
`distribution-registry-htpasswd`; it does not read any Secret value. The saved
plan also binds the deployment, image, pod, and template identities.

The sole configuration change is one repository policy
`{"users":["ci-reader"],"actions":["read"]}` under `repositories["**"]`.
Existing writer, `cluster-reader`, anonymous, admin, authentication, storage,
and retention settings are byte or value preserving. The original applied
kustomize source is outside the local repositories and was not located. Carry
`configuration.ci-reader.json` and this one-policy delta into that owning source
before its next reapplication. Do not claim durable source reconciliation until
that source is found, reviewed, and updated. A live patch alone will be reverted
by reapplication of the old source.

## Execution order for a later authorized operator

1. Verify that no registry publisher is active, including current release jobs.
   Establish a coordinated window for the one-replica `Recreate` deployment;
   its targeted restart has an availability interruption. Do not scale, change
   strategy, or restart unrelated workloads. Record the actual publisher check.
2. Run `python3 eng/ci-registry-reader/prepare.py --offline-check`, then
   `python3 eng/ci-registry-reader/prepare.py --check`. Any refusal or changed
   UID, resourceVersion, configuration value, Secret metadata/key, deployment,
   pod, or source identity stops this attempt. Prepare a newly reviewed plan;
   never silently rebase these patches. Before each later mutation, recheck
   untouched resources against the baseline and changed resources against the
   exact preceding patch result. Use explicit `--kubeconfig /home/administrator/.kube/config`
   and `--context jpiquot@local` on every `kubectl` command.
3. Create a **new** owner-only custody directory outside every Git worktree
   (mode `0700`, files `0600`, `umask 077`). Keep plaintext password, bcrypt
   row, the existing Secret snapshot, its exact old htpasswd bytes, the private
   Secret patch, authenticated probe files, and rollback material there. Disable
   shell tracing and command echo. Never print, log, commit, or pass password,
   bcrypt rows, authentication headers, or existing Secret values on argv.
   Generate this account's password cryptographically into a file, for example
   `openssl rand -base64 48 > "$custody/password"`. Create a bcrypt row through
   stdin: `htpasswd -nB -i ci-reader < "$custody/password" > "$custody/new-row"`.
   Refuse if `htpasswd` is unavailable; do not use a plaintext hash.
4. After the metadata-only check, capture the exact Secret JSON directly to
   `$custody/secret.before.json`, without terminal output. Decode only its
   `data.htpasswd` into `$custody/htpasswd.before`; verify its UID,
   resourceVersion, sole key, absence of `ci-reader`, and final newline. Build
   `$custody/htpasswd.after` as the **exact** original bytes followed by the
   single new bcrypt row. Assert the original prefix is byte-identical. Build
   `$custody/secret.patch.json` with JSON Patch `test` operations for UID,
   resourceVersion, and the exact old base64 `data.htpasswd`, followed by one
   `replace` of `/data/htpasswd` with the new base64 value. Keep this patch and
   the full original Secret only in custody. A mismatch stops the attempt;
   never overwrite or regenerate any existing row.
5. Recheck the live publisher state and run `prepare.py --check` once more.
   Apply the private Secret patch with `kubectl patch secret
   distribution-registry-htpasswd -n registry-distribution --type=json
   --patch-file "$custody/secret.patch.json" -o name`, suppressing routine
   output. Apply the public `configmap.patch.json` with the same guarded JSON
   patch mechanism to `configmap distribution-registry-config`. The ConfigMap
   patch tests UID, resourceVersion, and the entire original `config.json`
   value before replacing that one value. Capture the resulting identities and
   configuration hash, with no Secret values in the public record. A failed
   patch is a failed attempt; preserve its diagnostics and use guarded recovery.
6. Recheck that no publisher has started. Apply `reload.patch.json` to
   `deployment distribution-registry` using `kubectl patch ... --type=json
   --patch-file`. The patch tests deployment UID/resourceVersion, one replica,
   `Recreate`, exact image and volume bindings, then adds only the reviewed
   configuration SHA annotation to the pod template. The `config.json` volume
   uses `subPath`, so changing the ConfigMap alone cannot reload that file in
   the existing process. Wait for the one replacement pod to become Ready and
   verify its image, owner chain, loaded configuration and account behavior.
   Treat timeout or unexpected replacement as failure, with no success claim.
7. Measure the account from an external client against the known existing
   `eventstore` image index
   `sha256:cbaed729ae671eff9ecf69b40c8f646e0a27aeae62fe0481ce6be98cfa840c86`.
   Fetch its manifest/index with authenticated GET, hash the **body** and match
   that digest. Fetch the recorded linux/amd64 manifest
   `sha256:ed507d6478ee211a9da77f1d9affe3364859906584391417b782a6fa28fe64ca`,
   hash it, then fetch its actual config and at least one layer by their
   discovered digests and hash those bodies too. Use a private owner-only curl
   netrc file or equivalent stdin credential channel; no password in argv or
   shell history. Do not follow redirects with credentials to another host.
   Repeat known-existing manifest and blob GETs anonymously and with a fresh
   wrong credential; both must be refused. Probe create, update, and delete
   using only a cryptographically random **disposable** repository/tag/digest
   namespace that cannot target any retained image. A write probe must return
   an authorization refusal (401/403); a malformed-request or missing-object
   response does not prove denial. Never issue a write probe against retained
   content. Record only sanitized method/path category, status, source identity,
   and verified content hashes; preserve any failure as a failure.
8. Only after every read and refusal measurement passes, deliver the same
   account values to both `Hexalith/Hexalith.EventStore` and
   `Hexalith/Hexalith.Parties`: pipe `ci-reader` into `gh secret set
   HEXALITH_CI_REGISTRY_USERNAME --repo <owner/repo>` and redirect
   `$custody/password` into `gh secret set HEXALITH_CI_REGISTRY_PASSWORD
   --repo <owner/repo>`. `gh secret set` must receive the secret on stdin,
   never through `--body`, command substitution, argv, or logs. Verify only
   repository secret names and the explicit v2 workflow secret mappings. Do
   not place these credentials in other repositories or provider environments.

If any application or measurement fails, stop. Preserve the private before
snapshot and patches for an owner-reviewed rollback. A rollback must first
verify the post-change UID, current resourceVersion, and exact value added by
this attempt, then restore only the original value with a new guarded patch.
The same guard applies to a deployment annotation rollback. Never use an
unconditional `replace`, a stale resourceVersion, or the old configuration
source to overwrite concurrent work. Keep the first failed result and record
leftovers explicitly. This procedure does not grant candidate acceptance or
authorize publication.
