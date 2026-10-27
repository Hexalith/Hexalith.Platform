using Amazon.Runtime;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

// Infrastructure-only credential selection. No default SDK chain, metadata, web identity or process fallback.
internal static class ReferenceBackendCredentials
{
    internal static AWSCredentials Load()=>Load(CancellationToken.None);
    internal static AWSCredentials Load(CancellationToken token)
    {
        if (Environment.GetEnvironmentVariable("AWS_EC2_METADATA_DISABLED") != "true")
            throw new AuthorityUnavailableException("Explicit metadata exclusion required.");
        return Load(Environment.GetEnvironmentVariable("CUSTODY_REFERENCE_CREDENTIAL_FILE"),
            Environment.GetEnvironmentVariable("CUSTODY_REFERENCE_CREDENTIAL_PROFILE"),token);
    }
    internal static AWSCredentials Load(string? path,string? profile)=>Load(path,profile,CancellationToken.None);
    private static AWSCredentials Load(string? path,string? profile,CancellationToken token)
    {
        if (path is null || !Path.IsPathFullyQualified(path) || string.IsNullOrWhiteSpace(profile)
            || profile.IndexOfAny(['[', ']', '\r', '\n']) >= 0)
            throw new AuthorityUnavailableException("Exact scoped credential file/profile required.");
        return BoundedRegularFile.UseUtf8SpanAsync(path,16384,text=>Parse(text,profile),token).GetAwaiter().GetResult();
    }
    private static AWSCredentials Parse(ReadOnlySpan<char> text,string profile)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal); bool section = false;
        while (!text.IsEmpty)
        {
            int newline = text.IndexOf('\n');
            ReadOnlySpan<char> raw = newline < 0 ? text : text[..newline];
            text = newline < 0 ? [] : text[(newline + 1)..];
            ReadOnlySpan<char> line = raw.Trim(); if (line.Length == 0 || line[0] is '#' or ';') continue;
            if (line[0] == '[')
            {
                if (section || !line.SequenceEqual("[" + profile + "]")) throw new AuthorityUnavailableException("Credential file must contain only the exact scoped profile.");
                section = true; continue;
            }
            int equals = line.IndexOf('=');
            if (!section || equals <= 0) throw new AuthorityUnavailableException("Invalid scoped credential profile.");
            // SDK credential values intentionally require immutable strings for their transport lifetime.
            string key = line[..equals].Trim().ToString(), value = line[(equals + 1)..].Trim().ToString();
            if (key is not ("aws_access_key_id" or "aws_secret_access_key" or "aws_session_token") || value.Length == 0 || !values.TryAdd(key, value))
                throw new AuthorityUnavailableException("Unsupported or duplicate scoped credential entry.");
        }
        if (!values.TryGetValue("aws_access_key_id", out var access) || !values.TryGetValue("aws_secret_access_key", out var secret))
            throw new AuthorityUnavailableException("Incomplete scoped credentials.");
        return values.TryGetValue("aws_session_token", out var token) ? new SessionAWSCredentials(access, secret, token) : new BasicAWSCredentials(access, secret);
    }
}
