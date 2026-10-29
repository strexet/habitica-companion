using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Habitica.Domain.Workspace;

public enum ChecklistConversionMode { Create, Reuse, Defer }
public enum ChecklistConversionState { Active, CompletedArchived }
public sealed record ChecklistConversionItem(Guid SourceItemId, [property:JsonRequired] ChecklistConversionMode Mode,
    Guid? TargetTaskId, [property:JsonRequired] ChecklistConversionState DesiredState, string? Notes = null, HabiticaTaskDetails? GameDetails = null);
public sealed record ChecklistConversionCommand(Guid Id,Guid WorkspaceId,long ExpectedRevision,Guid ConversionId,Guid SourceTaskId,
    long SourceRevision,Guid ProjectId,string ProjectName,IReadOnlyList<ChecklistConversionItem> Items);
public sealed record ChecklistConversionBinding(Guid SourceItemId,Guid? TargetTaskId,ChecklistConversionState DesiredState,
    [property:JsonRequired] bool CreatedByConversion,string? AddedNotes,CompanionTask? OriginalTarget,[property:JsonRequired] bool HistoricalTimeUncertain);
public sealed record ChecklistConversionReceipt(ChecklistConversionCommand Command,string PayloadHash,DateTimeOffset AcceptedAtUtc,long ResultRevision);
public sealed record WorkspaceChecklistConversion(Guid Id,long Revision,Guid SourceTaskId,long SourceRevision,CompanionTask SourceSnapshot,
    HabiticaProjection? SourceProjection,Guid ProjectId,IReadOnlyList<ChecklistConversionBinding> Items,IReadOnlyList<ChecklistConversionReceipt> Receipts,
    bool IsArchived=false) : WorkspaceEntity(Id,Revision,IsArchived);

public static class WorkspaceChecklistConversions
{
    public static bool ValidCommand(ChecklistConversionCommand command) => command.Id!=Guid.Empty&&command.WorkspaceId!=Guid.Empty
        &&command.ConversionId!=Guid.Empty&&command.SourceTaskId!=Guid.Empty&&command.ProjectId!=Guid.Empty
        &&command.ExpectedRevision is >=1 and <WorkspaceDocument.MaxRevision&&command.SourceRevision>=1&&command.SourceRevision<=command.ExpectedRevision
        &&!string.IsNullOrWhiteSpace(command.ProjectName)&&command.ProjectName.Length<=256&&command.Items is {Count:>0 and <=100}
        &&command.Items.All(item=>item is not null&&item.SourceItemId!=Guid.Empty&&Enum.IsDefined(item.Mode)&&Enum.IsDefined(item.DesiredState)
            &&item.TargetTaskId!=Guid.Empty&&item.TargetTaskId!=command.SourceTaskId&&item.Notes?.Length is not >10000
            &&(item.Mode==ChecklistConversionMode.Defer?item.TargetTaskId is null&&item.Notes is null&&item.GameDetails is null:item.TargetTaskId is not null)
            &&(item.Mode==ChecklistConversionMode.Create?item.DesiredState==ChecklistConversionState.Active&&HabiticaTaskDetails.IsValid(item.GameDetails)
                &&item.GameDetails is {Difficulty:not null,Attribute:not null,Up:null,Down:null}&&item.GameDetails.Checklist is not {Count:>0}:item.GameDetails is null)
            &&(item.Mode!=ChecklistConversionMode.Reuse||item.Notes is null))
        &&command.Items.Select(item=>item.SourceItemId).Distinct().Count()==command.Items.Count
        &&command.Items.Where(item=>item.TargetTaskId is not null).Select(item=>item.TargetTaskId).Distinct().Count()==command.Items.Count(item=>item.TargetTaskId is not null);

    public static string Hash(ChecklistConversionCommand command)
    {
        static string Name<T>(T value) where T:struct,Enum=>JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
        var fields=new List<string?> {command.Id.ToString("D"),command.WorkspaceId.ToString("D"),command.ExpectedRevision.ToString(CultureInfo.InvariantCulture),
            command.ConversionId.ToString("D"),command.SourceTaskId.ToString("D"),command.SourceRevision.ToString(CultureInfo.InvariantCulture),command.ProjectId.ToString("D"),command.ProjectName,command.Items.Count.ToString(CultureInfo.InvariantCulture)};
        foreach(var item in command.Items){fields.AddRange([item.SourceItemId.ToString("D"),Name(item.Mode),item.TargetTaskId?.ToString("D"),Name(item.DesiredState),item.Notes,
            item.GameDetails?.Difficulty?.ToString("R",CultureInfo.InvariantCulture),item.GameDetails?.Attribute is {} attribute?Name(attribute):null]);}
        var text=new StringBuilder("companion-checklist-conversion-v1\n");foreach(var field in fields)text.Append(field is null?"-1:":Encoding.UTF8.GetByteCount(field).ToString(CultureInfo.InvariantCulture)+":"+field);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }

