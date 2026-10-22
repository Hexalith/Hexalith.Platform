# Enroll a domain module

The module owner uses this kit to prepare enrollment in Platform. Static validation
and the human demonstration are separate gates. The accepted published tool is
`4.35.2`, including ordinary declaration validation and the read-only `--adoption`
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
revisions. Use repository folders named `references/Hexalith.<id>`, compared
case-insensitively with the declared module identity. Add each missing direct
reference from the module root (replace the URL and ID):

```bash
git submodule add '<url>' 'references/Hexalith.<Id>'
```

Initialize the following minimum root paths at their recorded gitlinks, excluding
self; append every additional declared dependency's direct path to the command:

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

Copy each starter file only when absent; merge an existing declaration or inventory
with owner review. Then replace the sample identities, app/resource/server
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
for file in hexalith.module-manifest.v2.json hosting-inventory.json sm-2-checklist.md; do
  if [ ! -e "module/$file" ]; then
    cp "references/Hexalith.Platform/templates/domain-module-enrollment/$file" "module/$file"
  fi
done
```

Inventory every existing module-owned `*.AppHost`, `*.Aspire` and
`*.ServiceDefaults` project by exact tracked `.csproj` path, kind, accountable owner
and consumers. Replace every pending owner and consumer with an identified value;
use `"consumers": ["none"]` only when no consumers exist. Unedited pending defaults
fail adoption validation; plans and gates may remain pending. Rename the sample
inventory entries to existing projects; remove entries for projects that do not
exist. Use `"projects": []` if there are none.
Do not create sample hosting projects. Keep existing projects frozen, with explicit
replacement and retirement plans and pending gates. Do not add cross-module wiring.

Inventory completeness grants no retirement authority. Retirement requires producer
adoption, owner-reviewed **source/package/deployed parity**, and, when applicable,
EventStore **AD-22 exact-subject consumer-removal authority**. Record exact evidence
and authorized subject; never infer permission from a successful static report.
Only exact frozen hosting-project directories are excluded from the Dapr audit.
A frozen directory's last segment must equal its project file name without the
extension (ignoring case), and it must contain no other project; use a dedicated
hosting-project directory.
Root build files and dependency checks always remain in scope.

## 3. Restore exactly the accepted tool

Merge only the `hexalith.builds.module.cli` tool entry from the **direct Platform
checkout**, preserving the exact version and disabled roll-forward as well as every
other local tool entry. Do not pick the newest NuGet version. For example, with
Python 3:

```bash
mkdir -p .config
python3 - <<'PYTHON'
import json
from pathlib import Path
accepted = json.loads(Path("references/Hexalith.Platform/.config/dotnet-tools.json").read_text())
path = Path(".config/dotnet-tools.json")
local = json.loads(path.read_text()) if path.exists() else {"version": 1, "isRoot": True, "tools": {}}
local.setdefault("tools", {})["hexalith.builds.module.cli"] = accepted["tools"]["hexalith.builds.module.cli"]
path.write_text(json.dumps(local, indent=2) + "\n")
PYTHON
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
package/Release. Every `Hexalith*Root` and `Hexalith*FromSource` property name is
reserved for the tool, including names that describe the module's own repository.
Rename module-local properties, for example `HexalithProjectsRepositoryRoot` to
`ProjectsRepositoryDirectory`. An `Exists` guard on an unrelated optional import is legitimate.
An `Exists` guard that chooses a dependency, root or mode is forbidden. The sole
bootstrap exception is `HexalithBuildsRoot` defaulting to
`$(MSBuildThisFileDirectory)references/Hexalith.Builds` only when that property is
empty; the tool supplies its mapping before this inert default is evaluated.

Read configured Dapr component names using declared configuration keys. Declare
subscriptions, delivery routes and dead-letter policy in the manifest. Remove
`[Topic]`, `WithTopic`, subscribe-handler mapping and `/dapr/subscribe` discovery.
Preserve module ACL files as authoritative inputs. Review local mTLS,
deny-by-default receiver ACLs and scoped components with the owner.

Leave the declared component key empty or use an injected placeholder; for example,
in `appsettings.json`:

```json
{ "Sample": { "Dapr": { "EventsComponent": "" } } }
```

Read the injected name from `IConfiguration`, for example
`configuration["Sample:Dapr:EventsComponent"]`, and pass that value to Dapr.
`${SAMPLE_EVENTS_COMPONENT}`, `$(Sample:Dapr:EventsComponent)` and
`{{ Sample.Dapr.EventsComponent }}` are also accepted placeholders for an external
configuration provider to replace. The static audit accepts these forms; Platform
injects the declared configuration key during the run.

The audit is a **bounded static check**, limited to known patterns in supported
tracked files up to 1 MiB each after file-type filtering. Every tracked `.cs`,
`.csproj`, `.props`, `.targets`, `.ps1`, `.sh` and `.py` file is in scope, including
tests and tracked evidence copies. The dependency audit inspects those extensions.
The Dapr audit inspects `.cs` files and configuration defaults in `appsettings*.json`,
`appsettings*.yaml` or `appsettings*.yml`. Only exact frozen hosting-project
directories are excluded from the Dapr audit. Other JSON/YAML files, including
evidence JSON and OpenAPI YAML, are not read.

An immediately asserted existence probe, such as `Directory.Exists(path).ShouldBeFalse()`
or `Assert.True(File.Exists(path))`, is verification and passes; the same probe in a
condition is rejected. Dapr API arguments and Dapr-specific names (`StateStoreName`,
`PubSubName`, `SecretStoreName`, `bindingName`) reject literal component names;
generic `StoreName`/`ComponentName` literals fail only in a Dapr context. `null` in
any case and `~` count as unset. The dependency audit also rejects sibling paths that
`Path.Combine`, `Path.Join`, `os.path.join` or `Join-Path` builds from `..` segments,
parent-directory imports that start outside the module, and reserved-property
overrides in scripts and `dotnet` arguments. Generic Dapr calls are matched up to one
nested level. Human source review must inspect unusual configuration filenames,
indirection, unsupported languages, generated code and any constructs outside that
bound. Passing rows cannot prove runtime behavior or replace SM-2.

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
