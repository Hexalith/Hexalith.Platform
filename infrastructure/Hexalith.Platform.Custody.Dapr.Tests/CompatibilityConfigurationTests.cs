using Amazon;
using Amazon.Runtime;
using Hexalith.Platform.Custody.Dapr;

internal static class CompatibilityConfigurationTests
{
    internal static Task Run()
    {
        // Configuration objects only: no client, credentials, SDK request or remote AWS operation.
        foreach(ClientConfig config in new ClientConfig[]{AwsCompatibilityClientConfiguration.Kms(RegionEndpoint.EUWest1),AwsCompatibilityClientConfiguration.Dynamo(RegionEndpoint.EUWest1),AwsCompatibilityClientConfiguration.S3(RegionEndpoint.EUWest1)})
            if(!config.DisableLogging || config.LogMetrics || config.MaxErrorRetry!=0 || config.AllowAutoRedirect || !config.IgnoreConfiguredEndpointUrls)
                throw new Exception("Compatibility client could log decrypted response material or change the enrolled transport.");
        return Task.CompletedTask;
    }
}
