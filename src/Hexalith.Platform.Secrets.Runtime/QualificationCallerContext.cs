using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Http;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Authenticated workload identity and independently verified scope, never inferred from request content.</summary>
public sealed record QualificationCallerContext(string Identity, string ScopeReference);
