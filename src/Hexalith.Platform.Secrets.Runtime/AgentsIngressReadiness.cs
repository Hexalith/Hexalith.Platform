using System.Text.Json;
using Hexalith.Platform.Custody;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Checks current captured ingress dependencies without issuing or registering a delivery.</summary>
internal sealed class AgentsIngressReadiness(TrustedEnvelopeReplayVerifier verifier,PlatformSecurityDenialRecorder denials,
    TrustedEnvelopeAuthenticator authenticator,PlatformHmacService hmac,IPlatformHmacKeyProvider keys,IPlatformSigningProfileProvider profiles,
    IndependentDaprAuthority replay,AgentsOwningProcessInstallation installation,TimeProvider clock):IHealthCheck,IHostedService
{
    private readonly SemaphoreSlim _slots=new(4,4);
    public async Task StartAsync(CancellationToken token)
    { if(!(await CheckHealthAsync(new(),token).ConfigureAwait(false)).Status.Equals(HealthStatus.Healthy)) throw new InvalidOperationException("DependencyNotAvailable: InstalledCurrentPrivateIngressGraphReplayAuthoritySigningProfileAndKeys"); }
    public Task StopAsync(CancellationToken token)=>Task.CompletedTask;
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,CancellationToken cancellationToken=default)
    {
        // These are deliberately captured only to force construction of both actual private factories.
        _=verifier;_=denials;_=authenticator;_=hmac;
        if(!_slots.Wait(0)) return HealthCheckResult.Unhealthy("DependencyNotAvailable: InstalledPrivateIngressReadinessCapacity");
        using var ownership=new IndependentReceivingOwnership(_slots,cancellationToken);ownership.Start();
        var previous=IndependentReceivingOwnership.Current.Value;IndependentReceivingOwnership.Current.Value=ownership;
        try
        {
            var work=Task.Run(()=>CurrentAsync(ownership.Token),CancellationToken.None);ownership.Track(work);
            return await work.WaitAsync(ownership.Token).ConfigureAwait(false)?HealthCheckResult.Healthy():HealthCheckResult.Unhealthy("DependencyNotAvailable: InstalledCurrentPrivateIngressGraphReplayAuthoritySigningProfileAndKeys");
        }
        catch(Exception) { cancellationToken.ThrowIfCancellationRequested();return HealthCheckResult.Unhealthy("DependencyNotAvailable: InstalledCurrentPrivateIngressGraphReplayAuthoritySigningProfileAndKeys"); }
        finally { IndependentReceivingOwnership.Current.Value=previous; }
    }
    private async Task<bool> CurrentAsync(CancellationToken token)
    {
        if(!await InstalledCredentialIndependence.CheckAsync(installation.SpoolAuthority,installation.Recorder,token).ConfigureAwait(false)
            || !await InstalledCredentialIndependence.CheckAsync(installation.ProtectionEngine,installation.ProtectionObserver,token).ConfigureAwait(false)) return false;
        var profile=profiles.GetCurrent();if(profile is null || !profile.IsValid(clock.GetUtcNow()) || profile.Issuer!=installation.Secrets.Issuer || profile.Audience!=installation.Secrets.Audience) return false;
        var proof=await replay.ReadCurrentReplayReadinessAsync(token).ConfigureAwait(false);token.ThrowIfCancellationRequested();
        if(proof?.Response.Payload.Deserialize<Permission>(CustodyBindingContract.Json)?.Allowed!=true || !await InstalledCredentialIndependence.CheckAsync(installation.SpoolAuthority,installation.Recorder,token).ConfigureAwait(false)
            || !await InstalledCredentialIndependence.CheckAsync(installation.ProtectionEngine,installation.ProtectionObserver,token).ConfigureAwait(false)) return false;
        var metadata=new List<PlatformHmacKeyMetadata>();
        foreach(var scope in new[]{new PlatformHmacScope(installation.ProtectionTarget.TenantId,PlatformHmacPurpose.TrustedEnvelope),new PlatformHmacScope("system",PlatformHmacPurpose.SecurityObservation)})
        {
            var resolved=await keys.ResolveAsync(scope,null,token).ConfigureAwait(false);using var key=resolved.Key;token.ThrowIfCancellationRequested();
            if(resolved.Status!=CustodyStatus.Succeeded || key?.Scope!=scope) return false;metadata.Add(key.Metadata);
        }
        var finalProfile=profiles.GetCurrent();var now=clock.GetUtcNow();token.ThrowIfCancellationRequested();
        return finalProfile==profile && profile.IsValid(now) && IndependentDaprAuthority.Current(proof.Response,now.ToUnixTimeSeconds())
            && metadata.All(value=>value.State==PlatformHmacKeyState.Active && value.NotBefore<=now && value.VerifyUntil>now);
    }

    private sealed record Permission(bool Allowed);
}
