using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace Hexalith.Platform.Secrets.Inventory.Tests;

internal sealed class OfflineWorkloadToken(string? token) : IInventoryWorkloadToken
{
    public bool Configured => token is not null;
    public Task<string?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(token);
}
