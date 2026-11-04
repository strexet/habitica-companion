namespace Habitica.Domain.Workspace;

public sealed record HabiticaDailySchedule(string Frequency,int EveryX,DateTimeOffset StartDate,IReadOnlyDictionary<string,bool> Repeat,IReadOnlyList<int> DaysOfMonth,IReadOnlyList<int> WeeksOfMonth);
public sealed record LinkedDailyScheduleReview(HabiticaDailySchedule Before,RecurrenceRule Proposed,bool Supported,string? Failure,RecurrenceRule? BeforeNative=null,HabiticaDailySchedule? ExpectedAfter=null);
public static class LinkedDailySchedulePolicy
{
    private static readonly string[] Keys=["su","m","t","w","th","f","s"];
    public static LinkedDailyScheduleReview Review(HabiticaDailySchedule before,RecurrenceRule proposed,DailyAccountSchedule account)
    {
        LinkedDailyScheduleReview Refuse(string code)=>new(before,proposed,false,code);
        if(!ValidRaw(before))return Refuse("invalid_daily_schedule");
        var normalized=DailyProjectionSchedule.Normalize(proposed,account);if(normalized.Schedule is not {} desired)return Refuse(normalized.Failure??"unsupported_linked_schedule");
        if(desired.StartDate.Year is <2 or >9998)return Refuse("daily_start_unrepresentable");
        if(before.Frequency=="yearly"||before.EveryX is <1 or >366||before.WeeksOfMonth.Count>0||before.Frequency!="monthly"&&before.DaysOfMonth.Count>0)return Refuse("unsupported_retained_daily_schedule");
        var local=before.StartDate.AddMinutes(-account.TimezoneOffset);if(local.TimeOfDay!=TimeSpan.Zero)return Refuse("daily_start_unrepresentable");
        // PUT rewrites a nonempty monthly daysOfMonth array from the account-local start date.
        if(before.Frequency=="monthly"&&(before.DaysOfMonth.Count!=1||before.DaysOfMonth[0]!=local.Day))return Refuse("daily_month_update_unrepresentable");
        if(proposed.Frequency==RecurrenceFrequency.Monthly&&(proposed.DaysOfMonth.Count!=1||proposed.DaysOfMonth[0]!=proposed.StartDate.Day))return Refuse("daily_month_update_unrepresentable");
        var frequency=before.Frequency switch{"daily"=>RecurrenceFrequency.Daily,"weekly"=>RecurrenceFrequency.Weekly,_=>RecurrenceFrequency.Monthly};
        var previous=new RecurrenceRule(frequency,before.EveryX,DateOnly.FromDateTime(local.DateTime),proposed.TimeZoneId,frequency==RecurrenceFrequency.Weekly?Enumerable.Range(0,7).Where(i=>before.Repeat[Keys[i]]).Select(i=>(DayOfWeek)i).ToArray():[],frequency==RecurrenceFrequency.Monthly?before.DaysOfMonth.ToArray():[]);
        if(DailyProjectionSchedule.RuleFailure(previous) is {} failure)return Refuse(failure);
        // Inactive weekday defaults remain intact unless the reviewed weekly rule actually replaces the weekdays.
        var repeat=proposed.Frequency==RecurrenceFrequency.Weekly?desired.Repeat:Keys.ToDictionary(k=>k,k=>before.Repeat[k]);
        var after=new HabiticaDailySchedule(proposed.Frequency.ToString().ToLowerInvariant(),desired.EveryX,desired.StartDate,repeat,desired.DaysOfMonth.ToArray(),[]);
        return new(before,proposed,true,null,previous,after);
    }
    public static bool ValidRaw(HabiticaDailySchedule? value)=>value is not null&&value.Frequency is "daily" or "weekly" or "monthly" or "yearly"&&value.EveryX is >=0 and <=9999&&value.StartDate!=default&&value.StartDate.Offset==TimeSpan.Zero&&value.StartDate.Year is >=2 and <=9998
        &&value.Repeat is not null&&value.Repeat.Count==Keys.Length&&Keys.All(value.Repeat.ContainsKey)
        &&value.DaysOfMonth is not null&&value.DaysOfMonth.All(d=>d is >=1 and <=31)&&value.DaysOfMonth.Distinct().Count()==value.DaysOfMonth.Count
        &&value.WeeksOfMonth is not null&&value.WeeksOfMonth.All(d=>d is >=1 and <=5)&&value.WeeksOfMonth.Distinct().Count()==value.WeeksOfMonth.Count;
}

