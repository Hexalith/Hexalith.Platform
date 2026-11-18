namespace Hexalith.Platform.Composition;

using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;

/// <summary>Resolves and checks the exact released Linux/amd64 images loaded by the package run.</summary>
internal sealed class PlatformReleasedImages(
    PlatformCompositionOptions options, IReadOnlyList<PlatformServerBinding> servers, string directory) : ICompositionRunPreparation
{
    internal const string ResourceLabel = "hexalith.platform.resource";

    /// <summary>Measures the selected repository tag before composing its immutable container reference.</summary>
    internal static PlatformServerBinding Resolve(PlatformCompositionOptions options, PlatformServerBinding server)
    {
        string image = server.Image!;
        try
        {
            // Buildx selects the tag from the registry without consulting Docker's local manifest list store.
            using var response = JsonDocument.Parse(Probe(["buildx", "imagetools", "inspect", image, "--format", "{{json .Manifest}}"]));
            JsonElement release = response.RootElement;
            if (!IsDigest(release.GetProperty("digest").GetString())) throw Unavailable(server);
            var selected = release.TryGetProperty("manifests", out var manifests)
                ? manifests.EnumerateArray().Single(candidate => candidate.TryGetProperty("platform", out var platform) && IsLinuxX64(platform))
                : release;
            string? digest = selected.GetProperty("digest").GetString();
            if (!IsDigest(digest)) throw Unavailable(server);

            string reference = image[..image.LastIndexOf(':')] + "@" + digest;
            using var immutable = JsonDocument.Parse(Probe(["manifest", "inspect", "--verbose", reference]));
            JsonElement manifest = immutable.RootElement;
            JsonElement descriptor = manifest.GetProperty("Descriptor");
            if (manifest.GetProperty("Ref").GetString() != reference || descriptor.GetProperty("digest").GetString() != digest
                || !IsLinuxX64(descriptor.GetProperty("platform"))) throw Unavailable(server);
            byte[] raw = manifest.GetProperty("Raw").GetBytesFromBase64();
            if (digest != "sha256:" + Convert.ToHexStringLower(SHA256.HashData(raw))) throw Unavailable(server);
            using var body = JsonDocument.Parse(raw);
            string? config = body.RootElement.GetProperty("config").GetProperty("digest").GetString();
            if (body.RootElement.GetProperty("schemaVersion").GetInt32() != 2 || !IsDigest(config)) throw Unavailable(server);
            return server with { ImageDigest = digest!["sha256:".Length..], ImageConfigDigest = config };
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            throw Unavailable(server, exception);
        }

        string Probe(string[] arguments)
        {
            var start = CompositionProcess.CreateStartInfo("docker", arguments, options.PlatformRoot, null);
            var result = options.ImageProbeRunner(start,
                PlatformRoutingCatalog.ReaderTimeout(TimeSpan.FromSeconds(20), options.RunPlan?.StartupDeadlineUtc, DateTimeOffset.UtcNow),
                CancellationToken.None).GetAwaiter().GetResult();
            if (!result.Started || result.ExitCode != 0 || result.TimedOut || result.OutputTruncated) throw Unavailable(server);
            return result.Output;
        }
    }

    private static bool IsLinuxX64(JsonElement platform)
        => platform.TryGetProperty("os", out var os) && os.GetString() == "linux"
            && platform.TryGetProperty("architecture", out var architecture) && architecture.GetString() == "amd64";

    /// <inheritdoc />
    public async Task PrepareAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var plan = options.RunPlan!;
        var resolved = servers.Where(server => server.ImageDigest is not null).ToArray();
        var observations = await Task.WhenAll(resolved.Select(async server =>
        {
            string containerId;
            while (true)
            {
                string matches = await DockerAsync(server, ["ps", "--quiet", "--no-trunc", "--filter",
                    $"label={CompositionEnvironment.ContainerRunLabel}={plan.RunId}", "--filter", $"label={ResourceLabel}={server.ResourceId}"], cancellationToken).ConfigureAwait(false);
                if (matches.Length > 0)
                {
                    containerId = matches;
                    if (containerId.Length != 64 || !IsDigest("sha256:" + containerId)) throw LoadedMismatch(server);
                    break;
                }
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
            string facts = await DockerAsync(server, ["container", "inspect", "--format",
                "{{.Id}} {{.Image}} {{.Config.Image}} {{.State.Running}}", containerId], cancellationToken).ConfigureAwait(false);
            string[] fields = facts.Split(' ');
            if (fields.Length != 4 || fields[0] != containerId || fields[3] != "true"
                || !MatchesLoadedImage(server, fields[2], fields[1]))
                throw LoadedMismatch(server);
            return new
            {
                server.ResourceId, catalogImage = server.Image, expectedReference = server.ImmutableImage,
                expectedManifestDigest = "sha256:" + server.ImageDigest, expectedConfigDigest = server.ImageConfigDigest,
                observedReference = fields[2], observedImageId = fields[1], containerId,
            };
        })).ConfigureAwait(false);
        await File.WriteAllBytesAsync(Path.Combine(directory, "released-images.json"), JsonSerializer.SerializeToUtf8Bytes(new
        {
            plan.RunId, plan.AttemptId, plan.EnvironmentId, mappingHash = options.Mapping.ContentHash,
            qualificationCredit = false, images = observations,
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Checks the immutable reference and the loaded ID Docker reports for its image store.</summary>
    internal static bool MatchesLoadedImage(PlatformServerBinding server, string reference, string imageId)
        => server.ImageDigest is not null && server.ImageConfigDigest is not null
            && (reference == server.ImmutableImage || reference == server.Image + "@sha256:" + server.ImageDigest)
            && (imageId == server.ImageConfigDigest || imageId == "sha256:" + server.ImageDigest);

    private async Task<string> DockerAsync(PlatformServerBinding server, string[] arguments, CancellationToken cancellationToken)
    {
        var result = await options.ImageProbeRunner(CompositionProcess.CreateStartInfo("docker", arguments, options.PlatformRoot, null),
            TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
        if (!result.Started || result.ExitCode != 0 || result.TimedOut || result.OutputTruncated) throw LoadedMismatch(server);
        return result.Output.Trim();
    }

    private static bool IsDigest(string? value) => value is { Length: 71 } && value.StartsWith("sha256:", StringComparison.Ordinal)
        && value.Skip("sha256:".Length).All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static InvalidDataException Unavailable(PlatformServerBinding server, Exception? inner = null)
        => new($"{server.Module.Source}: {server.ResourceId}: image '{server.Image}' is unavailable or has no valid released Linux/amd64 digest.", inner);

    private static InvalidDataException LoadedMismatch(PlatformServerBinding server)
        => new($"{server.ResourceId}: image '{server.Image}' loaded a different artifact than selected reference '{server.ImmutableImage}'.");
}
