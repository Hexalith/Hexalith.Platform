using Aspire.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Composition;

/// <summary>Owns accepted launch descriptors during composition and failed Build, then transfers them to the built application's disposal scope.</summary>
public sealed class AcceptedLaunchLifetime:IDisposable
{
    private readonly IDistributedApplicationBuilder _builder;
    private readonly AcceptedLaunchLeaseOwner _owner=new();
    private bool _built;
    private bool _disposed;
    internal AcceptedLaunchLifetime(IDistributedApplicationBuilder builder)
    {
        if(builder.Services.Any(value=>value.ServiceType==typeof(AcceptedLaunchLifetime))) throw new InvalidOperationException("One accepted launch lifetime per builder required.");
        _builder=builder;
        builder.Services.AddSingleton(this);
        builder.Services.AddSingleton<AcceptedLaunchLeaseOwner>(_=>_owner);
    }
    internal void Add(AcceptedLaunchArtifactLease lease)
    {
        if(_built || _disposed) { lease.Dispose();throw new InvalidOperationException("Accepted composition lifetime is closed."); }
        _owner.Add(lease);
    }
    /// <summary>Builds without starting resources. Any failure disposes every acquired descriptor, including failures before host options/services resolve.</summary>
    public DistributedApplication Build()
    {
        if(_built || _disposed) throw new InvalidOperationException("Accepted launch lifetime is closed.");
        DistributedApplication? application=null;
        try
        {
            application=_builder.Build();
            // Force factory ownership after a successful Build. Host disposal now
            // owns the exact instance; no hosting-resolution order is assumed.
            if(!ReferenceEquals(application.Services.GetRequiredService<AcceptedLaunchLeaseOwner>(),_owner)) throw new InvalidOperationException("Exact accepted launch owner required.");
            _built=true;
            return application;
        }
        catch { try { application?.Dispose(); }finally { _owner.Dispose();_disposed=true; }throw; }
    }
    /// <summary>Releases composition abandoned before Build; a built application retains ownership until its own disposal.</summary>
    public void Dispose()
    {
        if(_disposed) return;
        _disposed=true;
        if(!_built) _owner.Dispose();
    }
}
