#!/usr/bin/env dotnet
#:sdk Aspire.AppHost.Sdk@13.6.5
#:property ManagePackageVersionsCentrally=false
#:property RuntimeFrameworkVersion=11.0.0
#:package Aspire.Hosting.Docker@13.6.5
#:package Aspire.Hosting.Kubernetes@13.6.5-preview.1.26559.7
#:package Aspire.Hosting.Redis@13.6.5
#:package Aspire.Hosting.Keycloak@13.6.5-preview.1.26559.7
#:package CommunityToolkit.Aspire.Hosting.Dapr@13.6.2-beta.959
#:package Microsoft.Extensions.Hosting@11.0.0
#:package Microsoft.Extensions.Http@11.0.0
#:package System.Text.Json@11.0.0
#:project src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj
using Aspire.Hosting;
using Hexalith.Platform.Composition;

var builder = DistributedApplication.CreateBuilder(args);
using var acceptedLaunch = AgentsHostComposition.OwnLaunch(builder);
PlatformAppModel.ComposeEntry(builder);
using var application = acceptedLaunch.Build();
application.Run();
