using Aspire.Hosting;
using Shouldly;

namespace Hexalith.Platform.Composition.Tests;

public sealed class AcceptedLaunchBoundaryTests
{
    [Theory]
    [InlineData("source")][InlineData("project")][InlineData("dependency")][InlineData("unlisted")][InlineData("replacement")][InlineData("kernel")][InlineData("interpreter")][InlineData("dynamic-loader")][InlineData("nonexecutable")][InlineData("valid")]
    [InlineData("invalid-null-only")][InlineData("invalid-entry")][InlineData("invalid-nonexec")][InlineData("invalid-bss-entry")]
    [InlineData("invalid-truncated")][InlineData("invalid-offset")][InlineData("invalid-filesz")][InlineData("invalid-memsz")]
    [InlineData("invalid-overflow")][InlineData("invalid-align")][InlineData("invalid-align-offset")][InlineData("invalid-overlap")]
    public void ActualAcceptedAspireCompositionChecksCompleteImmutableSiblingBytesBeforeAddingAnyResource(string mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        string root=Path.Combine(Path.GetTempPath(),"accepted-launch-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            string service=Path.Combine(root,"agents");Directory.CreateDirectory(service);
            foreach(string name in new[]{"Program.cs","app.csproj","dependency.dll"}) File.WriteAllText(Path.Combine(service,name),"exact accepted fixture "+name);
            var input=AcceptedLaunchFixture.Enroll(AgentsAcceptedDeploymentContextTests.Inputs(root));
            if(mutation is "interpreter" or "dynamic-loader")
            {
                string executable=Path.Combine(service,"fixture-executable");byte[] bytes=File.ReadAllBytes(executable);
                if(mutation=="interpreter") File.WriteAllText(executable,"#!/bin/sh\nexit 0\n");else { bytes[64]=3;File.WriteAllBytes(executable,bytes); }
                input=AcceptedLaunchFixture.Enroll(AgentsAcceptedDeploymentContextTests.Inputs(root));
            }
            if(mutation.StartsWith("invalid-",StringComparison.Ordinal))
            {
                string executable=Path.Combine(service,"fixture-executable");byte[] bytes=File.ReadAllBytes(executable);
                void U64(int at,ulong value)=>System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(at),value);
                switch(mutation)
                {
                    case "invalid-null-only":bytes[64]=0;break;
                    case "invalid-entry":U64(24,0);break;
                    case "invalid-nonexec":bytes[68]=4;break;
                    case "invalid-bss-entry":U64(96,120);break;
                    case "invalid-truncated":bytes=bytes[..100];break;
                    case "invalid-offset":U64(72,ulong.MaxValue);break;
                    case "invalid-filesz":U64(96,130);break;
                    case "invalid-memsz":U64(104,128);break;
                    case "invalid-overflow":U64(80,ulong.MaxValue-100);break;
                    case "invalid-align":U64(112,3);break;
                    case "invalid-align-offset":U64(80,0x400001);break;
                    case "invalid-overlap":Array.Resize(ref bytes,185);System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(56),2);bytes.AsSpan(64,56).CopyTo(bytes.AsSpan(120));break;
                }
                File.WriteAllBytes(executable,bytes);input=AcceptedLaunchFixture.Enroll(AgentsAcceptedDeploymentContextTests.Inputs(root));
            }
            var accepted=AgentsDeploymentProfile.ValidateAcceptedContext(input.Json,input.Register,input.Packet);
            if(mutation=="nonexecutable" && OperatingSystem.IsLinux()) File.SetUnixFileMode(Path.Combine(service,"fixture-executable"),UnixFileMode.UserRead|UnixFileMode.UserWrite);
            AcceptedLaunchArtifactLease.KernelProofOverride=(_,_)=>mutation!="kernel";
            if(mutation is "source" or "project" or "dependency") File.AppendAllText(Path.Combine(service,mutation=="source"?"Program.cs":mutation=="project"?"app.csproj":"dependency.dll"),"substituted");
            else if(mutation=="unlisted") File.WriteAllText(Path.Combine(service,"unaccepted.dll"),"unlisted dependency");
            else if(mutation=="replacement")
            {
                bool replace=true;AcceptedLaunchArtifactLease.KernelProofOverride=(_,directory)=>
                {
                    if(replace && directory) { replace=false;Directory.Move(service,service+"-original");Directory.CreateDirectory(service);File.WriteAllText(Path.Combine(service,"replacement"),"different inode"); }
                    return true;
                };
            }
            var builder=DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args=[],ProjectDirectory=root,AssemblyName=typeof(AcceptedLaunchBoundaryTests).Assembly.GetName().Name,DisableDashboard=true,AllowUnsecuredTransport=true });
            builder.Environment.EnvironmentName="Production";using var launchLifetime=AgentsHostComposition.OwnLaunch(builder);builder.AddExecutable("unrelated","/bin/true",root);var before=builder.Resources.ToArray();
            if(mutation=="valid")
            {
                AgentsHostComposition.Compose(builder,accepted,null);builder.Resources.Count.ShouldBe(before.Length+7);
                builder.Resources.Skip(before.Length).All(value=>value is Aspire.Hosting.ApplicationModel.ExecutableResource).ShouldBeTrue();
                var leases=builder.Resources.SelectMany(value=>value.Annotations.OfType<AcceptedLaunchArtifactLease>()).ToArray();leases.Length.ShouldBe(7);leases.All(value=>!value.IsDisposed).ShouldBeTrue();
                using(var app=launchLifetime.Build()) leases.All(value=>!value.IsDisposed).ShouldBeTrue();
                leases.All(value=>value.IsDisposed).ShouldBeTrue();
            }
            else { Should.Throw<IOException>(()=>AgentsHostComposition.Compose(builder,accepted,null));builder.Resources.ShouldBe(before); }
        }
        finally { AcceptedLaunchArtifactLease.KernelProofOverride=null;Directory.Delete(root,true); }
    }
}
