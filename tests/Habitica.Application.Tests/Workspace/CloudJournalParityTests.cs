using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class CloudJournalParityTests
{
    [Fact]
    public void ExtendedWorkerJournalRetainsTaskDetailsAndPortablePartialCommandHashes()
    {
        var snapshot = WorkspaceJournalJson.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "cloud-journal-v2.json")));
        Assert.Equal(3, snapshot.Commands.Count); Assert.Equal(3, snapshot.Events!.Count);
        Assert.Equal(1.5, snapshot.Document.Tasks[0].HabiticaDetails!.Difficulty);
        Assert.Equal(HabiticaTaskAttribute.Int, snapshot.Document.Tasks[0].HabiticaDetails!.Attribute);
        Assert.Empty(snapshot.Document.Tasks[0].HabiticaDetails!.Checklist!);
        Assert.Equal(HabiticaTaskAttribute.Per, snapshot.Commands[0].TaskSnapshot!.HabiticaDetails!.Attribute);
        Assert.Equal(2, snapshot.Commands[0].TaskSnapshot!.HabiticaDetails!.Checklist!.Count);
        Assert.All(snapshot.Commands, entry => Assert.Equal(WorkspaceCommandIdentity.Hash(entry.Command), entry.PayloadHash));
        Assert.Equal(WorkspaceAuthorityMode.Cloud, snapshot.Authority!.Mode);
    }

    [Theory]
    [InlineData("cloud-journal-v1.json", true)]
    [InlineData("cloud-journal-legacy.json", false)]
    public void Worker_export_preserves_CSharp_domain_journal_and_payload_identity(string fileName, bool includesAcceptedTask)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", fileName));
        var snapshot = WorkspaceJournalJson.Deserialize(json);
        Assert.Equal(includesAcceptedTask, snapshot.Commands[0].TaskSnapshot is not null);
        Assert.Single(snapshot.Commands); Assert.Single(snapshot.Events!); Assert.Single(snapshot.Document.Tasks);
        Assert.Equal(WorkspaceCommandIdentity.Hash(snapshot.Commands[0].Command), snapshot.Commands[0].PayloadHash);
        Assert.Equal(2, snapshot.Document.Revision); Assert.Equal(2, snapshot.StoreVersion);
        Assert.Equal(ProjectionCommandState.Pending, snapshot.Commands[0].ProjectionState);
        Assert.True(WorkspaceJournalValidation.IsValid(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(snapshot))));
    }
}
