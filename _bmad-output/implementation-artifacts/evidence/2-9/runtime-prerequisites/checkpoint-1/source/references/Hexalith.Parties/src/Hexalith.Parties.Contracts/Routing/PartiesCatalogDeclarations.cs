using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Queries;

[assembly: RouteCatalogDeclarationSource(typeof(Hexalith.Parties.Contracts.Routing.PartiesCatalogDeclarations))]

namespace Hexalith.Parties.Contracts.Routing;

/// <summary>Complete owner inventory for the existing domain-service commands, queries and named projections.</summary>
public static class PartiesCatalogDeclarations
{
    /// <summary>Owner schemas and eligibility for existing wires, including UI operations and internal identity/custody calls.</summary>
    public static IReadOnlyList<RouteOperationContract> Operations => Routes.Select(Operation).ToArray();

    /// <summary>Actual owner receiver methods, selected without changing declaration identities.</summary>
    public static IReadOnlyDictionary<string, string> ReceiverMethods => Routes.ToDictionary(route => route.RouteId,
        route => route.Kind switch { "command" => "process", "query" => "query", _ => "project/v2" }, StringComparer.Ordinal);

    private static RouteOperationContract Operation(RouteContractDeclaration route)
    {
        Type request;
        Type result;
        bool internalOperation;
        string eligibility = "UI-only";
        if (route.Kind == "projection")
        {
            request = typeof(EventStore.Contracts.Projections.ProjectionDispatchRequest);
            result = typeof(EventStore.Contracts.Projections.ProjectionDispatchResponse);
            internalOperation = true;
        }
        else if (route.Kind == "command")
        {
            request = typeof(PartiesCatalogDeclarations).Assembly.GetType(route.Execution!.HandlerType, throwOnError: true)!;
            internalOperation = request == typeof(EstablishHumanActorBinding) || request == typeof(RebindHumanActorBinding)
                || request == typeof(RevokeHumanActorBinding) || request == typeof(ProvisionAgentParty)
                || request == typeof(RotatePartyKey) || request == typeof(CompletePartyErasure)
                || request == typeof(MarkErasureVerified) || request == typeof(MarkPartyEncryptionKeyDeleted)
                || request == typeof(RetryErasureVerification);
            eligibility = request == typeof(EraseParty) ? "confirmation-required"
                : request == typeof(CreatePartyComposite) || request == typeof(UpdatePartyComposite)
                    || request == typeof(DeactivateParty) || request == typeof(ReactivateParty) ? "agent-eligible" : "UI-only";
            result = typeof(SubmitCommandResponse);
        }
        else
        {
            (request, result) = route.MessageType switch
            {
                "PartyDetail" or "GetParty" => (typeof(GetParty), typeof(Models.PartyDetail)),
                "PartyIndex" => (typeof(ListParties), typeof(Models.PagedResult<Models.PartyIndexEntry>)),
                "PartySearch" => (typeof(SearchParties), typeof(Models.PagedResult<Models.PartySearchResult>)),
                "ExportPartyData" => (typeof(ExportPartyData), typeof(Models.PartyDataPortabilityPackage)),
                "GetProcessingRecords" => (typeof(GetProcessingRecords), typeof(Models.ProcessingActivityPage)),
                "GetErasureStatus" => (typeof(GetErasureStatus), typeof(Security.PartyErasureStatusRecord)),
                "GetErasureCertificate" => (typeof(GetErasureCertificate), typeof(Security.ErasureCertificate)),
                _ when route.MessageType == typeof(ResolvePartyIdentity).FullName => (typeof(ResolvePartyIdentity), typeof(Models.PartyIdentityResult)),
                _ when route.MessageType == typeof(ResolveHumanActorBindingAt).FullName => (typeof(ResolveHumanActorBindingAt), typeof(Models.HumanActorBindingResult)),
                _ when route.MessageType == ResolveChatBotPartyAuthority.QueryType => (typeof(ResolveChatBotPartyAuthority), typeof(ChatBotPartyAuthorityResult)),
                _ => throw new InvalidOperationException($"Unknown Parties query {route.RouteId}."),
            };
            internalOperation = route.ProjectionType is null;
            eligibility = request == typeof(GetParty) || request == typeof(ListParties) || request == typeof(SearchParties)
                ? "agent-eligible" : "UI-only";
        }
        var operation = RouteOperationSchema.Create(route, request, result, internalOperation ? "internal" : "public", eligibility, PartiesJsonOptions.Default);
        if (route.Kind == "command" && !internalOperation)
        {
            var schema = System.Text.Json.Nodes.JsonNode.Parse(operation.ResultSchema.GetRawText())!;
            var payload = RouteOperationSchema.Create(route, request, typeof(Models.PartyDetail), "public", eligibility, PartiesJsonOptions.Default);
            schema["properties"]!["resultPayload"] = System.Text.Json.Nodes.JsonNode.Parse(payload.ResultSchema.GetRawText());
            operation = operation with { ResultSchema = System.Text.Json.JsonSerializer.SerializeToElement(schema) };
            operation = operation with { Digest = RouteCatalogCodec.ComputeOperationContractDigest(operation) };
        }
        return operation;
    }
    /// <summary>Explicit local Mutation/24-hour policies; payload values and keys never enter the catalog.</summary>
    public static IReadOnlyList<CommandIntentDeclaration> Commands { get; } = [
        Command<AddContactChannel>(true),
        Command<AddIdentifier>(true),
        Command<CancelPartyErasure>(true),
        Command<CompletePartyErasure>(false),
        Command<CreateParty>(true),
        Command<CreatePartyComposite>(true),
        Command<DeactivateParty>(true),
        Command<EraseParty>(true),
        Command<EstablishHumanActorBinding>(true),
        Command<LiftRestriction>(true),
        Command<MarkErasureVerified>(false),
        Command<MarkPartyEncryptionKeyDeleted>(false),
        Command<ProvisionAgentParty>(false),
        Command<ReactivateParty>(true),
        Command<RebindHumanActorBinding>(true),
        Command<RecordConsent>(true),
        Command<RemoveContactChannel>(true),
        Command<RemoveIdentifier>(true),
        Command<RestrictProcessing>(true),
        Command<RetryErasureVerification>(false),
        Command<RevokeConsent>(true),
        Command<RevokeHumanActorBinding>(true),
        Command<RotatePartyKey>(true),
        Command<SetIsNaturalPerson>(true),
        Command<UpdateContactChannel>(true),
        Command<UpdateOrganizationDetails>(true),
        Command<UpdatePartyComposite>(true),
        Command<UpdatePersonDetails>(true)];

