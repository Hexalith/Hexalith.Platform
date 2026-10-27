using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Platform.Secrets.Runtime;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class OwnedWorkloadProofTests
{
    [Fact]
    public async Task SpanPrivateKeysIssueExactCurrentEventStoreProofFormatsAndRejectAllChangedCommandBindings()
    {
        using var key=RSA.Create(2048);byte[] der=key.ExportPkcs8PrivateKey();char[] encoded=Convert.ToBase64String(der).ToCharArray();
        try
        {
            var now=DateTimeOffset.UtcNow;string publicKey=Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
            var stream=new StreamReadRequest("tenant","domain","aggregate");string proof=OwnedWorkloadProof.Issue(stream,"caller",encoded,now);
            BoundStreamReadProof.Verify(proof,stream,"caller",publicKey,now).ShouldBeTrue();
            foreach(var changed in new[]{stream with { Tenant="other" },stream with { Domain="other" },stream with { AggregateId="other" }}) BoundStreamReadProof.Verify(proof,changed,"caller",publicKey,now).ShouldBeFalse();
            BoundStreamReadProof.Verify(proof,stream,"other",publicKey,now).ShouldBeFalse();BoundStreamReadProof.Verify(proof,stream,"caller",publicKey,now.AddSeconds(30)).ShouldBeFalse();
            var command=new SubmitCommandRequest("message","tenant","domain","aggregate","Fixture.Command",JsonSerializer.SerializeToElement(new { value=1 }),IdempotencyKey:"message");
            var request=new BoundWorkloadCommandRequest(command,"principal");proof=OwnedWorkloadProof.Issue(request,"caller",encoded,now);
            BoundWorkloadCommandProof.Verify(request,proof,"caller",publicKey,now).ShouldBeTrue();
            foreach(var changed in new[]{command with { Tenant="other" },command with { Domain="other" },command with { AggregateId="other" },command with { MessageId="other" },command with { IdempotencyKey="other" },command with { CommandType="Other.Command" },command with { Payload=JsonSerializer.SerializeToElement(new { value=2 }) },command with { Extensions=new Dictionary<string,string>{{"changed","true"}} }})
                BoundWorkloadCommandProof.Verify(new(changed,"principal"),proof,"caller",publicKey,now).ShouldBeFalse();
            BoundWorkloadCommandProof.Verify(request with { PrincipalId="other" },proof,"caller",publicKey,now).ShouldBeFalse();
            Should.Throw<ArgumentException>(()=>OwnedWorkloadProof.Issue(stream,"caller",encoded,now.ToOffset(TimeSpan.FromHours(1))));
            using var small=RSA.Create(1024);Should.Throw<ArgumentException>(()=>OwnedWorkloadProof.Issue(stream,"caller",Convert.ToBase64String(small.ExportPkcs8PrivateKey()).AsSpan(),now));
            string file=Path.GetTempFileName();try
            {
                await File.WriteAllTextAsync(file,new string(encoded),TestContext.Current.CancellationToken);
                ReadOnlyMemory<char> captured=default;
                // Capture the owned array only in the fixture to prove the callback storage is cleared.
                await BoundedRegularFile.UseUtf8SpanAsync(file,16384,text=> { text.SequenceEqual(encoded).ShouldBeTrue();return true; },TestContext.Current.CancellationToken);
                captured.IsEmpty.ShouldBeTrue();
                await Should.ThrowAsync<InvalidOperationException>(()=>BoundedRegularFile.UseUtf8SpanAsync<bool>(file,16384,_=>throw new InvalidOperationException("fixture callback"),TestContext.Current.CancellationToken));
            }
            finally { File.Delete(file); }
        }
        finally { CryptographicOperations.ZeroMemory(der);Array.Clear(encoded); }
    }
    [Theory]
    [InlineData("valid")][InlineData("invalid-encoding")][InlineData("small-key")][InlineData("cancelled")]
    public async Task ActualOwnedQueryProofMatchesSdkAndClearsDecodedPrivateBuffersOnEveryExit(string kind)
    {
        using var rsa=RSA.Create(kind=="small-key"?1024:2048);byte[] privateDer=rsa.ExportPkcs8PrivateKey();
        string path=Path.GetTempFileName();byte[]? decoded=null;var now=DateTimeOffset.UtcNow;
        var query=new SubmitQueryRequest("tenant","domain","aggregate","ExactQuery",Payload:JsonSerializer.SerializeToElement(new { original=1 }));
        var request=new BoundOwnerQueryRequest(query,"principal");
        using var cancellation=new CancellationTokenSource();
        try
        {
            await File.WriteAllTextAsync(path,kind=="invalid-encoding"?"not base64":Convert.ToBase64String(privateDer),TestContext.Current.CancellationToken);
            OwnedWorkloadProof.DecodedPrivateKeyInspection=value=> { decoded=value;if(kind=="cancelled") throw new OperationCanceledException(cancellation.Token); };
            Task<string> Issue()=>BoundedRegularFile.UseUtf8SpanAsync(path,16384,material=>OwnedWorkloadProof.Issue(request,"caller",material,now),cancellation.Token);
            if(kind=="invalid-encoding") await Should.ThrowAsync<FormatException>(Issue);
            else if(kind=="small-key") await Should.ThrowAsync<ArgumentException>(Issue);
            else if(kind=="cancelled") await Should.ThrowAsync<OperationCanceledException>(Issue);
            else
            {
                string proof=await Issue();string publicKey=Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
                BoundOwnerQueryProof.Verify(request,proof,"caller",publicKey,now).ShouldBeTrue();
                foreach(var changed in new[]{query with { Tenant="other" },query with { Domain="other" },query with { AggregateId="other" },query with { QueryType="OtherQuery" },query with { Payload=JsonSerializer.SerializeToElement(new { original=2 }) }})
                    BoundOwnerQueryProof.Verify(new(changed,"principal"),proof,"caller",publicKey,now).ShouldBeFalse();
                BoundOwnerQueryProof.Verify(request with { PrincipalId="other" },proof,"caller",publicKey,now).ShouldBeFalse();
                BoundOwnerQueryProof.Verify(request,proof,"other",publicKey,now).ShouldBeFalse();
                BoundOwnerQueryProof.Verify(request,proof,"caller",publicKey,now.AddSeconds(30)).ShouldBeFalse();
            }
            decoded.ShouldNotBeNull().ShouldAllBe(value=>value==0);
        }
        finally { OwnedWorkloadProof.DecodedPrivateKeyInspection=null;CryptographicOperations.ZeroMemory(privateDer);File.Delete(path); }
    }

}
