namespace Hexalith.Platform.Works.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Composition;
using Shouldly;

/// <summary>Production admission refuses incomplete or foreign provider manifests.</summary>
public sealed class WorksProductionProfileTests
{
    /// <summary>The checked-in schema supplies every required setting for omission coverage.</summary>
    public static IEnumerable<object[]> RequiredSettings()
        => JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "config", "works-production-profile.schema.json")))!
            ["required"]!.AsArray().Select(value => new object[] { value!.GetValue<string>() });

    /// <summary>A complete reference manifest is structurally admissible without granting production authority.</summary>
    [Fact]
    public void CompleteBindingCandidateIsValid()
        => Should.NotThrow(() => WorksProductionProfile.Validate(Candidate().ToJsonString()));

    /// <summary>Every absent binding is a startup refusal.</summary>
    [Theory]
    [MemberData(nameof(RequiredSettings))]
    public void MissingBindingIsRefused(string setting)
    {
        JsonObject candidate = Candidate();
        candidate.Remove(setting);
        Should.Throw<InvalidDataException>(() => WorksProductionProfile.Validate(candidate.ToJsonString()));
    }

    /// <summary>Provider references cannot be empty or whitespace.</summary>
    [Theory]
    [MemberData(nameof(RequiredSettings))]
    public void BlankBindingIsRefused(string setting)
    {
        JsonObject candidate = Candidate();
        candidate[setting] = " ";
        Should.Throw<InvalidDataException>(() => WorksProductionProfile.Validate(candidate.ToJsonString()));
    }

    /// <summary>False security switches, foreign callers and unbound policies cannot be admitted.</summary>
    [Theory]
    [InlineData("gatewayCaller", "other-app")]
    [InlineData("moduleCaller", "other-app")]
    [InlineData("operatorCaller", "other-app")]
    [InlineData("trustDomain", "public")]
    [InlineData("trustDomain", "Public")]
    [InlineData("trustDomain", "LOCALHOST")]
    [InlineData("namespace", "default")]
    [InlineData("namespace", "Default")]
    [InlineData("namespace", "DEVELOPMENT")]
    [InlineData("brokerPolicySha256", "unbound")]
    [InlineData("networkPolicySha256", "unbound")]
    [InlineData("workloadAuthority", "http://authority.example")]
    [InlineData("workloadAuthority", "https://localhost")]
    [InlineData("telemetryEndpoint", "http://collector.example")]
    public void ForeignOrUnprotectedBindingIsRefused(string setting, string value)
    {
        JsonObject candidate = Candidate();
        candidate[setting] = value;
        Should.Throw<InvalidDataException>(() => WorksProductionProfile.Validate(candidate.ToJsonString()));
    }

    /// <summary>Both mandatory transport protections must remain enabled.</summary>
    [Theory]
    [InlineData("mtlsEnabled")]
    [InlineData("brokerTlsEnabled")]
    public void DisabledTransportProtectionIsRefused(string setting)
    {
        JsonObject candidate = Candidate();
        candidate[setting] = false;
        Should.Throw<InvalidDataException>(() => WorksProductionProfile.Validate(candidate.ToJsonString()));
    }

    /// <summary>Unknown and duplicate keys cannot silently shadow production settings.</summary>
    [Fact]
    public void UnknownAndDuplicateSettingsAreRefused()
    {
        JsonObject candidate = Candidate();
        candidate["allowDevelopment"] = true;
        Should.Throw<InvalidDataException>(() => WorksProductionProfile.Validate(candidate.ToJsonString()));
        string valid = Candidate().ToJsonString();
        Should.Throw<InvalidDataException>(() => WorksProductionProfile.Validate(valid[..^1] + ",\"mtlsEnabled\":false}"));
        Should.Throw<InvalidDataException>(() => WorksProductionProfile.Validate("[]"));
        Should.Throw<JsonException>(() => WorksProductionProfile.Validate("{"));
    }

    internal static string Root
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "apphost.cs"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("The Platform checkout is unavailable.");
        }
    }

    internal static JsonObject Candidate() => new()
    {
        ["trustDomain"] = "platform.example", ["namespace"] = "works-production", ["mtlsEnabled"] = true,
        ["gatewayCaller"] = "eventstore", ["moduleCaller"] = "works", ["operatorCaller"] = "eventstore-admin",
        ["brokerTlsEnabled"] = true, ["brokerPolicySha256"] = new string('a', 64), ["networkPolicySha256"] = new string('b', 64),
        ["workloadAuthority"] = "https://authority.example", ["workloadKeyReference"] = "key-v1",
        ["delegationProvider"] = "delegation-v1", ["secretsProvider"] = "secrets-v1", ["schedulerProvider"] = "scheduler-v1",
        ["stateProvider"] = "state-v1", ["auditProvider"] = "audit-v1", ["backupSet"] = "fleet-v1",
        ["retentionPolicy"] = "retention-v1", ["telemetryEndpoint"] = "https://collector.example",
    };
}
