using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Notifications;

public static class ReminderIdentity
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) }
    };
    public static bool Valid(ReminderIntent value)
    {
        if(value.Operation is not ("set" or "cancel")||value.TaskId==Guid.Empty||value.ScheduleId==Guid.Empty||value.ExpectedRevision<0||value.ExpectedRevision>=WorkspaceDocument.MaxRevision
            ||(value.ScheduleId is null)!=(value.ExpectedRevision==0)||!WorkspaceTaskReview.ValidZone(value.TimeZoneId)||value.ConsentVersion!="scheduled-reminders-v1"||value.Targets is null)return false;
        if(value.Operation=="cancel")return value.ScheduleId is not null&&value.ReviewAfterUtc is null&&value.Repeat is null&&value.Targets.Count==0;
        if(value.ReviewAfterUtc is not {} cue||cue==default||cue.Offset!=TimeSpan.Zero||value.Targets.Count is <1 or >4||value.Targets.Any(t=>t is null||t.DeviceId==Guid.Empty||t.SubscriptionId==Guid.Empty||t.Revision is <1 or >WorkspaceDocument.MaxRevision)
            ||value.Targets.Select(t=>t.DeviceId).Distinct().Count()!=value.Targets.Count)return false;
        return value.Repeat is not {} repeat||RecurrenceCalendar.IsStructurallyValid(repeat.Rule)&&repeat.Rule.TimeZoneId==value.TimeZoneId
            &&System.Text.RegularExpressions.Regex.IsMatch(repeat.Time??"","^(?:[01][0-9]|2[0-3]):[0-5][0-9]$")&&repeat.Gap=="skip"&&repeat.Overlap is "earlier" or "later" or "skip"
            &&repeat.Through>=repeat.Rule.StartDate&&repeat.Through.DayNumber-repeat.Rule.StartDate.DayNumber<366;
    }
    // Match JSON.stringify's validated ASCII-only field order, including explicit nulls.
    public static object Wire(ReminderIntent value)=>new {value.Operation,value.ScheduleId,value.ExpectedRevision,value.TaskId,ReviewAfterUtc=WorkspaceTaskReview.CanonicalUtc(value.ReviewAfterUtc),value.TimeZoneId,value.Repeat,value.Targets,value.ConsentVersion};
    public static string Hash(ReminderPreparation value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{value.ExpectedWorkspaceRevision,Intent=Wire(value.Intent)},Json))).ToLowerInvariant();
    public static bool Same(ReminderIntent? first,ReminderIntent? second)=>first is not null&&second is not null&&JsonSerializer.Serialize(Wire(first),Json)==JsonSerializer.Serialize(Wire(second),Json);
}
