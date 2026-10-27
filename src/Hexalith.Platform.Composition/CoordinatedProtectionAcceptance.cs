using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.Platform.Composition;

/// <summary>Checks the actual complete coordinated protection acceptance and public raw-byte pins before production host admission.</summary>
internal static class CoordinatedProtectionAcceptance
{
    internal static void Validate(JsonElement packet,string register,string platformTarget,string platformDate,string profilePin,string planPin)
    {
        var protection=packet.GetProperty("protectionBinding");var joint=packet.GetProperty("jointBinding");
        string target=protection.GetProperty("targetCommit").GetString()!,date=protection.GetProperty("integrationDate").GetString()!;
        string config=packet.GetProperty("jointConfigurationSha256").GetString()!,protectionProfile=protection.GetProperty("profileSha256").GetString()!;
        if(!Regex.IsMatch(target,"^[0-9a-f]{40}$") || !DateOnly.TryParseExact(date,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out _)
            || !Regex.IsMatch(config,"^[0-9a-f]{64}$") || !Regex.IsMatch(protectionProfile,"^[0-9a-f]{64}$")
            || packet.GetProperty("protectionIntegrationDate").GetString()!=date || protection.GetProperty("configurationSha256").GetString()!=config
            || protection.GetProperty("planSha256").GetString()!=planPin || joint.GetProperty("platformCommit").GetString()!=platformTarget
            || joint.GetProperty("hostCommit").GetString()!=platformTarget || joint.GetProperty("secretsCommit").GetString()!=platformTarget
            || joint.GetProperty("platformIntegrationDate").GetString()!=platformDate || joint.GetProperty("protectionCommit").GetString()!=target
            || joint.GetProperty("protectionIntegrationDate").GetString()!=date || joint.GetProperty("profileSha256").GetString()!=profilePin
            || joint.GetProperty("protectionProfileSha256").GetString()!=protectionProfile || joint.GetProperty("planSha256").GetString()!=planPin)
            throw new InvalidDataException("Complete coordinated protection target/date/profile/configuration/plan binding required.");
        var installed=packet.GetProperty("installedHashes");
        if(installed.GetProperty("hostProfileSha256").GetString()!=profilePin || installed.GetProperty("protectionProfileSha256").GetString()!=protectionProfile
            || installed.GetProperty("jointConfigurationSha256").GetString()!=config) throw new InvalidDataException("Installed raw pins differ from coordinated binding.");
        string section=Section(register,"EXT-PROTECTION-1");
        if(AcceptedDependencyRecords.Value(section,"TargetVersionOrCommit")!="`"+target+"`"
            || AcceptedDependencyRecords.Value(section,"TargetIntegrationDate")!="`"+date+"`"
            || AcceptedDependencyRecords.Value(section,"AcceptedStatus") is not ("`Committed`" or "`Available`")
            || Hash(Encoding.UTF8.GetBytes(section))!=protection.GetProperty("acceptedSectionSha256").GetString())
            throw new InvalidDataException("Exact accepted protection commitment required.");
        using var accepted=Binding(section,"ProtectionQualificationBinding");
        foreach(string pin in new[]{"targetCommit","integrationDate","profileSha256","configurationSha256","planSha256"})
            if(accepted.RootElement.GetProperty(pin).GetString()!=protection.GetProperty(pin).GetString()) throw new InvalidDataException("Accepted protection binding changed.");
        string jointDigest=CanonicalHash(joint);
        foreach(string record in new[]{"EXT-SECRETS-1","EXT-HOST-1"})
        {
            string platformSection=Section(register,record);using var acceptedJoint=Binding(platformSection,"JointQualificationBinding");
            if(acceptedJoint.RootElement.GetProperty("configurationSha256").GetString()!=config || acceptedJoint.RootElement.GetProperty("bindingSha256").GetString()!=jointDigest
                || acceptedJoint.RootElement.GetProperty("planSha256").GetString()!=planPin) throw new InvalidDataException("Accepted common joint binding changed.");
            using var owner=Binding(platformSection,record=="EXT-HOST-1"?"HostQualificationBinding":"RuntimeQualificationBinding");
            if(owner.RootElement.GetProperty("configurationRawSha256").GetString()!=installed.GetProperty("custodyConfigurationSha256").GetString()
                || owner.RootElement.GetProperty("configurationSha256").GetString()!=joint.GetProperty("configurationSha256").GetString()
                || owner.RootElement.GetProperty("profileSha256").GetString()!=profilePin || owner.RootElement.GetProperty("cohortPlanSha256").GetString()!=planPin)
                throw new InvalidDataException("Accepted owner raw configuration/profile/plan pins changed.");
        }
    }
    private static string Section(string text,string id)=>AcceptedDependencyRecords.Section(text,id);
    private static JsonDocument Binding(string section,string name)=>AcceptedDependencyRecords.Binding(section,name);
    internal static string CanonicalHash(JsonElement value)=>Hash(PythonCanonicalJson.Encode(value));
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
