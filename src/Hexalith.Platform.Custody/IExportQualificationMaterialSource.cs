namespace Hexalith.Platform.Custody;

/// <summary>Explicit dedicated export-key resolver. Production profile, exact native key/holder admission and restricted credential protocol remain required inputs.</summary>
public interface IExportQualificationMaterialSource
{
    /// <summary>Names missing concrete installed purpose/key/holder protocols before source execution.</summary>
    Task<IReadOnlyList<string>> PreflightAsync(ExportQualificationInput input,ExportQualificationMaterialUse use,CancellationToken cancellationToken);
    /// <summary>Releases only dedicated original export-family material into an owned ephemeral holder.</summary>
    Task<ExportQualificationMaterial?> ResolveAsync(ExportQualificationInput input,ExportQualificationMaterialUse use,CancellationToken cancellationToken);
}
