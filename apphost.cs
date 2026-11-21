#!/usr/bin/env dotnet
#:sdk Aspire.AppHost.Sdk@17.0.0
#:property ManagePackageVersionsCentrally=false
#:property RuntimeFrameworkVersion=11.0.0
#:property SuppressAspireAppHostSdkObsoleteWarning=true
#:package Aspire.Hosting.Docker@17.0.0
#:package Aspire.Hosting.Kubernetes@17.0.0
#:package Aspire.Hosting.Redis@17.0.0
#:package Aspire.Hosting.Keycloak@17.0.0-preview.1.26567.5
#:package CommunityToolkit.Aspire.Hosting.Dapr@17.0.0-preview.1.261120-0454
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
