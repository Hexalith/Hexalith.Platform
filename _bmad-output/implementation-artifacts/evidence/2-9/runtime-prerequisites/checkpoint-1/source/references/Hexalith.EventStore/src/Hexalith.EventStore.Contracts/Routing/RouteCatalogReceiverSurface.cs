namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Independently observed receiver fence and the complete actual endpoint inventory.</summary>
/// <param name="FenceVersion">Installed enforcement implementation.</param>
/// <param name="Endpoints">Every actual mapped endpoint, including explicitly denied prefix overlaps.</param>
public sealed record RouteCatalogReceiverSurface(string FenceVersion, IReadOnlyList<RouteCatalogReceiverEndpoint> Endpoints);

/// <summary>Actual route, method and authorization metadata; admitted endpoints are finite concrete operations only.</summary>
/// <param name="Pattern">Actual route pattern.</param>
/// <param name="Methods">Actual allowed HTTP verbs.</param>
/// <param name="Operation">Canonical authenticated operation, if enrolled.</param>
/// <param name="Policies">Actual authorization policy names.</param>
/// <param name="Anonymous">Whether the endpoint allows anonymous execution.</param>
/// <param name="IsPattern">Whether the route contains parameter, catch-all or constraint syntax.</param>
/// <param name="Admitted">Whether the installed fence permits this exact endpoint over Dapr service invocation.</param>
public sealed record RouteCatalogReceiverEndpoint(string Pattern, IReadOnlyList<string> Methods, string? Operation,
    IReadOnlyList<string> Policies, bool Anonymous, bool IsPattern, bool Admitted);
