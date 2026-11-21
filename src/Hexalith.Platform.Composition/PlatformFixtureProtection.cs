namespace Hexalith.Platform.Composition;

using System.Security.Cryptography;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Builds.Tooling.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>Owns the disposable protection dependency for the authenticated signed fixture only.</summary>
internal sealed class PlatformFixtureProtection : IHealthCheck
{
    private const string _tokenVariable = "HEXALITH_STORY32_PROTECTION_BEARER";
    private const string _healthName = "fixture-erasure-root-installed";
    private readonly CompositionRunPlan _plan;
    private readonly string _directory;
    private readonly IResourceBuilder<ExecutableResource> _resource;
    private readonly IResourceBuilder<ParameterResource> _credential;

    private PlatformFixtureProtection(IDistributedApplicationBuilder builder, PlatformCompositionOptions options)
    {
        _plan = options.RunPlan ?? throw PlatformFixturePackageQualification.Refusal("protection-run-owner");
        _directory = Path.Combine(_plan.Workspace, "fixture-protection");
        if (Path.GetFileName(_plan.Workspace) != _plan.RunId || new DirectoryInfo(_plan.Workspace).LinkTarget is not null)
            throw PlatformFixturePackageQualification.Refusal("protection-workspace");

        string provisioner = Committed(options.PlatformRoot, "eng/test-extension-release/provision-fixture-protection.py");
        string bootstrap = Committed(options.PlatformRoot, "deploy/erasure-root/bootstrap.py");
        _ = Committed(options.PlatformRoot, "deploy/erasure-root/custody-policy.hcl");

        string credential = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        _credential = builder.AddParameter("fixture-erasure-root-credential", () => credential, secret: true);
        builder.Services.AddHealthChecks().AddCheck(_healthName, this);
        _resource = builder.AddExecutable("fixture-erasure-root", "python3", _plan.Workspace,
            "-I", "-B", provisioner, bootstrap, _plan.RunId)
            .WithEnvironment(CompositionEnvironment.RunId, _plan.RunId)
            .WithEnvironment(_tokenVariable, _credential)
            .WithHealthCheck(_healthName);
    }

    internal static PlatformFixtureProtection Attach(IDistributedApplicationBuilder builder, PlatformCompositionOptions options)
        => options.FixturePackageProof?.Matches(options) == true
            ? new(builder, options)
            : throw PlatformFixturePackageQualification.Refusal("protection-fixture-proof");

    internal void Configure(IResourceBuilder<ProjectResource> gateway)
    {
        gateway.WaitFor(_resource)
            .WithEnvironment(_tokenVariable, _credential)
            .WithEnvironment("EventStore__PayloadProtection__Enabled", "true")
            .WithEnvironment("EventStore__PayloadProtection__QualificationObserver", "true")
            .WithEnvironment("EventStore__PayloadProtection__InstallationFile", InstallationPath)
            .WithEnvironment(async context =>
            {
                string? digest;
                while ((digest = GetVerifiedInstallation()) is null)
                {
                    if (_plan.StartupDeadlineUtc is { } deadline && DateTimeOffset.UtcNow >= deadline)
                        throw PlatformFixturePackageQualification.Refusal("protection-installation-deadline");
                    await Task.Delay(100, context.CancellationToken).ConfigureAwait(false);
                }
                context.EnvironmentVariables["EventStore__PayloadProtection__InstallationSha256"] = digest;
            });
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetVerifiedInstallation() is not null ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The run-owned native protection installation is unavailable."));
    }

    private string InstallationPath => Path.Combine(_directory, "installation.json");

    private string? GetVerifiedInstallation()
    {
        string receipt = Path.Combine(_directory, "installed.json");
        if (!File.Exists(receipt)) return null;
        _ = PlatformFixturePackageQualification.Hash(receipt);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(receipt));
        JsonElement body = document.RootElement;
        string expected = body.GetProperty("installationSha256").GetString()!;
        if (body.GetProperty("runId").GetString() != _plan.RunId || !body.GetProperty("healthy").GetBoolean()
            || body.GetProperty("runtimeAccess").GetString() != "read-only"
            || body.GetProperty("networkMode").GetString() != "none" || body.GetProperty("listenerAddress").GetString() != "127.0.0.1"
            || body.GetProperty("publishedPorts").GetInt32() != 0
            || !body.GetProperty("rootRevoked").GetBoolean() || expected != PlatformFixturePackageQualification.Hash(InstallationPath))
            throw PlatformFixturePackageQualification.Refusal("protection-installation-identity");
        return expected;
    }

    private static string Committed(string root, string relative)
    {
        string path = Path.Combine(root, relative);
        PlatformPartiesRunProof.RequireCommittedFile(root, path);
        _ = PlatformFixturePackageQualification.Hash(path);
        return path;
    }
}
