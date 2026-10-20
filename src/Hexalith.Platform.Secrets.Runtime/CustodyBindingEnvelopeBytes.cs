using System.Globalization;
using System.Text;

using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

internal sealed record OuterEnvelopeV1(CustodyKeyObjectIdentity Identity, string KekVersion, string OpaqueWrappedKeyReference,
    string OuterKeyGeneration, byte[] Nonce, byte[] Ciphertext, byte[] Tag);

// Wire codecs only. No encryption, persistent bare C0, key generation or provider storage is implemented here.
internal static class CustodyBindingEnvelopeBytes
{
    internal static byte[] Inner(CustodyKeyObjectIdentity identity, string kekVersion, byte[] nonce, byte[] ciphertext, byte[] tag)
        => Inner(new WrappedCustodyKey(identity, kekVersion, nonce, ciphertext, tag));
    internal static byte[] Inner(WrappedCustodyKey c0)
    {
        CustodyKeyLifecycleActor.ValidateIdentity(c0.Identity);
        if (!CustodyBindingContract.Text(c0.KekVersion) || c0.Nonce.Length != 12 || c0.Ciphertext.Length != 32 || c0.Tag.Length != 16)
        { throw new ArgumentException("Malformed original C0."); }
        return Utf8("{\"Ciphertext\":" + Quote(Base64(c0.Ciphertext)) + ",\"Identity\":" + Identity(c0.Identity)
            + ",\"KekVersion\":" + Quote(c0.KekVersion) + ",\"Nonce\":" + Quote(Base64(c0.Nonce)) + ",\"Tag\":" + Quote(Base64(c0.Tag)) + "}");
    }

    internal static byte[] OuterAad(OuterEnvelopeV1 c1)
    {
        Validate(c1);
        var i = c1.Identity;
        return PlatformCanonicalBytes.Components(["custody-epoch-outer-v1", i.TenantId, i.ObjectId, i.Purpose.ToString(), i.KeyAlias,
            i.KeyVersion, i.LifecycleVersion, i.StoreTarget, i.ContractVersion, c1.KekVersion, c1.OpaqueWrappedKeyReference, c1.OuterKeyGeneration]);
    }

    internal static byte[] Outer(OuterEnvelopeV1 c1)
    {
        Validate(c1);
        return Utf8("{\"Ciphertext\":" + Quote(Base64(c1.Ciphertext)) + ",\"Identity\":" + Identity(c1.Identity)
            + ",\"KekVersion\":" + Quote(c1.KekVersion) + ",\"Nonce\":" + Quote(Base64(c1.Nonce))
            + ",\"OpaqueWrappedKeyReference\":" + Quote(c1.OpaqueWrappedKeyReference) + ",\"OuterKeyGeneration\":" + Quote(c1.OuterKeyGeneration)
            + ",\"Tag\":" + Quote(Base64(c1.Tag)) + "}");
    }

    private static void Validate(OuterEnvelopeV1 c1)
    {
        CustodyKeyLifecycleActor.ValidateIdentity(c1.Identity);
        if (!CustodyBindingContract.Text(c1.KekVersion) || !CustodyBindingContract.Text(c1.OpaqueWrappedKeyReference)
            || !CustodyBindingContract.Text(c1.OuterKeyGeneration) || c1.Nonce.Length != 12 || c1.Tag.Length != 16 || c1.Ciphertext.Length == 0)
        { throw new ArgumentException("Malformed outer envelope."); }
    }

    private static string Identity(CustodyKeyObjectIdentity i) => "{\"ContractVersion\":" + Quote(i.ContractVersion)
        + ",\"KeyAlias\":" + Quote(i.KeyAlias) + ",\"KeyVersion\":" + Quote(i.KeyVersion) + ",\"LifecycleVersion\":" + Quote(i.LifecycleVersion)
        + ",\"ObjectId\":" + Quote(i.ObjectId) + ",\"Purpose\":" + Quote(i.Purpose.ToString()) + ",\"StoreTarget\":" + Quote(i.StoreTarget)
        + ",\"TenantId\":" + Quote(i.TenantId) + "}";
    private static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Utf8(string text) => new UTF8Encoding(false, true).GetBytes(text);
    private static string Quote(string value)
    {
        _ = Utf8(value); // reject isolated surrogates, preserve combining characters without normalization
        var text = new StringBuilder("\"");
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': text.Append("\\\""); break;
                case '\\': text.Append("\\\\"); break;
                case '\b': text.Append("\\b"); break;
                case '\t': text.Append("\\t"); break;
                case '\n': text.Append("\\n"); break;
                case '\f': text.Append("\\f"); break;
                case '\r': text.Append("\\r"); break;
                default:
                    if (c < 0x20) { text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)); }
                    else { text.Append(c); }
                    break;
            }
        }
        return text.Append('"').ToString();
    }
}
