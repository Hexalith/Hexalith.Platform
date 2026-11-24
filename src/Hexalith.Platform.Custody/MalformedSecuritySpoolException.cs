namespace Hexalith.Platform.Custody;

/// <summary>Indicates that durable private security spool data failed structural or exact-byte validation.</summary>
public sealed class MalformedSecuritySpoolException : IOException
{
    internal MalformedSecuritySpoolException(string message) : base(message)
    {
    }
}
