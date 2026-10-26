using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Independent owning-service composition for the shipped recipe receiving producer. All parent/native inputs and both separately credential-bound indexed stores must already be enrolled.</summary>
public static class Fr34OccurrenceAuthorityApplication
{
    /// <summary>Serves actual claim/read-use admission handlers; creates no trust, native registration, store genesis or physical evidence.</summary>
    public static Task<int> RunAsync(string installedReceiver,Fr34OccurrenceRecipe recipe,IndependentDaprAuthority claimAuthority,
        IndependentDaprAuthority readUseAuthority,ICustodyOccurrenceMaterialClient native,TimeProvider? clock=null,
        IndependentDaprAuthority? commandOriginalAuthority=null,CustodyLifecycleClient? custody=null,ICustodyReservedManifestSource? manifests=null)
    {
        var producer=new Fr34OccurrenceProducer(recipe,new InstalledQualificationOriginalStore(claimAuthority),new InstalledQualificationOriginalStore(readUseAuthority),native,custody,manifests);
        var handlers=new List<IIndependentOwnerOperationHandler> { new Fr34OccurrenceOperationHandler(recipe,producer,clock??TimeProvider.System) };
        if(commandOriginalAuthority is not null) handlers.Add(new Fr34CommandAuthorizationHandler(recipe,producer,commandOriginalAuthority,clock??TimeProvider.System));
        return IndependentAuthorityHttpApplication.RunAsync(installedReceiver,ownerHandlers:handlers);
    }
}
