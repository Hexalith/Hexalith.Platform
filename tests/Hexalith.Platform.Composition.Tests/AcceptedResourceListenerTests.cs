using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using System.Text.Json.Nodes;
using System.Text.Json;
using Shouldly;

namespace Hexalith.Platform.Composition.Tests;

public sealed class AcceptedResourceListenerTests
{
    [Theory]
    [InlineData("valid")][InlineData("missing")][InlineData("extra")][InlineData("conflict")][InlineData("endpoint-conflict")][InlineData("wrong-role")][InlineData("wrong-address")][InlineData("wrong-port")][InlineData("inventory-substitution")][InlineData("sealed-model-substitution")][InlineData("existing-endpoint-conflict")][InlineData("missing-ui-host")][InlineData("wrong-ui-component")][InlineData("wrong-ui-registration")][InlineData("ui-host-contract-bytes")]
    public async Task ActualAcceptedSevenResourceListenersAreCompleteBoundAndAppliedBeforeAnyResourceRegistration(string mutation)
    {
        string root=Path.Combine(Path.GetTempPath(),"accepted-listener-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            string? actualUiContract=null;
            if(mutation=="ui-host-contract-bytes")
            {
                var changed=AcceptedLaunchFixture.Listeners()["agents-ui"]!["uiHost"]!["contract"]!.DeepClone();changed["listenerArgument"]="--unrelated";actualUiContract=changed.ToJsonString();
            }
            var input=AcceptedLaunchFixture.Enroll(AgentsAcceptedDeploymentContextTests.Inputs(root,listeners=>
            {
                switch(mutation)
                {
                    case "missing":listeners.Remove("agents-ui");break;
                    case "extra":listeners["unknown"]=listeners["agents"]!.DeepClone();break;
                    case "conflict":listeners["agents-ui"]!["port"]=listeners["agents"]!["port"]!.DeepClone();break;
                    case "endpoint-conflict":listeners["agents-ui"]!["endpointName"]=listeners["agents"]!["endpointName"]!.DeepClone();break;
                    case "missing-ui-host":listeners["agents-ui"]!.AsObject().Remove("uiHost");break;
                    case "wrong-ui-component":listeners["agents-ui"]!["uiHost"]!["contract"]!["uiComponentIdentity"]="Unrelated.UI";break;
                    case "wrong-ui-registration":listeners["agents-ui"]!["uiHost"]!["contract"]!["frontComposerRegistrationIdentity"]="Unrelated.Registration";break;
                    case "ui-host-contract-bytes":listeners["agents-ui"]!["uiHost"]!["sha256"]=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(actualUiContract!)));break;
                    case "wrong-role":listeners["tenants-api"]!["role"]="tenants-owner";break;
                    case "wrong-address":listeners["agents"]!["address"]="invalid.invalid";break;
                    case "wrong-port":listeners["agents"]!["port"]=65536;break;
                }
                if(mutation is "wrong-ui-component" or "wrong-ui-registration")
                    listeners["agents-ui"]!["uiHost"]!["sha256"]=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(listeners["agents-ui"]!["uiHost"]!["contract"]!.ToJsonString())));
            }),actualUiContract);
            if(mutation=="inventory-substitution")
            {
                var packet=JsonNode.Parse(input.Packet)!;packet["launchInventory"]!["resources"]![6]!["listener"]!["port"]=24999;
                using var launch=JsonDocument.Parse(packet["launchInventory"]!.ToJsonString());
                packet["jointBinding"]!["launchInventorySha256"]=CoordinatedProtectionAcceptance.CanonicalHash(launch.RootElement);
                using var joint=JsonDocument.Parse(packet["jointBinding"]!.ToJsonString());string jointHash=CoordinatedProtectionAcceptance.CanonicalHash(joint.RootElement);
                string register=input.Register;
                foreach(string id in new[]{"EXT-HOST-1","EXT-SECRETS-1"})
                {
                    string section=AcceptedDependencyRecords.Section(register,id);using var pin=AcceptedDependencyRecords.Binding(section,"JointQualificationBinding");
                    var changed=JsonNode.Parse(pin.RootElement.GetRawText())!;changed["bindingSha256"]=jointHash;
                    register=register.Replace(section,section.Replace(pin.RootElement.GetRawText(),changed.ToJsonString(),StringComparison.Ordinal),StringComparison.Ordinal);
                }
                input=(input.Json,register,packet.ToJsonString());
            }
            var builder=DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args=[],ProjectDirectory=root,AssemblyName=typeof(AcceptedResourceListenerTests).Assembly.GetName().Name,DisableDashboard=true,AllowUnsecuredTransport=true });
            using var launchLifetime=AgentsHostComposition.OwnLaunch(builder);
            builder.Environment.EnvironmentName="Production";var unrelated=builder.AddExecutable("unrelated","/bin/true",root);
            if(mutation=="existing-endpoint-conflict") unrelated.WithHttpEndpoint(port:24100,targetPort:24100,isProxied:false);
            var before=builder.Resources.ToArray();AcceptedLaunchArtifactLease.KernelProofOverride=(_,_)=>true;
            if(mutation!="valid")
            {
                var refusal=Should.Throw<InvalidDataException>(()=>
                {
                    var changed=AgentsDeploymentProfile.ValidateAcceptedContext(input.Json,input.Register,input.Packet);
                    if(mutation=="sealed-model-substitution") changed=changed with { ListenerBindingsJson=changed.ListenerBindingsJson!.Replace("24100","24999",StringComparison.Ordinal) };
                    AgentsHostComposition.Compose(builder,changed,null);
                });
                if(mutation is "missing-ui-host" or "wrong-ui-component" or "wrong-ui-registration") refusal.Message.ShouldBe(AcceptedListenerBindings.UiHostRequirement);
                if(mutation=="ui-host-contract-bytes") refusal.Message.ShouldBe(AcceptedListenerBindings.UiHostRequirement+": actual host contract bytes differ.");
                if(mutation=="inventory-substitution") refusal.Message.ShouldBe(AcceptedListenerBindings.Requirement+": accepted listener substitution for agents-ui");
                builder.Resources.ShouldBe(before);return;
            }
            var accepted=AgentsDeploymentProfile.ValidateAcceptedContext(input.Json,input.Register,input.Packet);
            AgentsHostComposition.Compose(builder,accepted,null);builder.Resources.Count.ShouldBe(before.Length+7);
            var bindings=AcceptedLaunchFixture.Listeners();
            foreach(var resource in builder.Resources.Skip(before.Length))
            {
                var expected=bindings[resource.Name]!;int port=expected["port"]!.GetValue<int>();string url="http://127.0.0.1:"+port;
                var endpoint=resource.Annotations.OfType<EndpointAnnotation>().Single();endpoint.Port.ShouldBe(port);endpoint.TargetPort.ShouldBe(port);endpoint.Name.ShouldBe(expected["endpointName"]!.GetValue<string>());endpoint.IsProxied.ShouldBeFalse();endpoint.TargetHost.ShouldBe("127.0.0.1");
                var environment=new Dictionary<string,object>();
                foreach(var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>()) await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext,resource,environment,TestContext.Current.CancellationToken));
                environment["ASPNETCORE_URLS"].ShouldBe(url);
                var arguments=new List<object>();foreach(var callback in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>()) await callback.Callback(new CommandLineArgsCallbackContext(arguments,TestContext.Current.CancellationToken));
                arguments.ShouldContain("--urls");arguments.ShouldContain(url);
            }
            using var app=launchLifetime.Build();
        }
        finally { AcceptedLaunchArtifactLease.KernelProofOverride=null;Directory.Delete(root,true); }
    }
}
