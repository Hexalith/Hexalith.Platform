using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Shouldly;

namespace Hexalith.Platform.Composition.Tests;

public sealed class PythonCanonicalBindingTests
{
    [Theory]
    [InlineData("{\"é\":\"日本語 😀\\u007f\\b\\t\\n\\f\\r\\u0000\\\\\\\"/\",\"😀\":true,\"\":null,\"z\":[false,{},[]]}")]
    [InlineData("{\"numbers\":[0,-0,0.0,-0.0,1.0,1.2345,1e-4,1e-5,1e15,1e16,1e20,1e-20,1.7976931348623157e308,5e-324,123456789012345678901234567890],\"extension\":{\"b\":1,\"a\":2}}")]
    public async Task CompleteBindingEncodingMatchesActualPythonIncludingUniqueExtensionsAndLegacyAsciiIdentity(string json)
    {
        using var document=JsonDocument.Parse(json);
        var start=new ProcessStartInfo("python3") { RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false };
        start.ArgumentList.Add("-c");start.ArgumentList.Add("import base64,json,sys; v=json.loads(base64.b64decode(sys.argv[1])); print(base64.b64encode(json.dumps(v,sort_keys=True,separators=(',',':'),ensure_ascii=True).encode()).decode())");
        start.ArgumentList.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
        using var process=Process.Start(start)!;string output=await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);string error=await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        process.ExitCode.ShouldBe(0,error);PythonCanonicalJson.Encode(document.RootElement).ShouldBe(Convert.FromBase64String(output.Trim()));
    }
}
