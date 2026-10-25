using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact externally accepted public anchor bytes and identity.</summary>
public sealed record QualificationInstalledAnchor(string Identity, string Version, string PublicKeyPem, string Sha256);
