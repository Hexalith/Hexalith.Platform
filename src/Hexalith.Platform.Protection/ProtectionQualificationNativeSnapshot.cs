using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Actual mixed-root directory snapshot source inputs. The native producer derives the source geometry from the real canary state before engine invocation.</summary>
/// <param name="Entries">Independent complete entry/root/digest/writer enrollments.</param><param name="Parent">Exact original whole-snapshot occurrence.</param>
/// <param name="Source">Native snapshot cell; its cache and all entry proofs commit in the same source transaction.</param>
public sealed record ProtectionQualificationNativeSnapshot(ErasureRootOccurrenceEnrollment[] Entries, PayloadProtectionOccurrenceContext Parent, ProtectionQualificationNativeCell Source);
