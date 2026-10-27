using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.Platform.Composition;

/// <summary>Unambiguous accepted sections and JSON. Raw section bytes remain untouched for their accepted digest.</summary>
internal static class AcceptedDependencyRecords
{
    internal static string Section(string text,string id)
    {
        var matches=Regex.Matches(text,@"^### "+Regex.Escape(id)+@"\b[^\n]*\n(.*?)(?=^### |^## |\z)",RegexOptions.Multiline|RegexOptions.Singleline);
        if(matches.Count!=1) throw new InvalidDataException("Exactly one accepted section required: "+id);
        string section=matches[0].Value;
        var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(Match row in Regex.Matches(section,@"^\| `([^`]+)` \| (.*?) \|\s*$",RegexOptions.Multiline))
            if(!seen.Add(row.Groups[1].Value)) throw new InvalidDataException("Repeated accepted commitment: "+row.Groups[1].Value);
        foreach(string required in new[]{"Owner","Repository","RequiredArtifact","TargetVersionOrCommit","TargetIntegrationDate","CompatibilityContractAndVerificationCommand","RequiredEvidenceLevel","AcceptedStatus","ConsumingStories"})
            _=Value(section,required);
        return section;
    }
    internal static string Value(string section,string name)
    {
        var rows=Regex.Matches(section,@"^\| `"+Regex.Escape(name)+@"` \| (.*?) \|\s*$",RegexOptions.Multiline);
        if(rows.Count!=1) throw new InvalidDataException("Exactly one accepted commitment required: "+name);
        return rows[0].Groups[1].Value;
    }
    internal static JsonDocument Binding(string section,string name)
    {
        string value=Value(section,name);
        if(value.Length<3 || value[0]!='`' || value[^1]!='`') throw new InvalidDataException("Accepted JSON binding required: "+name);
        return Json(value[1..^1]);
    }
    internal static JsonDocument Json(string text)
    {
        var document=JsonDocument.Parse(text);
        try { Unique(document.RootElement);return document; }
        catch { document.Dispose();throw; }
    }
    private static void Unique(JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Object)
        {
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var property in value.EnumerateObject())
            {
                if(!seen.Add(property.Name)) throw new InvalidDataException("Repeated accepted JSON property: "+property.Name);
                Unique(property.Value);
            }
        }
        else if(value.ValueKind==JsonValueKind.Array) foreach(var child in value.EnumerateArray()) Unique(child);
    }
}
