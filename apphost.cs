#:sdk Aspire.AppHost.Sdk@13.4.6

// Hexalith.Platform AppHost — platform-owned composition root (EXT-HOST-1).
// Domain modules must not own AppHost / Aspire / ServiceDefaults projects.
// Agents DomainService + FrontComposer UI wiring lands with Agents Story 5.6.

var builder = DistributedApplication.CreateBuilder(args);

builder.Build().Run();
