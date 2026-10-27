using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Custody.Dapr;

internal sealed record NativeArchiveCapacityInstallation(string Protocol,string Directory,long QuotaBytes,long CompletionBytes,int MaximumPending=30000,string? VolumeFenceIdentity=null,ProtectedArchiveAllocationInstallation? ProtectedAllocations=null);
