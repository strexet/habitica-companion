using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Habitica.Domain.User;
using Habitica.Storage;
using Habitica.WebApp.State;
using Microsoft.JSInterop;
namespace Habitica.WebApp.Tests.State;

public sealed partial class AppSessionControllerTests
{
    private sealed class RecoveryRuntime : IJSRuntime, IJSObjectReference
    {
        public Dictionary<string,string> Rows {get;} = new();
        private bool _locked;
        public ValueTask<T> InvokeAsync<T>(string name, object?[]? args) => InvokeAsync<T>(name,default,args);
        public ValueTask<T> InvokeAsync<T>(string name,CancellationToken token,object?[]? args)
        {
            token.ThrowIfCancellationRequested(); object? value=null;
            if(name=="import")value=this;
            else if(name=="acquireGearRecovery"){value=!_locked;_locked=true;}
            else if(name=="releaseGearRecovery")_locked=false;
            else if(name=="getGearRecovery")value=Rows.GetValueOrDefault((string)args![0]!);
            else if(name=="commitGearRecovery")
            {
                var scope=(string)args![0]!;var previous=Rows.GetValueOrDefault(scope);
                var revision=previous is null?0:JsonSerializer.Deserialize<SpellGearRecovery>(previous,new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Revision;
                value=revision==(long)args[1]!;
                if((bool)value)Rows[scope]=(string)args[2]!;
            }
            return ValueTask.FromResult(value is null?default!:(T)value);
        }
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private static async Task<(AppSessionController Controller, FakeHabiticaSyncClient Api, RecoveryRuntime Runtime)> RecoverySetup(string current="head_later")
    {
        var runtime=new RecoveryRuntime();var scope=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("synthetic-user")));
        runtime.Rows[scope]=JsonSerializer.Serialize(new SpellGearRecovery(Guid.NewGuid(),1,
            [new("Head","head_old","head_new",GearRecoverySlotStatus.InFlight)]),new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var user=CreateUserSnapshot() with {Equipment=new(new(current,null,null,null,null),new(null,null,null,null,null))};
        var api=new FakeHabiticaSyncClient(user,CreateTaskSnapshot(),CreatePartySnapshot());
        var controller=CreateController(new FakeDiagnosticsLogStore([]),api,spellGearRecoveryStore:new(runtime));
        await controller.SignInAsync(new(){UserId="synthetic-user",ApiToken="synthetic-secret",PersistLocally=false});
        return(controller,api,runtime);
    }
    [Fact]
    public async Task Recovery_preserves_later_user_selection_and_never_casts()
    {
        var(c,api,_)=await RecoverySetup();var reviewed=await c.ReviewSpellGearRecoveryAsync();var result=await c.ReviewSpellGearRecoveryAsync(restore:true,expectedOperationId:reviewed.Journal!.OperationId);
        Assert.NotNull(result.Journal);Assert.Contains("later selection",result.Message);Assert.Empty(api.EquipCalls);Assert.Empty(api.CastCalls);
    }
    [Fact]
    public async Task Explicit_supersession_clears_only_saved_work_without_equipping()
    {
        var(c,api,_)=await RecoverySetup();var reviewed=await c.ReviewSpellGearRecoveryAsync();var result=await c.ReviewSpellGearRecoveryAsync(keepCurrent:true,expectedOperationId:reviewed.Journal!.OperationId);
        Assert.Null(result.Journal);Assert.Null((await c.ReviewSpellGearRecoveryAsync()).Journal);Assert.Empty(api.EquipCalls);Assert.Empty(api.CastCalls);
    }
    [Fact]
    public async Task Already_restored_slot_is_verified_without_toggling_again()
    {
        var(c,api,_)=await RecoverySetup("head_old");var reviewed=await c.ReviewSpellGearRecoveryAsync();var result=await c.ReviewSpellGearRecoveryAsync(restore:true,expectedOperationId:reviewed.Journal!.OperationId);
        Assert.Null(result.Journal);Assert.Empty(api.EquipCalls);Assert.Empty(api.CastCalls);
    }
    [Fact]
    public async Task New_account_cannot_read_previous_accounts_saved_work()
    {
        var(c,api,_)=await RecoverySetup();Assert.NotNull((await c.ReviewSpellGearRecoveryAsync()).Journal);
        await c.SignInAsync(new(){UserId="synthetic-other",ApiToken="synthetic-other-secret",PersistLocally=false});
        Assert.Null((await c.ReviewSpellGearRecoveryAsync()).Journal);Assert.Empty(api.EquipCalls);
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Journal_survives_lost_equip_acknowledgement_and_never_replays_cast(bool loseResponse, bool cancelAfterCast)
    {
        var runtime=new RecoveryRuntime();
        var user=CreateUserSnapshot() with {RetrievedAtUtc=DateTimeOffset.UtcNow,Mana=500m,Equipment=new(new("head_old",null,null,null,null),new(null,null,null,null,null)),Inventory=new(0,0,0,0,0,0,["head_new"])};
        var api=new FakeHabiticaSyncClient(user,CreateTaskSnapshot(),CreatePartySnapshot());
        api.EquipAction=(number,_)=>{
            api.UserReadOverride=user with {Equipment=new(new(number==1?"head_new":"head_old",null,null,null,null),new(null,null,null,null,null))};
            return number==1&&loseResponse?Task.FromException(new Habitica.Api.HabiticaMutationException(true)):Task.CompletedTask;
        };
        var c=CreateController(new FakeDiagnosticsLogStore([]),api,spellGearRecoveryStore:new(runtime));
        await c.SignInAsync(new(){UserId="synthetic-user",ApiToken="synthetic-secret",PersistLocally=false});
        using var cancel=new CancellationTokenSource();
        if(cancelAfterCast)api.CastAction=(_,_)=>{cancel.Cancel();return Task.CompletedTask;};
        var outcome=await c.CastSpellAsync(new("fireball","todo-1",1,true,new("head_new",null,null,null,null)),cancel.Token);
        var review=await c.ReviewSpellGearRecoveryAsync();
        if(loseResponse)
        {
            Assert.Empty(api.CastCalls);Assert.Single(api.EquipCalls);Assert.NotNull(review.Journal);
            Assert.Equal(GearRecoverySlotStatus.InFlight,review.Journal!.Slots[0].Status);
            var recovered=await c.ReviewSpellGearRecoveryAsync(restore:true,expectedOperationId:review.Journal.OperationId);
            Assert.Null(recovered.Journal);Assert.Equal(2,api.EquipCalls.Count);Assert.Empty(api.CastCalls);
        }
        else
        {
            Assert.True(outcome.Succeeded,outcome.Message);Assert.Single(api.CastCalls);Assert.Equal(2,api.EquipCalls.Count);Assert.Null(review.Journal);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_manual_restore_retains_exact_slot_and_does_not_retry(bool quota)
    {
        var(c,api,_)=await RecoverySetup("head_new");var review=await c.ReviewSpellGearRecoveryAsync();
        api.EquipAction=(_,_)=>Task.FromException(quota
            ?new Habitica.Api.HabiticaApiException(System.Net.HttpStatusCode.TooManyRequests,"synthetic refusal")
            :new Habitica.Api.HabiticaMutationException(true));
        var result=await c.ReviewSpellGearRecoveryAsync(restore:true,expectedOperationId:review.Journal!.OperationId);
        Assert.Contains("Head",result.Message);Assert.NotNull(result.Journal);Assert.Single(api.EquipCalls);Assert.Empty(api.CastCalls);
        Assert.NotNull((await c.ReviewSpellGearRecoveryAsync()).Journal);
    }
    [Fact]
    public async Task Stale_review_identity_cannot_restore_or_supersede()
    {
        var(c,api,_)=await RecoverySetup("head_new");var result=await c.ReviewSpellGearRecoveryAsync(restore:true,expectedOperationId:Guid.NewGuid());
        Assert.Contains("changed",result.Message);Assert.Empty(api.EquipCalls);Assert.NotNull(result.Journal);
    }
    [Fact]
    public async Task Cancelled_manual_restore_retains_work_and_releases_execution_state()
    {
        var(c,api,_)=await RecoverySetup("head_new");var review=await c.ReviewSpellGearRecoveryAsync();using var cancel=new CancellationTokenSource();
        api.EquipAction=(_,token)=>{cancel.Cancel();return Task.FromCanceled(token);};
        var result=await c.ReviewSpellGearRecoveryAsync(restore:true,expectedOperationId:review.Journal!.OperationId,token:cancel.Token);
        Assert.Contains("stopped",result.Message);Assert.NotNull(result.Journal);Assert.False(c.State.IsBusy);Assert.Single(api.EquipCalls);Assert.Empty(api.CastCalls);
    }

    [Fact]
    public async Task Recovery_deadline_retains_dispatched_slot_without_retry()
    {
        var(c,api,_)=await RecoverySetup("head_new");var review=await c.ReviewSpellGearRecoveryAsync();
        api.EquipAction=(_,token)=>Task.Delay(Timeout.Infinite,token);
        var result=await c.ReviewSpellGearRecoveryAsync(restore:true,expectedOperationId:review.Journal!.OperationId);
        Assert.Contains("Head: recovery stopped",result.Message);Assert.NotNull(result.Journal);Assert.Single(api.EquipCalls);Assert.False(c.State.IsBusy);
        Assert.NotNull((await c.ReviewSpellGearRecoveryAsync()).Journal);
    }
    [Fact]
    public async Task Duplicate_recovery_request_cannot_dispatch_second_toggle()
    {
        var(c,api,_)=await RecoverySetup("head_new");var review=await c.ReviewSpellGearRecoveryAsync();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.EquipAction=async(_,token)=>{entered.SetResult();await release.Task.WaitAsync(token);api.UserReadOverride=api.UserSnapshot with {Equipment=new(new("head_old",null,null,null,null),new(null,null,null,null,null))};};
        var first=c.ReviewSpellGearRecoveryAsync(restore:true,expectedOperationId:review.Journal!.OperationId);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var duplicate=await c.ReviewSpellGearRecoveryAsync(restore:true,expectedOperationId:review.Journal.OperationId);
        Assert.Contains("finish the current action",duplicate.Message);Assert.Single(api.EquipCalls);
        release.SetResult();Assert.Null((await first).Journal);Assert.Empty(api.CastCalls);
    }

}
