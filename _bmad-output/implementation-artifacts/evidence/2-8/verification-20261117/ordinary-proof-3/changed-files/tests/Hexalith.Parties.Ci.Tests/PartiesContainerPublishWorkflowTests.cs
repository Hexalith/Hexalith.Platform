using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

namespace Hexalith.Parties.Ci.Tests;

public sealed class PartiesContainerPublishWorkflowTests
{
    private const int ExpectedPackageCount = 11;
    private const string BuildsCiSha = "3c8096180ecf2aa539ac454815efbfceb8a92822";
    private const string BuildsValidationSha = "332b2c144defba546fe0a09626a4a72f2340e22f";
    private const string BuildsExecutionSha = "11a3a79cfac4bba9548c6364a17a01ee9b561370";
    private static readonly TimeSpan WorkflowReadTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PublicationPreflightTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Both locked npm projects enforce the selected distribution before installing.</summary>
    /// <param name="directory">The npm project directory.</param>
    [Theory]
    [InlineData("")]
    [InlineData("tests/e2e/")]
    public void NpmProjectsEnforceExactNodeAndNpm(string directory)
    {
        using JsonDocument manifest = JsonDocument.Parse(CiTestPaths.ReadRepoFile($"{directory}package.json"));
        using JsonDocument packageLock = JsonDocument.Parse(CiTestPaths.ReadRepoFile($"{directory}package-lock.json"));
        foreach (JsonElement project in new[] { manifest.RootElement, packageLock.RootElement.GetProperty("packages").GetProperty("") })
        {
            JsonElement engines = project.GetProperty("engines");
            engines.GetProperty("node").GetString().ShouldBe("24.22.1");
            engines.GetProperty("npm").GetString().ShouldBe("11.19.1");
        }

        CiTestPaths.ReadRepoFile($"{directory}.npmrc").Trim().ShouldBe("engine-strict=true");
    }

    [Theory]
    [InlineData("package.json", "package-lock.json", "@commitlint/cli", "^21.2.4", "21.2.4")]
    [InlineData("package.json", "package-lock.json", "@commitlint/config-conventional", "^21.2.4", "21.2.4")]
    [InlineData("package.json", "package-lock.json", "@semantic-release/commit-analyzer", "^13.0.1", "13.0.1")]
    [InlineData("package.json", "package-lock.json", "@semantic-release/exec", "^7.1.0", "7.1.0")]
    [InlineData("package.json", "package-lock.json", "@semantic-release/github", "^12.0.10", "12.0.10")]
    [InlineData("package.json", "package-lock.json", "@semantic-release/release-notes-generator", "^14.1.1", "14.1.1")]
    [InlineData("package.json", "package-lock.json", "semantic-release", "^25.0.9", "25.0.9")]
    [InlineData("tests/e2e/package.json", "tests/e2e/package-lock.json", "@axe-core/playwright", "^4.14.0", "4.14.0")]
    [InlineData("tests/e2e/package.json", "tests/e2e/package-lock.json", "@playwright/test", "^1.65.0", "1.65.0")]
    [InlineData("tests/e2e/package.json", "tests/e2e/package-lock.json", "@types/node", "^24.19.2", "24.19.2")]
    [InlineData("tests/e2e/package.json", "tests/e2e/package-lock.json", "typescript", "^7.0.2", "7.0.2")]
    public void DependencyManifestsAndLocksMatchSelectedVersions(
        string manifestPath,
        string lockPath,
        string packageName,
        string expectedRange,
        string expectedResolvedVersion)
    {
        using JsonDocument manifest = JsonDocument.Parse(CiTestPaths.ReadRepoFile(manifestPath));
        using JsonDocument packageLock = JsonDocument.Parse(CiTestPaths.ReadRepoFile(lockPath));

        manifest.RootElement.GetProperty("devDependencies").GetProperty(packageName).GetString()
            .ShouldBe(expectedRange);
        JsonElement lockPackages = packageLock.RootElement.GetProperty("packages");
        lockPackages.GetProperty("").GetProperty("devDependencies").GetProperty(packageName).GetString()
            .ShouldBe(expectedRange);
        lockPackages.GetProperty($"node_modules/{packageName}").GetProperty("version").GetString()
            .ShouldBe(expectedResolvedVersion);
    }

    [Fact]
    public void PublicPackageEvidenceGateRequiresTheSameWorkflowArtifactAfterSuccessfulCi()
    {
        string job = ReadWorkflowJob(CiTestPaths.ReadRepoFile(".github/workflows/ci.yml"), "verify-public-package-integration");
        job.ShouldContain("needs: ci");
        job.ShouldContain("if: always()");
        job.ShouldContain("CI_RESULT: ${{ needs.ci.result }}");
        job.ShouldContain("test \"$CI_RESULT\" = success");
        job.ShouldContain("actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c");
        job.ShouldContain("name: module-integration-diagnostics");
        job.ShouldContain("scripts/verify-public-package-integration.py");
        job.ShouldContain("--source \"$GITHUB_SHA\"");
        foreach (string bypass in new[] { "continue-on-error:", "run-id:", "github-token:", "merge-multiple:" }) job.ShouldNotContain(bypass);
    }

    [Fact]
    public void CommitlintWorkflowUsesTheReviewedValidationRevision()
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/commitlint.yml");

