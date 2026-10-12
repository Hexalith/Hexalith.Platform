#!/usr/bin/env dotnet
#:sdk Aspire.AppHost.Sdk@13.6.0
#:property ManagePackageVersionsCentrally=false
#:package Aspire.Hosting.Docker@13.6.1
#:package Aspire.Hosting.Redis@13.6.1
#:package Aspire.Hosting.Keycloak@13.6.1-preview.1.26506.6
#:package CommunityToolkit.Aspire.Hosting.Dapr@13.6.0-preview.1.261001-0243
#:package Hexalith.EventStore.Aspire@3.120.0
#:project src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj
using Aspire.Hosting;
using Hexalith.Platform.Composition;

var builder = DistributedApplication.CreateBuilder(args);
if (bool.TryParse(builder.Configuration["Platform:Agents:Enabled"], out bool agents) && agents)
{
    throw new InvalidOperationException("DependencyNotAvailable: EXT-HOST-1. Full Agents composition and accepted owner prerequisites are unavailable.");
}
if (bool.TryParse(builder.Configuration["Platform:Works:Enabled"], out bool works) && works)
{
    WorksPreviewModel.Compose(builder);
}
else
{
    PlatformAppModel.Compose(builder);
}
builder.Build().Run();
