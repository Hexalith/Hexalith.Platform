namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Native archive volume capacity and durable write probe. The owning executable uses the actual filesystem implementation.</summary>
public interface IIndependentArchiveStorage
{
    /// <summary>Reads available bytes on the volume containing this exact archive path.</summary>
    long AvailableBytes(string archiveDirectory);
    /// <summary>Proves current durable create/delete access in the actual existing archive directory.</summary>
    Task ProbeWritableAsync(string archiveDirectory,CancellationToken cancellationToken);
}
