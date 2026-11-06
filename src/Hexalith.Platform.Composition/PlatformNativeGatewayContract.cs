#if HEXALITH_PLATFORM_CANONICAL_SCOPES
namespace Hexalith.Platform.Composition;

using System.Globalization;
using System.Net.Http.Headers;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Tenants.Contracts.Commands;

/// <summary>Builds native preparation calls from the selected owner declarations and already validated committed gateway readback.</summary>
internal static class PlatformNativeGatewayContract
{
    /// <summary>Grants only this run's public tenant and the three actual management operations with their status reads.</summary>
    internal static CompositionTokenRequest OperatorIdentity(string subject, string tenant, RouteCatalogEnvelope catalog, TimeSpan remaining)
    {
        RouteContractDeclaration[] commands = [Command<BootstrapGlobalAdmin>(catalog), Command<CreateTenant>(catalog), Command<AddUserToTenant>(catalog)];
        string[] reads = commands.Select(command => command.Domain).Distinct(StringComparer.Ordinal).Select(domain =>
            catalog.Declarations.First(declaration => declaration.Kind == "query" && declaration.Domain == domain
                && declaration.PlatformAdmission?.Namespace == "platform:" + domain
                && declaration.PlatformAdmission.RequiredHumanPermission == domain + ":read").PlatformAdmission!.RequiredHumanPermission).ToArray();
        return new(subject, [tenant], commands.Select(command => command.Domain).Distinct(StringComparer.Ordinal).ToArray(),
            ["command:submit", "query:read", .. commands.Select(command => command.PlatformAdmission!.RequiredHumanPermission), .. reads], true, remaining) { Surface = "ui" };
    }

    /// <summary>Resolves one actual owner-declared management command, never its historical storage tenant.</summary>
    internal static RouteContractDeclaration Command<T>(RouteCatalogEnvelope catalog) where T : ICommandContract
    {
        RouteContractDeclaration command = catalog.Declarations.Single(declaration => declaration.Kind == "command"
            && declaration.Domain == T.Domain && declaration.Execution?.HandlerType == typeof(T).FullName);
        if (command.PlatformAdmission is not { } admission || admission.Namespace != "platform:" + command.Domain
            || string.IsNullOrWhiteSpace(admission.RequiredHumanPermission))
            throw new InvalidOperationException("The actual owner management scope is unavailable.");
        return command;
    }

    /// <summary>Copies the exact validated gateway generation onto a preparation client; the receiver still verifies its own live authority.</summary>
    internal static void Bind(HttpRequestHeaders headers, RouteCatalogGenerationReadback gateway, string? platformScope = null)
    {
        if (!gateway.Serving || gateway.Epoch <= 0 || gateway.RootDigest is not { Length: 64 }
            || gateway.ActivationBindingDigest is not { Length: 64 })
            throw new InvalidOperationException("The committed gateway invocation binding is unavailable.");
        headers.Add(RouteCatalogInvocationHandler.RootHeader, gateway.RootDigest);
        headers.Add(RouteCatalogInvocationHandler.EpochHeader, gateway.Epoch.ToString(CultureInfo.InvariantCulture));
        headers.Add(RouteCatalogInvocationHandler.BindingHeader, gateway.ActivationBindingDigest);
        if (platformScope is not null) headers.Add("X-Hexalith-Scope", platformScope);
    }

    /// <summary>Retains the matching human principal beside the independently signed workload/proof on the bound platform request.</summary>
    internal static void BindBootstrap(HttpRequestHeaders headers, RouteCatalogGenerationReadback gateway, string scope, string operatorToken)
    {
        Bind(headers, gateway, scope);
        headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
    }
}
#endif
