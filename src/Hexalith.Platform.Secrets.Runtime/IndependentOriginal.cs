using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Durable exact original, including uncertain executions. No request identity is recycled.</summary>
public sealed record IndependentOriginal(JsonElement Request, JsonElement? Result, string SubmissionState = "Unsent", long RetainedRevision = 0, long ResultRetainedRevision = 0);
