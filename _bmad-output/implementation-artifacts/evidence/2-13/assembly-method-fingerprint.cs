using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

// A source-equality supplement: retain method signatures/IL and the metadata
// references used by those IL tokens, independently of revision/debug attributes.
using var stream = File.OpenRead(args[0]);
using var pe = new PEReader(stream);
MetadataReader reader = pe.GetMetadataReader();
List<string> rows = [];
foreach (TypeReferenceHandle handle in reader.TypeReferences)
{
    var type = reader.GetTypeReference(handle);
    rows.Add($"type-ref:{MetadataTokens.GetToken(handle)}:{reader.GetString(type.Namespace)}:{reader.GetString(type.Name)}:{MetadataTokens.GetToken(type.ResolutionScope)}");
}
foreach (MemberReferenceHandle handle in reader.MemberReferences)
{
    var member = reader.GetMemberReference(handle);
    rows.Add($"member-ref:{MetadataTokens.GetToken(handle)}:{MetadataTokens.GetToken(member.Parent)}:{reader.GetString(member.Name)}:{Convert.ToHexString(reader.GetBlobBytes(member.Signature))}");
}
foreach (MethodDefinitionHandle handle in reader.MethodDefinitions)
{
    var method = reader.GetMethodDefinition(handle);
    string body = "";
    if (method.RelativeVirtualAddress != 0)
    {
        MethodBodyBlock block = pe.GetMethodBody(method.RelativeVirtualAddress);
        body = $"{block.MaxStack}:{block.LocalVariablesInitialized}:{MetadataTokens.GetToken(block.LocalSignature)}:{Convert.ToHexString(block.GetILBytes()!)}";
        foreach (var region in block.ExceptionRegions)
            body += $":{region.Kind}:{region.TryOffset}:{region.TryLength}:{region.HandlerOffset}:{region.HandlerLength}:{region.FilterOffset}:{MetadataTokens.GetToken(region.CatchType)}";
    }
    rows.Add($"method:{MetadataTokens.GetToken(handle)}:{MetadataTokens.GetToken(method.GetDeclaringType())}:{reader.GetString(method.Name)}:{method.Attributes}:{method.ImplAttributes}:{Convert.ToHexString(reader.GetBlobBytes(method.Signature))}:{body}");
}
byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(rows);
Console.WriteLine(JsonSerializer.Serialize(new { assembly = args[0], methods = reader.MethodDefinitions.Count,
    typeReferences = reader.TypeReferences.Count, memberReferences = reader.MemberReferences.Count,
    methodAndReferenceSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
    scope = "Method signatures/IL/exception regions and referenced metadata; revision/debug attributes intentionally excluded. Use only with complete production-source equality." }));
