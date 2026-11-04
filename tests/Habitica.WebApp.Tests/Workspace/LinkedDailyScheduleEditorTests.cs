using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
namespace Habitica.WebApp.Tests.Workspace;
public sealed class LinkedDailyScheduleEditorTests:BunitContext
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-11-02T12:00:00Z");
    private static RecurrenceRule Rule=>new(RecurrenceFrequency.Daily,1,new(2026,10,27),"UTC",[],[]);
    private static HabiticaDailySchedule Raw=>new("daily",1,DateTimeOffset.Parse("2026-10-27T00:00:00Z"),new Dictionary<string,bool>{{"su",true},{"m",true},{"t",true},{"w",true},{"th",true},{"f",true},{"s",true}},[],[]);
    private sealed class Clock:TimeProvider{public override DateTimeOffset GetUtcNow()=>Now;}
    private async Task<(TodoOrganizationFixture F,CompanionTask Task)> Setup(bool cloud=true,bool longName=false,ProjectionState state=ProjectionState.Linked)
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();var task=new CompanionTask(Guid.NewGuid(),1,longName?new string('W',180)+" linked Daily":"Linked Daily fixture",CompanionTaskKind.Daily,CompanionTaskState.Active,CompanionPriority.Normal,Now,Now,Recurrence:Rule);
        var document=new WorkspaceDocument{Id=Guid.NewGuid(),Revision=10,Tasks=[task],Projections=[new(Guid.NewGuid(),2,task.Id,state,"90000000-0000-4000-8000-000000000001",Fingerprint:new string('a',64))]};var f=new TodoOrganizationFixture();await f.Load(cloud,document);
        f.Task.Client.PreparationTransform=command=>{if(command.Operation!=WorkspaceOperation.EditLinkedDailySchedule)return command;var review=LinkedDailySchedulePolicy.Review(Raw,command.Patch!.Recurrence!,new(0,0));return command with{Patch=command.Patch with{LinkedSchedule=new(Guid.Parse(document.Projections[0].HabiticaTaskId!),new string('a',64),DateTimeOffset.UtcNow,new(0,0),Raw,review.ExpectedAfter!)}};};
        Services.AddSingleton<IAppSessionController>(f.Task.Session);Services.AddSingleton<IWorkspaceCloudClient>(f.Task.Client);Services.AddSingleton(f.Task.Database);Services.AddSingleton(f.Task.Controller);Services.AddSingleton(f.Organization);Services.AddSingleton(f.Todo);Services.AddSingleton<TodoTaskDraftStore>();Services.AddSingleton<TimeProvider>(new Clock());return(f,task);
    }
    private static void Save(string name,string markup,string state)=>Qualification.PageLayoutEvidence.Save(name,markup,"/todo/dailies",state,["form","actions","confirmation","disabled-controls","error","navigation"],nameof(LinkedDailyScheduleEditorTests));
    private IRenderedComponent<TodoTaskPanel> Open(CompanionTask task){var cut=Render<TodoTaskPanel>(p=>p.Add(x=>x.Kind,CompanionTaskKind.Daily));cut.Find($"[data-testid=todo-recurring-{task.Id:D}]").Click();return cut;}
    [Fact]public async Task Linked_schedule_reviews_exact_provider_fields_dates_and_pending_original_receipt_without_scoring()
    {
        var(f,task)=await Setup();using var owned=f;var cut=Open(task);Assert.Null(cut.Find("[data-testid=recurring-frequency]").Closest("fieldset")!.GetAttribute("disabled"));Assert.NotNull(cut.Find("[data-testid=recurring-lifecycle]").Closest("fieldset")!.GetAttribute("disabled"));Assert.True(cut.Find("[data-testid=recurring-clear]").HasAttribute("disabled"));Save("linked-daily-editor",cut.Markup,"linked-cloud-schedule-authoring");
        cut.Find("[data-testid=recurring-interval]").Change("3");cut.Find("[data-testid=recurring-editor] form").Submit();cut.WaitForAssertion(()=>Assert.NotNull(f.Task.Controller.Preview));var preview=f.Task.Controller.Preview!;Assert.Equal(WorkspaceOperation.EditLinkedDailySchedule,preview.Command.Operation);Assert.True(preview.MayAffectHabitica);Assert.Contains("Retained Habitica rule",cut.Find("[data-testid=linked-daily-review]").TextContent);Assert.Contains("su: on",cut.Markup);Assert.Contains("no Cron",cut.Markup);Assert.Contains("next 366-day",cut.Markup);Assert.DoesNotContain("No Habitica schedule or reward changes",cut.Find("[data-testid=todo-review]").TextContent);Save("linked-daily-review",cut.Markup,"exact-full-provider-and-native-date-review");
        cut.Find("[data-testid=todo-confirm]").Click();cut.WaitForAssertion(()=>Assert.NotNull(f.Task.Controller.LastReceipt));Assert.Single(f.Task.Client.AcceptedIds);Assert.Empty(f.Task.Client.State.Document.Completions);Assert.Empty(f.Task.Client.State.Document.HabitScores);Assert.Equal(3,f.Task.Client.State.Document.Tasks[0].Recurrence!.Interval);Assert.Single(f.Task.Client.State.Commands.Last().Effects);Assert.Contains("No original normalized provider response",cut.Markup);Save("linked-daily-pending",cut.Markup,"native-epoch-awaits-original-provider-response");var sync=Render<TodoTaskPanel>(p=>p.Add(x=>x.ShowCapture,false).Add(x=>x.ShowTaskList,false).Add(x=>x.ExpandSourcePicker,true).Add(x=>x.ShowSyncLink,false));Qualification.PageLayoutEvidence.Save("linked-daily-sync",sync.Markup,"/todo/sync","original-pending-linked-schedule-epoch",["form","actions","confirmation","disabled-controls","error","navigation"],nameof(LinkedDailyScheduleEditorTests));
    }
    [Fact]public async Task Linked_schedule_loss_of_acceptance_retains_original_command_and_recovers_without_repreparing()
    {
        var(f,task)=await Setup();using var owned=f;f.Task.Client.LoseAcceptance=true;var cut=Open(task);cut.Find("[data-testid=recurring-interval]").Change("2");cut.Find("[data-testid=recurring-editor] form").Submit();var id=f.Task.Controller.Preview!.PreparedCommandId;cut.Find("[data-testid=todo-confirm]").Click();cut.WaitForAssertion(()=>Assert.True(f.Task.Controller.AcceptanceUnknown));Save("linked-daily-unknown",cut.Markup,"original-acceptance-receipt-required");await f.Todo.CheckOriginalAsync();Assert.Equal(id,f.Task.Controller.LastReceipt!.CommandId);Assert.Single(f.Task.Client.PreparationIds);Assert.Single(f.Task.Client.AcceptedIds);Assert.Equal(id,Assert.Single(f.Task.Client.ReceiptReads));
    }
    [Theory][InlineData(false,ProjectionState.Linked)][InlineData(true,ProjectionState.UnknownOutcome)][InlineData(true,ProjectionState.ExternalConflict)]
    public async Task Browser_and_uncertain_mapping_keep_linked_schedule_read_only(bool cloud,ProjectionState state)
    {
        var(f,task)=await Setup(cloud,state:state);using var owned=f;var cut=Open(task);Assert.NotNull(cut.Find("[data-testid=recurring-frequency]").Closest("fieldset")!.GetAttribute("disabled"));Assert.Empty(f.Task.Client.PreparationIds);Save("linked-daily-readonly-"+(cloud?state.ToString().ToLowerInvariant():"browser"),cut.Markup,"linked-source-or-outcome-unavailable");
    }
    [Fact]public async Task Long_task_and_exact_normalized_response_preserve_readable_review_after_account_change()
    {
        var(f,task)=await Setup(longName:true);using var owned=f;var cut=Open(task);cut.Find("[data-testid=recurring-interval]").Change("3");cut.Find("[data-testid=recurring-editor] form").Submit();Save("linked-daily-long",cut.Markup,"long-private-identity-review");f.Task.Session.SetState(f.Task.Session.State with{UserId=Guid.NewGuid().ToString("D")});cut.WaitForAssertion(()=>Assert.Empty(cut.FindAll("[data-testid=linked-daily-review]")));Assert.DoesNotContain(task.Text,cut.Markup);Assert.Empty(f.Task.Client.AcceptedIds);Save("linked-daily-account",cut.Markup,"account-change-clears-private-schedule-review");
    }
    [Fact]public void Independent_month_end_preview_skips_missing_months_and_labels_native_semantics()
    {
        var rule=new RecurrenceRule(RecurrenceFrequency.Monthly,1,new(2026,10,31),"UTC",[],[31]);var review=LinkedDailySchedulePolicy.Review(Raw,rule,new(0,0));var context=new LinkedDailyScheduleContext(Guid.NewGuid(),new string('a',64),Now,new(0,0),Raw,review.ExpectedAfter!);var cut=Render<LinkedDailySchedulePreview>(p=>p.Add(x=>x.Context,context).Add(x=>x.Proposed,rule).Add(x=>x.NativeBefore,Rule));Assert.Contains("2026-12-31",cut.Markup);Assert.DoesNotContain("2026-11-30",cut.Markup);Assert.Contains("does not pause Habitica penalties",cut.Markup);Save("linked-daily-month-end",cut.Markup,"month-gaps-with-explicit-penalty-semantics");
    }
}
