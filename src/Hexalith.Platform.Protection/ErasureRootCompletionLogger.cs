using Microsoft.Extensions.Logging;

namespace Hexalith.Platform.Protection;

/// <summary>Dedicated safe worker logger for private receivers whose application logging deliberately has no general providers.</summary>
internal static class ErasureRootCompletionLogger
{
    private static readonly ILoggerFactory Factory = LoggerFactory.Create(builder => builder.AddSimpleConsole(options => options.SingleLine = true).SetMinimumLevel(LogLevel.Information));
    internal static ILogger<T> Create<T>() => Factory.CreateLogger<T>();
}
