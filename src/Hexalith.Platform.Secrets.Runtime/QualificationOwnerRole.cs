using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Current independently enrolled role signer and exact public identity; construction does not enroll it.</summary>
public sealed record QualificationOwnerRole(string Role,string Identity,string Version,string PublicKeyPem);
