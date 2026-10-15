using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceTaskDetailsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-15T00:00:00Z");
    [Fact]
    public void TypedDetailsPersistIndependentlyOfUrgencyAndPartialUpdatesPreserveUnspecifiedSettings()
    {
        var empty = new WorkspaceJournalSnapshot(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, []);
        var checklist = new[] { new TaskChecklistItem(Guid.NewGuid(), "Résumé ☕") };
        var details = new HabiticaTaskDetails(1.5, HabiticaTaskAttribute.Per, checklist);
        var create = new WorkspaceCommand(Guid.NewGuid(), empty.Document.Id, 1, WorkspaceOperation.CreateTodo, Guid.NewGuid(), new(Text: "Task details", Priority: CompanionPriority.Urgent, HabiticaDetails: details), ProjectToHabitica: true);
        var accepted = WorkspaceCommandEngine.Accept(empty, create, Now);
        Assert.Equal(SubmissionDisposition.Accepted, accepted.Disposition);
        var snapshot = WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(accepted.Snapshot!));
        Assert.Equal(CompanionPriority.Urgent, snapshot.Document.Tasks[0].Priority);
        Assert.Equal(1.5, snapshot.Document.Tasks[0].HabiticaDetails!.Difficulty);
        var changed = WorkspaceCommandEngine.Accept(snapshot, new(Guid.NewGuid(), empty.Document.Id, snapshot.Document.Revision, WorkspaceOperation.UpdateTask, create.TaskId, new(HabiticaDetails: new(Attribute: HabiticaTaskAttribute.Int))), Now).Snapshot!;
        Assert.Equal(1.5, changed.Document.Tasks[0].HabiticaDetails!.Difficulty);
        Assert.Equal(HabiticaTaskAttribute.Int, changed.Document.Tasks[0].HabiticaDetails!.Attribute);
        Assert.Equal(checklist, changed.Document.Tasks[0].HabiticaDetails!.Checklist);
        Assert.Equal(HabiticaTaskAttribute.Per, changed.Commands[0].TaskSnapshot!.HabiticaDetails!.Attribute);
        var cleared = WorkspaceCommandEngine.Accept(changed, new(Guid.NewGuid(), empty.Document.Id, changed.Document.Revision, WorkspaceOperation.UpdateTask, create.TaskId, new(HabiticaDetails: new(Checklist: []))), Now).Snapshot!;
        Assert.Empty(cleared.Document.Tasks[0].HabiticaDetails!.Checklist!);
        Assert.Equal(1.5, cleared.Document.Tasks[0].HabiticaDetails!.Difficulty);
        Assert.True(WorkspaceJournalValidation.IsValid(cleared));
    }
    [Fact]
    public void InvalidDetailsAreRejectedBeforeAcceptanceAndInvalidImportedIntentCannotPassValidation()
    {
        var empty = new WorkspaceJournalSnapshot(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, []);
        var item = new TaskChecklistItem(Guid.NewGuid(), "Check");
        foreach (var details in new HabiticaTaskDetails[] { new(3), new(double.NaN), new(Attribute: (HabiticaTaskAttribute)99), new(Checklist: [item, item]), new(Checklist: [item with { Text = "" }]), new(Checklist: [item with { Text = new string('x', 257) }]), new(Checklist: [item with { Id = Guid.Empty }]), new() })
        {
            var result = WorkspaceCommandEngine.Accept(empty, new(Guid.NewGuid(), empty.Document.Id, 1, WorkspaceOperation.CreateTodo, Guid.NewGuid(), new(Text: "Reject", HabiticaDetails: details)), Now);
            Assert.Equal(SubmissionDisposition.Invalid, result.Disposition); Assert.Empty(result.Snapshot!.Commands);
        }
    }
}