    private static bool SnapshotValid(CompanionTask task,long revision) => task.Id!=Guid.Empty&&task.Revision>=1&&task.Revision<=revision&&task.Kind==CompanionTaskKind.Todo
        &&Enum.IsDefined(task.State)&&Enum.IsDefined(task.Priority)&&!string.IsNullOrWhiteSpace(task.Text)&&task.Text.Length<=2048&&task.Notes?.Length is not >10000
        &&task.CreatedAtUtc!=default&&task.CreatedAtUtc.Offset==TimeSpan.Zero&&task.UpdatedAtUtc.Offset==TimeSpan.Zero&&task.CreatedAtUtc<=task.UpdatedAtUtc
        &&task.Recurrence is null&&HabiticaTaskDetails.IsValid(task.HabiticaDetails)&&task.HabiticaDetails?.Up is null&&task.HabiticaDetails?.Down is null
        &&(task.ExecutionStage is null||Enum.IsDefined(task.ExecutionStage.Value))&&task.CategoryId!=Guid.Empty&&task.ProjectId!=Guid.Empty&&task.SectionId!=Guid.Empty&&task.ParentTaskId!=Guid.Empty
        &&(task.LabelIds is null||task.LabelIds.All(id=>id!=Guid.Empty)&&task.LabelIds.Distinct().Count()==task.LabelIds.Count)
        &&(task.DependsOn is null||task.DependsOn.All(id=>id!=Guid.Empty)&&task.DependsOn.Distinct().Count()==task.DependsOn.Count)
        &&(task.ReviewAfterUtc is null||task.ReviewAfterUtc.Value!=default&&task.ReviewAfterUtc.Value.Offset==TimeSpan.Zero)
        &&(task.StaleSinceUtc is null||task.StaleSinceUtc.Value!=default&&task.StaleSinceUtc.Value.Offset==TimeSpan.Zero)&&task.PlannedDate!=DateOnly.MinValue&&task.Deadline!=DateOnly.MinValue;

