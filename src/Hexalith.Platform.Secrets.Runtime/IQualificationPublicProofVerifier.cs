using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Verifies the accepted independently pinned public role proof before journaling or returning it.</summary>
public interface IQualificationPublicProofVerifier
{
    /// <summary>Checks signature, current role identity and complete request binding.</summary>
    bool Verify(string role, JsonObject request, JsonObject envelope, CancellationToken cancellationToken = default);
    /// <summary>Verifies an immutable exact original at its retained proof time after a separate fresh current authorization.</summary>
    bool VerifyOriginal(string role,JsonObject request,JsonObject envelope,long originalTime,CancellationToken cancellationToken=default)=>Verify(role,request,envelope,cancellationToken);
}
