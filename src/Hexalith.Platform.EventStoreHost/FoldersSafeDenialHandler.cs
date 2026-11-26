#if HEXALITH_PLATFORM_EXTENSION_SDK
using Hexalith.EventStore.Server.DomainServices;
using Microsoft.AspNetCore.Diagnostics;

/// <summary>Preserves the owner’s metadata-only synchronous denials at the composed gateway.</summary>
internal sealed class FoldersSafeDenialHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainServiceSafeDenialException denial) return false;
        if (context.RequestAborted.IsCancellationRequested || context.Response.HasStarted) return true;

        (string code, string title) = denial.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => ("authentication_failure", "Authentication required"),
            StatusCodes.Status403Forbidden => ("authorization_denied", "Authorization denied"),
            StatusCodes.Status404NotFound => ("not_found_to_caller", "Resource not available"),
            _ => ("read_model_unavailable", "Read model unavailable"),
        };
        await Results.Problem(
            type: $"https://hexalith.io/problems/{code}",
            title: title,
            statusCode: denial.StatusCode,
            extensions: new Dictionary<string, object?>
            {
                ["category"] = code,
                ["code"] = code,
                ["retryable"] = denial.StatusCode == StatusCodes.Status503ServiceUnavailable,
                ["details"] = new Dictionary<string, object?> { ["visibility"] = "metadata_only" },
            }).ExecuteAsync(context).ConfigureAwait(false);
        return true;
    }
}
#endif
