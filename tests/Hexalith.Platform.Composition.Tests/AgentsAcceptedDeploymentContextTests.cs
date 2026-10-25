using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Shouldly;

namespace Hexalith.Platform.Composition.Tests;

public sealed class AgentsAcceptedDeploymentContextTests
{
    [Fact]
    public void RealDateMustAgreeAcrossAcceptedRegisterPacketAndExactProfileBytes()
    {
        var (json, register, packet) = Inputs();
        var model = AgentsDeploymentProfile.ValidateAcceptedContext(json, register, packet);
        model.IntegrationDate.ShouldBe("2026-11-03"); model.AcceptedContextValidated.ShouldBeTrue();
        Should.Throw<InvalidDataException>(() => AgentsDeploymentProfile.ValidateAcceptedContext(json.Replace("recorder-ref", "changed-recorder", StringComparison.Ordinal), register, packet));
        Should.Throw<InvalidDataException>(() => AgentsDeploymentProfile.ValidateAcceptedContext(json, register, packet.Replace("2026-11-03", "2026-11-04", StringComparison.Ordinal)));
        Should.Throw<InvalidDataException>(() => AgentsDeploymentProfile.ValidateAcceptedContext(json, register, packet.Replace(AgentsDeploymentProfile.ExpectedCohortPlanSha256, new string('c', 64), StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("2026-02-30")]
    [InlineData("2026-1-03")]
    [InlineData("2026-11-03T00:00:00")]
    public void InvalidCalendarDateCannotBeComposed(string date)
    {
        var (json, _, _) = Inputs();
        Should.Throw<InvalidDataException>(() => AgentsDeploymentProfile.Validate(json.Replace("2026-11-03", date, StringComparison.Ordinal)));
    }

    [Fact]
    public void ParsingReferencesAloneDoesNotGrantProductionCompositionAuthority()
    {
        var (json, _, _) = Inputs();
        AgentsDeploymentProfile.Validate(json).AcceptedContextValidated.ShouldBeFalse();
    }

    private static (string Json, string Register, string Packet) Inputs()
    {
        var profile = new Dictionary<string, object?>
        {
            ["host"] = "192.168.1.30", ["integrationDate"] = "2026-11-03", ["partiesBranch"] = "B",
            ["timing"] = new { L = 300, S = 30, O = 600, H = 86400, R = 604800 },
            ["services"] = new { eventStore = "../eventstore", tenants = "../tenants", parties = "../parties", conversations = "../conversations", agents = "../agents" },
            ["replicaCount"] = 3,
        };
        foreach (string name in new[] { "currentTenant", "globalAdministrator", "humanIdentity", "approverEvidence", "replay", "recorder", "worker", "spoolBackend", "failureModel", "protectionEngine", "protectionObserver", "partiesBinding", "conversationsBinding" }) profile[name] = name + "-ref";
        string json = JsonSerializer.Serialize(profile);
        string profileHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        string planHash = AgentsDeploymentProfile.ExpectedCohortPlanSha256; string target = new('a', 40);
        string packet = JsonSerializer.Serialize(new { candidateCommit = target, platformIntegrationDate = "2026-11-03", profileSha256 = profileHash, cohortPlanSha256 = planHash });
        string fields = "\n| `TargetVersionOrCommit` | `" + target + "` |\n| `TargetIntegrationDate` | `2026-11-03` |\n| `AcceptedStatus` | `Committed` |\n";
        string pin = JsonSerializer.Serialize(new { profileSha256 = profileHash, cohortPlanSha256 = planHash });
        string register = "### EXT-HOST-1 — Host\n" + fields + "| `HostQualificationBinding` | `" + pin + "` |\n### EXT-SECRETS-1 — Secrets\n" + fields;
        return (json, register, packet);
    }
}
