These three unapplied Deployments are the public qualification role frontends. Their
`Program.cs` loads `/references/runtime.json` and the explicit
`PLATFORM_QUALIFICATION_INSTALLATION=/references/qualification-installation.json`.
The latter is a `QualificationInstallation`: its `ScopeAuthority`,
`OriginalAuthority` and `Owner` bindings each contain their exact independently
returned `CredentialReference`. Public proof keys are embedded in its
`PublicAnchors`; this frontend consumes no private signing key or separate anchor
file. Independent receiving owners/signers are separate installations.

The reference ConfigMap must supply the consumed `runtime.json` and
`qualification-installation.json` keys. Each is mounted individually with the
same `subPath` and selected absolute path. The restricted Secret's actual three
key names remain the `OWNER_<ROLE>_{SCOPE_AUTHORITY,ORIGINAL_AUTHORITY,OWNER}_CREDENTIAL_KEY`
returns. Their individual `subPath` mount destinations remain the exact
`OWNER_<ROLE>_{SCOPE_AUTHORITY,ORIGINAL_AUTHORITY,OWNER}_CREDENTIAL_FILE` returns;
each bundle `CredentialReference` must equal `file:` plus that absolute path.
These placeholders supply no credential, appointment, public trust or installed
hash. The owner must also return compatible images, directory permissions,
existing ConfigMap/Secret objects, all scoped grants and the journal claims.

Container image parents `/references` and every selected credential parent must
be real directories. Individual file/subPath mounts expose regular files at the
selected locators. Mounting the entire ConfigMap/Secret at those parents would
expose AtomicWriter `key -> ..data/key` links instead, which the configuration
and private signing/anchor loaders deliberately reject. Apply the same individual
file rule to every selected configuration, `RoleSigningKeyFile`, `SigningKeyFile`
and standalone public-anchor input in a separately supplied receiving-owner or
Python authority installation, using its original configured absolute locator
and actual independently returned key name. No receiving-owner installation or
private-key Secret is fabricated by these frontend templates.

Treat the reference and subPath-mounted credential objects as immutable for the
pod's lifetime. Kubernetes does not refresh subPath binds on an AtomicWriter
rotation; see its [Secret mount documentation](https://kubernetes.io/docs/concepts/configuration/secret/)
and [ConfigMap mount documentation](https://kubernetes.io/docs/tasks/configure-pod-container/configure-pod-configmap/).
An independently authorized change needs replacement objects and a
pod restart, with revalidation of exact public pins/current authority; the source
here neither applies manifests nor changes installation references. Do not copy
credentials with an init container or invent a startup enrollment fallback.

`SecretsRuntimeConfiguration.Load`, `BoundedRegularFile`, the C# signer loader,
and Python `bounded_file_io`/selected public configuration and private signing
loaders require no symbolic component. The separate C#
`DaprSecretsInvoker.ReadMountedBearerAsync` and Python
`qualify.read_mounted_credential` transport readers support bounded projected
bearer links, newline handling and rotation. Whole projected bearer mounts are
therefore supported only for those transport readers, if the independently
supplied deployment selects them; that does not extend to configuration,
private signing keys or selected public-anchor files. The proposed individual
bearer mounts use the immutable/restart contract above.
