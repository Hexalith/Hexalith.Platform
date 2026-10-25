using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Isolated canary content envelope protected by the actual EventStore write pipeline.</summary>
public sealed record Fr34CanaryContent(string Value, ProtectionTarget Target);
