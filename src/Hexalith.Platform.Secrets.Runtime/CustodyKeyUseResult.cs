using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Value-free delivery status, optionally owning a one-time key response.</summary>
public sealed record CustodyKeyUseResult(string State, CustodyReleasedKey? Key = null, bool HoldersCompleted = false);