    /// <summary>Existing wire query identities, including internal computed authority queries.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Queries { get; } = [
        Query("detail", "PartyDetail", typeof(GetParty).FullName!, "party-detail"),
        Query("get-party", "GetParty", typeof(GetParty).FullName!, "party-detail"),
        Query("index", "PartyIndex", typeof(ListParties).FullName!, "party-index"),
        Query("search", "PartySearch", typeof(SearchParties).FullName!, "party-index"),
        Query("export", "ExportPartyData", "Hexalith.Parties.Queries.ExportPartyDataQueryHandler", "party-detail"),
        Query("processing-records", "GetProcessingRecords", typeof(GetProcessingRecords).FullName!, "party-detail"),
        Query("erasure-status", "GetErasureStatus", "Hexalith.Parties.Queries.GetErasureStatusQueryHandler", "party-detail"),
        Query("erasure-certificate", "GetErasureCertificate", "Hexalith.Parties.Queries.GetErasureCertificateQueryHandler", "party-detail"),
        Query("identity", typeof(ResolvePartyIdentity).FullName!, typeof(ResolvePartyIdentity).FullName!, null),
        Query("human-binding-at", typeof(ResolveHumanActorBindingAt).FullName!, typeof(ResolveHumanActorBindingAt).FullName!, null),
        Query("chatbot-authority", ResolveChatBotPartyAuthority.QueryType, typeof(ResolveChatBotPartyAuthority).FullName!, null, true)];

    /// <summary>Actual named handlers and all their owned read-model slots.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Projections { get; } = [
        Projection("party-detail", "Hexalith.Parties.Projections.Handlers.PartyDetailSdkProjectionHandler"),
        Projection("party-index", "Hexalith.Parties.Projections.Handlers.PartyIndexSdkProjectionHandler")];

    /// <summary>Complete compiled declaration source.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Routes { get; } = [.. Commands.Select(c => c.Route), .. Queries, .. Projections];

    private static CommandIntentDeclaration Command<T>(bool subject)
    {
        string wire = typeof(T).Name;
        string id = Kebab(wire);
        return new(new("party-" + id, "command", "party", wire, null, "v1", 1, "workload",
            EventStoreOperationIdentifiers.DomainServiceProcess, null, [],
            new(typeof(T).FullName!, "clr-short-name-v1", [wire], "domain-service", null, [])),
            "hexalith:party:" + id + ":intent-v1", "party:" + id, "party:" + id + ":local-policy-v1",
            new("hexalith:party:" + id + ":descriptor-v1", 1,
                subject ? [new("target", "string"), new("payload-sha256", "string"), new("acting-subject", "string")]
                    : [new("target", "string"), new("payload-sha256", "string")]),
            IdempotencyReplayRetentionTier.Mutation, subject);
    }

    private static RouteContractDeclaration Query(string id, string wire, string handler, string? projection, bool delegated = false) =>
        new("party-query-" + id, "query", "party", wire, projection, "v1", null, "workload", EventStoreOperationIdentifiers.DomainServiceQuery,
            null, [], new(handler, "exact", [wire], "domain-service", projection is null ? "computed" : "projection", [], RequiresDelegatedContext: delegated));

    private static RouteContractDeclaration Projection(string name, string handler) => new(name + "-projection", "projection", "party",
        null, name, "v1", null, "workload", EventStoreOperationIdentifiers.DomainServiceProject, null, [],
        new(handler, "exact", [name], "domain-service", null, [name]));

    private static string Kebab(string value) => string.Concat(value.Select((character, index) =>
        char.IsUpper(character) && index > 0 ? "-" + char.ToLowerInvariant(character) : char.ToLowerInvariant(character).ToString()));
}

