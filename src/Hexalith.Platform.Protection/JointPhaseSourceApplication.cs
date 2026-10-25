using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Independent owning source application; its signing/read credentials never enter the phase command process.</summary>
public static class JointPhaseSourceApplication
{
    /// <summary>Serves only the current authenticated typed-native source publisher. Startup performs no enrollment or qualification.</summary>
    public static Task<int> RunAsync(string independentReceivingInstallation,JointPhaseNativeSourcePublisher publisher,TimeProvider? clock=null)
        =>IndependentAuthorityHttpApplication.RunAsync(independentReceivingInstallation,ownerHandlers:[new JointPhaseSourceOperationHandler(publisher,clock??TimeProvider.System)]);
}
