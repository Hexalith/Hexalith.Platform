#if HEXALITH_PLATFORM_CANONICAL_SCOPES
namespace Hexalith.Platform.Composition.Tests;

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Configuration;
using CommandStatusResponse = Hexalith.EventStore.Models.CommandStatusResponse;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.Validation;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Tenants.Contracts.Commands;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Shouldly;

/// <summary>Executes published SDK request validators with structural fixture authority; grants no live receiver or runtime qualification.</summary>
internal static class PlatformNativeGatewayContractChecks
{
    /// <summary>Checks actual caller helpers against published peer, JWT, declared platform admission and status boundaries.</summary>
    internal static async Task AssertAsync(string directory, string tenant)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var activation = JsonSerializer.Deserialize<RouteCatalogActivation>(File.ReadAllBytes(Path.Combine(directory, "activation.json")))!;
        var readback = JsonSerializer.Deserialize<RouteCatalogGenerationReadback>(File.ReadAllBytes(Path.Combine(directory, "eventstore.committed.json")))!;
        var catalog = RouteCatalogCodec.Decode(activation.CanonicalCatalog, activation.RootDigest);
        // A structural component snapshot exercises the genuine public SDK validators, not a running/observed authority.
        var snapshot = new CommittedRouteCatalogSnapshot(activation, readback.Epoch, "eventstore",
            new RouteCatalogStateCoordinates("component-store", "component-state", "http://127.0.0.1:3500/"));
        using var legacy = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/v1/commands/bound/workload");
        RouteCatalogPeerBinding.Matches(Headers(legacy.Headers), snapshot).ShouldBeFalse();
        string key = CompositionSigningKey.Create();
        const string subject = "g4-operator-component";
        CompositionTokenRequest identity = PlatformNativeGatewayContract.OperatorIdentity(subject, tenant, catalog, TimeSpan.FromMinutes(1));
        identity.Tenants.ShouldBe([tenant]);
        identity.Permissions.Order().ShouldBe(new[] { "command:submit", "query:read", "global-administrators:bootstrap", "global-administrators:read", "tenants:create", "tenants:add-user", "tenants:read" }.Order());
        string jwt = CompositionTokenFactory.Create(key, identity, DateTimeOffset.UtcNow);
        ClaimsPrincipal principal = await ValidateAsync(jwt, key);
        principal.FindAll("sub").ShouldHaveSingleItem().Value.ShouldBe(subject);
        TenantCanonicalizer.TryCanonicalizeGrants(principal.FindAll("eventstore:tenant").Select(claim => claim.Value), out var grants).ShouldBeTrue();
        grants.ShouldBe([tenant]);
        RouteContractDeclaration[] commands = [PlatformNativeGatewayContract.Command<BootstrapGlobalAdmin>(catalog), PlatformNativeGatewayContract.Command<CreateTenant>(catalog), PlatformNativeGatewayContract.Command<AddUserToTenant>(catalog)];
        foreach (RouteContractDeclaration command in commands)
        {
            string scope = command.PlatformAdmission!.Namespace;
            LegacyTenantStorageScopeAdapter.TryMapPlatform(command, scope, principal, out string storage).ShouldBeTrue();
            storage.ShouldBe(TenantCanonicalizer.ReservedStorageTenant);
            LegacyTenantStorageScopeAdapter.TryMapPlatform(command, "system", principal, out _).ShouldBeFalse();
            LegacyTenantStorageScopeAdapter.TryMapPlatform(command, scope + "-foreign", principal, out _).ShouldBeFalse();
            var missingPermission = new ClaimsPrincipal(new ClaimsIdentity(principal.Claims.Where(claim =>
                claim.Type != "eventstore:permission" || claim.Value != command.PlatformAdmission.RequiredHumanPermission), "component"));
            LegacyTenantStorageScopeAdapter.TryMapPlatform(command, scope, missingPermission, out _).ShouldBeFalse();
            var reservedGrant = new ClaimsPrincipal(new ClaimsIdentity([.. principal.Claims, new("eventstore:tenant", "system")], "component"));
            LegacyTenantStorageScopeAdapter.TryMapPlatform(command, scope, reservedGrant, out _).ShouldBeFalse();
            using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/v1/commands/bound/workload");
            PlatformNativeGatewayContract.BindBootstrap(request.Headers, readback, scope, jwt);
            request.Headers.Authorization.ShouldBe(new AuthenticationHeaderValue("Bearer", jwt));
            request.Headers.GetValues("X-Hexalith-Scope").ShouldBe([scope]);
            request.Headers.Contains("X-Hexalith-Tenant").ShouldBeFalse();
            RouteCatalogPeerBinding.Matches(Headers(request.Headers), snapshot).ShouldBeTrue();
            foreach (string header in new[] { RouteCatalogInvocationHandler.RootHeader, RouteCatalogInvocationHandler.EpochHeader, RouteCatalogInvocationHandler.BindingHeader })
            {
                HeaderDictionary missing = Headers(request.Headers); missing.Remove(header);
                RouteCatalogPeerBinding.Matches(missing, snapshot).ShouldBeFalse();
                HeaderDictionary changed = Headers(request.Headers); changed[header] = "foreign";
                RouteCatalogPeerBinding.Matches(changed, snapshot).ShouldBeFalse();
                HeaderDictionary duplicate = Headers(request.Headers); duplicate.Append(header, duplicate[header].ToString());
                RouteCatalogPeerBinding.Matches(duplicate, snapshot).ShouldBeFalse();
            }
            await AssertStatusAsync(request.Headers, principal, command.Domain, token);
        }
        await AssertBoundBootstrapAsync(catalog, readback, principal, jwt, subject, token);
        using var invalid = new HttpRequestMessage();
        Should.Throw<InvalidOperationException>(() => PlatformNativeGatewayContract.Bind(invalid.Headers, readback with { Epoch = 0 }));
        Should.Throw<InvalidOperationException>(() => PlatformNativeGatewayContract.Bind(invalid.Headers, readback with { Serving = false }));
        // Native identity never inherits management permissions, global role or reserved/platform tenants.
        string nativeJwt = CompositionTokenFactory.Create(key, new("g4-native-tests", [tenant], ["party", "tenants"],
            ["command:submit", "query:read"], false, TimeSpan.FromMinutes(1)) { Surface = "agent" }, DateTimeOffset.UtcNow);
        ClaimsPrincipal native = await ValidateAsync(nativeJwt, key);
        native.FindAll("eventstore:tenant").Select(claim => claim.Value).ShouldBe([tenant]);
        native.FindAll("eventstore:permission").Select(claim => claim.Value).ShouldBe(["command:submit", "query:read"]);
        native.IsInRole("GlobalAdministrator").ShouldBeFalse();
        foreach (RouteContractDeclaration command in commands)
            LegacyTenantStorageScopeAdapter.TryMapPlatform(command, command.PlatformAdmission!.Namespace, native, out _).ShouldBeFalse();
    }


    private static async Task AssertBoundBootstrapAsync(RouteCatalogEnvelope catalog, RouteCatalogGenerationReadback readback,
        ClaimsPrincipal principal, string jwt, string subject, CancellationToken token)
    {
        using RSA key = RSA.Create();
        string publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        string scope = PlatformNativeGatewayContract.Command<BootstrapGlobalAdmin>(catalog).PlatformAdmission!.Namespace;
        var command = new SubmitCommandRequest("bootstrap-component-1", scope, BootstrapGlobalAdmin.Domain, "global-administrators",
            BootstrapGlobalAdmin.CommandType, JsonSerializer.SerializeToElement(new BootstrapGlobalAdmin(subject)), CorrelationId: "bootstrap-component-1");
        var bound = new BoundWorkloadCommandRequest(command, subject);
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/v1/commands/bound/workload");
        PlatformNativeGatewayContract.BindBootstrap(request.Headers, readback, scope, jwt);
        request.Headers.Add(BoundWorkloadCommandProof.HeaderName, BoundWorkloadCommandProof.Issue(bound, "tenants",
            Convert.ToBase64String(key.ExportPkcs8PrivateKey()), DateTimeOffset.UtcNow));
        var authentication = Substitute.For<IAuthenticationService>();
        var ticket = new AuthenticationTicket(principal, JwtBearerDefaults.AuthenticationScheme);
        authentication.AuthenticateAsync(Arg.Any<HttpContext>(), JwtBearerDefaults.AuthenticationScheme).Returns(AuthenticateResult.Success(ticket));
        using ServiceProvider services = new ServiceCollection().AddSingleton(authentication).BuildServiceProvider();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>()).Returns(new SubmitCommandResult(command.CorrelationId!, MessageId: command.MessageId));
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(
            new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(catalog), readback.RootDigest, readback.Epoch), () => Task.CompletedTask));
        var context = new DefaultHttpContext { RequestServices = services, User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "tenants"), new Claim(ClaimTypes.NameIdentifier, "workload:tenants")], "DaprInternal")) };
        context.Request.Scheme = "http"; context.Request.Host = new HostString("localhost");
        foreach (var (name, value) in Headers(request.Headers)) context.Request.Headers[name] = value;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:BoundCommand:AllowedDomains:tenants"] = BootstrapGlobalAdmin.Domain,
            ["Authentication:BoundStreamRead:PublicKeys:tenants"] = publicKey,
        }).Build();
        var controller = new CommandsController(mediator, new ExtensionMetadataSanitizer(Options.Create(new ExtensionMetadataOptions())),
            NullLogger<CommandsController>.Instance, configuration: configuration, catalogAuthority: authority)
            { ControllerContext = new ControllerContext { HttpContext = context } };
        await AssertWorkloadHeadersAsync(context, token);
        // Real published controller checks bound signature + human identity + catalog-authorized platform mapping.
        (await controller.SubmitBoundWorkload(bound, token)).ShouldBeOfType<AcceptedResult>();
        await mediator.Received(1).Send(Arg.Is<SubmitCommand>(submitted => submitted.Tenant == "system"
            && submitted.UserId == subject && submitted.AuthenticatedSubject == subject && submitted.IsGlobalAdmin), token);
        authentication.AuthenticateAsync(Arg.Any<HttpContext>(), JwtBearerDefaults.AuthenticationScheme).Returns(AuthenticateResult.NoResult());
        (await controller.SubmitBoundWorkload(bound, token)).ShouldBeOfType<ForbidResult>();
        var foreign = new ClaimsPrincipal(new ClaimsIdentity([.. principal.Claims.Where(claim => claim.Type != "sub"), new Claim("sub", "foreign-subject")], "component"));
        authentication.AuthenticateAsync(Arg.Any<HttpContext>(), JwtBearerDefaults.AuthenticationScheme)
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(foreign, JwtBearerDefaults.AuthenticationScheme)));
        (await controller.SubmitBoundWorkload(bound, token)).ShouldBeOfType<ForbidResult>();
        await mediator.Received(1).Send(Arg.Any<SubmitCommand>(), token);
    }

    private static async Task AssertWorkloadHeadersAsync(DefaultHttpContext context, CancellationToken token)
    {
        context.RequestAborted = token;
        context.Request.Path = "/api/v1/commands/bound/workload";
        var monitor = Substitute.For<IOptionsMonitor<WorkloadAuthenticationOptions>>();
        monitor.Get("DaprInternal").Returns(new WorkloadAuthenticationOptions { Audience = "eventstore", AllowedCallers = ["tenants"] });
        var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName = Environments.Development;
        string channelToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [DaprAppChannelToken.ConfigurationKey] = channelToken }).Build();
        // MessageReceived checks channel/credential shape; signature and human authorization are independently checked above/below.
        context.Request.Headers[DaprAppChannelToken.HeaderName] = channelToken;
        context.Request.Headers[EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName] = "component-assertion-not-an-issued-identity";
        var events = new WorkloadJwtBearerEvents(monitor, configuration, environment, WorkloadSecurityClock.System, NullLogger<WorkloadJwtBearerEvents>.Instance);
        var options = new JwtBearerOptions();
        options.TokenValidationParameters.PropertyBag = new Dictionary<string, object> { [ConfigureWorkloadJwtBearerOptions.ConfiguredMarkerKey] = true };
        var scheme = new AuthenticationScheme("DaprInternal", "component", typeof(JwtBearerHandler));
        var accepted = new MessageReceivedContext(context, scheme, options);
        await events.MessageReceived(accepted);
        accepted.Result.ShouldBeNull(); accepted.Token.ShouldBe("component-assertion-not-an-issued-identity");
        string scope = context.Request.Headers["X-Hexalith-Scope"].ToString();
        context.Request.Headers.Remove("X-Hexalith-Scope");
        var missing = new MessageReceivedContext(context, scheme, options); await events.MessageReceived(missing);
        missing.Result!.Failure.ShouldBeOfType<WorkloadAuthenticationException>().ReasonCode.ShouldBe(WorkloadAuthenticationReasons.CredentialConflict);
        context.Request.Headers["X-Hexalith-Scope"] = new Microsoft.Extensions.Primitives.StringValues([scope, scope]);
        var duplicate = new MessageReceivedContext(context, scheme, options); await events.MessageReceived(duplicate);
        duplicate.Result!.Failure.ShouldBeOfType<WorkloadAuthenticationException>().ReasonCode.ShouldBe(WorkloadAuthenticationReasons.CredentialConflict);
        context.Request.Headers["X-Hexalith-Scope"] = scope;
    }

    private static async Task<ClaimsPrincipal> ValidateAsync(string jwt, string key)
    {
        TokenValidationResult validation = await new JsonWebTokenHandler { MapInboundClaims = false }.ValidateTokenAsync(jwt, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = CompositionEnvironment.TokenIssuer,
            ValidateAudience = true, ValidAudience = CompositionEnvironment.TokenAudience,
            ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true, RequireSignedTokens = true, ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.Zero,
        });
        validation.IsValid.ShouldBeTrue();
        return await new EventStoreClaimsTransformation(NullLogger<EventStoreClaimsTransformation>.Instance)
            .TransformAsync(new ClaimsPrincipal(validation.ClaimsIdentity));
    }

    private static HeaderDictionary Headers(HttpRequestHeaders headers) => new(headers.ToDictionary(pair => pair.Key,
        pair => new Microsoft.Extensions.Primitives.StringValues(pair.Value.ToArray()), StringComparer.OrdinalIgnoreCase));

    private static async Task AssertStatusAsync(HttpRequestHeaders headers, ClaimsPrincipal principal, string domain, CancellationToken token)
    {
        const string messageId = "component-command-1";
        var store = Substitute.For<ICommandStatusStore>();
        var record = new CommandStatusRecord(CommandStatus.Completed, DateTimeOffset.UtcNow, "component-aggregate", 1, null, null, null)
            { MessageId = messageId, Domain = domain, CommittedEventSequence = 4 };
        store.ReadStatusAsync("system", messageId, Arg.Any<CancellationToken>()).Returns(record);
        var context = new DefaultHttpContext { User = principal };
        foreach (var (name, value) in Headers(headers)) context.Request.Headers[name] = value;
        var controller = new CommandStatusController(store, NullLogger<CommandStatusController>.Instance)
            { ControllerContext = new ControllerContext { HttpContext = context } };
        (await controller.GetStatus(messageId, token)).ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<CommandStatusResponse>().TenantId.ShouldBe("system");
        await store.Received(1).ReadStatusAsync("system", messageId, token);
        context.Request.Headers.Remove("X-Hexalith-Scope");
        (await controller.GetStatus(messageId, token)).ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(403);
        context.Request.Headers["X-Hexalith-Scope"] = "platform:" + domain;
        context.Request.Headers["X-Hexalith-Tenant"] = principal.FindFirst("eventstore:tenant")!.Value;
        (await controller.GetStatus(messageId, token)).ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(403);
    }
}
#endif