    public static bool IsValid(WorkspaceDocument document)
    {
        if(document.ChecklistConversions is null)return true;
        if(document.ChecklistConversions.Count>1000)return false;
        var targetOwners=new HashSet<Guid>();
        var sources=new HashSet<Guid>();var commands=new HashSet<Guid>(document.OrganizationHistory?.Select(row=>row.Command.Id)??[]);
        foreach(var taskCommand in document.Completions.Select(row=>row.CommandId).Concat(document.HabitScores.Select(row=>row.CommandId)).Concat((document.Restorations??[]).Select(row=>row.CommandId)).Concat((document.Unarchives??[]).Select(row=>row.CommandId)).Concat((document.OccurrenceCorrections??[]).Select(row=>row.CommandId)))commands.Add(taskCommand);
        foreach(var conversion in document.ChecklistConversions)
        {
            if(conversion is null||!sources.Add(conversion.SourceTaskId)||conversion.SourceRevision<1||conversion.SourceRevision>conversion.Revision
                ||conversion.SourceSnapshot is not {Kind:CompanionTaskKind.Todo} source||source.Id!=conversion.SourceTaskId||source.Revision!=conversion.SourceRevision
                ||!SnapshotValid(source,document.Revision)||!document.Tasks.Any(task=>task.Id==source.Id)||!document.Projects.Any(project=>project.Id==conversion.ProjectId)
                ||string.IsNullOrWhiteSpace(source.Text)||source.Text.Length>2048||source.Notes?.Length>10000||!Enum.IsDefined(source.Priority)||!Enum.IsDefined(source.State)
                ||source.CreatedAtUtc==default||source.CreatedAtUtc.Offset!=TimeSpan.Zero||source.UpdatedAtUtc.Offset!=TimeSpan.Zero||source.CreatedAtUtc>source.UpdatedAtUtc
                ||!HabiticaTaskDetails.IsValid(source.HabiticaDetails)||source.HabiticaDetails?.Checklist is not {Count:>0} checklist
                ||conversion.Items is null||!checklist.Select(item=>item.Id).SequenceEqual(conversion.Items.Select(item=>item.SourceItemId))
                ||conversion.Items.Any(item=>item is null||!Enum.IsDefined(item.DesiredState)||item.AddedNotes?.Length>10000
                    ||item.HistoricalTimeUncertain!=(item.DesiredState==ChecklistConversionState.CompletedArchived)
                    ||item.TargetTaskId==source.Id||item.TargetTaskId==Guid.Empty
                    ||item.TargetTaskId is {} target&&(!document.Tasks.Any(task=>task.Id==target&&task.Kind==CompanionTaskKind.Todo)||item.OriginalTarget is not {} original||original.Id!=target||!SnapshotValid(original,document.Revision)
                        ||string.IsNullOrWhiteSpace(original.Text)||original.Text.Length>2048||original.Notes?.Length>10000||!Enum.IsDefined(original.Priority)||!Enum.IsDefined(original.State)
                        ||original.CreatedAtUtc==default||original.CreatedAtUtc.Offset!=TimeSpan.Zero||original.UpdatedAtUtc.Offset!=TimeSpan.Zero||original.CreatedAtUtc>original.UpdatedAtUtc)
                    ||item.TargetTaskId is null&&(item.CreatedByConversion||item.OriginalTarget is not null))
                ||conversion.Items.Where(item=>item.TargetTaskId is not null).Select(item=>item.TargetTaskId).Distinct().Count()!=conversion.Items.Count(item=>item.TargetTaskId is not null)
                ||conversion.Receipts is not {Count:>0 and <=1000})return false;
            long revision=0;var bound=new Dictionary<Guid,(Guid Target,ChecklistConversionState State,bool Created,string? Notes)>();
            foreach(var receipt in conversion.Receipts)
            {
                var command=receipt?.Command;if(command is null||!ValidCommand(command)||command.WorkspaceId!=document.Id||command.ConversionId!=conversion.Id
                    ||command.SourceTaskId!=conversion.SourceTaskId||command.SourceRevision!=conversion.SourceRevision||command.ProjectId!=conversion.ProjectId
                    ||!command.Items.Select(item=>item.SourceItemId).SequenceEqual(checklist.Select(item=>item.Id))||!commands.Add(command.Id)
                    ||receipt!.PayloadHash!=Hash(command)||receipt.AcceptedAtUtc==default||receipt.AcceptedAtUtc.Offset!=TimeSpan.Zero
                    ||command.ExpectedRevision<revision||receipt.ResultRevision<=command.ExpectedRevision||receipt.ResultRevision<=revision||receipt.ResultRevision>conversion.Revision)return false;
                foreach(var item in command.Items)
                {
                    if(bound.TryGetValue(item.SourceItemId,out var existing))
                    {
                        if(item.TargetTaskId!=existing.Target||item.DesiredState!=existing.State||item.Mode==ChecklistConversionMode.Defer||item.Notes is not null&&item.Notes!=existing.Notes)return false;
                    }
                    else if(item.TargetTaskId is {} target)bound[item.SourceItemId]=(target,item.DesiredState,item.Mode==ChecklistConversionMode.Create,item.Notes);
                }
                revision=receipt.ResultRevision;
            }
            if(revision!=conversion.Revision)return false;
            foreach(var binding in conversion.Items)
            {
                if(bound.TryGetValue(binding.SourceItemId,out var identity))
                {if(binding.TargetTaskId!=identity.Target||binding.DesiredState!=identity.State||binding.CreatedByConversion!=identity.Created||binding.AddedNotes!=identity.Notes||!targetOwners.Add(identity.Target))return false;}
                else if(binding.TargetTaskId is not null||conversion.Receipts[^1].Command.Items.Single(item=>item.SourceItemId==binding.SourceItemId).DesiredState!=binding.DesiredState)return false;
            }
            if(conversion.SourceProjection is {} projection&&(projection.TaskId!=source.Id||projection.Id==Guid.Empty||projection.Revision<1||projection.Revision>document.Revision||!Enum.IsDefined(projection.State)
                ||projection.Fingerprint is not null&&!System.Text.RegularExpressions.Regex.IsMatch(projection.Fingerprint,"^[a-f0-9]{64}$")
                ||projection.CorrelationAlias is not null&&projection.CorrelationAlias!=$"companion_{document.Id:N}_{source.Id:N}"
                ||projection.HabiticaTaskId is {} remote&&(!Guid.TryParseExact(remote,"D",out var remoteId)||remoteId==Guid.Empty)))return false;
        }
        return true;
    }
}
