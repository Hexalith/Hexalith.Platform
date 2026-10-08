namespace Hexalith.Platform.Custody;

/// <summary>Private exact context for export-key encryption, separate from HMAC or approval keys.</summary>
/// <param name="TenantId">The exact owner tenant.</param>
/// <param name="ExportId">The immutable export identity.</param>
/// <param name="KekVersion">The exact wrapping-key version.</param>
internal sealed record ExportKeyWrapContext(string TenantId, string ExportId, string KekVersion);
