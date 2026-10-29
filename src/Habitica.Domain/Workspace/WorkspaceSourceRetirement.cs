using System.Text.Json;
using System.Text.Json.Serialization;

namespace Habitica.Domain.Workspace;

public enum SourceRetirementOutcome { OriginalReceipt, AlreadyAbsent, ObservedAbsentAfterUnknown }
/// <summary>Retained portable audit. Importing a receipt grants no native execution authority.</summary>
public sealed record WorkspaceSourceRetirement(Guid Id,long Revision,Guid ConversionId,Guid SourceTaskId,long SourceRevision,long ReviewedRevision,
    Guid AccountId,string HabiticaTaskId,string ContextHash,[property:JsonRequired] SourceRetirementOutcome Outcome,Guid? NativeAttemptId,
    DateTimeOffset VerifiedAtUtc,DateTimeOffset RetiredAtUtc,bool IsArchived=false) : WorkspaceEntity(Id,Revision,IsArchived);

public static class WorkspaceSourceRetirements
{
    public static bool IsValid(WorkspaceDocument document)
    {
        if(document.SourceRetirements is null)return true;
        if(document.SourceRetirements.Count>1000)return false;
        var sources=new HashSet<Guid>();var conversions=new HashSet<Guid>();
        var commands=new HashSet<Guid>((document.OrganizationHistory??[]).Select(row=>row.Command.Id)
            .Concat((document.ChecklistConversions??[]).SelectMany(row=>row.Receipts.Select(receipt=>receipt.Command.Id)))
            .Concat(document.Completions.Select(row=>row.CommandId)).Concat(document.HabitScores.Select(row=>row.CommandId)).Concat((document.Restorations??[]).Select(row=>row.CommandId)).Concat((document.Unarchives??[]).Select(row=>row.CommandId)).Concat((document.OccurrenceCorrections??[]).Select(row=>row.CommandId)));
        foreach(var row in document.SourceRetirements)
        {
            if(row is null||row.Id==Guid.Empty||!commands.Add(row.Id)||row.IsArchived||!sources.Add(row.SourceTaskId)||!conversions.Add(row.ConversionId)
                ||row.AccountId==Guid.Empty||document.TagLedger is {} ledger&&ledger.AccountId!=row.AccountId||!Enum.IsDefined(row.Outcome)
                ||!Guid.TryParseExact(row.HabiticaTaskId,"D",out var remote)||remote==Guid.Empty||row.ContextHash is null||!System.Text.RegularExpressions.Regex.IsMatch(row.ContextHash,"^[a-f0-9]{64}$")
                ||row.NativeAttemptId==Guid.Empty||(row.Outcome==SourceRetirementOutcome.AlreadyAbsent?row.NativeAttemptId is not null:row.NativeAttemptId is null)
                ||row.VerifiedAtUtc==default||row.VerifiedAtUtc.Offset!=TimeSpan.Zero||row.RetiredAtUtc==default||row.RetiredAtUtc.Offset!=TimeSpan.Zero||row.VerifiedAtUtc>row.RetiredAtUtc
                ||row.SourceRevision<1||row.ReviewedRevision<row.SourceRevision||row.ReviewedRevision>=row.Revision||row.Revision>document.Revision)return false;
            var conversion=document.ChecklistConversions?.SingleOrDefault(value=>value.Id==row.ConversionId);
            var source=document.Tasks.SingleOrDefault(task=>task.Id==row.SourceTaskId);
            var mapping=document.Projections.SingleOrDefault(value=>value.TaskId==row.SourceTaskId);
            if(conversion is null||conversion.SourceTaskId!=row.SourceTaskId||conversion.SourceRevision!=row.SourceRevision||conversion.Revision>row.ReviewedRevision
                ||conversion.Items.Any(item=>item.TargetTaskId is null)||source is not {Kind:CompanionTaskKind.Todo,IsArchived:true}
                ||source.Revision!=row.Revision||source.UpdatedAtUtc!=row.RetiredAtUtc
                ||mapping is not {State:ProjectionState.Linked,IsArchived:true,Fingerprint:{Length:64}}||mapping.HabiticaTaskId!=row.HabiticaTaskId||mapping.Revision!=row.Revision)return false;
            // Archival must preserve every source field except lifecycle revision, timestamp and archive flag.
            var restored=source with {Revision=conversion.SourceSnapshot.Revision,UpdatedAtUtc=conversion.SourceSnapshot.UpdatedAtUtc,IsArchived=conversion.SourceSnapshot.IsArchived};
            if(JsonSerializer.Serialize(restored)!=JsonSerializer.Serialize(conversion.SourceSnapshot))return false;
        }
        return true;
    }
}
