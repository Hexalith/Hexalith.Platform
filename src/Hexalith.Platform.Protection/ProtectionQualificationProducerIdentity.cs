using System.Security.Cryptography;
using Hexalith.EventStore.PayloadProtection;

namespace Hexalith.Platform.Protection;

/// <summary>Actual executor engine and installed role identity bound into independently authenticated retained producer originals.</summary>
/// <param name="EngineSourceRevision">Loaded executor engine revision.</param><param name="EngineBinarySha256">Loaded executor engine binary.</param><param name="RoleIdentity">Accepted executor identity.</param>
/// <param name="BindingDigest">Accepted role/installation binding.</param><param name="StoreId">Native ERC identity.</param><param name="EnrollmentId">Native ERC enrollment.</param>
public sealed record ProtectionQualificationProducerIdentity(string EngineSourceRevision, string EngineBinarySha256, string RoleIdentity, string BindingDigest, string StoreId, string EnrollmentId)
{
    internal static ProtectionQualificationProducerIdentity Capture(InteractionPayloadProtectionService engine, ErasureRootInstallation installation, string roleIdentity = "component-executor", string bindingDigest = "component")
        => new(engine.GetIdentity().SourceRevision, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(InteractionPayloadProtectionService).Assembly.Location))),
            roleIdentity, bindingDigest, installation.StoreId, installation.EnrollmentId);
}
