using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceProjectRemovalTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-20T12:00:00Z");
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "project-removal-v3.json")));
    private static WorkspaceJournalSnapshot Seed(JsonDocument fixture) => new(WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("document").GetRawText()), 10, [], []);

    [Fact]
    public void Portable_v3_approval_hashes_preserve_legacy_defaults_and_bind_every_reference()
    {
        using var fixture = Fixture();
        Assert.Equal(3, (int)WorkspaceOrganizationOperation.Remove);
        foreach (var row in fixture.RootElement.GetProperty("hashes").EnumerateArray())
        {
            var command = WorkspaceTaskApiJson.Deserialize<WorkspaceOrganizationCommand>(row.GetProperty("command").GetRawText());
            Assert.True(WorkspaceOrganizationCommands.IsValid(command)); Assert.Equal(row.GetProperty("payloadHash").GetString(), WorkspaceOrganizationCommands.Hash(command));
        }
        Assert.DoesNotContain("isRemoved", WorkspaceTaskApiJson.Serialize(new WorkspaceProject(Guid.NewGuid(), 1, "Legacy")));
        Assert.DoesNotContain("removal", WorkspaceTaskApiJson.Serialize(new WorkspaceOrganizationPatch(Name: "Legacy")));
    }

    [Fact]
    public void Reviewed_removal_preserves_all_task_history_hierarchy_sections_and_mapping_and_has_one_original_receipt()
    {
        using var fixture = Fixture(); var original = Seed(fixture);
        foreach (var row in fixture.RootElement.GetProperty("hashes").EnumerateArray())
        {
            var command = WorkspaceTaskApiJson.Deserialize<WorkspaceOrganizationCommand>(row.GetProperty("command").GetRawText());
            var impact = WorkspaceProjectRemovalQuery.Evaluate(original.Document, command.EntityId, 10); Assert.True(impact.CanRemove);
            Assert.True(impact.Removal.Matches(original.Document, command.EntityId));
            var accepted = WorkspaceOrganizationEngine.Accept(original, command, Now); Assert.Equal(SubmissionDisposition.Accepted, accepted.Disposition);
            var state = accepted.Snapshot!; var removed = state.Document.Projects.Single(project => project.Id == command.EntityId); Assert.True(removed.IsRemoved && removed.IsArchived);
            Assert.DoesNotContain(WorkspaceOrganization.Projects(state.Document, new()).Items, project => project.Id == command.EntityId);
            Assert.Empty(WorkspaceOrganization.Sections(state.Document, new(), command.EntityId).Items);
            Assert.Equal(WorkspaceTaskApiJson.Serialize(original.Document.Tasks), WorkspaceTaskApiJson.Serialize(state.Document.Tasks));
            Assert.Equal(WorkspaceTaskApiJson.Serialize(original.Document.Sections), WorkspaceTaskApiJson.Serialize(state.Document.Sections));
            Assert.Equal(WorkspaceTaskApiJson.Serialize(original.Document.Projections), WorkspaceTaskApiJson.Serialize(state.Document.Projections));
            Assert.Equal(original.Document.Completions, state.Document.Completions); Assert.Equal(original.Commands, state.Commands); Assert.Equal(original.Document.HabitScores, state.Document.HabitScores);
            Assert.Equal(WorkspaceJournalJson.Serialize(state), WorkspaceJournalJson.Serialize(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(state))));
            Assert.False(WorkspaceProjectRemovalQuery.Evaluate(state.Document, command.EntityId).CanRemove);
            var duplicate = WorkspaceOrganizationEngine.Accept(state, command, Now.AddMinutes(1)); Assert.Equal(SubmissionDisposition.Duplicate, duplicate.Disposition); Assert.Equal(accepted.Entry, duplicate.Entry);
            Assert.Single(state.Document.OrganizationHistory!);
            Assert.Equal("organization_unavailable", WorkspaceOrganizationEngine.Accept(state, command with { Id = Guid.NewGuid(), ExpectedRevision = 11 }, Now).ErrorCode);
        }
    }

    [Fact]
    public void Exact_removed_assignments_are_visually_unassigned_without_clearing_canonical_references()
    {
        using var fixture = Fixture(); var before = Seed(fixture); var project = before.Document.Projects[0]; var task = before.Document.Tasks[0];
        var command = WorkspaceTaskApiJson.Deserialize<WorkspaceOrganizationCommand>(fixture.RootElement.GetProperty("hashes")[0].GetProperty("command").GetRawText());
        Assert.DoesNotContain(task, WorkspacePlanningQuery.Evaluate(before.Document, WorkspacePlanningList.Inbox, new(2026, 10, 20)).Tasks);
        Assert.Contains(task, WorkspaceTaskQuery.Evaluate(before.Document, new(CategoryId: project.CategoryId)).Tasks);
        var state = WorkspaceOrganizationEngine.Accept(before, command, Now).Snapshot!;
        Assert.Contains(task, WorkspacePlanningQuery.Evaluate(state.Document, WorkspacePlanningList.Inbox, new(2026, 10, 20)).Tasks);
        Assert.DoesNotContain(task, WorkspaceTaskQuery.Evaluate(state.Document, new(CategoryId: project.CategoryId)).Tasks);
        Assert.Contains(task, WorkspaceTaskQuery.Evaluate(state.Document, new(ProjectId: project.Id)).Tasks);
        Assert.Equal(project.Id, state.Document.Tasks[0].ProjectId);
        Assert.Equal("invalid_planning_reference", WorkspaceCommandEngine.Accept(state, new(Guid.NewGuid(), state.Document.Id, state.Document.Revision,
            WorkspaceOperation.PlanTask, task.Id, new(Planning: new(ProjectId: project.Id))), Now).ErrorCode);
        var reassigned = WorkspaceCommandEngine.Accept(state, new(Guid.NewGuid(), state.Document.Id, state.Document.Revision,
            WorkspaceOperation.PlanTask, task.Id, new(Planning: new(ProjectId: before.Document.Projects[1].Id, ClearSection: true))), Now);
        Assert.Equal(SubmissionDisposition.Accepted, reassigned.Disposition); Assert.True(WorkspaceJournalValidation.IsValid(reassigned.Snapshot!)); Assert.Empty(reassigned.Entry!.Effects);
    }

    [Fact]
    public void Missing_changed_reordered_or_mixed_approvals_and_tampered_tombstones_never_accept()
    {
        using var fixture = Fixture(); var state = Seed(fixture);
        var command = WorkspaceTaskApiJson.Deserialize<WorkspaceOrganizationCommand>(fixture.RootElement.GetProperty("hashes")[0].GetProperty("command").GetRawText());
        var approval = command.Patch!.Removal!;
        foreach (var invalid in new WorkspaceOrganizationCommand[]
        {
            command with { Patch = null }, command with { Kind = WorkspaceOrganizationKind.Category }, command with { Patch = new(Name: "Mixed", Removal: approval) },
            command with { Patch = new(Removal: new([], approval.SectionIds)) }, command with { Patch = new(Removal: new(approval.TaskIds.Reverse().ToArray(), approval.SectionIds)) },
            command with { Patch = new(Removal: new(approval.TaskIds, [])) }, command with { Patch = new(Removal: new([Guid.NewGuid()], approval.SectionIds)) }
        }) Assert.Equal(SubmissionDisposition.Invalid, WorkspaceOrganizationEngine.Accept(state, invalid, Now).Disposition);
        Assert.Equal(SubmissionDisposition.RevisionConflict, WorkspaceOrganizationEngine.Accept(state, command with { ExpectedRevision = 9 }, Now).Disposition);
        var accepted = WorkspaceOrganizationEngine.Accept(state, command, Now).Snapshot!; var history = accepted.Document.OrganizationHistory!.Single();
        var changed = history.Command with { Patch = new(Removal: new([], approval.SectionIds)) };
        Assert.False(WorkspaceJournalValidation.IsValid(accepted with { Document = accepted.Document with { OrganizationHistory = [history with { Command = changed }] } }));
        Assert.False(WorkspaceJournalValidation.IsValid(accepted with { Document = accepted.Document with { OrganizationHistory = null } }));
        Assert.False(WorkspaceJournalValidation.IsValid(accepted with { Document = accepted.Document with { Projects = accepted.Document.Projects.Select(project => project.Id == command.EntityId ? project with { IsRemoved = false } : project).ToArray() } }));
    }
}
