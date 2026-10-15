using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class CloudJournalParityTests
{
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
