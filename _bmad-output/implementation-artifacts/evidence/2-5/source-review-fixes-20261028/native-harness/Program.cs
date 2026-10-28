using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;

// Resolve Tooling's direct libc imports through the probe, so the injected EINTR
// reaches its actual private P/Invoke rather than only runtime/libc PLT calls.
nint probe = NativeLibrary.Load(args[0]);
Assembly tooling = typeof(CompositionDocumentStore).Assembly;
NativeLibrary.SetDllImportResolver(tooling, (name, _, _) => name == "libc" ? probe : 0);
string directory = args[1];
Directory.CreateDirectory(directory);
string metadata = Path.Combine(directory, "metadata-read.json");
await CompositionDocumentStore.WriteAsync(metadata, new { status = "committed" }, CancellationToken.None);
MethodInfo read = typeof(CompositionDocumentStore).GetMethod("ReadIfExistsAsync", BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(typeof(JsonDocument));
using JsonDocument? document = await (Task<JsonDocument?>)read.Invoke(null, [metadata, CancellationToken.None])!;
if (document?.RootElement.GetProperty("status").GetString() != "committed")
    throw new InvalidOperationException("The shared metadata read did not preserve its committed bytes.");
MethodInfo snapshot = tooling.GetType("Hexalith.Builds.Tooling.Runtime.NativeTestExecutor")!.GetMethod("WriteSnapshotAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
string archive = Path.Combine(directory, "archive.json");
byte[] bytes = "{\"status\":\"committed\"}"u8.ToArray();
await (Task)snapshot.Invoke(null, [archive, directory, bytes, CancellationToken.None])!;
if (!File.ReadAllBytes(archive).SequenceEqual(bytes))
    throw new InvalidOperationException("The archive snapshot changed bytes.");
Console.WriteLine("Passed: interrupted shared lock, durable metadata round trip, archive round trip.");
