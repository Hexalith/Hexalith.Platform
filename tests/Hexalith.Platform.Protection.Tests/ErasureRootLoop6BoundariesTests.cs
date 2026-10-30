using System.Security.Cryptography;
using System.Text.Json;
using Shouldly;
using Dapr.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Early allocation and exact legacy framing boundaries on actual production helpers.</summary>
public sealed class ErasureRootLoop6BoundariesTests
{
    [Fact]
    public void ProductionInitialDaprTransportRegistrationSurvivesLaterDefaultRegistration()
    {
        var services = new ServiceCollection(); bool configured = false;
        services.AddDaprClient(builder => { configured = true; ErasureRootDaprTransport.Configure(builder); });
        services.AddDaprClient();
        using var provider = services.BuildServiceProvider(); provider.GetRequiredService<DaprClient>().ShouldNotBeNull(); configured.ShouldBeTrue();
    }

    [Theory]
    [InlineData("erc-shared-v2", 128 * 1024 + 1)]
    [InlineData("erc-chunks-v1", 4 * 1024 * 1024 + 1)]
    public async Task HashValidOversizedChunkRefusesBeforeFetchingAnyFollowingDistinctBuffer(string format, int length)
    {
        var token = TestContext.Current.CancellationToken; var store = new ErasureRootStoreFixture();
        byte[] oversized = new byte[length]; string hash = Convert.ToHexString(SHA256.HashData(oversized));
        (await store.CreateArchiveAsync("tenant-a", hash, oversized, token)).ShouldBeTrue();
        int total = format == "erc-chunks-v1" ? 8 * 1024 * 1024 : length + 1;
        string id = new('A', 64), absent = new('B', 64);
        byte[] frame = JsonSerializer.SerializeToUtf8Bytes(new ErasureRootChunkedArchive(format, total, [hash, absent]));
        (await store.CreateArchiveAsync("tenant-a", id, frame, token)).ShouldBeTrue(); int before = store.ArchiveReads;
        await Should.ThrowAsync<InvalidOperationException>(() => ErasureRootArchives.ReadBytesAsync(store, "tenant-a", id, token));
        (store.ArchiveReads - before).ShouldBe(2);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CumulativeAndDuplicateChunksRefuseAtTheFirstRemainingLengthViolation(bool duplicate)
    {
        var token = TestContext.Current.CancellationToken; var store = new ErasureRootStoreFixture();
        byte[] first = new byte[16 * 1024], second = Enumerable.Repeat((byte)1, 16 * 1024).ToArray();
        string a = Convert.ToHexString(SHA256.HashData(first)), b = Convert.ToHexString(SHA256.HashData(second));
        await store.CreateArchiveAsync("tenant-a", a, first, token); await store.CreateArchiveAsync("tenant-a", b, second, token);
        string id = new('A', 64);
        await store.CreateArchiveAsync("tenant-a", id, JsonSerializer.SerializeToUtf8Bytes(new ErasureRootChunkedArchive("erc-shared-v2", 24 * 1024, [a, duplicate ? a : b, new string('B', 64)])), token);
        int before = store.ArchiveReads;
        await Should.ThrowAsync<InvalidOperationException>(() => ErasureRootArchives.ReadBytesAsync(store, "tenant-a", id, token));
        (store.ArchiveReads - before).ShouldBe(duplicate ? 2 : 3);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PinnedAcquisitionRejectsOversizedAndGrowingInputsWithoutReopeningAcceptedFile(bool growing)
    {
        string path = Path.Combine(Path.GetTempPath(), "erc-pinned-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllBytes(path, new byte[growing ? 32 : 1025]);
            string digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            Should.Throw<InvalidOperationException>(() => ErasureRootHostComposition.ReadPinned(path, digest, 1024,
                growing ? () => { using var append = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite); append.Write(new byte[1024]); } : null));
        }
        finally { File.Delete(path); }
    }
    [Theory]
    [InlineData(1024 * 1024)]
    [InlineData(8 * 1024 * 1024)]
    public void ActualPinnedAcquisitionMaximaKeepOneExactAcceptedBuffer(int maximum)
    {
        string path = Path.Combine(Path.GetTempPath(), "erc-pinned-max-" + Guid.NewGuid().ToString("N"));
        try
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(maximum); File.WriteAllBytes(path, bytes);
            ErasureRootHostComposition.ReadPinned(path, Convert.ToHexString(SHA256.HashData(bytes)), maximum).ShouldBe(bytes);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void PinnedAcceptedMaximumBufferKeepsExactDigestAndLegacyNodeHashOmitsNewNullFormat()
    {
        string path = Path.Combine(Path.GetTempPath(), "erc-pinned-" + Guid.NewGuid().ToString("N"));
        try
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(1024); File.WriteAllBytes(path, bytes);
            ErasureRootHostComposition.ReadPinned(path, Convert.ToHexString(SHA256.HashData(bytes)), 1024).ShouldBe(bytes);
        }
        finally { File.Delete(path); }
        var legacy = new { TenantId = "tenant-a", StoreId = "erc", EnrollmentId = "enrollment", Prefix = new string('A', 64), Children = new SortedDictionary<string, string>(), Identity = "id", Original = (JsonElement?)JsonSerializer.SerializeToElement("value"), OriginalChunks = (string[]?)null, OriginalLength = 0, OriginalSha256 = (string?)null };
        byte[] original = JsonSerializer.SerializeToUtf8Bytes(legacy);
        ErasureRootCustody.Hash(JsonSerializer.Deserialize<ErasureRootSourceIndexNode>(original, ErasureRootCustody.Json)).ShouldBe(ErasureRootCustody.Hash(legacy));
    }
}
