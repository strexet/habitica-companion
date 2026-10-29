using Habitica.Domain.Workspace;
namespace Habitica.Application.Tests.Workspace;
public sealed class WorkspaceLinkedRecoveryTests
{
    [Theory] [InlineData("todo")] [InlineData("daily")]
    public void Worker_recovery_receipts_round_trip_with_exact_source_hash_and_causal_native_history(string kind)
    {
        var state=WorkspaceJournalJson.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","linked-recovery-"+kind+"-journal-v1.json")));
        Assert.True(WorkspaceJournalValidation.IsValid(state));
        var receipt=Assert.Single(state.Document.LinkedTaskRecoveries!);var completion=Assert.Single(state.Document.Completions);
        Assert.Equal(WorkspaceLinkedRecovery.ContextHash(state.Document.Id,receipt,completion),receipt.ContextHash);
        Assert.Equal(receipt.Id,receipt.RecoveryAttemptId);Assert.NotEqual(receipt.SourceAttemptId,receipt.RecoveryAttemptId);
        Assert.Equal(CompanionTaskState.Active,Assert.Single(state.Document.Tasks).State);
        if(kind=="todo")Assert.Equal(receipt.Id,Assert.Single(state.Document.Restorations!).CommandId);
        else {Assert.Equal(OccurrenceState.Pending,Assert.Single(state.Document.Occurrences).State);Assert.Equal(completion.Revision,Assert.Single(state.Document.OccurrenceCorrections!).PreviousOccurrence.Revision);}
        Assert.True(WorkspaceJournalValidation.IsValid(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(state))));
        Assert.DoesNotContain(state.Commands,entry=>entry.Command.Id==receipt.Id);
    }
    [Theory] [InlineData("todo")] [InlineData("daily")]
    public void Receipt_tampering_missing_native_pair_or_original_dispatch_cannot_establish_recovery(string kind)
    {
        var state=WorkspaceJournalJson.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","linked-recovery-"+kind+"-journal-v1.json")));
        var row=Assert.Single(state.Document.LinkedTaskRecoveries!);
        var changes=new Func<LinkedTaskRecovery,LinkedTaskRecovery>[] {value=>value with {ContextHash=new string('b',64)},value=>value with {SourceAttemptId=Guid.NewGuid()},value=>value with {RecoveryAttemptId=Guid.NewGuid()},value=>value with {AccountId=Guid.NewGuid()},value=>value with {ReviewedRevision=value.ReviewedRevision-1},value=>value with {MappingFingerprint=new string('b',64)},value=>value with {SourceContext=value.SourceContext with {CommandPayloadHash=new string('b',64)}},value=>value with {IsArchived=true}};
        foreach(var change in changes)Assert.False(WorkspaceJournalValidation.IsValid(state with {Document=state.Document with {LinkedTaskRecoveries=[change(row)]}}));
        Assert.False(WorkspaceJournalValidation.IsValid(state with {Events=state.Events!.Where(value=>value.AttemptToken!=row.SourceAttemptId).ToArray()}));
        Assert.False(WorkspaceJournalValidation.IsValid(state with {Document=state.Document with {Restorations=[],OccurrenceCorrections=[]}}));
        Assert.False(WorkspaceJournalValidation.IsValid(state with {Document=state.Document with {LinkedTaskRecoveries=null}}));
        Assert.False(WorkspaceJournalValidation.IsValid(state with {Document=state.Document with {LinkedTaskRecoveries=[row,row with {Id=Guid.NewGuid()}]}}));
    }
}
