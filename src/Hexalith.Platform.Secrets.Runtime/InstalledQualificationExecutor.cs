using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Executor role restricted to its independent installed owner.</summary>
public sealed class InstalledQualificationExecutor(IndependentDaprAuthority scopes, IndependentDaprAuthority owner)
    : InstalledQualificationProvider(scopes, owner, "executor"), IQualificationExecutorProvider;