        workflow.ShouldContain($"uses: Hexalith/Hexalith.Builds/.github/workflows/commitlint.yml@{BuildsValidationSha}");
        workflow.ShouldContain("node-version: '24.22.1'");
        workflow.ShouldContain("pull-request-title: ${{ github.event.pull_request.title || '' }}");
        workflow.ShouldNotContain("commitlint.yml@main");
    }

    [Fact]
    public void CiWorkflowDelegatesToSharedDomainCiWithPartiesTestLanes()
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/ci.yml");

        workflow.ShouldContain($"Hexalith/Hexalith.Builds/.github/workflows/domain-ci-v2.yml@{BuildsCiSha}");
        string manual = CiTestPaths.ReadRepoFile(".github/workflows/ci-lifecycle-qualification.yml");
        manual.ShouldContain("Hexalith/Hexalith.Builds/.github/workflows/domain-ci-v2.yml@6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b");
        workflow.ShouldContain("solution: Hexalith.Parties.Standalone.slnx");
        workflow.ShouldContain("test-platform: microsoft-testing-platform");
        workflow.ShouldContain("run-consumer-validation: true");
        workflow.ShouldContain("run-coverage-gate: false");
        workflow.ShouldContain("tests/Hexalith.Parties.Contracts.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Authentication.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Client.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Server.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Projections.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Security.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.AdminPortal.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.ConsumerPortal.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.UI.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Picker.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Mcp.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Sample.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Ci.Tests");
        string caller = ReadWorkflowJob(workflow, "ci");
        caller.ShouldContain("module-manifest: module/hexalith.module-manifest.v2.json");
        caller.ShouldContain("integration-test-profiles: platform-package");
        caller.ShouldContain("registry-pull-username: ${{ secrets.HEXALITH_CI_REGISTRY_USERNAME }}");
        caller.ShouldContain("registry-pull-password: ${{ secrets.HEXALITH_CI_REGISTRY_PASSWORD }}");
        caller.ShouldContain("tests/Hexalith.Parties.Aspire.Tests");
        caller.Split('\n').Count(line => line.StartsWith("        tests/", StringComparison.Ordinal)).ShouldBe(15);
        foreach (string retired in new[] { "integration-test-projects:", "aspire-test-project:", "aspire-prepare-script:", "aspire-continue-on-error:", "dapr-version:", "dapr-runtime-version:", "secrets: inherit" })
        {
            caller.ShouldNotContain(retired);
        }
        using JsonDocument tools = JsonDocument.Parse(CiTestPaths.ReadRepoFile(".config/dotnet-tools.json"));
        JsonElement tool = tools.RootElement.GetProperty("tools").GetProperty("hexalith.builds.module.cli");
        tool.GetProperty("version").GetString().ShouldBe("4.48.2");
        tool.GetProperty("rollForward").GetBoolean().ShouldBeFalse();
        workflow.ShouldNotContain("submodules: recursive");

        foreach (var cohort in new[] { (Job: "eventstore-404-package-cohort", Version: "4.0.4", Tenants: "5.11.2", Memories: "3.0.11", CatalogCount: 3, CapabilityCount: 23),
            (Job: "eventstore-410-package-cohort", Version: "4.1.0", Tenants: "7.0.2", Memories: "3.0.23", CatalogCount: 10, CapabilityCount: 33) })
        {
            string packageCohort = ReadWorkflowJob(workflow, cohort.Job);
            packageCohort.ShouldContain("-p:HexalithEventStoreVersion=" + cohort.Version);
            packageCohort.ShouldContain("-p:HexalithTenantsVersion=" + cohort.Tenants);
            packageCohort.ShouldContain("-p:HexalithMemoriesVersion=" + cohort.Memories);
            packageCohort.ShouldContain("--filter-class Hexalith.Parties.Contracts.Tests.PartiesCatalogTests");
            packageCohort.ShouldContain("--filter-class Hexalith.Parties.Contracts.Tests.PartiesCatalogAdapterTests");
            packageCohort.ShouldContain($"--minimum-expected-tests {cohort.CatalogCount} --fail-skips on");
            packageCohort.ShouldContain("--filter-method Hexalith.Parties.Ci.Tests.AspireRuntimePreparationTests.CatalogApiAvailabilityDoesNotSelectNativeHosts");
            packageCohort.ShouldContain($"--minimum-expected-tests {cohort.CapabilityCount} --fail-skips on");
            string focusedRuns = packageCohort[packageCohort.IndexOf("dotnet test --project ", StringComparison.Ordinal)..];
            focusedRuns.ShouldNotContain("-m:1");
        }
        string latestCohort = ReadWorkflowJob(workflow, "eventstore-410-package-cohort");
        latestCohort.ShouldContain("--filter-method Hexalith.Parties.Ci.Tests.AspireRuntimePreparationTests.EventStoreTransportAndSelectorCapabilitiesRequireExactQualification");
        latestCohort.ShouldContain("--filter-class Hexalith.Parties.Tests.Gateway.PartiesCatalogGatewaySelectorTests");
        latestCohort.ShouldContain("--minimum-expected-tests 9 --fail-skips on");
        string legacyProtocol = ReadWorkflowJob(workflow, "memories-3018-package-protocol");
        legacyProtocol.ShouldContain("-p:HexalithEventStoreVersion=4.0.4");
        legacyProtocol.ShouldContain("-p:HexalithTenantsVersion=7.0.0");
        legacyProtocol.ShouldContain("-p:HexalithMemoriesVersion=3.0.18");
        legacyProtocol.ShouldContain("--minimum-expected-tests 52 --fail-skips on");

        string sharedWorkflow = ReadPinnedBuildsWorkflow("domain-ci-v2.yml");
        sharedWorkflow.ShouldContain("default: 'vstest'");
        sharedWorkflow.ShouldContain("inputs.test-platform == 'microsoft-testing-platform'");
        sharedWorkflow.ShouldContain("--report-xunit-trx");
        sharedWorkflow.ShouldNotContain("--filter-not-trait");
        sharedWorkflow.ShouldNotContain("--filter-trait");
    }

    [Fact]
    public void ReleaseWorkflowPublishesOnlyPartiesContainersThroughSharedDomainPreparation()
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/release.yml").Replace("\r\n", "\n");
        string release = ReadReleaseJob();
        string preparation = ReadReleaseStep("Prepare domain release");
        string publisher = ReadReleaseStep("Prepare release container publisher");

        workflow.ShouldContain("on:\n  workflow_dispatch:");
        workflow.ShouldNotContain("on:\n  push:");
        release.ShouldContain("    needs: verify-source\n    runs-on: ubuntu-latest\n    timeout-minutes: 60\n    environment: production\n");
        release.ShouldContain("      actions: read\n");
        release.ShouldContain("      contents: write\n");
        release.ShouldContain("      id-token: write\n");
        release.ShouldContain("      issues: write\n");
        release.ShouldContain("      pull-requests: write\n");
        release.ShouldNotContain("attestations:");
        release.ShouldNotContain("domain-release.yml@");
        preparation.ShouldContain($"uses: Hexalith/Hexalith.Builds/Github/prepare-domain-release@{BuildsExecutionSha}\n");
        preparation.ShouldContain($"builds-execution-sha: {BuildsExecutionSha}\n");
        preparation.ShouldContain("solution: Hexalith.Parties.Standalone.slnx\n");
        preparation.ShouldContain("node-version: '24.22.1'\n");
        preparation.ShouldContain("source-branch: main\n");
        preparation.ShouldContain("source-ci-workflow: ci.yml\n");
        preparation.ShouldContain("package-manifest: tools/release-packages.json\n");
        preparation.ShouldContain("expected-package-count: 11\n");
        publisher.ShouldContain("uses: ./.hexalith/builds-execution/Github/publish-containers\n");
        publisher.ShouldContain($"builds-execution-sha: {BuildsExecutionSha}\n");
        workflow.ShouldContain("actions/workflows/ci.yml/runs");

        string[] expectedContainers =
        [
            "src/Hexalith.Parties/Hexalith.Parties.csproj|parties",
            "src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj|parties-mcp",
            "src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj|parties-ui",
        ];
        foreach (string step in new[] { publisher, ReadReleaseStep("Semantic Release") })
        {
            step.Split('\n').Select(line => line.Trim())
                .Where(line => line.StartsWith("src/", StringComparison.Ordinal)).ToArray()
                .ShouldBe(expectedContainers);
        }

        workflow.ShouldContain("verify-publication:");
        workflow.ShouldNotContain("secrets: inherit");
        workflow.ShouldNotContain("tests/Hexalith.Parties.Ci.Tests");
        workflow.ShouldNotContain("eventstore-admin");
        workflow.ShouldNotContain("sample-blazor-ui");
        workflow.ShouldNotContain("|tenants");
        workflow.ShouldNotContain("|memories");
        workflow.ShouldNotContain(":latest");
    }

    /// <summary>The UI requires ICU-capable runtime assets rather than invariant globalization.</summary>
    [Fact]
    public void UiContainerPreservesGlobalizationAndAlpineExtraRuntime()
    {
        XDocument project = XDocument.Parse(CiTestPaths.ReadRepoFile("src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj"));

        project.Descendants("InvariantGlobalization").Single().Value.ShouldBe("false");
        project.Descendants("ContainerBaseImage").Single().Value.ShouldBe("mcr.microsoft.com/dotnet/aspnet:10.0-alpine-extra");
        project.Descendants("ContainerFamily").Single().Value.ShouldBe("alpine-extra");
    }

    /// <summary>Each credential-bearing job independently declares its production environment.</summary>
    /// <param name="job">The job whose environment is removed in the negative control.</param>
    [Theory]
    [InlineData("release")]
    [InlineData("verify-publication")]
    public void ProtectedJobsRequireTheirOwnProductionEnvironment(string job)
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/release.yml").Replace("\r\n", "\n");
        string original = ReadWorkflowJob(workflow, job);
        RequireProductionEnvironment(original);
        string mutated = workflow.Replace(original, original.Replace("    environment: production\n", string.Empty));
        mutated.ShouldNotBe(workflow);

        string other = job == "release" ? "verify-publication" : "release";
        RequireProductionEnvironment(ReadWorkflowJob(mutated, other));
        Assert.Throws<ShouldAssertException>(() => RequireProductionEnvironment(ReadWorkflowJob(mutated, job)));
    }

    [Fact]
    public void ReleaseWorkflowUsesDispatchedCheckoutAndTemporaryCallerOwnedNuGetKey()
    {
        string release = ReadReleaseJob();
        string checkout = ReadReleaseStep("Check out dispatched source");
        string preparation = ReadReleaseStep("Prepare domain release");
        string login = ReadReleaseStep("NuGet trusted publishing login");
        string publication = ReadReleaseStep("Semantic Release");

        checkout.ShouldContain("uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1");
        checkout.ShouldContain("ref: ${{ github.sha }}\n");
        checkout.ShouldContain("fetch-depth: 0\n");
        checkout.ShouldContain("submodules: false\n");
        checkout.ShouldContain("persist-credentials: false\n");
        preparation.ShouldContain("id: prepare\n");
        preparation.ShouldContain("nuget-user: ${{ vars.NUGET_USER }}\n");
        login.ShouldContain("id: nuget-login\n");
        login.ShouldContain("uses: NuGet/login@8d196754b4036150537f80ac539e15c2f1028841");
        login.ShouldContain("user: ${{ vars.NUGET_USER }}\n");
        publication.ShouldContain("NUGET_API_KEY: ${{ steps.nuget-login.outputs.NUGET_API_KEY }}\n");
        release.Split('\n').Count(line => line.TrimStart().StartsWith("NUGET_API_KEY:", StringComparison.Ordinal))
            .ShouldBe(1);
        release.ShouldNotContain("secrets.NUGET_API_KEY");
        release.ShouldNotContain("github.actor");
        release.ShouldNotContain("github.repository_owner");
        release.IndexOf("id: nuget-login", StringComparison.Ordinal)
            .ShouldBeGreaterThan(release.IndexOf("id: prepare", StringComparison.Ordinal));
        release.IndexOf("name: Semantic Release", StringComparison.Ordinal)
            .ShouldBeGreaterThan(release.IndexOf("id: nuget-login", StringComparison.Ordinal));
    }

    [Fact]
    public void ReleaseWorkflowGatesAuthenticationAndPublicationOnSharedFreezeVerdict()
    {
        ReadReleaseStep("Prepare domain release").ShouldContain(
            "publication-flag: ${{ vars.HEXALITH_RELEASE_PUBLISH_ENABLED }}\n");

        foreach (string name in new[]
        {
            "Validate changed root gitlinks before publication",
            "Set up arm64 emulation",
            "Prepare release container publisher",
            "Authenticate release container registry",
            "NuGet trusted publishing login",
            "Semantic Release",
        })
        {
            ReadReleaseStep(name).ShouldContain("if: ${{ steps.prepare.outputs.publish-enabled == 'true' }}\n");
        }

        string emulation = ReadReleaseStep("Set up arm64 emulation");
        emulation.ShouldContain("uses: docker/setup-qemu-action@1f40c72289eff860ee54a304f1438e3cff362e0a");
        emulation.ShouldContain("platforms: arm64\n");
        string evidence = ReadReleaseStep("Upload complete release evidence");
        evidence.ShouldContain("if: ${{ always() }}\n");
        evidence.ShouldContain("uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a");
        evidence.ShouldContain("path: .hexalith/release-evidence/**\n");
        evidence.ShouldContain("include-hidden-files: true\n");
        evidence.ShouldContain("retention-days: 30\n");
    }

    [Fact]
    public void ReleaseWorkflowValidatesRootGitlinksAgainstProvedFloorBeforeLogin()
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/release.yml").Replace("\r\n", "\n");
        string release = ReadReleaseJob();
        string gate = ReadReleaseStep("Validate changed root gitlinks before publication");

        workflow.ShouldContain("\n      release-floor-tag: ${{ steps.registry-floor.outputs.release-floor-tag }}\n");
        workflow.ShouldContain("\n        id: registry-floor\n");
        int floorProved = workflow.IndexOf(
            "Release tag floor ${floor_tag} is at or above every published version of the declared packages.",
            StringComparison.Ordinal);
        int floorExported = workflow.IndexOf(
            "printf 'release-floor-tag=%s\\n' \"$floor_tag\" >> \"$GITHUB_OUTPUT\"",
            StringComparison.Ordinal);
        floorProved.ShouldBeGreaterThan(0);
        floorExported.ShouldBeGreaterThan(floorProved);
        gate.ShouldContain("\n        shell: bash\n");
        gate.ShouldContain("\n          RELEASE_FLOOR_TAG: ${{ needs.verify-source.outputs.release-floor-tag }}\n");
        gate.ShouldContain("\n        run: bash scripts/gitlink-rc-gate.sh --diff \"$RELEASE_FLOOR_TAG\"\n");
        int gatePosition = release.IndexOf("name: Validate changed root gitlinks before publication", StringComparison.Ordinal);
        gatePosition.ShouldBeGreaterThan(release.IndexOf("id: prepare", StringComparison.Ordinal));
        gatePosition.ShouldBeLessThan(release.IndexOf("name: Authenticate release container registry", StringComparison.Ordinal));
        gatePosition.ShouldBeLessThan(release.IndexOf("id: nuget-login", StringComparison.Ordinal));
        gatePosition.ShouldBeLessThan(release.IndexOf("name: Semantic Release", StringComparison.Ordinal));
    }

    [Fact]
    public void ReleaseWorkflowSkipsPublicationVerificationWhenFrozen()
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/release.yml").Replace("\r\n", "\n");
        ReadReleaseJob().ShouldContain(
            "\n    outputs:\n      publish-enabled: ${{ steps.prepare.outputs.publish-enabled }}\n");
        int verificationStart = workflow.IndexOf("  verify-publication:\n", StringComparison.Ordinal);
        verificationStart.ShouldBeGreaterThan(0);
        string verification = workflow[verificationStart..];
        verification.ShouldContain("\n    needs: release\n    if: ${{ needs.release.outputs.publish-enabled == 'true' }}\n");
    }

    [Theory]
    [InlineData("if: ${{ steps.prepare.outputs.publish-enabled == 'true' }}", false)]
    [InlineData("if: ${{ steps.prepare.outputs.publish-enabled == 'true' }}", true)]
    [InlineData("uses: NuGet/login@8d196754b4036150537f80ac539e15c2f1028841", false)]
    [InlineData("uses: NuGet/login@8d196754b4036150537f80ac539e15c2f1028841", true)]
    public void ReleaseWorkflowStructureRejectsAuthenticationFieldsOnlyInComments(string requiredField, bool inlineComment)
    {
        ArgumentNullException.ThrowIfNull(requiredField);
        const string loginName = "NuGet trusted publishing login";
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/release.yml").Replace("\r\n", "\n");
        string originalLogin = workflow.Split("      - name: ", StringSplitOptions.None)
            .Single(step => step.StartsWith($"{loginName}\n", StringComparison.Ordinal));
        string fieldLine = originalLogin.Split('\n')
            .Single(line => line.TrimStart().StartsWith(requiredField, StringComparison.Ordinal));
        string replacement = inlineComment
            ? $"        {requiredField.Split(':')[0]}: unapproved # {requiredField}"
            : $"        # {requiredField}";
        string mutatedWorkflow = workflow.Replace(originalLogin, originalLogin.Replace(fieldLine, replacement));

        ReadReleaseStep(loginName).ShouldContain(requiredField);
        string mutatedLogin = ReadReleaseStep(loginName, ReadReleaseJob(mutatedWorkflow));
        Assert.Throws<ShouldAssertException>(() => mutatedLogin.ShouldContain(requiredField));
    }

    [Fact]
    public void ReleaseWorkflowPreservesCompleteSemanticReleasePreflightEnvironment()
    {
        string publication = ReadReleaseStep("Semantic Release");
        foreach (string entry in new[]
        {
            "GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}",
            "HEXALITH_CONTAINER_PROJECTS: |",
            "HEXALITH_ZOT_REGISTRY: ${{ vars.HEXALITH_ZOT_REGISTRY || 'registry.hexalith.com' }}",
            "HEXALITH_ZOT_USERNAME: ${{ secrets.HEXALITH_ZOT_USERNAME != '' && secrets.HEXALITH_ZOT_API_KEY != '' && secrets.HEXALITH_ZOT_USERNAME || secrets.ZOT_REGISTRY_USERNAME }}",
            "HEXALITH_ZOT_API_KEY: ${{ secrets.HEXALITH_ZOT_USERNAME != '' && secrets.HEXALITH_ZOT_API_KEY != '' && secrets.HEXALITH_ZOT_API_KEY || secrets.ZOT_REGISTRY_API_KEY }}",
            $"HEXALITH_BUILDS_EXECUTION_SHA: {BuildsExecutionSha}",
            "HEXALITH_RELEASE_ENVIRONMENT: production",
            "HEXALITH_RELEASE_SOURCE_BRANCH: main",
            "HEXALITH_RELEASE_SOURCE_CI_WORKFLOW: ci.yml",
            "HEXALITH_RELEASE_PACKAGE_MANIFEST: tools/release-packages.json",
            "HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT: '11'",
            "HEXALITH_RELEASE_RESERVED_VERSION: ''",
            "HEXALITH_RELEASE_AUTHORITY_ISSUE_URL: ''",
            "HEXALITH_RELEASE_AUTHORITY_OWNER: ''",
            "HEXALITH_RELEASE_REQUIRE_AUTHORITY: 'false'",
        })
        {
            publication.ShouldContain($"          {entry}\n");
        }

        publication.ShouldContain("run: npm exec --no -- semantic-release\n");
    }

    /// <summary>Authentication, publishing and verification select the same complete Zot credential pair.</summary>
    /// <param name="job">The production job containing the credential boundary.</param>
    /// <param name="step">The step that authenticates to Zot.</param>
    [Theory]
    [InlineData("release", "Authenticate release container registry")]
    [InlineData("release", "Semantic Release")]
    [InlineData("verify-publication", "Verify every declared registry artifact")]
    public void PublicationBoundariesPreservePreferredZotSecretsAndConfiguredFallback(string job, string step)
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/release.yml").Replace("\r\n", "\n");
        string boundary = ReadReleaseStep(step, ReadWorkflowJob(workflow, job));
        const string completePreferredPair = "secrets.HEXALITH_ZOT_USERNAME != '' && secrets.HEXALITH_ZOT_API_KEY != ''";
        foreach ((string name, string fallback) in new[]
        {
            ("HEXALITH_ZOT_USERNAME", "ZOT_REGISTRY_USERNAME"),
            ("HEXALITH_ZOT_API_KEY", "ZOT_REGISTRY_API_KEY"),
        })
        {
            boundary.Split('\n').Single(line => line.StartsWith($"          {name}:", StringComparison.Ordinal))
                .ShouldBe($"          {name}: ${{{{ {completePreferredPair} && secrets.{name} || secrets.{fallback} }}}}");
        }
    }

    /// <summary>Registry authentication fails before acquiring a NuGet credential or publishing packages.</summary>
    [Fact]
    public void ReleaseWorkflowAuthenticatesRegistryBeforeNuGetLoginAndPublication()
    {
        string release = ReadReleaseJob();
        string authentication = ReadReleaseStep("Authenticate release container registry");
        authentication.ShouldContain("shell: bash\n");
        authentication.ShouldContain("set -euo pipefail\n");
        authentication.ShouldContain(": \"${HEXALITH_ZOT_USERNAME:?A complete Zot credential pair is required.}\"\n");
        authentication.ShouldContain(": \"${HEXALITH_ZOT_API_KEY:?A complete Zot credential pair is required.}\"\n");
        authentication.ShouldContain("printf '%s' \"$HEXALITH_ZOT_API_KEY\" | docker login \"$HEXALITH_ZOT_REGISTRY\" --username \"$HEXALITH_ZOT_USERNAME\" --password-stdin\n");
        int authenticationPosition = release.IndexOf("name: Authenticate release container registry", StringComparison.Ordinal);
        authenticationPosition.ShouldBeGreaterThan(release.IndexOf("name: Prepare release container publisher", StringComparison.Ordinal));
        authenticationPosition.ShouldBeLessThan(release.IndexOf("id: nuget-login", StringComparison.Ordinal));
        authenticationPosition.ShouldBeLessThan(release.IndexOf("name: Semantic Release", StringComparison.Ordinal));
    }

    [Fact]
    public void ReleaseWorkflowRequiresFixedFullCiProofAtEveryPublicationBoundary()
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/release.yml");

        workflow.ShouldContain("actions/workflows/ci.yml/runs");
        ReadReleaseStep("Prepare domain release").ShouldContain("source-ci-workflow: ci.yml\n");
        ReadReleaseStep("Semantic Release").ShouldContain("HEXALITH_RELEASE_SOURCE_CI_WORKFLOW: ci.yml\n");
        workflow.ShouldNotContain("bypass-validation");
        workflow.ShouldNotContain("BYPASS_VALIDATION");
        workflow.ShouldNotContain("commitlint.yml");
        workflow.ShouldNotContain("select-source-proof");
        workflow.ShouldNotContain("outputs.source-ci-workflow");
        workflow.ShouldNotContain("inputs:");
    }

    [Fact]
    public void MainPushRunsCiAndCodeQlWhileReleaseRequiresManualDispatch()
    {
        foreach (string path in new[] { ".github/workflows/ci.yml", ".github/workflows/codeql.yml" })
        {
            string workflow = CiTestPaths.ReadRepoFile(path).Replace("\r\n", "\n");
            string triggers = workflow[..workflow.IndexOf("\nconcurrency:", StringComparison.Ordinal)];
            triggers.ShouldContain("on:\n  push:\n    branches: [main]\n");
            string sharedWorkflow = path == ".github/workflows/ci.yml" ? "domain-ci-v2.yml" : "codeql.yml";
            string job = path == ".github/workflows/ci.yml" ? "ci" : "codeql";
            string sourceSha = path == ".github/workflows/ci.yml" ? BuildsCiSha : BuildsValidationSha;
            workflow.ShouldContain($"\njobs:\n  {job}:\n    uses: Hexalith/Hexalith.Builds/.github/workflows/{sharedWorkflow}@{sourceSha}\n");
        }

        string release = CiTestPaths.ReadRepoFile(".github/workflows/release.yml").Replace("\r\n", "\n");
        RequireManualReleaseTrigger(release);
    }

    [Theory]
    [InlineData("push")]
    [InlineData("pull_request")]
    [InlineData("schedule")]
    [InlineData("repository_dispatch")]
    [InlineData("workflow_run")]
    [InlineData("issue_comment")]
    public void ManualReleaseTriggerContractRejectsAdditionalAutomaticEvents(string automaticEvent)
    {
        string release = CiTestPaths.ReadRepoFile(".github/workflows/release.yml").Replace("\r\n", "\n");
        string mutated = release.Replace("on:\n  workflow_dispatch:\n", $"on:\n  workflow_dispatch:\n  {automaticEvent}:\n");

        mutated.ShouldNotBe(release);
        Assert.Throws<ShouldAssertException>(() => RequireManualReleaseTrigger(mutated));
    }

    [Fact]
    public void ServerAndIntegrationTestsReplaceObsoleteCollectionBehaviorWithNonParallelCollections()
    {
        string serverAssembly = CiTestPaths.ReadRepoFile("tests/Hexalith.Parties.Server.Tests/NonParallelCollection.cs");
        string integrationAssembly = CiTestPaths.ReadRepoFile("tests/Hexalith.Parties.IntegrationTests/NonParallelCollection.cs");
        string serverCollectionUse = CiTestPaths.ReadRepoFile(
            "tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCreateTests.cs");
        string integrationCollectionUse = CiTestPaths.ReadRepoFile(
            "tests/Hexalith.Parties.IntegrationTests/Events/TenantIsolationTests.cs");

        serverAssembly.ShouldContain("[CollectionDefinition(Name, DisableParallelization = true)]");
        serverAssembly.ShouldNotContain("CollectionBehavior");
        integrationAssembly.ShouldContain("[CollectionDefinition(Name, DisableParallelization = true)]");
        integrationAssembly.ShouldNotContain("CollectionBehavior");
        serverCollectionUse.ShouldContain("[Collection(\"Non-parallel\")]");
        integrationCollectionUse.ShouldContain("[Collection(\"Non-parallel\")]");

        foreach (string path in Directory.EnumerateFiles(
            CiTestPaths.RepoFile("tests/Hexalith.Parties.Server.Tests"),
            "*.cs",
            SearchOption.AllDirectories).Concat(Directory.EnumerateFiles(
            CiTestPaths.RepoFile("tests/Hexalith.Parties.IntegrationTests"),
            "*.cs",
            SearchOption.AllDirectories)))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            File.ReadAllText(path).ShouldNotContain("CollectionBehavior");
        }
    }

    [Fact]
    public void ReleaseSupportFilesDeclareSemanticReleaseAndSecretContracts()
    {
        string packageJson = CiTestPaths.ReadRepoFile("package.json");
        string releaseConfig = CiTestPaths.ReadRepoFile("release.config.cjs");
        string secretCheck = CiTestPaths.ReadRepoFile("scripts/validate-release-secrets.sh");
        string publicationPreflight = CiTestPaths.ReadRepoFile("scripts/validate-publication-preflight.sh");

        packageJson.ShouldContain("\"semantic-release\"");
        packageJson.ShouldContain("\"@commitlint/cli\"");
        releaseConfig.ShouldContain("verifyReleaseCmd");
        releaseConfig.ShouldContain("scripts/pack-release-packages.py");
        releaseConfig.ShouldContain("scripts/validate-nuget-packages.py");
        releaseConfig.ShouldContain("scripts/validate-consumer-package-references.py");
        releaseConfig.ShouldContain("scripts/validate-publication-preflight.sh ${nextRelease.version} verify");
        releaseConfig.ShouldContain("scripts/validate-publication-preflight.sh ${nextRelease.version} publish");
        releaseConfig.ShouldContain("dotnet nuget push \"./nupkgs/Hexalith.Parties.*.nupkg\"");
        releaseConfig.ShouldContain("./.hexalith/release/publish-containers.sh");
        releaseConfig.ShouldNotContain("--skip-duplicate");
        string containerTargets = CiTestPaths.ReadRepoFile("Directory.Build.targets");
        containerTargets.ShouldContain("Target Name=\"RebindContainerProvenanceLabels\"");
        containerTargets.ShouldContain("AfterTargets=\"_ParseItemsForPublishingSingleContainer\"");
        containerTargets.ShouldContain("https://github.com/Hexalith/Hexalith.Parties");
        containerTargets.ShouldContain("<ContainerGenerateLabelsImageSource>false</ContainerGenerateLabelsImageSource>");
        // Dependency-selection metadata for isolated tests is unrelated to image branding.
        // Check every container property/item, so EventStore defaults still cannot leak.
        string containerConfiguration = string.Join("\n", System.Xml.Linq.XDocument.Parse(containerTargets).Descendants()
            .Where(element => element.Name.LocalName.StartsWith("Container", StringComparison.Ordinal)));
        containerConfiguration.ShouldNotContain("Hexalith.EventStore");

        publicationPreflight.ShouldContain("readonly expected_package_count=11");
        publicationPreflight.ShouldContain(
            "HEXALITH_RELEASE_SOURCE_CI_WORKFLOW must be exactly ci.yml.");
        publicationPreflight.ShouldNotContain("commitlint.yml");
        publicationPreflight.ShouldContain("--container-repository \"registry.hexalith.com/parties\"");
        publicationPreflight.ShouldContain("--container-repository \"registry.hexalith.com/parties-mcp\"");
        publicationPreflight.ShouldContain("--container-repository \"registry.hexalith.com/parties-ui\"");
        secretCheck.ShouldContain("NUGET_API_KEY");
        secretCheck.ShouldContain("HEXALITH_ZOT_USERNAME");
        secretCheck.ShouldContain("HEXALITH_ZOT_API_KEY");
        secretCheck.ShouldNotContain("ZOT_REGISTRY_PASSWORD");
    }

    /// <summary>The existing publication reader rejects missing/invalid created labels with current shared source.</summary>
    [Fact]
    public async Task CurrentSharedValidatorPreservesThePublicationReaderContract()
    {
        var start = new ProcessStartInfo("python3")
        {
            WorkingDirectory = CiTestPaths.RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            ArgumentList = { CiTestPaths.RepoFile("tests/Hexalith.Parties.Ci.Tests/oci_validator_consumer_cases.py"), CiTestPaths.RepoFile("references/Hexalith.Builds") },
        };
        using Process process = Process.Start(start).ShouldNotBeNull();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(true);
            process.ExitCode.ShouldBe(0, await output.ConfigureAwait(true) + await error.ConfigureAwait(true));
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); }
        }
    }

    /// <summary>Executes the actual provenance targets at the image-creation boundary.</summary>
    /// <param name="created">The publisher-owned created instant.</param>
    /// <param name="valid">Whether the Gregorian calendar instant is valid.</param>
    [Theory]
    [InlineData("2024-02-29T23:59:59Z", true)]
    [InlineData("2000-02-29T00:00:00Z", true)]
    [InlineData("2026-04-30T12:34:56Z", true)]
    [InlineData("2023-02-29T00:00:00Z", false)]
    [InlineData("1900-02-29T00:00:00Z", false)]
    [InlineData("2024-04-31T00:00:00Z", false)]
    [InlineData("0000-01-01T00:00:00Z", false)]
    [InlineData("2026-01-01T24:00:00Z", false)]
    [InlineData("", false)]
    public async Task ContainerProvenanceTargetsValidateCalendarAndPreserveExactLabels(string created, bool valid)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"parties-provenance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string labelsPath = Path.Combine(directory, "labels.txt");
            string projectPath = Path.Combine(directory, "fixture.proj");
            new XDocument(new XElement("Project",
                new XElement("PropertyGroup",
                    new XElement("EnableContainer", "true"),
                    new XElement("_IsSingleRIDBuild", "true"),
                    new XElement("ContainerProvenanceSourceSha", new string('a', 40)),
                    new XElement("ContainerProvenanceReleaseVersion", "1.2.3"),
                    new XElement("ContainerProvenanceCreated", created)),
                new XElement("Import", new XAttribute("Project", CiTestPaths.RepoFile("Directory.Build.targets"))),
                new XElement("Target", new XAttribute("Name", "_ParseItemsForPublishingSingleContainer"),
                    new XElement("ItemGroup", new XElement("ContainerLabel", new XAttribute("Include", "org.opencontainers.image.source"), new XAttribute("Value", "https")))),
                new XElement("Target", new XAttribute("Name", "_PublishSingleContainer"),
                    new XAttribute("DependsOnTargets", "_ParseItemsForPublishingSingleContainer"),
                    new XElement("WriteLinesToFile", new XAttribute("File", labelsPath),
                        new XAttribute("Lines", "@(ContainerLabel->'%(Identity)=%(Value)')"), new XAttribute("Overwrite", "true"))))).Save(projectPath);
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet")
                {
                    ArgumentList = { "msbuild", projectPath, "-nologo", "-v:q", "-t:_PublishSingleContainer" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                },
            };
            process.Start().ShouldBeTrue();
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Provenance target fixture exceeded its deadline.");
            }
            string diagnostic = await stdout.ConfigureAwait(true) + await stderr.ConfigureAwait(true);
            if (valid)
            {
                process.ExitCode.ShouldBe(0, diagnostic);
                string[] labels = File.ReadAllLines(labelsPath);
                labels.Count(line => line.StartsWith("org.opencontainers.image.source=", StringComparison.Ordinal)).ShouldBe(1);
                labels.ShouldContain("org.opencontainers.image.source=https://github.com/Hexalith/Hexalith.Parties");
                labels.ShouldContain("org.opencontainers.image.created=" + created);
                labels.ShouldContain("org.opencontainers.artifact.created=" + created);
            }
            else
            {
                process.ExitCode.ShouldNotBe(0);
                diagnostic.ShouldContain("ContainerProvenanceCreated");
                File.Exists(labelsPath).ShouldBeFalse();
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReleasePackageManifestDeclaresExactElevenPackageInventory()
    {
        using JsonDocument manifest = JsonDocument.Parse(CiTestPaths.ReadRepoFile("tools/release-packages.json"));
        JsonElement[] packages = manifest.RootElement.GetProperty("packages").EnumerateArray().ToArray();

        packages.Length.ShouldBe(ExpectedPackageCount);
        using JsonDocument policy = JsonDocument.Parse(CiTestPaths.ReadRepoFile("tools/nuget-trusted-publishing-policy.json"));
        policy.RootElement.GetProperty("packageIds").EnumerateArray().Select(package => package.GetString())
            .ShouldBe(packages.Select(package => package.GetProperty("id").GetString()));
        packages.Select(package => package.GetProperty("id").GetString()).ShouldBe(
        [
            "Hexalith.Parties.Contracts",
            "Hexalith.Parties.Client",
            "Hexalith.Parties.AdminPortal",
            "Hexalith.Parties.ConsumerPortal",
            "Hexalith.Parties.Picker",
            "Hexalith.Parties.Authentication",
            "Hexalith.Parties.Projections",
            "Hexalith.Parties.Security",
            "Hexalith.Parties.Testing",
            "Hexalith.Parties.Aspire",
            "Hexalith.Parties.EventStoreExtensions",
        ]);
        packages.Select(package => package.GetProperty("project").GetString()).ShouldBe(
        [
            "src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj",
            "src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj",
            "src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj",
            "src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj",
            "src/Hexalith.Parties.Picker/Hexalith.Parties.Picker.csproj",
            "src/Hexalith.Parties.Authentication/Hexalith.Parties.Authentication.csproj",
            "src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj",
            "src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj",
            "src/Hexalith.Parties.Testing/Hexalith.Parties.Testing.csproj",
            "src/Hexalith.Parties.Aspire/Hexalith.Parties.Aspire.csproj",
            "src/Hexalith.Parties.EventStoreExtensions/Hexalith.Parties.EventStoreExtensions.csproj",
        ]);
    }

    [Theory]
    [InlineData("verify", "ci.yml")]
    [InlineData("publish", "ci.yml")]
    public void PublicationPreflightWrapperForwardsAllowedSourceWorkflowUnchanged(
        string phase,
        string sourceCiWorkflow)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Publication preflight wrapper tests require bash.");
        }

        (int exitCode, string error, bool preflightInvoked, string[] arguments) =
            RunPublicationPreflightWrapper(phase, ExpectedPackageCount.ToString(), sourceCiWorkflow);

        exitCode.ShouldBe(0, error);
        preflightInvoked.ShouldBeTrue();
        ArgumentValues(arguments, "--source-ci-workflow").ShouldBe([sourceCiWorkflow]);
        ArgumentValues(arguments, "--phase").ShouldBe([phase]);
    }

    [Theory]
    [InlineData("verify", "commitlint.yml")]
    [InlineData("publish", "commitlint.yml")]
    [InlineData("verify", "nightly.yml")]
    [InlineData("verify", "ci.yaml")]
    [InlineData("verify", "")]
    public void PublicationPreflightWrapperRejectsOtherSourceWorkflowBeforeSharedPreflight(
        string phase,
        string sourceCiWorkflow)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Publication preflight wrapper tests require bash.");
        }

        (int exitCode, string error, bool preflightInvoked, string[] _) =
            RunPublicationPreflightWrapper(phase, ExpectedPackageCount.ToString(), sourceCiWorkflow);

        exitCode.ShouldNotBe(0);
        error.ShouldContain("must be exactly ci.yml.");
        preflightInvoked.ShouldBeFalse();
    }

    [Theory]
    [InlineData("verify")]
    [InlineData("publish")]
    public void PublicationPreflightWrapperForwardsExactPackageAndContainerSet(string phase)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Publication preflight wrapper tests require bash.");
        }

        (int exitCode, string error, bool preflightInvoked, string[] arguments) =
            RunPublicationPreflightWrapper(phase, ExpectedPackageCount.ToString());

        exitCode.ShouldBe(0, error);
        preflightInvoked.ShouldBeTrue();
        arguments.Where(argument => argument == "--container-repository").Count().ShouldBe(3);
        ArgumentValues(arguments, "--container-repository").ShouldBe(
        [
            "registry.hexalith.com/parties",
            "registry.hexalith.com/parties-mcp",
            "registry.hexalith.com/parties-ui",
        ]);
        ArgumentValues(arguments, "--expected-package-count").ShouldBe(["11"]);
        ArgumentValues(arguments, "--phase").ShouldBe([phase]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("9")]
    [InlineData("10")]
    [InlineData("12")]
    public void PublicationPreflightWrapperRejectsPackageCountDriftBeforeSharedPreflight(string? packageCount)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Publication preflight wrapper tests require bash.");
        }

        (int exitCode, string error, bool preflightInvoked, string[] _) =
            RunPublicationPreflightWrapper("verify", packageCount);

        exitCode.ShouldNotBe(0);
        error.ShouldContain("expected-package-count input must be exactly 11");
        preflightInvoked.ShouldBeFalse();
    }

    [Fact]
    public void CommitlintAndDependabotUseReleaseCompatibleCommitContracts()
    {
        string commitlintConfig = CiTestPaths.ReadRepoFile("commitlint.config.mjs");
        string commitlint = CiTestPaths.ReadRepoFile(".github/workflows/commitlint.yml");
        string dependabot = CiTestPaths.ReadRepoFile(".github/dependabot.yml");

        commitlintConfig.ShouldContain("'body-max-line-length': [2, 'always', 200]");
        commitlintConfig.ShouldContain("'header-max-length': [2, 'always', 200]");
        commitlint.ShouldContain("types: [opened, synchronize, reopened, edited]");
        commitlint.ShouldContain("push:");
        commitlint.ShouldContain("pull-request-title: ${{ github.event.pull_request.title || '' }}");
        dependabot.ShouldContain("prefix: \"build(deps)\"");
        dependabot.ShouldNotContain("prefix: \"chore(deps)\"");
    }

    [Fact]
    public void CiDocsDescribeSharedCiReleaseAndZotApiKeyPublishContract()
    {
        string ci = CiTestPaths.ReadRepoFile("docs/ci.md");
        string secrets = CiTestPaths.ReadRepoFile("docs/ci-secrets-checklist.md");

        ci.ShouldContain($"Hexalith/Hexalith.Builds/.github/workflows/domain-ci-v2.yml@{BuildsCiSha}");
        ci.ShouldContain($"Hexalith/Hexalith.Builds/Github/prepare-domain-release@{BuildsExecutionSha}");
        ci.ShouldContain("workflow_dispatch");
        ci.ShouldContain("successful exact-source push run of `ci.yml`");
        ci.ShouldNotContain("bypass-validation");
        secrets.ShouldNotContain("bypass-validation");
        ci.ShouldContain("package graph selects EventStore through `HexalithEventStoreVersion`");
        ci.ShouldContain("-getProperty:HexalithEventStoreVersion,HexalithCommonsVersion");
        ci.ShouldContain("Package mode remains the authoritative CI and release path");
        ci.ShouldContain("source mode is diagnostic only");
        ci.ShouldContain("registry.hexalith.com/parties");
        ci.ShouldContain("registry.hexalith.com/parties-mcp");
        ci.ShouldContain("registry.hexalith.com/parties-ui");
        ci.ShouldContain("does not apply runtime deployment manifests");
        secrets.ShouldContain("NUGET_API_KEY");
        ci.ShouldContain("NUGET_USER");
        secrets.ShouldContain("NUGET_USER=jpiquot");
        secrets.ShouldContain("package owner `Hexalith`");
        secrets.ShouldContain("short-lived");
        secrets.ShouldContain("no fallback");
        secrets.ShouldContain("HEXALITH_ZOT_USERNAME");
        secrets.ShouldContain("HEXALITH_ZOT_API_KEY");
        secrets.ShouldContain("Zot API key");
        secrets.ShouldNotContain("ZOT_REGISTRY_PASSWORD");
    }

    /// <summary>
    /// Reads the selected reusable workflow from its immutable Git object rather than the checkout.
    /// </summary>
    private static string ReadPinnedBuildsWorkflow(string workflow)
    {
        string sourceSha = workflow == "domain-ci-v2.yml" ? BuildsCiSha : BuildsValidationSha;
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = CiTestPaths.RepoFile("references/Hexalith.Builds"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("show");
        start.ArgumentList.Add($"{sourceSha}:.github/workflows/{workflow}");
        using Process process = new() { StartInfo = start };
        process.Start().ShouldBeTrue("Could not read the selected Builds workflow Git object.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit((int)WorkflowReadTimeout.TotalMilliseconds);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        string error = errorTask.GetAwaiter().GetResult();
        exited.ShouldBeTrue($"Reading the selected Builds workflow timed out: {error}");
        process.ExitCode.ShouldBe(0, $"The Builds checkout must contain {sourceSha}: {error}");
        return outputTask.GetAwaiter().GetResult();
    }

    private static void RequireManualReleaseTrigger(string workflow)
    {
        int start = workflow.IndexOf("\non:\n", StringComparison.Ordinal);
        int end = workflow.IndexOf("\nconcurrency:", StringComparison.Ordinal);
        start.ShouldBeGreaterThan(0);
        end.ShouldBeGreaterThan(start);
        workflow[start..end].Trim().ShouldBe("on:\n  workflow_dispatch:");
    }

    private static void RequireProductionEnvironment(string job)
        => job.Split('\n').ShouldContain("    environment: production");

    private static string ReadWorkflowJob(string workflow, string name)
    {
        string[] lines = workflow.Replace("\r\n", "\n").Split('\n');
        int start = Array.IndexOf(lines, $"  {name}:");
        start.ShouldBeGreaterThanOrEqualTo(0);
        int end = start + 1;
        while (end < lines.Length && !(lines[end].StartsWith("  ", StringComparison.Ordinal)
            && lines[end].Length > 2 && lines[end][2] != ' ' && lines[end].EndsWith(':')))
        {
            end++;
        }
        return string.Join('\n', lines[start..end]);
    }

    private static string ReadReleaseJob(string? workflow = null)
    {
        workflow = (workflow ?? CiTestPaths.ReadRepoFile(".github/workflows/release.yml")).Replace("\r\n", "\n");
        int start = workflow.IndexOf("  release:\n", StringComparison.Ordinal);
        int end = workflow.IndexOf("  verify-publication:\n", StringComparison.Ordinal);
        start.ShouldBeGreaterThan(0);
        end.ShouldBeGreaterThan(start);
        return string.Join('\n', workflow[start..end].Split('\n').Select(line =>
        {
            int comment = line.IndexOf('#');
            return comment < 0 ? line : line[..comment].TrimEnd();
        }));
    }

    private static string ReadReleaseStep(string name, string? release = null)
        => (release ?? ReadReleaseJob()).Split("      - name: ", StringSplitOptions.None)
            .Single(step => step.StartsWith($"{name}\n", StringComparison.Ordinal));

    private static string[] ArgumentValues(string[] arguments, string option)
    {
        List<string> values = [];
        for (int index = 0; index < arguments.Length; index++)
        {
            if (arguments[index] == option)
            {
                (index + 1).ShouldBeLessThan(arguments.Length);
                values.Add(arguments[++index]);
            }
        }

        return [.. values];
    }

    private static (int ExitCode, string Error, bool PreflightInvoked, string[] Arguments)
        RunPublicationPreflightWrapper(
            string phase,
            string? workflowPackageCount,
            string sourceCiWorkflow = "ci.yml")
    {
        string temporary = Path.Combine(Path.GetTempPath(), $"hexalith-parties-preflight-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string invocationMarker = Path.Combine(temporary, "preflight-invoked");
            string argumentsPath = Path.Combine(temporary, "preflight-arguments");
            string recordingPreflight = Path.Combine(temporary, "record-preflight.sh");
            File.WriteAllText(
                recordingPreflight,
                "#!/usr/bin/env bash\n" +
                "set -euo pipefail\n" +
                ": > \"$PREFLIGHT_INVOCATION_MARKER\"\n" +
                "printf '%s\\n' \"$@\" > \"$PREFLIGHT_ARGUMENTS\"\n");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    recordingPreflight,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            ProcessStartInfo start = new("bash")
            {
                WorkingDirectory = CiTestPaths.RepositoryRoot,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(CiTestPaths.RepoFile("scripts/validate-publication-preflight.sh"));
            start.ArgumentList.Add("99.0.0");
            start.ArgumentList.Add(phase);
            start.Environment["HEXALITH_BUILDS_EXECUTION_SHA"] = new string('a', 40);
            start.Environment["HEXALITH_RELEASE_ENVIRONMENT"] = "production";
            start.Environment["HEXALITH_RELEASE_SOURCE_BRANCH"] = "main";
            start.Environment["HEXALITH_RELEASE_SOURCE_CI_WORKFLOW"] = sourceCiWorkflow;
            start.Environment["HEXALITH_RELEASE_PACKAGE_MANIFEST"] = "tools/release-packages.json";
            start.Environment["GITHUB_SHA"] = new string('b', 40);
            start.Environment["HEXALITH_PUBLICATION_PREFLIGHT"] = recordingPreflight;
            start.Environment["HEXALITH_ZOT_REGISTRY"] = "registry.hexalith.com";
            start.Environment["PREFLIGHT_INVOCATION_MARKER"] = invocationMarker;
            start.Environment["PREFLIGHT_ARGUMENTS"] = argumentsPath;
            start.Environment.Remove("HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT");
            if (workflowPackageCount is not null)
            {
                start.Environment["HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT"] = workflowPackageCount;
            }

            using Process process = new() { StartInfo = start };
            process.Start().ShouldBeTrue("Could not start the publication preflight wrapper.");
            process.StandardOutput.ReadToEnd();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            bool exited = process.WaitForExit((int)PublicationPreflightTimeout.TotalMilliseconds);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            string error = errorTask.GetAwaiter().GetResult();
            exited.ShouldBeTrue($"Publication preflight wrapper timed out: {error}");
            string[] arguments = File.Exists(argumentsPath) ? File.ReadAllLines(argumentsPath) : [];
            return (process.ExitCode, error, File.Exists(invocationMarker), arguments);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }
}
