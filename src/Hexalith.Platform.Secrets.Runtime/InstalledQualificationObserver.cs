using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Observer role restricted to persisted owner reads.</summary>
public sealed class InstalledQualificationObserver(IndependentDaprAuthority scopes, IndependentDaprAuthority owner)
    : InstalledQualificationProvider(scopes, owner, "observer"), IQualificationObserverProvider;
