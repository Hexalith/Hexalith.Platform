namespace Hexalith.Platform.Composition;

/// <summary>One grant a local operator token may carry: the global-administrator role or one exact human permission.</summary>
/// <param name="Scope">The generated-realm client scope that adds only this grant.</param>
/// <param name="ClaimType">The claim the grant adds.</param>
/// <param name="ClaimValue">The exact claim value.</param>
internal sealed record PlatformOperatorGrant(string Scope, string ClaimType, string ClaimValue)
{
    internal const string ScopePrefix = "platform-operator.";
    internal const string RoleClaimType = "role";
    internal const string PermissionClaimType = "eventstore:permission";

    /// <summary>Gets the global-administrator role grant, the cutover's only grant.</summary>
    internal static PlatformOperatorGrant GlobalAdministrator { get; } = new(ScopePrefix + "global-administrator", RoleClaimType, "GlobalAdministrator");

    /// <summary>Gets a value indicating whether this grant is the global-administrator role.</summary>
    internal bool IsGlobalAdministrator => ClaimType == RoleClaimType;

    /// <summary>Creates the grant of one exact catalog-declared human permission.</summary>
    internal static PlatformOperatorGrant Permission(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        string[] parts = permission.Split(':');
        if (parts.Length != 2 || parts.Any(part => part.Length == 0 || part.Any(character => !char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character) && character != '-')))
            throw new InvalidDataException($"Operator permission '{permission}' is not a plain '<domain>:<action>' grant.");
        return new(ScopePrefix + permission.Replace(':', '.'), PermissionClaimType, permission);
    }
}
