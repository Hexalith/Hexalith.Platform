using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Tenants.Contracts.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Hexalith.Platform.Secrets.Inventory.Tests;

internal sealed class OfflineRouteHandler(Uri target) : DelegatingHandler(new HttpClientHandler())
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    { request.RequestUri = new Uri(target, request.RequestUri!.PathAndQuery); return base.SendAsync(request, cancellationToken); }
}
