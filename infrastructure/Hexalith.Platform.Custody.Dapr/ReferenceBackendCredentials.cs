using Amazon.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

// Infrastructure-only credential selection. No default SDK chain, metadata, web identity or process fallback.
internal static class ReferenceBackendCredentials
{
    internal static AWSCredentials Load()
    {
        if (Environment.GetEnvironmentVariable("AWS_EC2_METADATA_DISABLED") != "true")
            throw new AuthorityUnavailableException("Explicit metadata exclusion required.");
        return Load(Environment.GetEnvironmentVariable("CUSTODY_REFERENCE_CREDENTIAL_FILE"),
            Environment.GetEnvironmentVariable("CUSTODY_REFERENCE_CREDENTIAL_PROFILE"));
    }
    internal static AWSCredentials Load(string? path, string? profile)
    {
        if (path is null || !Path.IsPathFullyQualified(path) || string.IsNullOrWhiteSpace(profile)
            || profile.IndexOfAny(['[', ']', '\r', '\n']) >= 0 || !File.Exists(path) || new FileInfo(path).Length is <= 0 or > 16384)
            throw new AuthorityUnavailableException("Exact scoped credential file/profile required.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal); bool section = false;
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim(); if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';')) continue;
            if (line.StartsWith('['))
            {
                if (section || line != "[" + profile + "]") throw new AuthorityUnavailableException("Credential file must contain only the exact scoped profile.");
                section = true; continue;
            }
            int equals = line.IndexOf('=');
            if (!section || equals <= 0) throw new AuthorityUnavailableException("Invalid scoped credential profile.");
            string key = line[..equals].Trim(), value = line[(equals + 1)..].Trim();
            if (key is not ("aws_access_key_id" or "aws_secret_access_key" or "aws_session_token") || value.Length == 0 || !values.TryAdd(key, value))
                throw new AuthorityUnavailableException("Unsupported or duplicate scoped credential entry.");
        }
        if (!values.TryGetValue("aws_access_key_id", out var access) || !values.TryGetValue("aws_secret_access_key", out var secret))
            throw new AuthorityUnavailableException("Incomplete scoped credentials.");
        return values.TryGetValue("aws_session_token", out var token) ? new SessionAWSCredentials(access, secret, token) : new BasicAWSCredentials(access, secret);
    }
}
