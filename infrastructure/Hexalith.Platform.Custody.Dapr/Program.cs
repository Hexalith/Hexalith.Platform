using Amazon;
using Amazon.DynamoDBv2;
using Amazon.S3;
using Amazon.KeyManagementService;
using Hexalith.Platform.Custody.Dapr;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Security.Cryptography;
using Hexalith.Platform.Secrets.Runtime;

if (args.Length == 2 && args[0] == "--serve-independent-authority")
    return await IndependentAuthorityHttpApplication.RunAsync(args[1]);

// No startup bootstrap, resource creation, fixture fallback, automatic worker or production DI registration.
if ((args.Length != 2 && args.Length != 3) || args[0] is not ("--check-profile" or "--serve" or "--worker" or "--serve-role" or "--serve-owner-authority"))
{
    Console.Error.WriteLine("Usage: Hexalith.Platform.Custody.Dapr --check-profile|--serve <pre-enrolled-profile.json> OR --worker <profile.json> <signed-command.json>");
    return 2;
}
try
{
    using var startupBudget=new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var profile = CustodyJson.Read<ComponentProfile>(await BoundedRegularFile.ReadAsync(args[1],1024*1024,startupBudget.Token)); profile.Validate();
    if (args[0] == "--check-profile") { Console.WriteLine(profile.ProfileDigest); return 0; }
    _=await BoundedDurableFileOperation.RunAsync(ct=> { StartupGuard.Validate(args[0],profile.SocketPath);ct.ThrowIfCancellationRequested();return Task.FromResult(true); },startupBudget.Token);
    var roleInstallation = args[0] is "--serve-role" or "--serve-owner-authority" && args.Length == 3 ? CustodyJson.Read<CustodyRoleInstallation>(await BoundedRegularFile.ReadAsync(args[2],1024*1024,startupBudget.Token)) : null;
    if (args[0] == "--serve-role" && roleInstallation is null) throw new AuthorityUnavailableException("Independent role installation required.");
    using var signer = ECDsa.Create(); await BoundedRegularFile.UseUtf8SpanAsync(roleInstallation?.SigningKeyFile ?? profile.ComponentSigningKeyPath,16384,value=>{ signer.ImportFromPem(value);return true; },startupBudget.Token);
    using var publicSigner = ECDsa.Create(); publicSigner.ImportFromPem(roleInstallation is null ? profile.ComponentPublicKeyPem :
        profile.Trust.Single(t => t.KeyId == roleInstallation.SignerId && t.Purpose == (roleInstallation.Role switch { "authorizer" => "private-operation", "observer" => "independent-observer", _ => roleInstallation.Role })).PublicKeyPem);
    if (!signer.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(publicSigner.ExportSubjectPublicKeyInfo()))
        throw new AuthorityUnavailableException("Installed response signer mismatch.");
    using var nativeDapr = new Dapr.Client.DaprClientBuilder().Build();
    // The AWS reference remains explicit compatibility source; an absent native profile never selects an installation.
    var credentials = profile.Native is null ? ReferenceBackendCredentials.Load(startupBudget.Token) : null;
    var region = credentials is null ? null : RegionEndpoint.GetBySystemName(profile.Region);
    using var kms = credentials is null ? null : new AmazonKeyManagementServiceClient(credentials, AwsCompatibilityClientConfiguration.Kms(region));
    using var dynamo = credentials is null ? null : new AmazonDynamoDBClient(credentials, AwsCompatibilityClientConfiguration.Dynamo(region));
    using var s3 = credentials is null ? null : new AmazonS3Client(credentials, AwsCompatibilityClientConfiguration.S3(region));
    IAuthorityStore store = profile.Native is { } installed
        ? new NativeDaprAuthorityStore(profile, installed, nativeDapr, TimeProvider.System)
        : new AwsAuthorityStore(profile, dynamo!, s3!, TimeProvider.System);
    using var native = profile.Native is { } installation ? new OpenBaoNativeOperations(profile, installation, store, nativeDapr) : null;
    if (roleInstallation is not null && args[0] == "--serve-role")
    {
        var receiver = new CustodyIndependentRoleReceiver(profile, roleInstallation, store, nativeDapr, signer, TimeProvider.System);
        var roleBuilder = WebApplication.CreateSlimBuilder(); roleBuilder.Logging.ClearProviders();
        roleBuilder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = CustodyBindingContract.CandidateRequestMessageBytes);
        var roleApp = roleBuilder.Build();
        roleApp.MapPost("/custody-lifecycle/{method}", (HttpContext context, string method, CancellationToken token) => receiver.HandleAsync(context, method, token));
        await roleApp.RunAsync(); return 0;
    }
    if (args[0] == "--serve-owner-authority")
    {
        // Role installation has its own enrolled private-operation signer; the independent authority profile is a separate explicit input.
        if (roleInstallation is null || roleInstallation.Role != "authorizer"
            || Environment.GetEnvironmentVariable("PLATFORM_INDEPENDENT_AUTHORITY_INSTALLATION") is not { Length: > 0 } authorityInstallation)
            throw new AuthorityUnavailableException("Native owner authorizer and independent receiving installation required.");
        var originalAuthority = new IndependentDaprAuthority(new DaprSecretsInvoker(nativeDapr), roleInstallation.OriginalAuthority, TimeProvider.System);
        return await IndependentAuthorityHttpApplication.RunAsync(authorityInstallation, ownerHandlers:
            [new CustodyOwnerOperationHandler(profile, store, originalAuthority, roleInstallation.SignerId, signer, TimeProvider.System)]);
    }
    var verifier = new StatementVerifier(profile, TimeProvider.System);
    IEpochOperations epochs = native is not null ? native : new AwsEpochOperations(profile, kms!);
    ICustodyRegistrationPublisher? publisher = profile.RegistrationPublisher is { } publication
        ? new DaprCustodyRegistrationPublisher(new IndependentDaprAuthority(new DaprSecretsInvoker(nativeDapr), publication, TimeProvider.System))
        : null;
    var exchange = new KeyExchange(profile, store, epochs, native is not null ? native : new AwsKeyAccess(profile, kms!), verifier, publisher);
    if (args[0] == "--worker")
    {
        if (args.Length != 3) throw new AuthorityUnavailableException("Signed worker command required.");
        var packet = CustodyJson.Read<WorkerPacket>(await BoundedRegularFile.ReadAsync(args[2],1024*1024,startupBudget.Token));
        await WorkerEntry.ExecuteAsync(new RecoverableLifecycle(profile, store, epochs, verifier, signer), packet, CancellationToken.None, exchange);
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
    if (OperatingSystem.IsLinux()) File.SetUnixFileMode(profile.SocketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite);
    await app.WaitForShutdownAsync(); return 0;
}
catch { Console.Error.WriteLine("Custody component closed: profile, trust, authority or socket initialization failed."); return 1; }
