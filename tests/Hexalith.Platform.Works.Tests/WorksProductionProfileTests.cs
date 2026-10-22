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
    [InlineData("trustDomain", " platform.example")]
    [InlineData("trustDomain", "platform.example ")]
    [InlineData("namespace", " works-production")]
    [InlineData("namespace", "works-production\t")]
    [InlineData("workloadAuthority", "http://authority.example")]
    [InlineData("workloadAuthority", "https://localhost")]
    [InlineData("workloadAuthority", "https://operator:secret@authority.example")]
    [InlineData("workloadAuthority", "https://operator@authority.example")]
    [InlineData("telemetryEndpoint", "http://collector.example")]
    [InlineData("telemetryEndpoint", "https://exporter:token@collector.example")]
    public void ForeignOrUnprotectedBindingIsRefused(string setting, string value)
    {
        JsonObject candidate = Candidate();
        candidate[setting] = value;
        Should.Throw<InvalidDataException>(() => WorksProductionProfile.Validate(candidate.ToJsonString()));
    }

    /// <summary>Self-invocation cannot use runtimes with absent or caller-supplied attribution.</summary>
    [Theory]
    [InlineData("1.18.3")]
    [InlineData("1.18.6")]
    [InlineData("1.17.99")]
    [InlineData("1.9.0")]
    [InlineData("0.99.99")]
    [InlineData("1.18.7-preview")]
    [InlineData("01.18.7")]
    [InlineData("1.18.07")]
    public void UnsafeDaprRuntimeIsRefused(string runtime)
    {
        JsonObject candidate = Candidate();
        candidate["daprRuntimeVersion"] = runtime;
        Should.Throw<InvalidDataException>(() => WorksProductionProfile.Validate(candidate.ToJsonString()))
            .Message.ShouldContain("1.18.7");
    }

    /// <summary>The floor is numeric, not an exact pin or a string comparison: every stable later runtime is admissible.</summary>
    [Theory]
    [InlineData("1.18.7")]
    [InlineData("1.18.8")]
    [InlineData("1.18.10")]
    [InlineData("1.19.0")]
    [InlineData("1.100.0")]
    [InlineData("2.0.0")]
    public void QualifiedOrLaterDaprRuntimeIsAccepted(string runtime)
    {
        JsonObject candidate = Candidate();
        candidate["daprRuntimeVersion"] = runtime;
        Should.NotThrow(() => WorksProductionProfile.Validate(candidate.ToJsonString()));
    }

    /// <summary>The checked-in schema's runtime pattern and floor agree with the startup validator for every case.</summary>
    [Theory]
    [InlineData("1.18.7", true)]
    [InlineData("1.18.8", true)]
    [InlineData("1.18.10", true)]
    [InlineData("1.19.0", true)]
    [InlineData("1.100.0", true)]
    [InlineData("2.0.0", true)]
    [InlineData("1.18.6", false)]
    [InlineData("1.18.3", false)]
    [InlineData("1.17.99", false)]
    [InlineData("1.9.0", false)]
    [InlineData("0.99.99", false)]
    [InlineData("1.18.7-preview", false)]
    [InlineData("01.18.7", false)]
    [InlineData("1.18.07", false)]
    public void SchemaRuntimeFloorAgreesWithTheValidator(string runtime, bool admissible)
    {
        JsonNode schema = JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "config", "works-production-profile.schema.json")))!
            ["properties"]!["daprRuntimeVersion"]!;
        string pattern = schema["pattern"]!.GetValue<string>();
        string floor = schema["not"]!["pattern"]!.GetValue<string>();
        bool schemaAdmits = System.Text.RegularExpressions.Regex.IsMatch(runtime, pattern)
            && !System.Text.RegularExpressions.Regex.IsMatch(runtime, floor);
        schemaAdmits.ShouldBe(admissible, "schema daprRuntimeVersion " + runtime);
        JsonObject candidate = Candidate();
        candidate["daprRuntimeVersion"] = runtime;
        bool validatorAdmits = true;
        try
        {
            WorksProductionProfile.Validate(candidate.ToJsonString());
        }
        catch (InvalidDataException)
        {
            validatorAdmits = false;
        }

        validatorAdmits.ShouldBe(schemaAdmits, "validator daprRuntimeVersion " + runtime);
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
        ["daprRuntimeVersion"] = "1.18.7", ["trustDomain"] = "platform.example", ["namespace"] = "works-production", ["mtlsEnabled"] = true,
        ["gatewayCaller"] = "eventstore", ["moduleCaller"] = "works", ["operatorCaller"] = "eventstore-admin",
        ["brokerTlsEnabled"] = true, ["brokerPolicySha256"] = new string('a', 64), ["networkPolicySha256"] = new string('b', 64),
        ["workloadAuthority"] = "https://authority.example", ["workloadKeyReference"] = "key-v1",
        ["delegationProvider"] = "delegation-v1", ["secretsProvider"] = "secrets-v1", ["schedulerProvider"] = "scheduler-v1",
        ["stateProvider"] = "state-v1", ["auditProvider"] = "audit-v1", ["backupSet"] = "fleet-v1",
        ["retentionPolicy"] = "retention-v1", ["telemetryEndpoint"] = "https://collector.example",
    };
}
