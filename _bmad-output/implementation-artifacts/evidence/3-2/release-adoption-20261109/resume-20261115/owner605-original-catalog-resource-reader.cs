using System.Reflection;
Assembly assembly = Assembly.LoadFile(Path.GetFullPath(args[0]));
using Stream resource = assembly.GetManifestResourceStream("Hexalith.Builds.Tooling.Manifest.platform-version-catalog.json") ?? throw new InvalidDataException("Missing catalog resource");
using var output = File.Open(args[1], FileMode.CreateNew);
resource.CopyTo(output);
