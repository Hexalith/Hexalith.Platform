using Hexalith.Platform.Custody.Dapr;

internal static class ReferenceCredentialTests
{
    internal static Task Run()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "[scoped-fixture]\naws_access_key_id=fixture-access\naws_secret_access_key=fixture-secret\naws_session_token=fixture-session\n");
            var credential = ReferenceBackendCredentials.Load(file, "scoped-fixture").GetCredentials();
            if (!credential.UseToken || credential.Token != "fixture-session") throw new Exception("Exact mounted profile was not consumed.");
            foreach (string invalid in new[] { "[other]\naws_access_key_id=x\naws_secret_access_key=y\n", "[scoped-fixture]\ncredential_source=Ec2InstanceMetadata\n", "[scoped-fixture]\naws_access_key_id=x\naws_access_key_id=y\naws_secret_access_key=z\n" })
            {
                File.WriteAllText(file, invalid);
                try { ReferenceBackendCredentials.Load(file, "scoped-fixture"); } catch (AuthorityUnavailableException) { continue; }
                throw new Exception("Unsupported native credential fallback/profile was accepted.");
            }
        }
        finally { File.Delete(file); }
        return Task.CompletedTask;
    }
}
