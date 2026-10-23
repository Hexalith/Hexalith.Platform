using Amazon;
using Amazon.DynamoDBv2;
using Amazon.S3;
using Amazon.KeyManagementService;
using Hexalith.Platform.Custody.Dapr;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Security.Cryptography;
using Hexalith.Platform.Secrets.Runtime;

// No startup bootstrap, resource creation, fixture fallback, automatic worker or production DI registration.
if ((args.Length != 2 && args.Length != 3) || args[0] is not ("--check-profile" or "--serve" or "--worker"))
{
    Console.Error.WriteLine("Usage: Hexalith.Platform.Custody.Dapr --check-profile|--serve <pre-enrolled-profile.json> OR --worker <profile.json> <signed-command.json>");
    return 2;
}
try
{
    var profile = ComponentProfile.Load(args[1]); profile.Validate();
    if (args[0] == "--check-profile") { Console.WriteLine(profile.ProfileDigest); return 0; }
    var nativeRegion = RegionEndpoint.GetBySystemName(profile.Region);
    var kmsConfig = new AmazonKeyManagementServiceConfig { RegionEndpoint = nativeRegion, MaxErrorRetry = 0, AllowAutoRedirect = false, DisableLogging = true, LogMetrics = false, IgnoreConfiguredEndpointUrls = true };
    using var kms = new AmazonKeyManagementServiceClient(kmsConfig);
    if (!OperatingSystem.IsLinux()) throw new AuthorityUnavailableException("Linux protected UDS required.");
    string directory = Path.GetDirectoryName(profile.SocketPath)!;
    if (!Directory.Exists(directory) || File.Exists(profile.SocketPath)
        || (File.GetUnixFileMode(directory) & (UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
        throw new AuthorityUnavailableException("Pre-owned private socket directory required; stale sockets are never removed automatically.");
    using var signer = ECDsa.Create(); signer.ImportFromPem(File.ReadAllText(profile.ComponentSigningKeyPath));
    using var publicSigner = ECDsa.Create(); publicSigner.ImportFromPem(profile.ComponentPublicKeyPem);
    if (!signer.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(publicSigner.ExportSubjectPublicKeyInfo()))
        throw new AuthorityUnavailableException("Installed response signer mismatch.");
    var region = RegionEndpoint.GetBySystemName(profile.Region);
    var dynamoConfig = new AmazonDynamoDBConfig { RegionEndpoint = region, MaxErrorRetry = 0, AllowAutoRedirect = false, DisableLogging = true, LogMetrics = false, IgnoreConfiguredEndpointUrls = true };
    var s3Config = new AmazonS3Config { RegionEndpoint = region, MaxErrorRetry = 0, AllowAutoRedirect = false, DisableLogging = true, LogMetrics = false, IgnoreConfiguredEndpointUrls = true };
    using var dynamo = new AmazonDynamoDBClient(dynamoConfig); using var s3 = new AmazonS3Client(s3Config);
    var store = new AwsAuthorityStore(profile, dynamo, s3, TimeProvider.System);
    var verifier = new StatementVerifier(profile, TimeProvider.System);
    var epochs = new AwsEpochOperations(profile, kms);
    var exchange = new KeyExchange(profile, store, epochs, new AwsKeyAccess(profile, kms), verifier);
    if (args[0] == "--worker")
    {
        if (args.Length != 3) throw new AuthorityUnavailableException("Signed worker command required.");
        var packet = CustodyJson.Read<WorkerPacket>(File.ReadAllBytes(args[2]));
        await WorkerEntry.ExecuteAsync(new RecoverableLifecycle(profile, store, epochs, verifier), packet, CancellationToken.None, exchange);
        Console.WriteLine("Worker command retained; only independently verified final publication establishes a terminal original."); return 0;
    }
    var provider = new SemanticProvider(profile, store, verifier, signer, exchange);
    var builder = WebApplication.CreateSlimBuilder(); builder.Logging.ClearProviders();
    builder.WebHost.ConfigureKestrel(options => options.ListenUnixSocket(profile.SocketPath, listen => listen.Protocols = HttpProtocols.Http2));
    builder.Services.AddSingleton(profile); builder.Services.AddSingleton<IAuthorityStore>(store); builder.Services.AddSingleton(provider);
    builder.Services.AddSingleton<BindingService>();
    builder.Services.AddGrpc(options => { options.MaxReceiveMessageSize = CustodyBindingContract.CandidateRequestMessageBytes;
        options.MaxSendMessageSize = CustodyBindingContract.MaximumPageFrameBytes - 1; options.EnableDetailedErrors = false; });
    builder.Services.AddGrpcReflection();
    var app = builder.Build(); app.MapGrpcService<BindingService>(); app.MapGrpcReflectionService();
    await app.StartAsync();
    File.SetUnixFileMode(profile.SocketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite);
    await app.WaitForShutdownAsync(); return 0;
}
catch { Console.Error.WriteLine("Custody component closed: profile, trust, authority or socket initialization failed."); return 1; }
