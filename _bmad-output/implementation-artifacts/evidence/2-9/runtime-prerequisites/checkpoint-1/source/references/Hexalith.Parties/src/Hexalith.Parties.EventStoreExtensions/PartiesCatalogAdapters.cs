using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Routing;

[assembly: RouteCatalogAdapterSource(typeof(Hexalith.Parties.EventStoreExtensions.PartiesCatalogAdapters))]

namespace Hexalith.Parties.EventStoreExtensions;

/// <summary>Owner-approved local adapters for the complete typed command payloads.</summary>
public static class PartiesCatalogAdapters
{
    /// <summary>Creates the exact adapter inventory without enabling hosted admission.</summary>
    public static IReadOnlyList<IDeclaredIdempotencyIntentAdapter> Create() => [
        Adapter<AddContactChannel>(),
        Adapter<AddIdentifier>(),
        Adapter<CancelPartyErasure>(),
        Adapter<CompletePartyErasure>(),
        Adapter<CreateParty>(),
        Adapter<CreatePartyComposite>(),
        Adapter<DeactivateParty>(),
        Adapter<EraseParty>(),
        Adapter<EstablishHumanActorBinding>(),
        Adapter<LiftRestriction>(),
        Adapter<MarkErasureVerified>(),
        Adapter<MarkPartyEncryptionKeyDeleted>(),
        Adapter<ProvisionAgentParty>(),
        Adapter<ReactivateParty>(),
        Adapter<RebindHumanActorBinding>(),
        Adapter<RecordConsent>(),
        Adapter<RemoveContactChannel>(),
        Adapter<RemoveIdentifier>(),
        Adapter<RestrictProcessing>(),
        Adapter<RetryErasureVerification>(),
        Adapter<RevokeConsent>(),
        Adapter<RevokeHumanActorBinding>(),
        Adapter<RotatePartyKey>(),
        Adapter<SetIsNaturalPerson>(),
        Adapter<UpdateContactChannel>(),
        Adapter<UpdateOrganizationDetails>(),
        Adapter<UpdatePartyComposite>(),
        Adapter<UpdatePersonDetails>()];

    private static DeclaredIdempotencyIntentAdapter<T> Adapter<T>() => new(
        PartiesCatalogDeclarations.Commands.Single(c => c.Route.Execution!.HandlerType == typeof(T).FullName),
        payload => new Dictionary<string, object?> { ["payload-sha256"] = CanonicalIntentPayload.Sha256(payload) });
}

