using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Enrollment role verifies externally provisioned trust; it cannot create trust at startup.</summary>
public sealed class InstalledQualificationEnrollment(IndependentDaprAuthority scopes, IndependentDaprAuthority owner)
    : InstalledQualificationProvider(scopes, owner, "enrollment"), IQualificationEnrollmentProvider;
