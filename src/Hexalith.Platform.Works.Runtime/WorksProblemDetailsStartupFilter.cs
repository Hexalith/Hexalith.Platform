using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Hexalith.Platform.Works.Runtime;

/// <summary>Applies Platform-owned exception and empty-status responses around the SDK pipeline.</summary>
internal sealed class WorksProblemDetailsStartupFilter : IStartupFilter
{
    /// <inheritdoc/>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.UseExceptionHandler();
            app.UseStatusCodePages();
            next(app);
        };
}
