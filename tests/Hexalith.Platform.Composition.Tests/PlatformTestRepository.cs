namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;

/// <summary>Resolves the effective repository root recorded in the collecting binary.</summary>
internal static class PlatformTestRepository
{
    /// <summary>Gets the build-declared root, then the requested override, then the nearest repository ancestor.</summary>
    internal static string Root
    {
        get
        {
            string? declared = typeof(PlatformTestRepository).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .SingleOrDefault(metadata => metadata.Key == "PlatformTestRepositoryRoot")?.Value;
            if (declared is not null) return Path.GetFullPath(declared);
            string? selected = Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_TEST_ROOT");
            DirectoryInfo? directory = new(selected ?? AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "apphost.cs"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("The Platform test repository root is unavailable.");
        }
    }
}
