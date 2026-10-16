using System.Runtime.Loader;

/// <summary>Loads Platform-owned telemetry dependencies into its explicitly selected Works process.</summary>
internal static class StartupHook
{
    /// <summary>Resolves dependencies beside this pinned deployment artifact; never installs operator authority.</summary>
    public static void Initialize()
    {
        if (Environment.GetEnvironmentVariable("WORKS_PLATFORM_RUNTIME") != "1")
            throw new InvalidOperationException("Platform Works runtime requires explicit host enablement.");
        string directory = Path.GetDirectoryName(typeof(StartupHook).Assembly.Location)!;
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            string candidate = Path.Combine(directory, name.Name + ".dll");
            return File.Exists(candidate) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate) : null;
        };
    }
}
