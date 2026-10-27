using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

internal static class QualificationObservationStage
{
    internal static readonly string[] Stages=["AfterEffect","AfterRestart","AfterRestore","AfterLookup"];
    internal static string Locator(string id,string stage)=>id+":native-observation-v2:"+stage;
    internal static QualificationNativeStageBoundary RequireNative(QualificationObservedOriginal raw,QualificationObservationStageRequest expected)
    {
        var native=raw.Source["nativeStageBoundary"]?.Deserialize<QualificationNativeStageBoundary>(CustodyBindingContract.Json);
        if(native is null || (expected.Stage=="AfterLookup"?native.Stage is not ("AfterRestore" or "AfterLookup"):native.Stage!=expected.Stage) || string.IsNullOrWhiteSpace(native.SourceIncarnation) || string.IsNullOrWhiteSpace(native.SourceRevision)
            || expected.Stage!="AfterLookup" && (native.ControlOperationId!=expected.ControlOperationId || expected.ControlOriginalSha256 is not null && native.ControlOriginalSha256!=expected.ControlOriginalSha256)
            || expected.Stage!="AfterEffect" && native.ControlOriginalSha256 is not { Length:64 }
            || expected.Stage=="AfterEffect" && (native.ControlOperationId is not null || native.ControlOriginalSha256 is not null))
            throw new InvalidOperationException("External.ExactNativeStageControlIncarnationAndHistoricalObservationProtocol:"+expected.Stage);
        return native;
    }
}
