using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Composition.Tests;

// Disposable public bytes and injected kernel proof; does not establish an installed immutable release.
internal static class AcceptedLaunchFixture
{
    internal static JsonObject Listeners()
    {
        var result=new JsonObject();int port=24100;
        foreach(var pair in new[]{("eventstore","eventstore-owner"),("tenants","tenants-owner"),("tenants-api","tenants-api"),("parties","parties-owner"),("conversations","conversations-owner"),("agents","agents-owner"),("agents-ui","agents-ui-host")})
            result[pair.Item1]=new JsonObject { ["protocol"]="AspNetCoreHttpListenerV1",["role"]=pair.Item2,["address"]="127.0.0.1",["port"]=port++,["endpointName"]=pair.Item1+"-http" };
        var contract=new JsonObject {
            ["protocol"]="IndependentlyAcceptedFrontComposerUiHostHttpV1",["entryPoint"]="fixture-executable",
            ["uiComponentIdentity"]="Hexalith.Agents.UI",["frontComposerRegistrationIdentity"]="Hexalith.Agents.UI.Composition.AgentsFrontComposerRegistration",
            ["uiComponentArtifact"]="fixture-ui-component.bin",["uiComponentSha256"]=Hash(UiComponent),
            ["frontComposerRegistrationArtifact"]="fixture-frontcomposer-registration.bin",["frontComposerRegistrationSha256"]=Hash(Registration),
            ["evidenceSha256"]=new string('f',64),["listenerArgument"]="--urls",["listenerEnvironment"]="ASPNETCORE_URLS" };
        result["agents-ui"]!["uiHost"]=new JsonObject { ["artifact"]="fixture-ui-host-contract.json",["sha256"]=Hash(contract.ToJsonString()),["contract"]=contract };
        return result;
    }
    private const string UiComponent="fixture-only existing UI component identity";
    private const string Registration="fixture-only existing FrontComposer registration identity";
    private static string Hash(string value)=>Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    private static byte[] StaticElf()
    {
        // Valid bounded static ELF load map; no fixture process or qualification phase is started.
        byte[] bytes=new byte[129];new byte[]{127,69,76,70,2,1,1}.CopyTo(bytes,0);
        void U16(int at,ushort value)=>System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at),value);
        void U64(int at,ulong value)=>System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(at),value);
        U16(16,2);U16(18,System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture==System.Runtime.InteropServices.Architecture.Arm64?(ushort)183:(ushort)62);
        bytes[20]=1;U64(24,0x400078);U64(32,64);U16(52,64);U16(54,56);U16(56,1);
        bytes[64]=1;bytes[68]=5;U64(80,0x400000);U64(88,0x400000);U64(96,129);U64(104,129);U64(112,4096);
        new byte[]{0x31,0xff,0xb8,0x3c,0,0,0,0x0f,0x05}.CopyTo(bytes,120);return bytes;
    }
    internal static (string Json,string Register,string Packet) Enroll((string Json,string Register,string Packet) input,string? actualUiHostContract=null)
    {
        var profile=JsonNode.Parse(input.Json)!;var packet=JsonNode.Parse(input.Packet)!;var resources=new JsonArray();
        if(profile["listeners"]?["agents-ui"]?["uiHost"] is { } uiHost)
        {
            string uiRoot=profile["services"]!["agents"]!.GetValue<string>();Directory.CreateDirectory(uiRoot);var contract=uiHost["contract"]!;
            File.WriteAllText(Path.Combine(uiRoot,uiHost["artifact"]!.GetValue<string>()),actualUiHostContract??contract.ToJsonString());
            File.WriteAllText(Path.Combine(uiRoot,contract["uiComponentArtifact"]!.GetValue<string>()),UiComponent);
            File.WriteAllText(Path.Combine(uiRoot,contract["frontComposerRegistrationArtifact"]!.GetValue<string>()),Registration);
        }
        foreach(var pair in new[]{("eventstore","eventStore"),("tenants","tenants"),("tenants-api","tenants"),("parties","parties"),("conversations","conversations"),("agents","agents"),("agents-ui","agents")})
        {
            string root=profile["services"]![pair.Item2]!.GetValue<string>();Directory.CreateDirectory(root);
            string entry="fixture-executable";string entryPath=Path.Combine(root,entry);if(!File.Exists(entryPath)) File.WriteAllBytes(entryPath,StaticElf());
            if(OperatingSystem.IsLinux()) File.SetUnixFileMode(entryPath,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
            var files=new JsonArray();
            foreach(string path in Directory.GetFiles(root,"*",SearchOption.AllDirectories).Order(StringComparer.Ordinal)) files.Add(new JsonObject { ["path"]=Path.GetRelativePath(root,path),["bytes"]=new FileInfo(path).Length,["sha256"]=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) });
            using var held=HeldArchiveDirectory.Acquire(root);
            resources.Add(new JsonObject { ["resource"]=pair.Item1,["root"]=root,["directoryIdentity"]=held.Identity,["entryPoint"]=entry,["executionProtocol"]="SelfContainedStaticElfV1",["wholeImageNativeLoaderClosure"]=new JsonObject { ["protocol"]="IndependentlyAcceptedStaticElfKernelClosureV1",["evidenceSha256"]=new string('f',64) },["files"]=files,["listener"]=profile["listeners"]?[pair.Item1]?.DeepClone() });
        }
        var inventory=new JsonObject { ["protocol"]="AcceptedCompleteImmutableLaunchGraphV1",["resources"]=resources };
        packet["launchInventory"]=inventory;
        using var document=System.Text.Json.JsonDocument.Parse(inventory.ToJsonString());
        packet["jointBinding"]!["launchInventorySha256"]=CoordinatedProtectionAcceptance.CanonicalHash(document.RootElement);
        using var joint=System.Text.Json.JsonDocument.Parse(packet["jointBinding"]!.ToJsonString());string hash=CoordinatedProtectionAcceptance.CanonicalHash(joint.RootElement);
        string register=input.Register;
        foreach(string id in new[]{"EXT-HOST-1","EXT-SECRETS-1"})
        {
            string section=AcceptedDependencyRecords.Section(register,id);using var pin=AcceptedDependencyRecords.Binding(section,"JointQualificationBinding");
            var changed=JsonNode.Parse(pin.RootElement.GetRawText())!;changed["bindingSha256"]=hash;
            register=register.Replace(section,section.Replace(pin.RootElement.GetRawText(),changed.ToJsonString(),StringComparison.Ordinal),StringComparison.Ordinal);
        }
        return (input.Json,register,packet.ToJsonString());
    }
}
