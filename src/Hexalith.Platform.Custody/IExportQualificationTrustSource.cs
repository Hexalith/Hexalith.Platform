namespace Hexalith.Platform.Custody;

/// <summary>Separate independently current public export signing trust; application startup cannot create an anchor.</summary>
public interface IExportQualificationTrustSource
{
    /// <summary>Reads only the exact original version's independently published public verifier.</summary>
    Task<ExportQualificationTrust?> ResolveAsync(ExportQualificationInput input,CancellationToken cancellationToken);
}
