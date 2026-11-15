#!/usr/bin/env python3
"""Collect revisions, source hashes and restored package observations for the final verification record."""
import hashlib
import json
import subprocess
import sys
from pathlib import Path

P = Path("/home/administrator/projects/hexalith/platform")
F = P / "references/Hexalith.Folders"
B = P / "references/Hexalith.Builds"


def git(repo, *args):
    return subprocess.run(["git", "-C", str(repo), *args], capture_output=True, text=True).stdout.strip()


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest() if Path(path).is_file() else None


repositories = {}
for name, repo in [("Platform", P), ("Builds", B), ("Folders", F), ("EventStore", P / "references/Hexalith.EventStore"),
                   ("Tenants", P / "references/Hexalith.Tenants"), ("FrontComposer", P / "references/Hexalith.FrontComposer"),
                   ("Memories", P / "references/Hexalith.Memories"), ("Commons", P / "references/Hexalith.Commons"),
                   ("Parties", P / "references/Hexalith.Parties"), ("McpCli", P / "references/Hexalith.McpCli")]:
    entry = dict(head=git(repo, "rev-parse", "HEAD"), tags=[t for t in git(repo, "tag", "--points-at", "HEAD").splitlines() if t],
                 trackedChanges=[line for line in git(repo, "status", "--porcelain").splitlines() if not line.startswith("??")])
    if repo != P:
        entry["platformGitlink"] = git(P, "rev-parse", "HEAD:" + str(repo.relative_to(P)))
    repositories[name] = entry

platform_files = [
    "src/Hexalith.Platform.Composition/PlatformWorkloadEnrollment.cs",
    "src/Hexalith.Platform.Composition/PlatformWorkloadEnrollmentInventory.cs",
    "src/Hexalith.Platform.Composition/PlatformRunOwnedWorkloadEnrollment.cs",
    "src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj",
    "tests/Hexalith.Platform.Composition.Tests/PlatformRunOwnedWorkloadEnrollmentTests.cs",
    "tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj",
    "infrastructure/keycloak-workload-binding/run_owned.py",
    "infrastructure/keycloak-workload-binding/run_owned_component.py",
    "infrastructure/keycloak-workload-binding/test_run_owned.py",
    "infrastructure/keycloak-workload-binding/verify_component.py",
    "infrastructure/keycloak-workload-binding/target/keycloak-workload-binding-1.0.0-SNAPSHOT.jar",
    "docs/security/workload-binding-enrollment.md",
    "Directory.Packages.props",
    "Directory.Build.targets",
]
folders_files = [
    "src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj",
    "tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs",
    "tests/Hexalith.Folders.AppHost.Tests/OperationalProviderFixture.cs",
    "tests/Hexalith.Folders.AppHost.Tests/WorkloadEnrollmentComponentTests.cs",
    "tests/Hexalith.Folders.AppHost.Tests/WorkloadEnrollmentConfigurationTests.cs",
    "Directory.Build.props",
    "Directory.Build.targets",
    "src/Hexalith.Folders.UI/Components/CorrelationCopyButton.razor",
    "src/Hexalith.Folders.UI/Components/SafeCopyId.razor",
    "src/Hexalith.Folders.UI/Services/FoldersUserContextAccessor.cs",
    "tests/Hexalith.Folders.UI.Tests/SafeCopyIdTests.cs",
    "tests/Hexalith.Folders.UI.Tests/CorrelationCopyButtonTests.cs",
    "tests/Hexalith.Folders.UI.Tests/UserContextAccessorRegistrationTests.cs",
    "tests/Hexalith.Folders.Contracts.Tests/Deployment/AccessibilityCiWorkflowConformanceTests.cs",
    "tests/Hexalith.Folders.Contracts.Tests/Deployment/ContractParityCiWorkflowConformanceTests.cs",
    "tests/Hexalith.Folders.Contracts.Tests/Deployment/E2eCiWorkflowConformanceTests.cs",
    "tests/Hexalith.Folders.Contracts.Tests/Deployment/NfrTraceabilityConformanceTests.cs",
    "tests/Hexalith.Folders.Contracts.Tests/Deployment/SecurityRedactionCiWorkflowConformanceTests.cs",
    "tests/Hexalith.Folders.Contracts.Tests/OpenApi/ContractSpineCiGateTests.cs",
    "tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs",
    "tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.ReportTests.cs",
    "tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs",
    "tests/Hexalith.Folders.Contracts.Tests/McpCli/PackedContractsClosureTests.cs",
]
builds_files = ["Props/Directory.Packages.props", "Tools/write-platform-version-catalog.ps1",
                "src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj"]
sources = dict(Platform={f: sha(P / f) for f in platform_files}, Folders={f: sha(F / f) for f in folders_files},
               Builds={f: sha(B / f) for f in builds_files})


def assets(project_dir):
    path = Path(project_dir) / "obj/project.assets.json"
    if not path.is_file():
        return None
    data = json.loads(path.read_text())
    libraries = data["libraries"]
    selected = sorted(k for k in libraries if k.split("/")[0].startswith(("Hexalith.", "Microsoft.AspNetCore.OpenApi", "Microsoft.OpenApi")))
    return dict(frameworks=list(data["project"]["frameworks"]),
                packages=[k for k in selected if libraries[k]["type"] == "package"],
                projects=[k for k in selected if libraries[k]["type"] == "project"])


observations = {name: assets(path) for name, path in [
    ("Composition", P / "src/Hexalith.Platform.Composition"),
    ("Composition.Tests", P / "tests/Hexalith.Platform.Composition.Tests"),
    ("Folders.AppHost.Tests", F / "tests/Hexalith.Folders.AppHost.Tests"),
    ("Folders.UI.Tests", F / "tests/Hexalith.Folders.UI.Tests"),
    ("Folders.Contracts.Tests", F / "tests/Hexalith.Folders.Contracts.Tests")]}

json.dump(dict(repositories=repositories, sources=sources, restoredAssets=observations), sys.stdout, indent=1)
