namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using Shouldly;

/// <summary>Current private owner preparation retains one exact generation, including historical .NET 10 controls.</summary>
public sealed class PlatformCurrentOwnerTargetFrameworkTests
{
    [Fact]
    public void OnlyTheExactSinglePreparationFrameworkCanAcquireAuthority()
    {
        foreach (string expected in new[] { "net10.0", "net11.0" })
        {
            using JsonDocument exact = Assets((expected, expected));
            PlatformCurrentOwnerProof.RequirePreparationFramework(exact.RootElement, expected);
            string other = expected == "net10.0" ? "net11.0" : "net10.0";
            foreach ((string Name, string Alias)[] refused in new[]
            {
                Array.Empty<(string, string)>(), [(other, other)], [(expected, other)],
                [(expected, expected), (other, other)], [("net12.0", "net12.0")],
            })
            {
                using JsonDocument invalid = Assets(refused);
                Should.Throw<InvalidDataException>(() => PlatformCurrentOwnerProof.RequirePreparationFramework(invalid.RootElement, expected));
            }
            foreach (string unsupported in new[] { "net9.0", "net12.0", "net11.0+metadata" })
                Should.Throw<InvalidDataException>(() => PlatformCurrentOwnerProof.RequirePreparationFramework(exact.RootElement, unsupported));
        }
    }

    private static JsonDocument Assets(params (string Name, string Alias)[] frameworks)
        => JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            project = new { frameworks = frameworks.ToDictionary(item => item.Name, item => new { targetAlias = item.Alias }) },
        }));
}