public sealed record LinkedDailyScheduleContext(Guid HabiticaTaskId,string Fingerprint,DateTimeOffset ObservedAtUtc,DailyAccountSchedule Account,HabiticaDailySchedule Before,HabiticaDailySchedule ExpectedAfter)
{
    public bool IsValid(RecurrenceRule? proposed)
    {
        if(proposed is null||HabiticaTaskId==Guid.Empty||Fingerprint is null||Fingerprint.Length!=64||Fingerprint.Any(c=>!"0123456789abcdef".Contains(c))||ObservedAtUtc==default||ObservedAtUtc.Offset!=TimeSpan.Zero||Account is null||!LinkedDailySchedulePolicy.ValidRaw(Before)||!LinkedDailySchedulePolicy.ValidRaw(ExpectedAfter))return false;
        var review=LinkedDailySchedulePolicy.Review(Before,proposed,Account);
        return review.Supported&&review.ExpectedAfter is {} expected&&ScheduleFields(expected).SequenceEqual(ScheduleFields(ExpectedAfter));
    }
    private static string Number(long value)=>value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static string Ticks(DateTimeOffset value)=>Number(value.UtcTicks-DateTimeOffset.UnixEpoch.UtcTicks);
    private static IEnumerable<string> ScheduleFields(HabiticaDailySchedule row)=>new[]{row.Frequency,Number(row.EveryX),Ticks(row.StartDate)}.Concat(new[]{"su","m","t","w","th","f","s"}.Select(key=>row.Repeat[key]?"1":"0")).Concat(new[]{Number(row.DaysOfMonth.Count)}).Concat(row.DaysOfMonth.Order().Select(day=>Number(day))).Concat(new[]{Number(row.WeeksOfMonth.Count)}).Concat(row.WeeksOfMonth.Order().Select(day=>Number(day)));
    public IEnumerable<string> HashFields()=>new[]{HabiticaTaskId.ToString("D"),Fingerprint,Ticks(ObservedAtUtc),Number(Account.TimezoneOffset),Number(Account.DayStart)}.Concat(ScheduleFields(Before)).Concat(ScheduleFields(ExpectedAfter));
}

public sealed record LinkedDailyScheduleResult(
    [property:System.Text.Json.Serialization.JsonRequired] DateTimeOffset ObservedAtUtc,
    [property:System.Text.Json.Serialization.JsonRequired] HabiticaDailySchedule Actual,
    [property:System.Text.Json.Serialization.JsonRequired] bool MatchesExpected,
    [property:System.Text.Json.Serialization.JsonRequired] Guid AttemptId)
{
    public bool IsValid(LinkedDailyScheduleContext? context)=>context is not null&&ObservedAtUtc!=default&&ObservedAtUtc.Offset==TimeSpan.Zero&&AttemptId!=Guid.Empty&&LinkedDailySchedulePolicy.ValidRaw(Actual)&&MatchesExpected==Same(Actual,context.ExpectedAfter);
    private static bool Same(HabiticaDailySchedule a,HabiticaDailySchedule b)=>a.Frequency==b.Frequency&&a.EveryX==b.EveryX&&a.StartDate==b.StartDate&&a.Repeat.Count==b.Repeat.Count&&a.Repeat.All(row=>b.Repeat.TryGetValue(row.Key,out var value)&&row.Value==value)&&a.DaysOfMonth.Order().SequenceEqual(b.DaysOfMonth.Order())&&a.WeeksOfMonth.Order().SequenceEqual(b.WeeksOfMonth.Order());
}
