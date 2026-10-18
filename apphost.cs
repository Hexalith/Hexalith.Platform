#!/usr/bin/env dotnet
#:sdk Aspire.AppHost.Sdk@13.6.1
#:property ManagePackageVersionsCentrally=false
#:package Aspire.Hosting.Docker@13.6.1
#:package Aspire.Hosting.Redis@13.6.1
#:package Aspire.Hosting.Keycloak@13.6.1-preview.1.26506.6
#:package CommunityToolkit.Aspire.Hosting.Dapr@13.6.1-beta.935
#:package Hexalith.EventStore.Aspire@3.125.1
#:package Hexalith.EventStore.ServiceDefaults@3.125.1
#:project src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj
using Aspire.Hosting;
using Hexalith.Platform.Composition;

var builder = DistributedApplication.CreateBuilder(args);
PlatformAppModel.ComposeEntry(builder);
builder.Build().Run();
