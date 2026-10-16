using Hexalith.Platform.Secrets.Qualification.Observer;
using Hexalith.Platform.Secrets.Runtime;

using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class ObserverJournalDurabilityTests
{
    [Fact]
    public void PublicationFlushFailureCannotAcknowledgeAndRestartRepublishesDurably()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var durability = new FailPublication();
            var journal = new ContentFreeObserverJournal(directory, durability);
            Should.Throw<SecretsDependencyNotAvailableException>(() => journal.Append("original-identifier"));
            durability.Calls.ShouldBe(3); // Directory/parent admission syncs succeed; post-rename publication sync fails.
            File.Exists(Path.Combine(directory, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("original-identifier"))) + ".id")).ShouldBeTrue();
            // A fresh host uses the real filesystem durability boundary before acknowledging the existing entry.
            var restarted = new ContentFreeObserverJournal(directory);
            restarted.Append("original-identifier");
            restarted.Read().ShouldBe(["original-identifier"]);
            new ContentFreeObserverJournal(directory).Read().ShouldBe(["original-identifier"]);
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class FailPublication : IObserverJournalDurability
    {
        public int Calls { get; private set; }
        public void FlushDirectory(string directory)
        {
            Calls++;
            if (Calls == 3) { throw new SecretsDependencyNotAvailableException("qualification.observer.directoryPublicationDurability", "Persistence boundary unavailable."); }
        }
    }
}
