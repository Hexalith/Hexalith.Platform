using System.Security.Cryptography;
using System.Text;

namespace Hexalith.Platform.Identity;

/// <summary>Private purpose-separated issuer/subject alias digests; no raw login identity enters Party history.</summary>
public static class PlatformLoginAliasDigest
{
    /// <summary>Derives a global alias independent of the Party operation's tenant.</summary>
    public static string Create(string issuer, string subject, string? aliasKeyBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        byte[] key = Convert.FromBase64String(aliasKeyBase64
            ?? throw new InvalidOperationException("Private identity alias custody is unavailable."));
        try
        {
            if (key.Length < 32)
            {
                throw new InvalidOperationException("Private identity alias custody is unavailable.");
            }

            // Length framing avoids issuer/subject delimiter ambiguity.
            string framed = "hexalith-global-login-alias-v1\0" + issuer.Length + ":" + issuer + subject.Length + ":" + subject;
            return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(framed)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
