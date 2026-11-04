namespace Hexalith.Platform.Composition;

using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;

/// <summary>An in-memory proof created only by the key-bearing runner entry point; never serialized or accepted from Compose options.</summary>
internal sealed class PlatformQualificationOwnerProof
{
    private readonly string _sourceHash;
    private readonly string _planDigest;
    private readonly IReadOnlyList<string> _owners;

    private PlatformQualificationOwnerProof(CompositionRunPlan plan)
    {
        _sourceHash = plan.SourceMappingHash!;
        _planDigest = Digest(plan);
        _owners = CompositionQualificationOwners.Normalize(plan.QualificationOwners);
    }

    /// <summary>Authenticates the complete additive request with the parent's existing in-memory run key.</summary>
    internal static PlatformQualificationOwnerProof Create(CompositionRunPlan plan, string signingKey)
    {
        CompositionQualificationOwners.Verify(plan, signingKey, required: true);
        return new(plan);
    }

    /// <summary>Rechecks the actual mapping, request and metadata after entry-point verification and before effects.</summary>
    internal bool Matches(PlatformCompositionOptions options) => options.RunPlan is { } plan
        && plan.SourceMappingHash == _sourceHash && options.Mapping.ContentHash == _sourceHash
        && Digest(plan) == _planDigest
        && options.ActiveModule == plan.PlatformActiveModule
        && options.PlatformRoot == plan.SourceMapping?.PlatformIdentity?.PlatformPath
        && (options.EnvironmentId is null || options.EnvironmentId == plan.EnvironmentId)
        && options.DeclarationPaths.SequenceEqual(plan.PlatformDeclarationPaths, StringComparer.Ordinal)
        && CompositionQualificationOwners.Normalize(options.QualificationOwners).SequenceEqual(_owners, StringComparer.Ordinal);

    private static string Digest(CompositionRunPlan plan) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(plan)));
}
