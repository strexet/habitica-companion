using Bunit;
using Habitica.Application.Notifications;
using Habitica.Application.Workspace;
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
        cut.Find("input[type='checkbox']").Change(true);cut.Find("[data-testid='schedule-prepare']").Click();cut.WaitForAssertion(()=>Assert.NotNull(f.Reminders.Preview));Assert.Equal(0,f.Api.Acceptances);Assert.Null(f.Reminders.Preview!.Intent!.Repeat);Save("push-schedule-preview",cut.Markup,"/todo/reminders","exact-one-time-preview");
        f.Api.LoseAccept=true;cut.Find("[data-testid='schedule-accept']").Click();cut.WaitForAssertion(()=>Assert.True(f.Reminders.Pending!.AcceptanceAttempted));Save("push-schedule-unknown",cut.Markup,"/todo/reminders","acceptance-unknown-original-id-retained");var focusBeforeRecovery=JSInterop.Invocations.Count(row=>row.Identifier=="Blazor._internal.domWrapper.focus");cut.Find("[data-testid='schedule-recover']").Click();cut.WaitForAssertion(()=>Assert.NotNull(f.Reminders.Receipt));cut.WaitForAssertion(()=>Assert.True(JSInterop.Invocations.Count(row=>row.Identifier=="Blazor._internal.domWrapper.focus")>focusBeforeRecovery));Save("push-schedule-receipt",cut.Markup,"/todo/reminders","configuration-only-receipt");
        await f.Reminders.LoadAsync(f.Intent.TaskId);f.Api.Preview=null;cut.Render();cut.Find("[data-testid='schedule-mode']").Change("weekly");cut.Find("[data-testid='schedule-use-cue']").Click();cut.Find("input[type='checkbox']").Change(true);cut.Find("[data-testid='schedule-overlap']").Change("later");cut.Find("[data-testid='schedule-prepare']").Click();cut.WaitForAssertion(()=>Assert.NotNull(f.Reminders.Preview));Assert.Equal("later",f.Reminders.Preview!.Intent!.Repeat!.Overlap);Assert.Equal("skip",f.Reminders.Preview.Intent.Repeat.Gap);Save("push-schedule-weekly",cut.Markup,"/todo/reminders","explicit-repeat-dst-and-end-date-preview");Assert.Empty(f.Task.Client.AcceptedIds);Assert.Empty(f.Task.Client.PreparationIds);
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
