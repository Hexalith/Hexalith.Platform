using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hexalith.Platform.Composition;

// Exact json.dumps(sort_keys=True,separators=(',',':'),ensure_ascii=True) binding identity.
// Historical Python signatures keep their existing identity; raw packet bytes remain separate.
internal static class PythonCanonicalJson
{
    internal static byte[] Encode(JsonElement value)
    {
        var output=new StringBuilder();Write(output,value);return Encoding.ASCII.GetBytes(output.ToString());
    }
    private static void Quote(StringBuilder output,string value)
    {
        output.Append('"');
        foreach(char item in value)
        {
            output.Append(item switch { '"'=>"\\\"",'\\'=>"\\\\",'\b'=>"\\b",'\f'=>"\\f",'\n'=>"\\n",'\r'=>"\\r",'\t'=>"\\t",
                <' ' or >='\x7f'=>"\\u"+((int)item).ToString("x4",CultureInfo.InvariantCulture),_=>item.ToString() });
        }
        output.Append('"');
    }
    private static string Number(JsonElement value)
    {
        string raw=value.GetRawText();
        if(!raw.Contains('.') && !raw.Contains('e') && !raw.Contains('E')) return System.Numerics.BigInteger.Parse(raw,CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        double number=value.GetDouble();if(!double.IsFinite(number)) throw new InvalidDataException("Finite JSON binding numbers required.");
        if(number==0) return double.IsNegative(number)?"-0.0":"0.0";
        string shortest=number.ToString("R",CultureInfo.InvariantCulture).ToLowerInvariant();
        bool negative=shortest.StartsWith('-');if(negative) shortest=shortest[1..];
        string[] parts=shortest.Split('e');int exponent=parts.Length==2?int.Parse(parts[1],CultureInfo.InvariantCulture):0;
        int point=parts[0].IndexOf('.');if(point<0) point=parts[0].Length;
        string digits=parts[0].Replace(".","",StringComparison.Ordinal);exponent+=point-1;
        while(digits.Length>1 && digits[0]=='0') { digits=digits[1..];exponent--; }
        digits=digits.TrimEnd('0');
        string result;
        if(exponent < -4 || exponent>=16) result=digits[0]+(digits.Length>1?"."+digits[1..]:"")+"e"+(exponent<0?"-":"+")+Math.Abs(exponent).ToString("D2",CultureInfo.InvariantCulture);
        else if(exponent<0) result="0."+new string('0',-exponent-1)+digits;
        else if(exponent+1>=digits.Length) result=digits+new string('0',exponent+1-digits.Length)+".0";
        else result=digits.Insert(exponent+1,".");
        return (negative?"-":"")+result;
    }
    private static void Write(StringBuilder output,JsonElement value)
    {
        switch(value.ValueKind)
        {
            case JsonValueKind.Object:
                output.Append('{');bool first=true;
                // UTF-32 ordering matches Python's Unicode scalar ordering (including supplementary keys).
                foreach(var property in value.EnumerateObject().OrderBy(p=>string.Join("",p.Name.EnumerateRunes().Select(r=>r.Value.ToString("x8",CultureInfo.InvariantCulture))),StringComparer.Ordinal))
                { if(!first) output.Append(',');first=false;Quote(output,property.Name);output.Append(':');Write(output,property.Value); }
                output.Append('}');break;
            case JsonValueKind.Array:
                output.Append('[');bool start=true;foreach(var item in value.EnumerateArray()) { if(!start) output.Append(',');start=false;Write(output,item); }output.Append(']');break;
            case JsonValueKind.String:Quote(output,value.GetString()!);break;
            case JsonValueKind.Number:output.Append(Number(value));break;
            case JsonValueKind.True:output.Append("true");break;
            case JsonValueKind.False:output.Append("false");break;
            case JsonValueKind.Null:output.Append("null");break;
            default:throw new InvalidDataException("Supported JSON binding required.");
        }
    }
}
