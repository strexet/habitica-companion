using Bunit;
using Habitica.Application.Notifications;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
namespace Habitica.WebApp.Tests.Workspace;
public sealed class PushReminderComponentTests:BunitContext
{
    private async Task<PushReminderFixture> Setup()
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();var f=new PushReminderFixture();await f.InitializeAsync();
        Services.AddSingleton<IAppSessionController>(f.Task.Session);Services.AddSingleton<IWorkspaceCloudClient>(f.Task.Client);Services.AddSingleton<ITaskReminderClient>(f.Api);Services.AddSingleton(f.Task.Database);Services.AddSingleton(f.Task.Controller);Services.AddSingleton(f.Todo);Services.AddSingleton(f.Devices);Services.AddSingleton(f.Reminders);Services.AddSingleton<TodoTaskDraftStore>();Services.AddSingleton(TimeProvider.System);return f;
    }
    private static void Save(string name,string markup,string route,string state)=>Qualification.PageLayoutEvidence.Save(name,markup,route,state,["form","navigation","disabled-controls","confirmation","error"],nameof(PushReminderComponentTests));
    [Fact] public async Task Named_day_browser_fixture_uses_typed_exact_source_readiness_and_identity_control()
    {
        var f=await Setup();var cue=new DateTimeOffset(2027,1,3,9,0,0,TimeSpan.Zero);
        f.Task.Client.State=f.Task.Client.State with{Document=f.Task.Client.State.Document with{Tasks=[f.Task.Client.State.Document.Tasks[0] with{ReviewAfterUtc=cue}]}};
        var rule=new RecurrenceRule(RecurrenceFrequency.Weekly,2,new(2027,1,3),"UTC",[DayOfWeek.Sunday,DayOfWeek.Wednesday],[]);var intent=f.Intent with{ReviewAfterUtc=cue,Repeat=new(rule,"09:00",new(2027,3,3),"skip","later")};var preparation=new ReminderPreparation(Guid.NewGuid(),2,intent);
        Assert.True(ReminderIdentity.Valid(intent));Assert.Equal(DayOfWeek.Sunday,rule.StartDate.DayOfWeek);
        var schedule=new ReminderSchedule(Guid.NewGuid(),7,intent.TaskId,"paused","cue_changed",intent,null,null);var state=f.Api.Read! with{Schedules=[schedule]};
        var data=new{Account=WorkspaceTaskFixture.User,Journal=f.Task.Client.State,State=state,Descriptor=new Habitica.Application.Integration.IntegrationRecoveryDescriptor("integration-recovery-v1",PushReminderFixture.Profile,PushReminderFixture.Connection),HashControl=new{Wire=new{preparation.ExpectedWorkspaceRevision,Intent=ReminderIdentity.Wire(intent)},Hash=ReminderIdentity.Hash(preparation)}};
        var output=Environment.GetEnvironmentVariable("HABITICA_BROWSER_EVIDENCE");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"push-days-source.json"),System.Text.Json.JsonSerializer.Serialize(data,ReminderIdentity.Json));}
    }

    [Fact] public async Task Named_weekdays_keep_Sunday_identity_and_specific_empty_feedback_before_exact_review()
    {
        var f=await Setup();var cue=new DateTimeOffset(2027,1,3,9,0,0,TimeSpan.Zero);
        f.Task.Client.State=f.Task.Client.State with{Document=f.Task.Client.State.Document with{Tasks=[f.Task.Client.State.Document.Tasks[0] with{ReviewAfterUtc=cue}]}};
        await f.Todo.LoadSelectedAsync();await f.Reminders.LoadAsync(f.Intent.TaskId);
        var cut=Render<TaskPushReminderEditor>(p=>p.Add(c=>c.TaskId,f.Intent.TaskId));
        cut.Find("[data-testid='schedule-mode']").Change("weekly");cut.Find("[data-testid='schedule-use-cue']").Click();
        var sunday=cut.Find("[data-testid='schedule-day-Sunday']");Assert.True(sunday.HasAttribute("checked"));Assert.Contains("Sunday",sunday.ParentElement!.TextContent);
        cut.Find($"[data-testid='schedule-device-{f.Api.Device.DeviceId:D}']").Change(true);cut.Find("[data-testid='schedule-overlap']").Change("later");
        sunday.Change(false);cut.Find("[data-testid='schedule-prepare']").Click();Assert.Equal(0,f.Api.Preparations);
        Assert.Equal("Choose at least one weekday.",cut.Find("#schedule-weekdays-error").TextContent);Assert.Equal("schedule-weekdays-error",cut.Find("[data-testid='schedule-day-Sunday']").GetAttribute("aria-describedby"));
        Save("push-days-weekly-empty",cut.Markup,"/todo/reminders","specific-empty-weekday-feedback");
        cut.Find("[data-testid='schedule-day-Sunday']").Change(true);cut.Find("[data-testid='schedule-day-Wednesday']").Change(true);cut.Find("[data-testid='schedule-prepare']").Click();cut.WaitForAssertion(()=>Assert.NotNull(f.Reminders.Preview));
        var intent=f.Reminders.Preview!.Intent!;Assert.Equal(new[]{DayOfWeek.Sunday,DayOfWeek.Wednesday},intent.Repeat!.Rule.Weekdays);Assert.Equal(cue,intent.ReviewAfterUtc);Assert.Equal("UTC",intent.TimeZoneId);Assert.Equal("later",intent.Repeat.Overlap);Assert.Equal("skip",intent.Repeat.Gap);Assert.Equal(new[]{new ReminderTarget(f.Api.Device.DeviceId,1,f.Api.Device.SubscriptionId)},intent.Targets);Assert.Contains("Sunday (0)",cut.Find("[data-testid='schedule-preview']").TextContent);Assert.Contains("Wednesday (3)",cut.Find("[data-testid='schedule-preview']").TextContent);Assert.Empty(cut.FindAll("#schedule-weekdays-error"));
        Save("push-days-weekly-review",cut.Markup,"/todo/reminders","named-weekday-exact-pending-review");
        Assert.Equal(0,f.Api.Acceptances);Assert.Equal(0,f.BrowserApi.Permissions);
    }
    [Fact] public async Task Named_month_dates_retain_exact_schedule_revision_and_cannot_replace_pending_original()
    {
        var f=await Setup();var cue=new DateTimeOffset(2027,1,1,9,0,0,TimeSpan.Zero);
        f.Task.Client.State=f.Task.Client.State with{Document=f.Task.Client.State.Document with{Tasks=[f.Task.Client.State.Document.Tasks[0] with{ReviewAfterUtc=cue}]}};
        await f.Todo.LoadSelectedAsync();var repeat=new ReminderRepeat(new(RecurrenceFrequency.Monthly,2,new(2027,1,1),"UTC",[],[1,31]),"09:00",new(2027,3,31),"skip","earlier");var schedule=new ReminderSchedule(Guid.NewGuid(),7,f.Intent.TaskId,"paused","cue_changed",f.Intent with{Repeat=repeat},null,null);f.Api.Read=f.Api.Read! with{Schedules=[schedule]};await f.Reminders.LoadAsync(f.Intent.TaskId);
        var cut=Render<TaskPushReminderEditor>(p=>p.Add(c=>c.TaskId,f.Intent.TaskId));cut.FindAll("button").Single(b=>b.TextContent.Contains("Edit this schedule")).Click();
        Assert.Equal(31,cut.FindAll("[data-testid^='schedule-month-day-']").Count);Assert.True(cut.Find("[data-testid='schedule-month-day-31']").HasAttribute("checked"));
        cut.Find("[data-testid='schedule-month-day-1']").Change(false);cut.Find("[data-testid='schedule-month-day-31']").Change(false);cut.Find("[data-testid='schedule-prepare']").Click();Assert.Equal("Choose at least one day of the month.",cut.Find("#schedule-monthdays-error").TextContent);Assert.Equal(0,f.Api.Preparations);
        Save("push-days-monthly-empty",cut.Markup,"/todo/reminders","specific-empty-month-date-feedback");
        cut.Find("[data-testid='schedule-month-day-1']").Change(true);cut.Find("[data-testid='schedule-month-day-31']").Change(true);
        Save("push-days-monthly-edit",cut.Markup,"/todo/reminders","retained-monthly-schedule-exact-dates");
        cut.Find("[data-testid='schedule-prepare']").Click();cut.WaitForAssertion(()=>Assert.NotNull(f.Reminders.Preview));var intent=f.Reminders.Preview!.Intent!;Assert.Equal(new[]{1,31},intent.Repeat!.Rule.DaysOfMonth);Assert.Empty(intent.Repeat.Rule.Weekdays);Assert.Equal(2,intent.Repeat.Rule.Interval);Assert.Equal(schedule.ScheduleId,intent.ScheduleId);Assert.Equal(7,intent.ExpectedRevision);Assert.Equal(cue,intent.ReviewAfterUtc);
        var original=f.Reminders.Pending;Assert.True(cut.Find("fieldset").HasAttribute("disabled"));cut.Find("[data-testid='schedule-prepare']").Click();Assert.Equal(original,f.Reminders.Pending);Assert.Equal(1,f.Api.Preparations);Assert.Equal(0,f.Api.Acceptances);Assert.Contains("start 2027-01-01",cut.Find("[data-testid='schedule-preview']").TextContent);Assert.Contains("Days of month: 1, 31",cut.Find("[data-testid='schedule-preview']").TextContent);Assert.DoesNotContain("Weekdays:",cut.Find("[data-testid='schedule-preview']").TextContent);
        Save("push-days-monthly-review",cut.Markup,"/todo/reminders","exact-retained-monthly-pending-review");
    }

    [Fact] public async Task Unchecked_pending_failure_and_retry_keep_capability_observations_distinct_without_permission()
    {
        var f=await Setup();using var fresh=new PushDeviceController(f.Task.Session,f.Api,f.BrowserApi,f.Local);Services.AddSingleton(fresh);
        var reads=f.BrowserApi.CapabilityReads;var cut=Render<PushDevicePanel>();
        Assert.Contains("Browser capability: not checked",cut.Markup);Assert.Contains("Permission: not checked",cut.Markup);Assert.DoesNotContain("unsupported",cut.Markup);
        Assert.Equal(reads,f.BrowserApi.CapabilityReads);Assert.Equal(0,f.BrowserApi.Permissions);Save("push-capability-unchecked",cut.Markup,"/todo/sync","capability-not-checked");
        f.BrowserApi.PrepareGate=new(TaskCreationOptions.RunContinuationsAsynchronously);var check=fresh.RefreshAsync();
        cut.WaitForAssertion(()=>Assert.Contains("Browser capability: checking",cut.Markup));Assert.True(cut.Find("[data-testid='push-refresh']").HasAttribute("disabled"));
        Save("push-capability-checking",cut.Markup,"/todo/sync","explicit-capability-check-pending");f.BrowserApi.PrepareGate.SetException(new InvalidOperationException("Synthetic probe failure"));await check;
        cut.WaitForAssertion(()=>Assert.Contains("Browser capability: check failed",cut.Markup));Assert.DoesNotContain("unsupported",cut.Markup);Assert.Equal(0,f.BrowserApi.Permissions);Save("push-capability-failed",cut.Markup,"/todo/sync","explicit-capability-check-failed");
        f.BrowserApi.PrepareGate=null;cut.Find("[data-testid='push-refresh']").Click();cut.WaitForAssertion(()=>Assert.Contains("Browser capability: available",cut.Markup));Assert.Contains("Permission: not requested",cut.Markup);Assert.Equal(0,f.BrowserApi.Permissions);
    }
    [Theory]
    [InlineData("available","default","available","not requested",true)]
    [InlineData("available","granted","available","granted",true)]
    [InlineData("available","denied","available","denied",false)]
    [InlineData("installation-required","default","installation required","not requested",false)]
    [InlineData("unsupported","unsupported","unsupported","unsupported",false)]
    [InlineData("insecure","default","secure context required","not requested",false)]
    public async Task Observed_capability_and_permission_are_distinct_and_never_prompt_on_render_or_acknowledgement(string state,string permission,string capabilityText,string permissionText,bool mayRegister)
    {
        var f=await Setup();f.BrowserApi.Capability=new(state,permission);await f.Devices.RefreshAsync();var cut=Render<PushDevicePanel>();
        Assert.Contains("Browser capability: "+capabilityText,cut.Markup);Assert.Contains("Permission: "+permissionText,cut.Markup);cut.Find("[data-testid='push-ack']").Change(true);
        Assert.Equal(!mayRegister,cut.Find("[data-testid='push-register']").HasAttribute("disabled"));Assert.Equal(0,f.BrowserApi.Permissions);Assert.Empty(f.Api.Subscriptions);
        Save("push-capability-"+state+"-"+permission,cut.Markup,"/todo/sync","observed-"+state+"-"+permission);
    }
    [Fact] public async Task Browser_module_probe_failure_remains_unprepared_instead_of_claiming_unsupported()
    {
        var f=await Setup();var module=JSInterop.SetupModule("./js/pwaPush.js");module.Setup<BrowserPushCapability>("capability").SetException(new Microsoft.JSInterop.JSException("Synthetic module read failed"));
        await using var browser=new Habitica.WebApp.Integration.PwaPushBrowser(JSInterop.JSRuntime);var observed=await browser.PrepareAsync(default);Assert.Equal("unprepared",observed.State);
        using var controller=new PushDeviceController(f.Task.Session,f.Api,browser,f.Local);Services.AddSingleton(controller);await controller.RefreshAsync();var cut=Render<PushDevicePanel>();Assert.Contains("Browser capability: check failed",cut.Markup);Assert.False(controller.CanRegister);Assert.DoesNotContain("requestPermission",string.Join(" ",module.Invocations.Select(x=>x.Identifier)));
        Save("push-capability-module-failed",cut.Markup,"/todo/sync","browser-module-probe-failed-without-unsupported-claim");
    }
    [Fact] public async Task Device_setup_requires_acknowledgement_and_never_prompts_on_render()
    {
        var f=await Setup();var cut=Render<PushDevicePanel>();Assert.Equal(0,f.BrowserApi.Permissions);Assert.True(cut.Find("[data-testid='push-register']").HasAttribute("disabled"));Save("push-device-ready",cut.Markup,"/todo/sync","explicit-consent-device-ready");
        cut.Find("[data-testid='push-ack']").Change(true);cut.Find("[data-testid='push-register']").Click();cut.WaitForAssertion(()=>Assert.Single(f.Api.Subscriptions));Assert.NotNull(f.Devices.Binding);Assert.Contains("delete",cut.Markup);Save("push-device-enrolled",cut.Markup,"/todo/sync","enrolled-with-separate-revoke-and-local-removal");
        f.Api.LosePush=true;cut.Find("[data-testid='push-consent']").Click();cut.WaitForAssertion(()=>Assert.NotNull(f.Devices.Pending));Save("push-device-pending",cut.Markup,"/todo/sync","original-request-unconfirmed");
        cut.Find("[data-testid='push-recover']").Click();cut.WaitForAssertion(()=>Assert.Null(f.Devices.Pending));Assert.Equal(1,f.BrowserApi.Permissions);f.Task.Session.SetState(f.Task.Session.State with{UserId=Guid.NewGuid().ToString()});f.Local.Device=new(0,null,null);await f.Devices.RefreshAsync();cut.WaitForAssertion(()=>Assert.False(cut.Find("[data-testid='push-ack']").HasAttribute("checked")));
    }
    [Theory][InlineData("denied","available")][InlineData("unsupported","unsupported")][InlineData("default","installation-required")]
    public async Task Denied_unsupported_and_installation_prerequisites_disable_device_button(string permission,string capability)
    {
        var f=await Setup();f.BrowserApi.Capability=new(capability,permission);await f.Devices.RefreshAsync();var cut=Render<PushDevicePanel>();cut.Find("[data-testid='push-ack']").Change(true);Assert.True(cut.Find("[data-testid='push-register']").HasAttribute("disabled"));Assert.Equal(0,f.BrowserApi.Permissions);Save("push-device-"+capability+"-"+permission,cut.Markup,"/todo/sync",capability+"-"+permission);
    }
    [Fact] public async Task One_time_and_weekly_schedule_review_recover_exact_receipts_and_preserve_task()
    {
        var f=await Setup();var cut=Render<TaskPushReminderEditor>(p=>p.Add(c=>c.TaskId,f.Intent.TaskId));Save("push-schedule-empty",cut.Markup,"/todo/reminders","one-time-initial-editor");
        cut.Find($"[data-testid='schedule-device-{f.Api.Device.DeviceId:D}']").Change(true);cut.Find("[data-testid='schedule-prepare']").Click();cut.WaitForAssertion(()=>Assert.NotNull(f.Reminders.Preview));Assert.Equal(0,f.Api.Acceptances);Assert.Null(f.Reminders.Preview!.Intent!.Repeat);Save("push-schedule-preview",cut.Markup,"/todo/reminders","exact-one-time-preview");
        f.Api.LoseAccept=true;cut.Find("[data-testid='schedule-accept']").Click();cut.WaitForAssertion(()=>Assert.True(f.Reminders.Pending!.AcceptanceAttempted));Save("push-schedule-unknown",cut.Markup,"/todo/reminders","acceptance-unknown-original-id-retained");var focusBeforeRecovery=JSInterop.Invocations.Count(row=>row.Identifier=="Blazor._internal.domWrapper.focus");cut.Find("[data-testid='schedule-recover']").Click();cut.WaitForAssertion(()=>Assert.NotNull(f.Reminders.Receipt));cut.WaitForAssertion(()=>Assert.True(JSInterop.Invocations.Count(row=>row.Identifier=="Blazor._internal.domWrapper.focus")>focusBeforeRecovery));Save("push-schedule-receipt",cut.Markup,"/todo/reminders","configuration-only-receipt");
        await f.Reminders.LoadAsync(f.Intent.TaskId);f.Api.Preview=null;cut.Render();cut.Find("[data-testid='schedule-mode']").Change("weekly");cut.Find("[data-testid='schedule-use-cue']").Click();cut.Find($"[data-testid='schedule-device-{f.Api.Device.DeviceId:D}']").Change(true);cut.Find("[data-testid='schedule-overlap']").Change("later");cut.Find("[data-testid='schedule-prepare']").Click();cut.WaitForAssertion(()=>Assert.NotNull(f.Reminders.Preview));Assert.Equal("later",f.Reminders.Preview!.Intent!.Repeat!.Overlap);Assert.Equal("skip",f.Reminders.Preview.Intent.Repeat.Gap);Save("push-schedule-weekly",cut.Markup,"/todo/reminders","explicit-repeat-dst-and-end-date-preview");Assert.Empty(f.Task.Client.AcceptedIds);Assert.Empty(f.Task.Client.PreparationIds);
    }
    [Fact] public async Task Editing_a_retained_paused_schedule_binds_exact_identity_and_revision()
    {
        var f=await Setup();var schedule=new ReminderSchedule(Guid.NewGuid(),7,f.Intent.TaskId,"paused","cue_changed",f.Intent,null,null);f.Api.Read=f.Api.Read! with{Schedules=[schedule]};await f.Reminders.LoadAsync(f.Intent.TaskId);
        var cut=Render<TaskPushReminderEditor>(p=>p.Add(c=>c.TaskId,f.Intent.TaskId));cut.FindAll("button").Single(button=>button.TextContent.Contains("Edit this schedule")).Click();cut.Find("[data-testid='schedule-prepare']").Click();
        cut.WaitForAssertion(()=>Assert.NotNull(f.Reminders.Preview));Assert.Equal(schedule.ScheduleId,f.Reminders.Preview!.Intent!.ScheduleId);Assert.Equal(7,f.Reminders.Preview.Intent.ExpectedRevision);Assert.Equal(0,f.Api.Acceptances);
    }
    [Fact] public async Task Disabled_sender_and_cancelled_schedules_keep_visible_delivery_limits()
    {
        var f=await Setup();f.Api.Read=f.Api.Read! with{Available=false,Schedules=[new(Guid.NewGuid(),2,f.Intent.TaskId,"paused","device_or_consent_changed",f.Intent,null,null)]};await f.Reminders.LoadAsync(f.Intent.TaskId);var cut=Render<TaskPushReminderEditor>(p=>p.Add(c=>c.TaskId,f.Intent.TaskId));Assert.Contains("paused",cut.Markup);Assert.Contains("Shown and read are unknown",cut.Markup);Assert.True(cut.Find("fieldset").HasAttribute("disabled"));Save("push-schedule-paused",cut.Markup,"/todo/reminders","disabled-service-paused-schedule");
    }
    [Fact] public async Task Full_reminders_route_exposes_source_task_cue_and_schedule_without_implicit_write()
    {
        var f=await Setup();Services.GetRequiredService<NavigationManager>().NavigateTo("/todo/reminders?taskId="+f.Intent.TaskId);var cut=Render<TodoRemindersPage>();Assert.NotEmpty(cut.FindAll("[data-testid='task-review-editor']"));Assert.NotEmpty(cut.FindAll("[data-testid='push-schedule']"));Assert.Empty(f.Task.Client.PreparationIds);Assert.Equal(0,f.Api.Preparations);Assert.Equal(0,f.BrowserApi.Permissions);Save("push-reminders-route",cut.Markup,"/todo/reminders","source-task-cue-device-schedule-page");
        cut.Find("[data-testid='review-action']").Change("Clear");cut.Find("[data-testid='review-prepare']").Click();cut.WaitForAssertion(()=>Assert.NotNull(f.Task.Controller.Preview));Assert.Equal(Habitica.Domain.Workspace.WorkspaceOperation.ReviewTask,f.Task.Controller.Preview!.Command.Operation);Assert.False(f.Task.Controller.Preview.MayAffectHabitica);
        Save("push-reminders-cue-confirm",cut.Markup,"/todo/reminders","exact-native-cue-change-from-push-page");var beforeFocus=JSInterop.Invocations.Count(row=>row.Identifier=="Blazor._internal.domWrapper.focus");
        cut.Find("[data-testid='todo-confirm']").Click();cut.WaitForAssertion(()=>Assert.Null(f.Todo.Snapshot!.Document.Tasks.Single().ReviewAfterUtc));cut.WaitForAssertion(()=>Assert.True(JSInterop.Invocations.Count(row=>row.Identifier=="Blazor._internal.domWrapper.focus")>beforeFocus));Assert.Equal(0,f.Api.Preparations);
        cut.Find("[data-testid='reminder-task-filter']").Input("private filter canary");f.Task.Session.SetState(f.Task.Session.State with{UserId=Guid.NewGuid().ToString()});cut.WaitForAssertion(()=>Assert.Equal("",cut.Find("[data-testid='reminder-task-filter']").GetAttribute("value")));
    }
    [Fact] public async Task Notification_open_requires_explicit_fresh_access_and_discards_private_content_on_logout()
    {
        var f=await Setup();var target=new NotificationTarget(f.Api.Read!.WorkspaceId,f.Intent.TaskId,Guid.NewGuid());Services.GetRequiredService<NavigationManager>().NavigateTo(NotificationRoute.Build(target));var cut=Render<NotificationOpenPage>();Assert.DoesNotContain("Synthetic push task",cut.Markup);Save("push-open-unchecked",cut.Markup,"/todo/reminders/open","authenticated-unchecked");
        f.Task.Client.FailLoads=true;cut.Find("[data-testid='notification-check']").Click();cut.WaitForAssertion(()=>Assert.Contains("unavailable",cut.Markup));Assert.DoesNotContain("Synthetic push task",cut.Markup);Save("push-open-offline",cut.Markup,"/todo/reminders/open","offline-fresh-access-refused");
        f.Task.Client.FailLoads=false;cut.Find("[data-testid='notification-check']").Click();cut.WaitForAssertion(()=>Assert.Contains("Synthetic push task",cut.Markup));Save("push-open-verified",cut.Markup,"/todo/reminders/open","fresh-exact-task-access");
        f.Task.Session.SetState(f.Task.Session.State with{IsAuthenticated=false});cut.WaitForAssertion(()=>Assert.DoesNotContain("Synthetic push task",cut.Markup));Assert.Contains("returnTo=",cut.Markup);Save("push-open-signin",cut.Markup,"/todo/reminders/open","signed-out-safe-return");Assert.Empty(f.Task.Client.AcceptedIds);Assert.Empty(f.Task.Client.PreparationIds);
    }
}
