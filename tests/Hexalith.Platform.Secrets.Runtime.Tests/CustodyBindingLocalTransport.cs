using System.Text.Json;

using Dapr.Client;

using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

// Explicit opt-in, test-only actual SDK driver. No production provider or
// authorization seam is registered. The existing 238-test contract is retained.
internal static class CustodyBindingLocalTransport
{
    internal static async Task RunIfConfiguredAsync(CancellationToken cancellationToken)
    {
        string? configPath = Environment.GetEnvironmentVariable("EXT_SECRETS_1_LOCAL_TRANSPORT");
        if (configPath is null) { return; }
        var config = JsonSerializer.Deserialize<Configuration>(File.ReadAllBytes(configPath))!;
        var observations = new List<object>();
        foreach (string name in config.Cases)
        {
            byte[] data;
            string operation;
            var metadata = new Dictionary<string, string>();
            int cancelMilliseconds = 0;
            string? syntheticIdentity = null;
            if (name == "manifest")
            {
                var request = CustodyBindingFixture.Manifest(1000, new string('\u0001', 2048), maximal: true);
                DeletionConsumptionIdentity.Capture(request.Manifest!.Original);
                data = CustodyBindingContract.Encode(request);
                operation = request.Envelope.Operation;
            }
            else if (name == "transition")
            {
                var pending = new byte[RecoverableAnchoredState.MaximumPendingBytes];
                var transition = new AnchoredStateTransition("owner|purpose", 1, 2, CustodyBindingFixture.Digest,
                    CustodyBindingContract.Hash(pending), pending);
                var request = CustodyBindingFixture.Bind(new(CustodyBindingFixture.Envelope("admit-transition", target: transition.TargetDigest), Transition: transition));
                data = CustodyBindingContract.Encode(request);
                operation = request.Envelope.Operation;
            }
            else if (name.StartsWith("data-", StringComparison.Ordinal) || name.StartsWith("api-protobuf-", StringComparison.Ordinal))
            {
                operation = "fixture-send";
                int length = int.Parse(name[(name.StartsWith("data-", StringComparison.Ordinal) ? 5 : 13)..], System.Globalization.CultureInfo.InvariantCulture);
                if (name.StartsWith("api-protobuf-", StringComparison.Ordinal))
                {
                    int target = length;
                    length -= 64;
                    while (CustodyBindingDaprInvoker.ApiRequestSize("synthetic-configured-role", operation, length) < target) { length++; }
                    if (CustodyBindingDaprInvoker.ApiRequestSize("synthetic-configured-role", operation, length) != target) { throw new InvalidOperationException("Cannot construct exact synthetic API boundary."); }
                }
                data = new byte[length];
            }
            else
            {
                string fault = name.StartsWith("status-", StringComparison.Ordinal) ? name[7..] : name;
                string identity = "synthetic-original-" + fault;
                syntheticIdentity = identity;
                metadata["fixture-identity"] = identity;
                data = JsonSerializer.SerializeToUtf8Bytes(new { SyntheticIdentity = identity, KeyDeliveryEnabled = false });
                operation = name.StartsWith("status-", StringComparison.Ordinal) ? "fixture-status" : "fixture-send";
                if (operation == "fixture-send")
                {
                    metadata["fixture-fault"] = fault;
                    if (fault is "cancellation" or "late-duplicate") { cancelMilliseconds = 75; }
                }
            }
            using var handler = new CountingHandler(name);
            using DaprClient client = new DaprClientBuilder().UseGrpcEndpoint(config.Endpoint)
                .UseGrpcChannelOptions(new GrpcChannelOptions
                {
                    HttpHandler = handler,
                    MaxSendMessageSize = 134_217_728,
                    MaxReceiveMessageSize = 1_048_575,
                    MaxRetryAttempts = 1,
                    ServiceConfig = new ServiceConfig(),
                }).Build();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(60_000);
            string result = "ReceivedSyntheticUnverified";
            try
            {
                var binding = new BindingRequest("synthetic-configured-role", operation) { Data = data };
                foreach (var entry in metadata) { binding.Metadata.Add(entry.Key, entry.Value); }
                Task send = client.InvokeBindingAsync(binding, timeout.Token);
                if (cancelMilliseconds > 0)
                {
                    bool admitted = false;
                    for (int attempt = 0; attempt < 1000 && !send.IsCompleted; attempt++)
                    {
                        if (File.ReadAllText(config.SinkObservations).Contains("\"identity\":\"" + syntheticIdentity + "\"", StringComparison.Ordinal))
                        { admitted = true; timeout.Cancel(); break; }
                        await Task.Delay(10, cancellationToken);
                    }
                    if (!admitted) { throw new InvalidOperationException("Cancellation fault requires an independently observed sink admission."); }
                }
                await send;
            }
            catch (Exception exception) when (exception is OperationCanceledException or Grpc.Core.RpcException or HttpRequestException)
            {
                result = "Unknown:" + exception.GetType().Name;
            }
            observations.Add(new { Name = name, Operation = operation, DataBytes = data.Length,
                DataSha256 = CustodyBindingContract.Hash(data), SdkCalls = handler.Calls,
                Result = result, ConfiguredRetries = 0, MaxSendMessageBytes = 134_217_728,
                MaxReceiveMessageBytes = 1_048_575, CompletionPermission = false });
            // Allow a deliberately late fixture delivery to remain observable
            // before the next independent original. This is no closure proof.
            if (cancelMilliseconds > 0) { await Task.Delay(500, cancellationToken); }
        }
        File.WriteAllBytes(config.Output, JsonSerializer.SerializeToUtf8Bytes(observations, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class CountingHandler(string syntheticCase) : DelegatingHandler(new SocketsHttpHandler())
    {
        internal int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            request.Headers.TryAddWithoutValidation("x-f1-case", syntheticCase);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed record Configuration(string Endpoint, string Output, string SinkObservations, string[] Cases);
}
