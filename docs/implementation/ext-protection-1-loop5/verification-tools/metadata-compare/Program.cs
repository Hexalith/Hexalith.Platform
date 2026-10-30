using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
static string Hex(byte[]? bytes) => Convert.ToHexString(bytes ?? []);
static string Sha(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
static SortedDictionary<string,string> Read(string path)
{
    using var stream = File.OpenRead(path); using var pe = new PEReader(stream); var r=pe.GetMetadataReader();
    string Token(EntityHandle h) => h.IsNil ? "nil" : MetadataTokens.GetToken(h).ToString();
    string Attributes(CustomAttributeHandleCollection attributes) => string.Join(";",attributes.Select(h=> {var a=r.GetCustomAttribute(h);return Token(a.Constructor)+":"+Hex(r.GetBlobBytes(a.Value));}));
    string Constant(ConstantHandle h) {if(h.IsNil)return "nil";var c=r.GetConstant(h);return c.TypeCode+":"+Hex(r.GetBlobBytes(c.Value));}
    string TypeName(TypeDefinitionHandle h) {var t=r.GetTypeDefinition(h);var parent=t.GetDeclaringType();return parent.IsNil?r.GetString(t.Namespace)+"."+r.GetString(t.Name):TypeName(parent)+"+"+r.GetString(t.Name);}
    var result=new SortedDictionary<string,string>(StringComparer.Ordinal);
    foreach(var th in r.TypeDefinitions)
    {
        var t=r.GetTypeDefinition(th);var text=new StringBuilder();text.Append(t.Attributes).Append('|').Append(Token(t.BaseType)).Append('|').Append(Attributes(t.GetCustomAttributes()));
        foreach(var fh in t.GetFields()) {var f=r.GetFieldDefinition(fh);text.Append("\nF:").Append(r.GetString(f.Name)).Append('|').Append(f.Attributes).Append('|').Append(Hex(r.GetBlobBytes(f.Signature))).Append('|').Append(Constant(f.GetDefaultValue())).Append('|').Append(Attributes(f.GetCustomAttributes()));}
        foreach(var mh in t.GetMethods())
        {
            var m=r.GetMethodDefinition(mh);text.Append("\nM:").Append(r.GetString(m.Name)).Append('|').Append(m.Attributes).Append('|').Append(m.ImplAttributes).Append('|').Append(Hex(r.GetBlobBytes(m.Signature))).Append('|').Append(Attributes(m.GetCustomAttributes()));
            foreach(var ph in m.GetParameters()){var p=r.GetParameter(ph);text.Append("\nP:").Append(r.GetString(p.Name)).Append('|').Append(p.Attributes).Append('|').Append(p.SequenceNumber).Append('|').Append(Constant(p.GetDefaultValue())).Append('|').Append(Attributes(p.GetCustomAttributes()));}
            if(m.RelativeVirtualAddress!=0){var b=pe.GetMethodBody(m.RelativeVirtualAddress);text.Append("\nB:").Append(b.MaxStack).Append('|').Append(b.LocalVariablesInitialized).Append('|').Append(Token(b.LocalSignature)).Append('|').Append(Hex(b.GetILBytes()));foreach(var e in b.ExceptionRegions)text.Append("\nEH:").Append(e.Kind).Append('|').Append(e.TryOffset).Append('|').Append(e.TryLength).Append('|').Append(e.HandlerOffset).Append('|').Append(e.HandlerLength).Append('|').Append(e.FilterOffset).Append('|').Append(Token(e.CatchType));}
        }
        foreach(var ph in t.GetProperties()){var p=r.GetPropertyDefinition(ph);var a=p.GetAccessors();text.Append("\nPROP:").Append(r.GetString(p.Name)).Append('|').Append(p.Attributes).Append('|').Append(Hex(r.GetBlobBytes(p.Signature))).Append('|').Append(Constant(p.GetDefaultValue())).Append('|').Append(Attributes(p.GetCustomAttributes())).Append('|').Append(Token(a.Getter)).Append('|').Append(Token(a.Setter));}
        foreach(var ih in t.GetInterfaceImplementations()){var i=r.GetInterfaceImplementation(ih);text.Append("\nI:").Append(Token(i.Interface)).Append('|').Append(Attributes(i.GetCustomAttributes()));}
        foreach(var gh in t.GetGenericParameters()){var g=r.GetGenericParameter(gh);text.Append("\nG:").Append(r.GetString(g.Name)).Append('|').Append(g.Attributes).Append('|').Append(g.Index);foreach(var ch in g.GetConstraints()){var c=r.GetGenericParameterConstraint(ch);text.Append('|').Append(Token(c.Type)).Append('|').Append(Attributes(c.GetCustomAttributes()));}}
        result[TypeName(th)]=Sha(text.ToString());
    }
    result["$assembly-custom-attributes"]=Sha(Attributes(r.GetAssemblyDefinition().GetCustomAttributes()));
    result["$assembly-references"]=Sha(string.Join(";",r.AssemblyReferences.Select(h=>{var a=r.GetAssemblyReference(h);return r.GetString(a.Name)+"|"+a.Version+"|"+r.GetString(a.Culture)+"|"+a.Flags+"|"+Hex(r.GetBlobBytes(a.PublicKeyOrToken));})));
    var resources=pe.PEHeaders.CorHeader!.ResourcesDirectory;result["$managed-resources"]=resources.Size==0 ? "empty":Hex(SHA256.HashData(pe.GetSectionData(resources.RelativeVirtualAddress).GetContent(0,resources.Size).AsSpan()));
    return result;
}
var a=Read(args[0]);var b=Read(args[1]);var differences=a.Keys.Union(b.Keys).Where(k=>!a.TryGetValue(k,out var x)||!b.TryGetValue(k,out var y)||x!=y).ToArray();
Console.WriteLine(JsonSerializer.Serialize(new{first=args[0],second=args[1],typeAndAssemblyRecords=a.Count,differences,changedTypeRecords=differences.Select(k=>new{type=k,first=a.GetValueOrDefault(k),second=b.GetValueOrDefault(k)}),firstSemanticSha256=Sha(JsonSerializer.Serialize(a)),secondSemanticSha256=Sha(JsonSerializer.Serialize(b)),excludes="PE layout, MVID, debug directory and PDB; hashes defined types, signatures, methods/locals/exception regions, fields/constants, properties, interfaces/generics, custom attributes, assembly references and managed resources"},new JsonSerializerOptions{WriteIndented=true}));
return differences.Length==0?0:1;
