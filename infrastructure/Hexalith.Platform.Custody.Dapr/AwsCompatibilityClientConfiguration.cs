using Amazon;
using Amazon.Runtime;
using Amazon.KeyManagementService;
using Amazon.DynamoDBv2;
using Amazon.S3;

namespace Hexalith.Platform.Custody.Dapr;

// Retained compatibility lane only. No SDK response body or metric may expose decrypted KEK/root material.
internal static class AwsCompatibilityClientConfiguration
{
    internal static AmazonKeyManagementServiceConfig Kms(RegionEndpoint? region)=>Secure(new AmazonKeyManagementServiceConfig(),region);
    internal static AmazonDynamoDBConfig Dynamo(RegionEndpoint? region)=>Secure(new AmazonDynamoDBConfig(),region);
    internal static AmazonS3Config S3(RegionEndpoint? region)=>Secure(new AmazonS3Config(),region);
    private static T Secure<T>(T config,RegionEndpoint? region) where T:ClientConfig
    {
        config.RegionEndpoint=region;config.DisableLogging=true;config.LogMetrics=false;config.MaxErrorRetry=0;
        config.AllowAutoRedirect=false;config.IgnoreConfiguredEndpointUrls=true;return config;
    }
}
