# Enroll a domain module

The module owner uses this kit to prepare enrollment in Platform. Static validation
and the human demonstration are separate gates. The accepted published tool is
`4.35.0`, including ordinary declaration validation and the read-only `--adoption`
report. Copy the exact pin from the direct Platform checkout as described below.
[Release evidence](../_bmad-output/implementation-artifacts/evidence/1-13/README.md)
records owner approval, gated publication and fresh acceptance. Static PASS rows
leave the owner's live SM-2 demonstration separately unverified.

## 1. Declare and initialize the minimum workspace

Use the module repository as the active Git root. Declare direct root submodules
in `.gitmodules`: `Hexalith.Platform`, `Hexalith.Builds`, `Hexalith.McpCli`,
`Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Memories`, and every additional
declared server dependency. Exclude the active module itself; the three minimum
server dependencies are distinct identities. Commit gitlinks at the selected
revisions. Initialize only these root paths at their recorded gitlinks:

```bash
git -c submodule.recurse=false submodule update --init --checkout -- \
  references/Hexalith.Platform references/Hexalith.Builds references/Hexalith.McpCli \
  references/Hexalith.EventStore references/Hexalith.Tenants references/Hexalith.Memories
git submodule status
```

Never add `--recursive` or `--remote`. Do not initialize a direct dependency's own
`references/*`. An already initialized nested submodule fails validation; its owner
must deinitialize that exact path before proceeding. Keep each checkout HEAD equal
to its module root's recorded gitlink. A sibling checkout's presence has no effect.

## 2. Customize the declaration and inventory

Copy the starter files, then replace the sample identities, app/resource/server
IDs, routes, topic names, configuration keys and readiness endpoint with the real
module contract. Keep all five groups: identity, runtime, surfaces, integration and
lifecycle. Dependencies name module identities, not repository paths. Add every
server dependency to both the declaration and the direct workspace references.
Readiness must describe the actual ready endpoint. The example declares a logical
pub/sub role, injected component key, explicit subscription route and dead-letter
topic; Platform assigns the physical scoped component names. Do not copy a literal
state-store, pub/sub or secret-store component name into code or configuration.

```bash
mkdir -p module
cp references/Hexalith.Platform/templates/domain-module-enrollment/hexalith.module-manifest.v2.json module/
cp references/Hexalith.Platform/templates/domain-module-enrollment/hosting-inventory.json module/
cp references/Hexalith.Platform/templates/domain-module-enrollment/sm-2-checklist.md module/
```

Inventory every existing module-owned `*.AppHost`, `*.Aspire` and
`*.ServiceDefaults` project by exact tracked `.csproj` path, kind, accountable owner
and consumers. Rename the sample inventory entries to existing projects; remove
entries for projects that do not exist. Use `"projects": []` if there are none.
Do not create sample hosting projects. Keep existing projects frozen, with explicit
replacement and retirement plans and pending gates. Do not add cross-module wiring.

Inventory completeness grants no retirement authority. Retirement requires producer
adoption, owner-reviewed **source/package/deployed parity**, and, when applicable,
EventStore **AD-22 exact-subject consumer-removal authority**. Record exact evidence
and authorized subject; never infer permission from a successful static report.
Only exact frozen hosting-project directories are excluded from bounded source
scans. Root build files and dependency checks always remain in scope.

## 3. Restore exactly the accepted tool

Copy the accepted tool manifest from the **direct Platform checkout**, preserving
the exact version and disabled roll-forward. Do not pick the newest NuGet version.

```bash
mkdir -p .config
cp references/Hexalith.Platform/.config/dotnet-tools.json .config/dotnet-tools.json
dotnet tool restore
dotnet tool run hexalith-module --version
dotnet tool run hexalith-module -- validate --manifest module/hexalith.module-manifest.v2.json
```

Run both adoption output modes with the restored accepted tool:

```bash
dotnet tool run hexalith-module -- validate --manifest module/hexalith.module-manifest.v2.json --adoption
dotnet tool run hexalith-module -- validate --manifest module/hexalith.module-manifest.v2.json --adoption --output json
```

Every static requirement reports PASS/FAIL, file and reason. Failures produce a
nonzero exit while independent requirements are still checked. Validation is
read-only: it never initializes references, resolves a source mapping, restores
packages or starts resources. It inspects tracked module-owned source, build,
configuration and helper files. Add enrollment files to normal owner review so
that source scans see the intended tracked workspace.

## 4. Use the tool's source mapping and Dapr conventions

Remove sibling/ancestor/nested checkout discovery and package-fallback helpers.
Remove file-based dependency/mode selectors and module assignments to tool-owned
root/mode properties. Project/package pairs may consume `UseHexalithProjectReferences`
and the tool-provided `Hexalith*Root` values; only the tool selects source/Debug or
package/Release. An `Exists` guard on an unrelated optional import is legitimate.
An `Exists` guard that chooses a dependency, root or mode is forbidden.

Read configured Dapr component names using declared configuration keys. Declare
subscriptions, delivery routes and dead-letter policy in the manifest. Remove
`[Topic]`, `WithTopic`, subscribe-handler mapping and `/dapr/subscribe` discovery.
Preserve module ACL files as authoritative inputs. Review local mTLS,
deny-by-default receiver ACLs and scoped components with the owner.

The audit is a **bounded static check**, limited to known patterns in supported
tracked files up to 1 MiB each. Human source review must inspect indirection,
unsupported languages, generated code and any constructs outside that bound.
Passing rows cannot prove runtime behavior or replace SM-2.

## 5. Run, debug and stop the active checkout

Install the exact SDK/Aspire/Dapr prerequisites documented in the direct Platform
[README](../README.md#requirements), including its isolated Dapr home. From the
module Git root:

```bash
dotnet tool run hexalith-module -- run --manifest module/hexalith.module-manifest.v2.json --mode source
dotnet tool run hexalith-module -- down --manifest module/hexalith.module-manifest.v2.json
dotnet tool run hexalith-module -- debug --manifest module/hexalith.module-manifest.v2.json
dotnet tool run hexalith-module -- down --manifest module/hexalith.module-manifest.v2.json
```

`run` and `debug` wait for declared readiness and required tasks; default deadline
is ten minutes unless a justified finite declared override applies. Failures must
identify the unready dependency and preserve diagnostics. `down` stops only the
tool-owned run resources. Record returned run identity and mapping. Package mode
uses released images and catalog packages; it also requires a clean, tagged direct
Platform checkout matching its committed identity.

## 6. Perform and record human SM-2 verification

Use the copied checklist. Bind module, direct Platform and tool identities to the
environment/run/mapping. Attach the debugger to the reported active-checkout
process; record a real breakpoint hit. Edit active-checkout source, run the intended
flow and record the runtime effect. Inspect direct-only initialization and absent
initialized nested references. Preserve command/output, diagnostics, verifier and
date, including failures. Keep all defaults pending until observed. Feed this
record into **Story 3.11**; static PASS rows leave SM-2 separately unverified.
