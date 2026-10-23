namespace Hexalith.Platform.Composition;

using System.Text.RegularExpressions;

/// <summary>Filters native console text before it enters the private diagnostic tail.</summary>
internal sealed partial class PlatformDaprDiagnosticRedactor(IEnumerable<string> secrets)
{
    private readonly string[] _secrets = secrets.Where(value => !string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal).OrderByDescending(value => value.Length).ToArray();

    /// <summary>Retains bounded native messages without process configuration or credentials.</summary>
    internal string Redact(string text)
    {
        // Reject configuration-bearing lines before truncation, including multiline key material.
        if (SensitiveConfiguration().IsMatch(text)) return "[redacted sensitive native log]";
        foreach (string secret in _secrets) text = text.Replace(secret, "[redacted]", StringComparison.Ordinal);
        text = Endpoint().Replace(text, "[redacted endpoint]");
        text = OpaqueCredential().Replace(text, "[redacted opaque value]");
        text = text.Replace('\r', ' ').Replace('\n', ' ');
        return text.Length <= 4096 ? text : text[..4096] + "[truncated message]";
    }

    [GeneratedRegex(@"(?i)(?:\b(?:token|password|passwd|secret|authorization|credential|connectionstring|signingkey)\b|-----|(?:^|\s)--[a-z]|\b(?-i:[A-Z][A-Z0-9_]*=)|\b(?:host|username|user\s+id|pwd)\s*=)")]
    private static partial Regex SensitiveConfiguration();

    [GeneratedRegex(@"(?i)\b[a-z][a-z0-9+.-]*://[^\s\""<>]+")]
    private static partial Regex Endpoint();

    [GeneratedRegex(@"[A-Za-z0-9_+/=-]{40,}(?:\.[A-Za-z0-9_+/=-]+)*")]
    private static partial Regex OpaqueCredential();
}
