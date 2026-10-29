namespace Habitica.Domain.Workspace;

public static class WorkspaceOrganizationHistory
{
    public static bool IsValid(WorkspaceDocument document)
    {
        if(document.OrganizationHistory is null)return true;
        var ids=new HashSet<Guid>();var latest=new Dictionary<Guid,WorkspaceOrganizationSnapshot>();long previousRevision=0;
        var taskCommands=document.Completions.Select(row=>row.CommandId).Concat(document.HabitScores.Select(row=>row.CommandId)).Concat((document.ReviewEvents??[]).Select(row=>row.CommandId)).Concat((document.Restorations??Array.Empty<TaskRestoration>()).Select(row=>row.CommandId)).Concat((document.Unarchives??[]).Select(row=>row.CommandId)).Concat((document.OccurrenceCorrections??[]).Select(row=>row.CommandId)).ToHashSet();
        var activityRevisions=document.Completions.Select(row=>row.Revision).Concat(document.HabitScores.Select(row=>row.Revision)).Concat((document.ReviewEvents??[]).Select(row=>row.Revision)).Concat((document.Restorations??Array.Empty<TaskRestoration>()).Select(row=>row.Revision)).Concat((document.Unarchives??[]).Select(row=>row.Revision)).Concat((document.OccurrenceCorrections??[]).Select(row=>row.Revision)).ToHashSet();
        var categories=document.Categories.Select(row=>row.Id).ToHashSet();var projects=document.Projects.Select(row=>row.Id).ToHashSet();var tasks=document.Tasks.Select(row=>row.Id).ToHashSet();var labels=document.Labels.Select(row=>row.Id).ToHashSet();
        var sectionReferences=document.Sections.Select(row=>(row.Id,row.ProjectId)).ToHashSet();
        foreach(var entry in document.OrganizationHistory)
        {
            if(entry is null||entry.Command is null||entry.Snapshot is null)return false;
            var command=entry.Command;var snapshot=entry.Snapshot;var patch=command.Patch;
            if(!WorkspaceOrganizationCommands.IsValid(command)||command.WorkspaceId!=document.Id||!ids.Add(command.Id)||taskCommands.Contains(command.Id)
                ||entry.PayloadHash is null||entry.PayloadHash.Length!=64||entry.PayloadHash.Any(c=>!(c is >= '0' and <= '9' or >= 'a' and <= 'f'))
                ||entry.AcceptedAtUtc==default||entry.AcceptedAtUtc.Offset!=TimeSpan.Zero||snapshot.Id!=command.EntityId||snapshot.Kind!=command.Kind
                ||snapshot.Revision!=command.ExpectedRevision+1||snapshot.Revision>document.Revision||snapshot.Revision<=previousRevision||activityRevisions.Contains(snapshot.Revision)
                ||string.IsNullOrWhiteSpace(snapshot.Name)||snapshot.Name.Length>256||snapshot.Position<0
                ||snapshot.IsRemoved!=(command.Operation==WorkspaceOrganizationOperation.Remove)&&!WorkspaceProjectAliases.IsRetainedClear(command)
                ||snapshot.Alias is {} alias&&(snapshot.Kind!=WorkspaceOrganizationKind.Project||!WorkspaceProjectAliases.IsCanonical(alias)))return false;
            if(patch?.Removal is {} removal&&(removal.TaskIds.Any(id=>!tasks.Contains(id))||removal.SectionIds.Any(id=>!sectionReferences.Contains((id,command.EntityId)))))return false;
            previousRevision=snapshot.Revision;
            if(snapshot.Kind!=WorkspaceOrganizationKind.Project&&snapshot.CategoryId is not null||snapshot.CategoryId is {} category&&!categories.Contains(category)
                ||snapshot.Kind!=WorkspaceOrganizationKind.Section&&(snapshot.ProjectId is not null||snapshot.Position!=0)
                ||snapshot.Kind==WorkspaceOrganizationKind.Section&&(snapshot.ProjectId is not {} project||!projects.Contains(project))
                ||snapshot.Kind!=WorkspaceOrganizationKind.SavedTaskView&&snapshot.Query is not null
                ||snapshot.Kind==WorkspaceOrganizationKind.SavedTaskView&&(snapshot.Query is null||!snapshot.Query.IsValid()
                    ||snapshot.Query.Filter.CategoryId is {} viewCategory&&!categories.Contains(viewCategory)
                    ||snapshot.Query.Filter.ProjectId is {} viewProject&&!projects.Contains(viewProject)
                    ||snapshot.Query.Filter.LabelId is {} viewLabel&&!labels.Contains(viewLabel))
                ||snapshot.Kind!=WorkspaceOrganizationKind.Goal&&snapshot.TaskIds is not null
                ||snapshot.Kind==WorkspaceOrganizationKind.Goal&&(snapshot.TaskIds is null||snapshot.TaskIds.Count>100000||snapshot.TaskIds.Distinct().Count()!=snapshot.TaskIds.Count||snapshot.TaskIds.Any(id=>!tasks.Contains(id))))return false;
            if(latest.TryGetValue(snapshot.Id,out var prior))
            {
                if(prior.Kind!=snapshot.Kind||(prior.IsRemoved||prior.IsArchived&&command.Operation!=WorkspaceOrganizationOperation.Remove)&&!WorkspaceProjectAliases.IsRetainedClear(command)||command.Operation==WorkspaceOrganizationOperation.Create||!WorkspaceOrganizationCommands.Apply(command,prior).Equivalent(snapshot))return false;
            }
            else if(command.Operation==WorkspaceOrganizationOperation.Create)
            {
                if(!WorkspaceOrganizationCommands.Apply(command,null).Equivalent(snapshot))return false;
            }
            else if(command.Operation is WorkspaceOrganizationOperation.Archive or WorkspaceOrganizationOperation.Remove ? !snapshot.IsArchived : snapshot.IsArchived&&!WorkspaceProjectAliases.IsRetainedClear(command)
                ||patch!.Alias is not null&&WorkspaceProjectAliases.Normalize(patch.Alias)!=snapshot.Alias||patch!.ClearAlias&&snapshot.Alias is not null
                ||patch!.Name is not null&&patch.Name!=snapshot.Name||patch.CategoryId is not null&&patch.CategoryId!=snapshot.CategoryId||patch.ClearCategory&&snapshot.CategoryId is not null
                ||patch.Query is not null&&patch.Query!=snapshot.Query
                ||patch.Position is not null&&patch.Position!=snapshot.Position||patch.TaskIds is not null&&(snapshot.TaskIds is null||!patch.TaskIds.SequenceEqual(snapshot.TaskIds)))return false;
            if(snapshot.Alias is {} reserved&&latest.Values.Any(row=>row.Id!=snapshot.Id&&row.Alias==reserved))return false;
            latest[snapshot.Id]=snapshot;
        }
        return latest.Values.All(snapshot=>snapshot.Equivalent(WorkspaceOrganizationSnapshot.From(document,snapshot.Kind,snapshot.Id)));
    }
}
