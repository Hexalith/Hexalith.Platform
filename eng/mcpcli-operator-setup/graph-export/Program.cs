using Hexalith.Platform.Composition;

if (args.Length != 2)
{
    throw new ArgumentException("Usage: GraphExport <Platform root> <new graph path>");
}
string root = Path.GetFullPath(args[0]);
string output = Path.GetFullPath(args[1]);
string graph = PlatformHostedPublishing.Describe(
    PlatformApplicationGraph.Resolve(PlatformHostedPublishing.ReferenceOptions(root))).GetRawText();
using FileStream file = new(output, FileMode.CreateNew, FileAccess.Write);
using StreamWriter writer = new(file);
writer.WriteLine(graph);
