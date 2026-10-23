using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.Builds.Tooling.Runtime;

public static class DaprSelfHostedMtls
{
    private const string CertificateDirectoryConfigurationKey = "Dapr:Mtls:CertificateDirectory";
    private const string ContainerCredentialsDirectory = "/var/run/dapr/credentials";
    private const string ContainerSentryConfigurationPath = "/var/run/dapr/config/sentry.yaml";
    private const string SchedulerDataDirectory = "/var/lock/dapr/scheduler";
    private const int PlacementContainerPort = 50005;
    private const int SchedulerContainerPort = 50006;
    private const int SentryPort = 50001;

    /// <summary>How many times a Sentry-issued credential is re-read while it is missing, locked, or empty.</summary>
    private const int CredentialReadAttempts = 20;

    /// <summary>The delay between credential read attempts (20 attempts ≈ 9.5 seconds of budget).</summary>
    private static readonly TimeSpan CredentialReadRetryDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>Adds Sentry with an allocated host listener and a private issuer directory.</summary>
    internal static (IResourceBuilder<ContainerResource> Sentry, string CertificateDirectory) AddSentry(
        IDistributedApplicationBuilder builder,
        string sentryConfigurationPath,
        string platformRoot,
        string runDirectory,
        int sentryHostPort,
        string? runtimeVersion = null,
        IReadOnlyList<string>? checkoutRoots = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(sentryConfigurationPath);

        string parent = ResolveCertificateParent(builder, platformRoot, runDirectory, checkoutRoots);
        string identity = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(runDirectory))))[..32];
        string certificateDirectory = Path.Combine(parent, "dapr-credentials-" + identity);
        if (new DirectoryInfo(certificateDirectory).LinkTarget is not null)
            throw new InvalidOperationException($"Private Sentry credential directory '{certificateDirectory}' must not be a symbolic link.");
        // Builds preflight and execution compose the same workspace. Reuse only that run's private child.
        CompositionWorkspace.CreatePrivateDirectory(certificateDirectory);

        IResourceBuilder<ContainerResource> sentry = builder
            .AddContainer("dapr-sentry", runtimeVersion is null ? "daprio/sentry" : "ghcr.io/dapr/sentry", runtimeVersion ?? CompositionToolchainPins.DaprRuntimeVersion)
            .WithEntrypoint("/sentry")
            .WithArgs(
                $"--issuer-credentials={ContainerCredentialsDirectory}",
                // Dapr CLI 1.18 does not forward spec.mtls.controlPlaneTrustDomain to daprd. Use daprd's
                // standalone default so the generated CLI resource and Sentry agree without a wrapper script.
                "--trust-domain=localhost",
                $"--config={ContainerSentryConfigurationPath}",
                "--listen-address=0.0.0.0",
                "--healthz-listen-address=0.0.0.0")
            .WithBindMount(certificateDirectory, ContainerCredentialsDirectory)
            .WithBindMount(sentryConfigurationPath, ContainerSentryConfigurationPath, isReadOnly: true)
            // Native daprd uses this allocated loopback listener directly while obtaining its identity.
            .WithEndpoint(port: sentryHostPort, targetPort: SentryPort, name: "grpc", isProxied: false)
            .WithHttpEndpoint(targetPort: 8080, name: "health")
            .WithHttpHealthCheck("/healthz", endpointName: "health")
            .WithLifetime(ContainerLifetime.Session)
            .ExcludeFromManifest();

        if (runtimeVersion is not null) sentry.WithImageSHA256("2f98508dff56c75329dbd51674c89f41ce349e06c7744ab2519cb69ba338d41f");
        string? containerUser = ResolveContainerUser();
        if (!string.IsNullOrWhiteSpace(containerUser))
        {
            sentry = sentry.WithContainerRuntimeArgs("--user", containerUser);
        }

        return (sentry, certificateDirectory);
    }

    /// <summary>
    /// Adds mTLS-enabled placement and scheduler services for the local sidecars.
    /// </summary>
    public static (IResourceBuilder<ContainerResource> Placement, IResourceBuilder<ContainerResource> Scheduler)
        AddControlPlane(
            IDistributedApplicationBuilder builder,
            IResourceBuilder<ContainerResource> sentry,
            string certificateDirectory,
            string schedulerDataDirectory,
            int placementHostPort,
            int schedulerHostPort,
            string? runtimeVersion = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(sentry);
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(schedulerDataDirectory);
        CompositionWorkspace.CreatePrivateDirectory(schedulerDataDirectory);

        IResourceBuilder<ContainerResource> placement = builder
            .AddContainer("dapr-placement-mtls", runtimeVersion is null ? "daprio/dapr" : "ghcr.io/dapr/placement", runtimeVersion ?? CompositionToolchainPins.DaprRuntimeVersion)
            .WithEntrypoint("./placement")
            .WithArgs(
                "--tls-enabled",
                "--sentry-address=dapr-sentry:50001",
                "--trust-domain=localhost",
                $"--trust-anchors-file={ContainerCredentialsDirectory}/ca.crt",
                "--healthz-listen-address=0.0.0.0",
                "--metadata-enabled")
            .WithBindMount(certificateDirectory, ContainerCredentialsDirectory, isReadOnly: true)
            .WithEndpoint(
                port: placementHostPort,
                targetPort: PlacementContainerPort,
                name: "grpc",
                isProxied: false)
            .WithHttpEndpoint(targetPort: 8080, name: "health")
            .WithHttpHealthCheck("/healthz", endpointName: "health")
            .WithLifetime(ContainerLifetime.Session)
            .WaitFor(sentry)
            .ExcludeFromManifest();

        IResourceBuilder<ContainerResource> scheduler = builder
            .AddContainer("dapr-scheduler-mtls", runtimeVersion is null ? "daprio/dapr" : "ghcr.io/dapr/scheduler", runtimeVersion ?? CompositionToolchainPins.DaprRuntimeVersion)
            .WithEntrypoint("./scheduler")
            .WithArgs(
                "--tls-enabled",
                "--sentry-address=dapr-sentry:50001",
                "--trust-domain=localhost",
                $"--trust-anchors-file={ContainerCredentialsDirectory}/ca.crt",
                $"--etcd-data-dir={SchedulerDataDirectory}",
                $"--override-broadcast-host-port=localhost:{schedulerHostPort}",
                "--healthz-listen-address=0.0.0.0")
            .WithBindMount(certificateDirectory, ContainerCredentialsDirectory, isReadOnly: true)
            .WithEndpoint(
                port: schedulerHostPort,
                targetPort: SchedulerContainerPort,
                name: "grpc",
                isProxied: false)
            .WithHttpEndpoint(targetPort: 8080, name: "health")
            .WithHttpHealthCheck("/healthz", endpointName: "health")
            .WithLifetime(ContainerLifetime.Session)
            .WaitFor(sentry)
            .ExcludeFromManifest();

        scheduler.WithBindMount(schedulerDataDirectory, "/var/lock");

        if (runtimeVersion is not null)
        {
            placement.WithImageSHA256("6caf20016d115d4a7f133b9206b739a10abd9f558d76683b27be9ab60f759e26");
            scheduler.WithImageSHA256("c9bb9ada0cd6a63cd92c26470da1985124e423432af4e39f09b96979fd1059c0");
        }
        string? containerUser = ResolveContainerUser();
        if (!string.IsNullOrWhiteSpace(containerUser))
        {
            placement = placement.WithContainerRuntimeArgs("--user", containerUser);
            scheduler = scheduler.WithContainerRuntimeArgs("--user", containerUser);
        }

        return (placement, scheduler);
    }

    /// <summary>
    /// Makes a project and its generated Dapr CLI wait for Sentry, then injects the generated trust bundle.
    /// </summary>
    public static void ConfigureSidecar<T>(
        IResourceBuilder<T> project,
        IDaprSidecarResource sidecar,
        IResourceBuilder<ContainerResource> sentry,
        string certificateDirectory,
        IResourceBuilder<ContainerResource>? placement = null,
        IResourceBuilder<ContainerResource>? scheduler = null)
        where T : IResourceWithWaitSupport
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(sidecar);
        ArgumentNullException.ThrowIfNull(sentry);
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateDirectory);

        _ = project.WaitFor(sentry);
        if (placement is not null)
        {
            _ = project.WaitFor(placement);
        }

        if (scheduler is not null)
        {
            _ = project.WaitFor(scheduler);
        }

        sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(async context =>
        {
            context.EnvironmentVariables["DAPR_TRUST_ANCHORS"] =
                await ReadCredentialAsync(certificateDirectory, "ca.crt", context.CancellationToken).ConfigureAwait(false);
            context.EnvironmentVariables["DAPR_CERT_CHAIN"] =
                await ReadCredentialAsync(certificateDirectory, "issuer.crt", context.CancellationToken).ConfigureAwait(false);
            context.EnvironmentVariables["DAPR_CERT_KEY"] =
                await ReadCredentialAsync(certificateDirectory, "issuer.key", context.CancellationToken).ConfigureAwait(false);
            context.EnvironmentVariables["DAPR_CONTROLPLANE_TRUST_DOMAIN"] = "localhost";
            context.EnvironmentVariables["DAPR_CONTROLPLANE_NAMESPACE"] = "default";
            context.EnvironmentVariables["NAMESPACE"] = "default";
        }));
    }

    /// <summary>
    /// Reads one Sentry-issued credential, retrying while it is missing, unreadable, or still empty.
    /// </summary>
    /// <remarks>
    /// Sentry reporting healthy does not mean it has finished flushing its issuer material to the mounted
    /// directory, and a single-shot read that happens to observe a zero-length file would otherwise hand the
    /// sidecar an empty PEM — which fails much later as an opaque mTLS handshake error instead of here. Fail
    /// closed with the real reason once the bounded budget is spent.
    /// </remarks>
    private static async Task<string> ReadCredentialAsync(
        string certificateDirectory,
        string fileName,
        CancellationToken cancellationToken)
        => await ReadCredentialAsync(
            certificateDirectory,
            fileName,
            CredentialReadAttempts,
            CredentialReadRetryDelay,
            static (delay, token) => Task.Delay(delay, token),
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Reads one Sentry-issued credential with a controllable bounded retry policy.
    /// </summary>
    /// <param name="certificateDirectory">The directory containing the credential.</param>
    /// <param name="fileName">The credential file name.</param>
    /// <param name="maxAttempts">The maximum number of read attempts.</param>
    /// <param name="retryDelay">The delay requested between attempts.</param>
    /// <param name="delayAsync">The delay boundary.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The non-empty credential contents.</returns>
    public static async Task<string> ReadCredentialAsync(
        string certificateDirectory,
        string fileName,
        int maxAttempts,
        TimeSpan retryDelay,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        CancellationToken cancellationToken)
    {
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "The credential read attempt budget must be positive.");
        }

        if (retryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay), retryDelay, "The credential retry delay must not be negative.");
        }

        ArgumentNullException.ThrowIfNull(delayAsync);

        string path = Path.Combine(certificateDirectory, fileName);
        Exception? lastFailure = null;
        string lastReason = "the credential file never became readable";

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (attempt > 0)
            {
                await delayAsync(retryDelay, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                string content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    return content;
                }

                lastFailure = null;
                lastReason = "the credential file was still empty";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastFailure = ex;
                lastReason = $"{ex.GetType().Name}: {ex.Message}";
            }
        }

        throw new InvalidOperationException(
            $"Dapr Sentry credential '{path}' was unavailable after Sentry became healthy: {lastReason} "
            + $"(retried {maxAttempts} times over "
            + $"{(maxAttempts - 1) * retryDelay.TotalSeconds:0.#} seconds). "
            + "Keep the certificate directory outside the repository and ensure the Sentry container user can write it.",
            lastFailure);
    }

    /// <summary>Resolves the issuer parent, refusing one that resolves inside any mapped checkout.</summary>
    /// <remarks>Composition calls this before it creates run state, so a misplaced parent leaves nothing behind.</remarks>
    internal static string ResolveCertificateParent(IDistributedApplicationBuilder builder, string platformRoot, string runDirectory, IReadOnlyList<string>? checkoutRoots)
    {
        string configured = builder.Configuration[CertificateDirectoryConfigurationKey] ?? runDirectory;
        string resolved = ResolveExistingLinks(Path.GetFullPath(configured));
        StringComparison pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        foreach (string checkout in new[] { platformRoot }.Concat(checkoutRoots ?? []).Distinct(StringComparer.Ordinal))
        {
            string repositoryRoot = ResolveExistingLinks(Path.GetFullPath(checkout));
            string relative = Path.GetRelativePath(repositoryRoot, resolved);
            bool hasSameRoot = string.Equals(Path.GetPathRoot(repositoryRoot), Path.GetPathRoot(resolved), pathComparison);
            if (hasSameRoot && !relative.StartsWith(".." + Path.DirectorySeparatorChar, pathComparison)
                && !string.Equals(relative, "..", pathComparison))
                throw new InvalidOperationException(
                    $"Configuration '{CertificateDirectoryConfigurationKey}' must resolve outside the repository; got '{resolved}' inside mapped checkout '{repositoryRoot}'.");
        }

        return resolved;
    }

    private static string ResolveExistingLinks(string path, int depth = 0)
    {
        // Linux refuses deeper chains with ELOOP; a cycle would otherwise recurse forever.
        if (depth > 40)
            throw new InvalidOperationException($"Path '{path}' has too many nested symbolic links.");
        string resolved = Path.GetFullPath(path);
        string root = Path.GetPathRoot(resolved)
            ?? throw new InvalidOperationException($"Path '{resolved}' has no filesystem root.");
        string current = root;
        string[] segments = resolved[root.Length..]
            .Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

        foreach (string segment in segments)
        {
            string candidate = Path.Combine(current, segment);
            var entry = new DirectoryInfo(candidate);
            if (entry.LinkTarget is not null)
            {
                FileSystemInfo target = entry.ResolveLinkTarget(returnFinalTarget: true)
                    ?? throw new InvalidOperationException($"Could not resolve symbolic link '{candidate}'.");
                // The final target can itself pass through linked ancestors.
                current = ResolveExistingLinks(target.FullName, depth + 1);
            }
            else
            {
                current = candidate;
            }
        }

        return Path.GetFullPath(current);
    }

    internal static string? ResolveContainerUser()
    {
        if (!OperatingSystem.IsLinux() || !File.Exists("/proc/self/status"))
        {
            return null;
        }

        Dictionary<string, string> status = File.ReadLines("/proc/self/status")
            .Select(static line => line.Split(':', 2))
            .Where(static parts => parts.Length == 2)
            .ToDictionary(static parts => parts[0], static parts => parts[1], StringComparer.Ordinal);
        string? userId = FirstValue(status, "Uid");
        string? groupId = FirstValue(status, "Gid");
        return userId is null || groupId is null ? null : $"{userId}:{groupId}";
    }

    private static string? FirstValue(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out string? value)
            ? value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            : null;
}
