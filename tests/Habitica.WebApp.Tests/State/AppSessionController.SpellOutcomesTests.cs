using System.Net;
using Habitica.Api;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Sync;
using Habitica.Domain.User;
using Habitica.WebApp.State;
namespace Habitica.WebApp.Tests.State;

public sealed partial class AppSessionControllerTests
{
    private sealed class SpellClock(DateTimeOffset now):TimeProvider {public override DateTimeOffset GetUtcNow()=>now;}
    private static async Task<(AppSessionController Controller,FakeHabiticaSyncClient Client,FakeDiagnosticsLogStore Logs,DateTimeOffset Now)> SpellOutcomeSetup(bool gear=false)
    {
        var now=DateTimeOffset.UtcNow;var user=CreateUserSnapshot() with {RetrievedAtUtc=now,Mana=500m};
        if(gear)user=user with {Equipment=new(new("head_old","armor_old",null,null,null),new(null,null,null,null,null)),Inventory=new(0,0,0,0,0,0,["head_new","armor_new"])};
        var logs=new FakeDiagnosticsLogStore([]);var client=new FakeHabiticaSyncClient(user,CreateTaskSnapshot(),CreatePartySnapshot());var controller=CreateController(logs,client,timeProvider:new SpellClock(now));await controller.SignInAsync(new(){UserId="synthetic-user",ApiToken="synthetic-secret",PersistLocally=false});return(controller,client,logs,now);
    }
    [Fact]
    public async Task Bulk_spell_known_rate_refusal_preserves_17_of_40_acknowledgements_and_cooldown_without_replay()
    {
        var(c,api,logs,now)=await SpellOutcomeSetup();api.CastAction=(n,_)=>n==18?Task.FromException(new HabiticaApiException(HttpStatusCode.TooManyRequests,"Private text must not escape",new(TimeSpan.FromSeconds(90),30,0,now.AddSeconds(60)))):Task.CompletedTask;
        var result=await c.CastSpellAsync(new("fireball","todo-1",40));var outcome=result.CastOutcome!;Assert.False(result.Succeeded);Assert.Equal(17,outcome.Acknowledged);Assert.Equal(40,outcome.Requested);Assert.False(outcome.CastOutcomeUnknown);Assert.Equal(SpellCastStage.Casting,outcome.Stage);Assert.Equal(now.AddSeconds(90),outcome.RetryAtUtc);Assert.Equal(18,api.CastCalls.Count);Assert.DoesNotContain("Private",result.Message);Assert.Equal(outcome,c.State.LastSpellCastOutcome);
        var log=logs.Entries.Last(row=>row.Operation=="spell-cast");Assert.Equal("17",log.Metadata["completed"]);Assert.Equal("429",log.Metadata["httpStatus"]);Assert.Equal("90",log.Metadata["cooldownSeconds"]);Assert.Equal("cast",log.Metadata["stage"]);
    }
    [Fact]
    public async Task All_10_casts_remain_successful_when_account_refresh_fails_and_cached_data_becomes_stale()
    {
        var(c,api,_,_)=await SpellOutcomeSetup();api.UserReadFailure=new HttpRequestException("Synthetic private response");var result=await c.CastSpellAsync(new("fireball","todo-1",10));var o=result.CastOutcome!;
        Assert.True(result.Succeeded);Assert.True(o.AllCastsAcknowledged);Assert.Equal(10,o.Acknowledged);Assert.Equal(SpellFollowupStatus.Failed,o.Refresh);Assert.Equal(SpellCastStage.RefreshUser,o.Stage);Assert.False(o.CastOutcomeUnknown);Assert.Equal(10,api.CastCalls.Count);Assert.Equal(SnapshotFreshnessState.Stale,c.State.UserFreshness);Assert.DoesNotContain("Synthetic private",result.Message);Assert.Null(c.State.ActiveSpellCastProgress);Assert.False(c.State.IsBusy);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Lost_or_cancelled_dispatched_cast_keeps_17_acknowledged_and_one_unknown(bool cancellation)
    {
        var(c,api,_,_)=await SpellOutcomeSetup();api.CastAction=(n,token)=>n==18?Task.FromException(cancellation?new HabiticaMutationCanceledException(true,token):new HabiticaMutationException(true)):Task.CompletedTask;
        var result=await c.CastSpellAsync(new("fireball","todo-1",40));Assert.Equal(17,result.CastOutcome!.Acknowledged);Assert.True(result.CastOutcome.CastOutcomeUnknown);Assert.False(result.Succeeded);Assert.Equal(cancellation,result.CastOutcome.Cancelled);Assert.Equal(18,api.CastCalls.Count);Assert.Contains("never replay",result.Message);
    }
    [Fact]
    public async Task Cancellation_after_17_acknowledged_and_before_next_dispatch_creates_no_unknown_cast()
    {
        var(c,api,_,_)=await SpellOutcomeSetup();using var cancel=new CancellationTokenSource();api.CastAction=(n,_)=>{if(n==17)cancel.Cancel();return Task.CompletedTask;};var result=await c.CastSpellAsync(new("fireball","todo-1",40),cancel.Token);Assert.Equal(17,result.CastOutcome!.Acknowledged);Assert.True(result.CastOutcome.Cancelled);Assert.False(result.CastOutcome.CastOutcomeUnknown);Assert.Equal(17,api.CastCalls.Count);
    }
    [Fact]
    public async Task Partial_auto_equip_refusal_restores_only_acknowledged_slots_and_never_casts()
    {
        var(c,api,_,_)=await SpellOutcomeSetup(true);api.EquipAction=(n,_)=>n==2?Task.FromException(new HabiticaApiException(HttpStatusCode.TooManyRequests,"Refused",new(TimeSpan.FromSeconds(30),30,0,null))):Task.CompletedTask;
        var result=await c.CastSpellAsync(new("fireball","todo-1",1,true,new("head_new","armor_new",null,null,null)));Assert.Empty(api.CastCalls);Assert.Equal(SpellCastStage.AutoEquip,result.CastOutcome!.Stage);Assert.Equal(SpellFollowupStatus.Completed,result.CastOutcome.Cleanup);Assert.Equal(new[]{"head_new","armor_new","head_old"},api.EquipCalls.Select(row=>row.Key));Assert.False(result.CastOutcome.EquipmentOutcomeUnknown);
    }
    [Fact]
    public async Task Restore_rate_refusal_does_not_retry_toggle_or_lose_completed_casts()
    {
        var(c,api,_,_)=await SpellOutcomeSetup(true);api.EquipAction=(n,_)=>n==2?Task.FromException(new HabiticaApiException(HttpStatusCode.TooManyRequests,"Refused",new(TimeSpan.FromSeconds(30),30,0,null))):Task.CompletedTask;
        var result=await c.CastSpellAsync(new("fireball","todo-1",1,true,new("head_new","armor_old",null,null,null)));Assert.True(result.Succeeded);Assert.True(result.CastOutcome!.AllCastsAcknowledged);Assert.Equal(SpellCastStage.RestoreGear,result.CastOutcome.Stage);Assert.Equal(SpellFollowupStatus.Failed,result.CastOutcome.Cleanup);Assert.Equal(2,api.EquipCalls.Count);Assert.Single(api.CastCalls);
    }
    [Fact]
    public async Task Unknown_auto_equip_never_issues_an_automatic_restore_or_cast()
    {
        var(c,api,_,_)=await SpellOutcomeSetup(true);api.EquipAction=(_,_)=>Task.FromException(new HabiticaMutationException(true));var result=await c.CastSpellAsync(new("fireball","todo-1",1,true,new("head_new","armor_old",null,null,null)));Assert.Single(api.EquipCalls);Assert.Empty(api.CastCalls);Assert.True(result.CastOutcome!.EquipmentOutcomeUnknown);Assert.Equal(SpellFollowupStatus.Unknown,result.CastOutcome.Cleanup);
    }
}
