using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal sealed record TrustedSigner(string KeyId, string Purpose, string PublicKeyPem);
