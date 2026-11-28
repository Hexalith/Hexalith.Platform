namespace Hexalith.Platform.Composition;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Tenants.Contracts.Commands;

/// <summary>The catalog operations behind the Tenants synthetic-tenant task, resolved before any resource starts.</summary>
/// <param name="CatalogDirectory">The run-owned catalog directory holding the gateway's committed readback.</param>
/// <param name="RootDigest">The generated catalog root the gateway must serve.</param>
/// <param name="Bootstrap">The global-administrator bootstrap the relay submits first.</param>
/// <param name="Create">The synthetic-tenant creation the task submits.</param>
internal sealed record PlatformOperatorTaskBinding(string CatalogDirectory, string RootDigest,
    PlatformOperatorOperation Bootstrap, PlatformOperatorOperation Create)
{
    internal const string ModuleId = "tenants";
    internal const string TaskName = "create-synthetic-tenant";

    /// <summary>Gets every single grant an operator token of this run may request; each token requests only what one call needs.</summary>
    internal IReadOnlyList<PlatformOperatorGrant> Grants =>
    [
        PlatformOperatorGrant.GlobalAdministrator,
        .. new[] { Bootstrap.Permission, Bootstrap.ReadPermission, Create.Permission, Create.ReadPermission }
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(PlatformOperatorGrant.Permission),
    ];

    /// <summary>Gets the domains the relay submits through the bound Tenants workload channel.</summary>
    internal IReadOnlyList<string> BoundDomains => [.. new[] { Bootstrap.Domain, Create.Domain }.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    /// <summary>Resolves both internal commands from the generated catalog, or returns null when either is not declared.</summary>
    internal static PlatformOperatorTaskBinding? Resolve(RouteCatalogEnvelope envelope, string directory, string rootDigest)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        PlatformOperatorOperation? bootstrap = Operation(envelope, declaration => declaration.Execution?.HandlerType == typeof(BootstrapGlobalAdmin).FullName);
        PlatformOperatorOperation? create = Operation(envelope, declaration => declaration.Domain == CreateSyntheticTenant.Domain
            && declaration.MessageType == CreateSyntheticTenant.CommandType);
        return bootstrap is null || create is null ? null : new(directory, rootDigest, bootstrap, create);
    }

    private static PlatformOperatorOperation? Operation(RouteCatalogEnvelope envelope, Func<RouteContractDeclaration, bool> select)
    {
        RouteContractDeclaration[] matches = [.. envelope.Declarations.Where(declaration => declaration.Kind == "command" && select(declaration))];
        if (matches is not [{ PlatformAdmission: { } admission, MessageType: { Length: > 0 } commandType } declaration]
            || admission.Namespace != "platform:" + declaration.Domain || string.IsNullOrWhiteSpace(admission.RequiredHumanPermission)
            || envelope.SchemaVersion == 3 && envelope.Operations?.Any(operation => operation.RouteId == declaration.RouteId && operation.Ingress == "internal") != true)
            return null;
        // EventStore admits an explicit platform status scope only with the namespace's read permission.
        return new(declaration.Domain, commandType, admission.Namespace, admission.RequiredHumanPermission,
            admission.Namespace["platform:".Length..] + ":read");
    }
}
