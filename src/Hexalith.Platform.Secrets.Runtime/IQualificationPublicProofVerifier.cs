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
}
