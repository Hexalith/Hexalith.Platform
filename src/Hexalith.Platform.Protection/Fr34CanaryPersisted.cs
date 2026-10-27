using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Exact canary event, whose content value must be encrypted before native persistence.</summary>
public sealed record Fr34CanaryPersisted(Fr34CanaryContent ProtectedContent) : IEventPayload;
