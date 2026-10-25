using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Public installation bundle, independently enrolled before startup. It contains no signing key or bearer bytes.</summary>
public sealed record QualificationInstallation(string Role, IndependentDaprAuthorityBinding ScopeAuthority,
    IndependentDaprAuthorityBinding OriginalAuthority, IndependentDaprAuthorityBinding Owner,
    IReadOnlyDictionary<string, QualificationInstalledAnchor> PublicAnchors);
