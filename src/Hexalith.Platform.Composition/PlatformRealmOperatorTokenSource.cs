namespace Hexalith.Platform.Composition;

using Hexalith.Builds.Tooling.Runtime;

/// <summary>Supplies the Builds writer-protocol cutover with a realm-signed operator token whose only grant is the global-administrator role.</summary>
/// <remarks>Registered only when the generated realm is active. The cutover never falls back to the runner key while it is registered.</remarks>
/// <param name="realmOperator">The generated realm's operator client.</param>
internal sealed class PlatformRealmOperatorTokenSource(PlatformRealmOperator realmOperator) : ICompositionOperatorTokenSource
{
    /// <inheritdoc />
    public string OperatorName => PlatformRealmOperator.ClientId;

    /// <inheritdoc />
    public Task<string> CreateGlobalAdministratorTokenAsync(CancellationToken cancellationToken) =>
        realmOperator.TokenAsync([PlatformOperatorGrant.GlobalAdministrator], cancellationToken);
}
