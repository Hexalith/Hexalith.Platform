using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Explicit independently administered service/store and signing installation. It is not enrollment authority.</summary>
public sealed record IndependentAuthorityReceiverInstallation(string EnrollmentId, string Scope, string TrustRevision,
    string Component, string StateKey, string SigningKeyFile, string PublicKeyPem, string AppChannelTokenReference, IndependentDaprAuthorityBinding? RecorderAuthority = null,
    string? OriginalArchiveDirectory = null, string? ArchiveEnrollmentSha256 = null, long CompletionReserveBytes = 0, IndependentDaprAuthorityBinding? RecoverySpoolAuthority = null,QualificationCohortAttemptBinding? CohortAttempt=null,IndependentArchiveCapacityInstallation? ArchiveCapacity=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] CustodyRegistrationVerifierInstallation? RegistrationVerifier=null);
