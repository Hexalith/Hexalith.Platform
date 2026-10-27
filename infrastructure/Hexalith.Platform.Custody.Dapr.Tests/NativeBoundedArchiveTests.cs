using System.Runtime.InteropServices;
using Dapr.Client;
using Hexalith.Platform.Custody.Dapr;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;

internal static class NativeBoundedArchiveTests
{
    internal static async Task Run()
    {
        using var fixture=new Fixture();using var dapr=Substitute.For<DaprClient>();
        string directory=Path.Combine(Path.GetTempPath(),"native-bounded-archive-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            string enrollment=Path.Combine(directory,"enrollment.json");await File.WriteAllTextAsync(enrollment,"fixture-enrollment");
            var install=new NativeInstallation("OpenBaoTransit","store","head",directory,directory,CustodyJson.Hash("fixture-enrollment"u8),"https://unused.invalid","transit","/unused",[],Fixture.Digest);
            var store=new NativeDaprAuthorityStore(fixture.Profile with { Native=install },install,dapr,TimeProvider.System);
            string category=Path.Combine(directory,"journal","native-result");Directory.CreateDirectory(category);string path=Path.Combine(category,"valid");byte[] bytes="exact-native-original"u8.ToArray();await File.WriteAllBytesAsync(path,bytes);
            var exact=await store.FindIntentResultAsync("journal/native-result/valid",default);if(exact is null || !(await store.ReadImmutableAsync(exact,default)).AsSpan().SequenceEqual(bytes)) throw new Exception("Exact native file unavailable");
            foreach(string kind in new[]{"fifo","symlink","oversize"})
            {
                string invalid=Path.Combine(category,kind);
                if(kind=="fifo") { if(Mkfifo(invalid,0x180)!=0) throw new Exception("Fixture FIFO unavailable"); }
                else if(kind=="symlink") File.CreateSymbolicLink(invalid,path);
                else { await using var file=File.Create(invalid);file.SetLength(CustodyBindingContract.MaximumOriginalBytes+1L); }
                using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await Refuse(()=>store.FindIntentResultAsync("journal/native-result/"+kind,deadline.Token).WaitAsync(deadline.Token));
            }
            using(var cancelled=new CancellationTokenSource()) { cancelled.Cancel();await Refuse(()=>store.ReadImmutableAsync(exact,cancelled.Token)); }
            // The adjacent enrolled archive manifest uses the same no-follow descriptor boundary.
            File.Delete(enrollment);if(Mkfifo(enrollment,0x180)!=0) throw new Exception("Fixture FIFO unavailable");
            using(var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(2))) await Refuse(()=>store.ReadImmutableAsync(exact,deadline.Token).WaitAsync(deadline.Token));
            if(dapr.ReceivedCalls().Any()) throw new Exception("Invalid native file reached transport");
        }
        finally { Directory.Delete(directory,true); }
    }
    private static async Task Refuse(Func<Task> action)
    { try { await action(); } catch(Exception error) when(error is IOException or AuthorityUnavailableException or OperationCanceledException) { return; }throw new Exception("Unsafe native file accepted"); }
    [DllImport("libc",EntryPoint="mkfifo",SetLastError=true)] private static extern int Mkfifo(string path,int mode);
}
