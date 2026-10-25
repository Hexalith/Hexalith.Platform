using Hexalith.Platform.Custody;
namespace Hexalith.Platform.Protection;
/// <summary>Independent persisted observation of the currently owned opened buffer.</summary>
public interface IFr34UnsealMeasurement
{
    /// <summary>Records exact native evidence; exceptions and cancellation confer no buffer ownership.</summary>
    Task<Fr34MeasuredCanaryOriginal?> RecordUnsealAsync(Fr34CanaryReference reference,ReadOnlyMemory<byte> opened,CancellationToken token);
}
