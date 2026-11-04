using System.Text.Json.Serialization;
namespace Habitica.Domain.Workspace;

public enum WorkspaceRelativeDatePreset { Today, BeforeToday, NextSevenDays, ThisWeek, NoDate }
public sealed record WorkspaceRelativeDateBounds(WorkspaceRelativeDatePreset Preset,DateOnly? From,DateOnly? Through);
public sealed record WorkspaceRelativeDateEvaluation(DateTimeOffset AsOfUtc,string TimeZoneId,DateOnly LocalDate,
    WorkspaceRelativeDateBounds? Planned,WorkspaceRelativeDateBounds? Deadline);

public static class WorkspaceRelativeDates
{
    public static WorkspaceRelativeDateEvaluation? Evaluate(WorkspaceSavedTaskQuery query,DateTimeOffset asOfUtc,DateOnly? expectedEvaluationDay=null)
    {
        if(!query.IsValid())throw new InvalidOperationException("invalid_saved_task_view");
        if(!query.HasRelativeDates){if(expectedEvaluationDay is not null)throw new InvalidOperationException("invalid_evaluation_context");return null;}
        if(asOfUtc==default||asOfUtc.Offset!=TimeSpan.Zero)throw new InvalidOperationException("invalid_evaluation_clock");
        var local=RecurrenceCalendar.LocalDateAt(asOfUtc,query.TimeZoneId!);
        if(local.Date is not {} day)throw new InvalidOperationException(local.Failure=="invalid_time_zone"?"relative_timezone_unavailable":"invalid_evaluation_clock");
        if(expectedEvaluationDay is {} expected&&expected!=day)throw new InvalidOperationException("stale_evaluation_day");
        return new(asOfUtc,query.TimeZoneId!,day,Bounds(query.Filter.PlannedRelative,day),Bounds(query.Filter.DeadlineRelative,day));
    }
    public static WorkspaceRelativeDateBounds? Bounds(WorkspaceRelativeDatePreset? preset,DateOnly day)
    {
        if(preset is null)return null;
        try
        {
            var monday=day.AddDays(-((int)day.DayOfWeek+6)%7);
            WorkspaceRelativeDateBounds result=preset.Value switch
            {
                WorkspaceRelativeDatePreset.Today=>new(preset.Value,day,day),
                WorkspaceRelativeDatePreset.BeforeToday=>new(preset.Value,null,day.AddDays(-1)),
                WorkspaceRelativeDatePreset.NextSevenDays=>new(preset.Value,day,day.AddDays(6)),
                WorkspaceRelativeDatePreset.ThisWeek=>new(preset.Value,monday,monday.AddDays(6)),
                WorkspaceRelativeDatePreset.NoDate=>new WorkspaceRelativeDateBounds(preset.Value,null,null),
                _=>throw new InvalidOperationException("invalid_saved_task_view")
            };
            if(result.From==DateOnly.MinValue||result.Through==DateOnly.MinValue)throw new InvalidOperationException("relative_bounds_unavailable");return result;
        }
        catch(ArgumentOutOfRangeException){throw new InvalidOperationException("relative_bounds_unavailable");}
    }
    public static bool IsTimeZoneShape(string? zone)=>zone is {Length:>0 and <=128}&&!string.IsNullOrWhiteSpace(zone)&&(zone=="UTC"||zone.Contains('/'));
    public static bool MatchesMissingDates(CompanionTask task,WorkspaceRelativeDateEvaluation? evaluation)=>(evaluation?.Planned?.Preset!=WorkspaceRelativeDatePreset.NoDate||task.PlannedDate is null)
        &&(evaluation?.Deadline?.Preset!=WorkspaceRelativeDatePreset.NoDate||task.Deadline is null);
}
