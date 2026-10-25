using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>One exact enrolled recipient and transport delivery. Retries retain all five fields.</summary>
public sealed record CustodyKeyUse(string KeyUseLeaseId, string DeliveryAttemptId, string RecipientProcessIncarnation,
    string TransportIncarnation, string OriginalRootReference);
