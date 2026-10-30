using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual controlled transport and single-open acquisition boundaries.</summary>
public sealed class ErasureRootLoop7BoundaryTests
{
    private static ErasureRootInstallation Installed => new("erc", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant"] = "epoch" });

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("erc\n")]
    [InlineData("erc\r")]
    [InlineData("erc\t")]
    [InlineData(" erc")]
    [InlineData("erc/id")]
    public void MalformedNativeIdentityRefusesBeforeTransport(string identity)
    {
        using var http = new HttpClient { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(1) };
        Should.Throw<ArgumentException>(() => new OpenBaoErasureRootStore(http, Installed with { StoreId = identity }, "env:ERC_TEST"));
        Should.Throw<ArgumentException>(() => new OpenBaoErasureRootStore(http, Installed with { EnrollmentId = identity }, "env:ERC_TEST"));
        Should.Throw<ArgumentException>(() => new OpenBaoErasureRootStore(http, Installed with { EnrollmentId = new string('x', 257) }, "env:ERC_TEST"));
        using var valid = new OpenBaoErasureRootStore(new Uri("https://erc.invalid/"), Installed with { StoreId = new string('x', 256) }, "env:ERC_TEST");
    }

    [Fact]
    public async Task OwnedClientIsDisposedWhenConstructorValidationRefuses()
    {
        using var http = new HttpClient { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(1) };
        Should.Throw<ArgumentException>(() => new OpenBaoErasureRootStore(http, Installed with { StoreId = "invalid\n" }, "env:ERC_TEST", ownsHttp: true));
        await Should.ThrowAsync<ObjectDisposedException>(() => http.GetAsync("https://erc.invalid/", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(302)]
    [InlineData(307)]
    public async Task OwnedNativeTransportNeverForwardsCredentialToRedirectDestination(int status)
    {
        string variable = "ERC_REDIRECT_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "synthetic-disposable-bearer");
        using var source = new TcpListener(IPAddress.Loopback, 0); using var destination = new TcpListener(IPAddress.Loopback, 0);
        source.Start(); destination.Start();
        int first = ((IPEndPoint)source.LocalEndpoint).Port, second = ((IPEndPoint)destination.LocalEndpoint).Port;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var received = Task.Run(async () =>
            {
                using var connection = await source.AcceptTcpClientAsync(timeout.Token);
                using var stream = connection.GetStream(); using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var headers = new List<string>(); string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(timeout.Token))) { headers.Add(line); }
                headers.ShouldContain("X-Vault-Token: synthetic-disposable-bearer");
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Redirect\r\nLocation: http://127.0.0.1:{second}/leak\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), timeout.Token);
            }, timeout.Token);
            using var store = OpenBaoErasureRootStore.CreateDisposableLoopback(new Uri($"http://127.0.0.1:{first}/"), Installed, "env:" + variable);
            await Should.ThrowAsync<InvalidOperationException>(() => store.ReadHeadAsync("tenant", timeout.Token));
            await received;
            destination.Pending().ShouldBeFalse();
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Fact]
    public async Task SingleOpenBuffersBoundGrowingInputsAndKeepAcceptedReplacedBytes()
    {
        string directory = Path.Combine(Path.GetTempPath(), "protection-loop7-file-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "input"); byte[] accepted = Enumerable.Repeat((byte)'x', 16384).ToArray(); string digest = Convert.ToHexString(SHA256.HashData(accepted));
        try
        {
            File.WriteAllBytes(path, accepted);
            ProtectionPinnedFile.Read(path, accepted.Length, digest).ShouldBe(accepted);
            Should.Throw<InvalidOperationException>(() => ProtectionPinnedFile.Read(path, accepted.Length, digest, () => { using var append = new FileStream(path, FileMode.Append); append.WriteByte(1); }));
            File.WriteAllBytes(path, accepted);
            ProtectionPinnedFile.Read(path, accepted.Length, digest, () => { File.Move(path, path + ".old"); File.WriteAllText(path, "unaccepted replacement"); }).ShouldBe(accepted);
            using (var oversized = new FileStream(path, FileMode.Create)) { oversized.SetLength(16 * 1024 * 1024 + 1); }
            Should.Throw<InvalidOperationException>(() => ProtectionPinnedFile.Read(path, 16 * 1024 * 1024));
            int callbacks = 0;
            (await ProtectionQualificationServerExecutable.RunCoreAsync("executor", path, digest, () => callbacks++, TestContext.Current.CancellationToken)).ShouldBe(2);
            callbacks.ShouldBe(0);
        }
        finally { Directory.Delete(directory, true); }
    }
}
