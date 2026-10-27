namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Existing independently enrolled physical pool shared by every independent authority head on the archive volume.</summary>
public sealed record IndependentArchiveCapacityInstallation(string Protocol,string Directory,string EnrollmentSha256,long QuotaBytes,int MaximumTickets=30000,string? VolumeFenceIdentity=null,ProtectedArchiveAllocationInstallation? ProtectedAllocations=null);
