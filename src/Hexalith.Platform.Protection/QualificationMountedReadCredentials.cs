namespace Hexalith.Platform.Protection;

/// <summary>Explicit externally issued resource-bound workload token and RSA proof-key locators. The reader never mints workload or decision authority.</summary>
public sealed record QualificationMountedReadCredentials(string Caller,string RsaPkcs8Base64File,IReadOnlyDictionary<string,string> AssertionFiles,string? OwnerQueryPrincipal=null);
