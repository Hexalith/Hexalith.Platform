using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>A complete authenticated manifest result and its independent restore-barrier receipt.</summary>
public sealed record CustodyManifestOriginal(DeletionManifestProviderResult Result, string? RestoreBarrierReceiptId);
