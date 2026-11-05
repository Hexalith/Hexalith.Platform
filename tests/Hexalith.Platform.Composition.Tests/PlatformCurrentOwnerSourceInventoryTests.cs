namespace Hexalith.Platform.Composition.Tests;

using System.Text;
using Hexalith.Builds.Tooling.Runtime;
using Shouldly;

/// <summary>Exercises real bounded Git inventories; private fixtures grant no current-owner runtime authority.</summary>
public sealed class PlatformCurrentOwnerSourceInventoryTests
{
    /// <summary>A total inventory larger than one public capture remains complete through bounded tree partitions.</summary>
    [Fact]
    public void LargeTrackedInventoryPreservesEveryRequiredOwnerInput()
    {
        using PlatformPartiesProofFixture fixture = new();
        string root = fixture.Options.Mapping.Root;
        List<string> expected = [.. Read(root, Revision(root))];
        string[] extensions = [".cs", ".csproj", ".props", ".targets", ".json"];
        for (int directory = 0; directory < 12; directory++)
            for (int file = 0; file < 120; file++)
            {
                string name = $"source/group-{directory}/{file}-" + new string('x', 90) + extensions[file % extensions.Length];
                Write(root, name);
                expected.Add(name);
            }
        foreach (string name in new[] { ".editorconfig", ".gitattributes", "NuGet.Config" })
        {
            Write(root, name);
            expected.Add(name);
        }
        Write(root, "references/Foreign/Excluded.cs");
        PlatformPartiesProofFixture.Commit(root);
        Run(root, "ls-files", "-z").OutputTruncated.ShouldBeTrue();
        string oldRevision = Revision(root);
        Read(root, oldRevision).ShouldBe(expected.Order(StringComparer.Ordinal));
        Write(root, "Later.cs");
        PlatformPartiesProofFixture.Commit(root);
        Read(root, oldRevision).ShouldBe(expected.Order(StringComparer.Ordinal));
        Read(root, Revision(root)).ShouldContain("Later.cs");
    }

    /// <summary>An oversized individual tree and an excessive depth both refuse instead of returning partial paths.</summary>
    [Fact]
    public void TruncatedOrOverDepthPartitionsRefuse()
    {
        using PlatformPartiesProofFixture fixture = new();
        string root = fixture.Options.Mapping.Root;
        Should.Throw<InvalidDataException>(() => PlatformCurrentOwnerSourceInventory.Read(root, Revision(root), DateTimeOffset.UtcNow.AddSeconds(-1)))
            .Message.ShouldContain("budget");
        for (int file = 0; file < 700; file++) Write(root, "flat/" + file + new string('x', 160) + ".cs");
        PlatformPartiesProofFixture.Commit(root);
        string revision = Revision(root);
        Run(root, "ls-tree", "-z", revision + ":flat").OutputTruncated.ShouldBeTrue();
        Should.Throw<InvalidDataException>(() => Read(root, revision)).Message.ShouldContain("ls-tree truncation");
        Directory.Delete(Path.Combine(root, "flat"), true);
        Write(root, string.Join('/', Enumerable.Repeat("deep", 66)) + "/Input.cs");
        PlatformPartiesProofFixture.Commit(root);
        Should.Throw<InvalidDataException>(() => Read(root, Revision(root))).Message.ShouldContain("depth-limit");
    }

    /// <summary>A successful parent listing followed by a missing child object never becomes a partial inventory.</summary>
    [Fact]
    public void FailedChildTreeRefusesAfterSuccessfulParentListing()
    {
        using PlatformPartiesProofFixture fixture = new();
        string root = fixture.Options.Mapping.Root;
        Write(root, "child/Required.props");
        PlatformPartiesProofFixture.Commit(root);
        string revision = Revision(root);
        string tree = PlatformPartiesProofFixture.Git(root, "rev-parse", revision + ":child");
        string objectPath = Path.Combine(root, ".git", "objects", tree[..2], tree[2..]);
        File.Exists(objectPath).ShouldBeTrue();
        File.Delete(objectPath);
        Run(root, "ls-tree", "-z", revision).ExitCode.ShouldBe(0);
        Run(root, "ls-tree", "-z", tree).ExitCode.ShouldNotBe(0);
        string failure = Should.Throw<InvalidDataException>(() => Read(root, revision)).Message;
        failure.ShouldContain("ls-tree exit 128");
        failure.ShouldNotContain(root);
        failure.ShouldNotContain("Required.props");
    }

    /// <summary>NUL names remain literal and working-tree directory links are refused without traversal.</summary>
    [Fact]
    public void LiteralNamesSurviveAndDirectoryLinksRefuse()
    {
        OperatingSystem.IsLinux().ShouldBeTrue();
        using PlatformPartiesProofFixture fixture = new();
        string root = fixture.Options.Mapping.Root;
        string[] names = [" leading.cs", "trailing.cs ", "line\nbreak.json", "tab\tfile.props", ":(glob)*.targets", "linked/Required.cs", "replacement-\ufffd.cs"];
        foreach (string name in names) Write(root, name);
        PlatformPartiesProofFixture.Commit(root);
        string revision = Revision(root);
        IReadOnlyList<string> files = Read(root, revision);
        foreach (string name in names) files.ShouldContain(name);
        string blob = PlatformPartiesProofFixture.Git(root, "rev-parse", revision + ": leading.cs");
        string rawTree = Path.Combine(fixture.Root, "invalid-encoding-tree");
        File.WriteAllBytes(rawTree, [.. Encoding.ASCII.GetBytes("100644 invalid-"), 255, .. Encoding.ASCII.GetBytes(".cs\0"), .. Convert.FromHexString(blob)]);
        string invalidTree = PlatformPartiesProofFixture.Git(root, "hash-object", "-w", "-t", "tree", "--", rawTree);
        Should.Throw<InvalidDataException>(() => Read(root, invalidTree)).Message.ShouldContain("ls-tree encoding");
        string moved = Path.Combine(fixture.Root, "outside-owner");
        Directory.Move(Path.Combine(root, "linked"), moved);
        Directory.CreateSymbolicLink(Path.Combine(root, "linked"), moved);
        Should.Throw<InvalidDataException>(() => Read(root, revision)).Message.ShouldContain("directory-link");
    }

    private static IReadOnlyList<string> Read(string root, string revision)
        => PlatformCurrentOwnerSourceInventory.Read(root, revision, DateTimeOffset.UtcNow.AddMinutes(1));

    private static string Revision(string root) => PlatformPartiesProofFixture.Git(root, "rev-parse", "HEAD");

    private static void Write(string root, string name)
    {
        string path = Path.Combine(root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "controlled tracked input");
    }

    private static CompositionProcessResult Run(string root, params string[] arguments)
        => CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("git", arguments, root, null), TimeSpan.FromSeconds(10), CancellationToken.None).GetAwaiter().GetResult();
}
