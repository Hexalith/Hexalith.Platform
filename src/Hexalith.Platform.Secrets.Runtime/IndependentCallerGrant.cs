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

/// <summary>Externally provisioned current caller permission, retained with the authority head.</summary>
public sealed record IndependentCallerGrant(string BearerSha256, string Identity, string ScopeReference,
    string Role, string[] Methods, DateTimeOffset ValidUntil, bool Revoked,[property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? CommandProofPublicKeySpkiBase64=null);
