using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Private canary command; no general application content or credentials.</summary>
public sealed record PersistFr34Canary(Fr34CanaryContent ProtectedContent);
