using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record OrganizationSubmission(SubmissionDisposition Disposition, WorkspaceJournalSnapshot? Snapshot,
    WorkspaceOrganizationEntry? Entry, string? ErrorCode = null)
{
    public bool? SavedInCompanion => Disposition == SubmissionDisposition.Unavailable ? null : Disposition is SubmissionDisposition.Accepted or SubmissionDisposition.Duplicate;
}

public static class WorkspaceOrganizationEngine
{
    public static OrganizationSubmission Accept(WorkspaceJournalSnapshot snapshot, WorkspaceOrganizationCommand command, DateTimeOffset now)
    {
        OrganizationSubmission Reject(SubmissionDisposition disposition, string code) => new(disposition,snapshot,null,code);
        if(!WorkspaceJournalValidation.IsValid(snapshot)) return Reject(SubmissionDisposition.Invalid,"invalid_workspace");
        if(!WorkspaceOrganizationCommands.IsValid(command)||command.WorkspaceId!=snapshot.Document.Id||now==default||now.Offset!=TimeSpan.Zero)
            return Reject(SubmissionDisposition.Invalid,"invalid_command");
        var document=snapshot.Document;var hash=WorkspaceOrganizationCommands.Hash(command);
        var existing=document.OrganizationHistory?.FirstOrDefault(entry=>entry.Command.Id==command.Id);
        if(existing is not null) return existing.PayloadHash==hash ? new(SubmissionDisposition.Duplicate,snapshot,existing) : Reject(SubmissionDisposition.IdempotencyConflict,"command_id_reused");
        if(snapshot.Commands.Any(entry=>entry.Command.Id==command.Id)||document.Completions.Any(row=>row.CommandId==command.Id)
            ||document.HabitScores.Any(row=>row.CommandId==command.Id)||(document.Restorations??[]).Any(row=>row.CommandId==command.Id))
            return Reject(SubmissionDisposition.IdempotencyConflict,"command_id_reused");
        if(snapshot.Authority?.Mode is WorkspaceAuthorityMode.Frozen or WorkspaceAuthorityMode.Cloud)
            return Reject(SubmissionDisposition.Invalid,"workspace_owned_elsewhere");
        if(command.ExpectedRevision!=document.Revision) return Reject(SubmissionDisposition.RevisionConflict,"stale_revision");
        if(document.Revision>=WorkspaceDocument.MaxRevision||snapshot.StoreVersion>=WorkspaceDocument.MaxRevision)
            return Reject(SubmissionDisposition.Invalid,"revision_exhausted");
        var current=WorkspaceOrganizationSnapshot.From(document,command.Kind,command.EntityId);
        if(command.Operation==WorkspaceOrganizationOperation.Create ? current is not null : current is null||current.IsArchived)
            return Reject(SubmissionDisposition.Invalid,"organization_unavailable");
        var patch=command.Patch;
        if(patch?.CategoryId is {} category&&!document.Categories.Any(row=>row.Id==category&&!row.IsArchived)
            ||patch?.ProjectId is {} project&&!document.Projects.Any(row=>row.Id==project&&!row.IsArchived)
            ||patch?.TaskIds is {} tasks&&tasks.Any(id=>!document.Tasks.Any(task=>task.Id==id&&!task.IsArchived&&task.Kind==CompanionTaskKind.Todo)))
            return Reject(SubmissionDisposition.Invalid,"invalid_organization_reference");
        var accepted=WorkspaceOrganizationCommands.Apply(command,current);
        static IReadOnlyList<T> Replace<T>(IReadOnlyList<T> rows,T value,bool create) where T:WorkspaceEntity => create ? rows.Append(value).ToArray() : rows.Select(row=>row.Id==value.Id?value:row).ToArray();
        var create=command.Operation==WorkspaceOrganizationOperation.Create;
        var result=command.Kind switch
        {
            WorkspaceOrganizationKind.Category=>document with { Categories=Replace(document.Categories,new WorkspaceCategory(accepted.Id,accepted.Revision,accepted.Name,accepted.IsArchived),create) },
            WorkspaceOrganizationKind.Project=>document with { Projects=Replace(document.Projects,new WorkspaceProject(accepted.Id,accepted.Revision,accepted.Name,accepted.CategoryId,accepted.IsArchived),create) },
            WorkspaceOrganizationKind.Section=>document with { Sections=Replace(document.Sections,new WorkspaceSection(accepted.Id,accepted.Revision,accepted.Name,accepted.ProjectId!.Value,accepted.Position,accepted.IsArchived),create) },
            WorkspaceOrganizationKind.Label=>document with { Labels=Replace(document.Labels,new WorkspaceLabel(accepted.Id,accepted.Revision,accepted.Name,accepted.IsArchived),create) },
            WorkspaceOrganizationKind.Goal=>document with { Goals=Replace(document.Goals,new WorkspaceGoal(accepted.Id,accepted.Revision,accepted.Name,accepted.TaskIds!,accepted.IsArchived),create) },
            _=>throw new InvalidOperationException()
        };
        var entry=new WorkspaceOrganizationEntry(command,hash,now,accepted);
        result=result with { Revision=accepted.Revision,OrganizationHistory=(document.OrganizationHistory??[]).Append(entry).ToArray() };
        var next=snapshot with { Document=result,StoreVersion=snapshot.StoreVersion+1 };
        return WorkspaceJournalValidation.IsValid(next) ? new(SubmissionDisposition.Accepted,next,entry) : Reject(SubmissionDisposition.Invalid,"invalid_result");
    }
}
