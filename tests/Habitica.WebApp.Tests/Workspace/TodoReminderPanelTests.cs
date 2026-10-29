using Bunit;
using Habitica.Application.Notifications;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
namespace Habitica.WebApp.Tests.Workspace;
public sealed class TodoReminderPanelTests : BunitContext
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-26T09:00:00Z");
    private async Task<(TodoOrganizationFixture Fixture,ReminderBrowserFixture Browser,ForegroundReminderController Controller)> Setup(bool available=true)
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();var f=new TodoOrganizationFixture();
        var task=new CompanionTask(Guid.NewGuid(),1,new string('W',140)+" Voluntary reminder",CompanionTaskKind.Todo,CompanionTaskState.Active,CompanionPriority.Normal,Now.AddDays(-1),Now.AddDays(-1),ReviewAfterUtc:Now.AddHours(-1));
        await f.Load(document:new(){Id=Guid.NewGuid(),Revision=10,Tasks=[task,task with{Id=Guid.NewGuid(),Text="Future cue",ReviewAfterUtc=Now.AddDays(1)}]});
        Services.AddSingleton<IAppSessionController>(f.Task.Session);Services.AddSingleton(f.Todo);var browser=new ReminderBrowserFixture();var controller=new ForegroundReminderController(f.Task.Session,f.Todo,browser);Services.AddSingleton(controller);if(available)await controller.InitializeAsync();return(f,browser,controller);
    }
    private static void Save(string name,string markup,string state,string test)=>Qualification.PageLayoutEvidence.Save(name,markup,"/todo/today",state,["actions","disabled-controls","error","navigation"],test);
    [Fact]
    public async Task Explicit_browser_action_is_separate_from_inapp_cues_and_read_only_catchup_persists()
    {
        var (f,browser,controller)=await Setup();using(f)using(controller)
        {
            var cut=Render<TodoReminderPanel>();Assert.True(cut.Find("[data-testid='reminder-browser']").HasAttribute("disabled"));Assert.Equal(0,browser.Permissions);Save("reminder-off",cut.Markup,"inapp-off-browser-disabled",nameof(Explicit_browser_action_is_separate_from_inapp_cues_and_read_only_catchup_persists));
            cut.Find("[data-testid='reminder-inapp']").Click();Assert.Single(cut.FindAll("[data-testid='reminder-cue']"));Assert.Contains("1 reached",cut.Markup);Assert.Contains("2 total",cut.Markup);Assert.Equal(0,browser.Permissions);Save("reminder-due",cut.Markup,"missed-review-cue-visible-with-no-browser-permission",nameof(Explicit_browser_action_is_separate_from_inapp_cues_and_read_only_catchup_persists));
            cut.Find("[data-testid='reminder-browser']").Click();Assert.Equal(1,browser.Permissions);Assert.Contains("granted",cut.Find("[data-testid='reminder-state']").TextContent);Save("reminder-enabled",cut.Markup,"browser-enabled-with-closed-app-limits",nameof(Explicit_browser_action_is_separate_from_inapp_cues_and_read_only_catchup_persists));
            cut.Find("[data-testid='reminder-browser-off']").Click();Assert.Single(cut.FindAll("[data-testid='reminder-cue']"));Assert.Empty(f.Task.Client.PreparationIds);Assert.Empty(f.Task.Client.AcceptedIds);Assert.Empty(f.Todo.Snapshot!.Commands);Assert.All(browser.Binds.Last().Cues,c=>Assert.DoesNotContain("Voluntary reminder",c.ToString()));
        }
    }
    [Theory]
    [InlineData("denied","available","idle")]
    [InlineData("unsupported","unsupported","idle")]
    [InlineData("granted","available","unknown")]
    [InlineData("granted","available","receipt-capacity")]
    public async Task Permission_failure_and_clock_change_keep_inapp_cues_and_honest_limits(string permission,string capability,string delivery)
    {
        var (f,browser,controller)=await Setup();using(f)using(controller)
        {
            await controller.SetInAppAsync(true);browser.Emit(browser.State with{Permission=permission,Capability=capability,Delivery=delivery,ClockChanged=true});var cut=Render<TodoReminderPanel>();Assert.Single(cut.FindAll("[data-testid='reminder-cue']"));Assert.Contains("Device clock changed",cut.Markup);Assert.Contains("background delivery are unavailable",cut.Markup);if(permission!="granted")Assert.True(cut.Find("[data-testid='reminder-browser']").HasAttribute("disabled"));Save("reminder-"+delivery+"-"+permission,cut.Markup,"explicit-reminder-limit-"+delivery+"-"+permission,nameof(Permission_failure_and_clock_change_keep_inapp_cues_and_honest_limits));
        }
    }
    [Fact]
    public async Task Account_switch_discards_cues_and_late_permission_callback_and_old_binding()
    {
        var (f,browser,controller)=await Setup();using(f)using(controller)
        {
            await controller.SetInAppAsync(true);var cut=Render<TodoReminderPanel>();var old=browser.State;browser.PermissionGate=new();var pending=controller.EnableBrowserAsync();f.Task.Session.SetState(f.Task.Session.State with{UserId="00000000-0000-4000-8000-000000000099"});browser.PermissionGate.SetResult(old with{Browser=true,Permission="granted"});await pending;browser.Emit(old);cut.WaitForAssertion(()=>Assert.Empty(cut.FindAll("[data-testid='reminder-cue']")));Assert.Empty(controller.Cues);Assert.False(controller.Ready);Assert.Null(browser.Binds.Last().Context);Assert.Empty(f.Task.Client.AcceptedIds);Save("reminder-unavailable",cut.Markup,"account-bound-source-unavailable",nameof(Account_switch_discards_cues_and_late_permission_callback_and_old_binding));
        }
    }
}
