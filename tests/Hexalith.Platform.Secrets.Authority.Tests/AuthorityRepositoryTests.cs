using Shouldly;

namespace Hexalith.Platform.Secrets.Authority.Tests;

public sealed class AuthorityRepositoryTests
{
    [Fact]
    public async Task RestartPreservesStateAndLostResponseRetriesReturnTheOriginalReceipt()
    {
        using var fixture = new AuthorityTestState();
        AuthorityState initial = await fixture.Repository.ReadAsync(TestContext.Current.CancellationToken);
        var operation = new AuthorityAdministration("original-operation", initial.Revision, "PublishProfile", Profile: fixture.Profile);
        AuthorityReceipt original = await fixture.Repository.AdministerAsync(AuthorityTestState.Administrator, operation, fixture.Clock.Now, TestContext.Current.CancellationToken);
        using var restarted = new AuthorityRepository(fixture.DirectoryPath);
        AuthorityReceipt retried = await restarted.AdministerAsync(AuthorityTestState.Administrator, operation, fixture.Clock.Now.AddMinutes(1), TestContext.Current.CancellationToken);
        retried.ShouldBe(original);
        var state = await restarted.ReadAsync(TestContext.Current.CancellationToken);
        state.Profile.ShouldBe(fixture.Profile);
        state.Audit.Length.ShouldBe(1);
        state.Revision.ShouldBe(original.Revision);
    }

    [Fact]
    public async Task OperationIdentityRejectsChangedPayloadAndAnotherAdministrator()
    {
        using var fixture = new AuthorityTestState();
        AuthorityState initial = await fixture.Repository.ReadAsync(TestContext.Current.CancellationToken);
        var operation = new AuthorityAdministration("original-operation", initial.Revision, "PublishProfile", Profile: fixture.Profile);
        await fixture.Repository.AdministerAsync(AuthorityTestState.Administrator, operation, fixture.Clock.Now, TestContext.Current.CancellationToken);
        await Should.ThrowAsync<AuthorityConflictException>(() => fixture.Repository.AdministerAsync(AuthorityTestState.Administrator,
            operation with { Profile = fixture.Profile with { Version = "changed" } }, fixture.Clock.Now, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<AuthorityConflictException>(() => fixture.Repository.AdministerAsync("another-owner", operation, fixture.Clock.Now, TestContext.Current.CancellationToken));
        (await fixture.Repository.ReadAsync(TestContext.Current.CancellationToken)).Audit.Length.ShouldBe(1);
    }

    [Fact]
    public async Task ConcurrentIndependentWritersFenceTheSameExpectedRevisionAndRetainBothOutcomes()
    {
        using var fixture = new AuthorityTestState();
        using var other = new AuthorityRepository(fixture.DirectoryPath);
        string revision = (await fixture.Repository.ReadAsync(TestContext.Current.CancellationToken)).Revision;
        var first = new AuthorityAdministration("first", revision, "PublishProfile", Profile: fixture.Profile);
        var second = new AuthorityAdministration("second", revision, "SetCaller", Caller: fixture.Enrollment);
        AuthorityReceipt[] outcomes = await Task.WhenAll(Task.Run(() => fixture.Repository.AdministerAsync(AuthorityTestState.Administrator, first, fixture.Clock.Now, TestContext.Current.CancellationToken)),
            Task.Run(() => other.AdministerAsync(AuthorityTestState.Administrator, second, fixture.Clock.Now, TestContext.Current.CancellationToken)));
        outcomes.Count(item => item.Outcome == "Applied").ShouldBe(1);
        outcomes.Count(item => item.Outcome == "RevisionConflict").ShouldBe(1);
        AuthorityState state = await fixture.Repository.ReadAsync(TestContext.Current.CancellationToken);
        state.Generation.ShouldBe(1);
        state.Audit.Length.ShouldBe(2);
        AuthorityAdministration rejected = outcomes[0].Outcome == "RevisionConflict" ? first : second;
        (await other.AdministerAsync(AuthorityTestState.Administrator, rejected, fixture.Clock.Now.AddSeconds(1), TestContext.Current.CancellationToken)).ShouldBe(outcomes.Single(item => item.Outcome == "RevisionConflict"));
    }

    [Theory]
    [InlineData("corrupt")]
    [InlineData("missing")]
    [InlineData("oversized")]
    public async Task CorruptMissingOrOversizedStorageNeverReturnsCachedOrEmptyAuthority(string mode)
    {
        using var fixture = new AuthorityTestState();
        await fixture.Seed();
        string path = Path.Combine(fixture.DirectoryPath, "authority.json");
        if (mode == "missing") { File.Delete(path); }
        else if (mode == "corrupt") { await File.WriteAllTextAsync(path, "{}", TestContext.Current.CancellationToken); }
        else { using var file = File.OpenWrite(path); file.SetLength(17 * 1024 * 1024); }
        await Should.ThrowAsync<IOException>(() => fixture.Repository.ReadAsync(TestContext.Current.CancellationToken));
        using var restarted = new AuthorityRepository(fixture.DirectoryPath);
        await Should.ThrowAsync<IOException>(() => restarted.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DirectoryFlushFailureWithholdsUncertainOutcomeUntilRestartRecoversOriginalOperation()
    {
        using var fixture = new AuthorityTestState();
        string revision = (await fixture.Repository.ReadAsync(TestContext.Current.CancellationToken)).Revision;
        using var failing = new FailingFlushRepository(fixture.DirectoryPath);
        var operation = new AuthorityAdministration("lost-commit-response", revision, "PublishProfile", Profile: fixture.Profile);
        await Should.ThrowAsync<IOException>(() => failing.AdministerAsync(AuthorityTestState.Administrator, operation, fixture.Clock.Now, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<IOException>(() => failing.ReadAsync(TestContext.Current.CancellationToken));
        using var restarted = new AuthorityRepository(fixture.DirectoryPath);
        AuthorityReceipt recovered = await restarted.AdministerAsync(AuthorityTestState.Administrator, operation, fixture.Clock.Now.AddMinutes(1), TestContext.Current.CancellationToken);
        recovered.Outcome.ShouldBe("Applied");
        (await restarted.ReadAsync(TestContext.Current.CancellationToken)).Audit.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task IncompleteUnrenamedTransactionCannotBecomeCurrent()
    {
        using var fixture = new AuthorityTestState();
        await fixture.Seed();
        string original = (await fixture.Repository.ReadAsync(TestContext.Current.CancellationToken)).Revision;
        await File.WriteAllTextAsync(Path.Combine(fixture.DirectoryPath, "transaction-uncommitted.tmp"), "partial", TestContext.Current.CancellationToken);
        using var restarted = new AuthorityRepository(fixture.DirectoryPath);
        (await restarted.ReadAsync(TestContext.Current.CancellationToken)).Revision.ShouldBe(original);
    }

    private sealed class FailingFlushRepository(string directory) : AuthorityRepository(directory)
    { protected override void FlushDirectory() => throw new IOException("Offline injected directory flush failure."); }
}
