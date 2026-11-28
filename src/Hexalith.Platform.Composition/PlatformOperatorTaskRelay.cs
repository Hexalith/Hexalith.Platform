#if HEXALITH_PLATFORM_CANONICAL_SCOPES
namespace Hexalith.Platform.Composition;

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Identity;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Serves only the Tenants synthetic-tenant task's submit and status calls on loopback, inside the AppHost that holds every
/// upstream secret. The task receives a per-run relay credential, never a realm token, signing key or proof key.
/// </summary>
/// <remarks>
/// The relay bootstraps its operator as global administrator once, then forwards the exact task command to EventStore's
/// bound-workload endpoint through the Tenants sidecar and polls the regular status endpoint. Each operator token carries
/// one permission grant; the creation token also carries the global-administrator role that the Tenants domain verifies.
/// Gateway answers are returned unchanged.
/// </remarks>
/// <param name="plan">The owning run plan.</param>
/// <param name="task">The scheduled environment-creation task and its private invocation path.</param>
/// <param name="binding">The catalog-declared commands the relay submits.</param>
/// <param name="access">The run-owned Tenants transport delegation.</param>
/// <param name="credentials">The realm or runner-key upstream credentials.</param>
/// <param name="upstream">Resolves the allocated upstream endpoints; tests replace the application model.</param>
internal sealed class PlatformOperatorTaskRelay(CompositionRunPlan plan, CompositionStartupTask task, PlatformOperatorTaskBinding binding,
    PlatformTenantBootstrapAccess access, IPlatformOperatorCredentials credentials,
    Func<IServiceProvider, PlatformOperatorUpstream>? upstream = null) : ICompositionRunPreparation, IAsyncDisposable
{
    private const string CommandsPath = "/api/v1/commands";
    private const string StatusPrefix = "/api/v1/commands/status/";
    private const int MaximumBodyBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan RequestBound = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(250);
    private static readonly string[] TaskCommandFields = ["aggregateId", "commandType", "domain", "messageId", "payload", "tenant"];

    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _bootstrapGate = new(1, 1);
    private readonly ConcurrentDictionary<string, byte> _submitted = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Task, byte> _inflight = new();
    private HttpListener? _listener;
    private Task? _loop;
    private byte[]? _expectedAuthorization;
    private IServiceProvider? _services;
    private PlatformOperatorUpstream? _upstream;
    private RouteCatalogGenerationReadback? _readback;
    private string? _operatorSubject;

    /// <summary>Gets the loopback relay origin once prepared.</summary>
    internal Uri? Endpoint { get; private set; }

    /// <summary>Selects the scheduled Tenants environment-creation task that takes this relay's invocation, if the plan has one.</summary>
    internal static CompositionStartupTask? SelectTask(CompositionRunPlan? plan)
    {
        CompositionStartupTask[] tasks = [.. (plan?.StartupTasks ?? []).Where(candidate => candidate.ModuleId == PlatformOperatorTaskBinding.ModuleId
            && candidate.Name == PlatformOperatorTaskBinding.TaskName && candidate.InvocationPath is not null)];
        return tasks.Length > 1
            ? throw new InvalidDataException($"tenants: lifecycle.tasks: operator task '{PlatformOperatorTaskBinding.TaskName}' is scheduled more than once.")
            : tasks.SingleOrDefault();
    }

    /// <inheritdoc />
    public async Task PrepareAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (_listener is not null) throw new InvalidOperationException("The operator relay is already prepared.");
        string path = RequireInvocationPath();
        _services = services;
        string credential = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        _expectedAuthorization = Encoding.ASCII.GetBytes("Bearer " + credential);
        Start();
        await WriteInvocationAsync(path, plan.TenantNamespace, Endpoint!, binding.Create.Scope, credential, cancellationToken).ConfigureAwait(false);
        await Console.Error.WriteLineAsync($"g4: operator relay for '{PlatformOperatorTaskBinding.ModuleId}/{PlatformOperatorTaskBinding.TaskName}' listens on loopback for operator '{credentials.OperatorName}'.").ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener?.Close();
        try
        {
            Task[] pending = [.. new[] { _loop }.OfType<Task>(), .. _inflight.Keys];
            await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(3), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A request that ignores cancellation cannot delay teardown; the run workspace is removed by its owner.
        }

        if (task.InvocationPath is { } path && File.Exists(path)) File.Delete(path);
        _bootstrapGate.Dispose();
        _stop.Dispose();
    }

    /// <summary>Writes the private task invocation atomically with owner-only access. It holds only the per-run relay credential.</summary>
    internal static async Task WriteInvocationAsync(string path, string tenant, Uri relay, string commandTenant, string credential, CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(path)!;
        CompositionWorkspace.CreatePrivateDirectory(directory);
        string temporary = path + ".tmp";
        File.Delete(temporary);
        var options = new FileStreamOptions { Access = FileAccess.Write, Mode = FileMode.CreateNew, Share = FileShare.None, Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        try
        {
            var stream = new FileStream(temporary, options);
            await using (stream.ConfigureAwait(false))
            {
                using var writer = new Utf8JsonWriter(stream);
                writer.WriteStartObject();
                writer.WriteString("syntheticTenantId", tenant);
                writer.WriteString("gatewayUrl", relay.GetLeftPart(UriPartial.Authority));
                writer.WriteString("commandTenant", commandTenant);
                writer.WriteStartObject("headers");
                writer.WriteString("Authorization", "Bearer " + credential);
                writer.WriteEndObject();
                writer.WriteEndObject();
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private string RequireInvocationPath()
    {
        string expected = Path.Combine(Path.GetFullPath(plan.Workspace), CompositionStartupTasks.InvocationDirectoryName);
        if (task.InvocationPath is not { } path || !Path.IsPathFullyQualified(path)
            || !string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), expected, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            || new FileInfo(path).LinkTarget is not null || new DirectoryInfo(expected).LinkTarget is not null)
            throw new InvalidDataException($"tenants: the '{PlatformOperatorTaskBinding.TaskName}' invocation path is outside this run's private task directory.");
        return path;
    }

    private void Start()
    {
        for (int attempt = 0; ; attempt++)
        {
            using (var probe = new TcpListener(IPAddress.Loopback, 0))
            {
                probe.Start();
                Endpoint = new Uri("http://127.0.0.1:" + ((IPEndPoint)probe.LocalEndpoint).Port.ToString(CultureInfo.InvariantCulture) + "/");
            }

            var listener = new HttpListener();
            listener.Prefixes.Add(Endpoint.AbsoluteUri);
            try
            {
                listener.Start();
                _listener = listener;
                break;
            }
            catch (HttpListenerException) when (attempt < 16)
            {
                listener.Close();
            }
        }

        _loop = Task.Run(() => AcceptAsync(_listener, _stop.Token), CancellationToken.None);
    }

    private async Task AcceptAsync(HttpListener listener, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().WaitAsync(stop).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or HttpListenerException or InvalidOperationException)
            {
                return;
            }

            Task handling = HandleAsync(context, stop);
            _inflight.TryAdd(handling, 0);
            _ = handling.ContinueWith(completed => _inflight.TryRemove(completed, out _), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken stop)
    {
        PlatformOperatorRelayResponse response;
        try
        {
            using var bound = CancellationTokenSource.CreateLinkedTokenSource(stop);
            bound.CancelAfter(RequestBound);
            response = await RouteAsync(context.Request, bound.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Support-safe: the failure class and operator only, never tokens, headers or bodies.
            await Console.Error.WriteLineAsync($"g4: operator relay request failed ({exception.GetType().Name}) for operator '{credentials.OperatorName}'.").ConfigureAwait(false);
            response = PlatformOperatorRelayResponse.Empty((int)HttpStatusCode.BadGateway);
        }

        try
        {
            context.Response.StatusCode = response.Status;
            if (response.ContentType is { Length: > 0 } contentType) context.Response.ContentType = contentType;
            context.Response.ContentLength64 = response.Body.Length;
            if (response.Body.Length > 0) await context.Response.OutputStream.WriteAsync(response.Body, stop).ConfigureAwait(false);
            context.Response.Close();
        }
        catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or IOException or OperationCanceledException)
        {
            context.Response.Abort();
        }
    }

    private async Task<PlatformOperatorRelayResponse> RouteAsync(HttpListenerRequest request, CancellationToken cancellationToken)
    {
        if (!IPAddress.IsLoopback(request.RemoteEndPoint.Address)) return PlatformOperatorRelayResponse.Empty((int)HttpStatusCode.Forbidden);
        if (!Authorized(request.Headers.GetValues("Authorization"))) return PlatformOperatorRelayResponse.Empty((int)HttpStatusCode.Unauthorized);
        string path = request.Url!.AbsolutePath;
        if (request.Url.Query.Length > 0) return PlatformOperatorRelayResponse.Empty((int)HttpStatusCode.NotFound);
        if (request.HttpMethod == HttpMethod.Post.Method && path == CommandsPath) return await SubmitAsync(request, cancellationToken).ConfigureAwait(false);
        if (request.HttpMethod == HttpMethod.Get.Method && path.StartsWith(StatusPrefix, StringComparison.Ordinal))
        {
            string id = Uri.UnescapeDataString(path[StatusPrefix.Length..]);
            return id.Length is > 0 and <= 128 && id.All(character => char.IsAsciiLetterOrDigit(character) || character == '-') && _submitted.ContainsKey(id)
                ? await StatusAsync(id, binding.Create, cancellationToken).ConfigureAwait(false)
                : PlatformOperatorRelayResponse.Empty((int)HttpStatusCode.NotFound);
        }

        return PlatformOperatorRelayResponse.Empty((int)HttpStatusCode.NotFound);
    }

    private bool Authorized(string[]? values) =>
        _expectedAuthorization is { } expected && values is [{ } value]
        && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(value), expected);

    private async Task<PlatformOperatorRelayResponse> SubmitAsync(HttpListenerRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength64 > MaximumBodyBytes) return PlatformOperatorRelayResponse.Empty((int)HttpStatusCode.BadRequest);
        byte[] body = new byte[MaximumBodyBytes + 1];
        int length = 0;
        int read;
        while ((read = await request.InputStream.ReadAsync(body.AsMemory(length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            length += read;
            if (length > MaximumBodyBytes) return PlatformOperatorRelayResponse.Empty((int)HttpStatusCode.BadRequest);
        }

        SubmitCommandRequest? command;
        try
        {
            command = TaskCommand(body.AsSpan(0, length));
        }
        catch (JsonException)
        {
            return PlatformOperatorRelayResponse.Empty((int)HttpStatusCode.BadRequest);
        }

        // Only the task's own creation of this environment's synthetic tenant is relayed.
        if (command is null) return PlatformOperatorRelayResponse.Empty((int)HttpStatusCode.Forbidden);
        (string? subject, PlatformOperatorRelayResponse? refusal) = await EnsureOperatorAsync(cancellationToken).ConfigureAwait(false);
        if (refusal is not null) return refusal;
        string token = await credentials.OperatorTokenAsync([PlatformOperatorGrant.GlobalAdministrator, PlatformOperatorGrant.Permission(binding.Create.Permission)], cancellationToken).ConfigureAwait(false);
        if (PlatformRealmOperator.Subject(token) != subject) throw new InvalidOperationException("The operator subject changed after bootstrap.");
        PlatformOperatorRelayResponse response = await SubmitBoundAsync(command, subject!, token, cancellationToken).ConfigureAwait(false);
        if (response.Status == (int)HttpStatusCode.Accepted)
        {
            _submitted.TryAdd(command.MessageId, 0);
            foreach (string id in AcceptedIdentifiers(response.Body)) _submitted.TryAdd(id, 0);
        }

        return response;
    }

    private SubmitCommandRequest? TaskCommand(ReadOnlySpan<byte> body)
    {
        using JsonDocument document = JsonDocument.Parse(body.ToArray());
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).SequenceEqual(TaskCommandFields, StringComparer.Ordinal)
            || Text(root, "messageId") is not { Length: > 0 and <= 128 } messageId
            || Text(root, "tenant") != binding.Create.Scope || Text(root, "domain") != binding.Create.Domain
            || Text(root, "commandType") != binding.Create.CommandType || Text(root, "aggregateId") != plan.TenantNamespace
            || root.GetProperty("payload") is not { ValueKind: JsonValueKind.Object } payload
            || payload.EnumerateObject().Count() != 1 || Text(payload, "tenantId") != plan.TenantNamespace)
            return null;
        return new SubmitCommandRequest(messageId, binding.Create.Scope, binding.Create.Domain, plan.TenantNamespace, binding.Create.CommandType, payload.Clone());

        static string? Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private async Task<(string? Subject, PlatformOperatorRelayResponse? Refusal)> EnsureOperatorAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _operatorSubject) is { } known) return (known, null);
        await _bootstrapGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_operatorSubject is { } bootstrapped) return (bootstrapped, null);
            string token = await credentials.OperatorTokenAsync([PlatformOperatorGrant.Permission(binding.Bootstrap.Permission)], cancellationToken).ConfigureAwait(false);
            string subject = PlatformRealmOperator.Subject(token);
            string messageId = UniqueIdHelper.GenerateSortableUniqueStringId();
            var command = new SubmitCommandRequest(messageId, binding.Bootstrap.Scope, binding.Bootstrap.Domain, TenantIdentity.GlobalAdministratorsAggregateId,
                binding.Bootstrap.CommandType, JsonSerializer.SerializeToElement(new BootstrapGlobalAdmin(subject), EventStorePayloadSerialization.Options), CorrelationId: messageId);
            PlatformOperatorRelayResponse accepted = await SubmitBoundAsync(command, subject, token, cancellationToken).ConfigureAwait(false);
            if (accepted.Status != (int)HttpStatusCode.Accepted)
            {
                await Console.Error.WriteLineAsync($"g4: operator relay bootstrap was refused (HTTP {accepted.Status}) for operator '{credentials.OperatorName}'.").ConfigureAwait(false);
                return (null, accepted);
            }

            while (true)
            {
                PlatformOperatorRelayResponse status = await StatusAsync(messageId, binding.Bootstrap, cancellationToken).ConfigureAwait(false);
                if (status.Status == (int)HttpStatusCode.OK)
                {
                    (int code, bool retryable, string? rejection) = Outcome(status.Body);
                    // A run whose native preparation already bootstrapped this operator keeps that administrator.
                    if (code == (int)CommandStatus.Completed
                        || code == (int)CommandStatus.Rejected && rejection?.Split(',', 2)[0].EndsWith(".GlobalAdminAlreadyBootstrappedRejection", StringComparison.Ordinal) == true)
                        break;
                    if (code >= (int)CommandStatus.Rejected && !retryable)
                        throw new InvalidOperationException("The operator bootstrap did not complete: " + ((CommandStatus)code).ToString() + ".");
                }
                else if (status.Status != (int)HttpStatusCode.NotFound)
                {
                    return (null, status);
                }

                await Task.Delay(PollDelay, cancellationToken).ConfigureAwait(false);
            }

            Volatile.Write(ref _operatorSubject, subject);
            return (subject, null);
        }
        finally
        {
            _bootstrapGate.Release();
        }
    }

    private async Task<PlatformOperatorRelayResponse> SubmitBoundAsync(SubmitCommandRequest command, string principal, string operatorToken, CancellationToken cancellationToken)
    {
        PlatformOperatorUpstream target = Upstream();
        RouteCatalogGenerationReadback readback = await ReadbackAsync(cancellationToken).ConfigureAwait(false);
        string? assertion = await credentials.WorkloadAssertionAsync(_services!.GetRequiredService<IHostEnvironment>(), target.GatewayAppId, command.Domain, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(assertion)) throw new InvalidOperationException("The Tenants workload assertion is unavailable.");
        var bound = new BoundWorkloadCommandRequest(command, principal);
        string proof = BoundWorkloadCommandProof.Issue(bound, access.AppId, access.PrivateKey, DateTimeOffset.UtcNow);
        using var client = Client(target.Sidecar);
        using var message = new HttpRequestMessage(HttpMethod.Post,
            "v1.0/invoke/" + Uri.EscapeDataString(target.GatewayAppId) + "/method/api/v1/commands/bound/workload")
        { Content = JsonContent.Create(bound, options: JsonOptions) };
        PlatformNativeGatewayContract.BindBootstrap(message.Headers, readback, command.Tenant, operatorToken);
        message.Headers.TryAddWithoutValidation(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
        message.Headers.TryAddWithoutValidation(BoundWorkloadCommandProof.HeaderName, proof);
        if (target.SidecarApiToken is { Length: > 0 } apiToken) message.Headers.TryAddWithoutValidation("dapr-api-token", apiToken);
        return await RelayAsync(client, message, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PlatformOperatorRelayResponse> StatusAsync(string messageId, PlatformOperatorOperation operation, CancellationToken cancellationToken)
    {
        string token = await credentials.OperatorTokenAsync([PlatformOperatorGrant.Permission(operation.ReadPermission)], cancellationToken).ConfigureAwait(false);
        PlatformOperatorUpstream target = Upstream();
        RouteCatalogGenerationReadback readback = await ReadbackAsync(cancellationToken).ConfigureAwait(false);
        using var client = Client(target.Gateway);
        using var message = new HttpRequestMessage(HttpMethod.Get, "api/v1/commands/status/" + Uri.EscapeDataString(messageId));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        PlatformNativeGatewayContract.Bind(message.Headers, readback, operation.Scope);
        return await RelayAsync(client, message, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<PlatformOperatorRelayResponse> RelayAsync(HttpClient client, HttpRequestMessage message, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return new((int)response.StatusCode, response.Content.Headers.ContentType?.ToString(), body);
    }

    private static HttpClient Client(Uri origin) =>
        new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false })
        { BaseAddress = origin, Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = MaximumBodyBytes };

    private PlatformOperatorUpstream Upstream() =>
        _upstream ??= (upstream ?? (services => PlatformOperatorUpstream.Resolve(services, access)))(_services ?? throw new InvalidOperationException("The operator relay is not prepared."));

    private async Task<RouteCatalogGenerationReadback> ReadbackAsync(CancellationToken cancellationToken)
    {
        if (_readback is { } cached) return cached;
        var file = new FileInfo(Path.Combine(binding.CatalogDirectory, PlatformOperatorUpstream.GatewayResource + ".committed.json"));
        if (!file.Exists || file.LinkTarget is not null || file.Length is <= 0 or > 1048576)
            throw new InvalidOperationException("The gateway's committed catalog readback is unavailable.");
        RouteCatalogGenerationReadback readback = JsonSerializer.Deserialize<RouteCatalogGenerationReadback>(
            await File.ReadAllBytesAsync(file.FullName, cancellationToken).ConfigureAwait(false))
            ?? throw new InvalidOperationException("The gateway's committed catalog readback is unreadable.");
        if (!readback.Serving || readback.RootDigest != binding.RootDigest)
            throw new InvalidOperationException("The gateway does not serve this run's routing catalog.");
        return _readback = readback;
    }

    private static (int Code, bool Retryable, string? Rejection) Outcome(byte[] body)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        int code = root.TryGetProperty("statusCode", out JsonElement status) && status.ValueKind == JsonValueKind.Number ? status.GetInt32() : -1;
        bool retryable = root.TryGetProperty("retryable", out JsonElement flag) && flag.ValueKind == JsonValueKind.True;
        string? rejection = root.TryGetProperty("rejectionEventType", out JsonElement type) && type.ValueKind == JsonValueKind.String ? type.GetString() : null;
        return (code, retryable, rejection);
    }

    private static IEnumerable<string> AcceptedIdentifiers(byte[] body)
    {
        List<string> identifiers = [];
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            foreach (string name in new[] { "messageId", "correlationId" })
                if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(name, out JsonElement value)
                    && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } identifier)
                    identifiers.Add(identifier);
        }
        catch (JsonException)
        {
            // An unreadable accepted body still admits the submitted message identity.
        }

        return identifiers;
    }
}
#endif
