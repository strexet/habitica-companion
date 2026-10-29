using Habitica.Application.Notifications;
using Habitica.WebApp.State;
namespace Habitica.WebApp.Tests.Workspace;
public sealed class PushReminderControllerTests
{
    [Fact] public async Task Read_never_prompts_and_consent_precedes_device_gesture_and_secret_upload()
    {
        var f=new PushReminderFixture();await f.InitializeAsync();Assert.Equal(0,f.BrowserApi.Permissions);
        await f.Devices.RegisterAsync(false);Assert.Empty(f.Api.Subscriptions);await f.Devices.SetConsentAsync(true,false);Assert.Empty(f.Api.Consents);
        await f.Devices.RegisterAsync(true);Assert.Equal(1,f.BrowserApi.Permissions);Assert.Single(f.Api.Subscriptions);Assert.NotNull(f.Devices.Binding);Assert.Null(f.Devices.Pending);
        var json=System.Text.Json.JsonSerializer.Serialize(f.Local.Device);Assert.DoesNotContain("endpoint",json);Assert.DoesNotContain("p256dh",json);Assert.DoesNotContain(f.BrowserApi.Result.Subscription!.Keys.Auth,json);
    }
    [Theory][InlineData("denied")][InlineData("installation-required")][InlineData("unsupported")]
    public async Task Prerequisite_failures_do_not_upload_device_material(string state)
    {var f=new PushReminderFixture();await f.InitializeAsync();f.BrowserApi.Result=new(state);await f.Devices.RegisterAsync(true);Assert.Empty(f.Api.Subscriptions);Assert.Null(f.Devices.Pending);Assert.Contains(state,f.Devices.Message);}
    [Fact] public async Task Lost_device_ack_recovers_original_without_new_permission_and_retry_retains_exact_ids()
    {
        var f=new PushReminderFixture();await f.InitializeAsync();f.Api.LosePush=true;await f.Devices.RegisterAsync(true);var pending=f.Devices.Pending!;
        await f.Devices.RegisterAsync(true);Assert.Single(f.Api.Subscriptions);
        await f.Devices.RetryOriginalAsync(true);Assert.Equal(2,f.Api.Subscriptions.Count);Assert.Equal(pending.RequestId,f.Api.Subscriptions[1].RequestId);Assert.Equal(pending.DeviceId,f.Api.Subscriptions[1].DeviceId);
        await f.Devices.RecoverAsync();Assert.Null(f.Devices.Pending);Assert.NotNull(f.Devices.Binding);Assert.Equal(2,f.BrowserApi.Permissions);
    }
    [Fact] public async Task Remote_revoke_and_matching_local_removal_are_separate_explicit_actions()
    {
        var f=new PushReminderFixture();await f.InitializeAsync();await f.Devices.RegisterAsync(true);f.BrowserApi.Current=new("available","granted",true,f.Devices.Binding!.Fingerprint);await f.Devices.RefreshAsync();
        await f.Devices.RevokeDeviceAsync(f.Api.Device.DeviceId);Assert.Single(f.Api.Unsubscribes);Assert.Equal(0,f.BrowserApi.Removals);Assert.True(f.Devices.CanRemoveLocal);
        await f.Devices.RemoveLocalAsync();Assert.Equal(1,f.BrowserApi.Removals);Assert.False(f.Devices.Current!.Exists);
    }
    [Fact] public async Task Local_conflict_blocks_auth_upload_and_account_switch_discards_late_browser_result()
    {
        var f=new PushReminderFixture();await f.InitializeAsync();f.Local.FailWrites=true;await f.Devices.RegisterAsync(true);Assert.Empty(f.Api.Subscriptions);Assert.False(f.Devices.StorageAvailable);
        f.Local.FailWrites=false;await f.Devices.RefreshAsync();f.BrowserApi.SubscribeGate=new(TaskCreationOptions.RunContinuationsAsynchronously);var operation=f.Devices.RegisterAsync(true);Assert.Equal(2,f.BrowserApi.Permissions);
        f.Task.Session.SetState(f.Task.Session.State with{UserId=Guid.NewGuid().ToString()});f.BrowserApi.SubscribeGate.SetResult(f.BrowserApi.Result);await operation;Assert.Null(f.Devices.Status);Assert.Null(f.Devices.Binding);Assert.Empty(f.Api.Subscriptions);
    }
    [Fact] public async Task Schedule_acceptance_loss_blocks_replacement_then_recovers_same_receipt_after_reload()
    {
        var f=new PushReminderFixture();await f.InitializeAsync();await f.Reminders.PrepareAsync(f.Intent);var original=f.Reminders.Preview!;Assert.Equal(0,f.Api.Acceptances);Assert.NotNull(f.Reminders.Pending);
        f.Api.LoseAccept=true;await f.Reminders.AcceptAsync();Assert.True(f.Reminders.Pending!.AcceptanceAttempted);Assert.Null(f.Reminders.Preview);await f.Reminders.DiscardUnsubmittedAsync();Assert.NotNull(f.Reminders.Pending);
        var resumed=new TaskReminderController(f.Task.Session,f.Todo,f.Api,f.Local);await resumed.LoadAsync(f.Intent.TaskId);await resumed.RecoverAsync();Assert.Null(resumed.Pending);Assert.Equal(original.CommandId,resumed.Receipt!.CommandId);Assert.Equal(1,f.Api.Acceptances);Assert.False(resumed.Receipt.Dispatched);Assert.Null(resumed.Receipt.Shown);
    }
    [Fact] public async Task Unknown_prepare_reuses_exact_intent_and_can_discard_only_before_acceptance()
    {
        var f=new PushReminderFixture();await f.InitializeAsync();f.Api.LosePrepare=true;await f.Reminders.PrepareAsync(f.Intent);var first=f.Reminders.Pending!;
        f.Api.LosePrepare=false;await f.Reminders.RetryPreviewAsync();Assert.Equal(first.Preparation,f.Api.Inputs[1]);Assert.NotNull(f.Reminders.Preview);await f.Reminders.DiscardUnsubmittedAsync();Assert.Null(f.Reminders.Pending);Assert.Equal(0,f.Api.Acceptances);
    }
    [Fact] public async Task Task_or_account_context_change_prevents_stale_preview_acceptance()
    {
        var f=new PushReminderFixture();await f.InitializeAsync();await f.Reminders.PrepareAsync(f.Intent);f.Task.Client.State=f.Task.Client.State with{Document=f.Task.Client.State.Document with{Revision=3}};await f.Todo.LoadSelectedAsync();Assert.False(f.Reminders.CanAccept);await f.Reminders.AcceptAsync();Assert.Equal(0,f.Api.Acceptances);
        f.Task.Session.SetState(f.Task.Session.State with{UserId=Guid.NewGuid().ToString()});Assert.Null(f.Reminders.State);Assert.Null(f.Reminders.Pending);Assert.Null(f.Reminders.Preview);
    }
    [Fact] public async Task Late_schedule_load_or_acceptance_never_reappears_after_signout()
    {
        var f=new PushReminderFixture();await f.InitializeAsync();await f.Reminders.PrepareAsync(f.Intent);f.Api.Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);f.Api.Gate=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending=f.Reminders.AcceptAsync();await f.Api.Entered.Task;f.Task.Session.SetState(f.Task.Session.State with{IsAuthenticated=false});f.Api.Gate.SetResult();await pending;Assert.Null(f.Reminders.State);Assert.Null(f.Reminders.Receipt);Assert.Null(f.Reminders.Preview);
    }
    [Fact] public async Task Cancellation_remains_possible_when_sender_is_disabled_and_never_changes_task_cue()
    {
        var f=new PushReminderFixture();await f.InitializeAsync();var cue=f.Intent.ReviewAfterUtc;f.Api.Read=f.Api.Read! with{Available=false};await f.Reminders.LoadAsync(f.Intent.TaskId);Assert.False(f.Reminders.CanPrepare);Assert.True(f.Reminders.CanChange);
        await f.Reminders.PrepareAsync(new("cancel",Guid.NewGuid(),4,f.Intent.TaskId,null,"UTC",null,[]));await f.Reminders.AcceptAsync();Assert.Equal("cancelled",f.Reminders.Receipt!.State);Assert.Equal(cue,f.Task.Client.State.Document.Tasks[0].ReviewAfterUtc);Assert.False(f.Reminders.Receipt.Scheduled);
    }
}
