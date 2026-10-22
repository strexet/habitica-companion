namespace Habitica.WebApp.State;

public enum SpellCastStage {Preparation,AutoEquip,Casting,RestoreGear,RefreshUser,RefreshTasks,RefreshParty,LoadCache,Completed}
public enum SpellFollowupStatus {NotNeeded,Pending,Completed,Failed,Unknown,Skipped}
public sealed record SpellCastOutcome(Guid OperationId,string SpellId,int Requested,int Acknowledged,SpellCastStage Stage,
    bool CastOutcomeUnknown,bool EquipmentOutcomeUnknown,bool Cancelled,SpellFollowupStatus Cleanup,SpellFollowupStatus Refresh,
    int? HttpStatus=null,DateTimeOffset? RetryAtUtc=null)
{
    public bool AllCastsAcknowledged=>Acknowledged==Requested&&!CastOutcomeUnknown;
    public bool HasWarning=>!AllCastsAcknowledged||EquipmentOutcomeUnknown||Cleanup is SpellFollowupStatus.Failed or SpellFollowupStatus.Unknown or SpellFollowupStatus.Skipped||Refresh is SpellFollowupStatus.Failed or SpellFollowupStatus.Unknown;
    public string StageLabel=>Stage switch {SpellCastStage.Preparation=>"preparing",SpellCastStage.AutoEquip=>"equipping spell gear",SpellCastStage.Casting=>"casting",SpellCastStage.RestoreGear=>"restoring gear",SpellCastStage.RefreshUser=>"refreshing account",SpellCastStage.RefreshTasks=>"refreshing tasks",SpellCastStage.RefreshParty=>"refreshing party",SpellCastStage.LoadCache=>"loading saved snapshots",_=>"finished"};
    public static string FollowupLabel(SpellFollowupStatus value)=>value switch {SpellFollowupStatus.NotNeeded=>"not required",SpellFollowupStatus.Pending=>"in progress",SpellFollowupStatus.Completed=>"completed",SpellFollowupStatus.Failed=>"needs attention",SpellFollowupStatus.Unknown=>"unknown; review needed",_=>"not attempted; review needed"};
    public string Summary=>Cancelled&&Acknowledged==0&&!CastOutcomeUnknown&&Stage==SpellCastStage.Preparation
        ?"Casting cancelled before it started."
        :$"Acknowledged {Acknowledged} of {Requested} casts. "+(CastOutcomeUnknown?"One dispatched cast has an unknown outcome; never replay the batch. ":AllCastsAcknowledged?"All casts were acknowledged. ":Cancelled?"Remaining casts cancelled. ":"Remaining casts stopped; no automatic retry. ")
            +$"Stage: {StageLabel}. Gear cleanup: {FollowupLabel(Cleanup)}. Snapshot refresh: {FollowupLabel(Refresh)}."
            +(EquipmentOutcomeUnknown?" Equipment state is uncertain; inspect current gear before another toggle.":"")
            +(RetryAtUtc is {} retry?$" Cooldown until {retry:u}; no cast retry was scheduled.":"");
}
